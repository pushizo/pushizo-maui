using System.Diagnostics.CodeAnalysis;
using Android.Runtime;
using HeroSignalSdk.Maui.Internal;
using Kotlin.Jvm.Functions;
using HsNative = global::HeroSignalSdk.Native.Android;

namespace HeroSignalSdk.Maui.Platforms;

internal static class KotlinCallbackConstants
{
    /// <summary><c>JavaCast&lt;T&gt;</c> eş nesne oluştururken kurucuları kullanır (kırpma için korunur).</summary>
    public const DynamicallyAccessedMemberTypes JavaPeerConstructors =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
}

/// <summary>Kotlin <c>(T) -&gt; Unit</c> dinleyicisi; işleyici hatası yerel SDK'ya sızmaz.</summary>
internal sealed class KotlinCallback<[DynamicallyAccessedMembers(KotlinCallbackConstants.JavaPeerConstructors)] T> : Java.Lang.Object, IFunction1 where T : Java.Lang.Object
{
    private readonly Action<T> _action;

    public KotlinCallback(Action<T> action) => _action = action;

    public Java.Lang.Object? Invoke(Java.Lang.Object? p0)
    {
        if (p0 is not null)
        {
            try
            {
                _action(p0 as T ?? p0.JavaCast<T>());
            }
            catch (Exception e)
            {
                SdkLog.Write(LogLevel.Error, $"{typeof(T).Name} dinleyicisi hata verdi", e);
            }
        }
        return Kotlin.Unit.Instance;
    }
}

/// <summary>In-app yaşam döngüsü dinleyicisi (yerel SDK ana iş parçacığında çağırır).</summary>
internal sealed class InAppLifecycleListener : Java.Lang.Object, HsNative.IHsInAppLifecycleListener
{
    private readonly Action<InAppLifecyclePhase, InAppMessage> _dispatch;

    public InAppLifecycleListener(Action<InAppLifecyclePhase, InAppMessage> dispatch) => _dispatch = dispatch;

    public void OnWillDisplay(HsNative.HsInAppLifecycleEvent e) => Dispatch(InAppLifecyclePhase.WillDisplay, e);
    public void OnDidDisplay(HsNative.HsInAppLifecycleEvent e) => Dispatch(InAppLifecyclePhase.DidDisplay, e);
    public void OnWillDismiss(HsNative.HsInAppLifecycleEvent e) => Dispatch(InAppLifecyclePhase.WillDismiss, e);
    public void OnDidDismiss(HsNative.HsInAppLifecycleEvent e) => Dispatch(InAppLifecyclePhase.DidDismiss, e);

    private void Dispatch(InAppLifecyclePhase phase, HsNative.HsInAppLifecycleEvent e)
    {
        try
        {
            _dispatch(phase, new InAppMessage(e.Message.Id, e.Message.ToJson()));
        }
        catch (Exception ex)
        {
            SdkLog.Write(LogLevel.Error, "in-app yaşam döngüsü olayı iletilemedi", ex);
        }
    }
}

/// <summary>
/// <c>HsCallback&lt;T&gt;</c> → <see cref="Task{TResult}"/>. Kotlin <c>Result</c> JVM'de kutusuz taşınır: başarıda değerin
/// kendisi, hatada <c>kotlin.Result$Failure</c> (alan: <c>exception</c>). Geri çağrı arka plan iş parçacığındadır.
/// </summary>
internal sealed class InboxCallback<T> : Java.Lang.Object, HsNative.IHsCallback
{
    private readonly Func<Java.Lang.Object?, T> _map;
    private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public InboxCallback(Func<Java.Lang.Object?, T> map) => _map = map;

    public Task<T> Task => _tcs.Task;

    public void OnResult(Java.Lang.Object result)
    {
        try
        {
            var value = KotlinResult.Unwrap(result, out var failure);
            if (failure is not null) _tcs.TrySetException(AndroidMapper.ToException(failure));
            else _tcs.TrySetResult(_map(value));
        }
        catch (Exception e)
        {
            _tcs.TrySetException(new HeroSignalException(HeroSignalException.InvalidResponse, "Yerel SDK yanıtı çözümlenemedi.", e));
        }
    }
}

internal static class KotlinResult
{
    private const string FailureClass = "kotlin.Result$Failure";
    private const string BoxClass = "kotlin.Result";

    /// <summary>Başarı değerini döndürür; hata varsa <paramref name="failure"/> doldurulur.</summary>
    public static Java.Lang.Object? Unwrap(Java.Lang.Object? result, out Java.Lang.Throwable? failure)
    {
        failure = null;
        if (result is null) return null;
        var className = result.Class.Name;
        if (className == BoxClass)
        {
            // Kutulu Result (genel tür konumunda): asıl değer "value" alanındadır.
            var field = result.Class.GetDeclaredField("value");
            field.Accessible = true;
            return Unwrap(field.Get(result), out failure);
        }
        if (className == FailureClass)
        {
            var field = result.Class.GetDeclaredField("exception");
            field.Accessible = true;
            var raw = field.Get(result);
            failure = raw?.JavaCast<Java.Lang.Throwable>() ?? new Java.Lang.IllegalStateException("Bilinmeyen yerel hata");
            return null;
        }
        return result;
    }
}

/// <summary>Yerel modellerin ortak modellere dönüşümü.</summary>
internal static class AndroidMapper
{
    public static T Cast<[DynamicallyAccessedMembers(KotlinCallbackConstants.JavaPeerConstructors)] T>(Java.Lang.Object? value) where T : Java.Lang.Object =>
        value as T ?? value?.JavaCast<T>()
        ?? throw new HeroSignalException(HeroSignalException.InvalidResponse, $"Yerel SDK boş {typeof(T).Name} döndürdü.");

    public static int ToInt(Java.Lang.Object? value) => value switch
    {
        Java.Lang.Integer i => i.IntValue(),
        Java.Lang.Number n => n.IntValue(),
        null => 0,
        _ => value.JavaCast<Java.Lang.Number>().IntValue(),
    };

    public static HeroSignalNotification Map(HsNative.HsNotification n) => new()
    {
        Title = n.Title,
        Body = n.Body,
        ImageUrl = n.ImageUrl,
        Url = n.Url,
        DeliveryId = n.DeliveryId,
        MessageId = n.MessageId,
        NotificationId = n.AndroidNotificationId?.IntValue().ToString(System.Globalization.CultureInfo.InvariantCulture),
        Group = n.GroupKey,
        Actions = n.Actions.Select(a => new NotificationAction(a.Id, a.Title, a.Url, a.Icon)).ToList(),
        Data = Copy(n.Data),
    };

    public static PushSubscriptionState Map(HsNative.HsPushSubscriptionState? s) =>
        s is null ? PushSubscriptionState.Empty : new PushSubscriptionState(s.Id, s.Token, s.OptedIn, s.Permission);

    public static InboxItem Map(HsNative.HsInboxItem i) => new()
    {
        Id = i.Id,
        MessageId = i.MessageId,
        Title = i.Title,
        Body = i.Body,
        Url = i.Url,
        ImageUrl = i.ImageUrl,
        Data = Copy(i.Data),
        Read = i.Read,
        ReadAt = Millis(i.ReadAt),
        ArchivedAt = Millis(i.ArchivedAt),
        ExpiresAt = Millis(i.ExpiresAt),
        CreatedAt = Millis(i.CreatedAt),
    };

    public static InboxPage Map(HsNative.HsInboxPage p) =>
        new(p.Items.Select(Map).ToList(), p.UnreadCount, p.NextCursor, p.HasMore);

    public static IReadOnlyDictionary<string, string> Copy(IDictionary<string, string>? map) =>
        map is null ? new Dictionary<string, string>() : new Dictionary<string, string>(map, StringComparer.Ordinal);

    private static DateTimeOffset? Millis(Java.Lang.Long? ms) => ms is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(ms.LongValue());

    /// <summary>Yerel hata → <see cref="HeroSignalException"/> (sunucu kodu ve HTTP durumu korunur).</summary>
    public static Exception ToException(Java.Lang.Throwable failure)
    {
        // Yönetilen eş türü her zaman en türetilmiş tür olmayabilir; Java sınıf adı esas alınır.
        var className = JNIEnv.GetClassNameFromInstance(failure.Handle);
        if (failure is HsNative.HsApiException || className == "com/herosignal/sdk/HsApiException")
        {
            var api = failure as HsNative.HsApiException ?? failure.JavaCast<HsNative.HsApiException>()!;
            return new HeroSignalException(api.Code, api.Message ?? api.Code, failure) { Status = api.Status == 0 ? null : api.Status };
        }
        if (failure is Java.Lang.IllegalStateException || className == "java/lang/IllegalStateException")
            return new HeroSignalException(HeroSignalException.NotReady, failure.Message ?? "SDK hazır değil.", failure);
        if (failure is Java.Lang.IllegalArgumentException || className == "java/lang/IllegalArgumentException")
            return new ArgumentException(failure.Message, failure);
        return new HeroSignalException(HeroSignalException.NativeError, failure.Message ?? className, failure);
    }

    /// <summary>
    /// Kanonik olay özelliği ağacı → Java nesneleri. Yerel SDK <c>JSONObject(map)</c> kullanır: iç içe Map → JSONObject,
    /// Collection → JSONArray; null değer kaybolmasın diye <c>JSONObject.NULL</c> kullanılır.
    /// </summary>
    public static IDictionary<string, Java.Lang.Object> ToJavaMap(Dictionary<string, object?> properties)
    {
        var map = new JavaDictionary<string, Java.Lang.Object>();
        foreach (var (key, value) in properties) map.Add(key, ToJava(value));
        return map;
    }

    private static Java.Lang.Object ToJava(object? value) => value switch
    {
        null => Org.Json.JSONObject.Null!,
        string s => new Java.Lang.String(s),
        bool b => Java.Lang.Boolean.ValueOf(b)!,
        long l => Java.Lang.Long.ValueOf(l)!,
        double d => Java.Lang.Double.ValueOf(d)!,
        Dictionary<string, object?> nested => (Java.Lang.Object)ToJavaMap(nested),
        List<object?> items => ToJavaList(items),
        _ => throw new InvalidOperationException("Kanonik olmayan olay özelliği değeri."),
    };

    private static JavaList<Java.Lang.Object> ToJavaList(List<object?> items)
    {
        var list = new JavaList<Java.Lang.Object>();
        foreach (var item in items) list.Add(ToJava(item));
        return list;
    }
}

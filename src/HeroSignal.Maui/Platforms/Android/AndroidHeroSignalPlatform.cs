using Android.Content;
using Android.OS;
using Android.Runtime;
using HeroSignalSdk.Maui.Internal;
using Microsoft.Maui.ApplicationModel;
using HsNative = global::HeroSignalSdk.Native.Android;

namespace HeroSignalSdk.Maui.Platforms;

/// <summary>
/// Android uygulaması: <c>com.herosignal.sdk.HeroSignal</c> (HeroSignal.Maui.Android.Binding). Kuyruk, oturum, token,
/// in-app tetikleri ve teslimat geri bildirimi yerel SDK'dadır. Tıklama, abonelik ve in-app olayları ana iş parçacığına
/// taşınır; ön plan olayı (<see cref="NotificationWillDisplayEventArgs"/>) karar eşzamanlı verilmesi gerektiğinden
/// yerel SDK'nın iş parçacığında çalışır.
/// </summary>
internal sealed class AndroidHeroSignalPlatform : IHeroSignalPlatform
{
    private readonly Handler _main = new(Looper.MainLooper!);
    private IHeroSignalEventSink? _sink;
    private KotlinCallback<HsNative.HsClickEvent>? _clickListener;
    private KotlinCallback<HsNative.HsForegroundEvent>? _foregroundListener;
    private KotlinCallback<HsNative.HsPushSubscriptionState>? _subscriptionObserver;
    private KotlinCallback<HsNative.HsInAppClickEvent>? _inAppClickListener;
    private InAppLifecycleListener? _inAppLifecycleListener;

    public bool IsSupported => true;

    public void Initialize(string appId, HeroSignalOptions options, IHeroSignalEventSink sink)
    {
        _sink = sink;
        SdkLog.Writer = static (level, message) =>
        {
            switch (level)
            {
                case LogLevel.Error: global::Android.Util.Log.Error("HeroSignal.Maui", message); break;
                case LogLevel.Warn: global::Android.Util.Log.Warn("HeroSignal.Maui", message); break;
                case LogLevel.Info: global::Android.Util.Log.Info("HeroSignal.Maui", message); break;
                default: global::Android.Util.Log.Debug("HeroSignal.Maui", message); break;
            }
        };
        var nativeOptions = new HsNative.HeroSignal.Options(
            options.ApiUrl,
            options.RequiresPrivacyConsent,
            ToNative(options.LogLevel),
            options.AndroidDefaultChannelName,
            options.AndroidDefaultChannelId);
        HsNative.HeroSignal.Initialize(global::Android.App.Application.Context, appId, nativeOptions);
        RegisterListeners(sink);
    }

    /// <summary>
    /// Dinleyiciler önce kaldırılıp yeniden eklenir: farklı App ID ile yeniden başlatmada yerel in-app denetleyicisi
    /// yenilense de olaylar kaybolmaz, aynı denetleyicide çoğalmaz. Tıklama dinleyicisi eklenince yerel SDK bekleyen
    /// (uygulamayı açan) tıklamayı hemen iletir.
    /// </summary>
    private void RegisterListeners(IHeroSignalEventSink sink)
    {
        if (_clickListener is not null) HsNative.HeroSignal.Notifications.RemoveClickListener(_clickListener);
        if (_foregroundListener is not null) HsNative.HeroSignal.Notifications.RemoveForegroundLifecycleListener(_foregroundListener);
        if (_subscriptionObserver is not null) HsNative.HeroSignal.User.PushSubscription.RemoveObserver(_subscriptionObserver);
        if (_inAppClickListener is not null) HsNative.HeroSignal.InAppMessages.RemoveClickListener(_inAppClickListener);
        if (_inAppLifecycleListener is not null) HsNative.HeroSignal.InAppMessages.RemoveLifecycleListener(_inAppLifecycleListener);

        _clickListener = new KotlinCallback<HsNative.HsClickEvent>(e =>
        {
            var args = new NotificationClickedEventArgs(AndroidMapper.Map(e.Notification), e.ActionId, e.Url);
            Post(() => sink.OnNotificationClicked(args));
        });
        _foregroundListener = new KotlinCallback<HsNative.HsForegroundEvent>(e =>
            sink.OnNotificationWillDisplay(new NotificationWillDisplayEventArgs(AndroidMapper.Map(e.Notification), e.PreventDefault, e.Display)));
        _subscriptionObserver = new KotlinCallback<HsNative.HsPushSubscriptionState>(s =>
        {
            var state = AndroidMapper.Map(s);
            Post(() => sink.OnPushSubscriptionChanged(state));
        });
        _inAppClickListener = new KotlinCallback<HsNative.HsInAppClickEvent>(e =>
        {
            var args = new InAppMessageClickedEventArgs(e.MessageId, e.ActionId, e.Url);
            Post(() => sink.OnInAppClicked(args));
        });
        _inAppLifecycleListener = new InAppLifecycleListener((phase, message) => Post(() => sink.OnInAppLifecycle(phase, message)));

        HsNative.HeroSignal.Notifications.AddForegroundLifecycleListener(_foregroundListener);
        HsNative.HeroSignal.User.PushSubscription.AddObserver(_subscriptionObserver);
        HsNative.HeroSignal.InAppMessages.AddClickListener(_inAppClickListener);
        HsNative.HeroSignal.InAppMessages.AddLifecycleListener(_inAppLifecycleListener);
        HsNative.HeroSignal.Notifications.AddClickListener(_clickListener);

        // Başlangıç durumu: sonraki değişiklikler gözlemciden gelir.
        var initial = PushSubscription;
        Post(() => sink.OnPushSubscriptionChanged(initial));
    }

    public void SetConsentGiven(bool given) => HsNative.HeroSignal.SetConsentGiven(given);
    public void Login(string externalId, string? identityHash) => HsNative.HeroSignal.Login(externalId, identityHash);
    public void Logout() => HsNative.HeroSignal.Logout();
    public void SetLogLevel(LogLevel level) => HsNative.HeroSignal.Debug.SetLogLevel(ToNative(level));

    // Kullanıcı
    public string? ExternalId => HsNative.HeroSignal.User.Instance!.ExternalId;
    public string? UserId => HsNative.HeroSignal.User.Instance!.Id;
    public void AddTags(IReadOnlyDictionary<string, string> tags) => HsNative.HeroSignal.User.AddTags(new Dictionary<string, string>(tags));
    public void RemoveTags(IReadOnlyList<string> keys) => HsNative.HeroSignal.User.RemoveTags(keys.ToList());
    public IReadOnlyDictionary<string, string> GetTags() => AndroidMapper.Copy(HsNative.HeroSignal.User.Tags);
    public void AddAliases(IReadOnlyDictionary<string, string> aliases) => HsNative.HeroSignal.User.AddAliases(new Dictionary<string, string>(aliases));
    public void RemoveAlias(string label) => HsNative.HeroSignal.User.RemoveAlias(label);
    public void SetLanguage(string languageCode) => HsNative.HeroSignal.User.SetLanguage(languageCode);
    public void AddEmail(string email) => HsNative.HeroSignal.User.AddEmail(email);
    public void RemoveEmail(string email) => HsNative.HeroSignal.User.RemoveEmail(email);
    public void AddSms(string phoneNumber) => HsNative.HeroSignal.User.AddSms(phoneNumber);
    public void RemoveSms(string phoneNumber) => HsNative.HeroSignal.User.RemoveSms(phoneNumber);
    public IReadOnlyList<string> Emails => HsNative.HeroSignal.User.Instance!.Emails.ToList();
    public IReadOnlyList<string> SmsNumbers => HsNative.HeroSignal.User.Instance!.SmsNumbers.ToList();

    // Push aboneliği
    public PushSubscriptionState PushSubscription => AndroidMapper.Map(HsNative.HeroSignal.User.PushSubscription.Instance!.State);
    public void OptIn() => HsNative.HeroSignal.User.PushSubscription.OptIn();
    public void OptOut() => HsNative.HeroSignal.User.PushSubscription.OptOut();

    // Bildirimler
    public bool NotificationPermission => HsNative.HeroSignal.Notifications.Instance!.Permission;

    /// <summary>
    /// Android 13+ POST_NOTIFICATIONS iznini MAUI Essentials ile ister (MauiAppCompatActivity izin sonucunu iletir),
    /// ardından yerel SDK'ya bildirir (abonelik push_capable olarak güncellenir). Eski sürümlerde izin kanal/sistem ayarıdır.
    /// </summary>
    public async Task<bool> RequestPermissionAsync(bool fallbackToSettings)
    {
        if (!NotificationPermission && OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.PostNotifications>()).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                SdkLog.Write(LogLevel.Warn, "bildirim izni istenemedi", e);
            }
        }
        HsNative.HeroSignal.Notifications.OnPermissionResult();
        var granted = NotificationPermission;
        _sink?.OnPermissionChanged(granted);
        if (!granted && fallbackToSettings) Post(OpenNotificationSettings);
        return granted;
    }

    private static void OpenNotificationSettings()
    {
        var context = global::Android.App.Application.Context;
        try
        {
            var intent = OperatingSystem.IsAndroidVersionAtLeast(26)
                ? new Intent(global::Android.Provider.Settings.ActionAppNotificationSettings)
                    .PutExtra(global::Android.Provider.Settings.ExtraAppPackage, context.PackageName)
                : new Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                    global::Android.Net.Uri.FromParts("package", context.PackageName, null));
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            AppInfo.ShowSettingsUI();
        }
    }

    public void ClearAllNotifications() => HsNative.HeroSignal.Notifications.ClearAll();

    /// <summary>Android'de kimlik, sistem bildirim kimliğidir (<see cref="HeroSignalNotification.NotificationId"/>).</summary>
    public void RemoveNotification(string id)
    {
        if (!int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var numeric))
            throw new ArgumentException("Android'de bildirim kimliği sayı olmalı (HeroSignalNotification.NotificationId).", nameof(id));
        HsNative.HeroSignal.Notifications.RemoveNotification(numeric);
    }

    public void RemoveGroupedNotifications(string group) => HsNative.HeroSignal.Notifications.RemoveGroupedNotifications(group);

    // In-app
    public void AddTriggers(IReadOnlyDictionary<string, string> triggers) => HsNative.HeroSignal.InAppMessages.AddTriggers(new Dictionary<string, string>(triggers));
    public void RemoveTriggers(IReadOnlyList<string> keys) => HsNative.HeroSignal.InAppMessages.RemoveTriggers(keys.ToList());
    public void ClearTriggers() => HsNative.HeroSignal.InAppMessages.ClearTriggers();
    public IReadOnlyDictionary<string, string> GetTriggers() => AndroidMapper.Copy(HsNative.HeroSignal.InAppMessages.Triggers);
    public void SetScreen(string? name) => HsNative.HeroSignal.InAppMessages.SetScreen(name);

    public bool InAppPaused
    {
        get => HsNative.HeroSignal.InAppMessages.Paused;
        set => HsNative.HeroSignal.InAppMessages.Paused = value;
    }

    // Bildirim kutusu
    public Task<InboxPage> InboxListAsync(int limit, string? cursor)
    {
        var callback = new InboxCallback<InboxPage>(value => AndroidMapper.Map(AndroidMapper.Cast<HsNative.HsInboxPage>(value)));
        HsNative.HeroSignal.Inbox.List(limit, cursor, callback);
        return callback.Task;
    }

    public Task<InboxItem> InboxMarkReadAsync(string id)
    {
        var callback = new InboxCallback<InboxItem>(value => AndroidMapper.Map(AndroidMapper.Cast<HsNative.HsInboxItem>(value)));
        HsNative.HeroSignal.Inbox.MarkRead(id, callback);
        return callback.Task;
    }

    public Task<int> InboxMarkAllReadAsync()
    {
        var callback = new InboxCallback<int>(AndroidMapper.ToInt);
        HsNative.HeroSignal.Inbox.MarkAllRead(callback);
        return callback.Task;
    }

    public Task InboxArchiveAsync(string id)
    {
        var callback = new InboxCallback<bool>(static _ => true);
        HsNative.HeroSignal.Inbox.Archive(id, callback);
        return callback.Task;
    }

    public Task<int> InboxUnreadCountAsync()
    {
        var callback = new InboxCallback<int>(AndroidMapper.ToInt);
        HsNative.HeroSignal.Inbox.UnreadCount(callback);
        return callback.Task;
    }

    // Oturum
    public void SendEvent(string name, Dictionary<string, object?> properties, double? value) =>
        HsNative.HeroSignal.Session.SendEvent(name, AndroidMapper.ToJavaMap(properties), value is { } v ? Java.Lang.Double.ValueOf(v) : null);

    private void Post(Action action) => _main.Post(() =>
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            SdkLog.Write(LogLevel.Error, "olay iletimi hata verdi", e);
        }
    });

    private static HsNative.HsLog.Level ToNative(LogLevel level) => (level switch
    {
        LogLevel.None => HsNative.HsLog.Level.None,
        LogLevel.Error => HsNative.HsLog.Level.Error,
        LogLevel.Info => HsNative.HsLog.Level.Info,
        LogLevel.Debug => HsNative.HsLog.Level.Debug,
        _ => HsNative.HsLog.Level.Warn,
    })!;
}

/// <summary>Bu hedefin platform uygulaması.</summary>
internal static class PlatformFactory
{
    public static IHeroSignalPlatform Create() => new AndroidHeroSignalPlatform();
}

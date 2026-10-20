using PushizoSdk.Maui.Internal;
using PushizoSdk.Native.Android;

namespace PushizoSdk.Maui.AndroidBridge;

/// <summary>
/// <see cref="INativeBridge"/>'in Android uygulaması: bağlanan Kotlin köprüsü <c>com.pushizo.maui.PushizoMauiBridge</c>
/// (sdk/maui/native/android/bridge) üzerinden yerel <c>com.pushizo.sdk.Pushizo</c>'i çağırır.
/// </summary>
internal sealed class AndroidNativeBridge : INativeBridge
{
    public void Start(string appId, string optionsJson, Action<string, string> onEvent) =>
        PushizoMauiBridge.Start(global::Android.App.Application.Context, appId, optionsJson, new EventListener(onEvent));

    public void SetConsentGiven(bool given) => PushizoMauiBridge.SetConsentGiven(given);

    public string? Login(string externalId, string? identityHash, string? jwt) => PushizoMauiBridge.Login(externalId, identityHash, jwt);

    public string? UpdateUserJwt(string jwt) => PushizoMauiBridge.UpdateUserJwt(jwt);

    public void Logout() => PushizoMauiBridge.Logout();

    public void SetLogLevel(int level) => PushizoMauiBridge.ApplyLogLevel(level);

    public string? GetExternalId() => PushizoMauiBridge.ExternalId();

    public string? GetUserId() => PushizoMauiBridge.UserId();

    public void AddTags(string tagsJson) => PushizoMauiBridge.AddTags(tagsJson);

    public void RemoveTags(string keysJson) => PushizoMauiBridge.RemoveTags(keysJson);

    public string GetTags() => PushizoMauiBridge.Tags();

    public void AddAliases(string aliasesJson) => PushizoMauiBridge.AddAliases(aliasesJson);

    public void RemoveAlias(string label) => PushizoMauiBridge.RemoveAlias(label);

    public void SetLanguage(string languageCode) => PushizoMauiBridge.SetLanguage(languageCode);

    public void AddEmail(string email) => PushizoMauiBridge.AddEmail(email);

    public void RemoveEmail(string email) => PushizoMauiBridge.RemoveEmail(email);

    public void AddSms(string phoneNumber) => PushizoMauiBridge.AddSms(phoneNumber);

    public void RemoveSms(string phoneNumber) => PushizoMauiBridge.RemoveSms(phoneNumber);

    public string GetEmails() => PushizoMauiBridge.Emails();

    public string GetSmsNumbers() => PushizoMauiBridge.SmsNumbers();

    public string GetPushSubscription() => PushizoMauiBridge.PushSubscription();

    public void OptIn() => PushizoMauiBridge.OptIn();

    public void OptOut() => PushizoMauiBridge.OptOut();

    public bool GetPermission() => PushizoMauiBridge.Permission();

    public void RequestPermission(bool fallbackToSettings, Action<bool> completion) =>
        PushizoMauiBridge.RequestPermission(
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity,
            fallbackToSettings,
            new PermissionCallback(completion));

    public void ClearAllNotifications() => PushizoMauiBridge.ClearAllNotifications();

    public void RemoveNotification(string id)
    {
        // Android tepsi kimliği tamsayıdır; iOS biçimindeki metin kimliğin burada karşılığı yoktur.
        if (!int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var androidNotificationId))
            throw new ArgumentException("Android bildirim kimliği tamsayı olmalı (PushizoNotification.AndroidNotificationId).", nameof(id));
        PushizoMauiBridge.RemoveNotification(androidNotificationId);
    }

    public void RemoveGroupedNotifications(string group) => PushizoMauiBridge.RemoveGroupedNotifications(group);

    public void AddTriggers(string triggersJson) => PushizoMauiBridge.AddTriggers(triggersJson);

    public void RemoveTriggers(string keysJson) => PushizoMauiBridge.RemoveTriggers(keysJson);

    public void ClearTriggers() => PushizoMauiBridge.ClearTriggers();

    public string GetTriggers() => PushizoMauiBridge.Triggers();

    public void SetScreen(string? name) => PushizoMauiBridge.SetScreen(name);

    public bool GetInAppPaused() => PushizoMauiBridge.InAppPaused();

    public void SetInAppPaused(bool paused) => PushizoMauiBridge.UpdateInAppPaused(paused);

    public void InboxList(int limit, string? cursor, NativeResult result) =>
        PushizoMauiBridge.InboxList(limit, cursor, new ResultCallback(result));

    public void InboxMarkRead(string id, NativeResult result) =>
        PushizoMauiBridge.InboxMarkRead(id, new ResultCallback(result));

    public void InboxMarkAllRead(NativeResult result) =>
        PushizoMauiBridge.InboxMarkAllRead(new ResultCallback(result));

    public void InboxArchive(string id, NativeResult result) =>
        PushizoMauiBridge.InboxArchive(id, new ResultCallback(result));

    public void InboxUnreadCount(NativeResult result) =>
        PushizoMauiBridge.InboxUnreadCount(new ResultCallback(result));

    public void GetPreferences(NativeResult result) =>
        PushizoMauiBridge.GetPreferences(new ResultCallback(result));

    public void SetPreferences(string preferencesJson, NativeResult result) =>
        PushizoMauiBridge.SetPreferences(preferencesJson, new ResultCallback(result));

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue) =>
        PushizoMauiBridge.SendEvent(name, propertiesJson, value, hasValue);

    /// <summary>Live Activities iOS'a özgüdür; Android'de çağrılar köprüye ulaşmadan <c>PLATFORM_NOT_SUPPORTED</c> olur.</summary>
    public bool SupportsLiveActivities => false;

    public void SetupDefaultLiveActivity(NativeResult result) => LiveActivitiesNotSupported(result);

    public void StartDefaultLiveActivity(string activityId, string attributesJson, string contentStateJson, NativeResult result) =>
        LiveActivitiesNotSupported(result);

    private static void LiveActivitiesNotSupported(NativeResult result) =>
        result(null, PushizoException.PlatformNotSupported, UnsupportedPlatform.LiveActivitiesUnsupportedMessage);

    public bool GetLocationShared() => PushizoMauiBridge.LocationShared();

    public void SetLocationShared(bool shared) => PushizoMauiBridge.UpdateLocationShared(shared);

    /// <summary>
    /// Açıklama diyaloğu ve sistem istemi Kotlin köprüsündedir; sonuç <c>OnRequestPermissionsResult</c> (istek kodu
    /// <c>Pushizo.Location.PERMISSION_REQUEST_CODE</c>) ya da etkinliğin ön plana dönüşüyle bildirilir.
    /// </summary>
    public void RequestLocationPermission(string? rationale, Action<bool> completion) =>
        PushizoMauiBridge.RequestLocationPermission(
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity,
            rationale,
            new PermissionCallback(completion));

    public void ChatCall(string method, string argsJson, NativeResult result) =>
        PushizoMauiBridge.ChatCall(method, argsJson, new ResultCallback(result));

    public void ForegroundPreventDefault(string eventId) => PushizoMauiBridge.ForegroundPreventDefault(eventId);

    public void ForegroundDisplay(string eventId) => PushizoMauiBridge.ForegroundDisplay(eventId);

    /// <summary>Yerel olay dinleyicisi (Java arayüzünü uygulayan .NET nesnesi; köprü güçlü referans tutar).</summary>
    private sealed class EventListener : Java.Lang.Object, IPushizoMauiListener
    {
        private readonly Action<string, string> _onEvent;

        public EventListener(Action<string, string> onEvent) => _onEvent = onEvent;

        public void OnEvent(string type, string json) => _onEvent(type, json);
    }

    private sealed class ResultCallback : Java.Lang.Object, IPushizoMauiResult
    {
        private readonly NativeResult _result;

        public ResultCallback(NativeResult result) => _result = result;

        public void OnResult(string? json, string? errorCode, string? errorMessage) => _result(json, errorCode, errorMessage);
    }

    private sealed class PermissionCallback : Java.Lang.Object, IPushizoMauiPermissionCallback
    {
        private readonly Action<bool> _completion;

        public PermissionCallback(Action<bool> completion) => _completion = completion;

        public void OnResult(bool granted) => _completion(granted);
    }
}

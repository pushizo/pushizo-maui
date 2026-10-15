using HeroSignalSdk.Maui.Internal;
using HeroSignalSdk.Native.Android;

namespace HeroSignalSdk.Maui.AndroidBridge;

/// <summary>
/// <see cref="INativeBridge"/>'in Android uygulaması: bağlanan Kotlin köprüsü <c>com.herosignal.maui.HeroSignalMauiBridge</c>
/// (sdk/maui/native/android/bridge) üzerinden yerel <c>com.herosignal.sdk.HeroSignal</c>'i çağırır.
/// </summary>
internal sealed class AndroidNativeBridge : INativeBridge
{
    public void Start(string appId, string optionsJson, Action<string, string> onEvent) =>
        HeroSignalMauiBridge.Start(global::Android.App.Application.Context, appId, optionsJson, new EventListener(onEvent));

    public void SetConsentGiven(bool given) => HeroSignalMauiBridge.SetConsentGiven(given);

    public string? Login(string externalId, string? identityHash, string? jwt) => HeroSignalMauiBridge.Login(externalId, identityHash, jwt);

    public string? UpdateUserJwt(string jwt) => HeroSignalMauiBridge.UpdateUserJwt(jwt);

    public void Logout() => HeroSignalMauiBridge.Logout();

    public void SetLogLevel(int level) => HeroSignalMauiBridge.ApplyLogLevel(level);

    public string? GetExternalId() => HeroSignalMauiBridge.ExternalId();

    public string? GetUserId() => HeroSignalMauiBridge.UserId();

    public void AddTags(string tagsJson) => HeroSignalMauiBridge.AddTags(tagsJson);

    public void RemoveTags(string keysJson) => HeroSignalMauiBridge.RemoveTags(keysJson);

    public string GetTags() => HeroSignalMauiBridge.Tags();

    public void AddAliases(string aliasesJson) => HeroSignalMauiBridge.AddAliases(aliasesJson);

    public void RemoveAlias(string label) => HeroSignalMauiBridge.RemoveAlias(label);

    public void SetLanguage(string languageCode) => HeroSignalMauiBridge.SetLanguage(languageCode);

    public void AddEmail(string email) => HeroSignalMauiBridge.AddEmail(email);

    public void RemoveEmail(string email) => HeroSignalMauiBridge.RemoveEmail(email);

    public void AddSms(string phoneNumber) => HeroSignalMauiBridge.AddSms(phoneNumber);

    public void RemoveSms(string phoneNumber) => HeroSignalMauiBridge.RemoveSms(phoneNumber);

    public string GetEmails() => HeroSignalMauiBridge.Emails();

    public string GetSmsNumbers() => HeroSignalMauiBridge.SmsNumbers();

    public string GetPushSubscription() => HeroSignalMauiBridge.PushSubscription();

    public void OptIn() => HeroSignalMauiBridge.OptIn();

    public void OptOut() => HeroSignalMauiBridge.OptOut();

    public bool GetPermission() => HeroSignalMauiBridge.Permission();

    public void RequestPermission(bool fallbackToSettings, Action<bool> completion) =>
        HeroSignalMauiBridge.RequestPermission(
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity,
            fallbackToSettings,
            new PermissionCallback(completion));

    public void ClearAllNotifications() => HeroSignalMauiBridge.ClearAllNotifications();

    public void RemoveNotification(string id)
    {
        // Android tepsi kimliği tamsayıdır; iOS biçimindeki metin kimliğin burada karşılığı yoktur.
        if (!int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var androidNotificationId))
            throw new ArgumentException("Android bildirim kimliği tamsayı olmalı (HeroSignalNotification.AndroidNotificationId).", nameof(id));
        HeroSignalMauiBridge.RemoveNotification(androidNotificationId);
    }

    public void RemoveGroupedNotifications(string group) => HeroSignalMauiBridge.RemoveGroupedNotifications(group);

    public void AddTriggers(string triggersJson) => HeroSignalMauiBridge.AddTriggers(triggersJson);

    public void RemoveTriggers(string keysJson) => HeroSignalMauiBridge.RemoveTriggers(keysJson);

    public void ClearTriggers() => HeroSignalMauiBridge.ClearTriggers();

    public string GetTriggers() => HeroSignalMauiBridge.Triggers();

    public void SetScreen(string? name) => HeroSignalMauiBridge.SetScreen(name);

    public bool GetInAppPaused() => HeroSignalMauiBridge.InAppPaused();

    public void SetInAppPaused(bool paused) => HeroSignalMauiBridge.UpdateInAppPaused(paused);

    public void InboxList(int limit, string? cursor, NativeResult result) =>
        HeroSignalMauiBridge.InboxList(limit, cursor, new ResultCallback(result));

    public void InboxMarkRead(string id, NativeResult result) =>
        HeroSignalMauiBridge.InboxMarkRead(id, new ResultCallback(result));

    public void InboxMarkAllRead(NativeResult result) =>
        HeroSignalMauiBridge.InboxMarkAllRead(new ResultCallback(result));

    public void InboxArchive(string id, NativeResult result) =>
        HeroSignalMauiBridge.InboxArchive(id, new ResultCallback(result));

    public void InboxUnreadCount(NativeResult result) =>
        HeroSignalMauiBridge.InboxUnreadCount(new ResultCallback(result));

    public void GetPreferences(NativeResult result) =>
        HeroSignalMauiBridge.GetPreferences(new ResultCallback(result));

    public void SetPreferences(string preferencesJson, NativeResult result) =>
        HeroSignalMauiBridge.SetPreferences(preferencesJson, new ResultCallback(result));

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue) =>
        HeroSignalMauiBridge.SendEvent(name, propertiesJson, value, hasValue);

    /// <summary>Live Activities iOS'a özgüdür; Android'de çağrılar köprüye ulaşmadan <c>PLATFORM_NOT_SUPPORTED</c> olur.</summary>
    public bool SupportsLiveActivities => false;

    public void SetupDefaultLiveActivity(NativeResult result) => LiveActivitiesNotSupported(result);

    public void StartDefaultLiveActivity(string activityId, string attributesJson, string contentStateJson, NativeResult result) =>
        LiveActivitiesNotSupported(result);

    private static void LiveActivitiesNotSupported(NativeResult result) =>
        result(null, HeroSignalException.PlatformNotSupported, UnsupportedPlatform.LiveActivitiesUnsupportedMessage);

    public bool GetLocationShared() => HeroSignalMauiBridge.LocationShared();

    public void SetLocationShared(bool shared) => HeroSignalMauiBridge.UpdateLocationShared(shared);

    /// <summary>
    /// Açıklama diyaloğu ve sistem istemi Kotlin köprüsündedir; sonuç <c>OnRequestPermissionsResult</c> (istek kodu
    /// <c>HeroSignal.Location.PERMISSION_REQUEST_CODE</c>) ya da etkinliğin ön plana dönüşüyle bildirilir.
    /// </summary>
    public void RequestLocationPermission(string? rationale, Action<bool> completion) =>
        HeroSignalMauiBridge.RequestLocationPermission(
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity,
            rationale,
            new PermissionCallback(completion));

    public void ForegroundPreventDefault(string eventId) => HeroSignalMauiBridge.ForegroundPreventDefault(eventId);

    public void ForegroundDisplay(string eventId) => HeroSignalMauiBridge.ForegroundDisplay(eventId);

    /// <summary>Yerel olay dinleyicisi (Java arayüzünü uygulayan .NET nesnesi; köprü güçlü referans tutar).</summary>
    private sealed class EventListener : Java.Lang.Object, IHeroSignalMauiListener
    {
        private readonly Action<string, string> _onEvent;

        public EventListener(Action<string, string> onEvent) => _onEvent = onEvent;

        public void OnEvent(string type, string json) => _onEvent(type, json);
    }

    private sealed class ResultCallback : Java.Lang.Object, IHeroSignalMauiResult
    {
        private readonly NativeResult _result;

        public ResultCallback(NativeResult result) => _result = result;

        public void OnResult(string? json, string? errorCode, string? errorMessage) => _result(json, errorCode, errorMessage);
    }

    private sealed class PermissionCallback : Java.Lang.Object, IHeroSignalMauiPermissionCallback
    {
        private readonly Action<bool> _completion;

        public PermissionCallback(Action<bool> completion) => _completion = completion;

        public void OnResult(bool granted) => _completion(granted);
    }
}

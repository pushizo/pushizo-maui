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

    public string? LoginWithProfile(string externalId, string profileJson, string? identityHash, string? jwt) =>
        PushizoMauiBridge.LoginWithProfile(externalId, profileJson, identityHash, jwt);

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

    public string? SetUserProperty(string key, string valueJson) => PushizoMauiBridge.SetUserProperty(key, valueJson);

    public void AddEmail(string email) => PushizoMauiBridge.AddEmail(email);

    public void RemoveEmail(string email) => PushizoMauiBridge.RemoveEmail(email);

    public void AddSms(string phoneNumber) => PushizoMauiBridge.AddSms(phoneNumber);

    public void RemoveSms(string phoneNumber) => PushizoMauiBridge.RemoveSms(phoneNumber);

    public string? RegisterAdm(string token) => PushizoMauiBridge.RegisterAdm(token);

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

    public void ContentCardsList(int limit, NativeResult result) =>
        PushizoMauiBridge.ContentCardsList(limit, new ResultCallback(result));

    public void ContentCardEvent(string id, string eventName, NativeResult result) =>
        PushizoMauiBridge.ContentCardEvent(id, eventName, new ResultCallback(result));

    public void GetPreferences(NativeResult result) =>
        PushizoMauiBridge.GetPreferences(new ResultCallback(result));

    public void SetPreferences(string preferencesJson, NativeResult result) =>
        PushizoMauiBridge.SetPreferences(preferencesJson, new ResultCallback(result));

    public void SetAdsPersonalizationConsent(bool granted, NativeResult result) =>
        PushizoMauiBridge.SetAdsPersonalizationConsent(granted, new ResultCallback(result));

    public void GetConsents(NativeResult result) => PushizoMauiBridge.GetConsents(new ResultCallback(result));

    public void SetAdvertisingId(string? id) => PushizoMauiBridge.SetAdvertisingId(id);

    /// <summary>Sayı yerel SDK'da saklanır ve sonraki bildirimlere yazılır; rozet bu cihazda gösterilebiliyorsa true.</summary>
    public bool SetBadgeCount(int count) => PushizoMauiBridge.SetBadgeCount(count);

    public int GetBadgeCount() => PushizoMauiBridge.BadgeCount();

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue, string? eventId) =>
        PushizoMauiBridge.SendEvent(name, propertiesJson, value, hasValue, eventId);

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

    public bool GetLocationPermissionGranted() => PushizoMauiBridge.LocationPermissionGranted();

    public bool GetLocationBackgroundPermissionGranted() => PushizoMauiBridge.LocationBackgroundPermissionGranted();

    /// <summary>Açıklama diyaloğu ve sistem istemi Kotlin köprüsündedir (konum izni akışıyla aynı sonuç yolu).</summary>
    public void RequestBackgroundLocationPermission(string? rationale, Action<bool> completion) =>
        PushizoMauiBridge.RequestBackgroundLocationPermission(
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity,
            rationale,
            new PermissionCallback(completion));

    public bool GetGeofencingEnabled() => PushizoMauiBridge.GeofencingEnabled();

    public void SetGeofencingEnabled(bool enabled) => PushizoMauiBridge.UpdateGeofencingEnabled(enabled);

    public string? DeliveryStatus() => PushizoMauiBridge.DeliveryStatus();

    public bool DeliveryShouldShowGuidance(int minIntervalDays) => PushizoMauiBridge.DeliveryShouldShowGuidance(minIntervalDays);

    public bool DeliveryOpenSettings(string setting) =>
        PushizoMauiBridge.DeliveryOpenSettings(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity, setting);

    /// <summary>Diyalog Kotlin köprüsünde etkin Activity'nin arayüz iş parçacığında gösterilir; sonuç oradan bir kez gelir.</summary>
    public void DeliveryShowGuidance(string setting, string? message, Action<bool> completion) =>
        PushizoMauiBridge.DeliveryShowGuidance(
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity,
            setting,
            message,
            new PermissionCallback(completion));

    public void FeatureFlagsRefresh(NativeResult result) => PushizoMauiBridge.FeatureFlagsRefresh(new ResultCallback(result));

    public bool FeatureFlagEnabled(string key, bool fallback) => PushizoMauiBridge.FeatureFlagEnabled(key, fallback);

    public string? FeatureFlagValue(string key) => PushizoMauiBridge.FeatureFlagValue(key);

    public void PlacementsList(string key, int limit, NativeResult result) =>
        PushizoMauiBridge.PlacementsList(key, limit, new ResultCallback(result));

    public void PlacementsReportImpression(string messageId) => PushizoMauiBridge.PlacementsReportImpression(messageId);

    public void PlacementsReportClick(string messageId, string buttonId) => PushizoMauiBridge.PlacementsReportClick(messageId, buttonId);

    public void PlacementsReportDismiss(string messageId) => PushizoMauiBridge.PlacementsReportDismiss(messageId);

    public void ChatCall(string method, string argsJson, NativeResult result) =>
        PushizoMauiBridge.ChatCall(method, argsJson, new ResultCallback(result));

    /// <summary>Etkin Activity varsa onun üzerinden, yoksa uygulama bağlamıyla (yeni görev) açılır.</summary>
    public void ChatShow() => PushizoMauiBridge.ChatShow(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity);

    public bool ChatHide() => PushizoMauiBridge.ChatHide();

    public bool ChatIsPresented() => PushizoMauiBridge.ChatIsPresented();

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

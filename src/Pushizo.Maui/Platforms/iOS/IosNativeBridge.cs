using Foundation;
using PushizoSdk.Maui.Internal;
using PushizoSdk.Native.iOS;

namespace PushizoSdk.Maui.IosBridge;

/// <summary>
/// <see cref="INativeBridge"/>'in iOS uygulaması: bağlanan @objc Swift cephesi <c>HSMauiBridge</c>
/// (Pushizo.Maui.iOS.Binding/native) üzerinden yerel <c>Pushizo</c> Swift SDK'sını çağırır.
/// </summary>
internal sealed class IosNativeBridge : INativeBridge
{
    public void Start(string appId, string optionsJson, Action<string, string> onEvent) =>
        HSMauiBridge.Start(appId, optionsJson, (type, json) => onEvent(type, json));

    public void SetConsentGiven(bool given) => HSMauiBridge.SetConsentGiven(given);

    public string? Login(string externalId, string? identityHash, string? jwt) => HSMauiBridge.Login(externalId, identityHash, jwt);

    public string? LoginWithProfile(string externalId, string profileJson, string? identityHash, string? jwt) =>
        HSMauiBridge.LoginWithProfile(externalId, profileJson, identityHash, jwt);

    public string? UpdateUserJwt(string jwt) => HSMauiBridge.UpdateUserJwt(jwt);

    public void Logout() => HSMauiBridge.Logout();

    public void SetLogLevel(int level) => HSMauiBridge.ApplyLogLevel(level);

    public string? GetExternalId() => HSMauiBridge.ExternalId();

    public string? GetUserId() => HSMauiBridge.UserId();

    public void AddTags(string tagsJson) => HSMauiBridge.AddTags(tagsJson);

    public void RemoveTags(string keysJson) => HSMauiBridge.RemoveTags(keysJson);

    public string GetTags() => HSMauiBridge.Tags();

    public void AddAliases(string aliasesJson) => HSMauiBridge.AddAliases(aliasesJson);

    public void RemoveAlias(string label) => HSMauiBridge.RemoveAlias(label);

    public void SetLanguage(string languageCode) => HSMauiBridge.SetLanguage(languageCode);

    /// <summary>Yerel iOS SDK'sı geçersiz girdiyi günlüğe yazıp yok sayar; hata mesajı dönmez.</summary>
    public string? SetUserProperty(string key, string valueJson)
    {
        HSMauiBridge.SetUserProperty(key, valueJson);
        return null;
    }

    public void AddEmail(string email) => HSMauiBridge.AddEmail(email);

    public void RemoveEmail(string email) => HSMauiBridge.RemoveEmail(email);

    public void AddSms(string phoneNumber) => HSMauiBridge.AddSms(phoneNumber);

    public void RemoveSms(string phoneNumber) => HSMauiBridge.RemoveSms(phoneNumber);

    /// <summary>ADM (Fire OS) yalnızca Android'dedir; iOS'ta etkisizdir.</summary>
    public string? RegisterAdm(string token) => null;

    public string GetEmails() => HSMauiBridge.Emails();

    public string GetSmsNumbers() => HSMauiBridge.SmsNumbers();

    public string GetPushSubscription() => HSMauiBridge.PushSubscription();

    public void OptIn() => HSMauiBridge.OptIn();

    public void OptOut() => HSMauiBridge.OptOut();

    public bool GetPermission() => HSMauiBridge.Permission();

    public void RequestPermission(bool fallbackToSettings, Action<bool> completion) =>
        HSMauiBridge.RequestPermission(fallbackToSettings, granted => completion(granted));

    public void ClearAllNotifications() => HSMauiBridge.ClearAllNotifications();

    public void RemoveNotification(string id) => HSMauiBridge.RemoveNotification(id);

    public void RemoveGroupedNotifications(string group) => HSMauiBridge.RemoveGroupedNotifications(group);

    public void AddTriggers(string triggersJson) => HSMauiBridge.AddTriggers(triggersJson);

    public void RemoveTriggers(string keysJson) => HSMauiBridge.RemoveTriggers(keysJson);

    public void ClearTriggers() => HSMauiBridge.ClearTriggers();

    public string GetTriggers() => HSMauiBridge.Triggers();

    public void SetScreen(string? name) => HSMauiBridge.SetScreen(name);

    public bool GetInAppPaused() => HSMauiBridge.InAppPaused();

    public void SetInAppPaused(bool paused) => HSMauiBridge.UpdateInAppPaused(paused);

    public void ChatCall(string method, string argsJson, NativeResult result) =>
        HSMauiBridge.ChatCall(method, argsJson, (json, code, message) => result(json, code, message));

    public void InboxList(int limit, string? cursor, NativeResult result) =>
        HSMauiBridge.InboxList(limit, cursor, (json, code, message) => result(json, code, message));

    public void InboxMarkRead(string id, NativeResult result) =>
        HSMauiBridge.InboxMarkRead(id, (json, code, message) => result(json, code, message));

    public void InboxMarkAllRead(NativeResult result) =>
        HSMauiBridge.InboxMarkAllRead((json, code, message) => result(json, code, message));

    public void InboxArchive(string id, NativeResult result) =>
        HSMauiBridge.InboxArchive(id, (json, code, message) => result(json, code, message));

    public void InboxUnreadCount(NativeResult result) =>
        HSMauiBridge.InboxUnreadCount((json, code, message) => result(json, code, message));

    public void ContentCardsList(int limit, NativeResult result) =>
        HSMauiBridge.ContentCardsList(limit, (json, code, message) => result(json, code, message));

    public void ContentCardEvent(string id, string eventName, NativeResult result) =>
        HSMauiBridge.ContentCardEvent(id, eventName, (json, code, message) => result(json, code, message));

    public void GetPreferences(NativeResult result) =>
        HSMauiBridge.GetPreferences((json, code, message) => result(json, code, message));

    public void SetPreferences(string preferencesJson, NativeResult result) =>
        HSMauiBridge.SetPreferences(preferencesJson, (json, code, message) => result(json, code, message));

    public void SetAdsPersonalizationConsent(bool granted, NativeResult result) =>
        HSMauiBridge.SetAdsPersonalizationConsent(granted, (json, code, message) => result(json, code, message));

    public void GetConsents(NativeResult result) =>
        HSMauiBridge.GetConsents((json, code, message) => result(json, code, message));

    public void SetAdvertisingId(string? id) => HSMauiBridge.SetAdvertisingId(id);

    /// <summary>iOS'ta rozet her zaman ayarlanır (yerel çağrı sonuç döndürmez).</summary>
    public bool SetBadgeCount(int count)
    {
        HSMauiBridge.SetBadgeCount(count);
        return true;
    }

    public int GetBadgeCount() => (int)HSMauiBridge.BadgeCount();

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue, string? eventId) =>
        HSMauiBridge.SendEvent(name, propertiesJson, value, hasValue, eventId);

    public bool SupportsLiveActivities => true;

    public void SetupDefaultLiveActivity(NativeResult result) =>
        HSMauiBridge.SetupDefaultLiveActivity((json, code, message) => result(json, code, message));

    public void StartDefaultLiveActivity(string activityId, string attributesJson, string contentStateJson, NativeResult result) =>
        HSMauiBridge.StartDefaultLiveActivity(activityId, attributesJson, contentStateJson, (json, code, message) => result(json, code, message));

    public bool GetLocationShared() => HSMauiBridge.LocationShared();

    public void SetLocationShared(bool shared) => HSMauiBridge.UpdateLocationShared(shared);

    public void RequestLocationPermission(string? rationale, Action<bool> completion) =>
        HSMauiBridge.RequestLocationPermission(rationale, granted => completion(granted));

    public bool GetLocationPermissionGranted() => HSMauiBridge.LocationPermissionGranted();

    public bool GetLocationBackgroundPermissionGranted() => HSMauiBridge.LocationBackgroundPermissionGranted();

    /// <summary>iOS "her zaman" iznini ister; açıklama metni (<paramref name="rationale"/>) Android'e özgüdür, yok sayılır.</summary>
    public void RequestBackgroundLocationPermission(string? rationale, Action<bool> completion) =>
        HSMauiBridge.RequestBackgroundLocationPermission(granted => completion(granted));

    public bool GetGeofencingEnabled() => HSMauiBridge.GeofencingEnabled();

    public void SetGeofencingEnabled(bool enabled) => HSMauiBridge.UpdateGeofencingEnabled(enabled);

    // Android teslimat güçlendirme (ADR-0092) iOS'ta yoktur: yerel çağrı yapılmaz; durum yok, diğerleri etkisiz false.
    public string? DeliveryStatus() => null;

    public bool DeliveryShouldShowGuidance(int minIntervalDays) => false;

    public bool DeliveryOpenSettings(string setting) => false;

    public void DeliveryShowGuidance(string setting, string? message, Action<bool> completion) => completion(false);

    public void FeatureFlagsRefresh(NativeResult result) =>
        HSMauiBridge.FeatureFlagsRefresh((json, code, message) => result(json, code, message));

    public bool FeatureFlagEnabled(string key, bool fallback) => HSMauiBridge.FeatureFlagEnabled(key, fallback);

    public string? FeatureFlagValue(string key) => HSMauiBridge.FeatureFlagValue(key);

    public void PlacementsList(string key, int limit, NativeResult result) =>
        HSMauiBridge.PlacementsList(key, limit, (json, code, message) => result(json, code, message));

    public void PlacementsReportImpression(string messageId) => HSMauiBridge.PlacementsReportImpression(messageId);

    public void PlacementsReportClick(string messageId, string buttonId) => HSMauiBridge.PlacementsReportClick(messageId, buttonId);

    public void PlacementsReportDismiss(string messageId) => HSMauiBridge.PlacementsReportDismiss(messageId);

    public void ChatShow() => HSMauiBridge.ChatShow();

    public bool ChatHide() => HSMauiBridge.ChatHide();

    public bool ChatIsPresented() => HSMauiBridge.ChatIsPresented();

    public void ForegroundPreventDefault(string eventId) => HSMauiBridge.ForegroundPreventDefault(eventId);

    public void ForegroundDisplay(string eventId) => HSMauiBridge.ForegroundDisplay(eventId);
}

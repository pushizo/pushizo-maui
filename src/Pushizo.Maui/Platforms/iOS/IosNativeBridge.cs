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

    public void AddEmail(string email) => HSMauiBridge.AddEmail(email);

    public void RemoveEmail(string email) => HSMauiBridge.RemoveEmail(email);

    public void AddSms(string phoneNumber) => HSMauiBridge.AddSms(phoneNumber);

    public void RemoveSms(string phoneNumber) => HSMauiBridge.RemoveSms(phoneNumber);

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

    public void GetPreferences(NativeResult result) =>
        HSMauiBridge.GetPreferences((json, code, message) => result(json, code, message));

    public void SetPreferences(string preferencesJson, NativeResult result) =>
        HSMauiBridge.SetPreferences(preferencesJson, (json, code, message) => result(json, code, message));

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue) =>
        HSMauiBridge.SendEvent(name, propertiesJson, value, hasValue);

    public bool SupportsLiveActivities => true;

    public void SetupDefaultLiveActivity(NativeResult result) =>
        HSMauiBridge.SetupDefaultLiveActivity((json, code, message) => result(json, code, message));

    public void StartDefaultLiveActivity(string activityId, string attributesJson, string contentStateJson, NativeResult result) =>
        HSMauiBridge.StartDefaultLiveActivity(activityId, attributesJson, contentStateJson, (json, code, message) => result(json, code, message));

    public bool GetLocationShared() => HSMauiBridge.LocationShared();

    public void SetLocationShared(bool shared) => HSMauiBridge.UpdateLocationShared(shared);

    public void RequestLocationPermission(string? rationale, Action<bool> completion) =>
        HSMauiBridge.RequestLocationPermission(rationale, granted => completion(granted));

    public void ForegroundPreventDefault(string eventId) => HSMauiBridge.ForegroundPreventDefault(eventId);

    public void ForegroundDisplay(string eventId) => HSMauiBridge.ForegroundDisplay(eventId);
}

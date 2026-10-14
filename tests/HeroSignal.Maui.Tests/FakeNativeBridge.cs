using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Tests;

/// <summary>
/// Yerel köprünün test ikizi: çağrıları kaydeder, okuma değerlerini ayarlanabilir tutar ve yerel olayları
/// (<see cref="Emit"/>) Android/iOS köprüleriyle aynı JSON biçimleriyle üretir.
/// </summary>
internal sealed class FakeNativeBridge : INativeBridge
{
    private Action<string, string>? _onEvent;

    public List<string> Calls { get; } = new();
    public string? StartedAppId { get; private set; }
    public string? OptionsJson { get; private set; }
    public int StartCount { get; private set; }

    public string? ExternalIdValue { get; set; }
    public string? UserIdValue { get; set; }
    public string TagsJson { get; set; } = "{}";
    public string EmailsJson { get; set; } = "[]";
    public string SmsJson { get; set; } = "[]";
    public string SubscriptionJson { get; set; } = """{"id":null,"token":null,"optedIn":false,"permission":false}""";
    public bool PermissionValue { get; set; }
    public bool PermissionResult { get; set; } = true;
    public string TriggersJson { get; set; } = "{}";
    public bool Paused { get; set; }

    /// <summary>Bildirim kutusu yanıtı: (json, hata kodu, hata mesajı).</summary>
    public (string? Json, string? Code, string? Message) InboxResponse { get; set; } = ("{}", null, null);

    /// <summary>Tercih yanıtı: (json, hata kodu, hata mesajı).</summary>
    public (string? Json, string? Code, string? Message) PreferencesResponse { get; set; } = ("""{"preferences":[],"categories":[]}""", null, null);

    public (string Name, string PropertiesJson, double Value, bool HasValue)? LastEvent { get; private set; }

    /// <summary>Yerel olay üretir (köprünün olay dinleyicisini eşzamanlı çağırır).</summary>
    public void Emit(string type, string json)
    {
        var handler = _onEvent ?? throw new InvalidOperationException("Köprü başlatılmadı.");
        handler(type, json);
    }

    private void Record(string call) => Calls.Add(call);

    public void Start(string appId, string optionsJson, Action<string, string> onEvent)
    {
        StartCount++;
        StartedAppId = appId;
        OptionsJson = optionsJson;
        _onEvent = onEvent;
    }

    public void SetConsentGiven(bool given) => Record($"consent:{given}");
    public void Login(string externalId, string? identityHash) => Record($"login:{externalId}:{identityHash ?? "<null>"}");
    public void Logout() => Record("logout");
    public void SetLogLevel(int level) => Record($"logLevel:{level}");
    public string? GetExternalId() => ExternalIdValue;
    public string? GetUserId() => UserIdValue;
    public void AddTags(string tagsJson) => Record($"addTags:{tagsJson}");
    public void RemoveTags(string keysJson) => Record($"removeTags:{keysJson}");
    public string GetTags() => TagsJson;
    public void AddAliases(string aliasesJson) => Record($"addAliases:{aliasesJson}");
    public void RemoveAlias(string label) => Record($"removeAlias:{label}");
    public void SetLanguage(string languageCode) => Record($"language:{languageCode}");
    public void AddEmail(string email) => Record($"addEmail:{email}");
    public void RemoveEmail(string email) => Record($"removeEmail:{email}");
    public void AddSms(string phoneNumber) => Record($"addSms:{phoneNumber}");
    public void RemoveSms(string phoneNumber) => Record($"removeSms:{phoneNumber}");
    public string GetEmails() => EmailsJson;
    public string GetSmsNumbers() => SmsJson;
    public string GetPushSubscription() => SubscriptionJson;
    public void OptIn() => Record("optIn");
    public void OptOut() => Record("optOut");
    public bool GetPermission() => PermissionValue;

    public void RequestPermission(bool fallbackToSettings, Action<bool> completion)
    {
        Record($"requestPermission:{fallbackToSettings}");
        // Gerçek köprüler sonucu başka bir iş parçacığından döndürür.
        ThreadPool.QueueUserWorkItem(_ => completion(PermissionResult));
    }

    public void ClearAllNotifications() => Record("clearAll");
    public void RemoveNotification(string id) => Record($"removeNotification:{id}");
    public void RemoveGroupedNotifications(string group) => Record($"removeGrouped:{group}");
    public void AddTriggers(string triggersJson) => Record($"addTriggers:{triggersJson}");
    public void RemoveTriggers(string keysJson) => Record($"removeTriggers:{keysJson}");
    public void ClearTriggers() => Record("clearTriggers");
    public string GetTriggers() => TriggersJson;
    public void SetScreen(string? name) => Record($"screen:{name ?? "<null>"}");
    public bool GetInAppPaused() => Paused;

    public void SetInAppPaused(bool paused)
    {
        Record($"paused:{paused}");
        Paused = paused;
    }

    private void Respond(string call, NativeResult result) => Respond(call, InboxResponse, result);

    private void Respond(string call, (string? Json, string? Code, string? Message) response, NativeResult result)
    {
        Record(call);
        var (json, code, message) = response;
        ThreadPool.QueueUserWorkItem(_ => result(json, code, message));
    }

    public void InboxList(int limit, string? cursor, NativeResult result) => Respond($"inboxList:{limit}:{cursor ?? "<null>"}", result);
    public void InboxMarkRead(string id, NativeResult result) => Respond($"inboxMarkRead:{id}", result);
    public void InboxMarkAllRead(NativeResult result) => Respond("inboxMarkAllRead", result);
    public void InboxArchive(string id, NativeResult result) => Respond($"inboxArchive:{id}", result);
    public void InboxUnreadCount(NativeResult result) => Respond("inboxUnreadCount", result);
    public void GetPreferences(NativeResult result) => Respond("getPreferences", PreferencesResponse, result);
    public void SetPreferences(string preferencesJson, NativeResult result) => Respond($"setPreferences:{preferencesJson}", PreferencesResponse, result);

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue)
    {
        Record($"sendEvent:{name}");
        LastEvent = (name, propertiesJson, value, hasValue);
    }

    public void ForegroundPreventDefault(string eventId) => Record($"preventDefault:{eventId}");
    public void ForegroundDisplay(string eventId) => Record($"display:{eventId}");
}

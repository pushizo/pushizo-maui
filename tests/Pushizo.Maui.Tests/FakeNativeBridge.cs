using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

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

    /// <summary>Sohbet yanıtları (yöntem → json, hata kodu, hata mesajı); tanımsız yöntem null JSON döner.</summary>
    public Dictionary<string, (string? Json, string? Code, string? Message)> ChatResponses { get; } = new();

    /// <summary>Sohbet çağrıları: (yöntem, argüman JSON'u).</summary>
    public List<(string Method, string ArgsJson)> ChatCalls { get; } = new();

    public void ChatCall(string method, string argsJson, NativeResult result)
    {
        ChatCalls.Add((method, argsJson));
        var (json, code, message) = ChatResponses.TryGetValue(method, out var r) ? r : (null, null, null);
        result(json, code, message);
    }

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
    /// <summary>Login yanıtı (yerel köprünün döndürdüğü kanıt hatası; null = başarı).</summary>
    public string? LoginError { get; set; }

    /// <summary>UpdateUserJwt yanıtı (ör. giriş yapılmamış; null = başarı).</summary>
    public string? UpdateUserJwtError { get; set; }

    public string? Login(string externalId, string? identityHash, string? jwt)
    {
        Record(jwt is null ? $"login:{externalId}:{identityHash ?? "<null>"}" : $"login:{externalId}:{identityHash ?? "<null>"}:jwt={jwt}");
        return LoginError;
    }

    public string? LoginWithProfile(string externalId, string profileJson, string? identityHash, string? jwt)
    {
        Record($"loginWithProfile:{externalId}:{profileJson}:{identityHash ?? "<null>"}:{jwt ?? "<null>"}");
        return LoginError;
    }

    /// <summary>RegisterAdm yanıtı (yerel SDK'nın geçersiz kimlik hatası; null = başarı).</summary>
    public string? RegisterAdmError { get; set; }

    public string? RegisterAdm(string token)
    {
        Record($"registerAdm:{token}");
        return RegisterAdmError;
    }

    /// <summary>Rıza yanıtı: (json, hata kodu, hata mesajı).</summary>
    public (string? Json, string? Code, string? Message) ConsentsResponse { get; set; } = ("""{"consents":[]}""", null, null);

    public void SetAdsPersonalizationConsent(bool granted, NativeResult result) =>
        Respond($"setAdsPersonalizationConsent:{granted}", PreferencesResponse, result);

    public void GetConsents(NativeResult result) => Respond("getConsents", ConsentsResponse, result);
    public void SetAdvertisingId(string? id) => Record($"advertisingId:{id ?? "<null>"}");

    /// <summary>Rozet çağrısının sonucu (Android: rozet gösterilebiliyor mu; iOS: her zaman true).</summary>
    public bool BadgeResult { get; set; } = true;

    /// <summary>Yerel SDK'daki rozet sayısı.</summary>
    public int BadgeCountValue { get; private set; }

    public bool SetBadgeCount(int count)
    {
        Record($"badge:{count}");
        BadgeCountValue = count;
        return BadgeResult;
    }

    public int GetBadgeCount() => BadgeCountValue;

    /// <summary>SetUserProperty yanıtı (Android yerel SDK'sının anahtar hatası; null = başarı).</summary>
    public string? UserPropertyError { get; set; }

    public string? SetUserProperty(string key, string valueJson)
    {
        Record($"setUserProperty:{key}:{valueJson}");
        return UserPropertyError;
    }

    /// <summary>Hazır sohbet ekranı açık mı (yerel SDK durumu).</summary>
    public bool ChatPresented { get; private set; }

    public void ChatShow()
    {
        Record("chatShow");
        ChatPresented = true;
    }

    public bool ChatHide()
    {
        Record("chatHide");
        var presented = ChatPresented;
        ChatPresented = false;
        return presented;
    }

    public bool ChatIsPresented() => ChatPresented;

    public string? UpdateUserJwt(string jwt)
    {
        Record($"updateUserJwt:{jwt}");
        return UpdateUserJwtError;
    }
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
    public void ContentCardsList(int limit, NativeResult result) => Respond($"contentCardsList:{limit}", result);
    public void ContentCardEvent(string id, string eventName, NativeResult result) => Respond($"contentCardEvent:{id}:{eventName}", result);
    public void GetPreferences(NativeResult result) => Respond("getPreferences", PreferencesResponse, result);
    public void SetPreferences(string preferencesJson, NativeResult result) => Respond($"setPreferences:{preferencesJson}", PreferencesResponse, result);

    /// <summary>Son olayın tekilleştirme kimliği (verilmediyse null).</summary>
    public string? LastEventId { get; private set; }

    public void SendEvent(string name, string propertiesJson, double value, bool hasValue, string? eventId)
    {
        Record($"sendEvent:{name}");
        LastEvent = (name, propertiesJson, value, hasValue);
        LastEventId = eventId;
    }

    /// <summary>false: Android köprüsü gibi Live Activities desteklenmez.</summary>
    public bool SupportsLiveActivities { get; set; } = true;

    /// <summary>Live Activities yanıtı: (json, hata kodu, hata mesajı).</summary>
    public (string? Json, string? Code, string? Message) LiveActivityResponse { get; set; } = (null, null, null);

    public void SetupDefaultLiveActivity(NativeResult result) => Respond("setupDefaultLiveActivity", LiveActivityResponse, result);

    public void StartDefaultLiveActivity(string activityId, string attributesJson, string contentStateJson, NativeResult result) =>
        Respond($"startDefaultLiveActivity:{activityId}:{attributesJson}:{contentStateJson}", LiveActivityResponse, result);

    /// <summary>Yerel SDK'daki kalıcı konum paylaşımı tercihi.</summary>
    public bool LocationSharedValue { get; set; }

    /// <summary>Konum izni isteğinin sonucu.</summary>
    public bool LocationPermissionResult { get; set; } = true;

    public string? LastLocationRationale { get; private set; }

    public bool GetLocationShared() => LocationSharedValue;

    public void SetLocationShared(bool shared)
    {
        Record($"locationShared:{shared}");
        LocationSharedValue = shared;
    }

    public void RequestLocationPermission(string? rationale, Action<bool> completion)
    {
        Record("requestLocationPermission");
        LastLocationRationale = rationale;
        // Gerçek köprüler sonucu başka bir iş parçacığından (iOS: ana, Android: UI/yaşam döngüsü) döndürür.
        ThreadPool.QueueUserWorkItem(_ => completion(LocationPermissionResult));
    }

    public bool LocationPermissionGrantedValue { get; set; }
    public bool LocationBackgroundPermissionGrantedValue { get; set; }

    /// <summary>Arka plan konum izni isteğinin sonucu.</summary>
    public bool BackgroundLocationPermissionResult { get; set; } = true;

    public bool GetLocationPermissionGranted() => LocationPermissionGrantedValue;
    public bool GetLocationBackgroundPermissionGranted() => LocationBackgroundPermissionGrantedValue;

    public void RequestBackgroundLocationPermission(string? rationale, Action<bool> completion)
    {
        Record("requestBackgroundLocationPermission");
        LastLocationRationale = rationale;
        ThreadPool.QueueUserWorkItem(_ => completion(BackgroundLocationPermissionResult));
    }

    /// <summary>Yerel SDK'daki kalıcı bölge izleme tercihi.</summary>
    public bool GeofencingEnabledValue { get; set; }

    public bool GetGeofencingEnabled() => GeofencingEnabledValue;

    public void SetGeofencingEnabled(bool enabled)
    {
        Record($"geofencing:{enabled}");
        GeofencingEnabledValue = enabled;
    }

    /// <summary>Yerel teslim durumu JSON'u (null: teşhis yok).</summary>
    public string? DeliveryStatusJson { get; set; }

    /// <summary><c>shouldShowGuidance</c>, <c>openSettings</c> ve <c>showGuidance</c> sonucu.</summary>
    public bool DeliveryResult { get; set; } = true;

    /// <summary>Teslimat çağrılarında fırlatılacak yerel hata (JNI hatası benzetimi).</summary>
    public Exception? DeliveryFailure { get; set; }

    public string? DeliveryStatus()
    {
        Record("deliveryStatus");
        if (DeliveryFailure is not null) throw DeliveryFailure;
        return DeliveryStatusJson;
    }

    public bool DeliveryShouldShowGuidance(int minIntervalDays)
    {
        Record($"deliveryShouldShowGuidance:{minIntervalDays}");
        if (DeliveryFailure is not null) throw DeliveryFailure;
        return DeliveryResult;
    }

    public bool DeliveryOpenSettings(string setting)
    {
        Record($"deliveryOpenSettings:{setting}");
        if (DeliveryFailure is not null) throw DeliveryFailure;
        return DeliveryResult;
    }

    public void DeliveryShowGuidance(string setting, string? message, Action<bool> completion)
    {
        Record($"deliveryShowGuidance:{setting}:{message ?? "<null>"}");
        if (DeliveryFailure is not null) throw DeliveryFailure;
        // Gerçek köprü sonucu arayüz iş parçacığından döndürür.
        ThreadPool.QueueUserWorkItem(_ => completion(DeliveryResult));
    }

    /// <summary>Bayrak yenileme yanıtı: (json, hata kodu, hata mesajı).</summary>
    public (string? Json, string? Code, string? Message) FeatureFlagsRefreshResponse { get; set; } = ("""{"refreshed":true}""", null, null);

    /// <summary>Yerel bayrak önbelleği: anahtar → (açık mı, <c>{"value":…}</c> JSON'u ya da null).</summary>
    public Dictionary<string, (bool Enabled, string? ValueJson)> FeatureFlags { get; } = new();

    public void FeatureFlagsRefresh(NativeResult result) => Respond("featureFlagsRefresh", FeatureFlagsRefreshResponse, result);

    public bool FeatureFlagEnabled(string key, bool fallback)
    {
        Record($"featureFlagEnabled:{key}:{fallback}");
        return FeatureFlags.TryGetValue(key, out var flag) ? flag.Enabled : fallback;
    }

    public string? FeatureFlagValue(string key)
    {
        Record($"featureFlagValue:{key}");
        return FeatureFlags.TryGetValue(key, out var flag) ? flag.ValueJson : null;
    }

    /// <summary>Yerleşim yanıtı: (json, hata kodu, hata mesajı).</summary>
    public (string? Json, string? Code, string? Message) PlacementsResponse { get; set; } = ("""{"messages":[]}""", null, null);

    public void PlacementsList(string key, int limit, NativeResult result) => Respond($"placementsList:{key}:{limit}", PlacementsResponse, result);
    public void PlacementsReportImpression(string messageId) => Record($"placementImpression:{messageId}");
    public void PlacementsReportClick(string messageId, string buttonId) => Record($"placementClick:{messageId}:{buttonId}");
    public void PlacementsReportDismiss(string messageId) => Record($"placementDismiss:{messageId}");

    public void ForegroundPreventDefault(string eventId) => Record($"preventDefault:{eventId}");
    public void ForegroundDisplay(string eventId) => Record($"display:{eventId}");
}

namespace PushizoSdk.Maui.Internal;

/// <summary>
/// Platform uygulaması (Android binding, iOS köprüsü). Girdiler bu katmana ulaşmadan <see cref="Pushizo"/>
/// tarafından doğrulanır ve normalize edilir; olaylar <see cref="IPushizoEventSink"/> ile geri bildirilir.
/// </summary>
internal interface IPushizoPlatform
{
    /// <summary>Bu platformda yerel SDK var mı (yoksa çağrılar etkisizdir, bildirim kutusu hata döndürür).</summary>
    bool IsSupported { get; }

    void Initialize(string appId, PushizoOptions options, IPushizoEventSink sink);
    void SetConsentGiven(bool given);
    void Login(string externalId, string? identityHash, string? jwt);
    void UpdateUserJwt(string jwt);
    void Logout();
    void SetLogLevel(LogLevel level);

    // Kullanıcı
    string? ExternalId { get; }
    string? UserId { get; }
    void AddTags(IReadOnlyDictionary<string, string> tags);
    void RemoveTags(IReadOnlyList<string> keys);
    IReadOnlyDictionary<string, string> GetTags();
    void AddAliases(IReadOnlyDictionary<string, string> aliases);
    void RemoveAlias(string label);
    void SetLanguage(string languageCode);
    void AddEmail(string email);
    void RemoveEmail(string email);
    void AddSms(string phoneNumber);
    void RemoveSms(string phoneNumber);
    IReadOnlyList<string> Emails { get; }
    IReadOnlyList<string> SmsNumbers { get; }
    Task<UserPreferences> GetPreferencesAsync();
    Task<UserPreferences> SetPreferencesAsync(IReadOnlyList<PreferenceInput> preferences);

    // Push aboneliği
    PushSubscriptionState PushSubscription { get; }
    void OptIn();
    void OptOut();

    // Bildirimler
    bool NotificationPermission { get; }
    Task<bool> RequestPermissionAsync(bool fallbackToSettings);
    void ClearAllNotifications();
    void RemoveNotification(string id);
    void RemoveGroupedNotifications(string group);

    // In-app
    void AddTriggers(IReadOnlyDictionary<string, string> triggers);
    void RemoveTriggers(IReadOnlyList<string> keys);
    void ClearTriggers();
    IReadOnlyDictionary<string, string> GetTriggers();
    void SetScreen(string? name);
    bool InAppPaused { get; set; }

    // Bildirim kutusu
    Task<InboxPage> InboxListAsync(int limit, string? cursor);
    Task<InboxItem> InboxMarkReadAsync(string id);
    Task<int> InboxMarkAllReadAsync();
    Task InboxArchiveAsync(string id);
    Task<int> InboxUnreadCountAsync();

    // Oturum
    void SendEvent(string name, Dictionary<string, object?> properties, double? value);

    // Live Activities (yalnızca iOS; girdiler doğrulanmış ve normalize edilmiştir: değerler string, long, double, bool)
    Task SetupDefaultLiveActivityAsync();
    Task StartDefaultLiveActivityAsync(string activityId, IReadOnlyDictionary<string, object> attributes, IReadOnlyDictionary<string, object> contentState);

    // Konum paylaşımı (v2 Faz 17b; yalnızca ön plan, gönderim yerel SDK'da)
    bool LocationShared { get; }
    void SetLocationShared(bool shared);

    /// <summary><paramref name="rationale"/> boş değilse kırpılmıştır; null ise yerel SDK'nın varsayılan metni kullanılır.</summary>
    Task<bool> RequestLocationPermissionAsync(string? rationale);
}

/// <summary>Yerel SDK olaylarının ortak katmana iletimi (herhangi bir iş parçacığından çağrılabilir).</summary>
internal interface IPushizoEventSink
{
    void OnNotificationClicked(NotificationClickedEventArgs args);
    void OnNotificationWillDisplay(NotificationWillDisplayEventArgs args);
    void OnPermissionChanged(bool permission);
    void OnPushSubscriptionChanged(PushSubscriptionState state);
    void OnInAppClicked(InAppMessageClickedEventArgs args);
    void OnInAppLifecycle(InAppLifecyclePhase phase, InAppMessage message);
}

internal enum InAppLifecyclePhase
{
    WillDisplay,
    DidDisplay,
    WillDismiss,
    DidDismiss,
}

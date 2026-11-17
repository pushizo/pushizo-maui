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

    /// <summary>Profille giriş; profil alanları doğrulanmış ve normalize edilmiştir.</summary>
    void Login(string externalId, PushizoUserProfile profile, string? identityHash, string? jwt);
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

    /// <summary>Kullanıcı özelliği; değer kanonik ağaçtır (string, bool, long, double, sözlük, liste) ya da null (sil).</summary>
    void SetUserProperty(string key, object? value);
    void AddEmail(string email);
    void RemoveEmail(string email);
    void AddSms(string phoneNumber);
    void RemoveSms(string phoneNumber);
    IReadOnlyList<string> Emails { get; }
    IReadOnlyList<string> SmsNumbers { get; }
    Task<UserPreferences> GetPreferencesAsync();
    Task<UserPreferences> SetPreferencesAsync(IReadOnlyList<PreferenceInput> preferences);

    /// <summary>Fire OS ADM kayıt kimliği (yalnızca Android; diğer hedeflerde etkisiz).</summary>
    void RegisterAdm(string token);

    // Reklam rızası ve reklam kimliği (ADR-0037)
    Task<UserPreferences> SetAdsPersonalizationConsentAsync(bool granted);
    Task<IReadOnlyList<UserConsent>> GetConsentsAsync();
    void SetAdvertisingId(string? id);

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

    /// <summary>Rozet sayısı (negatif değildir). Rozet ayarlandıysa/gösterilebiliyorsa true; yerel SDK olmayan hedefte false.</summary>
    bool SetBadgeCount(int count);

    /// <summary>Güncel rozet sayısı (yerel SDK olmayan hedefte 0).</summary>
    int BadgeCount { get; }

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
    Task<ContentCardFeed> ContentCardsListAsync(int limit);
    Task<ContentCard> ContentCardEventAsync(string id, string eventName);

    // Canlı sohbet (ADR-0040): yöntem adı + JSON argümanları yerel SDK'ya iletilir (bkz. INativeBridge.ChatCall)
    Task<string?> ChatCallAsync(string method, string argsJson);

    // Hazır sohbet arayüzü (yerel SDK'nın ekranı)
    void ChatShow();
    bool ChatHide();
    bool ChatIsPresented { get; }

    // Oturum
    void SendEvent(string name, Dictionary<string, object?> properties, double? value, string? eventId);

    // Live Activities (yalnızca iOS; girdiler doğrulanmış ve normalize edilmiştir: değerler string, long, double, bool)
    Task SetupDefaultLiveActivityAsync();
    Task StartDefaultLiveActivityAsync(string activityId, IReadOnlyDictionary<string, object> attributes, IReadOnlyDictionary<string, object> contentState);

    // Konum paylaşımı (v2 Faz 17b; yalnızca ön plan, gönderim yerel SDK'da)
    bool LocationShared { get; }
    void SetLocationShared(bool shared);

    /// <summary><paramref name="rationale"/> boş değilse kırpılmıştır; null ise yerel SDK'nın varsayılan metni kullanılır.</summary>
    Task<bool> RequestLocationPermissionAsync(string? rationale);

    bool LocationPermissionGranted { get; }
    bool LocationBackgroundPermissionGranted { get; }

    /// <summary>Arka plan ("her zaman") konum izni; <paramref name="rationale"/> yalnızca Android'de kullanılır.</summary>
    Task<bool> RequestBackgroundLocationPermissionAsync(string? rationale);

    // Arka plan bölge izleme (ADR-0065; varsayılan kapalı, izinler uygulamanındır)
    bool GeofencingEnabled { get; }
    void SetGeofencingEnabled(bool enabled);

    // Android teslimat güçlendirme (ADR-0092; yalnızca Android, diğer hedeflerde "desteklenmiyor" / false)
    DeliveryStatus GetDeliveryStatus();
    bool ShouldShowDeliveryGuidance(int minIntervalDays);
    bool OpenDeliverySettings(DeliverySetting setting);

    /// <summary><paramref name="message"/> boş değilse kırpılmıştır; null ise yerel SDK'nın yerelleştirilmiş metni kullanılır.</summary>
    Task<bool> ShowDeliveryGuidanceAsync(DeliverySetting setting, string? message);

    // Özellik bayrakları (ADR-0066; değerler yerel SDK'da saklanır)
    Task<bool> RefreshFeatureFlagsAsync();
    bool IsFeatureFlagEnabled(string key, bool defaultValue);

    /// <summary>Değer yoksa null (varsayılan çağıran katmanda uygulanır).</summary>
    object? GetFeatureFlagValue(string key);

    // Gömülü yerleşimler (ADR-0068; anahtar ve limit doğrulanmıştır)
    Task<IReadOnlyList<InAppMessage>> PlacementsListAsync(string key, int limit);
    void PlacementsReportImpression(string messageId);
    void PlacementsReportClick(string messageId, string buttonId);
    void PlacementsReportDismiss(string messageId);
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
    void OnChatEvent(ChatEventArgs args);
    void OnChatOpened(ChatOpenedEventArgs args);
    void OnFeatureFlagsChanged();
    void OnInboxChanged(InboxChangedEventArgs args);
}

internal enum InAppLifecyclePhase
{
    WillDisplay,
    DidDisplay,
    WillDismiss,
    DidDismiss,
}

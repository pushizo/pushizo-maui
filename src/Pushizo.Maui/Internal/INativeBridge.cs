namespace PushizoSdk.Maui.Internal;

/// <summary>
/// Yerel köprü sonucu: başarıda <paramref name="json"/> (işleme göre null olabilir), hatada
/// <paramref name="errorCode"/> + <paramref name="errorMessage"/>. Herhangi bir iş parçacığından çağrılabilir.
/// </summary>
internal delegate void NativeResult(string? json, string? errorCode, string? errorMessage);

/// <summary>
/// Yerel köprü sözleşmesi: Android'de <c>com.pushizo.maui.PushizoMauiBridge</c> (Kotlin), iOS'ta
/// <c>HSMauiBridge</c> (@objc Swift cephesi). Tüm karmaşık değerler JSON metni olarak taşınır; biçimler
/// <see cref="BridgeJson"/> ve <see cref="BridgeEvents"/> ile ayrıştırılır. Girdiler bu katmana ulaşmadan doğrulanmıştır.
/// </summary>
internal interface INativeBridge
{
    /// <summary>
    /// Yerel SDK'yı başlatır ve olay dinleyicisini bağlar. <paramref name="optionsJson"/> biçimi
    /// <see cref="BridgeJson.OptionsToJson"/>; olaylar <c>(tür, json)</c> olarak <paramref name="onEvent"/>'e gelir.
    /// </summary>
    void Start(string appId, string optionsJson, Action<string, string> onEvent);

    void SetConsentGiven(bool given);
    /// <summary>Kanıt app'in kimlik doğrulama kipine uymuyorsa hata mesajı döner (giriş yapılmaz); başarıda null.</summary>
    string? Login(string externalId, string? identityHash, string? jwt);

    /// <summary>
    /// Profille giriş: <paramref name="profileJson"/> <c>{"email","sms","tags":{},"aliases":{}}</c> (alanlar isteğe bağlı).
    /// Android'de yerel <c>Pushizo.login(externalId, HsUserProfile, …)</c>; iOS'ta köprü aynı sırayı uygular (doğrula, giriş,
    /// e-posta/SMS/tag/alias). Geçersiz profil veya kanıtta hata mesajı döner (giriş yapılmaz); başarıda null.
    /// </summary>
    string? LoginWithProfile(string externalId, string profileJson, string? identityHash, string? jwt);

    /// <summary>Kimlik JWT'sini yeniler; giriş yapılmamışsa hata mesajı döner, başarıda null.</summary>
    string? UpdateUserJwt(string jwt);
    void Logout();

    /// <summary>0 none … 4 debug (yerel SDK'larla aynı sıra).</summary>
    void SetLogLevel(int level);

    string? GetExternalId();
    string? GetUserId();

    /// <summary><c>{"anahtar":"değer"}</c>.</summary>
    void AddTags(string tagsJson);

    /// <summary><c>["anahtar"]</c>.</summary>
    void RemoveTags(string keysJson);

    /// <summary><c>{"anahtar":"değer"}</c>.</summary>
    string GetTags();

    void AddAliases(string aliasesJson);
    void RemoveAlias(string label);
    void SetLanguage(string languageCode);
    void AddEmail(string email);
    void RemoveEmail(string email);
    void AddSms(string phoneNumber);
    void RemoveSms(string phoneNumber);

    /// <summary>
    /// Fire OS ADM kayıt kimliği (yalnızca Android; iOS'ta etkisiz, null döner). Geçersiz kimlikte hata mesajı, başarıda null.
    /// </summary>
    string? RegisterAdm(string token);

    /// <summary><c>["adres"]</c>.</summary>
    string GetEmails();

    /// <summary><c>["+905…"]</c>.</summary>
    string GetSmsNumbers();

    /// <summary><c>{"id","token","optedIn","permission"}</c>.</summary>
    string GetPushSubscription();

    void OptIn();
    void OptOut();
    bool GetPermission();

    /// <summary>İzin ister; <paramref name="completion"/> güncel izin durumuyla bir kez çağrılır.</summary>
    void RequestPermission(bool fallbackToSettings, Action<bool> completion);

    void ClearAllNotifications();

    /// <summary>
    /// Tek bildirimi kaldırır. Android: tepsi kimliği (tamsayı metni); iOS: <c>UNNotificationRequest.identifier</c>.
    /// </summary>
    void RemoveNotification(string id);

    /// <summary>Android <c>android_group</c> / iOS <c>thread-id</c> grubundaki bildirimleri kaldırır.</summary>
    void RemoveGroupedNotifications(string group);

    void AddTriggers(string triggersJson);
    void RemoveTriggers(string keysJson);
    void ClearTriggers();
    string GetTriggers();
    void SetScreen(string? name);
    bool GetInAppPaused();
    void SetInAppPaused(bool paused);

    /// <summary>Başarı JSON'u: <c>{"items":[…],"unreadCount","nextCursor","hasMore"}</c>.</summary>
    void InboxList(int limit, string? cursor, NativeResult result);

    /// <summary>Başarı JSON'u: öğe.</summary>
    void InboxMarkRead(string id, NativeResult result);

    /// <summary>Başarı JSON'u: <c>{"unreadCount":n}</c>.</summary>
    void InboxMarkAllRead(NativeResult result);

    /// <summary>Başarı JSON'u: null.</summary>
    void InboxArchive(string id, NativeResult result);

    /// <summary>Başarı JSON'u: <c>{"unreadCount":n}</c>.</summary>
    void InboxUnreadCount(NativeResult result);

    /// <summary>İçerik kartları (ADR-0082): <c>{"cards":[…],"unviewedCount":n}</c>.</summary>
    void ContentCardsList(int limit, NativeResult result);

    /// <summary>Kart olayı (<c>impression</c>, <c>click</c>, <c>dismiss</c>); güncel kartı döndürür.</summary>
    void ContentCardEvent(string id, string eventName, NativeResult result);

    /// <summary>
    /// Başarı JSON'u: <c>{"preferences":[{"category","channel","optedIn","explicit","source","updatedAt"}],
    /// "categories":[{"key","name":{},"description":{},"channels":[…]|null,"defaultOptedIn"}]}</c>.
    /// </summary>
    void GetPreferences(NativeResult result);

    /// <summary>
    /// <paramref name="preferencesJson"/>: <c>[{"category","channel":null|"…","optedIn"}]</c>; başarı JSON'u
    /// <see cref="GetPreferences"/> ile aynı (güncel liste).
    /// </summary>
    void SetPreferences(string preferencesJson, NativeResult result);

    /// <summary>Reklam kişiselleştirme rızası (ADR-0037); başarı JSON'u <see cref="GetPreferences"/> ile aynı.</summary>
    void SetAdsPersonalizationConsent(bool granted, NativeResult result);

    /// <summary>Başarı JSON'u: <c>{"consents":[{"purpose","granted","source","updatedAt"}]}</c> (zaman epoch ms).</summary>
    void GetConsents(NativeResult result);

    /// <summary>Mobil reklam kimliği (GAID/IDFA; ADR-0037); null veya sıfır kimlik sunucudaki kaydı siler.</summary>
    void SetAdvertisingId(string? id);

    /// <summary>Uygulama rozetini ayarlar (yalnızca iOS; Android'de etkisiz: rozet görünen bildirimlerden hesaplanır).</summary>
    void SetBadgeCount(int count);

    /// <summary><paramref name="propertiesJson"/> JSON nesnesi; <paramref name="hasValue"/> false ise değer yok sayılır.</summary>
    void SendEvent(string name, string propertiesJson, double value, bool hasValue);

    /// <summary>
    /// Yerel SDK'da Live Activities var mı (yalnızca iOS). false ise <see cref="BridgePlatform"/> çağrıları köprüye
    /// iletmeden <c>PLATFORM_NOT_SUPPORTED</c> ile reddeder.
    /// </summary>
    bool SupportsLiveActivities { get; }

    /// <summary>
    /// Genel <c>PushizoDefaultLiveActivityAttributes</c> türünü izlemeye alır. Başarı JSON'u: null; hata kodları
    /// <c>PLATFORM_NOT_SUPPORTED</c> | <c>NOT_READY</c> | <c>INVALID_ARGUMENT</c> | <c>LIVE_ACTIVITY_FAILED</c>.
    /// </summary>
    void SetupDefaultLiveActivity(NativeResult result);

    /// <summary>
    /// Genel türle yerel etkinlik başlatır. <paramref name="attributesJson"/> / <paramref name="contentStateJson"/>: düz
    /// JSON nesnesi (değerler metin, sayı, bool). Başarı JSON'u: null; hata kodları <see cref="SetupDefaultLiveActivity"/> ile aynı.
    /// </summary>
    void StartDefaultLiveActivity(string activityId, string attributesJson, string contentStateJson, NativeResult result);

    /// <summary>Konum paylaşımı açık mı (yerel SDK'daki kalıcı tercih; izin durumundan bağımsız).</summary>
    bool GetLocationShared();

    /// <summary>Konum paylaşımını açar/kapatır; kapatınca yerel SDK sunucuya <c>location: null</c> gönderir.</summary>
    void SetLocationShared(bool shared);

    /// <summary>
    /// "Kullanırken" konum iznini ister: izin yoksa önce açıklama diyaloğu (<paramref name="rationale"/>, null ise yerel
    /// SDK'nın tr/en varsayılan metni), ardından sistem istemi. <paramref name="completion"/> güncel izin durumuyla bir kez çağrılır.
    /// </summary>
    void RequestLocationPermission(string? rationale, Action<bool> completion);

    /// <summary>Konum izni (yaklaşık/hassas; iOS'ta "kullanırken" veya "her zaman") verilmiş mi.</summary>
    bool GetLocationPermissionGranted();

    /// <summary>Arka plan konum izni (Android <c>ACCESS_BACKGROUND_LOCATION</c>, iOS "her zaman") verilmiş mi.</summary>
    bool GetLocationBackgroundPermissionGranted();

    /// <summary>
    /// Arka plan konum iznini ister (Android: açıklama diyaloğu + sistem istemi; iOS: <c>requestAlwaysPermission</c>,
    /// <paramref name="rationale"/> yok sayılır). <paramref name="completion"/> güncel izin durumuyla bir kez çağrılır.
    /// </summary>
    void RequestBackgroundLocationPermission(string? rationale, Action<bool> completion);

    /// <summary>Arka plan bölge izleme açık mı (yerel SDK'daki kalıcı tercih; ADR-0065, varsayılan kapalı).</summary>
    bool GetGeofencingEnabled();

    /// <summary>Arka plan bölge izlemeyi açar/kapatır; köprü izin istemez (izinler uygulamanındır).</summary>
    void SetGeofencingEnabled(bool enabled);

    /// <summary>
    /// Android teslim durumu (ADR-0092) JSON nesnesi olarak: <c>manufacturer</c>, <c>brand</c>, <c>vendor</c>,
    /// <c>aggressiveVendor</c>, <c>rom</c>, <c>romVersion</c>, <c>notificationsEnabled</c>, <c>channelBlocked</c>,
    /// <c>batteryOptimizationExempt</c>, <c>backgroundRestricted</c>, <c>standbyBucket</c>, <c>powerSaveMode</c>,
    /// <c>dataSaverEnabled</c>, <c>restricted</c>, <c>risk</c>, <c>recommendedSettings</c>. Teşhisin olmadığı platformda
    /// (iOS) veya yerel SDK başlatılmadıysa null.
    /// </summary>
    string? DeliveryStatus();

    /// <summary>Teslim riski var ve hazır diyalog son <paramref name="minIntervalDays"/> günde gösterilmedi mi (Android dışında false).</summary>
    bool DeliveryShouldShowGuidance(int minIntervalDays);

    /// <summary>
    /// Ayar ekranını açar. <paramref name="setting"/>: <c>auto</c> | <c>notifications</c> | <c>auto_start</c> |
    /// <c>battery_optimization</c> | <c>app_details</c>. Ekran açıldıysa true (Android dışında false); istisna atmaz.
    /// </summary>
    bool DeliveryOpenSettings(string setting);

    /// <summary>
    /// Hazır açıklama diyaloğu (<paramref name="message"/> null ise yerel SDK'nın yerelleştirilmiş metni).
    /// <paramref name="completion"/> bir kez çağrılır: kullanıcı kabul etti ve ayar ekranı açıldı mı (Android dışında false).
    /// </summary>
    void DeliveryShowGuidance(string setting, string? message, Action<bool> completion);

    /// <summary>Özellik bayraklarını hemen yeniler (ADR-0066). Başarı JSON'u: <c>{"refreshed":bool}</c>.</summary>
    void FeatureFlagsRefresh(NativeResult result);

    /// <summary>Bayrak açık mı; değer henüz yoksa <paramref name="fallback"/>.</summary>
    bool FeatureFlagEnabled(string key, bool fallback);

    /// <summary><c>{"value":…}</c> (metin, sayı, mantıksal, nesne veya dizi); değer yoksa null.</summary>
    string? FeatureFlagValue(string key);

    /// <summary>
    /// Gömülü yerleşim içeriği (ADR-0068). Başarı JSON'u: <c>{"messages":[{"messageId","json"}]}</c>; köprü dönen
    /// mesajları rapor çağrıları için kimliğe göre sınırlı bir önbellekte tutar.
    /// </summary>
    void PlacementsList(string key, int limit, NativeResult result);

    /// <summary>Gösterim bildirimi; mesaj köprü önbelleğinde yoksa yok sayılır.</summary>
    void PlacementsReportImpression(string messageId);

    /// <summary>Tıklama bildirimi; mesaj köprü önbelleğinde yoksa yok sayılır.</summary>
    void PlacementsReportClick(string messageId, string buttonId);

    /// <summary>Kapatma bildirimi; mesaj köprü önbelleğinde yoksa yok sayılır.</summary>
    void PlacementsReportDismiss(string messageId);

    /// <summary>
    /// Canlı sohbet çağrısı (ADR-0040). <paramref name="method"/>: start, getConfig, getConversation, getMessages,
    /// sendMessage, sendAttachment (base64), markRead, refreshUnreadCount, getUnreadCount, leaveOfflineMessage, setTyping,
    /// startListening, stopListening, isListening, attachmentUrl, getVisitorId. Başarı JSON'u yerel modelin camelCase sözlüğü
    /// (sayaçlar <c>{"unreadCount"}</c>, adres <c>{"url"}</c>, kimlik <c>{"visitorId"}</c>, dinleme <c>{"listening"}</c>;
    /// dönüşsüz işlemlerde null).
    /// </summary>
    void ChatCall(string method, string argsJson, NativeResult result);

    /// <summary>Ön plan olayının (<c>foreground</c> olayındaki <c>eventId</c>) yerel gösterimini engeller.</summary>
    void ForegroundPreventDefault(string eventId);

    /// <summary>Ön plan olayının bildirimini şimdi gösterir (yalnızca bir kez).</summary>
    void ForegroundDisplay(string eventId);
}

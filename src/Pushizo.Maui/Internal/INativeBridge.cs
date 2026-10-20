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

    /// <summary>
    /// Canlı sohbet çağrısı (ADR-0040). <paramref name="method"/>: start, getConfig, getConversation, getMessages,
    /// sendMessage, sendAttachment (base64), markRead, refreshUnreadCount, getUnreadCount, leaveOfflineMessage, setTyping,
    /// startListening, stopListening, attachmentUrl, getVisitorId. Başarı JSON'u yerel modelin camelCase sözlüğü
    /// (sayaçlar <c>{"unreadCount"}</c>, adres <c>{"url"}</c>, kimlik <c>{"visitorId"}</c>; dönüşsüz işlemlerde null).
    /// </summary>
    void ChatCall(string method, string argsJson, NativeResult result);

    /// <summary>Ön plan olayının (<c>foreground</c> olayındaki <c>eventId</c>) yerel gösterimini engeller.</summary>
    void ForegroundPreventDefault(string eventId);

    /// <summary>Ön plan olayının bildirimini şimdi gösterir (yalnızca bir kez).</summary>
    void ForegroundDisplay(string eventId);
}

namespace HeroSignalSdk.Maui.Internal;

/// <summary>
/// Yerel köprü sonucu: başarıda <paramref name="json"/> (işleme göre null olabilir), hatada
/// <paramref name="errorCode"/> + <paramref name="errorMessage"/>. Herhangi bir iş parçacığından çağrılabilir.
/// </summary>
internal delegate void NativeResult(string? json, string? errorCode, string? errorMessage);

/// <summary>
/// Yerel köprü sözleşmesi: Android'de <c>com.herosignal.maui.HeroSignalMauiBridge</c> (Kotlin), iOS'ta
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
    void Login(string externalId, string? identityHash);
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

    /// <summary>Ön plan olayının (<c>foreground</c> olayındaki <c>eventId</c>) yerel gösterimini engeller.</summary>
    void ForegroundPreventDefault(string eventId);

    /// <summary>Ön plan olayının bildirimini şimdi gösterir (yalnızca bir kez).</summary>
    void ForegroundDisplay(string eventId);
}

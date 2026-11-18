namespace PushizoSdk.Maui;

/// <summary>SDK günlük seviyesi (yerel SDK'lardaki <c>HsLog.Level</c> ile aynı sıra).</summary>
public enum LogLevel
{
    None = 0,
    Error = 1,
    Warn = 2,
    Info = 3,
    Debug = 4,
}

/// <summary>Bildirim aksiyon düğmesi.</summary>
public sealed record NotificationAction(string Id, string Title, string? Url, string? Icon);

/// <summary>Pushizo push bildirimi (tıklama ve ön plan olaylarında).</summary>
public sealed record PushizoNotification
{
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? ImageUrl { get; init; }
    public string? Url { get; init; }
    /// <summary>Teslimat kimliği (<c>hs.dlv</c>); Mesaj İzleme'deki kayıtla eşleşir.</summary>
    public string? DeliveryId { get; init; }
    public string? MessageId { get; init; }
    public IReadOnlyList<NotificationAction> Actions { get; init; } = Array.Empty<NotificationAction>();
    /// <summary>Müşteri verileri (<c>hs_</c> önekli alanlar hariç).</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = EmptyStringMap.Instance;
    /// <summary>
    /// Yalnızca Android: tepsi kimliği (<see cref="NotificationsManager.RemoveNotification(int)"/> için); iOS'ta null.
    /// </summary>
    public int? AndroidNotificationId { get; init; }
}

/// <summary>Push aboneliği durumu.</summary>
/// <param name="Id">Sunucudaki abonelik kimliği; kayıt tamamlanana kadar null.</param>
/// <param name="Token">FCM/APNs token'ı.</param>
/// <param name="OptedIn">Kullanıcı push almayı kabul etti ve sistem izni var.</param>
/// <param name="Permission">Sistem bildirim izni.</param>
public sealed record PushSubscriptionState(string? Id, string? Token, bool OptedIn, bool Permission)
{
    internal static readonly PushSubscriptionState Empty = new(null, null, false, false);
}

/// <summary>Bildirim kutusu öğesi (<c>docs/messaging.md</c> §8).</summary>
public sealed record InboxItem
{
    public required string Id { get; init; }
    public string? MessageId { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyDictionary<string, string> Data { get; init; } = EmptyStringMap.Instance;
    public bool Read { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>İçerik kartı türü (ADR-0082).</summary>
public enum ContentCardType
{
    Classic,
    CaptionedImage,
    ImageOnly,
}

/// <summary>İçerik kartı (ADR-0082); kartlar bildirim kutusunda görünmez.</summary>
public sealed record ContentCard
{
    public required string Id { get; init; }
    public string? MessageId { get; init; }
    public ContentCardType Type { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public string? ButtonText { get; init; }
    public IReadOnlyDictionary<string, string> Data { get; init; } = EmptyStringMap.Instance;
    public bool Pinned { get; init; }
    public int Priority { get; init; }
    public bool Viewed { get; init; }
    public bool Clicked { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>Kart akışı (sabitlenmiş → öncelik → yeni) ve görülmemiş kart sayısı.</summary>
public sealed record ContentCardFeed(IReadOnlyList<ContentCard> Cards, int UnviewedCount);

/// <summary>Bildirim kutusu sayfası (yeniden eskiye).</summary>
public sealed record InboxPage(IReadOnlyList<InboxItem> Items, int UnreadCount, string? NextCursor, bool HasMore);

/// <summary>
/// Kullanıcı tercihi (ADR-0019 §4): kategori × kanal (<c>null</c> = tüm kanallar) → opt-in. <see cref="Explicit"/> false
/// ise değer kategori varsayılanından türetilmiştir; <see cref="Source"/> kaydı yazan taraftır (sdk, api, preference_center…).
/// </summary>
public sealed record UserPreference
{
    public required string Category { get; init; }
    /// <summary><c>null</c> (tüm kanallar) veya <c>ios_push</c> | <c>android_push</c> | <c>web_push</c> | <c>email</c> | <c>sms</c>.</summary>
    public string? Channel { get; init; }
    public bool OptedIn { get; init; }
    public bool Explicit { get; init; }
    public string? Source { get; init; }
    /// <summary>Türetilmiş değerde null.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>Mesaj kategorisi: çok dilli ad/açıklama (<c>dil kodu → metin</c>), kanal kısıtı (<c>null</c> = tümü) ve varsayılan opt-in.</summary>
public sealed record PreferenceCategory
{
    public required string Key { get; init; }
    public IReadOnlyDictionary<string, string> Name { get; init; } = EmptyStringMap.Instance;
    public IReadOnlyDictionary<string, string> Description { get; init; } = EmptyStringMap.Instance;
    public IReadOnlyList<string>? Channels { get; init; }
    public bool DefaultOptedIn { get; init; } = true;

    /// <summary>Verilen dilde ad; yoksa İngilizce, o da yoksa ilk tanımlı dil, hiçbiri yoksa anahtar.</summary>
    public string LocalizedName(string language) =>
        Name.TryGetValue(language, out var name) ? name
        : Name.TryGetValue("en", out var en) ? en
        : Name.Count > 0 ? Name.OrderBy(p => p.Key, StringComparer.Ordinal).First().Value
        : Key;
}

/// <summary>
/// Rıza kaydı (ADR-0037; ör. <c>ads_personalization</c>). <see cref="Source"/> kaydı yazan taraftır; kayıt yoksa
/// <see cref="UpdatedAt"/> null.
/// </summary>
public sealed record UserConsent
{
    public required string Purpose { get; init; }
    public bool Granted { get; init; }
    public string? Source { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// Girişle birlikte ayarlanan profil: <c>Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" })</c>.
/// Geçersiz e-posta, telefon, tag veya alias girişten önce reddedilir; yarım kalmış bir kimlik değişimi olmaz.
/// </summary>
public readonly record struct PushizoUserProfile
{
    /// <summary>E-posta aboneliği (küçük harfe çevrilir).</summary>
    public string? Email { get; init; }

    /// <summary>SMS numarası (E.164'e normalize edilir).</summary>
    public string? Sms { get; init; }

    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Alias'lar (etiket → kimlik; en fazla 20).</summary>
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}

/// <summary><c>User.GetPreferencesAsync</c> / <c>SetPreferencesAsync</c> sonucu: kullanıcının tercihleri ve app'in kategorileri.</summary>
public sealed record UserPreferences(IReadOnlyList<UserPreference> Preferences, IReadOnlyList<PreferenceCategory> Categories);

/// <summary><c>User.SetPreferencesAsync</c> girdisi; <paramref name="Channel"/> null ise tüm kanallar.</summary>
/// <param name="Category">Kategori anahtarı (1-64 karakter).</param>
/// <param name="OptedIn">Kategoriye (ve kanala) abone mi.</param>
/// <param name="Channel"><c>null</c> veya <c>ios_push</c> | <c>android_push</c> | <c>web_push</c> | <c>email</c> | <c>sms</c>.</param>
public sealed record PreferenceInput(string Category, bool OptedIn, string? Channel = null);

/// <summary>In-app mesaj özeti (yaşam döngüsü olaylarında ve <see cref="PlacementsManager.ListAsync"/> sonucunda).</summary>
/// <param name="Id">Mesaj kimliği.</param>
/// <param name="Json">Sunucunun döndürdüğü ham mesaj JSON'u (özel işleme ve gömülü yerleşim çizimi için).</param>
public sealed record InAppMessage(string Id, string? Json);

/// <summary>Bildirim teslim riski (ADR-0092; yerel Android <c>HsDeliveryRisk</c>).</summary>
public enum DeliveryRisk
{
    /// <summary>Bilinen bir engel yok.</summary>
    None = 0,
    /// <summary>Bildirimler gecikebilir (saldırgan pil yönetimli üretici ve pil muafiyeti yok, seyrek kullanım kovası, veri tasarrufu).</summary>
    Elevated = 1,
    /// <summary>Bildirimler büyük olasılıkla gelmiyor (bildirim izni ya da kanalı kapalı, arka plan etkinliği kısıtlı).</summary>
    High = 2,
}

/// <summary><see cref="DeliveryManager.OpenSettings"/> ve <see cref="DeliveryManager.ShowGuidanceAsync"/> ile açılabilen Android ayar ekranı.</summary>
public enum DeliverySetting
{
    /// <summary>Duruma en uygun ekran (<see cref="DeliveryStatus.RecommendedSettings"/> ilk öğesi; öneri yoksa pil optimizasyonu).</summary>
    Auto = 0,
    /// <summary>Uygulamanın bildirim ayarları.</summary>
    Notifications = 1,
    /// <summary>Üreticinin otomatik başlatma / arka planda çalışma yöneticisi (yoksa uygulama ayrıntıları).</summary>
    AutoStart = 2,
    /// <summary>Pil optimizasyonu: üreticinin pil yöneticisi, yoksa sistemin pil optimizasyonu listesi.</summary>
    BatteryOptimization = 3,
    /// <summary>Uygulama ayrıntıları (arka plan kısıtı buradan kaldırılır).</summary>
    AppDetails = 4,
}

/// <summary>
/// Cihazın bildirim teslim durumu (<see cref="DeliveryManager.GetStatus"/>; yerel Android <c>HsDeliveryStatus</c>).
/// Kişisel veri içermez: üretici, ROM ve kısıt bayrakları. Yalnızca Android'de doludur; iOS, Windows ve MacCatalyst
/// hedeflerinde ve SDK başlatılmadan önce <see cref="Supported"/> false, <see cref="Risk"/> <see cref="DeliveryRisk.None"/>
/// ve <see cref="RecommendedSettings"/> boştur.
/// </summary>
public sealed record DeliveryStatus
{
    /// <summary>Teşhisin desteklenmediği platformların durumu (risk yok).</summary>
    public static DeliveryStatus Unsupported { get; } = new();

    /// <summary>Teşhis bu platformda okunabildi mi.</summary>
    public bool Supported { get; init; }
    /// <summary><c>Build.MANUFACTURER</c> (küçük harf).</summary>
    public string Manufacturer { get; init; } = string.Empty;
    /// <summary><c>Build.BRAND</c> (küçük harf).</summary>
    public string Brand { get; init; } = string.Empty;
    /// <summary>Üretici ailesi: <c>xiaomi</c>, <c>huawei</c>, <c>honor</c>, <c>oppo</c>, <c>realme</c>, <c>oneplus</c>, <c>vivo</c>, <c>samsung</c>, <c>meizu</c>, <c>asus</c>, <c>transsion</c>, <c>other</c>.</summary>
    public string Vendor { get; init; } = "other";
    /// <summary>Üreticinin kendi pil/otomatik başlatma yöneticisi arka plandaki uygulamaları durdurabiliyor mu.</summary>
    public bool AggressiveVendor { get; init; }
    /// <summary>ROM kimliği: <c>miui</c>, <c>hyperos</c>, <c>emui</c>, <c>harmonyos</c>, <c>magicos</c>, <c>coloros</c>, <c>oxygenos</c>, <c>realmeui</c>, <c>funtouchos</c>, <c>originos</c>, <c>oneui</c>, <c>flyme</c>, <c>android</c>.</summary>
    public string Rom { get; init; } = string.Empty;
    public string? RomVersion { get; init; }
    /// <summary>Bildirim izni verilmiş ve bildirimler açık mı.</summary>
    public bool NotificationsEnabled { get; init; } = true;
    /// <summary>Varsayılan bildirim kanalı kullanıcı tarafından kapatılmış mı.</summary>
    public bool ChannelBlocked { get; init; }
    /// <summary>Uygulama pil optimizasyonundan muaf mı.</summary>
    public bool BatteryOptimizationExempt { get; init; } = true;
    /// <summary>Kullanıcı arka plan etkinliğini kısıtlamış mı.</summary>
    public bool BackgroundRestricted { get; init; }
    /// <summary>Bekleme kovası: <c>exempted</c>, <c>active</c>, <c>working_set</c>, <c>frequent</c>, <c>rare</c>, <c>restricted</c> (okunamadıysa null).</summary>
    public string? StandbyBucket { get; init; }
    /// <summary>Pil tasarrufu kipi açık mı.</summary>
    public bool PowerSaveMode { get; init; }
    /// <summary>Veri tasarrufu uygulamanın arka plan verisini engelliyor mu.</summary>
    public bool DataSaverEnabled { get; init; }
    /// <summary>Sistem arka plan kısıtı: kullanıcı kısıtlamış ya da uygulama <c>rare</c>/<c>restricted</c> kovasında.</summary>
    public bool Restricted { get; init; }
    public DeliveryRisk Risk { get; init; } = DeliveryRisk.None;
    /// <summary>Kullanıcının açması önerilen ayar ekranları (önem sırasıyla; öneri yoksa boş).</summary>
    public IReadOnlyList<DeliverySetting> RecommendedSettings { get; init; } = Array.Empty<DeliverySetting>();
}

/// <summary>SDK çağrısı hatası. <see cref="Code"/> sunucu hata kodu (ör. <c>NOT_FOUND</c>) veya SDK durum kodudur.</summary>
public sealed class PushizoException : Exception
{
    public const string NotInitialized = "NOT_INITIALIZED";
    public const string NotReady = "NOT_READY";
    public const string PlatformNotSupported = "PLATFORM_NOT_SUPPORTED";
    public const string InvalidResponse = "INVALID_RESPONSE";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    /// <summary>iOS Live Activity başlatılamadı (Live Activities kapalı, <c>NSSupportsLiveActivities</c> yok vb.).</summary>
    public const string LiveActivityFailed = "LIVE_ACTIVITY_FAILED";

    public PushizoException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Hata kodu.</summary>
    public string Code { get; }

    /// <summary>HTTP durum kodu (sunucu hatalarında; aksi halde null).</summary>
    public int? Status { get; init; }
}

internal static class EmptyStringMap
{
    public static readonly IReadOnlyDictionary<string, string> Instance =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

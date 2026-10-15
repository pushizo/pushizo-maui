namespace HeroSignalSdk.Maui;

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

/// <summary>HeroSignal push bildirimi (tıklama ve ön plan olaylarında).</summary>
public sealed record HeroSignalNotification
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

/// <summary><c>User.GetPreferencesAsync</c> / <c>SetPreferencesAsync</c> sonucu: kullanıcının tercihleri ve app'in kategorileri.</summary>
public sealed record UserPreferences(IReadOnlyList<UserPreference> Preferences, IReadOnlyList<PreferenceCategory> Categories);

/// <summary><c>User.SetPreferencesAsync</c> girdisi; <paramref name="Channel"/> null ise tüm kanallar.</summary>
/// <param name="Category">Kategori anahtarı (1-64 karakter).</param>
/// <param name="OptedIn">Kategoriye (ve kanala) abone mi.</param>
/// <param name="Channel"><c>null</c> veya <c>ios_push</c> | <c>android_push</c> | <c>web_push</c> | <c>email</c> | <c>sms</c>.</param>
public sealed record PreferenceInput(string Category, bool OptedIn, string? Channel = null);

/// <summary>In-app mesaj özeti (yaşam döngüsü olaylarında).</summary>
/// <param name="Id">Mesaj kimliği.</param>
/// <param name="Json">Sunucunun döndürdüğü ham mesaj JSON'u (özel işleme için).</param>
public sealed record InAppMessage(string Id, string? Json);

/// <summary>SDK çağrısı hatası. <see cref="Code"/> sunucu hata kodu (ör. <c>NOT_FOUND</c>) veya SDK durum kodudur.</summary>
public sealed class HeroSignalException : Exception
{
    public const string NotInitialized = "NOT_INITIALIZED";
    public const string NotReady = "NOT_READY";
    public const string PlatformNotSupported = "PLATFORM_NOT_SUPPORTED";
    public const string InvalidResponse = "INVALID_RESPONSE";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    /// <summary>iOS Live Activity başlatılamadı (Live Activities kapalı, <c>NSSupportsLiveActivities</c> yok vb.).</summary>
    public const string LiveActivityFailed = "LIVE_ACTIVITY_FAILED";

    public HeroSignalException(string code, string message, Exception? innerException = null)
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

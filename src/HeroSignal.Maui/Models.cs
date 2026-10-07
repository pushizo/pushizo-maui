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

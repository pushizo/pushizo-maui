using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui;

/// <summary>
/// Bildirimler (<c>HeroSignal.Notifications</c>): izin, tıklama, ön plan gösterimi ve bildirim merkezini temizleme.
/// Olaylar yerel SDK'nın iş parçacığında tetiklenir; arayüz güncellemesi için
/// <c>MainThread.BeginInvokeOnMainThread</c> kullanın.
/// </summary>
public sealed class NotificationsManager
{
    private readonly object _gate = new();
    private EventHandler<NotificationClickedEventArgs>? _clicked;
    private NotificationClickedEventArgs? _pendingClick;
    private bool? _lastPermission;

    internal NotificationsManager()
    {
    }

    /// <summary>
    /// Bildirime (veya aksiyon düğmesine) tıklandı. Uygulama bildirimle açıldığında henüz abone yoksa son tıklama
    /// saklanır ve ilk abone eklendiğinde iletilir.
    /// </summary>
    public event EventHandler<NotificationClickedEventArgs>? Clicked
    {
        add
        {
            NotificationClickedEventArgs? pending;
            lock (_gate)
            {
                _clicked += value;
                pending = _pendingClick;
                _pendingClick = null;
            }
            if (pending is not null && value is not null) HeroSignal.Raise(value, pending, "Notifications.Clicked");
        }
        remove
        {
            lock (_gate) _clicked -= value;
        }
    }

    /// <summary>
    /// Uygulama ön plandayken bildirim geldi; <see cref="NotificationWillDisplayEventArgs.PreventDefault"/> ile
    /// gösterim engellenir (işleyici içinde eşzamanlı çağrılmalıdır).
    /// </summary>
    public event EventHandler<NotificationWillDisplayEventArgs>? ForegroundWillDisplay;

    /// <summary>Sistem bildirim izni değişti (yalnızca gerçek değişiklikte).</summary>
    public event EventHandler<NotificationPermissionChangedEventArgs>? PermissionChanged;

    /// <summary>Sistem bildirim izni var mı (Android 13+ POST_NOTIFICATIONS; iOS authorized/provisional).</summary>
    public bool Permission => HeroSignal.PlatformOrNull()?.NotificationPermission ?? false;

    /// <summary>
    /// Sistem bildirim iznini ister; sonuç izin durumudur. <paramref name="fallbackToSettings"/> true ise izin daha önce
    /// reddedildiğinde uygulamanın bildirim ayarları açılır.
    /// </summary>
    public Task<bool> RequestPermissionAsync(bool fallbackToSettings = false) =>
        HeroSignal.Platform().RequestPermissionAsync(fallbackToSettings);

    /// <summary>Bildirim merkezindeki bu uygulamaya ait tüm bildirimleri kaldırır (iOS'ta rozet de sıfırlanır).</summary>
    public void ClearAll() => HeroSignal.Platform().ClearAllNotifications();

    /// <summary>
    /// Tek bir HeroSignal bildirimini Android tepsi kimliğiyle (<see cref="HeroSignalNotification.AndroidNotificationId"/>)
    /// kaldırır. iOS'ta kimlik metne çevrilerek <c>UNNotificationRequest.identifier</c> olarak aranır.
    /// </summary>
    public void RemoveNotification(int androidNotificationId) =>
        HeroSignal.Platform().RemoveNotification(androidNotificationId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Tek bildirimi kaldırır. iOS: bildirim isteği kimliği (<c>UNNotificationRequest.identifier</c>). Android: tepsi
    /// kimliğinin metni; tamsayıya çevrilemezse <see cref="ArgumentException"/> fırlatılır.
    /// </summary>
    public void RemoveNotification(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        HeroSignal.Platform().RemoveNotification(identifier.Trim());
    }

    /// <summary>Aynı gruptaki bildirimleri kaldırır (Android <c>android_group</c>, iOS <c>thread-id</c>).</summary>
    public void RemoveGroupedNotifications(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        HeroSignal.Platform().RemoveGroupedNotifications(group);
    }

    internal void DispatchClicked(NotificationClickedEventArgs args)
    {
        EventHandler<NotificationClickedEventArgs>? handler;
        lock (_gate)
        {
            handler = _clicked;
            if (handler is null)
            {
                _pendingClick = args;
                return;
            }
        }
        HeroSignal.Raise(handler, args, "Notifications.Clicked");
    }

    internal void DispatchWillDisplay(NotificationWillDisplayEventArgs args) =>
        HeroSignal.Raise(ForegroundWillDisplay, args, "Notifications.ForegroundWillDisplay");

    internal void DispatchPermission(bool permission)
    {
        lock (_gate)
        {
            var previous = _lastPermission;
            _lastPermission = permission;
            // İlk gözlem (Initialize sırasında) başlangıç durumudur; değişiklik sayılmaz.
            if (previous is null || previous == permission) return;
        }
        HeroSignal.Raise(PermissionChanged, new NotificationPermissionChangedEventArgs(permission), "Notifications.PermissionChanged");
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _clicked = null;
            _pendingClick = null;
            _lastPermission = null;
        }
        ForegroundWillDisplay = null;
        PermissionChanged = null;
    }
}

/// <summary>
/// In-app mesajlar (<c>HeroSignal.InAppMessages</c>): tetikler yerel SDK'da değerlendirilir (<c>session_start</c>,
/// <see cref="SessionManager.SendEvent"/> adı, <see cref="SetScreen"/>, <see cref="AddTrigger"/> koşulları);
/// mesajlar yerel arayüzle çizilir. Aynı anda tek mesaj gösterilir.
/// </summary>
public sealed class InAppMessagesManager
{
    internal InAppMessagesManager()
    {
    }

    /// <summary>Mesaj düğmesine tıklandı.</summary>
    public event EventHandler<InAppMessageClickedEventArgs>? Clicked;

    /// <summary>Mesaj gösterilmek üzere.</summary>
    public event EventHandler<InAppMessageLifecycleEventArgs>? WillDisplay;

    /// <summary>Mesaj gösterildi (gösterim kaydedildi).</summary>
    public event EventHandler<InAppMessageLifecycleEventArgs>? DidDisplay;

    /// <summary>Mesaj kapanmak üzere.</summary>
    public event EventHandler<InAppMessageLifecycleEventArgs>? WillDismiss;

    /// <summary>Mesaj kapandı.</summary>
    public event EventHandler<InAppMessageLifecycleEventArgs>? DidDismiss;

    public void AddTrigger(string key, string value) => AddTriggers(new Dictionary<string, string> { [key] = value });

    /// <summary>Trigger ekler/günceller (anahtar 1-64, değer en fazla 200 karakter); bu cihazda kalıcıdır.</summary>
    public void AddTriggers(IReadOnlyDictionary<string, string> triggers)
    {
        var valid = Validation.RequireTriggers(triggers, nameof(triggers));
        if (valid.Count == 0) return;
        HeroSignal.Platform().AddTriggers(valid);
    }

    public void RemoveTrigger(string key) => RemoveTriggers(new[] { key });

    public void RemoveTriggers(IEnumerable<string> keys)
    {
        var valid = Validation.RequireKeys(keys, nameof(keys));
        if (valid.Count == 0) return;
        HeroSignal.Platform().RemoveTriggers(valid);
    }

    public void ClearTriggers() => HeroSignal.Platform().ClearTriggers();

    /// <summary>Geçerli trigger değerleri.</summary>
    public IReadOnlyDictionary<string, string> GetTriggers() =>
        HeroSignal.PlatformOrNull()?.GetTriggers() ?? new Dictionary<string, string>();

    /// <summary>Geçerli ekran adı (<c>screen</c> tetikleri); ekrandan çıkarken null. Örn. sayfanın <c>OnAppearing</c>'inde.</summary>
    public void SetScreen(string? name) =>
        HeroSignal.Platform().SetScreen(string.IsNullOrWhiteSpace(name) ? null : name.Trim());

    /// <summary>true iken yeni mesaj gösterilmez (tetikler kaydedilmeye devam eder).</summary>
    public bool Paused
    {
        get => HeroSignal.PlatformOrNull()?.InAppPaused ?? false;
        set => HeroSignal.Platform().InAppPaused = value;
    }

    internal void DispatchClicked(InAppMessageClickedEventArgs args) => HeroSignal.Raise(Clicked, args, "InAppMessages.Clicked");

    internal void DispatchLifecycle(InAppLifecyclePhase phase, InAppMessage message)
    {
        var args = new InAppMessageLifecycleEventArgs(message);
        switch (phase)
        {
            case InAppLifecyclePhase.WillDisplay: HeroSignal.Raise(WillDisplay, args, "InAppMessages.WillDisplay"); break;
            case InAppLifecyclePhase.DidDisplay: HeroSignal.Raise(DidDisplay, args, "InAppMessages.DidDisplay"); break;
            case InAppLifecyclePhase.WillDismiss: HeroSignal.Raise(WillDismiss, args, "InAppMessages.WillDismiss"); break;
            case InAppLifecyclePhase.DidDismiss: HeroSignal.Raise(DidDismiss, args, "InAppMessages.DidDismiss"); break;
        }
    }

    internal void Reset()
    {
        Clicked = null;
        WillDisplay = null;
        DidDisplay = null;
        WillDismiss = null;
        DidDismiss = null;
    }
}

/// <summary>
/// Bildirim kutusu (<c>HeroSignal.Inbox</c>, <c>add_to_inbox</c> mesajları). Abonelik ve kullanıcı kaydı tamamlanmadan
/// çağrılar <see cref="HeroSignalException"/> (<c>NOT_READY</c>) ile sonuçlanır; sunucu hataları sunucu kodunu taşır.
/// </summary>
public sealed class InboxManager
{
    internal InboxManager()
    {
    }

    /// <summary>Öğeler (yeniden eskiye), okunmamış sayısı ve sonraki sayfa imleci (<paramref name="limit"/> 1-200).</summary>
    public Task<InboxPage> ListAsync(int limit = 50, string? cursor = null)
    {
        Validation.RequireInboxLimit(limit, nameof(limit));
        return HeroSignal.Platform().InboxListAsync(limit, string.IsNullOrEmpty(cursor) ? null : cursor);
    }

    /// <summary>Öğeyi okundu işaretler; güncel öğeyi döndürür.</summary>
    public Task<InboxItem> MarkReadAsync(string id) => HeroSignal.Platform().InboxMarkReadAsync(RequireId(id));

    /// <summary>Tümünü okundu işaretler; kalan okunmamış sayısını döndürür.</summary>
    public Task<int> MarkAllReadAsync() => HeroSignal.Platform().InboxMarkAllReadAsync();

    /// <summary>Öğeyi kutudan kaldırır (arşivler).</summary>
    public Task ArchiveAsync(string id) => HeroSignal.Platform().InboxArchiveAsync(RequireId(id));

    /// <summary>Okunmamış öğe sayısı.</summary>
    public Task<int> UnreadCountAsync() => HeroSignal.Platform().InboxUnreadCountAsync();

    private static string RequireId(string id) =>
        string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Öğe kimliği gerekli.", nameof(id)) : id;
}

/// <summary>Oturum ve özel olaylar (<c>HeroSignal.Session</c>).</summary>
public sealed class SessionManager
{
    internal SessionManager()
    {
    }

    /// <summary>
    /// Özel olay / outcome gönderir (çevrimdışıyken kuyrukta bekler); olay adı in-app tetiklerini de çalıştırır.
    /// Özellikler: metin, sayı, bool, tarih, Guid, enum, iç içe sözlük ve liste (en fazla 16 seviye).
    /// </summary>
    public void SendEvent(string name, IReadOnlyDictionary<string, object?>? properties = null, double? value = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Olay adı gerekli.", nameof(name));
        if (value is { } v && !double.IsFinite(v)) throw new ArgumentException("Olay değeri sonlu bir sayı olmalı.", nameof(value));
        var normalized = EventProperties.Normalize(properties, nameof(properties));
        HeroSignal.Platform().SendEvent(name.Trim(), normalized, value);
    }
}

/// <summary>
/// iOS Live Activities (<c>HeroSignal.LiveActivities</c>, ADR-0024). Köprü, genel
/// <c>HeroSignalDefaultLiveActivityAttributes</c> türünü kullanır; sunucuda (dashboard/API) tür adı
/// <see cref="DefaultActivityType"/> olarak tanımlanır ve widget extension'da
/// <c>ActivityConfiguration(for: HeroSignalDefaultLiveActivityAttributes.self)</c> yazılır. Android ve diğer hedeflerde
/// çağrılar <see cref="HeroSignalException"/> (<c>PLATFORM_NOT_SUPPORTED</c>) ile sonuçlanır.
/// </summary>
public sealed class LiveActivitiesManager
{
    /// <summary>Genel türün sunucudaki adı.</summary>
    public const string DefaultActivityType = "HeroSignalDefaultLiveActivityAttributes";

    internal LiveActivitiesManager()
    {
    }

    /// <summary>
    /// Genel türü izlemeye alır: push-to-start belirteci (iOS 17.2+) ve başlayan her etkinliğin güncelleme belirteci
    /// sunucuya kaydedilir. <see cref="HeroSignal.Initialize"/>'dan sonra, uygulama her açıldığında çağrılmalıdır.
    /// </summary>
    /// <exception cref="HeroSignalException">
    /// <c>PLATFORM_NOT_SUPPORTED</c> (iOS 16.1 öncesi, Android), <c>NOT_READY</c> (yerel SDK başlatılmadı).
    /// </exception>
    public Task SetupDefaultLiveActivityAsync() => HeroSignal.Platform().SetupDefaultLiveActivityAsync();

    /// <summary>
    /// Genel türle yerel bir etkinlik başlatır (iOS 16.1+); sunucu sonraki <c>update</c>/<c>end</c> çağrılarını
    /// <paramref name="activityId"/> ile yapar. Değerler metin (en çok 4000 karakter), sonlu sayı veya bool; anahtarlar
    /// 1-64 karakter; <paramref name="attributes"/> içinde <c>herosignal</c> anahtarı ayrılmıştır. Widget'ta değerler
    /// <c>context.attributes.data["…"]</c> ve <c>context.state.data["…"]</c> ile okunur.
    /// </summary>
    /// <exception cref="HeroSignalException">
    /// <c>INVALID_ARGUMENT</c> (geçersiz kimlik/alan), <c>PLATFORM_NOT_SUPPORTED</c>, <c>NOT_READY</c>,
    /// <c>LIVE_ACTIVITY_FAILED</c> (<see cref="HeroSignalException.LiveActivityFailed"/>; Live Activities kapalı vb.).
    /// </exception>
    public Task StartDefaultLiveActivityAsync(string activityId, IDictionary<string, object> attributes, IDictionary<string, object> contentState)
    {
        var platform = HeroSignal.Platform();
        string id;
        IReadOnlyDictionary<string, object> attributeValues, stateValues;
        try
        {
            id = Validation.RequireLiveActivityId(activityId);
            attributeValues = Validation.NormalizeLiveActivityValues(attributes, nameof(attributes), reserveHeroSignalKey: true);
            stateValues = Validation.NormalizeLiveActivityValues(contentState, nameof(contentState), reserveHeroSignalKey: false);
        }
        catch (HeroSignalException e)
        {
            // Tüm Live Activity hataları (yerel SDK'dan gelenlerle aynı biçimde) Task üzerinden iletilir.
            return Task.FromException(e);
        }
        return platform.StartDefaultLiveActivityAsync(id, attributeValues, stateValues);
    }
}

/// <summary>Hata ayıklama (<c>HeroSignal.Debug</c>).</summary>
public sealed class DebugManager
{
    internal DebugManager()
    {
    }

    private volatile object? _explicitLevel;

    /// <summary>Geçerli günlük seviyesi.</summary>
    public LogLevel LogLevel => SdkLog.Level;

    /// <summary>
    /// C# katmanının ve yerel SDK'nın günlük seviyesini ayarlar. <see cref="HeroSignal.Initialize"/> öncesinde çağrılırsa
    /// <see cref="HeroSignalOptions.LogLevel"/> yerine bu değer kullanılır.
    /// </summary>
    public void SetLogLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level), level, "Geçersiz günlük seviyesi.");
        _explicitLevel = level;
        SdkLog.Level = level;
        HeroSignal.PlatformOrNull()?.SetLogLevel(level);
    }

    /// <summary><see cref="SetLogLevel"/> ile açıkça verilen seviye (yoksa null).</summary>
    internal LogLevel? ExplicitLevel => _explicitLevel is LogLevel level ? level : null;

    internal void Reset() => _explicitLevel = null;
}

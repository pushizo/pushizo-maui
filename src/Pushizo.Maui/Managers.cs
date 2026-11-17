using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui;

/// <summary>
/// Bildirimler (<c>Pushizo.Notifications</c>): izin, tıklama, ön plan gösterimi ve bildirim merkezini temizleme.
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
            if (pending is not null && value is not null) Pushizo.Raise(value, pending, "Notifications.Clicked");
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
    public bool Permission => Pushizo.PlatformOrNull()?.NotificationPermission ?? false;

    /// <summary>
    /// Sistem bildirim iznini ister; sonuç izin durumudur. <paramref name="fallbackToSettings"/> true ise izin daha önce
    /// reddedildiğinde uygulamanın bildirim ayarları açılır.
    /// </summary>
    public Task<bool> RequestPermissionAsync(bool fallbackToSettings = false) =>
        Pushizo.Platform().RequestPermissionAsync(fallbackToSettings);

    /// <summary>Bildirim merkezindeki bu uygulamaya ait tüm bildirimleri kaldırır (iOS'ta rozet de sıfırlanır).</summary>
    public void ClearAll() => Pushizo.Platform().ClearAllNotifications();

    /// <summary>
    /// Tek bir Pushizo bildirimini Android tepsi kimliğiyle (<see cref="PushizoNotification.AndroidNotificationId"/>)
    /// kaldırır. iOS'ta kimlik metne çevrilerek <c>UNNotificationRequest.identifier</c> olarak aranır.
    /// </summary>
    public void RemoveNotification(int androidNotificationId) =>
        Pushizo.Platform().RemoveNotification(androidNotificationId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Tek bildirimi kaldırır. iOS: bildirim isteği kimliği (<c>UNNotificationRequest.identifier</c>). Android: tepsi
    /// kimliğinin metni; tamsayıya çevrilemezse <see cref="ArgumentException"/> fırlatılır.
    /// </summary>
    public void RemoveNotification(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        Pushizo.Platform().RemoveNotification(identifier.Trim());
    }

    /// <summary>Aynı gruptaki bildirimleri kaldırır (Android <c>android_group</c>, iOS <c>thread-id</c>).</summary>
    public void RemoveGroupedNotifications(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        Pushizo.Platform().RemoveGroupedNotifications(group);
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
        Pushizo.Raise(handler, args, "Notifications.Clicked");
    }

    internal void DispatchWillDisplay(NotificationWillDisplayEventArgs args) =>
        Pushizo.Raise(ForegroundWillDisplay, args, "Notifications.ForegroundWillDisplay");

    internal void DispatchPermission(bool permission)
    {
        lock (_gate)
        {
            var previous = _lastPermission;
            _lastPermission = permission;
            // İlk gözlem (Initialize sırasında) başlangıç durumudur; değişiklik sayılmaz.
            if (previous is null || previous == permission) return;
        }
        Pushizo.Raise(PermissionChanged, new NotificationPermissionChangedEventArgs(permission), "Notifications.PermissionChanged");
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
/// In-app mesajlar (<c>Pushizo.InAppMessages</c>): tetikler yerel SDK'da değerlendirilir (<c>session_start</c>,
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
        Pushizo.Platform().AddTriggers(valid);
    }

    public void RemoveTrigger(string key) => RemoveTriggers(new[] { key });

    public void RemoveTriggers(IEnumerable<string> keys)
    {
        var valid = Validation.RequireKeys(keys, nameof(keys));
        if (valid.Count == 0) return;
        Pushizo.Platform().RemoveTriggers(valid);
    }

    public void ClearTriggers() => Pushizo.Platform().ClearTriggers();

    /// <summary>Geçerli trigger değerleri.</summary>
    public IReadOnlyDictionary<string, string> GetTriggers() =>
        Pushizo.PlatformOrNull()?.GetTriggers() ?? new Dictionary<string, string>();

    /// <summary>Geçerli ekran adı (<c>screen</c> tetikleri); ekrandan çıkarken null. Örn. sayfanın <c>OnAppearing</c>'inde.</summary>
    public void SetScreen(string? name) =>
        Pushizo.Platform().SetScreen(string.IsNullOrWhiteSpace(name) ? null : name.Trim());

    /// <summary>true iken yeni mesaj gösterilmez (tetikler kaydedilmeye devam eder).</summary>
    public bool Paused
    {
        get => Pushizo.PlatformOrNull()?.InAppPaused ?? false;
        set => Pushizo.Platform().InAppPaused = value;
    }

    internal void DispatchClicked(InAppMessageClickedEventArgs args) => Pushizo.Raise(Clicked, args, "InAppMessages.Clicked");

    internal void DispatchLifecycle(InAppLifecyclePhase phase, InAppMessage message)
    {
        var args = new InAppMessageLifecycleEventArgs(message);
        switch (phase)
        {
            case InAppLifecyclePhase.WillDisplay: Pushizo.Raise(WillDisplay, args, "InAppMessages.WillDisplay"); break;
            case InAppLifecyclePhase.DidDisplay: Pushizo.Raise(DidDisplay, args, "InAppMessages.DidDisplay"); break;
            case InAppLifecyclePhase.WillDismiss: Pushizo.Raise(WillDismiss, args, "InAppMessages.WillDismiss"); break;
            case InAppLifecyclePhase.DidDismiss: Pushizo.Raise(DidDismiss, args, "InAppMessages.DidDismiss"); break;
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
/// Bildirim kutusu (<c>Pushizo.Inbox</c>, <c>add_to_inbox</c> mesajları). Abonelik ve kullanıcı kaydı tamamlanmadan
/// çağrılar <see cref="PushizoException"/> (<c>NOT_READY</c>) ile sonuçlanır; sunucu hataları sunucu kodunu taşır.
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
        return Pushizo.Platform().InboxListAsync(limit, string.IsNullOrEmpty(cursor) ? null : cursor);
    }

    /// <summary>Öğeyi okundu işaretler; güncel öğeyi döndürür.</summary>
    public Task<InboxItem> MarkReadAsync(string id) => Pushizo.Platform().InboxMarkReadAsync(RequireId(id));

    /// <summary>Tümünü okundu işaretler; kalan okunmamış sayısını döndürür.</summary>
    public Task<int> MarkAllReadAsync() => Pushizo.Platform().InboxMarkAllReadAsync();

    /// <summary>Öğeyi kutudan kaldırır (arşivler).</summary>
    public Task ArchiveAsync(string id) => Pushizo.Platform().InboxArchiveAsync(RequireId(id));

    /// <summary>Okunmamış öğe sayısı.</summary>
    public Task<int> UnreadCountAsync() => Pushizo.Platform().InboxUnreadCountAsync();

    private static string RequireId(string id) =>
        string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Öğe kimliği gerekli.", nameof(id)) : id;
}

/// <summary>
/// İçerik kartları (<c>Pushizo.ContentCards</c>, ADR-0082): kalıcı kart akışı. Gösterimde <see cref="LogImpressionAsync"/>,
/// tıklamada <see cref="LogClickAsync"/>, kapatmada <see cref="DismissAsync"/> çağrılır.
/// </summary>
public sealed class ContentCardsManager
{
    internal ContentCardsManager()
    {
    }

    /// <summary>Kart akışı (<paramref name="limit"/> 1-100).</summary>
    public Task<ContentCardFeed> ListAsync(int limit = 50)
    {
        Validation.RequireContentCardLimit(limit, nameof(limit));
        return Pushizo.Platform().ContentCardsListAsync(limit);
    }

    /// <summary>Kart ekranda gösterildi (ilk gösterim kaydedilir).</summary>
    public Task<ContentCard> LogImpressionAsync(string id) => Pushizo.Platform().ContentCardEventAsync(RequireId(id), "impression");

    /// <summary>Karta tıklandı (gösterim de sayılır).</summary>
    public Task<ContentCard> LogClickAsync(string id) => Pushizo.Platform().ContentCardEventAsync(RequireId(id), "click");

    /// <summary>Kart kapatıldı; akıştan kalkar.</summary>
    public Task<ContentCard> DismissAsync(string id) => Pushizo.Platform().ContentCardEventAsync(RequireId(id), "dismiss");

    private static string RequireId(string id) =>
        string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Kart kimliği gerekli.", nameof(id)) : id;
}

/// <summary>Oturum ve özel olaylar (<c>Pushizo.Session</c>).</summary>
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
        Pushizo.Platform().SendEvent(name.Trim(), normalized, value);
    }
}

/// <summary>
/// iOS Live Activities (<c>Pushizo.LiveActivities</c>, ADR-0024). Köprü, genel
/// <c>PushizoDefaultLiveActivityAttributes</c> türünü kullanır; sunucuda (dashboard/API) tür adı
/// <see cref="DefaultActivityType"/> olarak tanımlanır ve widget extension'da
/// <c>ActivityConfiguration(for: PushizoDefaultLiveActivityAttributes.self)</c> yazılır. Android ve diğer hedeflerde
/// çağrılar <see cref="PushizoException"/> (<c>PLATFORM_NOT_SUPPORTED</c>) ile sonuçlanır.
/// </summary>
public sealed class LiveActivitiesManager
{
    /// <summary>Genel türün sunucudaki adı.</summary>
    public const string DefaultActivityType = "PushizoDefaultLiveActivityAttributes";

    internal LiveActivitiesManager()
    {
    }

    /// <summary>
    /// Genel türü izlemeye alır: push-to-start belirteci (iOS 17.2+) ve başlayan her etkinliğin güncelleme belirteci
    /// sunucuya kaydedilir. <see cref="Pushizo.Initialize"/>'dan sonra, uygulama her açıldığında çağrılmalıdır.
    /// </summary>
    /// <exception cref="PushizoException">
    /// <c>PLATFORM_NOT_SUPPORTED</c> (iOS 16.1 öncesi, Android), <c>NOT_READY</c> (yerel SDK başlatılmadı).
    /// </exception>
    public Task SetupDefaultLiveActivityAsync() => Pushizo.Platform().SetupDefaultLiveActivityAsync();

    /// <summary>
    /// Genel türle yerel bir etkinlik başlatır (iOS 16.1+); sunucu sonraki <c>update</c>/<c>end</c> çağrılarını
    /// <paramref name="activityId"/> ile yapar. Değerler metin (en çok 4000 karakter), sonlu sayı veya bool; anahtarlar
    /// 1-64 karakter; <paramref name="attributes"/> içinde <c>pushizo</c> anahtarı ayrılmıştır. Widget'ta değerler
    /// <c>context.attributes.data["…"]</c> ve <c>context.state.data["…"]</c> ile okunur.
    /// </summary>
    /// <exception cref="PushizoException">
    /// <c>INVALID_ARGUMENT</c> (geçersiz kimlik/alan), <c>PLATFORM_NOT_SUPPORTED</c>, <c>NOT_READY</c>,
    /// <c>LIVE_ACTIVITY_FAILED</c> (<see cref="PushizoException.LiveActivityFailed"/>; Live Activities kapalı vb.).
    /// </exception>
    public Task StartDefaultLiveActivityAsync(string activityId, IDictionary<string, object> attributes, IDictionary<string, object> contentState)
    {
        var platform = Pushizo.Platform();
        string id;
        IReadOnlyDictionary<string, object> attributeValues, stateValues;
        try
        {
            id = Validation.RequireLiveActivityId(activityId);
            attributeValues = Validation.NormalizeLiveActivityValues(attributes, nameof(attributes), reservePushizoKey: true);
            stateValues = Validation.NormalizeLiveActivityValues(contentState, nameof(contentState), reservePushizoKey: false);
        }
        catch (PushizoException e)
        {
            // Tüm Live Activity hataları (yerel SDK'dan gelenlerle aynı biçimde) Task üzerinden iletilir.
            return Task.FromException(e);
        }
        return platform.StartDefaultLiveActivityAsync(id, attributeValues, stateValues);
    }
}

/// <summary>
/// Konum paylaşımı (<c>Pushizo.Location</c>, v2 Faz 17b). Yalnızca ön plan: paylaşım açık ve izin varken yerel SDK
/// konumu oturum başında ve uygulama ön plana döndüğünde en sık 5 dakikada bir push aboneliğine yazar; izin yoksa veya
/// gizlilik onayı (<see cref="Pushizo.SetConsentGiven"/>) verilmemişse hiçbir şey göndermez. Tercih kalıcıdır. İlk
/// konum sunucuda KVKK açık rıza kaydı sayılır; <see cref="SetShared"/>(false) konumu ve rıza kaydını sildirir.
/// İsteğe bağlı arka plan bölge izleme (<see cref="SetGeofencingEnabled"/>, ADR-0065) ayrıdır ve varsayılan kapalıdır.
/// Windows ve MacCatalyst hedeflerinde etkisizdir (<see cref="IsShared"/>, <see cref="IsGeofencingEnabled"/> ve
/// <see cref="RequestPermissionAsync"/> false).
/// </summary>
public sealed class LocationManager
{
    internal LocationManager()
    {
    }

    /// <summary>Paylaşım açık mı (kalıcı tercih; izin durumundan bağımsız). Başlatılmadan önce false.</summary>
    public bool IsShared => Pushizo.PlatformOrNull()?.LocationShared ?? false;

    /// <summary>
    /// Paylaşımı açar/kapatır. Açılırken izin varsa konum hemen gönderilir; kapatılınca sunucuya <c>location: null</c>
    /// gönderilir (konum ve rıza kaydı silinir).
    /// </summary>
    public void SetShared(bool shared) => Pushizo.Platform().SetLocationShared(shared);

    /// <summary>
    /// "Kullanırken" konum iznini ister (arka plan konumu istenmez). İzin yoksa önce açıklama diyaloğu gösterilir
    /// (<paramref name="rationale"/>; boşsa SDK'nın Türkçe/İngilizce varsayılan metni), kullanıcı devam ederse sistem
    /// istemi açılır. Sonuç: izin var/verildi ise true; reddedildi, kısıtlı, açıklamada vazgeçildi ya da gerekli
    /// yapılandırma (iOS <c>NSLocationWhenInUseUsageDescription</c>, Android manifest konum izni) eksikse false.
    /// İzin, paylaşımı açmaz; ardından <see cref="SetShared"/>(true) çağrılmalıdır.
    /// </summary>
    public Task<bool> RequestPermissionAsync(string? rationale = null) =>
        Pushizo.Platform().RequestLocationPermissionAsync(string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim());

    /// <summary>Arka plan bölge izleme açık mı (kalıcı tercih; varsayılan kapalı). Başlatılmadan önce false.</summary>
    public bool IsGeofencingEnabled => Pushizo.PlatformOrNull()?.GeofencingEnabled ?? false;

    /// <summary>
    /// Arka plan bölge izlemeyi açar/kapatır (ADR-0065). İzlemeyi işletim sistemi yapar; sunucuya koordinat gitmez,
    /// yalnızca bölgeye giriş/çıkış bildirilir. Çalışması için <see cref="SetShared"/>(true) açık ve arka plan konum izni
    /// verilmiş olmalıdır: izni ve gerekli bildirimleri (iOS <c>NSLocationAlwaysAndWhenInUseUsageDescription</c>, Android
    /// <c>ACCESS_BACKGROUND_LOCATION</c> + <c>play-services-location</c>) uygulama ekler; köprü izin istemez.
    /// Kapatınca kayıtlı bölgeler kaldırılır.
    /// </summary>
    public void SetGeofencingEnabled(bool enabled) => Pushizo.Platform().SetGeofencingEnabled(enabled);
}

/// <summary>
/// Android teslimat güçlendirme (<c>Pushizo.Delivery</c>, ADR-0092): bazı üreticilerin pil/otomatik başlatma yöneticisi
/// arka plandaki uygulamayı durdurduğu için bildirimler gecikir ya da gelmez. Bu yüzey cihazdaki kısıtları okur ve
/// kullanıcıyı ilgili ayar ekranına götürür. Yalnızca Android'de etkilidir: iOS, Windows ve MacCatalyst hedeflerinde ve
/// SDK başlatılmadan önce durum <see cref="DeliveryStatus.Unsupported"/> döner, <see cref="OpenSettings"/> ile
/// <see cref="ShowGuidanceAsync"/> hiçbir şey yapmadan false döner. Hiçbir çağrı istisna atmaz.
/// </summary>
public sealed class DeliveryManager
{
    /// <summary>Hazır diyaloğun yeniden önerilme aralığı (gün; yerel SDK varsayılanıyla aynı).</summary>
    public const int DefaultGuidanceIntervalDays = 14;

    internal DeliveryManager()
    {
    }

    /// <summary>Cihazın teslim durumu: üretici, ROM, pil muafiyeti, arka plan kısıtı, risk ve önerilen ayar ekranları.</summary>
    public DeliveryStatus GetStatus() =>
        Guard(platform => platform.GetDeliveryStatus(), DeliveryStatus.Unsupported, "teslim durumu okunamadı");

    /// <summary>
    /// Hazır açıklama diyaloğu şimdi gösterilmeli mi: teslim riski var ve diyalog son <paramref name="minIntervalDays"/>
    /// gün içinde gösterilmedi (negatif değer 0 sayılır).
    /// </summary>
    public bool ShouldShowGuidance(int minIntervalDays = DefaultGuidanceIntervalDays) =>
        Guard(platform => platform.ShouldShowDeliveryGuidance(Math.Max(0, minIntervalDays)), false, "teslim yönlendirmesi denetlenemedi");

    /// <summary>
    /// Ayar ekranını açar. <see cref="DeliverySetting.Auto"/> duruma en uygun ekranı seçer; üreticiye özgü ekran
    /// bulunamazsa standart Android ekranına düşülür. Bir ekran açıldıysa true.
    /// </summary>
    public bool OpenSettings(DeliverySetting setting = DeliverySetting.Auto) =>
        Guard(platform => platform.OpenDeliverySettings(setting), false, "teslimat ayar ekranı açılamadı");

    /// <summary>
    /// Hazır açıklama diyaloğunu gösterir (22 dil; <paramref name="message"/> verilirse SDK metni yerine o gösterilir).
    /// Kullanıcı kabul ederse <paramref name="setting"/> ekranı açılır. Sonuç: ayar ekranı açıldıysa true; kullanıcı
    /// vazgeçtiyse, ekran açılamadıysa ya da etkin Activity yoksa false. SDK diyaloğu kendiliğinden göstermez; uygun anı
    /// uygulama seçer (<see cref="ShouldShowGuidance"/>).
    /// </summary>
    public async Task<bool> ShowGuidanceAsync(DeliverySetting setting = DeliverySetting.Auto, string? message = null)
    {
        var platform = Pushizo.PlatformOrNull();
        if (platform is null) return false;
        try
        {
            return await platform.ShowDeliveryGuidanceAsync(setting, string.IsNullOrWhiteSpace(message) ? null : message.Trim()).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            SdkLog.Write(LogLevel.Warn, "teslimat açıklama diyaloğu gösterilemedi", e);
            return false;
        }
    }

    /// <summary>Başlatılmamış SDK'da ve yerel hatada <paramref name="fallback"/> döner (bu yüzey istisna atmaz).</summary>
    private static T Guard<T>(Func<IPushizoPlatform, T> call, T fallback, string failure)
    {
        var platform = Pushizo.PlatformOrNull();
        if (platform is null) return fallback;
        try
        {
            return call(platform);
        }
        catch (Exception e)
        {
            SdkLog.Write(LogLevel.Warn, failure, e);
            return fallback;
        }
    }
}

/// <summary>
/// Özellik bayrakları (<c>Pushizo.FeatureFlags</c>, ADR-0066). Değerler yerel SDK tarafından oturum başında yenilenir
/// ve cihazda saklanır; ağ yoksa son değer, hiç alınmadıysa koddaki varsayılan döner. Gizlilik onayı beklenirken istek
/// gönderilmez. Android ve iOS dışındaki hedeflerde okumalar varsayılanı, <see cref="RefreshAsync"/> false döndürür.
/// </summary>
public sealed class FeatureFlagsManager
{
    internal FeatureFlagsManager()
    {
    }

    /// <summary>Değerleri hemen yeniler; başarıda true (hata son değerleri korur, false döner).</summary>
    public Task<bool> RefreshAsync() => Pushizo.Platform().RefreshFeatureFlagsAsync();

    /// <summary>Bayrak bu kullanıcı için açık mı; değer henüz yoksa (veya SDK başlatılmadıysa) <paramref name="defaultValue"/>.</summary>
    public bool IsEnabled(string key, bool defaultValue = false) =>
        Pushizo.PlatformOrNull()?.IsFeatureFlagEnabled(RequireKey(key), defaultValue) ?? defaultValue;

    /// <summary>
    /// Bayrağın değeri: <c>string</c>, <c>bool</c>, <c>long</c> (tam sayı), <c>double</c>,
    /// <c>Dictionary&lt;string, object?&gt;</c> (JSON nesnesi) veya <c>List&lt;object?&gt;</c> (JSON dizisi); değer henüz
    /// yoksa (veya SDK başlatılmadıysa) <paramref name="defaultValue"/> olduğu gibi döner.
    /// </summary>
    public object? GetValue(string key, object? defaultValue = null) =>
        Pushizo.PlatformOrNull()?.GetFeatureFlagValue(RequireKey(key)) ?? defaultValue;

    private static string RequireKey(string key) =>
        string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Bayrak anahtarı gerekli.", nameof(key)) : key;
}

/// <summary>
/// Gömülü yerleşimler (<c>Pushizo.Placements</c>, ADR-0068): uygulamanın yerleşim anahtarıyla adlandırdığı alanın
/// içeriği in-app mesaj modeliyle gelir (<see cref="InAppMessage.Json"/> sunucunun ham mesajıdır); çizim uygulamanındır,
/// tetikler değerlendirilmez. Gösterim liste alınınca değil içerik ekrana çizilince <see cref="ReportImpression"/> ile
/// bildirilir; raporlar yerel SDK'nın kalıcı kuyruğuyla gider ve yalnızca son <see cref="ListAsync"/> çağrılarından dönen
/// (en çok 50) mesaj için işlenir. Android ve iOS dışındaki hedeflerde liste boştur, raporlar etkisizdir.
/// </summary>
public sealed class PlacementsManager
{
    internal PlacementsManager()
    {
    }

    /// <summary>
    /// Alanın içeriği (önceliğe göre sıralı, en çok <paramref name="limit"/> mesaj; 1-10). Abonelik yokken veya gizlilik
    /// onayı beklenirken boş liste döner. <paramref name="key"/>: küçük harfle başlar; küçük harf, rakam ve <c>_ . -</c>
    /// (1-64 karakter).
    /// </summary>
    /// <exception cref="ArgumentException">Geçersiz anahtar.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> 1-10 dışında.</exception>
    /// <exception cref="PushizoException">Yerel SDK isteği reddetti (iOS'ta sunucu hata kodu; Android boş liste döner).</exception>
    public Task<IReadOnlyList<InAppMessage>> ListAsync(string key, int limit = 1)
    {
        Validation.RequirePlacementKey(key, nameof(key));
        Validation.RequirePlacementLimit(limit, nameof(limit));
        return Pushizo.Platform().PlacementsListAsync(key, limit);
    }

    /// <summary>İçerik ekrana çizildi.</summary>
    public void ReportImpression(InAppMessage message) => Pushizo.Platform().PlacementsReportImpression(RequireMessage(message).Id);

    /// <summary>İçerikteki <paramref name="buttonId"/> kimlikli düğmeye tıklandı.</summary>
    public void ReportClick(InAppMessage message, string buttonId)
    {
        var id = RequireMessage(message).Id;
        if (string.IsNullOrWhiteSpace(buttonId)) throw new ArgumentException("Düğme kimliği gerekli.", nameof(buttonId));
        Pushizo.Platform().PlacementsReportClick(id, buttonId);
    }

    /// <summary>İçerik kullanıcı tarafından kapatıldı.</summary>
    public void ReportDismiss(InAppMessage message) => Pushizo.Platform().PlacementsReportDismiss(RequireMessage(message).Id);

    private static InAppMessage RequireMessage(InAppMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Id)) throw new ArgumentException("Mesaj kimliği gerekli.", nameof(message));
        return message;
    }
}

/// <summary>Hata ayıklama (<c>Pushizo.Debug</c>).</summary>
public sealed class DebugManager
{
    internal DebugManager()
    {
    }

    private volatile object? _explicitLevel;

    /// <summary>Geçerli günlük seviyesi.</summary>
    public LogLevel LogLevel => SdkLog.Level;

    /// <summary>
    /// C# katmanının ve yerel SDK'nın günlük seviyesini ayarlar. <see cref="Pushizo.Initialize"/> öncesinde çağrılırsa
    /// <see cref="PushizoOptions.LogLevel"/> yerine bu değer kullanılır.
    /// </summary>
    public void SetLogLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level), level, "Geçersiz günlük seviyesi.");
        _explicitLevel = level;
        SdkLog.Level = level;
        Pushizo.PlatformOrNull()?.SetLogLevel(level);
    }

    /// <summary><see cref="SetLogLevel"/> ile açıkça verilen seviye (yoksa null).</summary>
    internal LogLevel? ExplicitLevel => _explicitLevel is LogLevel level ? level : null;

    internal void Reset() => _explicitLevel = null;
}

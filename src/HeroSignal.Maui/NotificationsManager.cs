using System.Globalization;
using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui;

/// <summary>Push izni, bildirim olayları ve bildirim tepsisi (<c>HeroSignal.Notifications</c>).</summary>
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
    /// Bildirime veya aksiyon düğmesine tıklandı. Uygulama bildirimle açıldıysa ve henüz işleyici yoksa son tıklama
    /// saklanır, ilk abone olan işleyiciye hemen iletilir (soğuk başlatmada tıklama kaybolmaz).
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
            if (pending is not null && value is not null)
                HeroSignal.Raise(value, pending, "Notifications.Clicked");
        }
        remove
        {
            lock (_gate) _clicked -= value;
        }
    }

    /// <summary>
    /// Uygulama ön plandayken bildirim geldi; <see cref="NotificationWillDisplayEventArgs.PreventDefault"/> gösterimi engeller.
    /// İşleyici eşzamanlı çalışır; karar sonradan verilecekse önce PreventDefault, sonra Display çağrılır.
    /// </summary>
    public event EventHandler<NotificationWillDisplayEventArgs>? ForegroundWillDisplay;

    /// <summary>Sistem bildirim izni değişti (yalnızca gerçek değişiklikte).</summary>
    public event EventHandler<NotificationPermissionChangedEventArgs>? PermissionChanged;

    /// <summary>Sistem bildirim izni var mı (Android 13+ POST_NOTIFICATIONS; iOS authorized/provisional).</summary>
    public bool Permission => HeroSignal.PlatformOrNull()?.NotificationPermission ?? false;

    /// <summary>
    /// Sistem bildirim iznini ister; sonuç izin durumudur. <paramref name="fallbackToSettings"/> true ise izin daha önce
    /// reddedildiyse uygulamanın sistem ayarları açılır (sonuç yine false döner; kullanıcı dönünce <see cref="PermissionChanged"/> gelir).
    /// </summary>
    public Task<bool> RequestPermissionAsync(bool fallbackToSettings = false) =>
        HeroSignal.Platform().RequestPermissionAsync(fallbackToSettings);

    /// <summary>SDK'nın gösterdiği tüm bildirimleri tepsiden kaldırır (iOS'ta rozet de sıfırlanır).</summary>
    public void ClearAll() => HeroSignal.Platform().ClearAllNotifications();

    /// <summary>
    /// Tek bir bildirimi tepsiden kaldırır. Kimlik <see cref="HeroSignalNotification.NotificationId"/> değeridir:
    /// Android'de sistem bildirim kimliği (sayı), iOS'ta istek kimliği veya teslimat kimliği (<c>hs.dlv</c>).
    /// </summary>
    public void RemoveNotification(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Bildirim kimliği gerekli.", nameof(id));
        HeroSignal.Platform().RemoveNotification(id.Trim());
    }

    /// <summary>Android sistem bildirim kimliğiyle kaldırır (<see cref="RemoveNotification(string)"/> ile aynı).</summary>
    public void RemoveNotification(int id) => RemoveNotification(id.ToString(CultureInfo.InvariantCulture));

    /// <summary>Gruptaki tüm HeroSignal bildirimlerini kaldırır (Android <c>hs_group</c>, iOS <c>thread-id</c>).</summary>
    public void RemoveGroupedNotifications(string group)
    {
        if (string.IsNullOrWhiteSpace(group)) throw new ArgumentException("Grup anahtarı gerekli.", nameof(group));
        HeroSignal.Platform().RemoveGroupedNotifications(group.Trim());
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
            if (_lastPermission == permission) return;
            _lastPermission = permission;
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

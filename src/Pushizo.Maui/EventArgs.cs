namespace PushizoSdk.Maui;

/// <summary>Bildirime (veya aksiyon düğmesine) tıklandı.</summary>
public sealed class NotificationClickedEventArgs : EventArgs
{
    public NotificationClickedEventArgs(PushizoNotification notification, string? actionId, string? url)
    {
        Notification = notification;
        ActionId = actionId;
        Url = url;
    }

    public PushizoNotification Notification { get; }
    /// <summary>Tıklanan düğmenin kimliği; bildirim gövdesine tıklandıysa null.</summary>
    public string? ActionId { get; }
    /// <summary>Düğmenin URL'si, yoksa bildirimin URL'si.</summary>
    public string? Url { get; }
}

/// <summary>
/// Uygulama ön plandayken bildirim geldi. <see cref="PreventDefault"/> gösterimi engeller; karar sonradan
/// verilecekse önce <see cref="PreventDefault"/>, ardından gerekirse <see cref="Display"/> çağrılır
/// (iOS'ta <see cref="Display"/> için süre 25 sn'dir).
/// </summary>
public sealed class NotificationWillDisplayEventArgs : EventArgs
{
    private readonly Action _preventDefault;
    private readonly Action _display;

    internal NotificationWillDisplayEventArgs(PushizoNotification notification, Action preventDefault, Action display)
    {
        Notification = notification;
        _preventDefault = preventDefault;
        _display = display;
    }

    public PushizoNotification Notification { get; }

    /// <summary>Bu olay için <see cref="PreventDefault"/> çağrıldı mı.</summary>
    public bool IsPrevented { get; private set; }

    /// <summary>Bildirim gösterilmez (olay işleyicisi içinde, eşzamanlı çağrılmalıdır).</summary>
    public void PreventDefault()
    {
        if (IsPrevented) return;
        IsPrevented = true;
        _preventDefault();
    }

    /// <summary>Bildirimi şimdi gösterir (yalnızca bir kez; <see cref="PreventDefault"/> sonrasında da çalışır).</summary>
    public void Display() => _display();
}

/// <summary>Sistem bildirim izni değişti.</summary>
public sealed class NotificationPermissionChangedEventArgs : EventArgs
{
    public NotificationPermissionChangedEventArgs(bool permission) => Permission = permission;

    public bool Permission { get; }
}

/// <summary>Push aboneliği (kimlik, token, opt-in veya izin) değişti.</summary>
public sealed class PushSubscriptionChangedEventArgs : EventArgs
{
    public PushSubscriptionChangedEventArgs(PushSubscriptionState previous, PushSubscriptionState current)
    {
        Previous = previous;
        Current = current;
    }

    public PushSubscriptionState Previous { get; }
    public PushSubscriptionState Current { get; }
}

/// <summary>In-app mesaj düğmesine tıklandı.</summary>
public sealed class InAppMessageClickedEventArgs : EventArgs
{
    public InAppMessageClickedEventArgs(string messageId, string actionId, string? url)
    {
        MessageId = messageId;
        ActionId = actionId;
        Url = url;
    }

    public string MessageId { get; }
    /// <summary>Düğme kimliği.</summary>
    public string ActionId { get; }
    /// <summary>url/deeplink aksiyonunun adresi; diğer aksiyonlarda null.</summary>
    public string? Url { get; }
}

/// <summary>In-app mesaj yaşam döngüsü (WillDisplay/DidDisplay/WillDismiss/DidDismiss).</summary>
public sealed class InAppMessageLifecycleEventArgs : EventArgs
{
    public InAppMessageLifecycleEventArgs(InAppMessage message) => Message = message;

    public InAppMessage Message { get; }
}

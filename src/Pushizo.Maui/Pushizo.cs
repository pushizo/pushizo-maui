using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui;

/// <summary>
/// Pushizo .NET MAUI SDK (eski sağlayıcı v5 yüzeyiyle uyumlu). Android'de yerel <c>com.pushizo:pushizo-android</c>,
/// iOS'ta yerel <c>Pushizo</c> Swift SDK'sı kullanılır; tetik değerlendirme, kuyruk ve teslimat geri bildirimi yereldedir.
/// <code>
/// Pushizo.Initialize("app_…", new PushizoOptions { ApiUrl = "https://api.example.com" });
/// await Pushizo.Notifications.RequestPermissionAsync();
/// Pushizo.Login("user_42");
/// Pushizo.User.AddTag("plan", "pro");
/// Pushizo.InAppMessages.AddTrigger("level", "gold");
/// var page = await Pushizo.Inbox.ListAsync(limit: 20);
/// await Pushizo.Chat.StartAsync();
/// Pushizo.Chat.EventReceived += (_, e) => { if (e.Type == ChatEventType.Message) Show(e.Message!); };
/// </code>
/// </summary>
public static class Pushizo
{
    /// <summary>Bu C# katmanının sürümü.</summary>
    public const string SdkVersion = "0.1.0";

    private static readonly object s_gate = new();
    private static IPushizoPlatform s_platform = PlatformFactory.Create();
    private static volatile bool s_initialized;
    private static string? s_appId;

    internal static EventSink Sink { get; } = new();

    /// <summary><see cref="Initialize"/> çağrıldı mı.</summary>
    public static bool IsInitialized => s_initialized;

    /// <summary>Bu platformda yerel SDK var mı (Android ve iOS; diğer hedeflerde çağrılar etkisizdir).</summary>
    public static bool IsSupported => s_platform.IsSupported;

    public static UserManager User { get; } = new();
    public static NotificationsManager Notifications { get; } = new();
    public static InAppMessagesManager InAppMessages { get; } = new();
    public static InboxManager Inbox { get; } = new();
    /// <summary>Canlı sohbet (ADR-0040).</summary>
    public static ChatManager Chat { get; } = new();
    public static SessionManager Session { get; } = new();
    public static LiveActivitiesManager LiveActivities { get; } = new();
    public static LocationManager Location { get; } = new();
    public static DebugManager Debug { get; } = new();

    /// <summary>
    /// SDK'yı başlatır (uygulama açılışında bir kez; <c>MauiProgram.CreateMauiApp</c> içinde
    /// <c>builder.UsePushizo(...)</c> ya da doğrudan bu metot). Aynı App ID ile tekrar çağrı etkisizdir;
    /// farklı App ID <see cref="InvalidOperationException"/> üretir.
    /// </summary>
    public static void Initialize(string appId, PushizoOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(appId)) throw new ArgumentException("appId gerekli.", nameof(appId));
        var opts = (options ?? new PushizoOptions()).Clone();
        if (string.IsNullOrWhiteSpace(opts.ApiUrl) || !Uri.TryCreate(opts.ApiUrl, UriKind.Absolute, out var api) || (api.Scheme != Uri.UriSchemeHttps && api.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("ApiUrl http(s) ile başlayan mutlak bir adres olmalı.", nameof(options));
        if (Debug.ExplicitLevel is { } explicitLevel) opts.LogLevel = explicitLevel;
        lock (s_gate)
        {
            if (s_initialized)
            {
                if (s_appId == appId) return;
                throw new InvalidOperationException("Pushizo farklı bir App ID ile zaten başlatıldı; App ID uygulama ömrü boyunca değiştirilemez.");
            }
            SdkLog.Level = opts.LogLevel;
            s_platform.Initialize(appId, opts, Sink);
            s_appId = appId;
            s_initialized = true;
            // Başlangıç durumları tohumlanır; sonraki olaylar yalnızca gerçek değişiklikte üretilir.
            User.PushSubscription.Seed(s_platform.PushSubscription);
            Notifications.DispatchPermission(s_platform.NotificationPermission);
        }
        if (!s_platform.IsSupported)
            SdkLog.Write(LogLevel.Warn, "bu platformda yerel Pushizo SDK'sı yok; çağrılar etkisiz (yalnızca Android ve iOS desteklenir)");
    }

    /// <summary>KVKK/GDPR onayı (<see cref="PushizoOptions.RequiresPrivacyConsent"/>). false: kuyruk ve kimlik temizlenir.</summary>
    public static void SetConsentGiven(bool given) => Platform().SetConsentGiven(given);

    /// <summary>
    /// Cihazı kullanıcı kimliğinize bağlar. Identity verification açıksa app ayarına göre sunucunuzun ürettiği HMAC
    /// <paramref name="identityHash"/> ve/veya ES256 <paramref name="jwt"/> (ADR-0023 §9; <c>sub</c> = externalId,
    /// <c>aud</c> = appId) gerekir. JWT tüm SDK isteklerinde <c>Authorization: Bearer</c> başlığıyla gider; süresi dolunca
    /// <see cref="UpdateUserJwt"/> ile yenilenir.
    /// </summary>
    /// <exception cref="PushizoException">Verilen kanıt app'in kimlik doğrulama kipine uymuyorsa (<c>NOT_READY</c>).</exception>
    public static void Login(string externalId, string? identityHash = null, string? jwt = null)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentException("externalId gerekli.", nameof(externalId));
        Platform().Login(
            externalId,
            string.IsNullOrEmpty(identityHash) ? null : identityHash,
            string.IsNullOrWhiteSpace(jwt) ? null : jwt);
    }

    /// <summary>
    /// Süresi dolan veya yenilenen kimlik JWT'sini günceller; sonraki SDK istekleri yeni belirteci taşır, kuyrukta bekleyen
    /// işlemler yeniden denenir.
    /// </summary>
    /// <exception cref="PushizoException">Giriş yapılmamışsa (<c>NOT_READY</c>).</exception>
    public static void UpdateUserJwt(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) throw new ArgumentException("jwt gerekli.", nameof(jwt));
        Platform().UpdateUserJwt(jwt);
    }

    /// <summary>Cihazı kullanıcıdan ayırır (anonim kullanıcıya döner); kimlik JWT'si cihazdan silinir.</summary>
    public static void Logout() => Platform().Logout();

    /// <summary>Başlatılmış platformu döndürür; <see cref="Initialize"/> çağrılmadıysa hata fırlatır.</summary>
    internal static IPushizoPlatform Platform()
    {
        if (!s_initialized) throw new InvalidOperationException("Önce Pushizo.Initialize çağrılmalı.");
        return s_platform;
    }

    /// <summary>Başlatılmamışsa null (okuma özellikleri için).</summary>
    internal static IPushizoPlatform? PlatformOrNull() => s_initialized ? s_platform : null;

    /// <summary>Birim testleri için: platformu değiştirir ve durumu sıfırlar.</summary>
    internal static void ResetForTests(IPushizoPlatform platform)
    {
        lock (s_gate)
        {
            s_platform = platform;
            s_initialized = false;
            s_appId = null;
            User.PushSubscription.Reset();
            Notifications.Reset();
            InAppMessages.Reset();
            Chat.Reset();
            Debug.Reset();
            SdkLog.Level = LogLevel.Warn;
        }
    }

    /// <summary>Olay işleyicisi hatası yerel SDK geri çağrısını bozmaz; günlüğe yazılır.</summary>
    internal static void Raise<T>(EventHandler<T>? handler, T args, string eventName) where T : EventArgs
    {
        if (handler is null) return;
        foreach (var d in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<T>)d)(null, args);
            }
            catch (Exception e)
            {
                SdkLog.Write(LogLevel.Error, $"{eventName} olay işleyicisi hata verdi", e);
            }
        }
    }

    /// <summary>Yerel olayları ilgili yöneticilere dağıtır.</summary>
    internal sealed class EventSink : IPushizoEventSink
    {
        public void OnNotificationClicked(NotificationClickedEventArgs args) => Notifications.DispatchClicked(args);

        public void OnNotificationWillDisplay(NotificationWillDisplayEventArgs args) => Notifications.DispatchWillDisplay(args);

        public void OnPermissionChanged(bool permission) => Notifications.DispatchPermission(permission);

        public void OnPushSubscriptionChanged(PushSubscriptionState state) => User.PushSubscription.Dispatch(state);

        public void OnInAppClicked(InAppMessageClickedEventArgs args) => InAppMessages.DispatchClicked(args);

        public void OnInAppLifecycle(InAppLifecyclePhase phase, InAppMessage message) => InAppMessages.DispatchLifecycle(phase, message);

        public void OnChatEvent(ChatEventArgs args) => Chat.Dispatch(args);

        public void OnChatOpened(ChatOpenedEventArgs args) => Chat.DispatchOpened(args);
    }
}

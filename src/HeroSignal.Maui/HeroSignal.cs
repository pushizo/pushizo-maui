using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui;

/// <summary>
/// HeroSignal .NET MAUI SDK (eski sağlayıcı v5 yüzeyiyle uyumlu). Android'de yerel <c>com.herosignal:herosignal-android</c>,
/// iOS'ta yerel <c>HeroSignal</c> Swift SDK'sı kullanılır; tetik değerlendirme, kuyruk ve teslimat geri bildirimi yereldedir.
/// <code>
/// HeroSignal.Initialize("app_…", new HeroSignalOptions { ApiUrl = "https://api.example.com" });
/// await HeroSignal.Notifications.RequestPermissionAsync();
/// HeroSignal.Login("user_42");
/// HeroSignal.User.AddTag("plan", "pro");
/// HeroSignal.InAppMessages.AddTrigger("level", "gold");
/// var page = await HeroSignal.Inbox.ListAsync(limit: 20);
/// </code>
/// </summary>
public static class HeroSignal
{
    /// <summary>Bu C# katmanının sürümü.</summary>
    public const string SdkVersion = "0.1.0";

    private static readonly object s_gate = new();
    private static IHeroSignalPlatform s_platform = PlatformFactory.Create();
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
    public static SessionManager Session { get; } = new();
    public static DebugManager Debug { get; } = new();

    /// <summary>
    /// SDK'yı başlatır (uygulama açılışında bir kez; <c>MauiProgram.CreateMauiApp</c> veya <c>App</c> kurucusu).
    /// Aynı App ID ile tekrar çağrı etkisizdir.
    /// </summary>
    public static void Initialize(string appId, HeroSignalOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(appId)) throw new ArgumentException("appId gerekli.", nameof(appId));
        var opts = (options ?? new HeroSignalOptions()).Clone();
        if (string.IsNullOrWhiteSpace(opts.ApiUrl) || !Uri.TryCreate(opts.ApiUrl, UriKind.Absolute, out var api) || (api.Scheme != Uri.UriSchemeHttps && api.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("ApiUrl http(s) ile başlayan mutlak bir adres olmalı.", nameof(options));
        lock (s_gate)
        {
            if (s_initialized && s_appId == appId) return;
            SdkLog.Level = opts.LogLevel;
            s_platform.Initialize(appId, opts, Sink);
            s_appId = appId;
            s_initialized = true;
        }
        if (!s_platform.IsSupported)
            SdkLog.Write(LogLevel.Warn, "bu platformda yerel HeroSignal SDK'sı yok; çağrılar etkisiz (yalnızca Android ve iOS desteklenir)");
    }

    /// <summary>KVKK/GDPR onayı (<see cref="HeroSignalOptions.RequiresPrivacyConsent"/>). false: kuyruk ve kimlik temizlenir.</summary>
    public static void SetConsentGiven(bool given) => Platform().SetConsentGiven(given);

    /// <summary>Cihazı kullanıcı kimliğinize bağlar. Identity verification açıksa sunucunuzun ürettiği <paramref name="identityHash"/> zorunludur.</summary>
    public static void Login(string externalId, string? identityHash = null)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentException("externalId gerekli.", nameof(externalId));
        Platform().Login(externalId, string.IsNullOrEmpty(identityHash) ? null : identityHash);
    }

    /// <summary>Cihazı kullanıcıdan ayırır (anonim kullanıcıya döner).</summary>
    public static void Logout() => Platform().Logout();

    /// <summary>Başlatılmış platformu döndürür; <see cref="Initialize"/> çağrılmadıysa hata fırlatır.</summary>
    internal static IHeroSignalPlatform Platform()
    {
        if (!s_initialized) throw new InvalidOperationException("Önce HeroSignal.Initialize çağrılmalı.");
        return s_platform;
    }

    /// <summary>Başlatılmamışsa null (okuma özellikleri için).</summary>
    internal static IHeroSignalPlatform? PlatformOrNull() => s_initialized ? s_platform : null;

    /// <summary>Birim testleri için: platformu değiştirir ve durumu sıfırlar.</summary>
    internal static void ResetForTests(IHeroSignalPlatform platform)
    {
        lock (s_gate)
        {
            s_platform = platform;
            s_initialized = false;
            s_appId = null;
            User.PushSubscription.Reset();
            Notifications.Reset();
            InAppMessages.Reset();
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
    internal sealed class EventSink : IHeroSignalEventSink
    {
        public void OnNotificationClicked(NotificationClickedEventArgs args) => Notifications.DispatchClicked(args);

        public void OnNotificationWillDisplay(NotificationWillDisplayEventArgs args) => Notifications.DispatchWillDisplay(args);

        public void OnPermissionChanged(bool permission) => Notifications.DispatchPermission(permission);

        public void OnPushSubscriptionChanged(PushSubscriptionState state) => User.PushSubscription.Dispatch(state);

        public void OnInAppClicked(InAppMessageClickedEventArgs args) => InAppMessages.DispatchClicked(args);

        public void OnInAppLifecycle(InAppLifecyclePhase phase, InAppMessage message) => InAppMessages.DispatchLifecycle(phase, message);
    }
}

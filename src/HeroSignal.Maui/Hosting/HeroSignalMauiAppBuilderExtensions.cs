using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace HeroSignalSdk.Maui;

/// <summary>
/// <c>MauiProgram.CreateMauiApp</c> kurulumu:
/// <code>
/// var builder = MauiApp.CreateBuilder();
/// builder.UseMauiApp&lt;App&gt;()
///        .UseHeroSignal("app_…", o =&gt; o.ApiUrl = "https://api.example.com");
/// </code>
/// SDK'yı başlatır ve gereken yaşam döngüsü olaylarını bağlar (Android: bildirim izni sonucu). iOS'ta APNs token'ı
/// <c>AppDelegate</c> içinden <c>HeroSignalIos.RegisteredForRemoteNotifications</c> ile iletilir.
/// </summary>
public static class HeroSignalMauiAppBuilderExtensions
{
    /// <summary>SDK'yı <paramref name="appId"/> ile başlatır; seçenekler <paramref name="configure"/> ile ayarlanır.</summary>
    public static MauiAppBuilder UseHeroSignal(this MauiAppBuilder builder, string appId, Action<HeroSignalOptions>? configure = null)
    {
        var options = new HeroSignalOptions();
        configure?.Invoke(options);
        return builder.UseHeroSignal(appId, options);
    }

    /// <summary>SDK'yı <paramref name="appId"/> ve hazır <paramref name="options"/> ile başlatır.</summary>
    public static MauiAppBuilder UseHeroSignal(this MauiAppBuilder builder, string appId, HeroSignalOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android.OnRequestPermissionsResult(
                (_, requestCode, _, _) => HeroSignalAndroid.OnRequestPermissionsResult(requestCode)));
#endif
        });
        HeroSignal.Initialize(appId, options);
        return builder;
    }
}

using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace PushizoSdk.Maui;

/// <summary>
/// <c>MauiProgram.CreateMauiApp</c> kurulumu:
/// <code>
/// var builder = MauiApp.CreateBuilder();
/// builder.UseMauiApp&lt;App&gt;()
///        .UsePushizo("app_…", o =&gt; o.ApiUrl = "https://api.example.com");
/// </code>
/// SDK'yı başlatır ve gereken yaşam döngüsü olaylarını bağlar (Android: bildirim izni sonucu). iOS'ta APNs token'ı
/// <c>AppDelegate</c> içinden <c>PushizoIos.RegisteredForRemoteNotifications</c> ile iletilir.
/// </summary>
public static class PushizoMauiAppBuilderExtensions
{
    /// <summary>SDK'yı <paramref name="appId"/> ile başlatır; seçenekler <paramref name="configure"/> ile ayarlanır.</summary>
    public static MauiAppBuilder UsePushizo(this MauiAppBuilder builder, string appId, Action<PushizoOptions>? configure = null)
    {
        var options = new PushizoOptions();
        configure?.Invoke(options);
        return builder.UsePushizo(appId, options);
    }

    /// <summary>SDK'yı <paramref name="appId"/> ve hazır <paramref name="options"/> ile başlatır.</summary>
    public static MauiAppBuilder UsePushizo(this MauiAppBuilder builder, string appId, PushizoOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android.OnRequestPermissionsResult(
                (_, requestCode, _, _) => PushizoAndroid.OnRequestPermissionsResult(requestCode)));
#endif
        });
        Pushizo.Initialize(appId, options);
        return builder;
    }
}

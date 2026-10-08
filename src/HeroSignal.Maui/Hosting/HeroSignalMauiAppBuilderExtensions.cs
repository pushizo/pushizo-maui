using Microsoft.Maui.Hosting;

namespace HeroSignalSdk.Maui;

/// <summary><c>MauiProgram.CreateMauiApp</c> kurulumu.</summary>
public static class HeroSignalMauiAppBuilderExtensions
{
    /// <summary>
    /// HeroSignal'i uygulama açılışında başlatır (<see cref="HeroSignal.Initialize"/> ile aynı):
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseHeroSignal("app_…", o =&gt; o.ApiUrl = "https://api.example.com");
    /// </code>
    /// iOS'ta APNs token'ı köprü tarafından uygulama temsilcisine eklenen
    /// <c>application:didRegisterForRemoteNotificationsWithDeviceToken:</c> ile alınır; temsilciniz bu metodu zaten
    /// uyguluyorsa içinden <c>HeroSignalIos.SetDeviceToken(deviceToken)</c> çağırın.
    /// </summary>
    public static MauiAppBuilder UseHeroSignal(this MauiAppBuilder builder, string appId, Action<HeroSignalOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new HeroSignalOptions();
        configure?.Invoke(options);
        HeroSignal.Initialize(appId, options);
        return builder;
    }
}

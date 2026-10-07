using Foundation;
using HeroSignalSdk.Native.iOS;

namespace HeroSignalSdk.Maui;

/// <summary>
/// iOS'a özgü bağlantı noktaları. Yerel SDK swizzling kullanmaz; APNs token'ı uygulamanın <c>AppDelegate</c>'inden iletilir:
/// <code>
/// [Register("AppDelegate")]
/// public class AppDelegate : MauiUIApplicationDelegate
/// {
///     protected override MauiApp CreateMauiApp() =&gt; MauiProgram.CreateMauiApp();
///
///     [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
///     public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken) =&gt;
///         HeroSignalIos.RegisteredForRemoteNotifications(deviceToken);
///
///     [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
///     public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error) =&gt;
///         HeroSignalIos.FailedToRegisterForRemoteNotifications(error);
/// }
/// </code>
/// Zengin medya ve teslimat onayı için Notification Service Extension hedefi yerel <c>HeroSignalExtension</c> ile
/// kurulur (bkz. sdk/maui/README.md).
/// </summary>
public static class HeroSignalIos
{
    /// <summary>APNs cihaz token'ını SDK'ya iletir (abonelik kaydı/güncellemesi).</summary>
    public static void RegisteredForRemoteNotifications(NSData deviceToken)
    {
        ArgumentNullException.ThrowIfNull(deviceToken);
        if (!HeroSignal.IsInitialized) return;
        HSMauiBridge.SetDeviceToken(deviceToken);
    }

    /// <summary>APNs kaydı başarısız oldu (günlüğe ve SDK hata raporuna yazılır).</summary>
    public static void FailedToRegisterForRemoteNotifications(NSError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!HeroSignal.IsInitialized) return;
        HSMauiBridge.SetDeviceTokenError(error);
    }
}

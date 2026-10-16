using Foundation;
using PushizoSdk.Native.iOS;

namespace PushizoSdk.Maui;

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
///         PushizoIos.RegisteredForRemoteNotifications(deviceToken);
///
///     [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
///     public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error) =&gt;
///         PushizoIos.FailedToRegisterForRemoteNotifications(error);
/// }
/// </code>
/// Zengin medya ve teslimat onayı için Notification Service Extension hedefi yerel <c>PushizoExtension</c> ile
/// kurulur (bkz. sdk/maui/README.md).
/// </summary>
public static class PushizoIos
{
    /// <summary>APNs cihaz token'ını SDK'ya iletir (abonelik kaydı/güncellemesi).</summary>
    public static void RegisteredForRemoteNotifications(NSData deviceToken)
    {
        ArgumentNullException.ThrowIfNull(deviceToken);
        if (!Pushizo.IsInitialized) return;
        HSMauiBridge.SetDeviceToken(deviceToken);
    }

    /// <summary>APNs kaydı başarısız oldu (günlüğe ve SDK hata raporuna yazılır).</summary>
    public static void FailedToRegisterForRemoteNotifications(NSError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!Pushizo.IsInitialized) return;
        HSMauiBridge.SetDeviceTokenError(error);
    }
}

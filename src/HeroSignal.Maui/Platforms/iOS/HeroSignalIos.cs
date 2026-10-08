using Foundation;
using HsBridge = global::HeroSignalSdk.Native.iOS.HSMauiBridge;

namespace HeroSignalSdk.Maui;

/// <summary>
/// iOS'a özgü APNs token iletimi. Köprü, uygulama temsilcisi bu geri çağrıları uygulamıyorsa onları kendisi ekler;
/// <c>AppDelegate</c> sınıfınız <c>RegisteredForRemoteNotifications</c> / <c>FailedToRegisterForRemoteNotifications</c>
/// metotlarını zaten geçersiz kılıyorsa bu metotları onların içinden çağırın.
/// </summary>
public static class HeroSignalIos
{
    /// <summary>APNs cihaz token'ını yerel SDK'ya iletir (abonelik oluşturulur/güncellenir).</summary>
    public static void SetDeviceToken(NSData deviceToken)
    {
        ArgumentNullException.ThrowIfNull(deviceToken);
        HsBridge.SetDeviceToken(deviceToken);
    }

    /// <summary>APNs kayıt hatasını yerel SDK'ya iletir (günlüğe yazılır, hata raporlanır).</summary>
    public static void SetDeviceTokenError(NSError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        HsBridge.SetDeviceTokenError(error);
    }
}

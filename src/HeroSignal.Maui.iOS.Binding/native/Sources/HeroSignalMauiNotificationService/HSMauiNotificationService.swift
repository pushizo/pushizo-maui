import Foundation
import HeroSignalExtension
// SPM: çekirdek tipler ayrı modülde; CocoaPods alt spec'leri tek modülde derlenir.
#if canImport(HeroSignalCore)
import HeroSignalCore
#endif
import UserNotifications

/**
 .NET MAUI Notification Service Extension köprüsü (Objective-C ABI). Uygulama uzantısı güvenli API'lerle derlenir
 (`APPLICATION_EXTENSION_API_ONLY`); ana köprüyü (UIApplication kullanan) içermez. C# NSE sınıfından:

 ```csharp
 [Register("NotificationService")]
 public class NotificationService : UNNotificationServiceExtension {
     readonly HSMauiNotificationService hs = new("app_…", "https://api.example.com", "group.com.example.app");
     public override void DidReceiveNotificationRequest(UNNotificationRequest request, Action<UNNotificationContent> handler)
         => hs.DidReceive(request, handler);
     public override void TimeWillExpire() => hs.ServiceExtensionTimeWillExpire();
 }
 ```
 */
@objc(HSMauiNotificationService)
public final class HSMauiNotificationService: NSObject {
    private let inner: HeroSignalNotificationServiceExtension

    @objc(initWithAppId:apiUrl:appGroup:)
    public init(appId: String, apiUrl: String, appGroup: String?) {
        inner = HeroSignalNotificationServiceExtension(appId: appId, apiUrl: apiUrl, appGroup: appGroup)
        super.init()
    }

    /// Zengin medya indirme, aksiyon kategorisi, rozet artışı ve `received` (confirmed delivery).
    @objc(didReceiveNotificationRequest:withContentHandler:)
    public func didReceive(_ request: UNNotificationRequest, withContentHandler handler: @escaping (UNNotificationContent) -> Void) {
        inner.didReceive(request, withContentHandler: handler)
    }

    /// Süre dolmadan son içeriği teslim eder.
    @objc(serviceExtensionTimeWillExpire)
    public func serviceExtensionTimeWillExpire() {
        inner.serviceExtensionTimeWillExpire()
    }
}

import Foundation
import HeroSignal
// SPM: çekirdek tipler ayrı modülde; CocoaPods alt spec'leri tek "HeroSignal" modülünde derlenir.
#if canImport(HeroSignalCore)
import HeroSignalCore
#endif
import ObjectiveC
import UIKit
import UserNotifications

/**
 .NET MAUI → HeroSignal iOS SDK köprüsü (Objective-C ABI). Swift'e özgü tipler (async, struct, enum) .NET binding'ine
 uygun biçime çevrilir: karmaşık değerler JSON metni, zamanlar epoch milisaniye. JSON biçimleri
 `HeroSignal.Maui/Internal/BridgeJson.cs` ile eşleşir.

 Olaylar tek işleyiciyle (`setEventHandler:`) ana iş parçacığında iletilir; işleyici kaydedilmeden gelen olaylar
 sıraya alınır. Olay türleri: `click`, `foreground`, `subscription`, `permission`, `inAppClick`, `inAppLifecycle`.
 */
@objc(HSMauiBridge)
public final class HSMauiBridge: NSObject {
    public typealias EventHandler = (String, String) -> Void

    private static var eventHandler: EventHandler?
    private static var pendingEvents: [(String, String)] = []
    private static var listenersRegistered = false
    private static var foregroundEvents: [String: HSForegroundEvent] = [:]
    private static var cachedPermission = false
    private static var cachedOptedIn = false
    private static let lifecycle = LifecycleListener()

    // MARK: Başlatma

    /// Olay işleyicisi; sıradaki olaylar hemen iletilir. `startWithAppId:` öncesinde çağrılmalıdır.
    @objc(setEventHandler:)
    public static func setEventHandler(_ handler: @escaping (String, String) -> Void) {
        onMain {
            eventHandler = handler
            let queued = pendingEvents
            pendingEvents.removeAll()
            queued.forEach { handler($0.0, $0.1) }
        }
    }

    /// `optionsJson`: `{"apiUrl","requiresPrivacyConsent","logLevel"(0-4),"appGroup","apnsEnvironment","provisionalAuthorization"}`.
    @objc(startWithAppId:optionsJson:)
    public static func start(appId: String, optionsJson: String) {
        let o = object(optionsJson)
        HeroSignal.initialize(appId) { options in
            if let apiUrl = o["apiUrl"] as? String { options.apiUrl = apiUrl }
            options.requiresPrivacyConsent = o["requiresPrivacyConsent"] as? Bool ?? false
            options.logLevel = logLevel(o["logLevel"] as? Int)
            options.appGroup = o["appGroup"] as? String
            options.apnsEnvironment = o["apnsEnvironment"] as? String
            options.provisionalAuthorization = o["provisionalAuthorization"] as? Bool ?? false
        }
        installAppDelegateHooks()
        guard !listenersRegistered else { refreshState(); return }
        listenersRegistered = true
        HeroSignal.Notifications.addForegroundLifecycleListener { event in
            let eventId = UUID().uuidString
            foregroundEvents[eventId] = event
            // `display()` için süre dolduğunda yerel SDK bildirimi kapatır; kayıt da temizlenir.
            DispatchQueue.main.asyncAfter(deadline: .now() + HSForegroundEvent.displayTimeout + 1) { foregroundEvents.removeValue(forKey: eventId) }
            emit("foreground", ["eventId": eventId, "notification": map(event.notification)])
        }
        HeroSignal.User.pushSubscription.addObserver { state in
            let granted = state.permission == .authorized || state.permission == .provisional
            cachedOptedIn = state.optedIn
            emit("subscription", map(state))
            updatePermission(granted)
        }
        HeroSignal.InAppMessages.addClickListener { e in
            emit("inAppClick", ["messageId": e.messageId, "actionId": e.actionId, "url": nn(e.url)])
        }
        HeroSignal.InAppMessages.addLifecycleListener(lifecycle)
        // Bekleyen (uygulamayı açan) tıklama dinleyici eklenince hemen iletilir.
        HeroSignal.Notifications.addClickListener { e in
            emit("click", ["notification": map(e.notification), "actionId": nn(e.actionId), "url": nn(e.url)])
        }
        NotificationCenter.default.addObserver(forName: UIApplication.didBecomeActiveNotification, object: nil, queue: .main) { _ in refreshState() }
        refreshState()
    }

    @objc(setConsentGiven:)
    public static func setConsentGiven(_ given: Bool) { HeroSignal.setConsentGiven(given) }

    @objc(loginWithExternalId:identityHash:)
    public static func login(externalId: String, identityHash: String?) { HeroSignal.login(externalId, identityHash: identityHash) }

    @objc(logout)
    public static func logout() { HeroSignal.logout() }

    /// 0 none, 1 error, 2 warn, 3 info, 4 debug.
    @objc(setLogLevel:)
    public static func setLogLevel(_ level: Int) { HeroSignal.Debug.setLogLevel(logLevel(level)) }

    // MARK: APNs token

    @objc(setDeviceToken:)
    public static func setDeviceToken(_ token: Data) { HeroSignal.setDeviceToken(token) }

    @objc(setDeviceTokenError:)
    public static func setDeviceTokenError(_ error: Error) { HeroSignal.setDeviceTokenError(error) }

    // MARK: Kullanıcı

    @objc(externalId) public static var externalId: String? { HeroSignal.User.externalId }
    @objc(userId) public static var userId: String? { HeroSignal.User.id }

    @objc(addTagsJson:)
    public static func addTags(json: String) { HeroSignal.User.addTags(stringMap(json)) }

    @objc(removeTagsJson:)
    public static func removeTags(json: String) { HeroSignal.User.removeTags(stringList(json)) }

    @objc(tagsJson) public static var tagsJson: String { encode(HeroSignal.User.getTags()) }

    @objc(addAliasesJson:)
    public static func addAliases(json: String) { HeroSignal.User.addAliases(stringMap(json)) }

    @objc(removeAlias:)
    public static func removeAlias(_ label: String) { HeroSignal.User.removeAlias(label) }

    @objc(setLanguage:)
    public static func setLanguage(_ code: String) { HeroSignal.User.setLanguage(code) }

    @objc(addEmail:) public static func addEmail(_ email: String) { HeroSignal.User.addEmail(email) }
    @objc(removeEmail:) public static func removeEmail(_ email: String) { HeroSignal.User.removeEmail(email) }
    @objc(addSms:) public static func addSms(_ phone: String) { HeroSignal.User.addSms(phone) }
    @objc(removeSms:) public static func removeSms(_ phone: String) { HeroSignal.User.removeSms(phone) }
    @objc(emailsJson) public static var emailsJson: String { encode(HeroSignal.User.emails) }
    @objc(smsNumbersJson) public static var smsNumbersJson: String { encode(HeroSignal.User.smsNumbers) }

    // MARK: Push aboneliği

    /// Son bilinen durum (`{"id","token","optedIn","permission"}`); izin durumu uygulama etkinleştiğinde yenilenir.
    @objc(pushSubscriptionJson) public static var pushSubscriptionJson: String {
        encode(["id": nn(HeroSignal.User.pushSubscription.id), "token": nn(HeroSignal.User.pushSubscription.token),
                "optedIn": cachedOptedIn, "permission": cachedPermission])
    }

    @objc(optIn) public static func optIn() { HeroSignal.User.pushSubscription.optIn() }
    @objc(optOut) public static func optOut() { HeroSignal.User.pushSubscription.optOut() }

    // MARK: Bildirimler

    @objc(notificationPermission) public static var notificationPermission: Bool { cachedPermission }

    /// İzin ister; reddedilmiş izinde `fallbackToSettings` ise uygulama ayarları açılır. Tamamlama ana iş parçacığında.
    @objc(requestPermissionWithFallbackToSettings:completion:)
    public static func requestPermission(fallbackToSettings: Bool, completion: @escaping (Bool) -> Void) {
        UNUserNotificationCenter.current().getNotificationSettings { settings in
            if settings.authorizationStatus == .denied {
                onMain {
                    updatePermission(false)
                    if fallbackToSettings, let url = URL(string: UIApplication.openNotificationSettingsURLStringCompat) {
                        UIApplication.shared.open(url)
                    }
                    completion(false)
                }
                return
            }
            HeroSignal.Notifications.requestPermission { granted in
                onMain {
                    updatePermission(granted)
                    completion(granted)
                }
            }
        }
    }

    @objc(clearAllNotifications) public static func clearAllNotifications() { HeroSignal.Notifications.clearAll() }

    @objc(removeNotification:)
    public static func removeNotification(_ id: String) { HeroSignal.Notifications.removeNotification(id: id) }

    @objc(removeGroupedNotifications:)
    public static func removeGroupedNotifications(_ group: String) { HeroSignal.Notifications.removeGroupedNotifications(group: group) }

    @objc(foregroundPreventDefault:)
    public static func foregroundPreventDefault(_ eventId: String) { foregroundEvents[eventId]?.preventDefault() }

    @objc(foregroundDisplay:)
    public static func foregroundDisplay(_ eventId: String) { foregroundEvents.removeValue(forKey: eventId)?.display() }

    // MARK: In-app

    @objc(addTriggersJson:) public static func addTriggers(json: String) { HeroSignal.InAppMessages.addTriggers(stringMap(json)) }
    @objc(removeTriggersJson:) public static func removeTriggers(json: String) { HeroSignal.InAppMessages.removeTriggers(stringList(json)) }
    @objc(clearTriggers) public static func clearTriggers() { HeroSignal.InAppMessages.clearTriggers() }
    @objc(triggersJson) public static var triggersJson: String { encode(HeroSignal.InAppMessages.getTriggers()) }
    @objc(setScreen:) public static func setScreen(_ name: String?) { HeroSignal.InAppMessages.setScreen(name) }

    @objc(inAppPaused) public static var inAppPaused: Bool {
        get { HeroSignal.InAppMessages.paused }
        set { HeroSignal.InAppMessages.paused = newValue }
    }

    // MARK: Bildirim kutusu (tamamlama ana iş parçacığında; hata alanı: domain HeroSignal, userInfo["code"])

    @objc(inboxListWithLimit:cursor:completion:)
    public static func inboxList(limit: Int, cursor: String?, completion: @escaping (String?, NSError?) -> Void) {
        run(completion) { try await encode(map(HeroSignal.Inbox.list(limit: limit, cursor: cursor))) }
    }

    @objc(inboxMarkRead:completion:)
    public static func inboxMarkRead(_ id: String, completion: @escaping (String?, NSError?) -> Void) {
        run(completion) { try await encode(map(HeroSignal.Inbox.markRead(id))) }
    }

    /// Sonuç `{"count":n}` (kalan okunmamış).
    @objc(inboxMarkAllReadWithCompletion:)
    public static func inboxMarkAllRead(completion: @escaping (String?, NSError?) -> Void) {
        run(completion) { try await encode(["count": HeroSignal.Inbox.markAllRead()]) }
    }

    @objc(inboxArchive:completion:)
    public static func inboxArchive(_ id: String, completion: @escaping (String?, NSError?) -> Void) {
        run(completion) { try await HeroSignal.Inbox.archive(id); return "{}" }
    }

    /// Sonuç `{"count":n}`.
    @objc(inboxUnreadCountWithCompletion:)
    public static func inboxUnreadCount(completion: @escaping (String?, NSError?) -> Void) {
        run(completion) { try await encode(["count": HeroSignal.Inbox.unreadCount()]) }
    }

    // MARK: Oturum

    /// `value` NSNumber (double) ya da nil.
    @objc(sendEvent:propertiesJson:value:)
    public static func sendEvent(_ name: String, propertiesJson: String, value: NSNumber?) {
        HeroSignal.Session.sendEvent(name, properties: object(propertiesJson), value: value?.doubleValue)
    }

    // MARK: Yardımcılar

    private final class LifecycleListener: HSInAppLifecycleListener {
        func onWillDisplay(_ message: HSInAppMessage) { emitLifecycle("willDisplay", message) }
        func onDidDisplay(_ message: HSInAppMessage) { emitLifecycle("didDisplay", message) }
        func onWillDismiss(_ message: HSInAppMessage) { emitLifecycle("willDismiss", message) }
        func onDidDismiss(_ message: HSInAppMessage) { emitLifecycle("didDismiss", message) }

        private func emitLifecycle(_ phase: String, _ message: HSInAppMessage) {
            let raw = (try? JSONSerialization.data(withJSONObject: message.raw)).flatMap { String(data: $0, encoding: .utf8) }
            HSMauiBridge.emit("inAppLifecycle", ["phase": phase, "messageId": message.id, "json": HSMauiBridge.nn(raw)])
        }
    }

    /// İzin ve abonelik durumunu sistemden yeniler (açılışta ve uygulama etkinleştiğinde).
    private static func refreshState() {
        Task {
            let state = await HeroSignal.User.pushSubscription.state()
            await MainActor.run {
                cachedOptedIn = state.optedIn
                emit("subscription", map(state))
                updatePermission(state.permission == .authorized || state.permission == .provisional)
            }
        }
    }

    private static func updatePermission(_ granted: Bool) {
        onMain {
            guard granted != cachedPermission else { return }
            cachedPermission = granted
            emit("permission", ["permission": granted])
        }
    }

    /// Uygulama temsilcisi APNs geri çağrılarını uygulamıyorsa eklenir (mevcut uygulama değiştirilmez, swizzling yok).
    /// Temsilci bunları zaten uyguluyorsa uygulama `HeroSignalIos.SetDeviceToken` ile token'ı iletir.
    private static func installAppDelegateHooks() {
        onMain {
            guard let delegate = UIApplication.shared.delegate else { return }
            let cls: AnyClass = type(of: delegate)
            let tokenSelector = #selector(UIApplicationDelegate.application(_:didRegisterForRemoteNotificationsWithDeviceToken:))
            if !delegate.responds(to: tokenSelector) {
                let block: @convention(block) (AnyObject, UIApplication, Data) -> Void = { _, _, token in HeroSignal.setDeviceToken(token) }
                class_addMethod(cls, tokenSelector, imp_implementationWithBlock(block), "v@:@@")
            }
            let errorSelector = #selector(UIApplicationDelegate.application(_:didFailToRegisterForRemoteNotificationsWithError:))
            if !delegate.responds(to: errorSelector) {
                let block: @convention(block) (AnyObject, UIApplication, NSError) -> Void = { _, _, error in HeroSignal.setDeviceTokenError(error) }
                class_addMethod(cls, errorSelector, imp_implementationWithBlock(block), "v@:@@")
            }
        }
    }

    fileprivate static func emit(_ type: String, _ payload: [String: Any]) {
        let json = encode(payload)
        onMain {
            if let handler = eventHandler { handler(type, json) } else { pendingEvents.append((type, json)) }
        }
    }

    private static func run(_ completion: @escaping (String?, NSError?) -> Void, _ body: @escaping () async throws -> String) {
        Task {
            do {
                let value = try await body()
                await MainActor.run { completion(value, nil) }
            } catch let error as HSApiError {
                let nsError = NSError(domain: "HeroSignal", code: error.status, userInfo: [NSLocalizedDescriptionKey: error.message, "code": error.code, "status": error.status])
                await MainActor.run { completion(nil, nsError) }
            } catch {
                let nsError = NSError(domain: "HeroSignal", code: 0, userInfo: [NSLocalizedDescriptionKey: "\(error)", "code": "NOT_READY"])
                await MainActor.run { completion(nil, nsError) }
            }
        }
    }

    private static func onMain(_ block: @escaping () -> Void) {
        if Thread.isMainThread { block() } else { DispatchQueue.main.async(execute: block) }
    }

    private static func logLevel(_ value: Int?) -> HSLog.Level { HSLog.Level(rawValue: value ?? 2) ?? .warn }

    private static func object(_ json: String) -> [String: Any] {
        guard let data = json.data(using: .utf8), let value = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return [:] }
        return value
    }

    private static func stringMap(_ json: String) -> [String: String] { object(json).compactMapValues { $0 as? String } }

    private static func stringList(_ json: String) -> [String] {
        guard let data = json.data(using: .utf8), let value = try? JSONSerialization.jsonObject(with: data) as? [Any] else { return [] }
        return value.compactMap { $0 as? String }
    }

    private static func encode(_ value: Any) -> String {
        guard JSONSerialization.isValidJSONObject(value), let data = try? JSONSerialization.data(withJSONObject: value) else { return "{}" }
        return String(data: data, encoding: .utf8) ?? "{}"
    }

    private static func millis(_ date: Date?) -> Any { nn(date.map { Int64($0.timeIntervalSince1970 * 1000) }) }

    /// JSONSerialization Optional kabul etmez; nil → NSNull (JSON null).
    fileprivate static func nn(_ value: Any?) -> Any { value ?? NSNull() }

    private static func map(_ n: HSNotification) -> [String: Any] {
        ["title": nn(n.title), "body": nn(n.body), "imageUrl": nn(n.imageUrl), "url": nn(n.url),
         "deliveryId": nn(n.deliveryId), "messageId": nn(n.messageId),
         // iOS'ta tepsiden kaldırma teslimat kimliğiyle eşleşir (HeroSignal.Notifications.removeNotification(id:)).
         "notificationId": nn(n.deliveryId),
         "actions": n.actions.map { ["id": $0.id, "title": $0.title, "url": nn($0.url), "icon": nn($0.icon)] },
         "data": n.data]
    }

    private static func map(_ s: HSPushSubscriptionState) -> [String: Any] {
        ["id": nn(s.id), "token": nn(s.token), "optedIn": s.optedIn, "permission": s.permission == .authorized || s.permission == .provisional]
    }

    private static func map(_ item: HSInboxItem) -> [String: Any] {
        ["id": item.id, "messageId": nn(item.messageId), "title": nn(item.title), "body": nn(item.body),
         "url": nn(item.url), "imageUrl": nn(item.imageUrl), "data": item.data, "read": item.read,
         "readAt": millis(item.readAt), "archivedAt": millis(item.archivedAt), "expiresAt": millis(item.expiresAt), "createdAt": millis(item.createdAt)]
    }

    private static func map(_ page: HSInboxPage) -> [String: Any] {
        ["items": page.items.map { map($0) }, "unreadCount": page.unreadCount, "nextCursor": nn(page.nextCursor), "hasMore": page.hasMore]
    }
}

private extension UIApplication {
    /// iOS 16+ doğrudan bildirim ayarları; öncesinde uygulama ayarları.
    static var openNotificationSettingsURLStringCompat: String {
        if #available(iOS 16.0, *) { return UIApplication.openNotificationSettingsURLString }
        return UIApplication.openSettingsURLString
    }
}

import Foundation
import HeroSignal
// SPM: çekirdek tipler ayrı modülde; CocoaPods'ta tek "HeroSignal" modülündedir.
#if canImport(HeroSignalCore)
import HeroSignalCore
#endif
import UIKit
import UserNotifications

/**
 .NET MAUI → iOS SDK köprüsü (@objc cephe). Yerel Swift SDK'sı (`enum HeroSignal`, async/await, Swift struct'ları)
 Objective-C'ye açık olmadığından .NET binding'i bu sınıfı bağlar. Yüzey yalnızca NSString (JSON), BOOL, NSInteger,
 double ve blok geri çağrıları kullanır; JSON biçimleri `HeroSignal.Maui` içindeki `BridgeJson`/`BridgeEvents` ve
 Android köprüsü (`HeroSignalMauiBridge.kt`) ile aynıdır. Zamanlar epoch milisaniyedir.
 */
@objc(HSMauiBridge)
public final class HSMauiBridge: NSObject {
    private static let lock = NSLock()
    private static var listener: ((String, String) -> Void)?
    private static var listenersRegistered = false
    private static var foregroundEvents: [String: HSForegroundEvent] = [:]
    /// Son bilinen abonelik durumu; izin bilgisi iOS'ta yalnızca eşzamansız okunabildiğinden önbelleklenir.
    private static var cachedOptedIn = false
    private static var cachedPermission = false
    private static var cachedStateKnown = false
    private static let lifecycleRelay = LifecycleRelay()

    // MARK: Başlatma

    /// `optionsJson`: `apiUrl`, `requiresPrivacyConsent`, `logLevel` (0-4), `iosAppGroup`, `iosApnsEnvironment`,
    /// `iosProvisionalAuthorization`. Olaylar `listener(tür, json)` ile eşzamanlı iletilir.
    @objc(startWithAppId:optionsJson:listener:)
    public static func start(appId: String, optionsJson: String, listener: @escaping (String, String) -> Void) {
        let o = (jsonObject(optionsJson) as? [String: Any]) ?? [:]
        lock.lock()
        self.listener = listener
        let registerListeners = !listenersRegistered
        listenersRegistered = true
        lock.unlock()

        HeroSignal.initialize(appId) { options in
            if let apiUrl = o["apiUrl"] as? String, !apiUrl.isEmpty { options.apiUrl = apiUrl }
            options.requiresPrivacyConsent = o["requiresPrivacyConsent"] as? Bool ?? false
            options.logLevel = logLevel((o["logLevel"] as? NSNumber)?.intValue ?? HSLog.Level.warn.rawValue)
            options.appGroup = nonEmpty(o["iosAppGroup"])
            options.apnsEnvironment = nonEmpty(o["iosApnsEnvironment"])
            options.provisionalAuthorization = o["iosProvisionalAuthorization"] as? Bool ?? false
        }

        if registerListeners {
            HeroSignal.User.pushSubscription.addObserver { state in
                updateCache(state)
                emit("subscription", subscriptionJson(id: state.id, token: state.token, optedIn: state.optedIn, permission: isGranted(state.permission)))
            }
            HeroSignal.Notifications.addForegroundLifecycleListener { event in
                let eventId = UUID().uuidString
                lock.lock(); foregroundEvents[eventId] = event; lock.unlock()
                // C# işleyicileri bu çağrı içinde eşzamanlı çalışır; preventDefault kararı SDK tarafından hemen okunur.
                emit("foreground", ["eventId": eventId, "notification": notificationJson(event.notification)])
                if !event.prevented {
                    lock.lock(); foregroundEvents.removeValue(forKey: eventId); lock.unlock()
                } else {
                    // SDK süre dolunca bildirimi göstermeden kapatır; karar kaydı da temizlenir.
                    DispatchQueue.main.asyncAfter(deadline: .now() + HSForegroundEvent.displayTimeout + 1) {
                        lock.lock(); foregroundEvents.removeValue(forKey: eventId); lock.unlock()
                    }
                }
            }
            HeroSignal.InAppMessages.addClickListener { event in
                emit("inAppClick", ["messageId": event.messageId, "actionId": event.actionId, "url": event.url ?? NSNull()])
            }
            HeroSignal.InAppMessages.addLifecycleListener(lifecycleRelay)
            // Yerel SDK izin gözlemcisi → `permission` olayı (`{"permission":bool}`): requestPermission sonrası ve uygulama
            // yeniden etkinleştiğinde (izin Ayarlar'dan değişmiş olabilir) etkin izin değiştiyse çağrılır.
            HeroSignal.Notifications.addPermissionObserver { granted in
                lock.lock(); cachedPermission = granted; lock.unlock()
                emit("permission", ["permission": granted])
            }
            // En son: uygulamayı açan (bekleyen) tıklama bu çağrıda iletilir.
            HeroSignal.Notifications.addClickListener { event in
                emit("click", [
                    "notification": notificationJson(event.notification),
                    "actionId": event.actionId ?? NSNull(),
                    "url": event.url ?? NSNull(),
                ])
            }
        }
        refreshState()
    }

    /// AppDelegate `application:didRegisterForRemoteNotificationsWithDeviceToken:` içinden.
    @objc(setDeviceToken:)
    public static func setDeviceToken(_ deviceToken: Data) {
        HeroSignal.setDeviceToken(deviceToken)
    }

    /// AppDelegate `application:didFailToRegisterForRemoteNotificationsWithError:` içinden.
    @objc(setDeviceTokenError:)
    public static func setDeviceTokenError(_ error: NSError) {
        HeroSignal.setDeviceTokenError(error)
    }

    @objc(setConsentGiven:)
    public static func setConsentGiven(_ given: Bool) { HeroSignal.setConsentGiven(given) }

    @objc(loginWithExternalId:identityHash:)
    public static func login(externalId: String, identityHash: String?) {
        HeroSignal.login(externalId, identityHash: identityHash)
    }

    @objc(logout)
    public static func logout() { HeroSignal.logout() }

    /// 0 none, 1 error, 2 warn, 3 info, 4 debug.
    @objc(applyLogLevel:)
    public static func applyLogLevel(_ level: Int) { HeroSignal.Debug.setLogLevel(logLevel(level)) }

    // MARK: Kullanıcı

    @objc(externalId)
    public static func externalId() -> String? { HeroSignal.User.externalId }

    @objc(userId)
    public static func userId() -> String? { HeroSignal.User.id }

    @objc(addTags:)
    public static func addTags(_ tagsJson: String) { HeroSignal.User.addTags(stringMap(tagsJson)) }

    @objc(removeTags:)
    public static func removeTags(_ keysJson: String) { HeroSignal.User.removeTags(stringList(keysJson)) }

    @objc(tags)
    public static func tags() -> String { jsonString(HeroSignal.User.getTags()) }

    @objc(addAliases:)
    public static func addAliases(_ aliasesJson: String) { HeroSignal.User.addAliases(stringMap(aliasesJson)) }

    @objc(removeAlias:)
    public static func removeAlias(_ label: String) { HeroSignal.User.removeAlias(label) }

    @objc(setLanguage:)
    public static func setLanguage(_ code: String) { HeroSignal.User.setLanguage(code) }

    @objc(addEmail:)
    public static func addEmail(_ email: String) { HeroSignal.User.addEmail(email) }

    @objc(removeEmail:)
    public static func removeEmail(_ email: String) { HeroSignal.User.removeEmail(email) }

    @objc(addSms:)
    public static func addSms(_ phoneNumber: String) { HeroSignal.User.addSms(phoneNumber) }

    @objc(removeSms:)
    public static func removeSms(_ phoneNumber: String) { HeroSignal.User.removeSms(phoneNumber) }

    @objc(emails)
    public static func emails() -> String { jsonString(HeroSignal.User.emails) }

    @objc(smsNumbers)
    public static func smsNumbers() -> String { jsonString(HeroSignal.User.smsNumbers) }

    // MARK: Push aboneliği ve izin

    /// `{"id","token","optedIn","permission"}`; id/token canlı, optedIn/permission son yenilemeden.
    @objc(pushSubscription)
    public static func pushSubscription() -> String {
        lock.lock()
        let optedIn = cachedOptedIn
        let permission = cachedPermission
        lock.unlock()
        return jsonString(subscriptionJson(id: HeroSignal.User.pushSubscription.id, token: HeroSignal.User.pushSubscription.token, optedIn: optedIn, permission: permission))
    }

    @objc(optIn)
    public static func optIn() { HeroSignal.User.pushSubscription.optIn() }

    @objc(optOut)
    public static func optOut() { HeroSignal.User.pushSubscription.optOut() }

    @objc(permission)
    public static func permission() -> Bool {
        lock.lock(); defer { lock.unlock() }
        return cachedPermission
    }

    /// Sistem iznini ister; izin daha önce reddedildiyse ve `fallbackToSettings` true ise uygulamanın bildirim ayarları açılır.
    @objc(requestPermissionWithFallbackToSettings:completion:)
    public static func requestPermission(fallbackToSettings: Bool, completion: @escaping (Bool) -> Void) {
        Task {
            let status = await HeroSignal.Notifications.permission()
            if status == .denied {
                if fallbackToSettings { await openNotificationSettings() }
                refreshState()
                completion(false)
                return
            }
            HeroSignal.Notifications.requestPermission { granted in
                refreshState()
                completion(granted)
            }
        }
    }

    /// Bildirim merkezindeki tüm bildirimleri kaldırır ve rozeti sıfırlar.
    @objc(clearAllNotifications)
    public static func clearAllNotifications() { HeroSignal.Notifications.clearAll() }

    // MARK: In-app (SDK in-app durumu ana iş parçacığında tutulur)

    @objc(addTriggers:)
    public static func addTriggers(_ triggersJson: String) {
        let triggers = stringMap(triggersJson)
        onMain { HeroSignal.InAppMessages.addTriggers(triggers) }
    }

    @objc(removeTriggers:)
    public static func removeTriggers(_ keysJson: String) {
        let keys = stringList(keysJson)
        onMain { HeroSignal.InAppMessages.removeTriggers(keys) }
    }

    @objc(clearTriggers)
    public static func clearTriggers() { onMain { HeroSignal.InAppMessages.clearTriggers() } }

    @objc(triggers)
    public static func triggers() -> String { readOnMain { jsonString(HeroSignal.InAppMessages.getTriggers()) } }

    @objc(setScreen:)
    public static func setScreen(_ name: String?) { onMain { HeroSignal.InAppMessages.setScreen(name) } }

    @objc(inAppPaused)
    public static func inAppPaused() -> Bool { readOnMain { HeroSignal.InAppMessages.paused } }

    @objc(updateInAppPaused:)
    public static func updateInAppPaused(_ paused: Bool) { onMain { HeroSignal.InAppMessages.paused = paused } }

    // MARK: Bildirim kutusu

    @objc(inboxListWithLimit:cursor:completion:)
    public static func inboxList(limit: Int, cursor: String?, completion: @escaping (String?, String?, String?) -> Void) {
        inbox(completion) {
            let page = try await HeroSignal.Inbox.list(limit: limit, cursor: cursor)
            return jsonString(pageJson(page))
        }
    }

    @objc(inboxMarkRead:completion:)
    public static func inboxMarkRead(_ id: String, completion: @escaping (String?, String?, String?) -> Void) {
        inbox(completion) {
            let item = try await HeroSignal.Inbox.markRead(id)
            return jsonString(itemJson(item))
        }
    }

    @objc(inboxMarkAllReadWithCompletion:)
    public static func inboxMarkAllRead(completion: @escaping (String?, String?, String?) -> Void) {
        inbox(completion) {
            let unread = try await HeroSignal.Inbox.markAllRead()
            return jsonString(["unreadCount": unread])
        }
    }

    @objc(inboxArchive:completion:)
    public static func inboxArchive(_ id: String, completion: @escaping (String?, String?, String?) -> Void) {
        inbox(completion) {
            try await HeroSignal.Inbox.archive(id)
            return nil
        }
    }

    @objc(inboxUnreadCountWithCompletion:)
    public static func inboxUnreadCount(completion: @escaping (String?, String?, String?) -> Void) {
        inbox(completion) {
            let unread = try await HeroSignal.Inbox.unreadCount()
            return jsonString(["unreadCount": unread])
        }
    }

    // MARK: Tercihler (ADR-0019 §4)

    /// Başarı JSON'u: `{"preferences":[…],"categories":[…]}` (bkz. `preferencesResultJson`).
    @objc(getPreferencesWithCompletion:)
    public static func getPreferences(completion: @escaping (String?, String?, String?) -> Void) {
        inbox(completion) {
            let result = try await HeroSignal.User.getPreferences()
            return jsonString(preferencesResultJson(result))
        }
    }

    /// `preferencesJson`: `[{"category","channel":null|"…","optedIn"}]`; doğrulama yerel SDK'dadır (geçersizse
    /// `INVALID_ARGUMENT`). Başarı JSON'u `getPreferences` ile aynıdır (güncel liste).
    @objc(setPreferences:completion:)
    public static func setPreferences(_ preferencesJson: String, completion: @escaping (String?, String?, String?) -> Void) {
        let inputs = preferenceInputs(preferencesJson)
        inbox(completion) {
            let result: HSPreferencesResult = try await HeroSignal.User.setPreferences(inputs)
            return jsonString(preferencesResultJson(result))
        }
    }

    // MARK: Oturum

    /// `propertiesJson` JSON nesnesi (iç içe nesne/dizi desteklenir); `hasValue` false ise değer gönderilmez.
    @objc(sendEvent:propertiesJson:value:hasValue:)
    public static func sendEvent(_ name: String, propertiesJson: String, value: Double, hasValue: Bool) {
        let properties = (jsonObject(propertiesJson) as? [String: Any]) ?? [:]
        HeroSignal.Session.sendEvent(name, properties: properties, value: hasValue ? value : nil)
    }

    // MARK: Ön plan kararı

    @objc(foregroundPreventDefault:)
    public static func foregroundPreventDefault(_ eventId: String) {
        lock.lock(); let event = foregroundEvents[eventId]; lock.unlock()
        event?.preventDefault()
    }

    @objc(foregroundDisplay:)
    public static func foregroundDisplay(_ eventId: String) {
        lock.lock(); let event = foregroundEvents.removeValue(forKey: eventId); lock.unlock()
        event?.display()
    }

    // MARK: Yardımcılar

    private final class LifecycleRelay: HSInAppLifecycleListener {
        func onWillDisplay(_ message: HSInAppMessage) { send("willDisplay", message) }
        func onDidDisplay(_ message: HSInAppMessage) { send("didDisplay", message) }
        func onWillDismiss(_ message: HSInAppMessage) { send("willDismiss", message) }
        func onDidDismiss(_ message: HSInAppMessage) { send("didDismiss", message) }

        private func send(_ event: String, _ message: HSInAppMessage) {
            HSMauiBridge.emit("inAppLifecycle", ["event": event, "messageId": message.id, "json": HSMauiBridge.jsonString(message.raw)])
        }
    }

    private static func emit(_ type: String, _ payload: [String: Any]) {
        lock.lock(); let l = listener; lock.unlock()
        l?(type, jsonString(payload))
    }

    /// İzin ve opt-in durumunu eşzamansız okur; değiştiyse `subscription` olayı üretir.
    private static func refreshState() {
        Task {
            let state = await HeroSignal.User.pushSubscription.state()
            if updateCache(state) {
                emit("subscription", subscriptionJson(id: state.id, token: state.token, optedIn: state.optedIn, permission: isGranted(state.permission)))
            }
        }
    }

    /// Önbelleği günceller; önceki bilinen durumdan farklıysa true.
    @discardableResult
    private static func updateCache(_ state: HSPushSubscriptionState) -> Bool {
        let granted = isGranted(state.permission)
        lock.lock(); defer { lock.unlock() }
        let changed = !cachedStateKnown || cachedOptedIn != state.optedIn || cachedPermission != granted
        cachedStateKnown = true
        cachedOptedIn = state.optedIn
        cachedPermission = granted
        return changed
    }

    private static func isGranted(_ status: UNAuthorizationStatus) -> Bool {
        switch status {
        case .authorized, .provisional, .ephemeral: return true
        default: return false
        }
    }

    @MainActor
    private static func openNotificationSettings() {
        let raw: String
        if #available(iOS 16.0, *) {
            raw = UIApplication.openNotificationSettingsURLString
        } else {
            raw = UIApplication.openSettingsURLString
        }
        guard let url = URL(string: raw) else { return }
        UIApplication.shared.open(url)
    }

    private static func inbox(_ completion: @escaping (String?, String?, String?) -> Void, _ body: @escaping () async throws -> String?) {
        Task {
            do {
                completion(try await body(), nil, nil)
            } catch let error as HSApiError {
                completion(nil, error.code, error.message)
            } catch let error as HSError {
                switch error {
                case .notInitialized: completion(nil, "NOT_INITIALIZED", "HeroSignal başlatılmadı")
                case .noSubscription: completion(nil, "NOT_READY", "Abonelik henüz kaydedilmedi")
                case .noUser: completion(nil, "NOT_READY", "Kullanıcı henüz kaydedilmedi")
                case .invalidArgument(let message): completion(nil, "INVALID_ARGUMENT", message)
                case .invalidResponse: completion(nil, "INVALID_RESPONSE", "Sunucu yanıtı ayrıştırılamadı")
                }
            } catch {
                completion(nil, "HS_ERROR", error.localizedDescription)
            }
        }
    }

    private static func onMain(_ block: @escaping () -> Void) {
        if Thread.isMainThread { block() } else { DispatchQueue.main.async(execute: block) }
    }

    private static func readOnMain<T>(_ block: () -> T) -> T {
        if Thread.isMainThread { return block() }
        return DispatchQueue.main.sync(execute: block)
    }

    private static func logLevel(_ value: Int) -> HSLog.Level { HSLog.Level(rawValue: value) ?? .warn }

    private static func nonEmpty(_ value: Any?) -> String? {
        guard let s = value as? String, !s.isEmpty else { return nil }
        return s
    }

    private static func jsonObject(_ json: String) -> Any? {
        guard let data = json.data(using: .utf8) else { return nil }
        return try? JSONSerialization.jsonObject(with: data)
    }

    private static func stringMap(_ json: String) -> [String: String] {
        guard let object = jsonObject(json) as? [String: Any] else { return [:] }
        return object.compactMapValues { $0 as? String }
    }

    private static func stringList(_ json: String) -> [String] {
        (jsonObject(json) as? [Any])?.compactMap { $0 as? String } ?? []
    }

    fileprivate static func jsonString(_ value: Any) -> String {
        guard JSONSerialization.isValidJSONObject(value),
              let data = try? JSONSerialization.data(withJSONObject: value),
              let text = String(data: data, encoding: .utf8) else { return "{}" }
        return text
    }

    private static func millis(_ date: Date?) -> Any {
        guard let date else { return NSNull() }
        return NSNumber(value: Int64((date.timeIntervalSince1970 * 1000).rounded()))
    }

    private static func notificationJson(_ n: HSNotification) -> [String: Any] {
        [
            "title": n.title ?? NSNull(),
            "body": n.body ?? NSNull(),
            "imageUrl": n.imageUrl ?? NSNull(),
            "url": n.url ?? NSNull(),
            "deliveryId": n.deliveryId ?? NSNull(),
            "messageId": n.messageId ?? NSNull(),
            "actions": n.actions.map { a -> [String: Any] in
                ["id": a.id, "title": a.title, "url": a.url ?? NSNull(), "icon": a.icon ?? NSNull()]
            },
            "data": n.data,
        ]
    }

    private static func subscriptionJson(id: String?, token: String?, optedIn: Bool, permission: Bool) -> [String: Any] {
        ["id": id ?? NSNull(), "token": token ?? NSNull(), "optedIn": optedIn, "permission": permission]
    }

    private static func itemJson(_ item: HSInboxItem) -> [String: Any] {
        [
            "id": item.id,
            "messageId": item.messageId ?? NSNull(),
            "title": item.title ?? NSNull(),
            "body": item.body ?? NSNull(),
            "url": item.url ?? NSNull(),
            "imageUrl": item.imageUrl ?? NSNull(),
            "data": item.data,
            "read": item.read,
            "readAt": millis(item.readAt),
            "archivedAt": millis(item.archivedAt),
            "expiresAt": millis(item.expiresAt),
            "createdAt": millis(item.createdAt),
        ]
    }

    /// Geçersiz öğeler (kategorisi metin olmayan) boş kategoriyle iletilir; yerel SDK doğrulaması reddeder.
    private static func preferenceInputs(_ json: String) -> [HSPreferenceInput] {
        ((jsonObject(json) as? [Any]) ?? []).map { element in
            let o = element as? [String: Any] ?? [:]
            return HSPreferenceInput(category: o["category"] as? String ?? "", channel: o["channel"] as? String, optedIn: o["optedIn"] as? Bool ?? false)
        }
    }

    private static func preferenceJson(_ p: HSPreference) -> [String: Any] {
        [
            "category": p.category,
            "channel": p.channel ?? NSNull(),
            "optedIn": p.optedIn,
            "explicit": p.explicit,
            "source": p.source ?? NSNull(),
            "updatedAt": millis(p.updatedAt),
        ]
    }

    private static func categoryJson(_ c: HSMessageCategory) -> [String: Any] {
        [
            "key": c.key,
            "name": c.name,
            "description": c.description,
            "channels": c.channels ?? NSNull(),
            "defaultOptedIn": c.defaultOptedIn,
        ]
    }

    private static func preferencesResultJson(_ r: HSPreferencesResult) -> [String: Any] {
        ["preferences": r.preferences.map { preferenceJson($0) }, "categories": r.categories.map { categoryJson($0) }]
    }

    private static func pageJson(_ page: HSInboxPage) -> [String: Any] {
        [
            "items": page.items.map { itemJson($0) },
            "unreadCount": page.unreadCount,
            "nextCursor": page.nextCursor ?? NSNull(),
            "hasMore": page.hasMore,
        ]
    }
}

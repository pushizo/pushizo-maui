package com.herosignal.maui

import android.Manifest
import android.app.Activity
import android.app.Application
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import androidx.core.app.ActivityCompat
import com.herosignal.sdk.HeroSignal
import com.herosignal.sdk.HsApiException
import com.herosignal.sdk.HsCallback
import com.herosignal.sdk.HsClickEvent
import com.herosignal.sdk.HsForegroundEvent
import com.herosignal.sdk.HsInAppClickEvent
import com.herosignal.sdk.HsInAppLifecycleEvent
import com.herosignal.sdk.HsInAppLifecycleListener
import com.herosignal.sdk.HsInboxItem
import com.herosignal.sdk.HsInboxPage
import com.herosignal.sdk.HsLog
import com.herosignal.sdk.HsNotification
import com.herosignal.sdk.HsPushSubscriptionState
import org.json.JSONArray
import org.json.JSONObject
import java.lang.ref.WeakReference
import java.util.UUID

/** Yerel olay dinleyicisi (C# tarafı uygular). Olaylar yerel SDK'nın iş parçacığında eşzamanlı iletilir. */
interface HeroSignalMauiListener {
    fun onEvent(type: String, json: String)
}

/** Eşzamansız işlem sonucu: başarıda `json`, hatada `errorCode` + `errorMessage` (arka plan iş parçacığında). */
interface HeroSignalMauiResult {
    fun onResult(json: String?, errorCode: String?, errorMessage: String?)
}

/** Bildirim izni isteğinin sonucu (güncel izin durumu). */
interface HeroSignalMauiPermissionCallback {
    fun onResult(granted: Boolean)
}

/**
 * .NET MAUI → Android SDK köprüsü. Java/.NET binding'inin kolayca bağlayabileceği yüzey: yalnızca metin (JSON), bool,
 * int, double ve arayüz geri çağrıları. JSON biçimleri `HeroSignal.Maui` içindeki `BridgeJson`/`BridgeEvents` ile
 * aynıdır; iOS köprüsü (`HSMauiBridge`) de aynı biçimleri üretir. Getter'lar bilerek `get` öneki taşımaz (binding
 * üreticisi bunları özelliğe çevirmesin diye).
 */
object HeroSignalMauiBridge {
    private const val MAX_PENDING_FOREGROUND = 32

    @Volatile private var listener: HeroSignalMauiListener? = null
    @Volatile private var appContext: Context? = null
    @Volatile private var listenersRegistered = false
    @Volatile private var lastPermission: Boolean? = null

    /** Ön planda bekleyen bildirim kararları (`foreground` olayının `eventId`'si → olay). */
    private val foregroundEvents = object : LinkedHashMap<String, HsForegroundEvent>() {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<String, HsForegroundEvent>?): Boolean = size > MAX_PENDING_FOREGROUND
    }

    private val permissionLock = Any()
    private val permissionCallbacks = ArrayList<HeroSignalMauiPermissionCallback>()
    private var permissionRequestPending = false
    private var permissionFallbackToSettings = false
    private var permissionActivity: WeakReference<Activity>? = null

    private val clickListener: (HsClickEvent) -> Unit = { e ->
        emit("click", JSONObject().put("notification", notificationJson(e.notification)).put("actionId", e.actionId ?: JSONObject.NULL).put("url", e.url ?: JSONObject.NULL))
    }

    private val foregroundListener: (HsForegroundEvent) -> Unit = { e ->
        val eventId = UUID.randomUUID().toString()
        synchronized(foregroundEvents) { foregroundEvents[eventId] = e }
        // C# işleyicileri bu çağrı içinde eşzamanlı çalışır; preventDefault kararı SDK gösterimden önce okunur.
        emit("foreground", JSONObject().put("eventId", eventId).put("notification", notificationJson(e.notification)))
        // Engellenmediyse SDK bildirimi kendisi gösterir; karar kaydı tutulmaz.
        if (!e.prevented) synchronized(foregroundEvents) { foregroundEvents.remove(eventId) }
    }

    private val subscriptionObserver: (HsPushSubscriptionState) -> Unit = { s ->
        lastPermission = s.permission
        emit("subscription", subscriptionJson(s))
    }

    private val inAppClickListener: (HsInAppClickEvent) -> Unit = { e ->
        emit("inAppClick", JSONObject().put("messageId", e.messageId).put("actionId", e.actionId).put("url", e.url ?: JSONObject.NULL))
    }

    private val inAppLifecycleListener = object : HsInAppLifecycleListener {
        private fun send(event: String, e: HsInAppLifecycleEvent) =
            emit("inAppLifecycle", JSONObject().put("event", event).put("messageId", e.message.id).put("json", e.message.toJson()))

        override fun onWillDisplay(event: HsInAppLifecycleEvent) = send("willDisplay", event)
        override fun onDidDisplay(event: HsInAppLifecycleEvent) = send("didDisplay", event)
        override fun onWillDismiss(event: HsInAppLifecycleEvent) = send("willDismiss", event)
        override fun onDidDismiss(event: HsInAppLifecycleEvent) = send("didDismiss", event)
    }

    /** Ön plana dönüşte izin isteği sonucunu ve ayarlardan yapılan izin değişikliklerini yakalar. */
    private val activityCallbacks = object : Application.ActivityLifecycleCallbacks {
        override fun onActivityResumed(activity: Activity) {
            resolvePermissionRequest()
            val now = HeroSignal.Notifications.permission
            val previous = lastPermission
            lastPermission = now
            // Kullanıcı izni sistem ayarlarından değiştirdiyse abonelik güncellenir ve gözlemciler bilgilendirilir.
            if (previous != null && previous != now) HeroSignal.Notifications.onPermissionResult()
        }

        override fun onActivityCreated(activity: Activity, savedInstanceState: Bundle?) {}
        override fun onActivityStarted(activity: Activity) {}
        override fun onActivityPaused(activity: Activity) {}
        override fun onActivityStopped(activity: Activity) {}
        override fun onActivitySaveInstanceState(activity: Activity, outState: Bundle) {}
        override fun onActivityDestroyed(activity: Activity) {}
    }

    /**
     * Yerel SDK'yı başlatır ve olay dinleyicilerini (bir kez) bağlar. `optionsJson`: `apiUrl`, `requiresPrivacyConsent`,
     * `logLevel` (0-4), `androidDefaultChannelName`, `androidDefaultChannelId`.
     */
    @JvmStatic
    fun start(context: Context, appId: String, optionsJson: String, listener: HeroSignalMauiListener) {
        val o = JSONObject(optionsJson)
        val ctx = context.applicationContext
        this.listener = listener
        appContext = ctx
        val defaults = HeroSignal.Options()
        HeroSignal.initialize(
            ctx,
            appId,
            HeroSignal.Options(
                apiUrl = o.optString("apiUrl").ifBlank { defaults.apiUrl },
                requiresPrivacyConsent = o.optBoolean("requiresPrivacyConsent", false),
                logLevel = logLevelOf(o.optInt("logLevel", HsLog.Level.WARN.priority)),
                defaultChannelName = o.optString("androidDefaultChannelName").ifBlank { defaults.defaultChannelName },
                defaultChannelId = o.optString("androidDefaultChannelId").ifBlank { defaults.defaultChannelId },
            ),
        )
        if (!listenersRegistered) {
            listenersRegistered = true
            (ctx as? Application)?.registerActivityLifecycleCallbacks(activityCallbacks)
            HeroSignal.User.pushSubscription.addObserver(subscriptionObserver)
            HeroSignal.Notifications.addForegroundLifecycleListener(foregroundListener)
            HeroSignal.InAppMessages.addClickListener(inAppClickListener)
            HeroSignal.InAppMessages.addLifecycleListener(inAppLifecycleListener)
            // En son: bekleyen (uygulamayı açan) tıklama bu çağrıda iletilir.
            HeroSignal.Notifications.addClickListener(clickListener)
        }
        lastPermission = HeroSignal.Notifications.permission
    }

    @JvmStatic fun setConsentGiven(given: Boolean) = HeroSignal.setConsentGiven(given)

    @JvmStatic fun login(externalId: String, identityHash: String?) = HeroSignal.login(externalId, identityHash)

    @JvmStatic fun logout() = HeroSignal.logout()

    /** 0 none, 1 error, 2 warn, 3 info, 4 debug. */
    @JvmStatic fun applyLogLevel(level: Int) = HeroSignal.Debug.setLogLevel(logLevelOf(level))

    @JvmStatic fun externalId(): String? = HeroSignal.User.externalId

    @JvmStatic fun userId(): String? = HeroSignal.User.id

    @JvmStatic fun addTags(tagsJson: String) = HeroSignal.User.addTags(stringMap(JSONObject(tagsJson)))

    @JvmStatic fun removeTags(keysJson: String) = HeroSignal.User.removeTags(stringList(JSONArray(keysJson)))

    @JvmStatic fun tags(): String = JSONObject(HeroSignal.User.getTags()).toString()

    @JvmStatic fun addAliases(aliasesJson: String) = HeroSignal.User.addAliases(stringMap(JSONObject(aliasesJson)))

    @JvmStatic fun removeAlias(label: String) = HeroSignal.User.removeAlias(label)

    @JvmStatic fun setLanguage(languageCode: String) = HeroSignal.User.setLanguage(languageCode)

    @JvmStatic fun addEmail(email: String) = HeroSignal.User.addEmail(email)

    @JvmStatic fun removeEmail(email: String) = HeroSignal.User.removeEmail(email)

    @JvmStatic fun addSms(phoneNumber: String) = HeroSignal.User.addSms(phoneNumber)

    @JvmStatic fun removeSms(phoneNumber: String) = HeroSignal.User.removeSms(phoneNumber)

    @JvmStatic fun emails(): String = JSONArray(HeroSignal.User.emails).toString()

    @JvmStatic fun smsNumbers(): String = JSONArray(HeroSignal.User.smsNumbers).toString()

    @JvmStatic fun pushSubscription(): String = subscriptionJson(HeroSignal.User.pushSubscription.state).toString()

    @JvmStatic fun optIn() = HeroSignal.User.pushSubscription.optIn()

    @JvmStatic fun optOut() = HeroSignal.User.pushSubscription.optOut()

    @JvmStatic fun permission(): Boolean = HeroSignal.Notifications.permission

    /**
     * Android 13+ bildirim iznini ister. Sonuç `onRequestPermissionsResult` (MAUI yaşam döngüsü) ya da etkinliğin ön
     * plana dönüşüyle bildirilir. `fallbackToSettings`: izin kalıcı olarak reddedilmişse uygulama bildirim ayarları açılır.
     */
    @JvmStatic
    fun requestPermission(activity: Activity?, fallbackToSettings: Boolean, callback: HeroSignalMauiPermissionCallback) {
        if (HeroSignal.Notifications.permission) {
            callback.onResult(true)
            return
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU || activity == null) {
            // Android 12 ve öncesinde çalışma zamanı izni yoktur; bildirimler kapalıysa yalnızca ayarlardan açılabilir.
            if (fallbackToSettings) openNotificationSettings(activity ?: appContext)
            callback.onResult(HeroSignal.Notifications.permission)
            return
        }
        synchronized(permissionLock) {
            permissionCallbacks.add(callback)
            permissionFallbackToSettings = permissionFallbackToSettings || fallbackToSettings
            if (permissionRequestPending) return
            permissionRequestPending = true
            permissionActivity = WeakReference(activity)
        }
        HeroSignal.Notifications.requestPermission(activity)
    }

    /** MAUI `OnRequestPermissionsResult` yaşam döngüsü olayından çağrılır. */
    @JvmStatic
    fun onRequestPermissionsResult(requestCode: Int) {
        if (requestCode != HeroSignal.Notifications.PERMISSION_REQUEST_CODE) return
        resolvePermissionRequest()
    }

    private fun resolvePermissionRequest() {
        val callbacks: List<HeroSignalMauiPermissionCallback>
        val fallback: Boolean
        val activity: Activity?
        synchronized(permissionLock) {
            if (!permissionRequestPending) return
            permissionRequestPending = false
            callbacks = permissionCallbacks.toList()
            permissionCallbacks.clear()
            fallback = permissionFallbackToSettings
            permissionFallbackToSettings = false
            activity = permissionActivity?.get()
            permissionActivity = null
        }
        HeroSignal.Notifications.onPermissionResult()
        val granted = HeroSignal.Notifications.permission
        lastPermission = granted
        // Gerekçe gösterilemiyorsa izin kalıcı olarak reddedilmiştir (sistem iletişim kutusu artık açılmaz).
        if (!granted && fallback && activity != null &&
            !ActivityCompat.shouldShowRequestPermissionRationale(activity, Manifest.permission.POST_NOTIFICATIONS)
        ) {
            openNotificationSettings(activity)
        }
        callbacks.forEach { runCatching { it.onResult(granted) } }
    }

    private fun openNotificationSettings(context: Context?) {
        val ctx = context ?: return
        val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, ctx.packageName)
        } else {
            Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.fromParts("package", ctx.packageName, null))
        }
        if (ctx !is Activity) intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        runCatching { ctx.startActivity(intent) }.onFailure { HsLog.w("bildirim ayarları açılamadı", it) }
    }

    /** Tepsideki HeroSignal bildirimlerini (ve grup özetlerini) kaldırır; uygulamanın diğer bildirimlerine dokunmaz. */
    @JvmStatic
    fun clearAllNotifications() {
        HeroSignal.Notifications.clearAll()
    }

    @JvmStatic fun addTriggers(triggersJson: String) = HeroSignal.InAppMessages.addTriggers(stringMap(JSONObject(triggersJson)))

    @JvmStatic fun removeTriggers(keysJson: String) = HeroSignal.InAppMessages.removeTriggers(stringList(JSONArray(keysJson)))

    @JvmStatic fun clearTriggers() = HeroSignal.InAppMessages.clearTriggers()

    @JvmStatic fun triggers(): String = JSONObject(HeroSignal.InAppMessages.getTriggers()).toString()

    @JvmStatic fun setScreen(name: String?) = HeroSignal.InAppMessages.setScreen(name)

    @JvmStatic fun inAppPaused(): Boolean = HeroSignal.InAppMessages.paused

    @JvmStatic fun updateInAppPaused(paused: Boolean) {
        HeroSignal.InAppMessages.paused = paused
    }

    @JvmStatic
    fun inboxList(limit: Int, cursor: String?, result: HeroSignalMauiResult) =
        HeroSignal.Inbox.list(limit, cursor, reply<HsInboxPage>(result) { pageJson(it).toString() })

    @JvmStatic
    fun inboxMarkRead(id: String, result: HeroSignalMauiResult) =
        HeroSignal.Inbox.markRead(id, reply<HsInboxItem>(result) { itemJson(it).toString() })

    @JvmStatic
    fun inboxMarkAllRead(result: HeroSignalMauiResult) =
        HeroSignal.Inbox.markAllRead(reply<Int>(result) { JSONObject().put("unreadCount", it).toString() })

    @JvmStatic
    fun inboxArchive(id: String, result: HeroSignalMauiResult) =
        HeroSignal.Inbox.archive(id, reply<Unit>(result) { null })

    @JvmStatic
    fun inboxUnreadCount(result: HeroSignalMauiResult) =
        HeroSignal.Inbox.unreadCount(reply<Int>(result) { JSONObject().put("unreadCount", it).toString() })

    /** `propertiesJson` JSON nesnesi (iç içe nesne/dizi desteklenir); `hasValue` false ise değer gönderilmez. */
    @JvmStatic
    fun sendEvent(name: String, propertiesJson: String, value: Double, hasValue: Boolean) =
        HeroSignal.Session.sendEvent(name, toMap(JSONObject(propertiesJson)), if (hasValue) value else null)

    @JvmStatic
    fun foregroundPreventDefault(eventId: String) {
        synchronized(foregroundEvents) { foregroundEvents[eventId] }?.preventDefault()
    }

    @JvmStatic
    fun foregroundDisplay(eventId: String) {
        synchronized(foregroundEvents) { foregroundEvents.remove(eventId) }?.display()
    }

    // --- Yardımcılar ---

    private fun emit(type: String, payload: JSONObject) {
        val l = listener ?: return
        runCatching { l.onEvent(type, payload.toString()) }.onFailure { HsLog.e("MAUI olay dinleyicisi hata verdi ($type)", it) }
    }

    private fun <T> reply(result: HeroSignalMauiResult, map: (T) -> String?) = HsCallback<T> { r ->
        r.fold(
            onSuccess = { value ->
                val json = runCatching { map(value) }
                json.fold(
                    onSuccess = { result.onResult(it, null, null) },
                    onFailure = { result.onResult(null, "INVALID_RESPONSE", it.message) },
                )
            },
            onFailure = { e -> result.onResult(null, errorCode(e), e.message ?: errorCode(e)) },
        )
    }

    private fun errorCode(e: Throwable): String = when (e) {
        is HsApiException -> e.code
        is IllegalStateException -> "NOT_READY"
        is IllegalArgumentException -> "INVALID_ARGUMENT"
        else -> "HS_ERROR"
    }

    private fun logLevelOf(level: Int): HsLog.Level =
        HsLog.Level.values().firstOrNull { it.priority == level } ?: HsLog.Level.WARN

    private fun stringMap(o: JSONObject): Map<String, String> =
        o.keys().asSequence().associateWith { o.getString(it) }

    private fun stringList(a: JSONArray): List<String> = (0 until a.length()).map { a.getString(it) }

    private fun toMap(o: JSONObject): Map<String, Any?> = o.keys().asSequence().associateWith { toValue(o.get(it)) }

    private fun toValue(v: Any?): Any? = when (v) {
        null, JSONObject.NULL -> null
        is JSONObject -> toMap(v)
        is JSONArray -> (0 until v.length()).map { toValue(v.get(it)) }
        else -> v
    }

    private fun nullable(v: Any?): Any = v ?: JSONObject.NULL

    private fun notificationJson(n: HsNotification): JSONObject {
        val actions = JSONArray()
        n.actions.forEach { a ->
            actions.put(JSONObject().put("id", a.id).put("title", a.title).put("url", nullable(a.url)).put("icon", nullable(a.icon)))
        }
        return JSONObject()
            .put("title", nullable(n.title))
            .put("body", nullable(n.body))
            .put("imageUrl", nullable(n.imageUrl))
            .put("url", nullable(n.url))
            .put("deliveryId", nullable(n.deliveryId))
            .put("messageId", nullable(n.messageId))
            .put("actions", actions)
            .put("data", JSONObject(n.data))
    }

    private fun subscriptionJson(s: HsPushSubscriptionState): JSONObject = JSONObject()
        .put("id", nullable(s.id))
        .put("token", nullable(s.token))
        .put("optedIn", s.optedIn)
        .put("permission", s.permission)

    private fun itemJson(i: HsInboxItem): JSONObject = JSONObject()
        .put("id", i.id)
        .put("messageId", nullable(i.messageId))
        .put("title", nullable(i.title))
        .put("body", nullable(i.body))
        .put("url", nullable(i.url))
        .put("imageUrl", nullable(i.imageUrl))
        .put("data", JSONObject(i.data))
        .put("read", i.read)
        .put("readAt", nullable(i.readAt))
        .put("archivedAt", nullable(i.archivedAt))
        .put("expiresAt", nullable(i.expiresAt))
        .put("createdAt", nullable(i.createdAt))

    private fun pageJson(p: HsInboxPage): JSONObject {
        val items = JSONArray()
        p.items.forEach { items.put(itemJson(it)) }
        return JSONObject()
            .put("items", items)
            .put("unreadCount", p.unreadCount)
            .put("nextCursor", nullable(p.nextCursor))
            .put("hasMore", p.hasMore)
    }
}

package com.pushizo.maui

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.app.Application
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import androidx.core.app.ActivityCompat
import com.pushizo.sdk.Pushizo
import com.pushizo.sdk.HsApiException
import com.pushizo.sdk.HsCallback
import com.pushizo.sdk.HsClickEvent
import com.pushizo.sdk.HsForegroundEvent
import com.pushizo.sdk.HsInAppClickEvent
import com.pushizo.sdk.HsInAppLifecycleEvent
import com.pushizo.sdk.HsInAppLifecycleListener
import com.pushizo.sdk.HsInboxItem
import com.pushizo.sdk.HsInboxPage
import com.pushizo.sdk.HsLog
import com.pushizo.sdk.HsMessageCategory
import com.pushizo.sdk.HsNotification
import com.pushizo.sdk.HsPreference
import com.pushizo.sdk.HsPreferenceInput
import com.pushizo.sdk.HsPreferencesResult
import com.pushizo.sdk.HsPushSubscriptionState
import com.pushizo.sdk.R as SdkR
import org.json.JSONArray
import org.json.JSONObject
import java.lang.ref.WeakReference
import java.util.UUID

/** Yerel olay dinleyicisi (C# tarafı uygular). Olaylar yerel SDK'nın iş parçacığında eşzamanlı iletilir. */
interface PushizoMauiListener {
    fun onEvent(type: String, json: String)
}

/** Eşzamansız işlem sonucu: başarıda `json`, hatada `errorCode` + `errorMessage` (arka plan iş parçacığında). */
interface PushizoMauiResult {
    fun onResult(json: String?, errorCode: String?, errorMessage: String?)
}

/** Bildirim veya konum izni isteğinin sonucu (güncel izin durumu). */
interface PushizoMauiPermissionCallback {
    fun onResult(granted: Boolean)
}

/**
 * .NET MAUI → Android SDK köprüsü. Java/.NET binding'inin kolayca bağlayabileceği yüzey: yalnızca metin (JSON), bool,
 * int, double ve arayüz geri çağrıları. JSON biçimleri `Pushizo.Maui` içindeki `BridgeJson`/`BridgeEvents` ile
 * aynıdır; iOS köprüsü (`HSMauiBridge`) de aynı biçimleri üretir. Getter'lar bilerek `get` öneki taşımaz (binding
 * üreticisi bunları özelliğe çevirmesin diye).
 */
object PushizoMauiBridge {
    private const val MAX_PENDING_FOREGROUND = 32

    @Volatile private var listener: PushizoMauiListener? = null
    @Volatile private var appContext: Context? = null
    @Volatile private var listenersRegistered = false

    /** Ön planda bekleyen bildirim kararları (`foreground` olayının `eventId`'si → olay). */
    private val foregroundEvents = object : LinkedHashMap<String, HsForegroundEvent>() {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<String, HsForegroundEvent>?): Boolean = size > MAX_PENDING_FOREGROUND
    }

    private val permissionLock = Any()
    private val permissionCallbacks = ArrayList<PushizoMauiPermissionCallback>()
    private var permissionRequestPending = false
    private var permissionFallbackToSettings = false
    private var permissionActivity: WeakReference<Activity>? = null

    private val locationPermissions = listOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)
    private val locationLock = Any()
    private val locationCallbacks = ArrayList<PushizoMauiPermissionCallback>()

    /** Açıklama diyaloğu veya sistem istemi açık (yeni istekler aynı sonucu bekler). */
    private var locationRequestActive = false

    /** Sistem konum izni istendi; sonuç `onRequestPermissionsResult` ya da ön plana dönüşle bekleniyor. */
    private var locationAwaitingResult = false

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

    private val subscriptionObserver: (HsPushSubscriptionState) -> Unit = { s -> emit("subscription", subscriptionJson(s)) }

    /**
     * Yerel SDK izin gözlemcisi → `permission` olayı (`{"permission":bool}`). İzin isteği sonucu ve uygulama ön plana
     * döndüğünde (izin sistem ayarlarından değişmiş olabilir) etkin izin değiştiyse çağrılır.
     */
    private val permissionObserver: (Boolean) -> Unit = { granted -> emit("permission", JSONObject().put("permission", granted)) }

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

    /**
     * Ön plana dönüşte bekleyen izin isteğini sonuçlandırır (sonuç `onRequestPermissionsResult` ile gelmediyse).
     * Ayarlardan yapılan izin değişikliklerini yerel SDK izin gözlemcisi yakalar.
     */
    private val activityCallbacks = object : Application.ActivityLifecycleCallbacks {
        override fun onActivityResumed(activity: Activity) {
            resolvePermissionRequest()
            resolveLocationRequest()
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
    fun start(context: Context, appId: String, optionsJson: String, listener: PushizoMauiListener) {
        val o = JSONObject(optionsJson)
        val ctx = context.applicationContext
        this.listener = listener
        appContext = ctx
        val defaults = Pushizo.Options()
        Pushizo.initialize(
            ctx,
            appId,
            Pushizo.Options(
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
            Pushizo.User.pushSubscription.addObserver(subscriptionObserver)
            Pushizo.Notifications.addPermissionObserver(permissionObserver)
            Pushizo.Notifications.addForegroundLifecycleListener(foregroundListener)
            Pushizo.InAppMessages.addClickListener(inAppClickListener)
            Pushizo.InAppMessages.addLifecycleListener(inAppLifecycleListener)
            // En son: bekleyen (uygulamayı açan) tıklama bu çağrıda iletilir.
            Pushizo.Notifications.addClickListener(clickListener)
        }
    }

    @JvmStatic fun setConsentGiven(given: Boolean) = Pushizo.setConsentGiven(given)

    /** Kanıt app'in kimlik doğrulama kipine uymuyorsa hata mesajı döner (giriş yapılmaz); başarıda null. */
    @JvmStatic fun login(externalId: String, identityHash: String?, jwt: String?): String? = identityError {
        Pushizo.login(externalId, identityHash, jwt)
    }

    /** Kimlik JWT'sini yeniler; giriş yapılmamışsa hata mesajı döner, başarıda null. */
    @JvmStatic fun updateUserJwt(jwt: String): String? = identityError { Pushizo.updateUserJwt(jwt) }

    /** Yerel SDK'nın kimlik doğrulama hatalarını .NET tarafına mesaj olarak taşır (Java istisnası köprüden sızmaz). */
    private inline fun identityError(block: () -> Unit): String? = try {
        block()
        null
    } catch (e: IllegalStateException) {
        e.message ?: "kimlik doğrulama hatası"
    } catch (e: IllegalArgumentException) {
        e.message ?: "geçersiz kimlik kanıtı"
    }

    @JvmStatic fun logout() = Pushizo.logout()

    /** 0 none, 1 error, 2 warn, 3 info, 4 debug. */
    @JvmStatic fun applyLogLevel(level: Int) = Pushizo.Debug.setLogLevel(logLevelOf(level))

    @JvmStatic fun externalId(): String? = Pushizo.User.externalId

    @JvmStatic fun userId(): String? = Pushizo.User.id

    @JvmStatic fun addTags(tagsJson: String) = Pushizo.User.addTags(stringMap(JSONObject(tagsJson)))

    @JvmStatic fun removeTags(keysJson: String) = Pushizo.User.removeTags(stringList(JSONArray(keysJson)))

    @JvmStatic fun tags(): String = JSONObject(Pushizo.User.getTags()).toString()

    @JvmStatic fun addAliases(aliasesJson: String) = Pushizo.User.addAliases(stringMap(JSONObject(aliasesJson)))

    @JvmStatic fun removeAlias(label: String) = Pushizo.User.removeAlias(label)

    @JvmStatic fun setLanguage(languageCode: String) = Pushizo.User.setLanguage(languageCode)

    @JvmStatic fun addEmail(email: String) = Pushizo.User.addEmail(email)

    @JvmStatic fun removeEmail(email: String) = Pushizo.User.removeEmail(email)

    @JvmStatic fun addSms(phoneNumber: String) = Pushizo.User.addSms(phoneNumber)

    @JvmStatic fun removeSms(phoneNumber: String) = Pushizo.User.removeSms(phoneNumber)

    @JvmStatic fun emails(): String = JSONArray(Pushizo.User.emails).toString()

    @JvmStatic fun smsNumbers(): String = JSONArray(Pushizo.User.smsNumbers).toString()

    @JvmStatic fun pushSubscription(): String = subscriptionJson(Pushizo.User.pushSubscription.state).toString()

    @JvmStatic fun optIn() = Pushizo.User.pushSubscription.optIn()

    @JvmStatic fun optOut() = Pushizo.User.pushSubscription.optOut()

    @JvmStatic fun permission(): Boolean = Pushizo.Notifications.permission

    /**
     * Android 13+ bildirim iznini ister. Sonuç `onRequestPermissionsResult` (MAUI yaşam döngüsü) ya da etkinliğin ön
     * plana dönüşüyle bildirilir. `fallbackToSettings`: izin kalıcı olarak reddedilmişse uygulama bildirim ayarları açılır.
     */
    @JvmStatic
    fun requestPermission(activity: Activity?, fallbackToSettings: Boolean, callback: PushizoMauiPermissionCallback) {
        if (Pushizo.Notifications.permission) {
            callback.onResult(true)
            return
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU || activity == null) {
            // Android 12 ve öncesinde çalışma zamanı izni yoktur; bildirimler kapalıysa yalnızca ayarlardan açılabilir.
            if (fallbackToSettings) openNotificationSettings(activity ?: appContext)
            callback.onResult(Pushizo.Notifications.permission)
            return
        }
        synchronized(permissionLock) {
            permissionCallbacks.add(callback)
            permissionFallbackToSettings = permissionFallbackToSettings || fallbackToSettings
            if (permissionRequestPending) return
            permissionRequestPending = true
            permissionActivity = WeakReference(activity)
        }
        Pushizo.Notifications.requestPermission(activity)
    }

    /** MAUI `OnRequestPermissionsResult` yaşam döngüsü olayından çağrılır (bildirim ve konum istek kodları). */
    @JvmStatic
    fun onRequestPermissionsResult(requestCode: Int) {
        when (requestCode) {
            Pushizo.Notifications.PERMISSION_REQUEST_CODE -> resolvePermissionRequest()
            Pushizo.Location.PERMISSION_REQUEST_CODE -> resolveLocationRequest()
        }
    }

    private fun resolvePermissionRequest() {
        val callbacks: List<PushizoMauiPermissionCallback>
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
        // İzin değiştiyse yerel SDK izin gözlemcisi `permission` olayını gönderir.
        Pushizo.Notifications.onPermissionResult()
        val granted = Pushizo.Notifications.permission
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

    /** Tepsideki Pushizo bildirimlerini (ve grup özetlerini) kaldırır; uygulamanın diğer bildirimlerine dokunmaz. */
    @JvmStatic
    fun clearAllNotifications() {
        Pushizo.Notifications.clearAll()
    }

    /** Tek bir Pushizo bildirimini tepsi kimliğiyle (`androidNotificationId`) kaldırır. */
    @JvmStatic
    fun removeNotification(androidNotificationId: Int) {
        Pushizo.Notifications.removeNotification(androidNotificationId)
    }

    /** Aynı grup anahtarına (`android_group`) sahip bildirimleri ve grup özetini kaldırır. */
    @JvmStatic
    fun removeGroupedNotifications(group: String) {
        Pushizo.Notifications.removeGroupedNotifications(group)
    }

    @JvmStatic fun addTriggers(triggersJson: String) = Pushizo.InAppMessages.addTriggers(stringMap(JSONObject(triggersJson)))

    @JvmStatic fun removeTriggers(keysJson: String) = Pushizo.InAppMessages.removeTriggers(stringList(JSONArray(keysJson)))

    @JvmStatic fun clearTriggers() = Pushizo.InAppMessages.clearTriggers()

    @JvmStatic fun triggers(): String = JSONObject(Pushizo.InAppMessages.getTriggers()).toString()

    @JvmStatic fun setScreen(name: String?) = Pushizo.InAppMessages.setScreen(name)

    @JvmStatic fun inAppPaused(): Boolean = Pushizo.InAppMessages.paused

    @JvmStatic fun updateInAppPaused(paused: Boolean) {
        Pushizo.InAppMessages.paused = paused
    }

    @JvmStatic
    fun inboxList(limit: Int, cursor: String?, result: PushizoMauiResult) =
        Pushizo.Inbox.list(limit, cursor, reply<HsInboxPage>(result) { pageJson(it).toString() })

    @JvmStatic
    fun inboxMarkRead(id: String, result: PushizoMauiResult) =
        Pushizo.Inbox.markRead(id, reply<HsInboxItem>(result) { itemJson(it).toString() })

    @JvmStatic
    fun inboxMarkAllRead(result: PushizoMauiResult) =
        Pushizo.Inbox.markAllRead(reply<Int>(result) { JSONObject().put("unreadCount", it).toString() })

    @JvmStatic
    fun inboxArchive(id: String, result: PushizoMauiResult) =
        Pushizo.Inbox.archive(id, reply<Unit>(result) { null })

    @JvmStatic
    fun inboxUnreadCount(result: PushizoMauiResult) =
        Pushizo.Inbox.unreadCount(reply<Int>(result) { JSONObject().put("unreadCount", it).toString() })

    // --- Tercihler (ADR-0019 §4) ---

    /** Başarı JSON'u: `{"preferences":[…],"categories":[…]}` (bkz. `preferencesResultJson`). */
    @JvmStatic
    fun getPreferences(result: PushizoMauiResult) =
        Pushizo.User.getPreferences(reply<HsPreferencesResult>(result) { preferencesResultJson(it).toString() })

    /**
     * `preferencesJson`: `[{"category","channel":null|"…","optedIn"}]`; doğrulama yerel SDK'dadır (geçersizse
     * `INVALID_ARGUMENT`). Başarı JSON'u `getPreferences` ile aynıdır (güncel liste).
     */
    @JvmStatic
    fun setPreferences(preferencesJson: String, result: PushizoMauiResult) {
        val inputs = runCatching { preferenceInputs(JSONArray(preferencesJson)) }.getOrElse {
            result.onResult(null, "INVALID_ARGUMENT", it.message ?: "Geçersiz tercih listesi")
            return
        }
        Pushizo.User.setPreferences(inputs, reply<HsPreferencesResult>(result) { preferencesResultJson(it).toString() })
    }

    /** `propertiesJson` JSON nesnesi (iç içe nesne/dizi desteklenir); `hasValue` false ise değer gönderilmez. */
    @JvmStatic
    fun sendEvent(name: String, propertiesJson: String, value: Double, hasValue: Boolean) =
        Pushizo.Session.sendEvent(name, toMap(JSONObject(propertiesJson)), if (hasValue) value else null)

    // --- Konum (v2 Faz 17b; yalnızca ön plan, gönderim yerel SDK'da) ---

    /** Paylaşım açık mı (kalıcı tercih; izin durumundan bağımsız). */
    @JvmStatic fun locationShared(): Boolean = Pushizo.Location.isShared

    /** Paylaşımı açar/kapatır; kapatınca sunucuya `location: null` gönderilir (konum ve rıza kaydı silinir). */
    @JvmStatic fun updateLocationShared(shared: Boolean) = Pushizo.Location.setShared(shared)

    /**
     * Konum iznini ister ve sonucu `callback` ile bildirir (yerel SDK'nın `Location.requestPermission`'ı sonuç döndürmediği
     * için akış burada kurulur): izin varsa hemen true; manifest'te `ACCESS_COARSE_LOCATION`/`ACCESS_FINE_LOCATION` yoksa
     * veya etkin Activity yoksa false; aksi halde SDK metinleriyle açıklama diyaloğu, "Devam" ile sistem istemi
     * (`Pushizo.Location.PERMISSION_REQUEST_CODE`). Sonuç `onRequestPermissionsResult` ya da etkinliğin ön plana
     * dönüşüyle gelir; `Pushizo.Location.onPermissionResult()` çağrılır (paylaşım açıksa konum hemen gönderilir).
     */
    @JvmStatic
    fun requestLocationPermission(activity: Activity?, rationale: String?, callback: PushizoMauiPermissionCallback) {
        if (Pushizo.Location.permissionGranted) {
            Pushizo.Location.onPermissionResult()
            callback.onResult(true)
            return
        }
        val permissions = (activity ?: appContext)?.let(::declaredLocationPermissions).orEmpty()
        if (permissions.isEmpty()) {
            HsLog.w("konum izni istenemedi: uygulama manifest'inde ACCESS_COARSE_LOCATION/ACCESS_FINE_LOCATION yok")
            callback.onResult(false)
            return
        }
        if (activity == null) {
            HsLog.w("konum izni istenemedi: etkin Activity yok")
            callback.onResult(false)
            return
        }
        synchronized(locationLock) {
            locationCallbacks.add(callback)
            if (locationRequestActive) return
            locationRequestActive = true
        }
        activity.runOnUiThread { showLocationRationale(activity, rationale, permissions) }
    }

    /** Açıklama diyaloğu (UI iş parçacığında); "Şimdi değil", geri tuşu ya da dışarı dokunma false ile sonuçlanır. */
    private fun showLocationRationale(activity: Activity, rationale: String?, permissions: List<String>) {
        if (activity.isFinishing || activity.isDestroyed) {
            finishLocationRequest(false)
            return
        }
        var proceed = false
        try {
            AlertDialog.Builder(activity)
                .setTitle(SdkR.string.hs_location_rationale_title)
                .setMessage(rationale?.takeIf { it.isNotBlank() } ?: activity.getString(SdkR.string.hs_location_rationale_message))
                .setPositiveButton(SdkR.string.hs_location_rationale_continue) { _, _ -> proceed = true }
                .setNegativeButton(SdkR.string.hs_location_rationale_not_now, null)
                .setOnDismissListener {
                    if (proceed) requestSystemLocationPermission(activity, permissions) else finishLocationRequest(false)
                }
                .show()
        } catch (e: RuntimeException) {
            HsLog.w("konum izni açıklaması gösterilemedi", e)
            finishLocationRequest(false)
        }
    }

    private fun requestSystemLocationPermission(activity: Activity, permissions: List<String>) {
        if (activity.isFinishing || activity.isDestroyed) {
            finishLocationRequest(false)
            return
        }
        synchronized(locationLock) { locationAwaitingResult = true }
        ActivityCompat.requestPermissions(activity, permissions.toTypedArray(), Pushizo.Location.PERMISSION_REQUEST_CODE)
    }

    /** Sistem istemi sonucunu bekleyenlere iletir (istem açık değilse etkisiz). */
    private fun resolveLocationRequest() {
        val callbacks: List<PushizoMauiPermissionCallback>
        synchronized(locationLock) {
            if (!locationAwaitingResult) return
            callbacks = takeLocationCallbacks()
        }
        // İzin verildiyse ve paylaşım açıksa yerel SDK konumu hemen gönderir.
        Pushizo.Location.onPermissionResult()
        val granted = Pushizo.Location.permissionGranted
        callbacks.forEach { runCatching { it.onResult(granted) } }
    }

    private fun finishLocationRequest(granted: Boolean) {
        val callbacks = synchronized(locationLock) { takeLocationCallbacks() }
        callbacks.forEach { runCatching { it.onResult(granted) } }
    }

    /** `locationLock` altında çağrılır: istek durumunu sıfırlar ve bekleyen geri çağrıları devralır. */
    private fun takeLocationCallbacks(): List<PushizoMauiPermissionCallback> {
        locationRequestActive = false
        locationAwaitingResult = false
        val callbacks = locationCallbacks.toList()
        locationCallbacks.clear()
        return callbacks
    }

    /** Uygulama manifest'inde tanımlı konum izinleri (yalnızca bunlar istenebilir; köprü izin eklemez). */
    @Suppress("DEPRECATION")
    private fun declaredLocationPermissions(context: Context): List<String> = runCatching {
        val info = context.packageManager.getPackageInfo(context.packageName, PackageManager.GET_PERMISSIONS)
        val requested = info.requestedPermissions?.toSet().orEmpty()
        locationPermissions.filter { it in requested }
    }.getOrDefault(emptyList())

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

    private fun <T> reply(result: PushizoMauiResult, map: (T) -> String?) = HsCallback<T> { r ->
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
            .put("androidNotificationId", n.androidNotificationId)
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

    private fun preferenceInputs(a: JSONArray): List<HsPreferenceInput> = (0 until a.length()).map { i ->
        val o = a.getJSONObject(i)
        HsPreferenceInput(
            category = o.getString("category"),
            channel = if (o.isNull("channel")) null else o.getString("channel"),
            optedIn = o.getBoolean("optedIn"),
        )
    }

    private fun preferenceJson(p: HsPreference): JSONObject = JSONObject()
        .put("category", p.category)
        .put("channel", nullable(p.channel))
        .put("optedIn", p.optedIn)
        .put("explicit", p.explicit)
        .put("source", nullable(p.source))
        .put("updatedAt", nullable(p.updatedAt))

    private fun categoryJson(c: HsMessageCategory): JSONObject = JSONObject()
        .put("key", c.key)
        .put("name", JSONObject(c.name))
        .put("description", JSONObject(c.description))
        .put("channels", c.channels?.let { JSONArray(it) } ?: JSONObject.NULL)
        .put("defaultOptedIn", c.defaultOptedIn)

    private fun preferencesResultJson(r: HsPreferencesResult): JSONObject {
        val preferences = JSONArray()
        r.preferences.forEach { preferences.put(preferenceJson(it)) }
        val categories = JSONArray()
        r.categories.forEach { categories.put(categoryJson(it)) }
        return JSONObject().put("preferences", preferences).put("categories", categories)
    }

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

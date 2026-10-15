# HeroSignal .NET MAUI SDK (`HeroSignal.Maui`)

Push bildirimleri, in-app mesajlar, bildirim kutusu, kullanıcı/tag/alias, bildirim tercihleri, konum paylaşımı ve e-posta/SMS abonelikleri için .NET MAUI
SDK'sı. Yerel HeroSignal Android (`com.herosignal:herosignal-android`) ve iOS (`HeroSignal` Swift) SDK'larını sarar;
tetik değerlendirme, çevrimdışı kuyruk, teslimat geri bildirimi ve in-app çizimi yerel SDK'dadır. API yüzeyi
plan 06 §6.1 (eski sağlayıcı v5 ile uyumlu) ve Flutter eklentisiyle aynıdır.

| Hedef | Davranış |
|-------|----------|
| `net8.0-android`, `net9.0-android` | Yerel Android SDK'sı (minSdk 23) |
| `net8.0-ios`, `net9.0-ios` | Yerel iOS SDK'sı (iOS 15+) |
| Diğerleri (Windows, MacCatalyst) | Çağrılar etkisizdir, bir kez uyarı yazılır; bildirim kutusu ve tercihler `PLATFORM_NOT_SUPPORTED` döndürür |

## Kurulum

```bash
dotnet add package HeroSignal.Maui
```

```csharp
// MauiProgram.cs
using HeroSignalSdk.Maui;

var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>()
       .UseHeroSignal("app_…", o =>
       {
           o.ApiUrl = "https://api.example.com";
           o.RequiresPrivacyConsent = false; // KVKK/GDPR: true ise SetConsentGiven(true) gelene kadar ağ çağrısı yok
       });
```

### Android

- Firebase konsolundan `google-services.json` dosyasını ekleyin:
  ```xml
  <ItemGroup Condition="$(TargetFramework.Contains('-android'))">
    <GoogleServicesJson Include="Platforms/Android/google-services.json" />
  </ItemGroup>
  ```
- Android 13+ bildirim izni `HeroSignal.Notifications.RequestPermissionAsync()` ile istenir. `UseHeroSignal` izin
  sonucunu yaşam döngüsü olayından otomatik alır; yalnızca `HeroSignal.Initialize` kullanıyorsanız `MainActivity`
  içinde `HeroSignalAndroid.OnRequestPermissionsResult(requestCode)` çağırın.

### iOS

- `Platforms/iOS/Entitlements.plist` içinde `aps-environment`, `Info.plist` içinde `UIBackgroundModes` →
  `remote-notification` olmalıdır.
- Yerel SDK swizzling kullanmaz; APNs token'ını `AppDelegate`'ten iletin:
  ```csharp
  [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
  public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken) =>
      HeroSignalIos.RegisteredForRemoteNotifications(deviceToken);

  [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
  public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error) =>
      HeroSignalIos.FailedToRegisterForRemoteNotifications(error);
  ```
- Zengin medya, rozet ve teslimat onayı için Xcode'da bir Notification Service Extension hedefi ekleyip yerel
  `HeroSignalExtension` (SPM) ya da `HeroSignal/Extension` (CocoaPods) ile `HeroSignalNotificationServiceExtension`
  sınıfını kullanın (bkz. `docs/sdk/ios.md`).

## Kullanım

```csharp
await HeroSignal.Notifications.RequestPermissionAsync(fallbackToSettings: true);
HeroSignal.Login("user_42", identityHash: hashFromYourServer);   // identity verification açıksa hash zorunlu
HeroSignal.User.AddTags(new Dictionary<string, string> { ["plan"] = "pro" });
HeroSignal.User.AddAlias("crm_id", "42");
HeroSignal.User.SetLanguage("tr");
HeroSignal.User.AddEmail("ali@example.com");
HeroSignal.User.AddSms("+905321234567");

// Bildirim tercihleri (ADR-0019 §4): kategori × kanal (Channel null = tüm kanallar)
var prefs = await HeroSignal.User.GetPreferencesAsync();
foreach (var category in prefs.Categories) Console.WriteLine(category.LocalizedName("tr"));
await HeroSignal.User.SetPreferencesAsync(new[]
{
    new PreferenceInput("promo", OptedIn: false),
    new PreferenceInput("news", OptedIn: true, Channel: "email"),
});

HeroSignal.User.PushSubscription.Changed += (_, e) => Console.WriteLine(e.Current.Id);
HeroSignal.Notifications.PermissionChanged += (_, e) => Console.WriteLine(e.Permission); // yerel izin gözlemcisinden
HeroSignal.Notifications.Clicked += (_, e) => Navigate(e.Url);
HeroSignal.Notifications.ForegroundWillDisplay += (_, e) =>
{
    if (IsChatOpen) e.PreventDefault();   // eşzamanlı; gerekirse sonra e.Display()
};

HeroSignal.InAppMessages.AddTrigger("level", "gold");
HeroSignal.InAppMessages.SetScreen("cart");
HeroSignal.InAppMessages.Clicked += (_, e) => Console.WriteLine(e.ActionId);
HeroSignal.InAppMessages.DidDisplay += (_, e) => Console.WriteLine(e.Message.Id);

var page = await HeroSignal.Inbox.ListAsync(limit: 20);
await HeroSignal.Inbox.MarkReadAsync(page.Items[0].Id);
await HeroSignal.Inbox.MarkAllReadAsync();
await HeroSignal.Inbox.ArchiveAsync(page.Items[0].Id);

HeroSignal.Session.SendEvent("purchase", new Dictionary<string, object?> { ["sku"] = "A1" }, value: 129.9);
HeroSignal.Notifications.ClearAll();
HeroSignal.Notifications.RemoveGroupedNotifications("orders");  // Android android_group / iOS thread-id
HeroSignal.Notifications.RemoveNotification(42);                // Android tepsi kimliği; iOS'ta RemoveNotification("istek-kimligi")
HeroSignal.Debug.SetLogLevel(LogLevel.Debug);
HeroSignal.Logout();
```

Olaylar yerel SDK'nın iş parçacığında tetiklenir; arayüz güncellemesi için `MainThread.BeginInvokeOnMainThread`
kullanın. Bildirim kutusu ve tercih hataları `HeroSignalException.Code` ile gelir (`NOT_READY`: abonelik/kullanıcı kaydı
henüz tamamlanmadı; `INVALID_ARGUMENT`: yerel SDK girdiyi reddetti; diğerleri sunucu hata kodu). `SetPreferencesAsync`
girdiyi köprüden önce doğrular (1-100 kayıt, kategori 1-64 karakter, kanal `ios_push` | `android_push` | `web_push` |
`email` | `sms` ya da null); geçersizse `ArgumentException`.

### JWT ile kimlik doğrulama

App ayarlarında kimlik doğrulama kipi (`identity_verification_mode`) `jwt` veya `both` ise kullanıcı, sunucunuzda
imzalanmış kısa ömürlü bir ES256 JWT ile kimliklendirilir (ADR-0023 §9; talepler: `sub` = external_id, `aud` = app_id, `exp`/`iat` zorunlu, en çok 24 saat, yalnızca ES256).

```csharp
HeroSignal.Login("user_42", jwt: jwtFromYourServer);                         // yalnızca JWT
HeroSignal.Login("user_42", identityHash: hmac, jwt: jwtFromYourServer);     // both kipinde ikisi birlikte
HeroSignal.UpdateUserJwt(refreshedJwt); // süresi dolmadan yenileyin
HeroSignal.Logout();
```

- JWT yerel SDK'da identity hash ile aynı kalıcı depoda saklanır ve tüm `/sdk/v1/...` isteklerinde
  `Authorization: Bearer <jwt>` başlığıyla gider; gövdeye/URL'ye yazılmaz, loglanmaz. `Logout` belirteci siler.
- `/config` yanıtında `identity_verification_required` true ise login öncesi kanıt denetlenir: `jwt` kipinde JWT, `hmac`
  kipinde `identityHash` zorunludur, `both` kipinde biri yeter; eksikse `Login` `HeroSignalException` (`NOT_READY`)
  fırlatır. Alan gelmeyen eski sunucularda kip `hmac` sayılır; mevcut `Login(id, identityHash)` çağrıları aynen çalışır.
- `UpdateUserJwt` giriş yapılmamışsa `HeroSignalException` (`NOT_READY`), boş belirteçte `ArgumentException` fırlatır;
  başarıda kuyrukta bekleyen işlemler yeni belirteçle yeniden denenir.

### Live Activities (iOS)

Köprü, iOS SDK'nın genel türü `HeroSignalDefaultLiveActivityAttributes`'ı kullanır (ADR-0024). Dashboard/API'de Live
Activity türü adı tam olarak `HeroSignalDefaultLiveActivityAttributes` olmalıdır.

```csharp
// Initialize'dan sonra, uygulama her açıldığında: push-to-start (iOS 17.2+) ve güncelleme belirteçleri kaydedilir.
await HeroSignal.LiveActivities.SetupDefaultLiveActivityAsync();

// Yerel başlatma (iOS 16.1+); sunucu sonraki update/end çağrılarını bu kimlikle yapar.
await HeroSignal.LiveActivities.StartDefaultLiveActivityAsync(
    "order_42",
    new Dictionary<string, object> { ["title"] = "Sipariş #42" },
    new Dictionary<string, object> { ["status"] = "Hazırlanıyor", ["eta"] = 15 });
```

- Widget extension (Xcode'da Swift ile; MAUI uygulamasına `AdditionalAppExtensions` öğesiyle eklenir):
  ```swift
  import ActivityKit
  import SwiftUI
  import WidgetKit
  import HeroSignalExtension // CocoaPods: pod 'HeroSignal/Extension' + import HeroSignal

  struct OrderLiveActivity: Widget {
      var body: some WidgetConfiguration {
          ActivityConfiguration(for: HeroSignalDefaultLiveActivityAttributes.self) { context in
              VStack {
                  Text(context.attributes.data["title"]?.stringValue ?? "")
                  Text(context.state.data["status"]?.description ?? "")
              }
          } dynamicIsland: { context in
              DynamicIsland {
                  DynamicIslandExpandedRegion(.center) { Text(context.state.data["status"]?.description ?? "") }
              } compactLeading: { Text("HS") } compactTrailing: { EmptyView() } minimal: { EmptyView() }
          }
      }
  }
  ```
  SPM'de widget hedefine `HeroSignalExtension` ürünü eklenir (`import HeroSignalExtension`); CocoaPods'ta
  `HeroSignal/Extension` alt spec'i (`import HeroSignal`).
- Uygulamanın `Platforms/iOS/Info.plist` dosyasına `NSSupportsLiveActivities` = `YES` ekleyin.
- Sunucu: `POST /v1/apps/{appId}/live-activities/HeroSignalDefaultLiveActivityAttributes/start|update|end`
  (`attributes` ve `content_state` düz nesnedir; widget'ta `data` sözlüğünden okunur). Push-to-start iOS 17.2+,
  yerel başlatma iOS 16.1+ gerektirir.
- Köprü girdiyi doğrular: `activityId` 1-100 karakter (harf, rakam, `_ . : -`), anahtar 1-64 karakter, değer metin
  (en çok 4000 karakter), sonlu sayı veya bool; `attributes` içinde `herosignal` anahtarı ayrılmıştır. Geçersizse
  `HeroSignalException` (`INVALID_ARGUMENT`).
- Hatalar `HeroSignalException.Code` taşır: `PLATFORM_NOT_SUPPORTED` (iOS 16.1 öncesi, Android ve diğer hedefler),
  `NOT_READY` (yerel SDK başlatılmadı), `INVALID_ARGUMENT`, `LIVE_ACTIVITY_FAILED` (Live Activities kapalı,
  `NSSupportsLiveActivities` eksik vb.).
- Tip-güvenli kendi `ActivityAttributes` türünüz için native tarafta `HeroSignal.LiveActivities.register(MyAttributes.self)`
  çağırın (ayrıntı: `docs/sdk/ios.md` "Live Activities").

### HTML in-app ve carousel

In-app mesajları yerel Android/iOS SDK'sı çizer; bu paket köprüdür. HTML in-app (sandbox'lı WebView, köprü
`close`/`click`/`addTag`/`trackEvent`) ve carousel blokları yerel SDK'dan olduğu gibi devralınır: ek kod veya
yapılandırma gerekmez, tıklama ve yaşam döngüsü olayları mevcut in-app dinleyicilerine gelir. Yetenek bildirimi
(`supports=html,carousel`) yerel SDK tarafından yapılır; desteklemeyen eski yerel sürümlere sunucu HTML mesaj göndermez
ve carousel bloklarını çıkarır (ADR-0027 §6–7).

### Konum

Konum paylaşımı isteğe bağlıdır ve yalnızca ön planda çalışır (arka plan konumu istenmez). Paylaşım açıkken yerel SDK
konumu oturum başında ve uygulama ön plana döndüğünde en sık 5 dakikada bir push aboneliğine yazar
(`PATCH /sdk/v1/apps/{appId}/subscriptions/{id}` → `location: { lat, lng, accuracy_m }`); gönderim yerel SDK'nın kalıcı
kuyruğuyla yapılır.

```csharp
// İzin yoksa önce açıklama diyaloğu (verilmezse SDK'nın Türkçe/İngilizce varsayılan metni), sonra sistem istemi.
var granted = await HeroSignal.Location.RequestPermissionAsync("Yakındaki mağazaları göstermek için konumunuzu kullanırız.");
if (granted) HeroSignal.Location.SetShared(true);   // tercih kalıcıdır

bool shared = HeroSignal.Location.IsShared;          // paylaşım açık mı (izin durumundan bağımsız)
HeroSignal.Location.SetShared(false);               // sunucuya location: null → konum ve rıza kaydı silinir
```

- `RequestPermissionAsync` izin varsa veya verildiyse `true`; reddedildi/kısıtlı, açıklamada "Şimdi değil" seçildi ya
  da aşağıdaki yapılandırma eksikse `false` döner. İzin paylaşımı açmaz; `SetShared(true)` ayrıca çağrılır.
- iOS: `Platforms/iOS/Info.plist` dosyasına `NSLocationWhenInUseUsageDescription` ekleyin (yoksa izin istenmez,
  sonuç `false`). Yalnızca "Kullanırken" izni istenir.
- Android: köprü izni otomatik eklemez; `Platforms/Android/AndroidManifest.xml` içine en az yaklaşık konumu ekleyin:
  ```xml
  <uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
  <!-- isteğe bağlı, hassas konum için: -->
  <uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
  ```
  Manifest'te hiçbiri yoksa ya da etkin Activity yoksa sonuç `false` olur. Sistem izni yerel SDK'nın istek koduyla
  (`HeroSignal.Location.PERMISSION_REQUEST_CODE` = `0x484C`) istenir; köprü sonucu `UseHeroSignal` yaşam döngüsü olayından
  alır (yalnızca `HeroSignal.Initialize` kullanıyorsanız bildirim izniyle aynı `HeroSignalAndroid.OnRequestPermissionsResult`
  çağrısı yeterlidir).
- İzin yoksa konum sessizce gönderilmez; `RequiresPrivacyConsent` açıkken `SetConsentGiven(true)` gelene kadar hiç
  gönderilmez.
- KVKK: ilk gönderilen konum sunucuda açık rıza kaydı olarak saklanır. Kullanıcıya paylaşımın amacını açıklayın ve
  kapatma seçeneği sunun; `SetShared(false)` sunucudaki konumu ve rıza kaydını hemen sildirir.
- Windows ve MacCatalyst hedeflerinde konum çağrıları etkisizdir (bir kez uyarı yazılır): `IsShared` ve
  `RequestPermissionAsync` `false` döner, hata fırlatılmaz.

## Mimari

```
HeroSignal (C# cephe, doğrulama, olaylar)  ──  BridgePlatform (JSON sözleşmesi, ortak)
        │                                          │
        ├─ Android: HeroSignal.Maui.Android.Binding → com.herosignal.maui.HeroSignalMauiBridge (Kotlin, native/android/bridge)
        │                                             → com.herosignal.sdk.HeroSignal (sdk/android)
        └─ iOS:     HeroSignal.Maui.iOS.Binding     → HSMauiBridge (@objc Swift, src/HeroSignal.Maui.iOS.Binding/native)
                                                      → HeroSignal (sdk/ios)
```

Yerel SDK'ların Kotlin `object`/lambda/`Result` ve Swift `enum`/async API'leri doğrudan bağlanamadığı için her
platformda yalnızca metin (JSON), bool, sayı ve geri çağrı kullanan küçük bir köprü vardır. İki köprü aynı JSON
biçimlerini üretir; ayrıştırma ve olay yönlendirme platformdan bağımsız katmandadır ve birim testleriyle doğrulanır.
İzin değişiklikleri yerel SDK izin gözlemcisinden (`addPermissionObserver`) `permission` olayı (`{"permission":bool}`)
olarak gelir; köprüler ön plana dönüşte kendi izin yoklamasını yapmaz.

## Derleme ve test

```bash
# Paylaşılan katman + birim testleri (iş yükü gerekmez)
dotnet test sdk/maui/tests/HeroSignal.Maui.Tests -p:HeroSignalIncludeAndroid=false

# Android (maui-android iş yükü, JDK 17, ANDROID_HOME, PATH'te gradle 8.x)
dotnet build sdk/maui/src/HeroSignal.Maui -f net9.0-android

# iOS (macOS + Xcode 15+ + maui-ios iş yükü; xcframework otomatik üretilir)
dotnet build sdk/maui/src/HeroSignal.Maui -f net9.0-ios
```

Android AAR'ları `src/HeroSignal.Maui.Android.Binding/build/native`, iOS xcframework'ü
`src/HeroSignal.Maui.iOS.Binding/build/native` altına üretilir; `sdk/android` ve `sdk/ios` altına çıktı yazılmaz.
Örnek uygulama: `examples/maui-app`.

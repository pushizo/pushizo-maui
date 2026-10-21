<!-- pushizo:header -->
<p align="center">
  <a href="https://pushizo.com"><img src="https://raw.githubusercontent.com/pushizo/.github/main/profile/assets/mark.svg" alt="Pushizo" width="56" height="56"></a>
</p>
<p align="center">
  <a href="https://docs.pushizo.com/docs/sdk/maui"><img src="https://img.shields.io/badge/docs-docs.pushizo.com-5b4bdb" alt="Docs"></a>
  <a href="https://docs.pushizo.com/docs/api"><img src="https://img.shields.io/badge/API-reference-b44fc4" alt="API reference"></a>
  <a href="https://github.com/pushizo/pushizo-maui/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-16130f" alt="License: MIT"></a>
</p>
<p align="center"><a href="#english">English</a> · <a href="#türkçe">Türkçe</a></p>

## English

**Pushizo .NET MAUI SDK** — .NET MAUI SDK that wraps the native Pushizo Android and iOS SDKs. The API surface matches eski sağlayıcı v5 and the Pushizo Flutter plugin.

> **Status:** Publishing to NuGet is in progress.<br>
> **Durum:** NuGet yayını hazırlanıyor.

**Install from GitHub until then · O zamana kadar GitHub'dan kurulum:**

```bash
git clone --branch v0.1.0 https://github.com/pushizo/pushizo-maui
dotnet pack pushizo-maui -c Release -o ./packages   # macOS + MAUI iş yükü / workload
dotnet add package Pushizo.Maui --source ./packages
```

**After publication · Yayından sonra:**

```bash
dotnet add package Pushizo.Maui
```

The detailed guide below is in Turkish; code samples are language-neutral. Full documentation:
[docs.pushizo.com/docs/sdk/maui](https://docs.pushizo.com/docs/sdk/maui) · API reference: [docs.pushizo.com/docs/api](https://docs.pushizo.com/docs/api).

## Türkçe
<!-- /pushizo:header -->

# Pushizo .NET MAUI SDK (`Pushizo.Maui`)

Push bildirimleri, in-app mesajlar, bildirim kutusu, kullanıcı/tag/alias, bildirim tercihleri, konum paylaşımı ve e-posta/SMS abonelikleri için .NET MAUI
SDK'sı. Yerel Pushizo Android (`com.pushizo:pushizo-android`) ve iOS (`Pushizo` Swift) SDK'larını sarar;
tetik değerlendirme, çevrimdışı kuyruk, teslimat geri bildirimi ve in-app çizimi yerel SDK'dadır. API yüzeyi
eski sağlayıcı v5 ile uyumlu ve Flutter eklentisiyle aynıdır.

| Hedef | Davranış |
|-------|----------|
| `net8.0-android`, `net9.0-android` | Yerel Android SDK'sı (minSdk 23) |
| `net8.0-ios`, `net9.0-ios` | Yerel iOS SDK'sı (iOS 15+) |
| Diğerleri (Windows, MacCatalyst) | Çağrılar etkisizdir, bir kez uyarı yazılır; bildirim kutusu ve tercihler `PLATFORM_NOT_SUPPORTED` döndürür |

## Kurulum

```bash
dotnet add package Pushizo.Maui
```

```csharp
// MauiProgram.cs
using PushizoSdk.Maui;

var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>()
       .UsePushizo("app_…", o =>
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
- Android 13+ bildirim izni `Pushizo.Notifications.RequestPermissionAsync()` ile istenir. `UsePushizo` izin
  sonucunu yaşam döngüsü olayından otomatik alır; yalnızca `Pushizo.Initialize` kullanıyorsanız `MainActivity`
  içinde `PushizoAndroid.OnRequestPermissionsResult(requestCode)` çağırın.

### iOS

- `Platforms/iOS/Entitlements.plist` içinde `aps-environment`, `Info.plist` içinde `UIBackgroundModes` →
  `remote-notification` olmalıdır.
- Yerel SDK swizzling kullanmaz; APNs token'ını `AppDelegate`'ten iletin:
  ```csharp
  [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
  public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken) =>
      PushizoIos.RegisteredForRemoteNotifications(deviceToken);

  [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
  public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error) =>
      PushizoIos.FailedToRegisterForRemoteNotifications(error);
  ```
- Zengin medya, rozet ve teslimat onayı için Xcode'da bir Notification Service Extension hedefi ekleyip yerel
  `PushizoExtension` (SPM) ya da `Pushizo/Extension` (CocoaPods) ile `PushizoNotificationServiceExtension`
  sınıfını kullanın (bkz. [ios rehberi](https://docs.pushizo.com/docs/sdk/ios)).

## Kullanım

```csharp
await Pushizo.Notifications.RequestPermissionAsync(fallbackToSettings: true);
Pushizo.Login("user_42", identityHash: hashFromYourServer);   // identity verification açıksa hash zorunlu
Pushizo.User.AddTags(new Dictionary<string, string> { ["plan"] = "pro" });
Pushizo.User.AddAlias("crm_id", "42");
Pushizo.User.SetLanguage("tr");
Pushizo.User.AddEmail("ali@example.com");
Pushizo.User.AddSms("+905321234567");

// Bildirim tercihleri: kategori × kanal (Channel null = tüm kanallar)
var prefs = await Pushizo.User.GetPreferencesAsync();
foreach (var category in prefs.Categories) Console.WriteLine(category.LocalizedName("tr"));
await Pushizo.User.SetPreferencesAsync(new[]
{
    new PreferenceInput("promo", OptedIn: false),
    new PreferenceInput("news", OptedIn: true, Channel: "email"),
});

Pushizo.User.PushSubscription.Changed += (_, e) => Console.WriteLine(e.Current.Id);
Pushizo.Notifications.PermissionChanged += (_, e) => Console.WriteLine(e.Permission); // yerel izin gözlemcisinden
Pushizo.Notifications.Clicked += (_, e) => Navigate(e.Url);
Pushizo.Notifications.ForegroundWillDisplay += (_, e) =>
{
    if (IsChatOpen) e.PreventDefault();   // eşzamanlı; gerekirse sonra e.Display()
};

Pushizo.InAppMessages.AddTrigger("level", "gold");
Pushizo.InAppMessages.SetScreen("cart");
Pushizo.InAppMessages.Clicked += (_, e) => Console.WriteLine(e.ActionId);
Pushizo.InAppMessages.DidDisplay += (_, e) => Console.WriteLine(e.Message.Id);

var page = await Pushizo.Inbox.ListAsync(limit: 20);
await Pushizo.Inbox.MarkReadAsync(page.Items[0].Id);
await Pushizo.Inbox.MarkAllReadAsync();
await Pushizo.Inbox.ArchiveAsync(page.Items[0].Id);

Pushizo.Session.SendEvent("purchase", new Dictionary<string, object?> { ["sku"] = "A1" }, value: 129.9);
Pushizo.Notifications.ClearAll();
Pushizo.Notifications.RemoveGroupedNotifications("orders");  // Android android_group / iOS thread-id
Pushizo.Notifications.RemoveNotification(42);                // Android tepsi kimliği; iOS'ta RemoveNotification("istek-kimligi")
Pushizo.Debug.SetLogLevel(LogLevel.Debug);
Pushizo.Logout();
```

Olaylar yerel SDK'nın iş parçacığında tetiklenir; arayüz güncellemesi için `MainThread.BeginInvokeOnMainThread`
kullanın. Bildirim kutusu ve tercih hataları `PushizoException.Code` ile gelir (`NOT_READY`: abonelik/kullanıcı kaydı
henüz tamamlanmadı; `INVALID_ARGUMENT`: yerel SDK girdiyi reddetti; diğerleri sunucu hata kodu). `SetPreferencesAsync`
girdiyi köprüden önce doğrular (1-100 kayıt, kategori 1-64 karakter, kanal `ios_push` | `android_push` | `web_push` |
`email` | `sms` ya da null); geçersizse `ArgumentException`.

### JWT ile kimlik doğrulama

App ayarlarında kimlik doğrulama kipi (`identity_verification_mode`) `jwt` veya `both` ise kullanıcı, sunucunuzda
imzalanmış kısa ömürlü bir ES256 JWT ile kimliklendirilir (talepler: `sub` = external_id, `aud` = app_id, `exp`/`iat` zorunlu, en çok 24 saat, yalnızca ES256).

```csharp
Pushizo.Login("user_42", jwt: jwtFromYourServer);                         // yalnızca JWT
Pushizo.Login("user_42", identityHash: hmac, jwt: jwtFromYourServer);     // both kipinde ikisi birlikte
Pushizo.UpdateUserJwt(refreshedJwt); // süresi dolmadan yenileyin
Pushizo.Logout();
```

- JWT yerel SDK'da identity hash ile aynı kalıcı depoda saklanır ve tüm `/sdk/v1/...` isteklerinde
  `Authorization: Bearer <jwt>` başlığıyla gider; gövdeye/URL'ye yazılmaz, loglanmaz. `Logout` belirteci siler.
- `/config` yanıtında `identity_verification_required` true ise login öncesi kanıt denetlenir: `jwt` kipinde JWT, `hmac`
  kipinde `identityHash` zorunludur, `both` kipinde biri yeter; eksikse `Login` `PushizoException` (`NOT_READY`)
  fırlatır. Alan gelmeyen eski sunucularda kip `hmac` sayılır; mevcut `Login(id, identityHash)` çağrıları aynen çalışır.
- `UpdateUserJwt` giriş yapılmamışsa `PushizoException` (`NOT_READY`), boş belirteçte `ArgumentException` fırlatır;
  başarıda kuyrukta bekleyen işlemler yeni belirteçle yeniden denenir.

### Live Activities (iOS)

Köprü, iOS SDK'nın genel türü `PushizoDefaultLiveActivityAttributes`'ı kullanır. Dashboard/API'de Live
Activity türü adı tam olarak `PushizoDefaultLiveActivityAttributes` olmalıdır.

```csharp
// Initialize'dan sonra, uygulama her açıldığında: push-to-start (iOS 17.2+) ve güncelleme belirteçleri kaydedilir.
await Pushizo.LiveActivities.SetupDefaultLiveActivityAsync();

// Yerel başlatma (iOS 16.1+); sunucu sonraki update/end çağrılarını bu kimlikle yapar.
await Pushizo.LiveActivities.StartDefaultLiveActivityAsync(
    "order_42",
    new Dictionary<string, object> { ["title"] = "Sipariş #42" },
    new Dictionary<string, object> { ["status"] = "Hazırlanıyor", ["eta"] = 15 });
```

- Widget extension (Xcode'da Swift ile; MAUI uygulamasına `AdditionalAppExtensions` öğesiyle eklenir):
  ```swift
  import ActivityKit
  import SwiftUI
  import WidgetKit
  import PushizoExtension // CocoaPods: pod 'Pushizo/Extension' + import Pushizo

  struct OrderLiveActivity: Widget {
      var body: some WidgetConfiguration {
          ActivityConfiguration(for: PushizoDefaultLiveActivityAttributes.self) { context in
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
  SPM'de widget hedefine `PushizoExtension` ürünü eklenir (`import PushizoExtension`); CocoaPods'ta
  `Pushizo/Extension` alt spec'i (`import Pushizo`).
- Uygulamanın `Platforms/iOS/Info.plist` dosyasına `NSSupportsLiveActivities` = `YES` ekleyin.
- Sunucu: `POST /v1/apps/{appId}/live-activities/PushizoDefaultLiveActivityAttributes/start|update|end`
  (`attributes` ve `content_state` düz nesnedir; widget'ta `data` sözlüğünden okunur). Push-to-start iOS 17.2+,
  yerel başlatma iOS 16.1+ gerektirir.
- Köprü girdiyi doğrular: `activityId` 1-100 karakter (harf, rakam, `_ . : -`), anahtar 1-64 karakter, değer metin
  (en çok 4000 karakter), sonlu sayı veya bool; `attributes` içinde `pushizo` anahtarı ayrılmıştır. Geçersizse
  `PushizoException` (`INVALID_ARGUMENT`).
- Hatalar `PushizoException.Code` taşır: `PLATFORM_NOT_SUPPORTED` (iOS 16.1 öncesi, Android ve diğer hedefler),
  `NOT_READY` (yerel SDK başlatılmadı), `INVALID_ARGUMENT`, `LIVE_ACTIVITY_FAILED` (Live Activities kapalı,
  `NSSupportsLiveActivities` eksik vb.).
- Tip-güvenli kendi `ActivityAttributes` türünüz için native tarafta `Pushizo.LiveActivities.register(MyAttributes.self)`
  çağırın (ayrıntı: [ios rehberi](https://docs.pushizo.com/docs/sdk/ios) "Live Activities").

### HTML in-app ve carousel

In-app mesajları yerel Android/iOS SDK'sı çizer; bu paket köprüdür. HTML in-app (sandbox'lı WebView, köprü
`close`/`click`/`addTag`/`trackEvent`) ve carousel blokları yerel SDK'dan olduğu gibi devralınır: ek kod veya
yapılandırma gerekmez, tıklama ve yaşam döngüsü olayları mevcut in-app dinleyicilerine gelir. Yetenek bildirimi
(`supports=html,carousel`) yerel SDK tarafından yapılır; desteklemeyen eski yerel sürümlere sunucu HTML mesaj göndermez
ve carousel bloklarını çıkarır.

### Canlı sohbet

```csharp
await Pushizo.Chat.StartAsync(name: "Ali");
var config = await Pushizo.Chat.GetConfigAsync();          // Online, OfflineMessage, ek sınırları
var page = await Pushizo.Chat.GetMessagesAsync();
Pushizo.Chat.EventReceived += (_, e) => { if (e.Type == ChatEventType.Message) Render(e.Message!); };
Pushizo.Chat.Opened += (_, e) => OpenChat(e.ConversationId); // sohbet bildirimine tıklama
await Pushizo.Chat.StartListeningAsync();                  // ekran açıkken; ayrılınca StopListeningAsync()
await Pushizo.Chat.SendMessageAsync("Merhaba");            // bot hızlı yanıtı: SendMessageAsync(label, optionId)
await Pushizo.Chat.MarkReadAsync(page.Messages[^1].Seq);
```

Ziyaretçi kimliği, çevrimdışı kuyruk (aynı `client_message_id` ile yeniden deneme), canlı akış (SSE; koparsa üstel
geri çekilme + `after_seq` yoklaması) ve push ile gelen temsilci mesajları (`pushizo_chat`) yerel Android/iOS SDK'sındadır;
`start` sonrası login/logout ve push aboneliği değişince bağlama yerel SDK'da yenilenir, logout ziyaretçiyi sıfırlar.
Mesajlar `seq` ile sıralanır; ağ yokken gönderilen mesaj `pending` döner ve gönderilince olayla sunucu kopyası gelir.
Ayrıntı: [android rehberi](https://docs.pushizo.com/docs/sdk/android) / [ios rehberi](https://docs.pushizo.com/docs/sdk/ios) → "Canlı sohbet".
Android ve iOS dışındaki hedeflerde çağrılar `PushizoException` (`PLATFORM_NOT_SUPPORTED`) ile sonuçlanır.

### Konum

Konum paylaşımı isteğe bağlıdır ve yalnızca ön planda çalışır (arka plan konumu istenmez). Paylaşım açıkken yerel SDK
konumu oturum başında ve uygulama ön plana döndüğünde en sık 5 dakikada bir push aboneliğine yazar
(`PATCH /sdk/v1/apps/{appId}/subscriptions/{id}` → `location: { lat, lng, accuracy_m }`); gönderim yerel SDK'nın kalıcı
kuyruğuyla yapılır.

```csharp
// İzin yoksa önce açıklama diyaloğu (verilmezse SDK'nın Türkçe/İngilizce varsayılan metni), sonra sistem istemi.
var granted = await Pushizo.Location.RequestPermissionAsync("Yakındaki mağazaları göstermek için konumunuzu kullanırız.");
if (granted) Pushizo.Location.SetShared(true);   // tercih kalıcıdır

bool shared = Pushizo.Location.IsShared;          // paylaşım açık mı (izin durumundan bağımsız)
Pushizo.Location.SetShared(false);               // sunucuya location: null → konum ve rıza kaydı silinir
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
  (`Pushizo.Location.PERMISSION_REQUEST_CODE` = `0x484C`) istenir; köprü sonucu `UsePushizo` yaşam döngüsü olayından
  alır (yalnızca `Pushizo.Initialize` kullanıyorsanız bildirim izniyle aynı `PushizoAndroid.OnRequestPermissionsResult`
  çağrısı yeterlidir).
- İzin yoksa konum sessizce gönderilmez; `RequiresPrivacyConsent` açıkken `SetConsentGiven(true)` gelene kadar hiç
  gönderilmez.
- KVKK: ilk gönderilen konum sunucuda açık rıza kaydı olarak saklanır. Kullanıcıya paylaşımın amacını açıklayın ve
  kapatma seçeneği sunun; `SetShared(false)` sunucudaki konumu ve rıza kaydını hemen sildirir.
- Windows ve MacCatalyst hedeflerinde konum çağrıları etkisizdir (bir kez uyarı yazılır): `IsShared` ve
  `RequestPermissionAsync` `false` döner, hata fırlatılmaz.

## Mimari

```
Pushizo (C# cephe, doğrulama, olaylar)  ──  BridgePlatform (JSON sözleşmesi, ortak)
        │                                          │
        ├─ Android: Pushizo.Maui.Android.Binding → com.pushizo.maui.PushizoMauiBridge (Kotlin, native/android/bridge)
        │                                             → com.pushizo.sdk.Pushizo (sdk/android)
        └─ iOS:     Pushizo.Maui.iOS.Binding     → HSMauiBridge (@objc Swift, src/Pushizo.Maui.iOS.Binding/native)
                                                      → Pushizo (sdk/ios)
```

Yerel SDK'ların Kotlin `object`/lambda/`Result` ve Swift `enum`/async API'leri doğrudan bağlanamadığı için her
platformda yalnızca metin (JSON), bool, sayı ve geri çağrı kullanan küçük bir köprü vardır. İki köprü aynı JSON
biçimlerini üretir; ayrıştırma ve olay yönlendirme platformdan bağımsız katmandadır ve birim testleriyle doğrulanır.
İzin değişiklikleri yerel SDK izin gözlemcisinden (`addPermissionObserver`) `permission` olayı (`{"permission":bool}`)
olarak gelir; köprüler ön plana dönüşte kendi izin yoklamasını yapmaz.

## Derleme ve test

```bash
# Paylaşılan katman + birim testleri (iş yükü gerekmez)
dotnet test sdk/maui/tests/Pushizo.Maui.Tests -p:PushizoIncludeAndroid=false

# Android (maui-android iş yükü, JDK 17, ANDROID_HOME, PATH'te gradle 8.x)
dotnet build sdk/maui/src/Pushizo.Maui -f net9.0-android

# iOS (macOS + Xcode 15+ + maui-ios iş yükü; xcframework otomatik üretilir)
dotnet build sdk/maui/src/Pushizo.Maui -f net9.0-ios
```

Android AAR'ları `src/Pushizo.Maui.Android.Binding/build/native`, iOS xcframework'ü
`src/Pushizo.Maui.iOS.Binding/build/native` altına üretilir; `sdk/android` ve `sdk/ios` altına çıktı yazılmaz.
Adım adım kurulum ve örnek kod: [maui rehberi](https://docs.pushizo.com/docs/sdk/maui).

<!-- pushizo:footer -->
---

## Support · Destek

- **Guide · Rehber:** [docs.pushizo.com/docs/sdk/maui](https://docs.pushizo.com/docs/sdk/maui)
- **API reference & OpenAPI · API referansı ve OpenAPI:** [docs.pushizo.com/docs/api](https://docs.pushizo.com/docs/api) ·
  [pushizo-openapi](https://github.com/pushizo/pushizo-openapi)
- **Bugs & ideas · Hata ve öneri:** [Issues](https://github.com/pushizo/pushizo-maui/issues)
- **Security · Güvenlik:** please do not open public issues; email [security@pushizo.com](mailto:security@pushizo.com) ·
  herkese açık issue açmayın, bu adrese yazın.
- **Help · Destek:** [support@pushizo.com](mailto:support@pushizo.com)

This repository is mirrored automatically from the Pushizo main repository on every release. Please open an issue
instead of a pull request; fixes land in the main repository and ship with the next release.
Bu depo her sürümde Pushizo ana deposundan otomatik yansıtılır. Pull request yerine issue açın; düzeltmeler ana depoda
yapılır ve bir sonraki sürümle buraya gelir.

Released under the MIT License · MIT lisanslıdır — [LICENSE](https://github.com/pushizo/pushizo-maui/blob/main/LICENSE).
<!-- /pushizo:footer -->

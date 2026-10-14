# HeroSignal .NET MAUI SDK (`HeroSignal.Maui`)

Push bildirimleri, in-app mesajlar, bildirim kutusu, kullanıcı/tag/alias, bildirim tercihleri ve e-posta/SMS abonelikleri için .NET MAUI
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
HeroSignal.Debug.SetLogLevel(LogLevel.Debug);
HeroSignal.Logout();
```

Olaylar yerel SDK'nın iş parçacığında tetiklenir; arayüz güncellemesi için `MainThread.BeginInvokeOnMainThread`
kullanın. Bildirim kutusu ve tercih hataları `HeroSignalException.Code` ile gelir (`NOT_READY`: abonelik/kullanıcı kaydı
henüz tamamlanmadı; `INVALID_ARGUMENT`: yerel SDK girdiyi reddetti; diğerleri sunucu hata kodu). `SetPreferencesAsync`
girdiyi köprüden önce doğrular (1-100 kayıt, kategori 1-64 karakter, kanal `ios_push` | `android_push` | `web_push` |
`email` | `sms` ya da null); geçersizse `ArgumentException`.

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

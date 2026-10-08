# HeroSignal.Maui

HeroSignal push bildirimleri, in-app mesajlar ve bildirim kutusu için .NET MAUI SDK'sı. Android'de yerel
`com.herosignal:herosignal-android`, iOS'ta yerel `HeroSignal` (Swift) SDK'sını sarar; API eski sağlayıcı v5 yüzeyiyle
uyumludur (plan 06 §6.1). Ayrıntılı kurulum rehberi: [`docs/sdk/maui.md`](../../docs/sdk/maui.md).

```csharp
// MauiProgram.cs
builder.UseMauiApp<App>()
       .UseHeroSignal("app_…", o => o.ApiUrl = "https://api.example.com");

// Herhangi bir yerde
await HeroSignal.Notifications.RequestPermissionAsync();
HeroSignal.Login("user_42");
HeroSignal.User.AddTag("plan", "pro");
HeroSignal.Notifications.Clicked += (_, e) => Shell.Current.GoToAsync(e.Url ?? "//home");
HeroSignal.InAppMessages.AddTrigger("level", "gold");
var page = await HeroSignal.Inbox.ListAsync(limit: 20);
HeroSignal.Notifications.ClearAll();
```

## Yapı

| Yol | İçerik |
|-----|--------|
| `src/HeroSignal.Maui` | C# API (`net9.0`, `net9.0-android`, macOS'ta `net9.0-ios`); `Platforms/Android`, `Platforms/iOS`, `Platforms/Unsupported` |
| `src/HeroSignal.Maui.Android.Binding` | `sdk/android` AAR'ının .NET Android binding'i; AAR derleme sırasında Gradle ile `build/native` altına üretilir |
| `src/HeroSignal.Maui.iOS.Binding` | Swift SDK için Objective-C köprüsü (`native/`, SPM) ve .NET iOS binding'i (`ApiDefinition.cs`, `StructsAndEnums.cs`) |
| `tests/HeroSignal.Maui.Tests` | Ortak C# katmanının xUnit testleri (`FakePlatform`; ortak vektörler `sdk/testdata`) |
| `tools/herosignal-native-out.init.gradle` | Gradle çıktısını `sdk/android` dışına yönlendiren init betiği |

## Derleme ve test

```bash
cd sdk/maui
dotnet build HeroSignal.Maui.sln          # net9.0 + net9.0-android (JDK 17, Gradle 8, Android SDK gerekir)
dotnet test HeroSignal.Maui.sln
dotnet pack src/HeroSignal.Maui -c Release
```

- Android binding'i ilk derlemede `sdk/android`'i Gradle ile derler; kaynaklar değişince AAR yeniden üretilir.
  Hazır AAR: `-p:HeroSignalAndroidAar=<yol>/herosignal-release.aar`. Android SDK konumu `ANDROID_HOME`,
  `ANDROID_SDK_ROOT` ya da `%LOCALAPPDATA%\Android\Sdk`'den bulunur.
- iOS hedefi yalnızca macOS'ta (Xcode 15+, `maui-ios` iş yükü) derlenir; Windows'ta otomatik atlanır.
  Elle açmak için `-p:HeroSignalIncludeIos=true`. Köprü XCFramework'leri `native/build-xcframeworks.sh` ile
  üretilir; hazır klasör için `-p:HeroSignalXcframeworksDir=<klasör>`.

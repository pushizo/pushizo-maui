using System.Text.RegularExpressions;

namespace PushizoSdk.Maui.Tests;

/// <summary>
/// Yerel köprü kaynaklarının sözleşme testleri: Windows'ta Swift derlenemediği ve Android binding'i ayrı derlendiği için
/// C# çağrıları, iOS bağlama tanımı (ApiDefinition), Swift @objc seçicileri ve Kotlin @JvmStatic yüzeyi kaynak düzeyinde
/// karşılaştırılır.
/// </summary>
public sealed partial class NativeBridgeSourceContractTests
{
    private static readonly string Root = FindSdkRoot();

    private static string FindSdkRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Pushizo.Maui.sln"))) return dir.FullName;
        }
        throw new InvalidOperationException("sdk/maui kök klasörü bulunamadı.");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string ApiDefinition => Read("src", "Pushizo.Maui.iOS.Binding", "ApiDefinition.cs");

    private static string Swift => Read("src", "Pushizo.Maui.iOS.Binding", "native", "Sources", "PushizoMauiBridge", "HSMauiBridge.swift");

    private static string Kotlin => Read("native", "android", "bridge", "src", "main", "java", "com", "pushizo", "maui", "PushizoMauiBridge.kt");

    private static IEnumerable<string> PlatformSources(string platform) =>
        Directory.GetFiles(Path.Combine(Root, "src", "Pushizo.Maui", "Platforms", platform), "*.cs").Select(File.ReadAllText);

    [GeneratedRegex(@"\[Export \(""([^""]+)""\)\]")]
    private static partial Regex ExportPattern();

    [GeneratedRegex(@"@objc\(([^)]+)\)")]
    private static partial Regex ObjcPattern();

    [GeneratedRegex(@"\[Export \(""[^""]+""\)\]\s*(?:\[return: NullAllowed\]\s*)?[\w<>]+\s+(\w+)\s*\(")]
    private static partial Regex BoundMethodPattern();

    [GeneratedRegex(@"\bHSMauiBridge\.(\w+)\s*\(")]
    private static partial Regex IosCallPattern();

    [GeneratedRegex(@"@JvmStatic\s+fun\s+(\w+)\s*\(")]
    private static partial Regex KotlinFunctionPattern();

    [GeneratedRegex(@"\bPushizoMauiBridge\.(\w+)\s*\(")]
    private static partial Regex AndroidCallPattern();

    [Fact]
    public void Ios_binding_selectors_match_swift_objc_selectors()
    {
        var exported = ExportPattern().Matches(ApiDefinition).Select(m => m.Groups[1].Value).ToHashSet();
        var swift = ObjcPattern().Matches(Swift).Select(m => m.Groups[1].Value).Where(s => s != "HSMauiBridge").ToHashSet();

        Assert.Empty(exported.Except(swift));
        Assert.Empty(swift.Except(exported));
        Assert.Contains("locationShared", exported);
        Assert.Contains("updateLocationShared:", exported);
        Assert.Contains("requestLocationPermissionWithRationale:completion:", exported);
        Assert.Contains("geofencingEnabled", exported);
        Assert.Contains("updateGeofencingEnabled:", exported);
        Assert.Contains("featureFlagsRefreshWithCompletion:", exported);
        Assert.Contains("featureFlagEnabled:fallback:", exported);
        Assert.Contains("featureFlagValue:", exported);
        Assert.Contains("placementsList:limit:completion:", exported);
        Assert.Contains("placementsReportClick:buttonId:", exported);
    }

    [Fact]
    public void Package_version_matches_sdk_version_constant()
    {
        var props = Read("Directory.Build.props");
        var match = Regex.Match(props, "<PushizoVersion>([^<]+)</PushizoVersion>");

        Assert.True(match.Success);
        Assert.Equal(Pushizo.SdkVersion, match.Groups[1].Value);
    }

    [Fact]
    public void Native_bridges_cache_placement_messages_for_reports()
    {
        // Rapor çağrıları yerel tarafta mesaj nesnesi ister; köprüler son liste yanıtlarını kimliğe göre saklar (en çok 50).
        Assert.Contains("MAX_PLACEMENT_MESSAGES = 50", Kotlin);
        Assert.Contains("Pushizo.Placements.list(key, limit)", Kotlin);
        Assert.Contains("maxPlacementMessages = 50", Swift);
        Assert.Contains("Pushizo.Placements.list(key, limit: limit)", Swift);
        // Bölge izleme: köprüler izin istemez, yalnızca yerel tercihi yazar/okur.
        Assert.Contains("Pushizo.Location.setGeofencingEnabled(enabled)", Kotlin);
        Assert.Contains("Pushizo.Location.setGeofencingEnabled(enabled)", Swift);
        Assert.DoesNotContain("ACCESS_BACKGROUND_LOCATION\"", Kotlin);
        // Arka plan izni yalnızca açık istekte (requestBackgroundLocationPermission) sorulur.
        Assert.Single(Regex.Matches(Swift, @"Pushizo\.Location\.requestAlwaysPermission\("));
        Assert.Single(Regex.Matches(Kotlin, @"listOf\(Manifest\.permission\.ACCESS_BACKGROUND_LOCATION\)"));
    }

    [Fact]
    public void Native_parity_calls_reach_native_sdk_on_both_platforms()
    {
        var kotlin = Kotlin;
        var swift = Swift;

        // Profille giriş: iki platform da yerel profil aşırı yüklemesini çağırır (doğrulama ve sıra yerel SDK'dadır).
        Assert.Contains("Pushizo.login(externalId, profile, identityHash, jwt)", kotlin);
        Assert.Contains("HsUserProfile(", kotlin);
        var login = swift[swift.IndexOf("func loginWithProfile", StringComparison.Ordinal)..swift.IndexOf("func updateUserJwt", StringComparison.Ordinal)];
        Assert.Contains("HSUserProfile(", login);
        Assert.Contains("try Pushizo.login(externalId, profile: profile, identityHash: identityHash, jwt: jwt)", login);
        // Yerel hata mesajı köprüden metin olarak döner (C# katmanı NOT_READY'ye çevirir); köprü kendi doğrulamasını yapmaz.
        Assert.Contains("catch HSError.invalidArgument(let message)", login);
        Assert.DoesNotContain("HSContacts.", login);
        Assert.DoesNotContain("Pushizo.User.add", login);

        // Reklam rızası ve reklam kimliği (ADR-0037).
        Assert.Contains("Pushizo.User.setAdsPersonalizationConsent(granted, reply<HsPreferencesResult>(result)", kotlin);
        Assert.Contains("Pushizo.User.getConsents(reply<List<HsConsent>>(result)", kotlin);
        Assert.Contains("Pushizo.Ads.setAdvertisingId(id)", kotlin);
        Assert.Contains("try await Pushizo.User.setAdsPersonalizationConsent(granted)", swift);
        Assert.Contains("try await Pushizo.User.getConsents()", swift);
        Assert.Contains("Pushizo.Ads.setAdvertisingId(id)", swift);
        // C# ayrıştırıcısının okuduğu rıza alanları iki köprüde de yazılır.
        foreach (var field in new[] { "purpose", "granted", "source", "updatedAt" })
        {
            Assert.Contains($".put(\"{field}\", ", kotlin[kotlin.IndexOf("private fun consentJson", StringComparison.Ordinal)..]);
            Assert.Contains($"\"{field}\": ", swift[swift.IndexOf("private static func consentJson", StringComparison.Ordinal)..]);
        }

        // Rozet: iki platformda da yerel çağrı; Android sonucu (rozet gösterilebiliyor mu) ve saklanan sayıyı döndürür.
        Assert.Contains("Pushizo.Notifications.setBadgeCount(max(0, count))", swift);
        Assert.Contains("fun setBadgeCount(count: Int): Boolean = Pushizo.Notifications.setBadgeCount(count)", kotlin);
        Assert.Contains("fun badgeCount(): Int = Pushizo.Notifications.badgeCount", kotlin);
        Assert.DoesNotContain("Android'de etkisiz", kotlin);

        // Konum izin durumu ve arka plan izni.
        Assert.Contains("fun locationPermissionGranted(): Boolean = Pushizo.Location.permissionGranted", kotlin);
        Assert.Contains("fun locationBackgroundPermissionGranted(): Boolean = Pushizo.Location.backgroundPermissionGranted", kotlin);
        Assert.Contains("SdkR.string.hs_geofence_rationale_title", kotlin);
        Assert.Contains("SdkR.string.hs_geofence_rationale_message", kotlin);
        Assert.Contains("if (background) Pushizo.Location.backgroundPermissionGranted else Pushizo.Location.permissionGranted", kotlin);
        // iOS izin durumu yerel API'den okunur; köprüde kendi CLLocationManager uygulaması kalmaz.
        Assert.Contains("func locationPermissionGranted() -> Bool { Pushizo.Location.permissionGranted }", swift);
        Assert.Contains("func locationBackgroundPermissionGranted() -> Bool { Pushizo.Location.backgroundPermissionGranted }", swift);
        Assert.DoesNotContain("CLLocationManager", swift);
        Assert.DoesNotContain("import CoreLocation", swift);

        // ADM yalnızca Android'dedir; sohbet dinleme durumu iki platformda da yerel SDK'dan okunur.
        Assert.Contains("Pushizo.User.registerAdm(token)", kotlin);
        Assert.Contains("public string? RegisterAdm(string token) => null;", string.Concat(PlatformSources("iOS")));
        Assert.Contains("\"isListening\" -> result.onResult(JSONObject().put(\"listening\", chat.isListening)", kotlin);
        Assert.Contains("case \"isListening\":", swift);
        Assert.Contains("Pushizo.Chat.isListening", swift);
    }

    [Fact]
    public void Round_three_apis_reach_native_sdk_on_both_platforms()
    {
        var kotlin = Kotlin;
        var swift = Swift;
        var android = string.Concat(PlatformSources("Android"));
        var ios = string.Concat(PlatformSources("iOS"));

        // User.setProperty: değer `{"value":…}` zarfıyla taşınır; Android anahtar hatasını mesaja çevirir.
        Assert.Contains("Pushizo.User.setProperty(key, toValue(JSONObject(valueJson).opt(\"value\")))", kotlin);
        var property = kotlin[kotlin.IndexOf("fun setUserProperty", StringComparison.Ordinal)..kotlin.IndexOf("fun addEmail", StringComparison.Ordinal)];
        Assert.Contains("catch (e: IllegalArgumentException)", property);
        Assert.Contains("Pushizo.User.setProperty(key, value is NSNull ? nil : value)", swift);
        Assert.Contains("PushizoMauiBridge.SetUserProperty(key, valueJson)", android);
        Assert.Contains("HSMauiBridge.SetUserProperty(key, valueJson)", ios);

        // Gözlemciler başlatmada bir kez bağlanır ve olaya çevrilir (push aboneliği gözlemcisiyle aynı kalıp).
        Assert.Contains("HsFeatureFlagObserver { emit(\"featureFlags\", JSONObject()) }", kotlin);
        Assert.Contains("Pushizo.FeatureFlags.addObserver(featureFlagObserver)", kotlin);
        Assert.Contains("HsInboxObserver { event -> emit(\"inbox\", JSONObject(event.toMap())) }", kotlin);
        Assert.Contains("Pushizo.Inbox.addObserver(inboxObserver)", kotlin);
        Assert.Contains("Pushizo.FeatureFlags.addObserver { emit(\"featureFlags\", [:]) }", swift);
        Assert.Contains("Pushizo.Inbox.addObserver { event in emit(\"inbox\", event.dictionary) }", swift);
        // C# olay türleri köprülerin gönderdiği adlarla aynıdır.
        Assert.Equal("featureFlags", PushizoSdk.Maui.Internal.BridgeEvents.FeatureFlags);
        Assert.Equal("inbox", PushizoSdk.Maui.Internal.BridgeEvents.Inbox);

        // Session.sendEvent eventId: kimlik yoksa mevcut yerel çağrı, varsa eventId'li aşırı yükleme.
        Assert.Contains("if (eventId == null) Pushizo.Session.sendEvent(name, properties, amount)", kotlin);
        Assert.Contains("else Pushizo.Session.sendEvent(name, properties, amount, eventId)", kotlin);
        Assert.Contains("Pushizo.Session.sendEvent(name, properties: properties, value: hasValue ? value : nil, eventId: eventId)", swift);
        Assert.Contains("sendEvent:propertiesJson:value:hasValue:eventId:", ApiDefinition);

        // Hazır sohbet arayüzü: Android etkin Activity (yoksa uygulama bağlamı) ile açar ve açık ekranı yaşam döngüsünden izler.
        Assert.Contains("Pushizo.Chat.show(context)", kotlin);
        Assert.Contains("fun chatHide(): Boolean = Pushizo.Chat.hide()", kotlin);
        Assert.Contains("if (activity is HsChatActivity) chatActivity = WeakReference(activity)", kotlin);
        Assert.Contains("if (chatActivity?.get() === activity) chatActivity = null", kotlin);
        Assert.Contains("PushizoMauiBridge.ChatShow(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity)", android);
        Assert.Contains("func chatShow() { Pushizo.Chat.show() }", swift);
        Assert.Contains("Pushizo.Chat.hide()", swift);
        Assert.Contains("func chatIsPresented() -> Bool { readOnMain { Pushizo.Chat.isPresented } }", swift);

        // Rozet okuma: iOS'ta yerel karşılığı olmadığından sistemden okunur.
        Assert.Contains("UIApplication.shared.applicationIconBadgeNumber", swift);
        Assert.Contains("PushizoMauiBridge.BadgeCount()", android);
        Assert.Contains("HSMauiBridge.BadgeCount()", ios);
    }

    [Fact]
    public void Ios_csharp_calls_exist_in_binding()
    {
        var bound = BoundMethodPattern().Matches(ApiDefinition).Select(m => m.Groups[1].Value).ToHashSet();
        var called = PlatformSources("iOS").SelectMany(s => IosCallPattern().Matches(s).Select(m => m.Groups[1].Value)).ToHashSet();

        Assert.NotEmpty(called);
        Assert.Empty(called.Except(bound));
    }

    [Fact]
    public void Android_csharp_calls_exist_in_kotlin_bridge()
    {
        var kotlin = KotlinFunctionPattern().Matches(Kotlin).Select(m => m.Groups[1].Value).ToHashSet();
        // Binding üreticisi Java adlarını PascalCase'e çevirir (requestLocationPermission → RequestLocationPermission).
        var called = PlatformSources("Android")
            .SelectMany(s => AndroidCallPattern().Matches(s).Select(m => char.ToLowerInvariant(m.Groups[1].Value[0]) + m.Groups[1].Value[1..]))
            .ToHashSet();

        Assert.Contains("requestLocationPermission", called);
        Assert.Contains("placementsList", called);
        Assert.Contains("featureFlagsRefresh", called);
        Assert.Contains("updateGeofencingEnabled", called);
        Assert.Empty(called.Except(kotlin));
    }

    [Fact]
    public void Delivery_bridge_calls_native_delivery_api_and_ios_stays_inert()
    {
        var kotlin = Kotlin;

        Assert.Contains("Pushizo.Delivery.status()?.let(::deliveryStatusJson)?.toString()", kotlin);
        Assert.Contains("Pushizo.Delivery.shouldShowGuidance(minIntervalDays)", kotlin);
        Assert.Contains("Pushizo.Delivery.openSettings(context, deliverySetting(setting))", kotlin);
        Assert.Contains("Pushizo.Delivery.showGuidance(activity, deliverySetting(setting), message?.takeIf { it.isNotBlank() }) { opened -> callback.onResult(opened) }", kotlin);
        // Köprü pil optimizasyonu iznini istemez (ADR-0092 §3).
        Assert.DoesNotContain("REQUEST_IGNORE_BATTERY_OPTIMIZATIONS", kotlin);

        // C# ayrıştırıcısının okuduğu her alan Kotlin JSON'unda yazılır.
        var parser = Read("src", "Pushizo.Maui", "Internal", "BridgeJson.cs");
        var start = parser.IndexOf("public static DeliveryStatus ParseDeliveryStatus", StringComparison.Ordinal);
        var body = parser[start..parser.IndexOf("ParseRefreshed", start, StringComparison.Ordinal)];
        var fields = Regex.Matches(body, "(?:Str|Bool|TryGetProperty)\\((?:root, )?\"(\\w+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Equal(16, fields.Count);
        foreach (var field in fields) Assert.Contains($".put(\"{field}\", ", kotlin);

        // iOS'ta teşhis yoktur: yerel çağrı (binding) eklenmez, köprü sabit yanıt verir.
        var ios = string.Concat(PlatformSources("iOS"));
        Assert.Contains("public string? DeliveryStatus() => null;", ios);
        Assert.Contains("public bool DeliveryOpenSettings(string setting) => false;", ios);
        Assert.Contains("public void DeliveryShowGuidance(string setting, string? message, Action<bool> completion) => completion(false);", ios);
        Assert.DoesNotContain("HSMauiBridge.Delivery", ios);
    }

    [Fact]
    public void Android_bridge_resolves_location_permission_result()
    {
        var kotlin = Kotlin;

        // Sistem istemi SDK'nın istek koduyla açılır; sonuç yaşam döngüsü olayından ya da ön plana dönüşle çözülür.
        Assert.Contains("ActivityCompat.requestPermissions(activity, permissions.toTypedArray(), Pushizo.Location.PERMISSION_REQUEST_CODE)", kotlin);
        Assert.Contains("Pushizo.Location.PERMISSION_REQUEST_CODE -> resolveLocationRequest()", kotlin);
        Assert.Contains("Pushizo.Location.onPermissionResult()", kotlin);
        Assert.Matches(@"onActivityResumed\(activity: Activity\) \{\s*resolvePermissionRequest\(\)\s*resolveLocationRequest\(\)", kotlin);
        // Diyalog metinleri SDK string kaynaklarından gelir.
        foreach (var key in new[] { "hs_location_rationale_title", "hs_location_rationale_message", "hs_location_rationale_continue", "hs_location_rationale_not_now" })
            Assert.Contains($"SdkR.string.{key}", kotlin);
    }
}

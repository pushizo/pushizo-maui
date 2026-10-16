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
        Assert.Empty(called.Except(kotlin));
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

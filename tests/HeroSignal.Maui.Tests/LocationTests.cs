using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Tests;

/// <summary>Konum paylaşımı (v2 Faz 17b): köprüye iletim, açıklama metni normalizasyonu ve desteklenmeyen hedefler.</summary>
public sealed class LocationTests : HeroSignalTestBase
{
    [Fact]
    public void SetShared_and_IsShared_reach_bridge()
    {
        InitializeSdk();
        Assert.False(HeroSignal.Location.IsShared);

        HeroSignal.Location.SetShared(true);
        Assert.True(HeroSignal.Location.IsShared);
        HeroSignal.Location.SetShared(false);
        Assert.False(HeroSignal.Location.IsShared);

        Assert.Equal(new[] { "locationShared:True", "locationShared:False" }, Bridge.Calls);
    }

    [Fact]
    public void IsShared_reads_native_preference()
    {
        Bridge.LocationSharedValue = true;
        InitializeSdk();

        Assert.True(HeroSignal.Location.IsShared);
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task RequestPermission_returns_native_result()
    {
        InitializeSdk();

        Bridge.LocationPermissionResult = true;
        Assert.True(await HeroSignal.Location.RequestPermissionAsync());
        Bridge.LocationPermissionResult = false;
        Assert.False(await HeroSignal.Location.RequestPermissionAsync("Yakındaki mağazalar için"));

        Assert.Equal(new[] { "requestLocationPermission", "requestLocationPermission" }, Bridge.Calls);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Yakındaki mağazaları göstermek için konumunuzu kullanırız.  ", "Yakındaki mağazaları göstermek için konumunuzu kullanırız.")]
    public async Task RequestPermission_normalizes_rationale(string? rationale, string? expected)
    {
        InitializeSdk();

        await HeroSignal.Location.RequestPermissionAsync(rationale);

        // Boş açıklama null olarak iletilir; yerel SDK kendi tr/en metnini kullanır.
        Assert.Equal(expected, Bridge.LastLocationRationale);
    }

    [Fact]
    public void Calls_before_initialize_throw_and_reads_are_safe()
    {
        Bridge.LocationSharedValue = true;

        Assert.False(HeroSignal.Location.IsShared);
        Assert.Throws<InvalidOperationException>(() => HeroSignal.Location.SetShared(true));
        // Bildirim izniyle aynı: başlatılmadan çağrı eşzamanlı olarak hata fırlatır (Task döndürülmez).
        Assert.Throws<InvalidOperationException>(() => { _ = HeroSignal.Location.RequestPermissionAsync(); });
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_is_a_safe_no_op()
    {
        HeroSignal.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        HeroSignal.Location.SetShared(true);
        Assert.False(HeroSignal.Location.IsShared);
        Assert.False(await HeroSignal.Location.RequestPermissionAsync("açıklama"));
    }
}

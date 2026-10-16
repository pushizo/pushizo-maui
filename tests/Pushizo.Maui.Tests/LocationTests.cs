using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>Konum paylaşımı (v2 Faz 17b): köprüye iletim, açıklama metni normalizasyonu ve desteklenmeyen hedefler.</summary>
public sealed class LocationTests : PushizoTestBase
{
    [Fact]
    public void SetShared_and_IsShared_reach_bridge()
    {
        InitializeSdk();
        Assert.False(Pushizo.Location.IsShared);

        Pushizo.Location.SetShared(true);
        Assert.True(Pushizo.Location.IsShared);
        Pushizo.Location.SetShared(false);
        Assert.False(Pushizo.Location.IsShared);

        Assert.Equal(new[] { "locationShared:True", "locationShared:False" }, Bridge.Calls);
    }

    [Fact]
    public void IsShared_reads_native_preference()
    {
        Bridge.LocationSharedValue = true;
        InitializeSdk();

        Assert.True(Pushizo.Location.IsShared);
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task RequestPermission_returns_native_result()
    {
        InitializeSdk();

        Bridge.LocationPermissionResult = true;
        Assert.True(await Pushizo.Location.RequestPermissionAsync());
        Bridge.LocationPermissionResult = false;
        Assert.False(await Pushizo.Location.RequestPermissionAsync("Yakındaki mağazalar için"));

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

        await Pushizo.Location.RequestPermissionAsync(rationale);

        // Boş açıklama null olarak iletilir; yerel SDK kendi tr/en metnini kullanır.
        Assert.Equal(expected, Bridge.LastLocationRationale);
    }

    [Fact]
    public void Calls_before_initialize_throw_and_reads_are_safe()
    {
        Bridge.LocationSharedValue = true;

        Assert.False(Pushizo.Location.IsShared);
        Assert.Throws<InvalidOperationException>(() => Pushizo.Location.SetShared(true));
        // Bildirim izniyle aynı: başlatılmadan çağrı eşzamanlı olarak hata fırlatır (Task döndürülmez).
        Assert.Throws<InvalidOperationException>(() => { _ = Pushizo.Location.RequestPermissionAsync(); });
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_is_a_safe_no_op()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Pushizo.Location.SetShared(true);
        Assert.False(Pushizo.Location.IsShared);
        Assert.False(await Pushizo.Location.RequestPermissionAsync("açıklama"));
    }
}

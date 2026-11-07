using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>Özellik bayrakları (ADR-0066): köprüye iletim, değer dönüşümü, varsayılanlar ve desteklenmeyen hedefler.</summary>
public sealed class FeatureFlagsTests : PushizoTestBase
{
    [Fact]
    public async Task Refresh_returns_native_result()
    {
        InitializeSdk();

        Assert.True(await Pushizo.FeatureFlags.RefreshAsync());
        Bridge.FeatureFlagsRefreshResponse = ("""{"refreshed":false}""", null, null);
        Assert.False(await Pushizo.FeatureFlags.RefreshAsync());

        Assert.Equal(new[] { "featureFlagsRefresh", "featureFlagsRefresh" }, Bridge.Calls);
    }

    [Fact]
    public async Task Refresh_errors_carry_native_code()
    {
        InitializeSdk();
        Bridge.FeatureFlagsRefreshResponse = (null, "HS_ERROR", "ağ hatası");

        var error = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.FeatureFlags.RefreshAsync());
        Assert.Equal("HS_ERROR", error.Code);

        Bridge.FeatureFlagsRefreshResponse = ("{bozuk", null, null);
        var invalid = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.FeatureFlags.RefreshAsync());
        Assert.Equal(PushizoException.InvalidResponse, invalid.Code);
    }

    [Fact]
    public void IsEnabled_passes_default_to_bridge()
    {
        InitializeSdk();
        Bridge.FeatureFlags["yeni_sepet"] = (true, null);

        Assert.True(Pushizo.FeatureFlags.IsEnabled("yeni_sepet"));
        Assert.False(Pushizo.FeatureFlags.IsEnabled("bilinmeyen"));
        Assert.True(Pushizo.FeatureFlags.IsEnabled("bilinmeyen", defaultValue: true));

        Assert.Equal(new[] { "featureFlagEnabled:yeni_sepet:False", "featureFlagEnabled:bilinmeyen:False", "featureFlagEnabled:bilinmeyen:True" }, Bridge.Calls);
    }

    [Fact]
    public void GetValue_maps_json_values()
    {
        InitializeSdk();
        Bridge.FeatureFlags["metin"] = (true, """{"value":"koyu"}""");
        Bridge.FeatureFlags["tam"] = (true, """{"value":10}""");
        Bridge.FeatureFlags["ondalik"] = (true, """{"value":2.5}""");
        Bridge.FeatureFlags["mantiksal"] = (true, """{"value":false}""");
        Bridge.FeatureFlags["nesne"] = (true, """{"value":{"limit":3,"etiketler":["a","b"],"bos":null}}""");
        Bridge.FeatureFlags["dizi"] = (true, """{"value":[1,"iki",true]}""");
        Bridge.FeatureFlags["degersiz"] = (true, null);
        Bridge.FeatureFlags["null"] = (true, """{"value":null}""");

        Assert.Equal("koyu", Pushizo.FeatureFlags.GetValue("metin"));
        Assert.Equal(10L, Pushizo.FeatureFlags.GetValue("tam"));
        Assert.Equal(2.5d, Pushizo.FeatureFlags.GetValue("ondalik"));
        Assert.Equal(false, Pushizo.FeatureFlags.GetValue("mantiksal"));

        var nesne = Assert.IsType<Dictionary<string, object?>>(Pushizo.FeatureFlags.GetValue("nesne"));
        Assert.Equal(3L, nesne["limit"]);
        Assert.Equal(new List<object?> { "a", "b" }, Assert.IsType<List<object?>>(nesne["etiketler"]));
        Assert.Null(nesne["bos"]);
        Assert.Equal(new List<object?> { 1L, "iki", true }, Assert.IsType<List<object?>>(Pushizo.FeatureFlags.GetValue("dizi")));

        // Değer yoksa koddaki varsayılan olduğu gibi döner.
        Assert.Null(Pushizo.FeatureFlags.GetValue("degersiz"));
        Assert.Equal(10, Pushizo.FeatureFlags.GetValue("degersiz", 10));
        Assert.Equal("varsayilan", Pushizo.FeatureFlags.GetValue("null", "varsayilan"));
        Assert.Equal("varsayilan", Pushizo.FeatureFlags.GetValue("bilinmeyen", "varsayilan"));
    }

    [Fact]
    public void GetValue_rejects_malformed_bridge_json()
    {
        InitializeSdk();
        Bridge.FeatureFlags["bozuk"] = (true, "{bozuk");

        var error = Assert.Throws<PushizoException>(() => Pushizo.FeatureFlags.GetValue("bozuk"));
        Assert.Equal(PushizoException.InvalidResponse, error.Code);
    }

    [Fact]
    public void Reads_before_initialize_return_default_and_refresh_throws()
    {
        Bridge.FeatureFlags["yeni_sepet"] = (true, """{"value":"x"}""");

        Assert.False(Pushizo.FeatureFlags.IsEnabled("yeni_sepet"));
        Assert.True(Pushizo.FeatureFlags.IsEnabled("yeni_sepet", defaultValue: true));
        Assert.Equal(5, Pushizo.FeatureFlags.GetValue("yeni_sepet", 5));
        Assert.Throws<InvalidOperationException>(() => { _ = Pushizo.FeatureFlags.RefreshAsync(); });
        Assert.Empty(Bridge.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Key_is_required(string? key)
    {
        InitializeSdk();

        Assert.Throws<ArgumentException>(() => Pushizo.FeatureFlags.IsEnabled(key!));
        Assert.Throws<ArgumentException>(() => Pushizo.FeatureFlags.GetValue(key!));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_returns_defaults()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Assert.False(await Pushizo.FeatureFlags.RefreshAsync());
        Assert.True(Pushizo.FeatureFlags.IsEnabled("x", defaultValue: true));
        Assert.Equal(7, Pushizo.FeatureFlags.GetValue("x", 7));
    }
}

using System.Text.Json;
using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>iOS Live Activities (ADR-0024): köprü JSON sözleşmesi, köprü tarafı doğrulama ve hata eşleme.</summary>
public sealed class LiveActivitiesTests : PushizoTestBase
{
    public LiveActivitiesTests() => InitializeSdk();

    private static Dictionary<string, object> Values(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public async Task Setup_reaches_bridge()
    {
        await Pushizo.LiveActivities.SetupDefaultLiveActivityAsync();

        Assert.Equal("setupDefaultLiveActivity", Bridge.Calls[^1]);
        Assert.Equal("PushizoDefaultLiveActivityAttributes", LiveActivitiesManager.DefaultActivityType);
    }

    [Fact]
    public async Task Start_serializes_normalized_values_as_flat_json()
    {
        await Pushizo.LiveActivities.StartDefaultLiveActivityAsync(
            "order:42",
            Values(("title", "Sipariş"), ("count", 3), ("big", 5_000_000_000L), ("ratio", 0.1f), ("price", 12.5m), ("vip", true)),
            Values(("status", "yolda"), ("eta", 15.5)));

        var call = Bridge.Calls[^1];
        Assert.StartsWith("startDefaultLiveActivity:order:42:", call);
        // Biçim: startDefaultLiveActivity:{id}:{attributesJson}:{contentStateJson}; kimlik ':' içerdiğinden JSON'lar sondan ayrılır.
        var json = call["startDefaultLiveActivity:order:42:".Length..];
        var split = json.IndexOf("}:{", StringComparison.Ordinal);
        using var attributes = JsonDocument.Parse(json[..(split + 1)]);
        using var state = JsonDocument.Parse(json[(split + 2)..]);

        var a = attributes.RootElement;
        Assert.Equal("Sipariş", a.GetProperty("title").GetString());
        Assert.Equal(3, a.GetProperty("count").GetInt64());
        Assert.Equal(5_000_000_000L, a.GetProperty("big").GetInt64());
        Assert.Equal(0.1, a.GetProperty("ratio").GetDouble());
        Assert.Equal(12.5, a.GetProperty("price").GetDouble());
        Assert.Equal(JsonValueKind.True, a.GetProperty("vip").ValueKind);
        Assert.Equal("yolda", state.RootElement.GetProperty("status").GetString());
        Assert.Equal(15.5, state.RootElement.GetProperty("eta").GetDouble());
    }

    [Fact]
    public async Task Start_allows_empty_dictionaries()
    {
        await Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", new Dictionary<string, object>(), new Dictionary<string, object>());

        Assert.Equal("startDefaultLiveActivity:a1:{}:{}", Bridge.Calls[^1]);
    }

    [Fact]
    public async Task Start_validates_before_bridge()
    {
        var empty = new Dictionary<string, object>();
        var invalid = new List<Func<Task>>
        {
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("", empty, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync(null!, empty, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync(new string('a', 101), empty, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("sipariş", empty, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("order 1", empty, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("order\n", empty, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", null!, empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", empty, null!),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("pushizo", "x")), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("", "x")), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values((new string('k', 65), "x")), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("t", new string('x', 4001))), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("nested", new Dictionary<string, object>())), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("list", new[] { 1, 2 })), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("when", DateTime.UtcNow)), empty),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", empty, Values(("eta", double.NaN))),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", empty, Values(("eta", float.PositiveInfinity))),
            () => Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", empty, new Dictionary<string, object> { ["eta"] = null! }),
        };
        foreach (var call in invalid)
        {
            var error = await Assert.ThrowsAsync<PushizoException>(call);
            Assert.Equal(PushizoException.InvalidArgument, error.Code);
        }

        Assert.Empty(Bridge.Calls);
        // pushizo yalnızca attributes içinde ayrılmıştır; geçerli kimlik biçimleri kabul edilir.
        await Pushizo.LiveActivities.StartDefaultLiveActivityAsync("Order_1.a:b-c", empty, Values(("pushizo", "x")));
        await Pushizo.LiveActivities.StartDefaultLiveActivityAsync(new string('a', 100), Values((new string('k', 64), new string('x', 4000))), empty);
        Assert.Equal(2, Bridge.Calls.Count);
    }

    [Fact]
    public async Task Native_errors_keep_code_and_message()
    {
        Bridge.LiveActivityResponse = (null, PushizoException.LiveActivityFailed, "Live Activities bu cihazda kapalı");
        var start = await Assert.ThrowsAsync<PushizoException>(() =>
            Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", new Dictionary<string, object>(), new Dictionary<string, object>()));
        Assert.Equal("LIVE_ACTIVITY_FAILED", start.Code);
        Assert.Equal("Live Activities bu cihazda kapalı", start.Message);

        Bridge.LiveActivityResponse = (null, PushizoException.PlatformNotSupported, "Live Activities iOS 16.1 veya üstünü gerektirir");
        var setup = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.LiveActivities.SetupDefaultLiveActivityAsync());
        Assert.Equal(PushizoException.PlatformNotSupported, setup.Code);
        Assert.Equal("Live Activities iOS 16.1 veya üstünü gerektirir", setup.Message);

        Bridge.LiveActivityResponse = (null, PushizoException.NotReady, "Pushizo.initialize çağrılmadı");
        Assert.Equal(PushizoException.NotReady, (await Assert.ThrowsAsync<PushizoException>(() => Pushizo.LiveActivities.SetupDefaultLiveActivityAsync())).Code);
    }

    [Fact]
    public async Task Android_bridge_reports_platform_not_supported_without_native_call()
    {
        Bridge.SupportsLiveActivities = false;

        var setup = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.LiveActivities.SetupDefaultLiveActivityAsync());
        var start = await Assert.ThrowsAsync<PushizoException>(() =>
            Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", Values(("title", "x")), Values(("status", "y"))));

        Assert.Equal(PushizoException.PlatformNotSupported, setup.Code);
        Assert.Equal(PushizoException.PlatformNotSupported, start.Code);
        Assert.Equal("Live Activities yalnızca iOS'ta desteklenir.", start.Message);
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_reports_platform_not_supported()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Assert.Equal(PushizoException.PlatformNotSupported,
            (await Assert.ThrowsAsync<PushizoException>(() => Pushizo.LiveActivities.SetupDefaultLiveActivityAsync())).Code);
        Assert.Equal(PushizoException.PlatformNotSupported,
            (await Assert.ThrowsAsync<PushizoException>(() =>
                Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", new Dictionary<string, object>(), new Dictionary<string, object>()))).Code);
    }

    [Fact]
    public async Task Calls_before_initialize_throw()
    {
        Pushizo.ResetForTests(new BridgePlatform(Bridge));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Pushizo.LiveActivities.SetupDefaultLiveActivityAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Pushizo.LiveActivities.StartDefaultLiveActivityAsync("a1", new Dictionary<string, object>(), new Dictionary<string, object>()));
        Assert.Empty(Bridge.Calls);
    }
}

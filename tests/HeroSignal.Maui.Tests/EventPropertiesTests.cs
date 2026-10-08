using System.Text.Json;
using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Tests;

public sealed class EventPropertiesTests
{
    private enum Tier { Gold }

    [Fact]
    public void Normalizes_supported_types_to_canonical_tree()
    {
        var id = Guid.Parse("8b0c5f0e-8c8b-4d2a-9d1c-2c9a3a1f0b7e");
        var result = EventProperties.Normalize(new Dictionary<string, object?>
        {
            ["s"] = "metin",
            ["b"] = true,
            ["i"] = 7,
            ["byte"] = (byte)2,
            ["ul"] = ulong.MaxValue,
            ["f"] = 1.5f,
            ["m"] = 2.25m,
            ["c"] = 'x',
            ["g"] = id,
            ["e"] = Tier.Gold,
            ["dto"] = new DateTimeOffset(2026, 10, 8, 12, 30, 0, TimeSpan.FromHours(3)),
            ["dt"] = new DateTime(2026, 10, 8, 9, 30, 0, DateTimeKind.Utc),
            ["n"] = null,
            ["list"] = new object?[] { 1, "a", null },
            ["map"] = new Dictionary<string, int> { ["k"] = 1 },
        }, "properties");

        Assert.Equal("metin", result["s"]);
        Assert.Equal(true, result["b"]);
        Assert.Equal(7L, result["i"]);
        Assert.Equal(2L, result["byte"]);
        Assert.Equal((double)ulong.MaxValue, result["ul"]);
        Assert.Equal(1.5, result["f"]);
        Assert.Equal(2.25, result["m"]);
        Assert.Equal("x", result["c"]);
        Assert.Equal(id.ToString(), result["g"]);
        Assert.Equal("Gold", result["e"]);
        Assert.Equal("2026-10-08T09:30:00.000Z", result["dto"]);
        Assert.Equal("2026-10-08T09:30:00.000Z", result["dt"]);
        Assert.Null(result["n"]);
        Assert.Equal(new List<object?> { 1L, "a", null }, result["list"]);
        Assert.Equal(1L, ((Dictionary<string, object?>)result["map"]!)["k"]);
    }

    [Fact]
    public void Rejects_invalid_values()
    {
        Assert.Throws<ArgumentException>(() => EventProperties.Normalize(new Dictionary<string, object?> { [""] = 1 }, "p"));
        Assert.Throws<ArgumentException>(() => EventProperties.Normalize(new Dictionary<string, object?> { ["x"] = double.PositiveInfinity }, "p"));
        Assert.Throws<ArgumentException>(() => EventProperties.Normalize(new Dictionary<string, object?> { ["x"] = new object() }, "p"));
        Assert.Throws<ArgumentException>(() => EventProperties.Normalize(new Dictionary<string, object?> { ["x"] = new Dictionary<int, int> { [1] = 1 } }, "p"));

        object? deep = 1;
        for (var i = 0; i < 17; i++) deep = new List<object?> { deep };
        Assert.Throws<ArgumentException>(() => EventProperties.Normalize(new Dictionary<string, object?> { ["x"] = deep }, "p"));
    }

    [Fact]
    public void Null_properties_become_empty()
    {
        Assert.Empty(EventProperties.Normalize(null, "p"));
    }

    [Fact]
    public void Writes_canonical_tree_as_json()
    {
        var tree = EventProperties.Normalize(new Dictionary<string, object?>
        {
            ["a"] = 1,
            ["b"] = new[] { true, false },
            ["c"] = new Dictionary<string, object?> { ["d"] = null, ["e"] = "ş" },
            ["f"] = 0.5,
        }, "p");
        using var doc = JsonDocument.Parse(EventProperties.ToJson(tree));
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("a").GetInt64());
        Assert.Equal(2, root.GetProperty("b").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("c").GetProperty("d").ValueKind);
        Assert.Equal("ş", root.GetProperty("c").GetProperty("e").GetString());
        Assert.Equal(0.5, root.GetProperty("f").GetDouble());
    }
}

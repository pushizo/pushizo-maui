using System.Text.Json;
using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Tests;

/// <summary>Ortak test vektörleri (<c>sdk/testdata/messaging-vectors.json</c>) ve doğrulama sınırları.</summary>
public sealed class ValidationTests
{
    public static IEnumerable<object?[]> EmailVectors() => Vectors("email");
    public static IEnumerable<object?[]> PhoneVectors() => Vectors("phone");

    private static IEnumerable<object?[]> Vectors(string section)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testdata", "messaging-vectors.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var item in doc.RootElement.GetProperty(section).EnumerateArray())
        {
            var expected = item.GetProperty("expected");
            yield return new object?[] { item.GetProperty("input").GetString(), expected.ValueKind == JsonValueKind.Null ? null : expected.GetString() };
        }
    }

    [Theory]
    [MemberData(nameof(EmailVectors))]
    public void Email_vectors(string input, string? expected) => Assert.Equal(expected, Validation.NormalizeEmail(input));

    [Theory]
    [MemberData(nameof(PhoneVectors))]
    public void Phone_vectors(string input, string? expected) => Assert.Equal(expected, Validation.NormalizePhone(input));

    [Fact]
    public void Vector_file_has_cases()
    {
        Assert.NotEmpty(EmailVectors());
        Assert.NotEmpty(PhoneVectors());
    }

    [Theory]
    [InlineData("crm_id", true)]
    [InlineData("a", true)]
    [InlineData("Crm", false)]
    [InlineData("crm-id", false)]
    [InlineData("external_id", false)]
    [InlineData("hs_id", false)]
    public void Alias_labels(string label, bool valid)
    {
        var error = Record.Exception(() => Validation.RequireAliasLabel(label, "label"));
        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Alias_values_are_trimmed_and_bounded()
    {
        var result = Validation.NormalizeAliases(new Dictionary<string, string> { ["crm_id"] = " 42 " }, "a");
        Assert.Equal("42", result["crm_id"]);
        Assert.Throws<ArgumentException>(() => Validation.NormalizeAliases(new Dictionary<string, string>(), "a"));
        Assert.Throws<ArgumentException>(() => Validation.NormalizeAliases(new Dictionary<string, string> { ["k"] = "  " }, "a"));
        Assert.Throws<ArgumentException>(() => Validation.NormalizeAliases(new Dictionary<string, string> { ["k"] = new string('x', 256) }, "a"));
    }

    [Theory]
    [InlineData("purchase", true)]
    [InlineData("checkout.completed", true)]
    [InlineData("ns:event-1", true)]
    [InlineData("bad name", false)]
    [InlineData("çarşı", false)]
    [InlineData("", false)]
    public void Event_names(string name, bool valid)
    {
        var error = Record.Exception(() => Validation.RequireEventName(name, "name"));
        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Inbox_limit_bounds()
    {
        Validation.RequireInboxLimit(1, "limit");
        Validation.RequireInboxLimit(200, "limit");
        Assert.Throws<ArgumentOutOfRangeException>(() => Validation.RequireInboxLimit(0, "limit"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Validation.RequireInboxLimit(201, "limit"));
    }
}

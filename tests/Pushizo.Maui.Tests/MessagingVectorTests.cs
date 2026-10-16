using System.Text.Json;
using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>Android/iOS/sunucu ile ortak normalizasyon vektörleri (sdk/testdata/messaging-vectors.json).</summary>
public sealed class MessagingVectorTests
{
    private static JsonElement Vectors(string section)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testdata", "messaging-vectors.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty(section).Clone();
    }

    [Fact]
    public void Email_vectors()
    {
        var count = 0;
        foreach (var v in Vectors("email").EnumerateArray())
        {
            var expected = v.GetProperty("expected").ValueKind == JsonValueKind.Null ? null : v.GetProperty("expected").GetString();
            Assert.Equal(expected, Validation.NormalizeEmail(v.GetProperty("input").GetString()));
            count++;
        }
        Assert.True(count > 0);
    }

    [Fact]
    public void Phone_vectors()
    {
        var count = 0;
        foreach (var v in Vectors("phone").EnumerateArray())
        {
            var expected = v.GetProperty("expected").ValueKind == JsonValueKind.Null ? null : v.GetProperty("expected").GetString();
            Assert.Equal(expected, Validation.NormalizePhone(v.GetProperty("input").GetString()));
            count++;
        }
        Assert.True(count > 0);
    }
}

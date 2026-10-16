using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>Kullanıcı tercihleri (ADR-0019 §4): köprü JSON sözleşmesi, doğrulama ve hata eşleme.</summary>
public sealed class PreferencesTests : PushizoTestBase
{
    private const string ResultJson = """
        {"preferences":[
          {"category":"promo","channel":null,"optedIn":false,"explicit":true,"source":"sdk","updatedAt":1759838400000},
          {"category":"news","channel":"email","optedIn":true,"explicit":false,"source":null,"updatedAt":null},
          {"category":"orders"}
        ],
        "categories":[
          {"key":"promo","name":{"tr":"Kampanyalar","en":"Promotions"},"description":{"tr":"İndirim duyuruları"},"channels":["android_push","email"],"defaultOptedIn":false},
          {"key":"news","name":{"de":"Neuigkeiten"},"description":{},"channels":null,"defaultOptedIn":true},
          {"key":"orders"},
          {"name":{"tr":"Anahtarsız"}}
        ]}
        """;

    public PreferencesTests() => InitializeSdk();

    [Fact]
    public async Task Get_preferences_parses_preferences_and_categories()
    {
        Bridge.PreferencesResponse = (ResultJson, null, null);

        var result = await Pushizo.User.GetPreferencesAsync();

        Assert.Equal("getPreferences", Bridge.Calls[^1]);
        Assert.Equal(3, result.Preferences.Count);
        var promo = result.Preferences[0];
        Assert.Equal("promo", promo.Category);
        Assert.Null(promo.Channel);
        Assert.False(promo.OptedIn);
        Assert.True(promo.Explicit);
        Assert.Equal("sdk", promo.Source);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759838400000), promo.UpdatedAt);
        var news = result.Preferences[1];
        Assert.Equal("email", news.Channel);
        Assert.True(news.OptedIn);
        Assert.False(news.Explicit);
        Assert.Null(news.Source);
        Assert.Null(news.UpdatedAt);
        Assert.False(result.Preferences[2].OptedIn);

        // Anahtarsız kategori atlanır.
        Assert.Equal(new[] { "promo", "news", "orders" }, result.Categories.Select(c => c.Key));
        var promoCategory = result.Categories[0];
        Assert.Equal("Kampanyalar", promoCategory.LocalizedName("tr"));
        Assert.Equal("Promotions", promoCategory.LocalizedName("fr"));
        Assert.Equal("İndirim duyuruları", promoCategory.Description["tr"]);
        Assert.Equal(new[] { "android_push", "email" }, promoCategory.Channels);
        Assert.False(promoCategory.DefaultOptedIn);
        Assert.Null(result.Categories[1].Channels);
        Assert.Equal("Neuigkeiten", result.Categories[1].LocalizedName("tr"));
        Assert.True(result.Categories[2].DefaultOptedIn);
        Assert.Equal("orders", result.Categories[2].LocalizedName("tr"));
    }

    [Fact]
    public async Task Set_preferences_serializes_inputs_and_returns_updated_list()
    {
        Bridge.PreferencesResponse = (ResultJson, null, null);

        var result = await Pushizo.User.SetPreferencesAsync(new[]
        {
            new PreferenceInput("promo", OptedIn: false),
            new PreferenceInput("news", OptedIn: true, Channel: "email"),
        });

        Assert.Equal(
            """setPreferences:[{"category":"promo","channel":null,"optedIn":false},{"category":"news","channel":"email","optedIn":true}]""",
            Bridge.Calls[^1]);
        Assert.Equal(3, result.Preferences.Count);
    }

    [Fact]
    public async Task Set_preferences_validates_inputs_before_bridge()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Pushizo.User.SetPreferencesAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.User.SetPreferencesAsync(Array.Empty<PreferenceInput>()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Pushizo.User.SetPreferencesAsync(Enumerable.Range(0, 101).Select(i => new PreferenceInput($"c{i}", true))));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.User.SetPreferencesAsync(new[] { new PreferenceInput(" ", true) }));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.User.SetPreferencesAsync(new[] { new PreferenceInput(new string('k', 65), true) }));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.User.SetPreferencesAsync(new[] { new PreferenceInput("promo", true, "push") }));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.User.SetPreferencesAsync(new PreferenceInput[] { null! }));
        Assert.Empty(Bridge.Calls);

        // Sınır değerler kabul edilir.
        await Pushizo.User.SetPreferencesAsync(Enumerable.Range(0, 100).Select(i => new PreferenceInput(new string('k', 64), i % 2 == 0, "sms")));
        Assert.Single(Bridge.Calls);
    }

    [Fact]
    public async Task Preference_errors_carry_native_code()
    {
        Bridge.PreferencesResponse = (null, "NOT_READY", "Abonelik henüz kaydedilmedi");
        var notReady = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetPreferencesAsync());
        Assert.Equal(PushizoException.NotReady, notReady.Code);
        Assert.Equal("Abonelik henüz kaydedilmedi", notReady.Message);

        Bridge.PreferencesResponse = (null, PushizoException.InvalidArgument, "Geçersiz kanal");
        var invalidArgument = await Assert.ThrowsAsync<PushizoException>(() =>
            Pushizo.User.SetPreferencesAsync(new[] { new PreferenceInput("promo", true) }));
        Assert.Equal(PushizoException.InvalidArgument, invalidArgument.Code);

        Bridge.PreferencesResponse = (null, null, null);
        var empty = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetPreferencesAsync());
        Assert.Equal(PushizoException.InvalidResponse, empty.Code);

        Bridge.PreferencesResponse = ("""{"preferences":[{"optedIn":true}]}""", null, null);
        var missingCategory = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetPreferencesAsync());
        Assert.Equal(PushizoException.InvalidResponse, missingCategory.Code);

        Bridge.PreferencesResponse = ("[]", null, null);
        var notObject = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetPreferencesAsync());
        Assert.Equal(PushizoException.InvalidResponse, notObject.Code);
    }

    [Fact]
    public async Task Preferences_require_initialize_and_native_platform()
    {
        Pushizo.ResetForTests(new BridgePlatform(Bridge));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pushizo.User.GetPreferencesAsync());

        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();
        var get = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetPreferencesAsync());
        Assert.Equal(PushizoException.PlatformNotSupported, get.Code);
        var set = await Assert.ThrowsAsync<PushizoException>(() =>
            Pushizo.User.SetPreferencesAsync(new[] { new PreferenceInput("promo", true) }));
        Assert.Equal(PushizoException.PlatformNotSupported, set.Code);
    }
}

using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>
/// Yerel SDK eşitliği: profille giriş, reklam rızası ve reklam kimliği (ADR-0037), rozet, konum izin durumu ve arka plan
/// konum izni, ADM kaydı, sohbet dinleme durumu. Köprüye iletim, doğrulama ve desteklenmeyen hedefler sınanır.
/// </summary>
public sealed class NativeParityTests : PushizoTestBase
{
    [Fact]
    public void Login_with_profile_normalizes_and_reaches_bridge()
    {
        InitializeSdk();

        Pushizo.Login(
            "user_42",
            new PushizoUserProfile
            {
                Email = " Ali@Ornek.com ",
                Sms = "+90 (532) 123-45-67",
                Tags = new Dictionary<string, string> { ["plan"] = "pro" },
                Aliases = new Dictionary<string, string> { ["crm_id"] = "42" },
            },
            jwt: "token");

        // JSON yazıcısı "+" işaretini \u002B olarak kaçışlar; içerik ayrıştırılarak karşılaştırılır.
        var call = Assert.Single(Bridge.Calls);
        Assert.StartsWith("loginWithProfile:user_42:{", call);
        Assert.EndsWith("}:<null>:token", call);
        using var profile = System.Text.Json.JsonDocument.Parse(call["loginWithProfile:user_42:".Length..^":<null>:token".Length]);
        var root = profile.RootElement;
        Assert.Equal("ali@ornek.com", root.GetProperty("email").GetString());
        Assert.Equal("+905321234567", root.GetProperty("sms").GetString());
        Assert.Equal("pro", root.GetProperty("tags").GetProperty("plan").GetString());
        Assert.Equal("42", root.GetProperty("aliases").GetProperty("crm_id").GetString());
    }

    [Fact]
    public void Login_with_empty_profile_sends_empty_object_and_plain_login_is_unchanged()
    {
        InitializeSdk();

        Pushizo.Login("user_1", new PushizoUserProfile(), identityHash: "hash");
        // Mevcut çağrı biçimleri (null kanıt dahil) profil aşırı yüklemesine kaymaz.
        Pushizo.Login("user_2", null, "token");

        Assert.Equal(new[] { "loginWithProfile:user_1:{}:hash:<null>", "login:user_2:<null>:jwt=token" }, Bridge.Calls);
    }

    [Fact]
    public void Login_with_invalid_profile_fails_before_login()
    {
        InitializeSdk();

        Assert.Throws<ArgumentException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Email = "gecersiz" }));
        Assert.Throws<ArgumentException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Sms = "abc" }));
        Assert.Throws<ArgumentException>(() => Pushizo.Login(" ", new PushizoUserProfile { Email = "ali@ornek.com" }));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public void Login_with_profile_surfaces_native_error()
    {
        InitializeSdk();
        Bridge.LoginError = "identityHash gerekli";

        var error = Assert.Throws<PushizoException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" }));

        Assert.Equal(PushizoException.NotReady, error.Code);
    }

    [Fact]
    public async Task Ads_consent_returns_preferences_and_consents_are_parsed()
    {
        InitializeSdk();
        Bridge.PreferencesResponse = ("""{"preferences":[{"category":"ads_personalization","channel":null,"optedIn":true,"explicit":true,"source":"sdk","updatedAt":1759838400000}],"categories":[]}""", null, null);
        Bridge.ConsentsResponse = ("""
            {"consents":[
              {"purpose":"ads_personalization","granted":true,"source":"sdk","updatedAt":1759838400000},
              {"purpose":"other","granted":false,"source":null,"updatedAt":null},
              {"granted":true}
            ]}
            """, null, null);

        var preferences = await Pushizo.User.SetAdsPersonalizationConsentAsync(true);
        var consents = await Pushizo.User.GetConsentsAsync();

        Assert.Equal(new[] { "setAdsPersonalizationConsent:True", "getConsents" }, Bridge.Calls);
        Assert.Equal("ads_personalization", Assert.Single(preferences.Preferences).Category);
        // Amaçsız kayıt atlanır.
        Assert.Equal(2, consents.Count);
        Assert.Equal("ads_personalization", consents[0].Purpose);
        Assert.True(consents[0].Granted);
        Assert.Equal("sdk", consents[0].Source);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759838400000), consents[0].UpdatedAt);
        Assert.False(consents[1].Granted);
        Assert.Null(consents[1].Source);
        Assert.Null(consents[1].UpdatedAt);
    }

    [Fact]
    public async Task Consent_errors_keep_native_code()
    {
        InitializeSdk();
        Bridge.ConsentsResponse = (null, "NOT_READY", "Abonelik henüz kaydedilmedi");
        Bridge.PreferencesResponse = (null, "NOT_READY", "Abonelik henüz kaydedilmedi");

        var get = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetConsentsAsync());
        var set = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.SetAdsPersonalizationConsentAsync(false));

        Assert.Equal(PushizoException.NotReady, get.Code);
        Assert.Equal(PushizoException.NotReady, set.Code);
    }

    [Theory]
    [InlineData("38400000-8cf0-11bd-b23e-10b96e40000d", "38400000-8cf0-11bd-b23e-10b96e40000d")]
    [InlineData("  38400000-8cf0-11bd-b23e-10b96e40000d ", "38400000-8cf0-11bd-b23e-10b96e40000d")]
    [InlineData(null, "<null>")]
    [InlineData("  ", "<null>")]
    public void Advertising_id_reaches_bridge(string? id, string expected)
    {
        InitializeSdk();

        Pushizo.Ads.SetAdvertisingId(id);

        Assert.Equal(new[] { $"advertisingId:{expected}" }, Bridge.Calls);
    }

    [Fact]
    public void Badge_count_is_validated_and_clear_sends_zero()
    {
        InitializeSdk();

        Pushizo.Notifications.SetBadgeCount(3);
        Pushizo.Notifications.ClearBadge();
        Assert.Throws<ArgumentOutOfRangeException>(() => Pushizo.Notifications.SetBadgeCount(-1));

        Assert.Equal(new[] { "badge:3", "badge:0" }, Bridge.Calls);
    }

    [Fact]
    public async Task Location_permission_state_and_background_request_reach_bridge()
    {
        Assert.False(Pushizo.Location.PermissionGranted);
        Assert.False(Pushizo.Location.BackgroundPermissionGranted);
        InitializeSdk();
        Bridge.LocationPermissionGrantedValue = true;
        Assert.True(Pushizo.Location.PermissionGranted);
        Assert.False(Pushizo.Location.BackgroundPermissionGranted);
        Bridge.LocationBackgroundPermissionGrantedValue = true;
        Assert.True(Pushizo.Location.BackgroundPermissionGranted);

        Bridge.BackgroundLocationPermissionResult = false;
        Assert.False(await Pushizo.Location.RequestBackgroundPermissionAsync("  Mağazaya vardığınızda haber vermek için  "));
        Assert.Equal("Mağazaya vardığınızda haber vermek için", Bridge.LastLocationRationale);
        Bridge.BackgroundLocationPermissionResult = true;
        // "Her zaman" izni aynı kavramdır: aynı köprü çağrısına gider, açıklama metni yoktur.
        Assert.True(await Pushizo.Location.RequestAlwaysPermissionAsync());
        Assert.Null(Bridge.LastLocationRationale);

        Assert.Equal(new[] { "requestBackgroundLocationPermission", "requestBackgroundLocationPermission" }, Bridge.Calls);
    }

    [Fact]
    public void Register_adm_trims_validates_and_maps_native_error()
    {
        InitializeSdk();

        Pushizo.User.RegisterAdm("  amzn1.adm-registration.v3.abc  ");
        Assert.Throws<ArgumentException>(() => Pushizo.User.RegisterAdm("  "));
        Bridge.RegisterAdmError = "ADM registration id";
        Assert.Throws<ArgumentException>(() => Pushizo.User.RegisterAdm("x"));

        Assert.Equal(new[] { "registerAdm:amzn1.adm-registration.v3.abc", "registerAdm:x" }, Bridge.Calls);
    }

    [Fact]
    public async Task Chat_is_listening_reads_native_state()
    {
        InitializeSdk();
        Bridge.ChatResponses["isListening"] = ("""{"listening":true}""", null, null);

        Assert.True(await Pushizo.Chat.IsListeningAsync());
        Assert.Equal("isListening", Bridge.ChatCalls[^1].Method);

        Bridge.ChatResponses["isListening"] = ("""{"listening":false}""", null, null);
        Assert.False(await Pushizo.Chat.IsListeningAsync());
    }

    [Fact]
    public void New_calls_require_initialize()
    {
        Assert.Throws<InvalidOperationException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" }));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Ads.SetAdvertisingId("id"));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Notifications.SetBadgeCount(1));
        Assert.Throws<InvalidOperationException>(() => Pushizo.User.RegisterAdm("id"));
        Assert.Throws<InvalidOperationException>(() => { _ = Pushizo.Location.RequestBackgroundPermissionAsync(); });
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_is_a_safe_no_op()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" });
        Pushizo.Ads.SetAdvertisingId("38400000-8cf0-11bd-b23e-10b96e40000d");
        Pushizo.Notifications.SetBadgeCount(2);
        Pushizo.Notifications.ClearBadge();
        Pushizo.User.RegisterAdm("amzn1.adm-registration.v3.abc");
        Assert.False(Pushizo.Location.PermissionGranted);
        Assert.False(Pushizo.Location.BackgroundPermissionGranted);
        Assert.False(await Pushizo.Location.RequestBackgroundPermissionAsync("açıklama"));
        Assert.False(await Pushizo.Location.RequestAlwaysPermissionAsync());
        Assert.False(await Pushizo.Chat.IsListeningAsync());

        // Rıza, tercihlerle aynı uçtadır: yerel SDK olmayan hedefte aynı hata.
        var set = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.SetAdsPersonalizationConsentAsync(true));
        Assert.Equal(PushizoException.PlatformNotSupported, set.Code);
        var get = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetConsentsAsync());
        Assert.Equal(PushizoException.PlatformNotSupported, get.Code);
    }
}

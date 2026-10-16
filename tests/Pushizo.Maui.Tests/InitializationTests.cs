using System.Text.Json;
using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

public sealed class InitializationTests : PushizoTestBase
{
    [Fact]
    public void Calls_before_initialize_throw()
    {
        Assert.False(Pushizo.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => Pushizo.Login("user_1"));
        Assert.Throws<InvalidOperationException>(() => Pushizo.User.AddTag("a", "b"));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Session.SendEvent("purchase"));
        // Okuma özellikleri başlatılmadan da güvenlidir.
        Assert.Null(Pushizo.User.ExternalId);
        Assert.Empty(Pushizo.User.GetTags());
        Assert.False(Pushizo.Notifications.Permission);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_requires_app_id(string appId) =>
        Assert.Throws<ArgumentException>(() => Pushizo.Initialize(appId));

    [Theory]
    [InlineData("")]
    [InlineData("api.example.com")]
    [InlineData("ftp://api.example.com")]
    public void Initialize_requires_http_api_url(string apiUrl) =>
        Assert.Throws<ArgumentException>(() => Pushizo.Initialize("app_test", new PushizoOptions { ApiUrl = apiUrl }));

    [Fact]
    public void Initialize_passes_options_as_json()
    {
        InitializeSdk(new PushizoOptions
        {
            ApiUrl = "http://10.0.2.2:4000",
            RequiresPrivacyConsent = true,
            LogLevel = LogLevel.Debug,
            AndroidDefaultChannelName = "Kampanyalar",
            AndroidDefaultChannelId = "promo",
            IosAppGroup = "group.com.example.app",
            IosProvisionalAuthorization = true,
        });

        Assert.Equal("app_test", Bridge.StartedAppId);
        using var doc = JsonDocument.Parse(Bridge.OptionsJson!);
        var o = doc.RootElement;
        Assert.Equal("http://10.0.2.2:4000", o.GetProperty("apiUrl").GetString());
        Assert.True(o.GetProperty("requiresPrivacyConsent").GetBoolean());
        Assert.Equal(4, o.GetProperty("logLevel").GetInt32());
        Assert.Equal("Kampanyalar", o.GetProperty("androidDefaultChannelName").GetString());
        Assert.Equal("promo", o.GetProperty("androidDefaultChannelId").GetString());
        Assert.Equal("group.com.example.app", o.GetProperty("iosAppGroup").GetString());
        Assert.Equal(JsonValueKind.Null, o.GetProperty("iosApnsEnvironment").ValueKind);
        Assert.True(o.GetProperty("iosProvisionalAuthorization").GetBoolean());
        Assert.Equal(LogLevel.Debug, Pushizo.Debug.LogLevel);
    }

    [Fact]
    public void Initialize_is_idempotent_for_same_app_id_and_rejects_another()
    {
        InitializeSdk();
        InitializeSdk();
        Assert.Equal(1, Bridge.StartCount);
        Assert.True(Pushizo.IsInitialized);
        Assert.True(Pushizo.IsSupported);
        Assert.Throws<InvalidOperationException>(() => Pushizo.Initialize("app_other"));
    }

    [Fact]
    public void Options_are_copied_at_initialize()
    {
        var options = new PushizoOptions { ApiUrl = "https://api.example.com" };
        Pushizo.Initialize("app_test", options);
        options.ApiUrl = "https://changed.example.com";
        Assert.Contains("https://api.example.com", Bridge.OptionsJson);
    }

    [Fact]
    public void Explicit_log_level_before_initialize_wins_over_options()
    {
        Pushizo.Debug.SetLogLevel(LogLevel.Info);
        InitializeSdk(new PushizoOptions { ApiUrl = "https://api.example.com", LogLevel = LogLevel.Error });
        using var doc = JsonDocument.Parse(Bridge.OptionsJson!);
        Assert.Equal(3, doc.RootElement.GetProperty("logLevel").GetInt32());

        Pushizo.Debug.SetLogLevel(LogLevel.None);
        Assert.Contains("logLevel:0", Bridge.Calls);
        Assert.Throws<ArgumentOutOfRangeException>(() => Pushizo.Debug.SetLogLevel((LogLevel)42));
    }

    [Fact]
    public void Login_logout_and_consent_reach_bridge()
    {
        InitializeSdk();
        Pushizo.Login("user_42");
        Pushizo.Login("user_43", "hash_abc");
        Pushizo.Login("user_44", "");
        Pushizo.Logout();
        Pushizo.SetConsentGiven(true);

        Assert.Equal(
            new[] { "login:user_42:<null>", "login:user_43:hash_abc", "login:user_44:<null>", "logout", "consent:True" },
            Bridge.Calls);
        Assert.Throws<ArgumentException>(() => Pushizo.Login(" "));
    }

    [Fact]
    public void Jwt_identity_verification_reaches_bridge_and_surfaces_errors()
    {
        InitializeSdk();
        Pushizo.Login("user_42", jwt: "a.b.c");
        Pushizo.Login("user_43", "hash_abc", "a.b.c");
        Pushizo.Login("user_44", jwt: " ");
        Pushizo.UpdateUserJwt("d.e.f");

        Assert.Equal(
            new[] { "login:user_42:<null>:jwt=a.b.c", "login:user_43:hash_abc:jwt=a.b.c", "login:user_44:<null>", "updateUserJwt:d.e.f" },
            Bridge.Calls);
        Assert.Throws<ArgumentException>(() => Pushizo.UpdateUserJwt(""));

        // Yerel SDK kanıtı app kipine uygun bulmazsa veya kullanıcı yoksa hata NOT_READY olarak yansır.
        Bridge.LoginError = "Bu app için kimlik JWT'si zorunlu.";
        var login = Assert.Throws<PushizoException>(() => Pushizo.Login("user_45", "hash_abc"));
        Assert.Equal(PushizoException.NotReady, login.Code);
        Assert.Equal("Bu app için kimlik JWT'si zorunlu.", login.Message);
        Bridge.UpdateUserJwtError = "Önce login ile kullanıcı kimliklendirilmeli";
        Assert.Equal(PushizoException.NotReady, Assert.Throws<PushizoException>(() => Pushizo.UpdateUserJwt("g.h.i")).Code);
    }

    [Fact]
    public void Unsupported_platform_is_a_safe_no_op()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        Pushizo.Initialize("app_test", new PushizoOptions { ApiUrl = "https://api.example.com" });

        Assert.False(Pushizo.IsSupported);
        Pushizo.Login("user_1");
        Pushizo.UpdateUserJwt("a.b.c");
        Pushizo.User.AddTag("plan", "pro");
        Pushizo.InAppMessages.AddTrigger("level", "gold");
        Pushizo.Session.SendEvent("purchase", value: 9.99);
        Assert.Empty(Pushizo.User.GetTags());
        Assert.Null(Pushizo.User.PushSubscription.Id);
    }

    [Fact]
    public async Task Unsupported_platform_inbox_reports_platform_not_supported()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        Pushizo.Initialize("app_test", new PushizoOptions { ApiUrl = "https://api.example.com" });

        var error = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Inbox.ListAsync());
        Assert.Equal(PushizoException.PlatformNotSupported, error.Code);
        Assert.False(await Pushizo.Notifications.RequestPermissionAsync());
    }
}

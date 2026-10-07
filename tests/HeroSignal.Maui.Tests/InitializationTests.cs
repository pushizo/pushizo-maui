using System.Text.Json;
using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Tests;

public sealed class InitializationTests : HeroSignalTestBase
{
    [Fact]
    public void Calls_before_initialize_throw()
    {
        Assert.False(HeroSignal.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => HeroSignal.Login("user_1"));
        Assert.Throws<InvalidOperationException>(() => HeroSignal.User.AddTag("a", "b"));
        Assert.Throws<InvalidOperationException>(() => HeroSignal.Session.SendEvent("purchase"));
        // Okuma özellikleri başlatılmadan da güvenlidir.
        Assert.Null(HeroSignal.User.ExternalId);
        Assert.Empty(HeroSignal.User.GetTags());
        Assert.False(HeroSignal.Notifications.Permission);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_requires_app_id(string appId) =>
        Assert.Throws<ArgumentException>(() => HeroSignal.Initialize(appId));

    [Theory]
    [InlineData("")]
    [InlineData("api.example.com")]
    [InlineData("ftp://api.example.com")]
    public void Initialize_requires_http_api_url(string apiUrl) =>
        Assert.Throws<ArgumentException>(() => HeroSignal.Initialize("app_test", new HeroSignalOptions { ApiUrl = apiUrl }));

    [Fact]
    public void Initialize_passes_options_as_json()
    {
        InitializeSdk(new HeroSignalOptions
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
        Assert.Equal(LogLevel.Debug, HeroSignal.Debug.LogLevel);
    }

    [Fact]
    public void Initialize_is_idempotent_for_same_app_id_and_rejects_another()
    {
        InitializeSdk();
        InitializeSdk();
        Assert.Equal(1, Bridge.StartCount);
        Assert.True(HeroSignal.IsInitialized);
        Assert.True(HeroSignal.IsSupported);
        Assert.Throws<InvalidOperationException>(() => HeroSignal.Initialize("app_other"));
    }

    [Fact]
    public void Options_are_copied_at_initialize()
    {
        var options = new HeroSignalOptions { ApiUrl = "https://api.example.com" };
        HeroSignal.Initialize("app_test", options);
        options.ApiUrl = "https://changed.example.com";
        Assert.Contains("https://api.example.com", Bridge.OptionsJson);
    }

    [Fact]
    public void Explicit_log_level_before_initialize_wins_over_options()
    {
        HeroSignal.Debug.SetLogLevel(LogLevel.Info);
        InitializeSdk(new HeroSignalOptions { ApiUrl = "https://api.example.com", LogLevel = LogLevel.Error });
        using var doc = JsonDocument.Parse(Bridge.OptionsJson!);
        Assert.Equal(3, doc.RootElement.GetProperty("logLevel").GetInt32());

        HeroSignal.Debug.SetLogLevel(LogLevel.None);
        Assert.Contains("logLevel:0", Bridge.Calls);
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroSignal.Debug.SetLogLevel((LogLevel)42));
    }

    [Fact]
    public void Login_logout_and_consent_reach_bridge()
    {
        InitializeSdk();
        HeroSignal.Login("user_42");
        HeroSignal.Login("user_43", "hash_abc");
        HeroSignal.Login("user_44", "");
        HeroSignal.Logout();
        HeroSignal.SetConsentGiven(true);

        Assert.Equal(
            new[] { "login:user_42:<null>", "login:user_43:hash_abc", "login:user_44:<null>", "logout", "consent:True" },
            Bridge.Calls);
        Assert.Throws<ArgumentException>(() => HeroSignal.Login(" "));
    }

    [Fact]
    public void Unsupported_platform_is_a_safe_no_op()
    {
        HeroSignal.ResetForTests(new UnsupportedPlatform());
        HeroSignal.Initialize("app_test", new HeroSignalOptions { ApiUrl = "https://api.example.com" });

        Assert.False(HeroSignal.IsSupported);
        HeroSignal.Login("user_1");
        HeroSignal.User.AddTag("plan", "pro");
        HeroSignal.InAppMessages.AddTrigger("level", "gold");
        HeroSignal.Session.SendEvent("purchase", value: 9.99);
        Assert.Empty(HeroSignal.User.GetTags());
        Assert.Null(HeroSignal.User.PushSubscription.Id);
    }

    [Fact]
    public async Task Unsupported_platform_inbox_reports_platform_not_supported()
    {
        HeroSignal.ResetForTests(new UnsupportedPlatform());
        HeroSignal.Initialize("app_test", new HeroSignalOptions { ApiUrl = "https://api.example.com" });

        var error = await Assert.ThrowsAsync<HeroSignalException>(() => HeroSignal.Inbox.ListAsync());
        Assert.Equal(HeroSignalException.PlatformNotSupported, error.Code);
        Assert.False(await HeroSignal.Notifications.RequestPermissionAsync());
    }
}

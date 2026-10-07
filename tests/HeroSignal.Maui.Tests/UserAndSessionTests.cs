using System.Text.Json;

namespace HeroSignalSdk.Maui.Tests;

public sealed class UserAndSessionTests : HeroSignalTestBase
{
    public UserAndSessionTests() => InitializeSdk();

    [Fact]
    public void Tags_are_serialized_and_parsed()
    {
        HeroSignal.User.AddTag("plan", "pro");
        HeroSignal.User.AddTags(new Dictionary<string, string> { ["city"] = "İzmir", ["level"] = "3" });
        HeroSignal.User.RemoveTag("plan");
        HeroSignal.User.AddTags(new Dictionary<string, string>());

        Assert.Equal("addTags:{\"plan\":\"pro\"}", Bridge.Calls[0]);
        using (var doc = JsonDocument.Parse(Bridge.Calls[1]["addTags:".Length..]))
        {
            Assert.Equal("İzmir", doc.RootElement.GetProperty("city").GetString());
        }
        Assert.Equal("removeTags:[\"plan\"]", Bridge.Calls[2]);
        Assert.Equal(3, Bridge.Calls.Count);

        Bridge.TagsJson = """{"plan":"pro","score":10,"vip":true}""";
        var tags = HeroSignal.User.GetTags();
        Assert.Equal("pro", tags["plan"]);
        Assert.Equal("10", tags["score"]);
        Assert.Equal("true", tags["vip"]);
    }

    [Fact]
    public void Tag_validation()
    {
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddTag("", "x"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddTags(new Dictionary<string, string> { ["k"] = null! }));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.RemoveTags(new[] { "ok", " " }));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public void Aliases_are_validated_and_trimmed()
    {
        HeroSignal.User.AddAlias("crm_id", "  42 ");
        Assert.Equal("addAliases:{\"crm_id\":\"42\"}", Bridge.Calls[0]);

        HeroSignal.User.RemoveAlias("crm_id");
        Assert.Equal("removeAlias:crm_id", Bridge.Calls[1]);

        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("external_id", "x"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("hs_id", "x"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("CRM", "x"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("crm_id", " "));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("crm_id", new string('x', 256)));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.RemoveAlias("external_id"));
        var tooMany = Enumerable.Range(0, 21).ToDictionary(i => $"a{i}", i => "v");
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAliases(tooMany));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAliases(new Dictionary<string, string>()));
        Assert.Equal(2, Bridge.Calls.Count);
    }

    [Fact]
    public void Email_and_sms_are_normalized()
    {
        HeroSignal.User.AddEmail(" Ali.Veli@Example.COM ");
        HeroSignal.User.RemoveEmail("ali.veli@example.com");
        HeroSignal.User.AddSms("+90 (532) 123-45-67");
        HeroSignal.User.RemoveSms("00905321234567");

        Assert.Equal(new[]
        {
            "addEmail:ali.veli@example.com",
            "removeEmail:ali.veli@example.com",
            "addSms:+905321234567",
            "removeSms:+905321234567",
        }, Bridge.Calls);
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddEmail("geçersiz"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddSms("05321234567"));

        Bridge.EmailsJson = """["ali@example.com"]""";
        Bridge.SmsJson = """["+905321234567"]""";
        Assert.Equal(new[] { "ali@example.com" }, HeroSignal.User.Emails);
        Assert.Equal(new[] { "+905321234567" }, HeroSignal.User.SmsNumbers);
    }

    [Fact]
    public void Language_and_identity()
    {
        HeroSignal.User.SetLanguage(" tr ");
        Assert.Equal("language:tr", Bridge.Calls[0]);
        Assert.Throws<ArgumentException>(() => HeroSignal.User.SetLanguage(""));

        Bridge.ExternalIdValue = "user_42";
        Bridge.UserIdValue = "usr_1";
        Assert.Equal("user_42", HeroSignal.User.ExternalId);
        Assert.Equal("usr_1", HeroSignal.User.Id);
    }

    [Fact]
    public void Push_subscription_reads_state_and_forwards_opt_in()
    {
        Bridge.SubscriptionJson = """{"id":"sub_1","token":"tok","optedIn":true,"permission":true}""";
        Assert.Equal("sub_1", HeroSignal.User.PushSubscription.Id);
        Assert.Equal("tok", HeroSignal.User.PushSubscription.Token);
        Assert.True(HeroSignal.User.PushSubscription.OptedIn);

        HeroSignal.User.PushSubscription.OptOut();
        HeroSignal.User.PushSubscription.OptIn();
        Assert.Equal(new[] { "optOut", "optIn" }, Bridge.Calls);
    }

    [Fact]
    public void Send_event_serializes_nested_properties()
    {
        var at = new DateTimeOffset(2026, 10, 7, 12, 30, 0, TimeSpan.FromHours(3));
        HeroSignal.Session.SendEvent(" purchase ", new Dictionary<string, object?>
        {
            ["amount"] = 129.9m,
            ["count"] = 2,
            ["gift"] = false,
            ["note"] = null,
            ["at"] = at,
            ["items"] = new[] { "a", "b" },
            ["meta"] = new Dictionary<string, object?> { ["source"] = "push", ["level"] = LogLevel.Info },
        }, 129.9);

        var (name, json, value, hasValue) = Bridge.LastEvent!.Value;
        Assert.Equal("purchase", name);
        Assert.Equal(129.9, value);
        Assert.True(hasValue);
        using var doc = JsonDocument.Parse(json);
        var p = doc.RootElement;
        Assert.Equal(129.9, p.GetProperty("amount").GetDouble(), 6);
        Assert.Equal(2, p.GetProperty("count").GetInt64());
        Assert.False(p.GetProperty("gift").GetBoolean());
        Assert.Equal(JsonValueKind.Null, p.GetProperty("note").ValueKind);
        Assert.Equal("2026-10-07T09:30:00.000Z", p.GetProperty("at").GetString());
        Assert.Equal(2, p.GetProperty("items").GetArrayLength());
        Assert.Equal("push", p.GetProperty("meta").GetProperty("source").GetString());
        Assert.Equal("Info", p.GetProperty("meta").GetProperty("level").GetString());
    }

    [Fact]
    public void Send_event_without_value_and_validation()
    {
        HeroSignal.Session.SendEvent("opened_cart");
        var (_, json, _, hasValue) = Bridge.LastEvent!.Value;
        Assert.Equal("{}", json);
        Assert.False(hasValue);

        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent(""));
        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent("x", value: double.NaN));
        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent("x", new Dictionary<string, object?> { ["bad"] = double.PositiveInfinity }));
        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent("x", new Dictionary<string, object?> { ["obj"] = new object() }));
    }
}

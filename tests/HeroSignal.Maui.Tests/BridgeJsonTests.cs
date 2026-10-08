using System.Text.Json;
using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Tests;

/// <summary>iOS köprüsü JSON biçimleri (HSMauiBridge.swift ile eşleşir).</summary>
public sealed class BridgeJsonTests
{
    [Fact]
    public void Parses_notification_click_with_actions_and_data()
    {
        const string json = """
        {"notification":{"title":"Sipariş","body":"Kargoda","imageUrl":"https://img","url":"https://x","deliveryId":"dlv_1",
          "messageId":"msg_1","notificationId":"dlv_1","group":"orders",
          "actions":[{"id":"track","title":"İzle","url":"https://t","icon":null},{"title":"kimliksiz"}],
          "data":{"order":"42","vip":true,"score":1.5,"skip":null}},
         "actionId":"track","url":"https://t"}
        """;
        var click = BridgeJson.ParseNotificationClick(json);
        Assert.Equal("track", click.ActionId);
        Assert.Equal("https://t", click.Url);
        var n = click.Notification;
        Assert.Equal("Sipariş", n.Title);
        Assert.Equal("dlv_1", n.DeliveryId);
        Assert.Equal("dlv_1", n.NotificationId);
        Assert.Equal("orders", n.Group);
        var action = Assert.Single(n.Actions);
        Assert.Equal(new NotificationAction("track", "İzle", "https://t", null), action);
        Assert.Equal("42", n.Data["order"]);
        Assert.Equal("true", n.Data["vip"]);
        Assert.Equal("1.5", n.Data["score"]);
        Assert.False(n.Data.ContainsKey("skip"));
    }

    [Fact]
    public void Click_without_notification_yields_empty_notification()
    {
        var click = BridgeJson.ParseNotificationClick("""{"actionId":null}""");
        Assert.Null(click.ActionId);
        Assert.Null(click.Notification.Title);
        Assert.Empty(click.Notification.Actions);
    }

    [Fact]
    public void Parses_subscription_permission_and_foreground()
    {
        var state = BridgeJson.ParseSubscriptionState("""{"id":"sub","token":null,"optedIn":true,"permission":false}""");
        Assert.Equal(new PushSubscriptionState("sub", null, true, false), state);
        Assert.True(BridgeJson.ParsePermission("""{"permission":true}"""));
        var (eventId, notification) = BridgeJson.ParseForeground("""{"eventId":"e1","notification":{"title":"T"}}""");
        Assert.Equal("e1", eventId);
        Assert.Equal("T", notification.Title);
        Assert.Throws<HeroSignalException>(() => BridgeJson.ParseForeground("""{"notification":{}}"""));
    }

    [Theory]
    [InlineData("willDisplay", "WillDisplay")]
    [InlineData("didDisplay", "DidDisplay")]
    [InlineData("willDismiss", "WillDismiss")]
    [InlineData("didDismiss", "DidDismiss")]
    public void Parses_in_app_lifecycle(string phase, string expectedPhase)
    {
        var expected = Enum.Parse<InAppLifecyclePhase>(expectedPhase);
        var (parsed, message) = BridgeJson.ParseInAppLifecycle($$"""{"phase":"{{phase}}","messageId":"m1","json":"{\"id\":\"m1\"}"}""");
        Assert.Equal(expected, parsed);
        Assert.Equal("m1", message.Id);
        Assert.Equal("""{"id":"m1"}""", message.Json);
    }

    [Fact]
    public void In_app_parsers_require_message_id()
    {
        Assert.Throws<HeroSignalException>(() => BridgeJson.ParseInAppLifecycle("""{"phase":"bogus","messageId":"m"}"""));
        Assert.Throws<HeroSignalException>(() => BridgeJson.ParseInAppClick("""{"actionId":"a"}"""));
        var click = BridgeJson.ParseInAppClick("""{"messageId":"m","actionId":"buy","url":null}""");
        Assert.Equal("buy", click.ActionId);
        Assert.Null(click.Url);
    }

    [Fact]
    public void Parses_inbox_page_with_epoch_millis()
    {
        const string json = """
        {"items":[{"id":"i1","title":"Merhaba","read":false,"createdAt":1700000000000,"readAt":null,"data":{"k":"v"}},
                  {"id":"i2","read":true,"readAt":1700000001000}, "bozuk"],
         "unreadCount":5,"nextCursor":"c2","hasMore":true}
        """;
        var page = BridgeJson.ParseInboxPage(json);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.UnreadCount);
        Assert.Equal("c2", page.NextCursor);
        Assert.True(page.HasMore);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000), page.Items[0].CreatedAt);
        Assert.Null(page.Items[0].ReadAt);
        Assert.Equal("v", page.Items[0].Data["k"]);
        Assert.True(page.Items[1].Read);
    }

    [Fact]
    public void Inbox_unread_count_falls_back_to_items()
    {
        var page = BridgeJson.ParseInboxPage("""{"items":[{"id":"a","read":false},{"id":"b","read":true}]}""");
        Assert.Equal(1, page.UnreadCount);
        Assert.Null(page.NextCursor);
        Assert.False(page.HasMore);
    }

    [Fact]
    public void Inbox_item_requires_id_and_counts_are_parsed()
    {
        Assert.Throws<HeroSignalException>(() => BridgeJson.ParseInboxItem("""{"title":"x"}"""));
        Assert.Equal(7, BridgeJson.ParseCount("""{"count":7}"""));
        Assert.Throws<HeroSignalException>(() => BridgeJson.ParseCount("{}"));
    }

    [Fact]
    public void Invalid_json_becomes_invalid_response()
    {
        var e = Assert.Throws<HeroSignalException>(() => BridgeJson.ParseNotification("{bozuk"));
        Assert.Equal(HeroSignalException.InvalidResponse, e.Code);
        Assert.IsAssignableFrom<JsonException>(e.InnerException);
    }

    [Fact]
    public void String_maps_and_lists_round_trip()
    {
        var map = new Dictionary<string, string> { ["plan"] = "pro", ["dil"] = "tr \"ş\"" };
        Assert.Equal(map, BridgeJson.ParseStringMap(BridgeJson.ToJson(map)));
        var list = new List<string> { "a", "ğ" };
        Assert.Equal(list, BridgeJson.ParseStringList(BridgeJson.ToJson(list)));
        Assert.Empty(BridgeJson.ParseStringMap(null));
        Assert.Empty(BridgeJson.ParseStringList(""));
        Assert.Equal(new[] { "x" }, BridgeJson.ParseStringList("""["x",1,null]"""));
    }

    [Fact]
    public void Options_json_contains_ios_fields_only_when_set()
    {
        var json = BridgeJson.OptionsJson(new HeroSignalOptions { ApiUrl = "https://api.example.com", LogLevel = LogLevel.Info, RequiresPrivacyConsent = true });
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            Assert.Equal("https://api.example.com", root.GetProperty("apiUrl").GetString());
            Assert.Equal(3, root.GetProperty("logLevel").GetInt32());
            Assert.True(root.GetProperty("requiresPrivacyConsent").GetBoolean());
            Assert.False(root.GetProperty("provisionalAuthorization").GetBoolean());
            Assert.False(root.TryGetProperty("appGroup", out _));
            Assert.False(root.TryGetProperty("apnsEnvironment", out _));
        }

        json = BridgeJson.OptionsJson(new HeroSignalOptions { IosAppGroup = "group.com.example", IosApnsEnvironment = "sandbox", IosProvisionalAuthorization = true });
        using (var doc = JsonDocument.Parse(json))
        {
            Assert.Equal("group.com.example", doc.RootElement.GetProperty("appGroup").GetString());
            Assert.Equal("sandbox", doc.RootElement.GetProperty("apnsEnvironment").GetString());
            Assert.True(doc.RootElement.GetProperty("provisionalAuthorization").GetBoolean());
        }
    }
}

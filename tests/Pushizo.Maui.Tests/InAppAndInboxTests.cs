namespace PushizoSdk.Maui.Tests;

public sealed class InAppAndInboxTests : PushizoTestBase
{
    public InAppAndInboxTests() => InitializeSdk();

    [Fact]
    public void Triggers_screen_and_pause()
    {
        Pushizo.InAppMessages.AddTrigger("level", "gold");
        Pushizo.InAppMessages.RemoveTriggers(new[] { "level" });
        Pushizo.InAppMessages.ClearTriggers();
        Pushizo.InAppMessages.SetScreen(" cart ");
        Pushizo.InAppMessages.SetScreen("  ");
        Pushizo.InAppMessages.Paused = true;

        Assert.Equal(new[]
        {
            "addTriggers:{\"level\":\"gold\"}",
            "removeTriggers:[\"level\"]",
            "clearTriggers",
            "screen:cart",
            "screen:<null>",
            "paused:True",
        }, Bridge.Calls);
        Assert.True(Pushizo.InAppMessages.Paused);

        Bridge.TriggersJson = """{"level":"gold"}""";
        Assert.Equal("gold", Pushizo.InAppMessages.GetTriggers()["level"]);

        Assert.Throws<ArgumentException>(() => Pushizo.InAppMessages.AddTrigger(new string('k', 65), "v"));
        Assert.Throws<ArgumentException>(() => Pushizo.InAppMessages.AddTrigger("k", new string('v', 201)));
    }

    [Fact]
    public async Task Inbox_list_parses_page()
    {
        Bridge.InboxResponse = ("""
            {"items":[
              {"id":"in_1","messageId":"msg_1","title":"Hoş geldin","body":"Merhaba","url":null,"imageUrl":"https://cdn.example.com/a.png",
               "data":{"k":"v"},"read":false,"readAt":null,"archivedAt":null,"expiresAt":1767225600000,"createdAt":1759838400000},
              {"id":"in_2","read":true,"readAt":1759842000000}
            ],"unreadCount":1,"nextCursor":"c_2","hasMore":true}
            """, null, null);

        var page = await Pushizo.Inbox.ListAsync(limit: 20, cursor: "c_1");

        Assert.Equal("inboxList:20:c_1", Bridge.Calls[^1]);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, page.UnreadCount);
        Assert.Equal("c_2", page.NextCursor);
        Assert.True(page.HasMore);
        var first = page.Items[0];
        Assert.Equal("Hoş geldin", first.Title);
        Assert.False(first.Read);
        Assert.Equal("v", first.Data["k"]);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759838400000), first.CreatedAt);
        Assert.Null(first.ReadAt);
        Assert.True(page.Items[1].Read);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759842000000), page.Items[1].ReadAt);
    }

    [Fact]
    public async Task Inbox_operations_map_results()
    {
        Bridge.InboxResponse = ("""{"id":"in_1","read":true,"readAt":1759842000000}""", null, null);
        var item = await Pushizo.Inbox.MarkReadAsync("in_1");
        Assert.True(item.Read);

        Bridge.InboxResponse = ("""{"unreadCount":0}""", null, null);
        Assert.Equal(0, await Pushizo.Inbox.MarkAllReadAsync());

        Bridge.InboxResponse = ("""{"unreadCount":5}""", null, null);
        Assert.Equal(5, await Pushizo.Inbox.UnreadCountAsync());

        Bridge.InboxResponse = (null, null, null);
        await Pushizo.Inbox.ArchiveAsync("in_1");

        Assert.Equal(new[] { "inboxMarkRead:in_1", "inboxMarkAllRead", "inboxUnreadCount", "inboxArchive:in_1" }, Bridge.Calls);
    }

    [Fact]
    public async Task Inbox_errors_carry_native_code()
    {
        Bridge.InboxResponse = (null, "NOT_READY", "Abonelik henüz kaydedilmedi");
        var notReady = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Inbox.ListAsync());
        Assert.Equal("NOT_READY", notReady.Code);
        Assert.Equal("Abonelik henüz kaydedilmedi", notReady.Message);

        Bridge.InboxResponse = (null, "NOT_FOUND", null);
        var notFound = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Inbox.MarkReadAsync("in_x"));
        Assert.Equal("NOT_FOUND", notFound.Code);

        Bridge.InboxResponse = ("{bozuk", null, null);
        var invalid = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Inbox.UnreadCountAsync());
        Assert.Equal(PushizoException.InvalidResponse, invalid.Code);

        Bridge.InboxResponse = ("""{"items":[{"title":"kimliksiz"}]}""", null, null);
        var missingId = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Inbox.ListAsync());
        Assert.Equal(PushizoException.InvalidResponse, missingId.Code);
    }

    [Fact]
    public async Task Inbox_argument_validation()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Inbox.ListAsync(limit: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Inbox.ListAsync(limit: 201));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Inbox.MarkReadAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Inbox.ArchiveAsync(""));
        Assert.Empty(Bridge.Calls);
    }
}

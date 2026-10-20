using System.Text.Json;

namespace PushizoSdk.Maui.Tests;

public sealed class ChatTests : PushizoTestBase
{
    private const string MessageJson = """
        {"id":"cmsg_1","seq":12,"clientMessageId":null,"sender":"bot","authorName":"Asistan","text":"Seçin",
         "attachments":[{"id":"att_1","name":"a.png","type":"image/png","size":2048}],
         "quickReplies":[{"id":"o1","label":"Satış"}],"status":"delivered","createdAt":1792490400000}
        """;

    public ChatTests() => InitializeSdk();

    private (string Method, JsonElement Args) LastCall()
    {
        var (method, args) = Bridge.ChatCalls[^1];
        return (method, JsonDocument.Parse(args).RootElement);
    }

    [Fact]
    public async Task Start_config_and_conversation_are_parsed()
    {
        Bridge.ChatResponses["start"] = ("""{"userId":"usr_1","conversation":{"id":"conv_1","status":"open","lastSeq":12,"agentLastReadSeq":10,"visitorLastReadSeq":11,"unreadCount":1,"botActive":true,"assigneeName":null,"createdAt":1792490340000}}""", null, null);
        Bridge.ChatResponses["getConfig"] = ("""{"enabled":true,"online":false,"offlineMessage":"Şu an çevrimdışıyız","attachmentsEnabled":true,"attachmentMaxBytes":10485760,"attachmentTypes":["image/png"],"language":"tr"}""", null, null);
        Bridge.ChatResponses["getConversation"] = ("""{"conversation":null,"unreadCount":0}""", null, null);

        var started = await Pushizo.Chat.StartAsync(name: "Ali");
        var (method, args) = LastCall();
        Assert.Equal("start", method);
        Assert.Equal("Ali", args.GetProperty("name").GetString());
        Assert.False(args.TryGetProperty("email", out _));
        Assert.Equal("usr_1", started.UserId);
        Assert.True(started.Conversation!.BotActive);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792490340000), started.Conversation.CreatedAt);

        var config = await Pushizo.Chat.GetConfigAsync("tr");
        Assert.False(config.Online);
        Assert.Equal("Şu an çevrimdışıyız", config.OfflineMessage);
        Assert.Equal(10485760, config.AttachmentMaxBytes);
        Assert.Equal(new[] { "image/png" }, config.AttachmentTypes);

        var state = await Pushizo.Chat.GetConversationAsync();
        Assert.Null(state.Conversation);
        Assert.Equal(0, state.UnreadCount);
    }

    [Fact]
    public async Task Messages_send_read_and_offline_calls()
    {
        Bridge.ChatResponses["getMessages"] = ($$"""{"messages":[{{MessageJson}},{"id":"cmsg_0","seq":11,"sender":"visitor","status":"read"}],"hasMore":true}""", null, null);
        Bridge.ChatResponses["sendMessage"] = (MessageJson, null, null);
        Bridge.ChatResponses["sendAttachment"] = (MessageJson, null, null);
        Bridge.ChatResponses["leaveOfflineMessage"] = (MessageJson, null, null);
        Bridge.ChatResponses["markRead"] = ("""{"unreadCount":0}""", null, null);
        Bridge.ChatResponses["attachmentUrl"] = ("""{"url":"https://api.example.com/sdk/v1/apps/app_test/chat/attachments/att_1?visitor_id=v&token=t"}""", null, null);

        var page = await Pushizo.Chat.GetMessagesAsync(afterSeq: 5);
        Assert.Equal(5, LastCall().Args.GetProperty("afterSeq").GetInt64());
        Assert.Equal(50, LastCall().Args.GetProperty("limit").GetInt32());
        Assert.Equal(new long[] { 11, 12 }, page.Messages.Select(m => m.Seq));
        Assert.True(page.HasMore);
        var bot = page.Messages[1];
        Assert.Equal("Satış", bot.QuickReplies![0].Label);
        Assert.Equal(2048, bot.Attachments[0].Size);
        Assert.True(page.Messages[0].IsFromVisitor);

        await Pushizo.Chat.SendMessageAsync("Merhaba", optionId: "o1");
        Assert.Equal("sendMessage", LastCall().Method);
        Assert.Equal("o1", LastCall().Args.GetProperty("optionId").GetString());

        await Pushizo.Chat.SendAttachmentAsync(new byte[] { 1, 2, 3 }, "a.png", "image/png");
        Assert.Equal("AQID", LastCall().Args.GetProperty("base64").GetString());

        await Pushizo.Chat.LeaveOfflineMessageAsync(" ali@example.com ", "Arayın");
        Assert.Equal("ali@example.com", LastCall().Args.GetProperty("email").GetString());

        Assert.Equal(0, await Pushizo.Chat.MarkReadAsync(12));
        Assert.Equal(12, LastCall().Args.GetProperty("seq").GetInt64());

        Assert.Contains("/chat/attachments/att_1", await Pushizo.Chat.GetAttachmentUrlAsync("att_1"));

        await Pushizo.Chat.SetTypingAsync(true);
        await Pushizo.Chat.StartListeningAsync();
        await Pushizo.Chat.StopListeningAsync();
        Assert.Equal(new[] { "setTyping", "startListening", "stopListening" }, Bridge.ChatCalls.TakeLast(3).Select(c => c.Method));
    }

    [Fact]
    public async Task Invalid_arguments_are_rejected_before_bridge_and_errors_carry_code()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Chat.GetMessagesAsync(limit: 101));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Chat.GetMessagesAsync(beforeSeq: 0));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Chat.SendMessageAsync("   "));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Chat.SendMessageAsync(new string('a', 4001)));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Chat.SendAttachmentAsync(new byte[] { 1 }, "a.exe", "application/x-msdownload"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Chat.MarkReadAsync(-1));
        Assert.Empty(Bridge.ChatCalls);

        Bridge.ChatResponses["sendMessage"] = (null, "HS_PLAN_FEATURE_REQUIRED", "Plan canlı sohbet içermiyor");
        var error = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Chat.SendMessageAsync("Merhaba"));
        Assert.Equal("HS_PLAN_FEATURE_REQUIRED", error.Code);
    }

    [Fact]
    public void Native_chat_events_are_dispatched_and_open_is_buffered_until_subscribed()
    {
        var events = new List<ChatEventArgs>();
        Pushizo.Chat.EventReceived += (_, e) => events.Add(e);

        Bridge.Emit("chat", $$"""{"type":"message","message":{{MessageJson}}}""");
        Bridge.Emit("chat", """{"type":"typing","typing":true,"name":"Ayşe"}""");
        Bridge.Emit("chat", """{"type":"read","seq":7}""");
        Bridge.Emit("chat", """{"type":"unread","count":2}""");
        Bridge.Emit("chat", """{"type":"conversation","conversationId":"conv_1","status":"resolved","botActive":false}""");
        Bridge.Emit("chat", """{"type":"error","code":"HS_PLAN_FEATURE_REQUIRED","message":null}""");
        Bridge.Emit("chat", """{"type":"gelecekteki_tur"}""");

        Assert.Equal(
            new[] { ChatEventType.Message, ChatEventType.Typing, ChatEventType.AgentRead, ChatEventType.UnreadCount, ChatEventType.Conversation, ChatEventType.Error },
            events.Select(e => e.Type));
        Assert.Equal("Seçin", events[0].Message!.Text);
        Assert.Equal("Ayşe", events[1].Name);
        Assert.Equal(7, events[2].Seq);
        Assert.Equal(2, events[3].Count);
        Assert.Equal("resolved", events[4].Status);
        Assert.Equal("HS_PLAN_FEATURE_REQUIRED", events[5].Code);

        Bridge.Emit("chatOpen", """{"conversationId":"conv_9","seq":4}""");
        var opened = new List<ChatOpenedEventArgs>();
        Pushizo.Chat.Opened += (_, e) => opened.Add(e);
        Assert.Single(opened);
        Assert.Equal("conv_9", opened[0].ConversationId);
        Bridge.Emit("chatOpen", """{"conversationId":"conv_10","seq":5}""");
        Assert.Equal(2, opened.Count);
    }
}

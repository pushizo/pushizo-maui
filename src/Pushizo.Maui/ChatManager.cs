using System.Text.Json;
using System.Text.Json.Nodes;
using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui;

/// <summary>Sohbet mesajı eki (<see cref="ChatManager.GetAttachmentUrlAsync"/> ile görüntülenir).</summary>
public sealed record ChatAttachment(string Id, string Name, string Type, long Size);

/// <summary>Bot sorusunun hızlı yanıt seçeneği; seçim <see cref="ChatManager.SendMessageAsync"/> <c>optionId</c> ile gönderilir.</summary>
public sealed record ChatQuickReply(string Id, string Label);

/// <summary>
/// Sohbet mesajı (ADR-0040). <see cref="Seq"/> konuşma içi sunucu sıra numarasıdır; çevrimdışı kuyrukta bekleyen yerel
/// mesajda 0 ve <see cref="Status"/> <c>pending</c> olur.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>Sunucu kimliği; bekleyen yerel mesajda null.</summary>
    public string? Id { get; init; }
    public long Seq { get; init; }
    public string? ClientMessageId { get; init; }
    /// <summary><c>visitor</c>, <c>agent</c>, <c>bot</c> veya <c>system</c>.</summary>
    public string Sender { get; init; } = "system";
    public string? AuthorName { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<ChatAttachment> Attachments { get; init; } = Array.Empty<ChatAttachment>();
    /// <summary>Bot sorusunun seçenekleri (yoksa null).</summary>
    public IReadOnlyList<ChatQuickReply>? QuickReplies { get; init; }
    /// <summary><c>pending</c> (yerel), <c>sent</c>, <c>delivered</c> veya <c>read</c>.</summary>
    public string Status { get; init; } = "sent";
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Ziyaretçinin (bu cihazın) mesajı mı.</summary>
    public bool IsFromVisitor => Sender == "visitor";
}

/// <summary>Ziyaretçinin konuşması (<c>open</c>, <c>pending</c>, <c>resolved</c>).</summary>
public sealed record ChatConversation
{
    public required string Id { get; init; }
    public string Status { get; init; } = "open";
    public long LastSeq { get; init; }
    /// <summary>Temsilcinin okuduğu son sıra (ziyaretçi mesajlarında "okundu").</summary>
    public long AgentLastReadSeq { get; init; }
    public long VisitorLastReadSeq { get; init; }
    public int UnreadCount { get; init; }
    public bool BotActive { get; init; }
    public string? AssigneeName { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>Konuşma (henüz yoksa null) ve okunmamış sayısı.</summary>
public sealed record ChatConversationState(ChatConversation? Conversation, int UnreadCount);

/// <summary>Mesaj sayfası (kronolojik).</summary>
public sealed record ChatMessagePage(IReadOnlyList<ChatMessage> Messages, bool HasMore);

/// <summary><see cref="ChatManager.StartAsync"/> sonucu.</summary>
public sealed record ChatIdentifyResult(string? UserId, ChatConversation? Conversation);

/// <summary>Sohbet widget ayarları ve çevrimiçi durumu.</summary>
public sealed record ChatConfig
{
    public bool Enabled { get; init; }
    /// <summary>Çalışma saatleri içinde ve en az bir temsilci (veya bot) çevrimiçi.</summary>
    public bool Online { get; init; }
    public bool WithinBusinessHours { get; init; }
    public string? Color { get; init; }
    public string? TextColor { get; init; }
    public string Position { get; init; } = "right";
    public string Theme { get; init; } = "auto";
    public string? Title { get; init; }
    public string? Greeting { get; init; }
    /// <summary>Çevrimdışıyken gösterilecek metin; çevrimdışı mesaj için <see cref="ChatManager.LeaveOfflineMessageAsync"/>.</summary>
    public string? OfflineMessage { get; init; }
    public bool CollectEmailOffline { get; init; }
    public bool RequireEmail { get; init; }
    public bool AttachmentsEnabled { get; init; }
    public long AttachmentMaxBytes { get; init; }
    public IReadOnlyList<string> AttachmentTypes { get; init; } = Array.Empty<string>();
    public string Language { get; init; } = "en";
}

/// <summary>Canlı sohbet olay türü.</summary>
public enum ChatEventType
{
    /// <summary>Yeni mesaj (<see cref="ChatEventArgs.Message"/>).</summary>
    Message,
    /// <summary>Temsilci yazıyor (<see cref="ChatEventArgs.Typing"/>, <see cref="ChatEventArgs.Name"/>).</summary>
    Typing,
    /// <summary>Temsilci bu sıraya kadar okudu (<see cref="ChatEventArgs.Seq"/>).</summary>
    AgentRead,
    /// <summary>Okunmamış sayısı değişti (<see cref="ChatEventArgs.Count"/>).</summary>
    UnreadCount,
    /// <summary>Konuşma durumu değişti (<see cref="ChatEventArgs.ConversationId"/>, <see cref="ChatEventArgs.Status"/>, <see cref="ChatEventArgs.BotActive"/>).</summary>
    Conversation,
    /// <summary>Canlı akış bağlandı/koptu (<see cref="ChatEventArgs.Connected"/>).</summary>
    Connection,
    /// <summary>Kalıcı hata; dinleme durdu (<see cref="ChatEventArgs.Code"/>, <see cref="ChatEventArgs.ErrorMessage"/>).</summary>
    Error,
}

/// <summary>Canlı sohbet olayı; türe göre ilgili alanlar doludur.</summary>
public sealed class ChatEventArgs : EventArgs
{
    internal ChatEventArgs(ChatEventType type) => Type = type;

    public ChatEventType Type { get; }
    public ChatMessage? Message { get; internal init; }
    public bool Typing { get; internal init; }
    public string? Name { get; internal init; }
    public long Seq { get; internal init; }
    public int Count { get; internal init; }
    public string? ConversationId { get; internal init; }
    public string? Status { get; internal init; }
    public bool BotActive { get; internal init; }
    public bool Connected { get; internal init; }
    public string? Code { get; internal init; }
    public string? ErrorMessage { get; internal init; }
}

/// <summary>Sohbet bildirimine tıklandı: uygulama sohbet ekranını açmalı.</summary>
public sealed class ChatOpenedEventArgs : EventArgs
{
    internal ChatOpenedEventArgs(string conversationId, long seq)
    {
        ConversationId = conversationId;
        Seq = seq;
    }

    public string ConversationId { get; }
    /// <summary>Bildirimi tetikleyen temsilci mesajının sıra numarası.</summary>
    public long Seq { get; }
}

/// <summary>
/// Canlı sohbet (<c>Pushizo.Chat</c>; ADR-0040, Android ve iOS). Ziyaretçi kimliği, çevrimdışı kuyruk, canlı akış (SSE +
/// yoklama yedeği) ve push ile gelen mesajlar yerel SDK'dadır; login/logout ve push aboneliği değişince bağlama yerel
/// SDK'da yenilenir. Olaylar yerel SDK'nın iş parçacığında (Android'de ana iş parçacığı) çağrılır.
/// </summary>
public sealed class ChatManager
{
    private static readonly string[] s_attachmentTypes = { "image/png", "image/jpeg", "image/webp", "image/gif", "application/pdf", "text/plain" };
    private const long MaxAttachmentBytes = 10L * 1024 * 1024;
    private const int MaxText = 4000;

    private readonly object _gate = new();
    private readonly List<ChatOpenedEventArgs> _pendingOpens = new();
    private EventHandler<ChatOpenedEventArgs>? _opened;

    internal ChatManager()
    {
    }

    /// <summary>Canlı sohbet olayları (mesaj, yazıyor, okundu, okunmamış, konuşma, bağlantı, hata).</summary>
    public event EventHandler<ChatEventArgs>? EventReceived;

    /// <summary>Sohbet bildirimine tıklama; dinleyici yokken (soğuk başlangıç) gelen tıklamalar ilk abonede iletilir.</summary>
    public event EventHandler<ChatOpenedEventArgs>? Opened
    {
        add
        {
            ChatOpenedEventArgs[] pending;
            lock (_gate)
            {
                _opened += value;
                pending = _pendingOpens.ToArray();
                _pendingOpens.Clear();
            }
            foreach (var args in pending) Pushizo.Raise(value, args, "Chat.Opened");
        }
        remove
        {
            lock (_gate) _opened -= value;
        }
    }

    /// <summary>Sohbeti etkinleştirir ve ziyaretçiyi bağlar (giriş yapmış kullanıcı, push aboneliği); konuşma varsa döner.</summary>
    public async Task<ChatIdentifyResult> StartAsync(string? name = null, string? email = null, string? language = null)
    {
        var args = new JsonObject();
        if (!string.IsNullOrWhiteSpace(name)) args["name"] = name;
        if (!string.IsNullOrWhiteSpace(email)) args["email"] = email;
        if (!string.IsNullOrWhiteSpace(language)) args["language"] = language;
        return ChatJson.ParseIdentify(Require(await Call("start", args).ConfigureAwait(false)));
    }

    /// <summary>Widget ayarları, çevrimiçi durumu, çevrimdışı metni ve ek sınırları.</summary>
    public async Task<ChatConfig> GetConfigAsync(string? language = null)
    {
        var args = new JsonObject();
        if (!string.IsNullOrWhiteSpace(language)) args["language"] = language;
        return ChatJson.ParseConfig(Require(await Call("getConfig", args).ConfigureAwait(false)));
    }

    /// <summary>Konuşma (yoksa null) ve okunmamış sayısı.</summary>
    public async Task<ChatConversationState> GetConversationAsync() =>
        ChatJson.ParseConversationState(Require(await Call("getConversation", new JsonObject()).ConfigureAwait(false)));

    /// <summary>
    /// Mesajlar (kronolojik): parametresiz en yeni <paramref name="limit"/>; <paramref name="afterSeq"/> sonrakiler,
    /// <paramref name="beforeSeq"/> daha eski geçmiş (1-100).
    /// </summary>
    public async Task<ChatMessagePage> GetMessagesAsync(long? afterSeq = null, long? beforeSeq = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "limit 1-100 olmalı.");
        if (afterSeq is < 0) throw new ArgumentOutOfRangeException(nameof(afterSeq), "afterSeq negatif olamaz.");
        if (beforeSeq is < 1) throw new ArgumentOutOfRangeException(nameof(beforeSeq), "beforeSeq en az 1 olmalı.");
        var args = new JsonObject { ["limit"] = limit };
        if (afterSeq is { } after) args["afterSeq"] = after;
        if (beforeSeq is { } before) args["beforeSeq"] = before;
        return ChatJson.ParsePage(Require(await Call("getMessages", args).ConfigureAwait(false)));
    }

    /// <summary>
    /// Mesaj gönderir (ilk mesaj konuşmayı açar); <paramref name="optionId"/> bot sorusunda seçilen hızlı yanıttır. Ağ
    /// yoksa mesaj yerel kuyruğa alınır ve <c>pending</c> mesaj döner; gönderilince <see cref="EventReceived"/> ile
    /// sunucu kopyası gelir.
    /// </summary>
    public async Task<ChatMessage> SendMessageAsync(string text, string? optionId = null)
    {
        var args = new JsonObject { ["text"] = RequireText(text, nameof(text)) };
        if (!string.IsNullOrWhiteSpace(optionId)) args["optionId"] = optionId;
        return ChatJson.ParseMessage(Require(await Call("sendMessage", args).ConfigureAwait(false)));
    }

    /// <summary>Ek gönderir (PNG/JPEG/WebP/GIF/PDF/düz metin, en çok 10 MB).</summary>
    public async Task<ChatMessage> SendAttachmentAsync(byte[] data, string fileName, string mimeType, string? text = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) throw new ArgumentException("Dosya boş olamaz.", nameof(data));
        if (data.LongLength > MaxAttachmentBytes) throw new ArgumentException("Dosya 10 MB sınırını aşıyor.", nameof(data));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName gerekli.", nameof(fileName));
        var mime = (mimeType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (Array.IndexOf(s_attachmentTypes, mime) < 0) throw new ArgumentException($"Desteklenmeyen dosya türü: {mime}.", nameof(mimeType));
        var args = new JsonObject { ["base64"] = Convert.ToBase64String(data), ["fileName"] = fileName, ["mimeType"] = mime };
        if (!string.IsNullOrWhiteSpace(text)) args["text"] = RequireText(text, nameof(text));
        return ChatJson.ParseMessage(Require(await Call("sendAttachment", args).ConfigureAwait(false)));
    }

    /// <summary><paramref name="seq"/> numarasına kadar okundu işaretler; kalan okunmamış sayısını döndürür.</summary>
    public async Task<int> MarkReadAsync(long seq)
    {
        if (seq < 0) throw new ArgumentOutOfRangeException(nameof(seq), "seq negatif olamaz.");
        return ChatJson.ParseUnread(Require(await Call("markRead", new JsonObject { ["seq"] = seq }).ConfigureAwait(false)));
    }

    /// <summary>Okunmamış sayısını sunucudan yeniler.</summary>
    public async Task<int> RefreshUnreadCountAsync() =>
        ChatJson.ParseUnread(Require(await Call("refreshUnreadCount", new JsonObject()).ConfigureAwait(false)));

    /// <summary>Son bilinen okunmamış sayısı (ağ çağrısı yok).</summary>
    public async Task<int> GetUnreadCountAsync() =>
        ChatJson.ParseUnread(Require(await Call("getUnreadCount", new JsonObject()).ConfigureAwait(false)));

    /// <summary>Çevrimdışı / çalışma saatleri dışında e-postayla mesaj bırakır.</summary>
    public async Task<ChatMessage> LeaveOfflineMessageAsync(string email, string text, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("email gerekli.", nameof(email));
        var args = new JsonObject { ["email"] = email.Trim(), ["text"] = RequireText(text, nameof(text)) };
        if (!string.IsNullOrWhiteSpace(name)) args["name"] = name;
        return ChatJson.ParseMessage(Require(await Call("leaveOfflineMessage", args).ConfigureAwait(false)));
    }

    /// <summary>Yazıyor göstergesi (yerel SDK tekrarları seyreltir).</summary>
    public Task SetTypingAsync(bool typing) => Call("setTyping", new JsonObject { ["typing"] = typing });

    /// <summary>Canlı dinlemeyi başlatır; koparsa yerel SDK geri çekilmeyle yeniden bağlanır ve eksikleri yoklar.</summary>
    public Task StartListeningAsync() => Call("startListening", new JsonObject());

    /// <summary>Canlı dinlemeyi durdurur (arka plana geçişte önerilir; temsilci mesajı push ile gelir).</summary>
    public Task StopListeningAsync() => Call("stopListening", new JsonObject());

    /// <summary>Ek dosyasının adresi; ziyaretçi belirtecini taşır, yalnızca görsel bileşenine verin ve loglamayın.</summary>
    public async Task<string> GetAttachmentUrlAsync(string attachmentId, bool download = false)
    {
        if (string.IsNullOrWhiteSpace(attachmentId)) throw new ArgumentException("attachmentId gerekli.", nameof(attachmentId));
        var json = Require(await Call("attachmentUrl", new JsonObject { ["attachmentId"] = attachmentId, ["download"] = download }).ConfigureAwait(false));
        return ChatJson.ParseString(json, "url") ?? throw new PushizoException(PushizoException.InvalidResponse, "Ek adresi alınamadı.");
    }

    /// <summary>Bu cihazın ziyaretçi kimliği (sohbet henüz kullanılmadıysa null).</summary>
    public async Task<string?> GetVisitorIdAsync() =>
        ChatJson.ParseString(Require(await Call("getVisitorId", new JsonObject()).ConfigureAwait(false)), "visitorId");

    private static Task<string?> Call(string method, JsonObject args) => Pushizo.Platform().ChatCallAsync(method, args.ToJsonString());

    private static string Require(string? json) =>
        json ?? throw new PushizoException(PushizoException.InvalidResponse, "Köprü yanıtı boş.");

    private static string RequireText(string? text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Mesaj metni boş olamaz.", name);
        if (text.Trim().Length > MaxText) throw new ArgumentException($"Mesaj en fazla {MaxText} karakter olabilir.", name);
        return text;
    }

    internal void Dispatch(ChatEventArgs args) => Pushizo.Raise(EventReceived, args, "Chat.EventReceived");

    internal void DispatchOpened(ChatOpenedEventArgs args)
    {
        EventHandler<ChatOpenedEventArgs>? handler;
        lock (_gate)
        {
            handler = _opened;
            if (handler is null)
            {
                if (_pendingOpens.Count >= 20) _pendingOpens.RemoveAt(0);
                _pendingOpens.Add(args);
                return;
            }
        }
        Pushizo.Raise(handler, args, "Chat.Opened");
    }

    internal void Reset()
    {
        lock (_gate)
        {
            EventReceived = null;
            _opened = null;
            _pendingOpens.Clear();
        }
    }
}

/// <summary>Yerel köprünün sohbet JSON biçimleri (Android <c>toMap()</c> / iOS <c>dictionary</c>; zamanlar epoch ms).</summary>
internal static class ChatJson
{
    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new PushizoException(PushizoException.InvalidResponse, "Köprü yanıtı ayrıştırılamadı.", e);
        }
    }

    public static ChatMessage ParseMessage(string json)
    {
        using var doc = Parse(json);
        return ReadMessage(doc.RootElement);
    }

    public static ChatMessagePage ParsePage(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        var messages = new List<ChatMessage>();
        if (root.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var m in arr.EnumerateArray())
                if (m.ValueKind == JsonValueKind.Object) messages.Add(ReadMessage(m));
        messages.Sort((a, b) => a.Seq.CompareTo(b.Seq));
        return new ChatMessagePage(messages, Bool(root, "hasMore"));
    }

    public static ChatConversationState ParseConversationState(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        return new ChatConversationState(ReadConversation(root, "conversation"), (int)Long(root, "unreadCount"));
    }

    public static ChatIdentifyResult ParseIdentify(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        return new ChatIdentifyResult(Str(root, "userId"), ReadConversation(root, "conversation"));
    }

    public static ChatConfig ParseConfig(string json)
    {
        using var doc = Parse(json);
        var e = doc.RootElement;
        var types = new List<string>();
        if (e.TryGetProperty("attachmentTypes", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var t in arr.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String) types.Add(t.GetString()!);
        return new ChatConfig
        {
            Enabled = Bool(e, "enabled"),
            Online = Bool(e, "online"),
            WithinBusinessHours = Bool(e, "withinBusinessHours"),
            Color = Str(e, "color"),
            TextColor = Str(e, "textColor"),
            Position = Str(e, "position") ?? "right",
            Theme = Str(e, "theme") ?? "auto",
            Title = Str(e, "title"),
            Greeting = Str(e, "greeting"),
            OfflineMessage = Str(e, "offlineMessage"),
            CollectEmailOffline = Bool(e, "collectEmailOffline"),
            RequireEmail = Bool(e, "requireEmail"),
            AttachmentsEnabled = Bool(e, "attachmentsEnabled"),
            AttachmentMaxBytes = Long(e, "attachmentMaxBytes"),
            AttachmentTypes = types,
            Language = Str(e, "language") ?? "en",
        };
    }

    public static int ParseUnread(string json)
    {
        using var doc = Parse(json);
        return (int)Long(doc.RootElement, "unreadCount");
    }

    public static string? ParseString(string json, string name)
    {
        using var doc = Parse(json);
        return Str(doc.RootElement, name);
    }

    /// <summary>
    /// <c>chat</c> olayı: <c>{"type":"message|typing|read|unread|conversation|connection|error",…}</c>. Bilinmeyen tür
    /// (daha yeni yerel köprü) null döner.
    /// </summary>
    public static ChatEventArgs? ParseEvent(string json)
    {
        using var doc = Parse(json);
        var e = doc.RootElement;
        return Str(e, "type") switch
        {
            "message" when e.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object =>
                new ChatEventArgs(ChatEventType.Message) { Message = ReadMessage(m) },
            "typing" => new ChatEventArgs(ChatEventType.Typing) { Typing = Bool(e, "typing"), Name = Str(e, "name") },
            "read" => new ChatEventArgs(ChatEventType.AgentRead) { Seq = Long(e, "seq") },
            "unread" => new ChatEventArgs(ChatEventType.UnreadCount) { Count = (int)Long(e, "count") },
            "conversation" => new ChatEventArgs(ChatEventType.Conversation)
            {
                ConversationId = Str(e, "conversationId"),
                Status = Str(e, "status") ?? "open",
                BotActive = Bool(e, "botActive"),
            },
            "connection" => new ChatEventArgs(ChatEventType.Connection) { Connected = Bool(e, "connected") },
            "error" => new ChatEventArgs(ChatEventType.Error) { Code = Str(e, "code") ?? "HS_ERROR", ErrorMessage = Str(e, "message") },
            _ => null,
        };
    }

    /// <summary><c>chatOpen</c> olayı: <c>{"conversationId","seq"}</c>.</summary>
    public static ChatOpenedEventArgs ParseOpened(string json)
    {
        using var doc = Parse(json);
        var e = doc.RootElement;
        return new ChatOpenedEventArgs(Str(e, "conversationId") ?? string.Empty, Long(e, "seq"));
    }

    private static ChatMessage ReadMessage(JsonElement e)
    {
        var attachments = new List<ChatAttachment>();
        if (e.TryGetProperty("attachments", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var a in arr.EnumerateArray())
                if (a.ValueKind == JsonValueKind.Object)
                    attachments.Add(new ChatAttachment(Str(a, "id") ?? string.Empty, Str(a, "name") ?? string.Empty, Str(a, "type") ?? string.Empty, Long(a, "size")));
        List<ChatQuickReply>? replies = null;
        if (e.TryGetProperty("quickReplies", out var qr) && qr.ValueKind == JsonValueKind.Array)
        {
            replies = new List<ChatQuickReply>();
            foreach (var q in qr.EnumerateArray())
                if (q.ValueKind == JsonValueKind.Object) replies.Add(new ChatQuickReply(Str(q, "id") ?? string.Empty, Str(q, "label") ?? string.Empty));
        }
        return new ChatMessage
        {
            Id = Str(e, "id"),
            Seq = Long(e, "seq"),
            ClientMessageId = Str(e, "clientMessageId"),
            Sender = Str(e, "sender") ?? "system",
            AuthorName = Str(e, "authorName"),
            Text = Str(e, "text"),
            Attachments = attachments,
            QuickReplies = replies,
            Status = Str(e, "status") ?? "sent",
            CreatedAt = Millis(e, "createdAt"),
        };
    }

    private static ChatConversation? ReadConversation(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var c) || c.ValueKind != JsonValueKind.Object) return null;
        var id = Str(c, "id");
        if (id is null) return null;
        return new ChatConversation
        {
            Id = id,
            Status = Str(c, "status") ?? "open",
            LastSeq = Long(c, "lastSeq"),
            AgentLastReadSeq = Long(c, "agentLastReadSeq"),
            VisitorLastReadSeq = Long(c, "visitorLastReadSeq"),
            UnreadCount = (int)Long(c, "unreadCount"),
            BotActive = Bool(c, "botActive"),
            AssigneeName = Str(c, "assigneeName"),
            CreatedAt = Millis(c, "createdAt"),
        };
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static long Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n
        : e.TryGetProperty(name, out var d) && d.ValueKind == JsonValueKind.Number ? (long)d.GetDouble() : 0L;

    private static DateTimeOffset? Millis(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? DateTimeOffset.FromUnixTimeMilliseconds((long)v.GetDouble()) : null;
}

using System.Text.Json;

namespace HeroSignalSdk.Maui.Internal;

/// <summary>
/// Yerel köprülerin (Android <c>native/android/bridge/…/HeroSignalMauiBridge.kt</c>, iOS
/// <c>HeroSignal.Maui.iOS.Binding/native/Sources/HeroSignalMauiBridge/HSMauiBridge.swift</c>) döndürdüğü ortak JSON
/// biçimlerinin ayrıştırıcısı ve yazıcısı; zamanlar epoch milisaniyedir.
/// </summary>
internal static class BridgeJson
{
    public static HeroSignalNotification ParseNotification(string json)
    {
        using var doc = Parse(json);
        return ReadNotification(doc.RootElement);
    }

    public static NotificationClickedEventArgs ParseNotificationClick(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        var notification = root.TryGetProperty("notification", out var n) && n.ValueKind == JsonValueKind.Object
            ? ReadNotification(n)
            : new HeroSignalNotification();
        return new NotificationClickedEventArgs(notification, Str(root, "actionId"), Str(root, "url"));
    }

    public static PushSubscriptionState ParseSubscriptionState(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        return new PushSubscriptionState(Str(root, "id"), Str(root, "token"), Bool(root, "optedIn"), Bool(root, "permission"));
    }

    /// <summary><c>{"permission":bool}</c> (yerel izin gözlemcisi olayı).</summary>
    public static bool ParsePermission(string json)
    {
        using var doc = Parse(json);
        return Bool(doc.RootElement, "permission");
    }

    public static InAppMessageClickedEventArgs ParseInAppClick(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        return new InAppMessageClickedEventArgs(
            Str(root, "messageId") ?? throw Invalid("messageId"),
            Str(root, "actionId") ?? string.Empty,
            Str(root, "url"));
    }

    public static InAppMessage ParseInAppMessage(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        return new InAppMessage(Str(root, "messageId") ?? throw Invalid("messageId"), Str(root, "json"));
    }

    public static InboxPage ParseInboxPage(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        var items = new List<InboxItem>();
        if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in arr.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object) items.Add(ReadInboxItem(element));
            }
        }
        var unread = root.TryGetProperty("unreadCount", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetInt32()
            : items.Count(i => !i.Read);
        return new InboxPage(items, unread, Str(root, "nextCursor"), Bool(root, "hasMore"));
    }

    public static InboxItem ParseInboxItem(string json)
    {
        using var doc = Parse(json);
        return ReadInboxItem(doc.RootElement);
    }

    /// <summary><c>{"key":"value"}</c> biçimindeki metin sözlüğü (tag/trigger).</summary>
    public static IReadOnlyDictionary<string, string> ParseStringMap(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
        using var doc = Parse(json);
        return ReadStringMap(doc.RootElement);
    }

    /// <summary><c>{"eventId":"…","notification":{…}}</c>.</summary>
    public static (string EventId, HeroSignalNotification Notification) ParseForeground(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        var eventId = Str(root, "eventId") ?? throw Invalid("eventId");
        var notification = root.TryGetProperty("notification", out var n) && n.ValueKind == JsonValueKind.Object
            ? ReadNotification(n)
            : new HeroSignalNotification();
        return (eventId, notification);
    }

    /// <summary><c>["a","b"]</c> biçimindeki metin listesi (e-posta/SMS).</summary>
    public static IReadOnlyList<string> ParseStringList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<string>();
        using var doc = Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.String) list.Add(e.GetString()!);
        }
        return list;
    }

    /// <summary><c>{"unreadCount":n}</c>.</summary>
    public static int ParseUnreadCount(string json)
    {
        using var doc = Parse(json);
        return doc.RootElement.TryGetProperty("unreadCount", out var u) && u.ValueKind == JsonValueKind.Number && u.TryGetInt32(out var n)
            ? n
            : throw Invalid("unreadCount");
    }

    /// <summary>
    /// <c>{"preferences":[…],"categories":[…]}</c> (<see cref="INativeBridge.GetPreferences"/>). Kategorisiz tercih
    /// <see cref="HeroSignalException.InvalidResponse"/> verir; anahtarsız kategori atlanır (yerel SDK'larla aynı).
    /// </summary>
    public static UserPreferences ParsePreferences(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Invalid("preferences");
        var preferences = new List<UserPreference>();
        if (root.TryGetProperty("preferences", out var prefs) && prefs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in prefs.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                preferences.Add(new UserPreference
                {
                    Category = Str(e, "category") ?? throw Invalid("category"),
                    Channel = Str(e, "channel"),
                    OptedIn = Bool(e, "optedIn"),
                    Explicit = Bool(e, "explicit"),
                    Source = Str(e, "source"),
                    UpdatedAt = Millis(e, "updatedAt"),
                });
            }
        }
        var categories = new List<PreferenceCategory>();
        if (root.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in cats.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var key = Str(e, "key");
                if (string.IsNullOrEmpty(key)) continue;
                categories.Add(new PreferenceCategory
                {
                    Key = key,
                    Name = e.TryGetProperty("name", out var name) ? ReadStringMap(name) : EmptyStringMap.Instance,
                    Description = e.TryGetProperty("description", out var description) ? ReadStringMap(description) : EmptyStringMap.Instance,
                    Channels = e.TryGetProperty("channels", out var channels) && channels.ValueKind == JsonValueKind.Array
                        ? channels.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()!).Where(c => c.Length > 0).ToList()
                        : null,
                    // Alan yoksa varsayılan açık (yerel SDK'lardaki `default_opted_in` varsayılanı).
                    DefaultOptedIn = !(e.TryGetProperty("defaultOptedIn", out var d) && d.ValueKind == JsonValueKind.False),
                });
            }
        }
        return new UserPreferences(preferences, categories);
    }

    /// <summary>Tercih girdileri → <c>[{"category","channel","optedIn"}]</c> (kanal yoksa JSON null).</summary>
    public static string PreferencesToJson(IReadOnlyList<PreferenceInput> preferences)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartArray();
            foreach (var p in preferences)
            {
                w.WriteStartObject();
                w.WriteString("category", p.Category);
                if (p.Channel is null) w.WriteNull("channel");
                else w.WriteString("channel", p.Channel);
                w.WriteBoolean("optedIn", p.OptedIn);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Yerel köprünün başlatma seçenekleri (<c>HeroSignalMauiBridge.start</c> / <c>HSMauiBridge.start</c>).</summary>
    public static string OptionsToJson(HeroSignalOptions options)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("apiUrl", options.ApiUrl);
            w.WriteBoolean("requiresPrivacyConsent", options.RequiresPrivacyConsent);
            w.WriteNumber("logLevel", (int)options.LogLevel);
            w.WriteString("androidDefaultChannelName", options.AndroidDefaultChannelName);
            w.WriteString("androidDefaultChannelId", options.AndroidDefaultChannelId);
            WriteOptional(w, "iosAppGroup", options.IosAppGroup);
            WriteOptional(w, "iosApnsEnvironment", options.IosApnsEnvironment);
            w.WriteBoolean("iosProvisionalAuthorization", options.IosProvisionalAuthorization);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteOptional(Utf8JsonWriter w, string name, string? value)
    {
        if (string.IsNullOrEmpty(value)) w.WriteNull(name);
        else w.WriteString(name, value);
    }

    /// <summary>
    /// Live Activity alanları → düz JSON nesnesi (<c>Validation.NormalizeLiveActivityValues</c> çıktısı: string, long,
    /// double, bool).
    /// </summary>
    public static string LiveActivityValuesToJson(IReadOnlyDictionary<string, object> values)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            foreach (var (key, value) in values)
            {
                switch (value)
                {
                    case string s: w.WriteString(key, s); break;
                    case bool b: w.WriteBoolean(key, b); break;
                    case long l: w.WriteNumber(key, l); break;
                    case double d: w.WriteNumber(key, d); break;
                    default: throw new InvalidOperationException("Normalize edilmemiş Live Activity değeri.");
                }
            }
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string ToJson(IReadOnlyDictionary<string, string> map) => JsonSerializer.Serialize(map, BridgeJsonContext.Default.IReadOnlyDictionaryStringString);

    public static string ToJson(IReadOnlyList<string> list) => JsonSerializer.Serialize(list, BridgeJsonContext.Default.IReadOnlyListString);

    private static HeroSignalNotification ReadNotification(JsonElement e)
    {
        var actions = new List<NotificationAction>();
        if (e.TryGetProperty("actions", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in arr.EnumerateArray())
            {
                var id = Str(a, "id");
                if (id is null) continue;
                actions.Add(new NotificationAction(id, Str(a, "title") ?? id, Str(a, "url"), Str(a, "icon")));
            }
        }
        return new HeroSignalNotification
        {
            Title = Str(e, "title"),
            Body = Str(e, "body"),
            ImageUrl = Str(e, "imageUrl"),
            Url = Str(e, "url"),
            DeliveryId = Str(e, "deliveryId"),
            MessageId = Str(e, "messageId"),
            Actions = actions,
            Data = e.TryGetProperty("data", out var data) ? ReadStringMap(data) : EmptyStringMap.Instance,
            AndroidNotificationId = e.TryGetProperty("androidNotificationId", out var trayId) && trayId.ValueKind == JsonValueKind.Number
                && trayId.TryGetInt32(out var androidNotificationId)
                ? androidNotificationId
                : null,
        };
    }

    private static InboxItem ReadInboxItem(JsonElement e) => new()
    {
        Id = Str(e, "id") ?? throw Invalid("id"),
        MessageId = Str(e, "messageId"),
        Title = Str(e, "title"),
        Body = Str(e, "body"),
        Url = Str(e, "url"),
        ImageUrl = Str(e, "imageUrl"),
        Data = e.TryGetProperty("data", out var data) ? ReadStringMap(data) : EmptyStringMap.Instance,
        Read = Bool(e, "read"),
        ReadAt = Millis(e, "readAt"),
        ArchivedAt = Millis(e, "archivedAt"),
        ExpiresAt = Millis(e, "expiresAt"),
        CreatedAt = Millis(e, "createdAt"),
    };

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement e)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (e.ValueKind != JsonValueKind.Object) return map;
        foreach (var p in e.EnumerateObject())
        {
            switch (p.Value.ValueKind)
            {
                case JsonValueKind.String: map[p.Name] = p.Value.GetString()!; break;
                case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False: map[p.Name] = p.Value.GetRawText(); break;
            }
        }
        return map;
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new HeroSignalException(HeroSignalException.InvalidResponse, "Köprü yanıtı ayrıştırılamadı.", e);
        }
    }

    private static HeroSignalException Invalid(string field) =>
        new(HeroSignalException.InvalidResponse, $"Köprü yanıtında '{field}' alanı eksik.");

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? Millis(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : null;
}

[System.Text.Json.Serialization.JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class BridgeJsonContext : System.Text.Json.Serialization.JsonSerializerContext;

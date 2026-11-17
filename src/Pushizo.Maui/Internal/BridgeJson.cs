using System.Text.Json;

namespace PushizoSdk.Maui.Internal;

/// <summary>
/// Yerel köprülerin (Android <c>native/android/bridge/…/PushizoMauiBridge.kt</c>, iOS
/// <c>Pushizo.Maui.iOS.Binding/native/Sources/PushizoMauiBridge/HSMauiBridge.swift</c>) döndürdüğü ortak JSON
/// biçimlerinin ayrıştırıcısı ve yazıcısı; zamanlar epoch milisaniyedir.
/// </summary>
internal static class BridgeJson
{
    public static PushizoNotification ParseNotification(string json)
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
            : new PushizoNotification();
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

    public static ContentCardFeed ParseContentCardFeed(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        var cards = new List<ContentCard>();
        if (root.TryGetProperty("cards", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in arr.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object) cards.Add(ReadContentCard(element));
            }
        }
        var unviewed = root.TryGetProperty("unviewedCount", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetInt32()
            : cards.Count(c => !c.Viewed);
        return new ContentCardFeed(cards, unviewed);
    }

    public static ContentCard ParseContentCard(string json)
    {
        using var doc = Parse(json);
        return ReadContentCard(doc.RootElement);
    }

    /// <summary><c>{"messages":[{"messageId","json"}]}</c> (<see cref="INativeBridge.PlacementsList"/>); kimliksiz öğe hatadır.</summary>
    public static IReadOnlyList<InAppMessage> ParsePlacementMessages(string json)
    {
        using var doc = Parse(json);
        var messages = new List<InAppMessage>();
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in arr.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                messages.Add(new InAppMessage(Str(element, "messageId") ?? throw Invalid("messageId"), Str(element, "json")));
            }
        }
        return messages;
    }

    /// <summary><see cref="DeliverySetting"/> → köprü değeri (<c>auto_start</c> …).</summary>
    public static string DeliverySettingWire(DeliverySetting setting) => setting switch
    {
        DeliverySetting.Notifications => "notifications",
        DeliverySetting.AutoStart => "auto_start",
        DeliverySetting.BatteryOptimization => "battery_optimization",
        DeliverySetting.AppDetails => "app_details",
        _ => "auto",
    };

    /// <summary>
    /// Android teslim durumu (<see cref="INativeBridge.DeliveryStatus"/>, ADR-0092). JSON yoksa (iOS, başlatılmamış yerel
    /// SDK) <see cref="DeliveryStatus.Unsupported"/>; yerel SDK'nın ileride ekleyeceği, bu sürümün tanımadığı ayar ekranı atlanır.
    /// </summary>
    public static DeliveryStatus ParseDeliveryStatus(string? json)
    {
        if (string.IsNullOrEmpty(json)) return DeliveryStatus.Unsupported;
        using var doc = Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new PushizoException(PushizoException.InvalidResponse, "Köprü yanıtı JSON nesnesi değil.");
        var settings = new List<DeliverySetting>();
        if (root.TryGetProperty("recommendedSettings", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                switch (item.GetString())
                {
                    case "notifications": settings.Add(DeliverySetting.Notifications); break;
                    case "auto_start": settings.Add(DeliverySetting.AutoStart); break;
                    case "battery_optimization": settings.Add(DeliverySetting.BatteryOptimization); break;
                    case "app_details": settings.Add(DeliverySetting.AppDetails); break;
                }
            }
        }
        return new DeliveryStatus
        {
            Supported = true,
            Manufacturer = Str(root, "manufacturer") ?? string.Empty,
            Brand = Str(root, "brand") ?? string.Empty,
            Vendor = Str(root, "vendor") ?? "other",
            AggressiveVendor = Bool(root, "aggressiveVendor"),
            Rom = Str(root, "rom") ?? string.Empty,
            RomVersion = Str(root, "romVersion"),
            NotificationsEnabled = Bool(root, "notificationsEnabled"),
            ChannelBlocked = Bool(root, "channelBlocked"),
            BatteryOptimizationExempt = Bool(root, "batteryOptimizationExempt"),
            BackgroundRestricted = Bool(root, "backgroundRestricted"),
            StandbyBucket = Str(root, "standbyBucket"),
            PowerSaveMode = Bool(root, "powerSaveMode"),
            DataSaverEnabled = Bool(root, "dataSaverEnabled"),
            Restricted = Bool(root, "restricted"),
            Risk = Str(root, "risk") switch
            {
                "high" => DeliveryRisk.High,
                "elevated" => DeliveryRisk.Elevated,
                _ => DeliveryRisk.None,
            },
            RecommendedSettings = settings.AsReadOnly(),
        };
    }

    /// <summary><c>{"refreshed":bool}</c> (<see cref="INativeBridge.FeatureFlagsRefresh"/>).</summary>
    public static bool ParseRefreshed(string json)
    {
        using var doc = Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object && Bool(doc.RootElement, "refreshed");
    }

    /// <summary>
    /// <c>{"value":…}</c> (<see cref="INativeBridge.FeatureFlagValue"/>) → string, bool, long (tam sayı), double,
    /// <c>Dictionary&lt;string, object?&gt;</c> veya <c>List&lt;object?&gt;</c>; değer yoksa null.
    /// </summary>
    public static object? ParseFlagValue(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("value", out var value)
            ? ReadValue(value)
            : null;
    }

    private static object? ReadValue(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                return e.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                // Koşullu ifade iki dalı double'a birleştirmesin diye tam sayı ayrı döndürülür.
                if (e.TryGetInt64(out var integer)) return integer;
                return e.GetDouble();
            case JsonValueKind.Object:
            {
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var p in e.EnumerateObject()) map[p.Name] = ReadValue(p.Value);
                return map;
            }
            case JsonValueKind.Array:
            {
                var items = new List<object?>();
                foreach (var item in e.EnumerateArray()) items.Add(ReadValue(item));
                return items;
            }
            default:
                return null;
        }
    }

    /// <summary><c>{"key":"value"}</c> biçimindeki metin sözlüğü (tag/trigger).</summary>
    public static IReadOnlyDictionary<string, string> ParseStringMap(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
        using var doc = Parse(json);
        return ReadStringMap(doc.RootElement);
    }

    /// <summary><c>{"eventId":"…","notification":{…}}</c>.</summary>
    public static (string EventId, PushizoNotification Notification) ParseForeground(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        var eventId = Str(root, "eventId") ?? throw Invalid("eventId");
        var notification = root.TryGetProperty("notification", out var n) && n.ValueKind == JsonValueKind.Object
            ? ReadNotification(n)
            : new PushizoNotification();
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
    /// <see cref="PushizoException.InvalidResponse"/> verir; anahtarsız kategori atlanır (yerel SDK'larla aynı).
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

    /// <summary><c>{"consents":[{"purpose","granted","source","updatedAt"}]}</c>; amaçsız kayıt atlanır (yerel SDK'larla aynı).</summary>
    public static IReadOnlyList<UserConsent> ParseConsents(string json)
    {
        using var doc = Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Invalid("consents");
        var consents = new List<UserConsent>();
        if (root.TryGetProperty("consents", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var purpose = Str(e, "purpose");
                if (string.IsNullOrEmpty(purpose)) continue;
                consents.Add(new UserConsent
                {
                    Purpose = purpose,
                    Granted = Bool(e, "granted"),
                    Source = Str(e, "source"),
                    UpdatedAt = Millis(e, "updatedAt"),
                });
            }
        }
        return consents;
    }

    /// <summary>Profille giriş yükü: <c>{"email","sms","tags":{},"aliases":{}}</c>; boş alanlar yazılmaz.</summary>
    public static string UserProfileToJson(PushizoUserProfile profile)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            if (profile.Email is not null) w.WriteString("email", profile.Email);
            if (profile.Sms is not null) w.WriteString("sms", profile.Sms);
            WriteMap(w, "tags", profile.Tags);
            WriteMap(w, "aliases", profile.Aliases);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());

        static void WriteMap(Utf8JsonWriter w, string name, IReadOnlyDictionary<string, string>? map)
        {
            if (map is null || map.Count == 0) return;
            w.WriteStartObject(name);
            foreach (var (key, value) in map) w.WriteString(key, value);
            w.WriteEndObject();
        }
    }

    /// <summary>Yerel köprünün başlatma seçenekleri (<c>PushizoMauiBridge.start</c> / <c>HSMauiBridge.start</c>).</summary>
    public static string OptionsToJson(PushizoOptions options)
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

    private static PushizoNotification ReadNotification(JsonElement e)
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
        return new PushizoNotification
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

    private static ContentCard ReadContentCard(JsonElement e) => new()
    {
        Id = Str(e, "id") ?? throw Invalid("id"),
        MessageId = Str(e, "messageId"),
        Type = Str(e, "type") switch
        {
            "captioned_image" => ContentCardType.CaptionedImage,
            "image_only" => ContentCardType.ImageOnly,
            _ => ContentCardType.Classic,
        },
        Title = Str(e, "title"),
        Body = Str(e, "body"),
        Url = Str(e, "url"),
        ImageUrl = Str(e, "imageUrl"),
        ButtonText = Str(e, "buttonText"),
        Data = e.TryGetProperty("data", out var data) ? ReadStringMap(data) : EmptyStringMap.Instance,
        Pinned = Bool(e, "pinned"),
        Priority = e.TryGetProperty("priority", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0,
        Viewed = Bool(e, "viewed"),
        Clicked = Bool(e, "clicked"),
        ExpiresAt = Millis(e, "expiresAt"),
        CreatedAt = Millis(e, "createdAt"),
    };

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
            throw new PushizoException(PushizoException.InvalidResponse, "Köprü yanıtı ayrıştırılamadı.", e);
        }
    }

    private static PushizoException Invalid(string field) =>
        new(PushizoException.InvalidResponse, $"Köprü yanıtında '{field}' alanı eksik.");

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

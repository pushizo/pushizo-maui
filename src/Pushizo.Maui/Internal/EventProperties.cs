using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace PushizoSdk.Maui.Internal;

/// <summary>
/// <c>Session.SendEvent</c> özelliklerini JSON uyumlu kanonik ağaca çevirir: string, bool, long, double,
/// iç içe sözlük (<c>Dictionary&lt;string, object?&gt;</c>), liste (<c>List&lt;object?&gt;</c>) ve null.
/// Desteklenmeyen tür (ör. akış, nesne) <see cref="ArgumentException"/> üretir.
/// </summary>
internal static class EventProperties
{
    private const int MaxDepth = 16;

    public static Dictionary<string, object?> Normalize(IReadOnlyDictionary<string, object?>? properties, string paramName)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (properties is null) return result;
        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Olay özelliği anahtarı boş olamaz.", paramName);
            result[key] = NormalizeValue(value, paramName, 1);
        }
        return result;
    }

    private static object? NormalizeValue(object? value, string paramName, int depth)
    {
        if (depth > MaxDepth) throw new ArgumentException("Olay özellikleri en fazla 16 seviye iç içe olabilir.", paramName);
        switch (value)
        {
            case null:
                return null;
            case string s:
                return s;
            case bool b:
                return b;
            case char c:
                return c.ToString();
            case sbyte or byte or short or ushort or int or uint or long:
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            case ulong ul:
                return ul <= long.MaxValue ? (long)ul : (double)ul;
            case float f:
                return RequireFinite(f, paramName);
            case double d:
                return RequireFinite(d, paramName);
            case decimal m:
                return (double)m;
            case DateTimeOffset dto:
                return dto.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            case DateTime dt:
                return (dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime())
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            case Guid g:
                return g.ToString();
            case Enum e:
                return e.ToString();
            case IDictionary dict:
            {
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is not string k || k.Length == 0)
                        throw new ArgumentException("Olay özelliği sözlük anahtarları boş olmayan metin olmalı.", paramName);
                    map[k] = NormalizeValue(entry.Value, paramName, depth + 1);
                }
                return map;
            }
            case IEnumerable<KeyValuePair<string, object?>> pairs:
            {
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var (k, v) in pairs)
                {
                    if (string.IsNullOrEmpty(k)) throw new ArgumentException("Olay özelliği sözlük anahtarları boş olamaz.", paramName);
                    map[k] = NormalizeValue(v, paramName, depth + 1);
                }
                return map;
            }
            case IEnumerable list:
            {
                var items = new List<object?>();
                foreach (var item in list) items.Add(NormalizeValue(item, paramName, depth + 1));
                return items;
            }
            default:
                throw new ArgumentException($"Desteklenmeyen olay özelliği türü: {value.GetType().FullName}.", paramName);
        }
    }

    private static double RequireFinite(double value, string paramName) =>
        double.IsFinite(value) ? value : throw new ArgumentException("Olay özelliği sayıları sonlu olmalı (NaN/Infinity desteklenmez).", paramName);

    /// <summary>Kanonik ağacı JSON'a yazar (iOS köprüsü özellikleri JSON metni olarak alır).</summary>
    public static string ToJson(Dictionary<string, object?> properties)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, properties);
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string s: writer.WriteStringValue(s); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case long l: writer.WriteNumberValue(l); break;
            case double d: writer.WriteNumberValue(d); break;
            case Dictionary<string, object?> map:
                writer.WriteStartObject();
                foreach (var (k, v) in map)
                {
                    writer.WritePropertyName(k);
                    Write(writer, v);
                }
                writer.WriteEndObject();
                break;
            case List<object?> items:
                writer.WriteStartArray();
                foreach (var item in items) Write(writer, item);
                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException("Kanonik olmayan olay özelliği değeri.");
        }
    }
}

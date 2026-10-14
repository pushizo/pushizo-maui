using System.Text.RegularExpressions;

namespace HeroSignalSdk.Maui.Internal;

/// <summary>
/// Girdi doğrulama ve normalizasyon; kurallar yerel SDK'lar ve sunucu (<c>packages/shared</c> messaging) ile aynıdır.
/// Ortak test vektörleri: <c>sdk/testdata/messaging-vectors.json</c>.
/// </summary>
internal static partial class Validation
{
    public const int MaxAliasesPerRequest = 20;

    [GeneratedRegex(@"^[^\s@""(),:;<>\[\\\]]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,62}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,62}[A-Za-z0-9])?)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();

    [GeneratedRegex(@"[\s().-]", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneSeparatorsRegex();

    [GeneratedRegex("^[a-z0-9_]{1,50}$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasLabelRegex();

    private static readonly HashSet<string> ReservedAliases = new(StringComparer.Ordinal) { "external_id", "hs_id" };

    /// <summary>Boşluk kırpma + küçük harf (kültürden bağımsız); geçersizse null.</summary>
    public static string? NormalizeEmail(string? value)
    {
        if (value is null) return null;
        var v = value.Trim().ToLowerInvariant();
        if (v.Length > 320 || !EmailRegex().IsMatch(v)) return null;
        return v;
    }

    /// <summary>E.164: boşluk, tire, parantez ve nokta atılır, <c>00</c> öneki <c>+</c> olur; geçersizse null.</summary>
    public static string? NormalizePhone(string? value)
    {
        if (value is null) return null;
        var v = PhoneSeparatorsRegex().Replace(value.Trim(), string.Empty);
        if (v.StartsWith("00", StringComparison.Ordinal)) v = "+" + v[2..];
        return E164Regex().IsMatch(v) ? v : null;
    }

    public static string RequireEmail(string email, string paramName) =>
        NormalizeEmail(email) ?? throw new ArgumentException("Geçersiz e-posta adresi.", paramName);

    public static string RequirePhone(string phoneNumber, string paramName) =>
        NormalizePhone(phoneNumber) ?? throw new ArgumentException("Geçersiz telefon numarası (E.164 bekleniyor, ör. +905321234567).", paramName);

    /// <summary>Alias etiketi geçersiz veya ayrılmışsa (external_id → Login/Logout, hs_id → sistem) hata fırlatır.</summary>
    public static void RequireAliasLabel(string label, string paramName)
    {
        ArgumentNullException.ThrowIfNull(label, paramName);
        if (!AliasLabelRegex().IsMatch(label))
            throw new ArgumentException($"Geçersiz alias etiketi \"{label}\": küçük harf, rakam ve alt çizgi (en fazla 50).", paramName);
        if (ReservedAliases.Contains(label))
            throw new ArgumentException($"\"{label}\" alias olarak ayarlanamaz (external_id için Login/Logout kullanın).", paramName);
    }

    /// <summary>Alias haritasını doğrular ve değerleri kırpar (1-20 alias, değer 1-255 karakter).</summary>
    public static IReadOnlyDictionary<string, string> NormalizeAliases(IReadOnlyDictionary<string, string> aliases, string paramName)
    {
        ArgumentNullException.ThrowIfNull(aliases, paramName);
        if (aliases.Count == 0) throw new ArgumentException("En az bir alias gerekli.", paramName);
        if (aliases.Count > MaxAliasesPerRequest)
            throw new ArgumentException($"Tek seferde en fazla {MaxAliasesPerRequest} alias eklenebilir.", paramName);
        var result = new Dictionary<string, string>(aliases.Count, StringComparer.Ordinal);
        foreach (var (label, raw) in aliases)
        {
            RequireAliasLabel(label, paramName);
            var value = (raw ?? string.Empty).Trim();
            if (value.Length is < 1 or > 255)
                throw new ArgumentException($"\"{label}\" alias değeri 1-255 karakter olmalı.", paramName);
            result[label] = value;
        }
        return result;
    }

    /// <summary>Tag haritası: anahtar boş olamaz, değer null olamaz (silmek için RemoveTag).</summary>
    public static IReadOnlyDictionary<string, string> RequireTags(IReadOnlyDictionary<string, string> tags, string paramName)
    {
        ArgumentNullException.ThrowIfNull(tags, paramName);
        var result = new Dictionary<string, string>(tags.Count, StringComparer.Ordinal);
        foreach (var (key, value) in tags)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Tag anahtarı boş olamaz.", paramName);
            result[key] = value ?? throw new ArgumentException($"\"{key}\" tag değeri null olamaz (silmek için RemoveTag kullanın).", paramName);
        }
        return result;
    }

    /// <summary>In-app trigger'ları: anahtar 1-64, değer en fazla 200 karakter.</summary>
    public static IReadOnlyDictionary<string, string> RequireTriggers(IReadOnlyDictionary<string, string> triggers, string paramName)
    {
        ArgumentNullException.ThrowIfNull(triggers, paramName);
        var result = new Dictionary<string, string>(triggers.Count, StringComparer.Ordinal);
        foreach (var (key, value) in triggers)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 64) throw new ArgumentException("Trigger anahtarı 1-64 karakter olmalı.", paramName);
            if (value is null || value.Length > 200) throw new ArgumentException("Trigger değeri en fazla 200 karakter olabilir.", paramName);
            result[key] = value;
        }
        return result;
    }

    /// <summary>Anahtar listesi: null/boş öğe içeremez.</summary>
    public static IReadOnlyList<string> RequireKeys(IEnumerable<string> keys, string paramName)
    {
        ArgumentNullException.ThrowIfNull(keys, paramName);
        var list = keys.ToList();
        if (list.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Anahtarlar boş olamaz.", paramName);
        return list;
    }

    public static void RequireInboxLimit(int limit, string paramName)
    {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(paramName, limit, "limit 1-200 olmalı.");
    }

    public const int MaxPreferencesPerRequest = 100;

    /// <summary>Tercih kanalları (ADR-0019 §4; yerel SDK'lardaki <c>HsPreferencesParser</c> ile aynı küme).</summary>
    private static readonly HashSet<string> PreferenceChannels = new(StringComparer.Ordinal) { "ios_push", "android_push", "web_push", "email", "sms" };

    /// <summary>Tercih girdileri: 1-100 kayıt, kategori 1-64 karakter (boşluktan ibaret olamaz), kanal null veya bilinen bir anahtar.</summary>
    public static IReadOnlyList<PreferenceInput> RequirePreferences(IEnumerable<PreferenceInput> preferences, string paramName)
    {
        ArgumentNullException.ThrowIfNull(preferences, paramName);
        var list = preferences.ToList();
        if (list.Count is < 1 or > MaxPreferencesPerRequest)
            throw new ArgumentException($"1-{MaxPreferencesPerRequest} tercih verilmeli.", paramName);
        foreach (var p in list)
        {
            if (p is null) throw new ArgumentException("Tercih null olamaz.", paramName);
            if (string.IsNullOrWhiteSpace(p.Category) || p.Category.Length > 64)
                throw new ArgumentException("Kategori anahtarı 1-64 karakter olmalı.", paramName);
            if (p.Channel is not null && !PreferenceChannels.Contains(p.Channel))
                throw new ArgumentException($"Geçersiz kanal: {p.Channel} (ios_push, android_push, web_push, email, sms ya da null).", paramName);
        }
        return list;
    }
}

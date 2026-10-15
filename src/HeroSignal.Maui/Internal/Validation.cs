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

    // \z: .NET'te $ sondaki satır sonunu da kabul eder.
    [GeneratedRegex(@"^[A-Za-z0-9_.:-]{1,100}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LiveActivityIdRegex();

    /// <summary>Live Activity alan anahtarı üst sınırı (yerel SDK ve sunucu).</summary>
    public const int MaxLiveActivityKeyLength = 64;

    /// <summary>Live Activity metin değeri üst sınırı (yerel SDK ve sunucu).</summary>
    public const int MaxLiveActivityStringLength = 4000;

    /// <summary>Etkinlik kimliği: 1-100 karakter; harf, rakam ve <c>_ . : -</c> (sunucu start kuralı).</summary>
    public static string RequireLiveActivityId(string activityId) =>
        activityId is not null && LiveActivityIdRegex().IsMatch(activityId)
            ? activityId
            : throw InvalidLiveActivity("activityId 1-100 karakter olmalı (harf, rakam ve _ . : -).");

    /// <summary>
    /// Live Activity alanlarını doğrular ve normalize eder: anahtar 1-64 karakter, değer metin (en çok 4000 karakter),
    /// sonlu sayı (tam sayılar long, diğerleri double) veya bool. <paramref name="reserveHeroSignalKey"/> true ise
    /// (<c>attributes</c>) <c>herosignal</c> anahtarı sunucu verisi için ayrılmıştır. Hata: <c>INVALID_ARGUMENT</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, object> NormalizeLiveActivityValues(IDictionary<string, object> values, string paramName, bool reserveHeroSignalKey)
    {
        if (values is null) throw InvalidLiveActivity($"{paramName} gerekli.");
        var result = new Dictionary<string, object>(values.Count, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxLiveActivityKeyLength)
                throw InvalidLiveActivity($"{paramName} anahtarları 1-{MaxLiveActivityKeyLength} karakter olmalı.");
            if (reserveHeroSignalKey && key == "herosignal")
                throw InvalidLiveActivity($"{paramName} içinde 'herosignal' anahtarı ayrılmıştır.");
            result[key] = NormalizeLiveActivityValue(value) ??
                throw InvalidLiveActivity($"{paramName}[\"{key}\"] metin (en çok {MaxLiveActivityStringLength} karakter), sonlu sayı veya bool olmalı.");
        }
        return result;
    }

    private static object? NormalizeLiveActivityValue(object? value) => value switch
    {
        string s => s.Length <= MaxLiveActivityStringLength ? s : null,
        bool b => b,
        sbyte or byte or short or ushort or int or uint or long => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
        ulong u => u <= long.MaxValue ? (long)u : (double)u,
        // float, en kısa ondalık gösterimiyle çevrilir (0.1f → 0.1; doğrudan dönüşüm 0.10000000149… verir).
        float f => float.IsFinite(f) ? double.Parse(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture) : null,
        double d => double.IsFinite(d) ? d : null,
        decimal m => (double)m,
        _ => null,
    };

    private static HeroSignalException InvalidLiveActivity(string message) => new(HeroSignalException.InvalidArgument, message);

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

using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui;

/// <summary>Özel olaylar ve outcome'lar (<c>HeroSignal.Session</c>).</summary>
public sealed class SessionManager
{
    internal SessionManager()
    {
    }

    /// <summary>
    /// Özel olay gönderir (çevrimdışıyken kuyruğa alınır). Ad 1-100 karakter; harf, rakam, <c>_ . : -</c>. Özellikler: metin, bool, sayı, tarih, Guid, enum, iç içe
    /// sözlük ve liste (en fazla 16 seviye). <paramref name="value"/> outcome değeridir (ör. sipariş tutarı).
    /// Olay adı aynı zamanda in-app <c>event</c> tetiklerini çalıştırır.
    /// </summary>
    public void SendEvent(string name, IReadOnlyDictionary<string, object?>? properties = null, double? value = null)
    {
        var trimmed = Validation.RequireEventName(name, nameof(name));
        if (value is { } v && !double.IsFinite(v))
            throw new ArgumentException("Olay değeri sonlu bir sayı olmalı.", nameof(value));
        var normalized = EventProperties.Normalize(properties, nameof(properties));
        HeroSignal.Platform().SendEvent(trimmed, normalized, value);
    }
}

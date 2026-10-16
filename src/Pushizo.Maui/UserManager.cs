using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui;

/// <summary>Kullanıcı: tag, alias, dil, e-posta/SMS abonelikleri, bildirim tercihleri ve push aboneliği (<c>Pushizo.User</c>).</summary>
public sealed class UserManager
{
    internal UserManager()
    {
    }

    /// <summary>Push aboneliği.</summary>
    public PushSubscriptionManager PushSubscription { get; } = new();

    /// <summary>Login ile verilen kullanıcı kimliği; anonimse null.</summary>
    public string? ExternalId => Pushizo.PlatformOrNull()?.ExternalId;

    /// <summary>Pushizo kullanıcı kimliği (abonelik kaydından sonra).</summary>
    public string? Id => Pushizo.PlatformOrNull()?.UserId;

    /// <summary>Bu cihazda kayıtlı e-posta adresleri.</summary>
    public IReadOnlyList<string> Emails => Pushizo.PlatformOrNull()?.Emails ?? Array.Empty<string>();

    /// <summary>Bu cihazda kayıtlı SMS numaraları (E.164).</summary>
    public IReadOnlyList<string> SmsNumbers => Pushizo.PlatformOrNull()?.SmsNumbers ?? Array.Empty<string>();

    public void AddTag(string key, string value) => AddTags(new Dictionary<string, string> { [key] = value });

    /// <summary>Tag ekler/günceller; çevrimdışıyken yerel kuyrukta bekler.</summary>
    public void AddTags(IReadOnlyDictionary<string, string> tags)
    {
        var valid = Validation.RequireTags(tags, nameof(tags));
        if (valid.Count == 0) return;
        Pushizo.Platform().AddTags(valid);
    }

    public void RemoveTag(string key) => RemoveTags(new[] { key });

    public void RemoveTags(IEnumerable<string> keys)
    {
        var valid = Validation.RequireKeys(keys, nameof(keys));
        if (valid.Count == 0) return;
        Pushizo.Platform().RemoveTags(valid);
    }

    /// <summary>Sunucudan son alınan tag'ler (yerel önbellek).</summary>
    public IReadOnlyDictionary<string, string> GetTags() =>
        Pushizo.PlatformOrNull()?.GetTags() ?? new Dictionary<string, string>();

    /// <summary>Alias ekler (ör. <c>AddAlias("crm_id", "42")</c>); etiket küçük harf/rakam/_ (en fazla 50).</summary>
    public void AddAlias(string label, string id) => AddAliases(new Dictionary<string, string> { [label] = id });

    /// <summary>En fazla 20 alias'ı tek istekte ekler.</summary>
    public void AddAliases(IReadOnlyDictionary<string, string> aliases) =>
        Pushizo.Platform().AddAliases(Validation.NormalizeAliases(aliases, nameof(aliases)));

    public void RemoveAlias(string label)
    {
        Validation.RequireAliasLabel(label, nameof(label));
        Pushizo.Platform().RemoveAlias(label);
    }

    /// <summary>Kullanıcının dili (ISO 639-1, ör. <c>tr</c>); mesaj içerikleri bu dilde seçilir.</summary>
    public void SetLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentException("Dil kodu gerekli.", nameof(languageCode));
        Pushizo.Platform().SetLanguage(languageCode.Trim());
    }

    /// <summary>E-posta aboneliği ekler (küçük harfe çevrilir); geçersiz adreste <see cref="ArgumentException"/>.</summary>
    public void AddEmail(string email) => Pushizo.Platform().AddEmail(Validation.RequireEmail(email, nameof(email)));

    /// <summary>E-posta aboneliğini siler.</summary>
    public void RemoveEmail(string email) => Pushizo.Platform().RemoveEmail(Validation.RequireEmail(email, nameof(email)));

    /// <summary>SMS aboneliği ekler; numara E.164'e normalize edilir (<c>+90 (532) 123-45-67</c> → <c>+905321234567</c>).</summary>
    public void AddSms(string phoneNumber) => Pushizo.Platform().AddSms(Validation.RequirePhone(phoneNumber, nameof(phoneNumber)));

    /// <summary>SMS aboneliğini siler.</summary>
    public void RemoveSms(string phoneNumber) => Pushizo.Platform().RemoveSms(Validation.RequirePhone(phoneNumber, nameof(phoneNumber)));

    /// <summary>
    /// Kullanıcının kategori/kanal tercihleri ve app'in mesaj kategorileri (ADR-0019 §4). Abonelik kaydı tamamlanmadan
    /// <see cref="PushizoException"/> (<c>NOT_READY</c>); sunucu hataları sunucu kodunu taşır.
    /// </summary>
    public Task<UserPreferences> GetPreferencesAsync() => Pushizo.Platform().GetPreferencesAsync();

    /// <summary>
    /// Tercihleri yazar (1-100 kayıt; <see cref="PreferenceInput.Channel"/> null = tüm kanallar) ve güncel listeyi döndürür.
    /// Geçersiz girdide <see cref="ArgumentException"/>.
    /// </summary>
    public Task<UserPreferences> SetPreferencesAsync(IEnumerable<PreferenceInput> preferences) =>
        Pushizo.Platform().SetPreferencesAsync(Validation.RequirePreferences(preferences, nameof(preferences)));
}

/// <summary>Push aboneliği (<c>Pushizo.User.PushSubscription</c>).</summary>
public sealed class PushSubscriptionManager
{
    private readonly object _gate = new();
    private PushSubscriptionState _last = PushSubscriptionState.Empty;

    internal PushSubscriptionManager()
    {
    }

    /// <summary>Abonelik kimliği, token, opt-in veya izin değiştiğinde (yalnızca gerçek değişiklikte).</summary>
    public event EventHandler<PushSubscriptionChangedEventArgs>? Changed;

    public string? Id => Current.Id;
    public string? Token => Current.Token;
    public bool OptedIn => Current.OptedIn;

    /// <summary>Güncel durum.</summary>
    public PushSubscriptionState Current => Pushizo.PlatformOrNull()?.PushSubscription ?? PushSubscriptionState.Empty;

    /// <summary>Push almayı yeniden açar (izin yoksa abonelik izin verilene kadar beklemede kalır).</summary>
    public void OptIn() => Pushizo.Platform().OptIn();

    /// <summary>Push almayı kapatır (abonelik silinmez).</summary>
    public void OptOut() => Pushizo.Platform().OptOut();

    internal void Dispatch(PushSubscriptionState state)
    {
        PushSubscriptionState previous;
        lock (_gate)
        {
            if (state == _last) return;
            previous = _last;
            _last = state;
        }
        Pushizo.Raise(Changed, new PushSubscriptionChangedEventArgs(previous, state), "PushSubscription.Changed");
        Pushizo.Notifications.DispatchPermission(state.Permission);
    }

    /// <summary>Başlangıç durumunu olay üretmeden kaydeder.</summary>
    internal void Seed(PushSubscriptionState state)
    {
        lock (_gate) _last = state;
    }

    internal void Reset()
    {
        lock (_gate) _last = PushSubscriptionState.Empty;
        Changed = null;
    }
}

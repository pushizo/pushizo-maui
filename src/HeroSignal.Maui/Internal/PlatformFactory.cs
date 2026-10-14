namespace HeroSignalSdk.Maui.Internal;

/// <summary>Derleme hedefine göre platform uygulamasını seçer.</summary>
internal static class PlatformFactory
{
    public static IHeroSignalPlatform Create()
    {
#if ANDROID
        return new BridgePlatform(new AndroidBridge.AndroidNativeBridge());
#elif IOS
        return new BridgePlatform(new IosBridge.IosNativeBridge());
#else
        return new UnsupportedPlatform();
#endif
    }
}

/// <summary>
/// Yerel HeroSignal SDK'sı olmayan hedefler (Windows, MacCatalyst, platformdan bağımsız net8.0): yazma çağrıları
/// etkisizdir ve bir kez uyarı günlüğe yazılır; okuma çağrıları boş değer, bildirim kutusu ve tercihler
/// <see cref="HeroSignalException.PlatformNotSupported"/> döndürür.
/// </summary>
internal sealed class UnsupportedPlatform : IHeroSignalPlatform
{
    private int _warned;

    public bool IsSupported => false;

    private void Ignored(string operation)
    {
        if (Interlocked.Exchange(ref _warned, 1) == 0)
            SdkLog.Write(LogLevel.Warn, $"{operation}: bu platformda yerel HeroSignal SDK'sı yok; çağrı etkisiz (yalnızca Android ve iOS)");
        else
            SdkLog.Write(LogLevel.Debug, $"{operation}: bu platformda etkisiz");
    }

    private static Task<T> NotSupported<T>(string feature = "Bildirim kutusu") =>
        Task.FromException<T>(new HeroSignalException(HeroSignalException.PlatformNotSupported, $"{feature} yalnızca Android ve iOS'ta kullanılabilir."));

    public void Initialize(string appId, HeroSignalOptions options, IHeroSignalEventSink sink)
    {
    }

    public void SetConsentGiven(bool given) => Ignored(nameof(SetConsentGiven));
    public void Login(string externalId, string? identityHash) => Ignored(nameof(Login));
    public void Logout() => Ignored(nameof(Logout));

    public void SetLogLevel(LogLevel level)
    {
    }

    public string? ExternalId => null;
    public string? UserId => null;
    public void AddTags(IReadOnlyDictionary<string, string> tags) => Ignored(nameof(AddTags));
    public void RemoveTags(IReadOnlyList<string> keys) => Ignored(nameof(RemoveTags));
    public IReadOnlyDictionary<string, string> GetTags() => new Dictionary<string, string>();
    public void AddAliases(IReadOnlyDictionary<string, string> aliases) => Ignored(nameof(AddAliases));
    public void RemoveAlias(string label) => Ignored(nameof(RemoveAlias));
    public void SetLanguage(string languageCode) => Ignored(nameof(SetLanguage));
    public void AddEmail(string email) => Ignored(nameof(AddEmail));
    public void RemoveEmail(string email) => Ignored(nameof(RemoveEmail));
    public void AddSms(string phoneNumber) => Ignored(nameof(AddSms));
    public void RemoveSms(string phoneNumber) => Ignored(nameof(RemoveSms));
    public IReadOnlyList<string> Emails => Array.Empty<string>();
    public IReadOnlyList<string> SmsNumbers => Array.Empty<string>();
    public Task<UserPreferences> GetPreferencesAsync() => NotSupported<UserPreferences>("Tercihler");
    public Task<UserPreferences> SetPreferencesAsync(IReadOnlyList<PreferenceInput> preferences) => NotSupported<UserPreferences>("Tercihler");
    public PushSubscriptionState PushSubscription => PushSubscriptionState.Empty;
    public void OptIn() => Ignored(nameof(OptIn));
    public void OptOut() => Ignored(nameof(OptOut));
    public bool NotificationPermission => false;

    public Task<bool> RequestPermissionAsync(bool fallbackToSettings)
    {
        Ignored(nameof(RequestPermissionAsync));
        return Task.FromResult(false);
    }

    public void ClearAllNotifications() => Ignored(nameof(ClearAllNotifications));
    public void RemoveNotification(string id) => Ignored(nameof(RemoveNotification));
    public void RemoveGroupedNotifications(string group) => Ignored(nameof(RemoveGroupedNotifications));
    public void AddTriggers(IReadOnlyDictionary<string, string> triggers) => Ignored(nameof(AddTriggers));
    public void RemoveTriggers(IReadOnlyList<string> keys) => Ignored(nameof(RemoveTriggers));
    public void ClearTriggers() => Ignored(nameof(ClearTriggers));
    public IReadOnlyDictionary<string, string> GetTriggers() => new Dictionary<string, string>();
    public void SetScreen(string? name) => Ignored(nameof(SetScreen));

    public bool InAppPaused
    {
        get => false;
        set => Ignored(nameof(InAppPaused));
    }

    public Task<InboxPage> InboxListAsync(int limit, string? cursor) => NotSupported<InboxPage>();
    public Task<InboxItem> InboxMarkReadAsync(string id) => NotSupported<InboxItem>();
    public Task<int> InboxMarkAllReadAsync() => NotSupported<int>();
    public Task InboxArchiveAsync(string id) => NotSupported<bool>();
    public Task<int> InboxUnreadCountAsync() => NotSupported<int>();
    public void SendEvent(string name, Dictionary<string, object?> properties, double? value) => Ignored(nameof(SendEvent));
}

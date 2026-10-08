using HeroSignalSdk.Maui.Internal;

namespace HeroSignalSdk.Maui.Platforms;

/// <summary>
/// Yerel SDK'sı olmayan hedefler (Windows, Mac Catalyst, net9.0): çağrılar etkisizdir ve Debug seviyesinde günlüğe
/// yazılır; okuma özellikleri boş değer döndürür, bildirim kutusu <see cref="HeroSignalException.PlatformNotSupported"/> verir.
/// Ortak kodun tek kod tabanıyla tüm MAUI hedeflerinde derlenip çalışabilmesi içindir.
/// </summary>
internal sealed class UnsupportedHeroSignalPlatform : IHeroSignalPlatform
{
    public bool IsSupported => false;

    public void Initialize(string appId, HeroSignalOptions options, IHeroSignalEventSink sink) => Skip(nameof(Initialize));
    public void SetConsentGiven(bool given) => Skip(nameof(SetConsentGiven));
    public void Login(string externalId, string? identityHash) => Skip(nameof(Login));
    public void Logout() => Skip(nameof(Logout));
    public void SetLogLevel(LogLevel level)
    {
    }

    public string? ExternalId => null;
    public string? UserId => null;
    public void AddTags(IReadOnlyDictionary<string, string> tags) => Skip(nameof(AddTags));
    public void RemoveTags(IReadOnlyList<string> keys) => Skip(nameof(RemoveTags));
    public IReadOnlyDictionary<string, string> GetTags() => new Dictionary<string, string>();
    public void AddAliases(IReadOnlyDictionary<string, string> aliases) => Skip(nameof(AddAliases));
    public void RemoveAlias(string label) => Skip(nameof(RemoveAlias));
    public void SetLanguage(string languageCode) => Skip(nameof(SetLanguage));
    public void AddEmail(string email) => Skip(nameof(AddEmail));
    public void RemoveEmail(string email) => Skip(nameof(RemoveEmail));
    public void AddSms(string phoneNumber) => Skip(nameof(AddSms));
    public void RemoveSms(string phoneNumber) => Skip(nameof(RemoveSms));
    public IReadOnlyList<string> Emails => Array.Empty<string>();
    public IReadOnlyList<string> SmsNumbers => Array.Empty<string>();

    public PushSubscriptionState PushSubscription => PushSubscriptionState.Empty;
    public void OptIn() => Skip(nameof(OptIn));
    public void OptOut() => Skip(nameof(OptOut));

    public bool NotificationPermission => false;

    public Task<bool> RequestPermissionAsync(bool fallbackToSettings)
    {
        Skip(nameof(RequestPermissionAsync));
        return Task.FromResult(false);
    }

    public void ClearAllNotifications() => Skip(nameof(ClearAllNotifications));
    public void RemoveNotification(string id) => Skip(nameof(RemoveNotification));
    public void RemoveGroupedNotifications(string group) => Skip(nameof(RemoveGroupedNotifications));

    public void AddTriggers(IReadOnlyDictionary<string, string> triggers) => Skip(nameof(AddTriggers));
    public void RemoveTriggers(IReadOnlyList<string> keys) => Skip(nameof(RemoveTriggers));
    public void ClearTriggers() => Skip(nameof(ClearTriggers));
    public IReadOnlyDictionary<string, string> GetTriggers() => new Dictionary<string, string>();
    public void SetScreen(string? name) => Skip(nameof(SetScreen));

    public bool InAppPaused
    {
        get => false;
        set => Skip(nameof(InAppPaused));
    }

    public Task<InboxPage> InboxListAsync(int limit, string? cursor) => Task.FromException<InboxPage>(NotSupported());
    public Task<InboxItem> InboxMarkReadAsync(string id) => Task.FromException<InboxItem>(NotSupported());
    public Task<int> InboxMarkAllReadAsync() => Task.FromException<int>(NotSupported());
    public Task InboxArchiveAsync(string id) => Task.FromException(NotSupported());
    public Task<int> InboxUnreadCountAsync() => Task.FromException<int>(NotSupported());

    public void SendEvent(string name, Dictionary<string, object?> properties, double? value) => Skip(nameof(SendEvent));

    private static void Skip(string operation) =>
        SdkLog.Write(LogLevel.Debug, $"{operation}: bu platformda yerel SDK yok, çağrı etkisiz");

    private static HeroSignalException NotSupported() =>
        new(HeroSignalException.PlatformNotSupported, "Bildirim kutusu yalnızca Android ve iOS'ta kullanılabilir.");
}

/// <summary>Bu hedefin platform uygulaması.</summary>
internal static class PlatformFactory
{
    public static IHeroSignalPlatform Create() => new UnsupportedHeroSignalPlatform();
}

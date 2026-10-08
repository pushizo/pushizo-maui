using Foundation;
using HeroSignalSdk.Maui.Internal;
using HsBridge = global::HeroSignalSdk.Native.iOS.HSMauiBridge;
using HsLogLevel = global::HeroSignalSdk.Native.iOS.HSMauiLogLevel;

namespace HeroSignalSdk.Maui.Platforms;

/// <summary>
/// iOS uygulaması: yerel Swift SDK'sı <c>HSMauiBridge</c> Objective-C köprüsü (HeroSignal.Maui.iOS.Binding) üzerinden
/// çağrılır; karmaşık değerler JSON ile taşınır (<see cref="BridgeJson"/>). Köprü olayları ana iş parçacığında gelir.
/// APNs token'ı köprünün uygulama temsilcisine eklediği geri çağrıyla ya da <see cref="HeroSignalIos.SetDeviceToken"/> ile alınır.
/// </summary>
internal sealed class IosHeroSignalPlatform : IHeroSignalPlatform
{
    private IHeroSignalEventSink? _sink;
    private bool _handlerRegistered;

    public bool IsSupported => true;

    public void Initialize(string appId, HeroSignalOptions options, IHeroSignalEventSink sink)
    {
        _sink = sink;
        SdkLog.Writer = static (level, message) => Console.WriteLine($"[HeroSignal.Maui] {level}: {message}");
        if (!_handlerRegistered)
        {
            _handlerRegistered = true;
            HsBridge.SetEventHandler(OnBridgeEvent);
        }
        HsBridge.Start(appId, BridgeJson.OptionsJson(options));
    }

    /// <summary>Köprü olayı → ortak katman. Çözümleme hatası günlüğe yazılır, uygulamayı durdurmaz.</summary>
    private void OnBridgeEvent(string type, string json)
    {
        var sink = _sink;
        if (sink is null) return;
        try
        {
            switch (type)
            {
                case "click":
                    sink.OnNotificationClicked(BridgeJson.ParseNotificationClick(json));
                    break;
                case "foreground":
                    var (eventId, notification) = BridgeJson.ParseForeground(json);
                    sink.OnNotificationWillDisplay(new NotificationWillDisplayEventArgs(
                        notification,
                        () => HsBridge.ForegroundPreventDefault(eventId),
                        () => HsBridge.ForegroundDisplay(eventId)));
                    break;
                case "subscription":
                    sink.OnPushSubscriptionChanged(BridgeJson.ParseSubscriptionState(json));
                    break;
                case "permission":
                    sink.OnPermissionChanged(BridgeJson.ParsePermission(json));
                    break;
                case "inAppClick":
                    sink.OnInAppClicked(BridgeJson.ParseInAppClick(json));
                    break;
                case "inAppLifecycle":
                    var (phase, message) = BridgeJson.ParseInAppLifecycle(json);
                    sink.OnInAppLifecycle(phase, message);
                    break;
                default:
                    SdkLog.Write(LogLevel.Debug, $"bilinmeyen köprü olayı: {type}");
                    break;
            }
        }
        catch (Exception e)
        {
            SdkLog.Write(LogLevel.Error, $"köprü olayı işlenemedi ({type})", e);
        }
    }

    public void SetConsentGiven(bool given) => HsBridge.SetConsentGiven(given);
    public void Login(string externalId, string? identityHash) => HsBridge.Login(externalId, identityHash);
    public void Logout() => HsBridge.Logout();
    public void SetLogLevel(LogLevel level) => HsBridge.SetLogLevel((HsLogLevel)(long)level);

    // Kullanıcı
    public string? ExternalId => HsBridge.ExternalId;
    public string? UserId => HsBridge.UserId;
    public void AddTags(IReadOnlyDictionary<string, string> tags) => HsBridge.AddTagsJson(BridgeJson.ToJson(tags));
    public void RemoveTags(IReadOnlyList<string> keys) => HsBridge.RemoveTagsJson(BridgeJson.ToJson(keys));
    public IReadOnlyDictionary<string, string> GetTags() => BridgeJson.ParseStringMap(HsBridge.TagsJson);
    public void AddAliases(IReadOnlyDictionary<string, string> aliases) => HsBridge.AddAliasesJson(BridgeJson.ToJson(aliases));
    public void RemoveAlias(string label) => HsBridge.RemoveAlias(label);
    public void SetLanguage(string languageCode) => HsBridge.SetLanguage(languageCode);
    public void AddEmail(string email) => HsBridge.AddEmail(email);
    public void RemoveEmail(string email) => HsBridge.RemoveEmail(email);
    public void AddSms(string phoneNumber) => HsBridge.AddSms(phoneNumber);
    public void RemoveSms(string phoneNumber) => HsBridge.RemoveSms(phoneNumber);
    public IReadOnlyList<string> Emails => BridgeJson.ParseStringList(HsBridge.EmailsJson);
    public IReadOnlyList<string> SmsNumbers => BridgeJson.ParseStringList(HsBridge.SmsNumbersJson);

    // Push aboneliği
    public PushSubscriptionState PushSubscription => BridgeJson.ParseSubscriptionState(HsBridge.PushSubscriptionJson);
    public void OptIn() => HsBridge.OptIn();
    public void OptOut() => HsBridge.OptOut();

    // Bildirimler
    public bool NotificationPermission => HsBridge.NotificationPermission;

    public Task<bool> RequestPermissionAsync(bool fallbackToSettings)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        HsBridge.RequestPermission(fallbackToSettings, granted =>
        {
            _sink?.OnPermissionChanged(granted);
            tcs.TrySetResult(granted);
        });
        return tcs.Task;
    }

    public void ClearAllNotifications() => HsBridge.ClearAllNotifications();
    public void RemoveNotification(string id) => HsBridge.RemoveNotification(id);
    public void RemoveGroupedNotifications(string group) => HsBridge.RemoveGroupedNotifications(group);

    // In-app
    public void AddTriggers(IReadOnlyDictionary<string, string> triggers) => HsBridge.AddTriggersJson(BridgeJson.ToJson(triggers));
    public void RemoveTriggers(IReadOnlyList<string> keys) => HsBridge.RemoveTriggersJson(BridgeJson.ToJson(keys));
    public void ClearTriggers() => HsBridge.ClearTriggers();
    public IReadOnlyDictionary<string, string> GetTriggers() => BridgeJson.ParseStringMap(HsBridge.TriggersJson);
    public void SetScreen(string? name) => HsBridge.SetScreen(name);

    public bool InAppPaused
    {
        get => HsBridge.InAppPaused;
        set => HsBridge.InAppPaused = value;
    }

    // Bildirim kutusu
    public Task<InboxPage> InboxListAsync(int limit, string? cursor)
    {
        var call = new BridgeCall<InboxPage>(BridgeJson.ParseInboxPage);
        HsBridge.InboxList(limit, cursor, call.Complete);
        return call.Task;
    }

    public Task<InboxItem> InboxMarkReadAsync(string id)
    {
        var call = new BridgeCall<InboxItem>(BridgeJson.ParseInboxItem);
        HsBridge.InboxMarkRead(id, call.Complete);
        return call.Task;
    }

    public Task<int> InboxMarkAllReadAsync()
    {
        var call = new BridgeCall<int>(BridgeJson.ParseCount);
        HsBridge.InboxMarkAllRead(call.Complete);
        return call.Task;
    }

    public Task InboxArchiveAsync(string id)
    {
        var call = new BridgeCall<bool>(static _ => true);
        HsBridge.InboxArchive(id, call.Complete);
        return call.Task;
    }

    public Task<int> InboxUnreadCountAsync()
    {
        var call = new BridgeCall<int>(BridgeJson.ParseCount);
        HsBridge.InboxUnreadCount(call.Complete);
        return call.Task;
    }

    // Oturum
    public void SendEvent(string name, Dictionary<string, object?> properties, double? value) =>
        HsBridge.SendEvent(name, EventProperties.ToJson(properties), value is { } v ? NSNumber.FromDouble(v) : null);

    /// <summary>Köprü tamamlaması → Task; NSError <c>userInfo["code"]</c> ve <c>status</c> korunur.</summary>
    private sealed class BridgeCall<T>
    {
        private readonly Func<string, T> _parse;
        private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BridgeCall(Func<string, T> parse) => _parse = parse;

        public Task<T> Task => _tcs.Task;

        public void Complete(string? json, NSError? error)
        {
            if (error is not null)
            {
                var code = (error.UserInfo["code"] as NSString)?.ToString() ?? HeroSignalException.NativeError;
                var status = (error.UserInfo["status"] as NSNumber)?.Int32Value;
                _tcs.TrySetException(new HeroSignalException(code, error.LocalizedDescription) { Status = status is > 0 ? status : null });
                return;
            }
            try
            {
                _tcs.TrySetResult(_parse(json ?? "{}"));
            }
            catch (Exception e)
            {
                _tcs.TrySetException(e is HeroSignalException ? e : new HeroSignalException(HeroSignalException.InvalidResponse, "Köprü yanıtı çözümlenemedi.", e));
            }
        }
    }
}

/// <summary>Bu hedefin platform uygulaması.</summary>
internal static class PlatformFactory
{
    public static IHeroSignalPlatform Create() => new IosHeroSignalPlatform();
}

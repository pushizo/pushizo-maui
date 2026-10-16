namespace PushizoSdk.Maui.Internal;

/// <summary>
/// <see cref="INativeBridge"/> üzerinden çalışan ortak platform uygulaması (Android ve iOS aynı JSON sözleşmesini kullanır).
/// Platforma özgü kod yalnızca köprü bağdaştırıcısındadır; dönüştürme, olay yönlendirme ve hata eşleme burada test edilir.
/// </summary>
internal sealed class BridgePlatform : IPushizoPlatform
{
    private readonly INativeBridge _bridge;
    private IPushizoEventSink? _sink;

    public BridgePlatform(INativeBridge bridge) => _bridge = bridge;

    public bool IsSupported => true;

    public void Initialize(string appId, PushizoOptions options, IPushizoEventSink sink)
    {
        _sink = sink;
        _bridge.Start(appId, BridgeJson.OptionsToJson(options), OnNativeEvent);
    }

    /// <summary>Yerel olay; hatalı yük yerel geri çağrıyı bozmaz, günlüğe yazılır.</summary>
    private void OnNativeEvent(string type, string json)
    {
        var sink = _sink;
        if (sink is null) return;
        try
        {
            BridgeEvents.Dispatch(sink, type, json, _bridge);
        }
        catch (Exception e)
        {
            SdkLog.Write(LogLevel.Error, $"yerel olay işlenemedi ({type})", e);
        }
    }

    public void SetConsentGiven(bool given) => _bridge.SetConsentGiven(given);

    public void Login(string externalId, string? identityHash, string? jwt) =>
        ThrowIfFailed(_bridge.Login(externalId, identityHash, jwt));

    public void UpdateUserJwt(string jwt) => ThrowIfFailed(_bridge.UpdateUserJwt(jwt));

    /// <summary>Yerel köprünün döndürdüğü kimlik hatası (kanıt eksik, kullanıcı yok) <c>NOT_READY</c> olarak fırlatılır.</summary>
    private static void ThrowIfFailed(string? error)
    {
        if (error is not null) throw new PushizoException(PushizoException.NotReady, error);
    }

    public void Logout() => _bridge.Logout();

    public void SetLogLevel(LogLevel level) => _bridge.SetLogLevel((int)level);

    public string? ExternalId => _bridge.GetExternalId();

    public string? UserId => _bridge.GetUserId();

    public void AddTags(IReadOnlyDictionary<string, string> tags) => _bridge.AddTags(BridgeJson.ToJson(tags));

    public void RemoveTags(IReadOnlyList<string> keys) => _bridge.RemoveTags(BridgeJson.ToJson(keys));

    public IReadOnlyDictionary<string, string> GetTags() => BridgeJson.ParseStringMap(_bridge.GetTags());

    public void AddAliases(IReadOnlyDictionary<string, string> aliases) => _bridge.AddAliases(BridgeJson.ToJson(aliases));

    public void RemoveAlias(string label) => _bridge.RemoveAlias(label);

    public void SetLanguage(string languageCode) => _bridge.SetLanguage(languageCode);

    public void AddEmail(string email) => _bridge.AddEmail(email);

    public void RemoveEmail(string email) => _bridge.RemoveEmail(email);

    public void AddSms(string phoneNumber) => _bridge.AddSms(phoneNumber);

    public void RemoveSms(string phoneNumber) => _bridge.RemoveSms(phoneNumber);

    public IReadOnlyList<string> Emails => BridgeJson.ParseStringList(_bridge.GetEmails());

    public IReadOnlyList<string> SmsNumbers => BridgeJson.ParseStringList(_bridge.GetSmsNumbers());

    public Task<UserPreferences> GetPreferencesAsync() =>
        Call(r => _bridge.GetPreferences(r), json => BridgeJson.ParsePreferences(Require(json)));

    public Task<UserPreferences> SetPreferencesAsync(IReadOnlyList<PreferenceInput> preferences)
    {
        var json = BridgeJson.PreferencesToJson(preferences);
        return Call(r => _bridge.SetPreferences(json, r), result => BridgeJson.ParsePreferences(Require(result)));
    }

    public PushSubscriptionState PushSubscription => BridgeJson.ParseSubscriptionState(_bridge.GetPushSubscription());

    public void OptIn() => _bridge.OptIn();

    public void OptOut() => _bridge.OptOut();

    public bool NotificationPermission => _bridge.GetPermission();

    public Task<bool> RequestPermissionAsync(bool fallbackToSettings) =>
        AskPermission(completion => _bridge.RequestPermission(fallbackToSettings, completion));

    public void ClearAllNotifications() => _bridge.ClearAllNotifications();

    public void RemoveNotification(string id) => _bridge.RemoveNotification(id);

    public void RemoveGroupedNotifications(string group) => _bridge.RemoveGroupedNotifications(group);

    public void AddTriggers(IReadOnlyDictionary<string, string> triggers) => _bridge.AddTriggers(BridgeJson.ToJson(triggers));

    public void RemoveTriggers(IReadOnlyList<string> keys) => _bridge.RemoveTriggers(BridgeJson.ToJson(keys));

    public void ClearTriggers() => _bridge.ClearTriggers();

    public IReadOnlyDictionary<string, string> GetTriggers() => BridgeJson.ParseStringMap(_bridge.GetTriggers());

    public void SetScreen(string? name) => _bridge.SetScreen(name);

    public bool InAppPaused
    {
        get => _bridge.GetInAppPaused();
        set => _bridge.SetInAppPaused(value);
    }

    public Task<InboxPage> InboxListAsync(int limit, string? cursor) =>
        Call(r => _bridge.InboxList(limit, cursor, r), json => BridgeJson.ParseInboxPage(Require(json)));

    public Task<InboxItem> InboxMarkReadAsync(string id) =>
        Call(r => _bridge.InboxMarkRead(id, r), json => BridgeJson.ParseInboxItem(Require(json)));

    public Task<int> InboxMarkAllReadAsync() =>
        Call(r => _bridge.InboxMarkAllRead(r), json => BridgeJson.ParseUnreadCount(Require(json)));

    public Task InboxArchiveAsync(string id) =>
        Call<object?>(r => _bridge.InboxArchive(id, r), _ => null);

    public Task<int> InboxUnreadCountAsync() =>
        Call(r => _bridge.InboxUnreadCount(r), json => BridgeJson.ParseUnreadCount(Require(json)));

    public void SendEvent(string name, Dictionary<string, object?> properties, double? value) =>
        _bridge.SendEvent(name, EventProperties.ToJson(properties), value ?? 0d, value.HasValue);

    public Task SetupDefaultLiveActivityAsync() =>
        _bridge.SupportsLiveActivities
            ? Call<object?>(r => _bridge.SetupDefaultLiveActivity(r), _ => null)
            : UnsupportedPlatform.LiveActivitiesNotSupported();

    public Task StartDefaultLiveActivityAsync(string activityId, IReadOnlyDictionary<string, object> attributes, IReadOnlyDictionary<string, object> contentState)
    {
        if (!_bridge.SupportsLiveActivities) return UnsupportedPlatform.LiveActivitiesNotSupported();
        var attributesJson = BridgeJson.LiveActivityValuesToJson(attributes);
        var contentStateJson = BridgeJson.LiveActivityValuesToJson(contentState);
        return Call<object?>(r => _bridge.StartDefaultLiveActivity(activityId, attributesJson, contentStateJson, r), _ => null);
    }

    public bool LocationShared => _bridge.GetLocationShared();

    public void SetLocationShared(bool shared) => _bridge.SetLocationShared(shared);

    public Task<bool> RequestLocationPermissionAsync(string? rationale) =>
        AskPermission(completion => _bridge.RequestLocationPermission(rationale, completion));

    /// <summary>Yerel izin geri çağrısını (herhangi bir iş parçacığından, bir kez) Task'a çevirir.</summary>
    private static Task<bool> AskPermission(Action<Action<bool>> invoke)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            invoke(granted => tcs.TrySetResult(granted));
        }
        catch (Exception e)
        {
            tcs.TrySetException(e);
        }
        return tcs.Task;
    }

    private static string Require(string? json) =>
        json ?? throw new PushizoException(PushizoException.InvalidResponse, "Köprü yanıtı boş.");

    /// <summary>Yerel geri çağrıyı Task'a çevirir; hata kodu <see cref="PushizoException.Code"/> olarak taşınır.</summary>
    private static Task<T> Call<T>(Action<NativeResult> invoke, Func<string?, T> map)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            invoke((json, errorCode, errorMessage) =>
            {
                if (errorCode is not null)
                {
                    tcs.TrySetException(new PushizoException(errorCode, errorMessage ?? errorCode));
                    return;
                }
                try
                {
                    tcs.TrySetResult(map(json));
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });
        }
        catch (Exception e)
        {
            tcs.TrySetException(e);
        }
        return tcs.Task;
    }
}

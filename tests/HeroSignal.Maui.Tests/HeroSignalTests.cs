using HeroSignalSdk.Maui.Internal;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HeroSignalSdk.Maui.Tests;

/// <summary>Statik <see cref="HeroSignal"/> yüzeyi: doğrulama, platforma iletim ve olay yönlendirme.</summary>
public sealed class HeroSignalTests : IDisposable
{
    private readonly FakePlatform _platform = new();

    public HeroSignalTests() => HeroSignal.ResetForTests(_platform);

    public void Dispose() => HeroSignal.ResetForTests(new FakePlatform());

    private void Init(Action<HeroSignalOptions>? configure = null)
    {
        var options = new HeroSignalOptions { ApiUrl = "https://api.example.com" };
        configure?.Invoke(options);
        HeroSignal.Initialize("app_test", options);
    }

    [Fact]
    public void Calls_before_initialize_throw()
    {
        Assert.False(HeroSignal.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => HeroSignal.Login("u1"));
        Assert.Throws<InvalidOperationException>(() => HeroSignal.User.AddTag("a", "b"));
        Assert.Throws<InvalidOperationException>(() => HeroSignal.Notifications.ClearAll());
        // Okuma özellikleri başlatılmadan da güvenlidir.
        Assert.Null(HeroSignal.User.ExternalId);
        Assert.False(HeroSignal.Notifications.Permission);
        Assert.Empty(HeroSignal.User.GetTags());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_requires_app_id(string appId) =>
        Assert.Throws<ArgumentException>(() => HeroSignal.Initialize(appId));

    [Theory]
    [InlineData("ftp://api.example.com")]
    [InlineData("api.example.com")]
    [InlineData("")]
    public void Initialize_requires_http_api_url(string apiUrl) =>
        Assert.Throws<ArgumentException>(() => HeroSignal.Initialize("app_1", new HeroSignalOptions { ApiUrl = apiUrl }));

    [Fact]
    public void Initialize_copies_options_and_is_idempotent_for_same_app()
    {
        var options = new HeroSignalOptions { ApiUrl = "http://10.0.2.2:4000", LogLevel = LogLevel.Debug, RequiresPrivacyConsent = true };
        HeroSignal.Initialize("app_1", options);
        options.ApiUrl = "https://changed.example.com";
        HeroSignal.Initialize("app_1", options);

        Assert.True(HeroSignal.IsInitialized);
        Assert.Equal(1, _platform.InitializeCount);
        Assert.Equal("http://10.0.2.2:4000", _platform.Options!.ApiUrl);
        Assert.True(_platform.Options.RequiresPrivacyConsent);
        Assert.Equal(LogLevel.Debug, HeroSignal.Debug.LogLevel);

        HeroSignal.Initialize("app_2", options);
        Assert.Equal(2, _platform.InitializeCount);
        Assert.Equal("app_2", _platform.AppId);
    }

    [Fact]
    public void Login_validates_and_normalizes_identity_hash()
    {
        Init();
        Assert.Throws<ArgumentException>(() => HeroSignal.Login(" "));
        HeroSignal.Login("user_42");
        HeroSignal.Login("user_42", "");
        HeroSignal.Login("user_42", "hash");
        HeroSignal.Logout();
        HeroSignal.SetConsentGiven(true);
        Assert.Equal(new[] { "login:user_42:-", "login:user_42:-", "login:user_42:hash", "logout", "consent:True" }, _platform.Calls);
    }

    [Fact]
    public void User_tags_aliases_and_contacts_are_validated_and_normalized()
    {
        Init();
        HeroSignal.User.AddTag("plan", "pro");
        Assert.Equal("pro", _platform.LastTags!["plan"]);
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddTags(new Dictionary<string, string> { [" "] = "x" }));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddTags(new Dictionary<string, string> { ["k"] = null! }));

        HeroSignal.User.RemoveTags(new[] { "a", "b" });
        Assert.Equal(new[] { "a", "b" }, _platform.LastKeys);
        Assert.Throws<ArgumentException>(() => HeroSignal.User.RemoveTag(""));

        HeroSignal.User.AddAlias("crm_id", "  42 ");
        Assert.Equal("42", _platform.LastAliases!["crm_id"]);
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("external_id", "1"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAlias("CRM", "1"));
        var tooMany = Enumerable.Range(0, Validation.MaxAliasesPerRequest + 1).ToDictionary(i => $"a{i}", i => "v");
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddAliases(tooMany));

        HeroSignal.User.AddEmail(" Ali@Example.COM ");
        HeroSignal.User.AddSms("+90 (532) 123-45-67");
        HeroSignal.User.RemoveSms("00905321234567");
        HeroSignal.User.SetLanguage(" tr ");
        Assert.Contains("addEmail:ali@example.com", _platform.Calls);
        Assert.Contains("addSms:+905321234567", _platform.Calls);
        Assert.Contains("removeSms:+905321234567", _platform.Calls);
        Assert.Contains("language:tr", _platform.Calls);
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddEmail("geçersiz"));
        Assert.Throws<ArgumentException>(() => HeroSignal.User.AddSms("123"));

        Assert.Equal("user_1", HeroSignal.User.ExternalId);
        Assert.Equal("pro", HeroSignal.User.GetTags()["plan"]);
        Assert.Single(HeroSignal.User.Emails);
    }

    [Fact]
    public void Push_subscription_changed_fires_only_on_real_change_and_updates_permission()
    {
        Init();
        var changes = new List<PushSubscriptionChangedEventArgs>();
        var permissions = new List<bool>();
        HeroSignal.User.PushSubscription.Changed += (_, e) => changes.Add(e);
        HeroSignal.Notifications.PermissionChanged += (_, e) => permissions.Add(e.Permission);

        var state = new PushSubscriptionState("sub_1", "tok", true, true);
        _platform.Sink!.OnPushSubscriptionChanged(state);
        _platform.Sink.OnPushSubscriptionChanged(state with { });
        _platform.Sink.OnPushSubscriptionChanged(state with { OptedIn = false, Permission = false });

        Assert.Equal(2, changes.Count);
        Assert.Null(changes[0].Previous.Id);
        Assert.Equal("sub_1", changes[0].Current.Id);
        Assert.False(changes[1].Current.OptedIn);
        Assert.Equal(new[] { true, false }, permissions);

        HeroSignal.User.PushSubscription.OptIn();
        HeroSignal.User.PushSubscription.OptOut();
        Assert.Equal(new[] { "optIn", "optOut" }, _platform.Calls);
        Assert.Equal("tok", HeroSignal.User.PushSubscription.Token);
    }

    [Fact]
    public async Task Request_permission_is_forwarded()
    {
        Init();
        _platform.PermissionResult = false;
        Assert.False(await HeroSignal.Notifications.RequestPermissionAsync(fallbackToSettings: true));
        Assert.Equal("requestPermission:True", _platform.Calls.Single());
    }

    [Fact]
    public void Notification_tray_methods_validate_and_forward()
    {
        Init();
        HeroSignal.Notifications.ClearAll();
        HeroSignal.Notifications.RemoveNotification(42);
        HeroSignal.Notifications.RemoveNotification(" dlv_1 ");
        HeroSignal.Notifications.RemoveGroupedNotifications("orders");
        Assert.Equal(new[] { "clearAll", "removeNotification:42", "removeNotification:dlv_1", "removeGroup:orders" }, _platform.Calls);
        Assert.Throws<ArgumentException>(() => HeroSignal.Notifications.RemoveNotification(""));
        Assert.Throws<ArgumentException>(() => HeroSignal.Notifications.RemoveGroupedNotifications(" "));
    }

    [Fact]
    public void Click_before_subscription_is_delivered_to_first_handler_only()
    {
        Init();
        var click = new NotificationClickedEventArgs(new HeroSignalNotification { Title = "T" }, "btn", "https://x");
        _platform.Sink!.OnNotificationClicked(click);

        var first = new List<NotificationClickedEventArgs>();
        var second = new List<NotificationClickedEventArgs>();
        HeroSignal.Notifications.Clicked += (_, e) => first.Add(e);
        HeroSignal.Notifications.Clicked += (_, e) => second.Add(e);

        Assert.Same(click, Assert.Single(first));
        Assert.Empty(second);

        var next = new NotificationClickedEventArgs(new HeroSignalNotification(), null, null);
        _platform.Sink.OnNotificationClicked(next);
        Assert.Equal(2, first.Count);
        Assert.Same(next, Assert.Single(second));
    }

    [Fact]
    public void Handler_exception_does_not_stop_other_handlers()
    {
        Init();
        var reached = false;
        HeroSignal.Notifications.Clicked += (_, _) => throw new InvalidOperationException("boom");
        HeroSignal.Notifications.Clicked += (_, _) => reached = true;
        _platform.Sink!.OnNotificationClicked(new NotificationClickedEventArgs(new HeroSignalNotification(), null, null));
        Assert.True(reached);
    }

    [Fact]
    public void Foreground_prevent_default_and_display_reach_native_callbacks()
    {
        Init();
        var prevented = 0;
        var displayed = 0;
        HeroSignal.Notifications.ForegroundWillDisplay += (_, e) =>
        {
            e.PreventDefault();
            e.PreventDefault();
            e.Display();
        };
        var args = new NotificationWillDisplayEventArgs(new HeroSignalNotification(), () => prevented++, () => displayed++);
        _platform.Sink!.OnNotificationWillDisplay(args);
        Assert.True(args.IsPrevented);
        Assert.Equal(1, prevented);
        Assert.Equal(1, displayed);
    }

    [Fact]
    public void In_app_triggers_and_events_are_routed()
    {
        Init();
        HeroSignal.InAppMessages.AddTrigger("level", "gold");
        HeroSignal.InAppMessages.AddTriggers(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        HeroSignal.InAppMessages.RemoveTrigger("a");
        Assert.Equal(new Dictionary<string, string> { ["level"] = "gold", ["b"] = "2" }, HeroSignal.InAppMessages.GetTriggers());
        HeroSignal.InAppMessages.ClearTriggers();
        Assert.Empty(HeroSignal.InAppMessages.GetTriggers());
        Assert.Throws<ArgumentException>(() => HeroSignal.InAppMessages.AddTrigger(new string('k', 65), "v"));
        Assert.Throws<ArgumentException>(() => HeroSignal.InAppMessages.AddTrigger("k", new string('v', 201)));

        HeroSignal.InAppMessages.SetScreen(" cart ");
        HeroSignal.InAppMessages.SetScreen("  ");
        Assert.Equal(new[] { "screen:cart", "screen:-" }, _platform.Calls);

        HeroSignal.InAppMessages.Paused = true;
        Assert.True(HeroSignal.InAppMessages.Paused);

        var phases = new List<string>();
        HeroSignal.InAppMessages.WillDisplay += (_, e) => phases.Add("will:" + e.Message.Id);
        HeroSignal.InAppMessages.DidDismiss += (_, e) => phases.Add("dismiss:" + e.Message.Id);
        InAppMessageClickedEventArgs? clicked = null;
        HeroSignal.InAppMessages.Clicked += (_, e) => clicked = e;
        _platform.Sink!.OnInAppLifecycle(InAppLifecyclePhase.WillDisplay, new InAppMessage("m1", null));
        _platform.Sink.OnInAppLifecycle(InAppLifecyclePhase.DidDisplay, new InAppMessage("m1", null));
        _platform.Sink.OnInAppLifecycle(InAppLifecyclePhase.DidDismiss, new InAppMessage("m1", null));
        _platform.Sink.OnInAppClicked(new InAppMessageClickedEventArgs("m1", "buy", "https://x"));
        Assert.Equal(new[] { "will:m1", "dismiss:m1" }, phases);
        Assert.Equal("buy", clicked!.ActionId);
    }

    [Fact]
    public async Task Inbox_validates_and_forwards()
    {
        Init();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => HeroSignal.Inbox.ListAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => HeroSignal.Inbox.ListAsync(201));
        await HeroSignal.Inbox.ListAsync(20, "");
        Assert.Equal((20, (string?)null), _platform.LastInboxList);
        await HeroSignal.Inbox.ListAsync(cursor: "c1");
        Assert.Equal((50, "c1"), _platform.LastInboxList);
        Assert.True((await HeroSignal.Inbox.MarkReadAsync("i1")).Read);
        Assert.Throws<ArgumentException>(() => { _ = HeroSignal.Inbox.MarkReadAsync(" "); });
        await HeroSignal.Inbox.ArchiveAsync("i2");
        Assert.Equal(3, await HeroSignal.Inbox.UnreadCountAsync());
        Assert.Contains("archive:i2", _platform.Calls);
    }

    [Fact]
    public async Task Inbox_reports_not_initialized_and_unsupported_platform()
    {
        var e = await Assert.ThrowsAsync<HeroSignalException>(() => HeroSignal.Inbox.UnreadCountAsync());
        Assert.Equal(HeroSignalException.NotInitialized, e.Code);

        _platform.IsSupported = false;
        Init();
        e = await Assert.ThrowsAsync<HeroSignalException>(() => HeroSignal.Inbox.ListAsync());
        Assert.Equal(HeroSignalException.PlatformNotSupported, e.Code);
    }

    [Fact]
    public void Session_send_event_validates_name_value_and_normalizes_properties()
    {
        Init();
        HeroSignal.Session.SendEvent(" purchase ", new Dictionary<string, object?>
        {
            ["amount"] = 12.5m,
            ["count"] = 3,
            ["tags"] = new[] { "a", "b" },
            ["nested"] = new Dictionary<string, object?> { ["ok"] = true },
            ["missing"] = null,
        }, 99.9);

        Assert.Equal("event:purchase", _platform.Calls.Single());
        var props = _platform.LastEventProperties!;
        Assert.Equal(12.5, props["amount"]);
        Assert.Equal(3L, props["count"]);
        Assert.Equal(new List<object?> { "a", "b" }, props["tags"]);
        Assert.Equal(true, ((Dictionary<string, object?>)props["nested"]!)["ok"]);
        Assert.Null(props["missing"]);
        Assert.Equal(99.9, _platform.LastEventValue);

        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent("bad name"));
        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent(new string('e', 101)));
        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent("ok", value: double.NaN));
        Assert.Throws<ArgumentException>(() => HeroSignal.Session.SendEvent("ok", new Dictionary<string, object?> { ["s"] = new MemoryStream() }));
    }

    [Fact]
    public void Debug_set_log_level_updates_sdk_and_platform()
    {
        HeroSignal.Debug.SetLogLevel(LogLevel.Info);
        Assert.Equal(LogLevel.Info, HeroSignal.Debug.LogLevel);
        Assert.Null(_platform.LastLogLevel);

        Init(o => o.LogLevel = LogLevel.Warn);
        HeroSignal.Debug.SetLogLevel(LogLevel.Debug);
        Assert.Equal(LogLevel.Debug, _platform.LastLogLevel);
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroSignal.Debug.SetLogLevel((LogLevel)42));
    }
}

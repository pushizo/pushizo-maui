namespace HeroSignalSdk.Maui.Tests;

public sealed class EventTests : HeroSignalTestBase
{
    private const string Notification =
        """{"title":"Kargo","body":"Yolda","imageUrl":null,"url":"app://orders/1","deliveryId":"dlv_1","messageId":"msg_1","actions":[{"id":"track","title":"Takip","url":"app://track","icon":null}],"data":{"order":"1"}}""";

    [Fact]
    public void Click_is_dispatched_with_notification_details()
    {
        InitializeSdk();
        NotificationClickedEventArgs? received = null;
        HeroSignal.Notifications.Clicked += (_, e) => received = e;

        Bridge.Emit("click", $$"""{"notification":{{Notification}},"actionId":"track","url":"app://track"}""");

        Assert.NotNull(received);
        Assert.Equal("track", received!.ActionId);
        Assert.Equal("app://track", received.Url);
        Assert.Equal("Kargo", received.Notification.Title);
        Assert.Equal("dlv_1", received.Notification.DeliveryId);
        Assert.Equal("1", received.Notification.Data["order"]);
        var action = Assert.Single(received.Notification.Actions);
        Assert.Equal("Takip", action.Title);
    }

    [Fact]
    public void Click_before_subscription_is_replayed_once_to_first_subscriber()
    {
        InitializeSdk();
        Bridge.Emit("click", $$"""{"notification":{{Notification}},"actionId":null,"url":"app://orders/1"}""");

        var first = new List<NotificationClickedEventArgs>();
        var second = new List<NotificationClickedEventArgs>();
        HeroSignal.Notifications.Clicked += (_, e) => first.Add(e);
        HeroSignal.Notifications.Clicked += (_, e) => second.Add(e);

        Assert.Single(first);
        Assert.Null(first[0].ActionId);
        Assert.Empty(second);
    }

    [Fact]
    public void Foreground_prevent_default_and_display_are_forwarded_synchronously()
    {
        InitializeSdk();
        NotificationWillDisplayEventArgs? captured = null;
        HeroSignal.Notifications.ForegroundWillDisplay += (_, e) =>
        {
            captured = e;
            e.PreventDefault();
            e.PreventDefault();
        };

        Bridge.Emit("foreground", $$"""{"eventId":"evt_1","notification":{{Notification}}}""");

        // Karar, köprü çağrısı dönmeden kaydedilmiş olmalı (yerel SDK gösterimden hemen önce okur).
        Assert.Equal(new[] { "preventDefault:evt_1" }, Bridge.Calls);
        Assert.True(captured!.IsPrevented);
        Assert.Equal("Yolda", captured.Notification.Body);

        captured.Display();
        Assert.Equal("display:evt_1", Bridge.Calls[^1]);
    }

    [Fact]
    public void Foreground_without_handlers_does_not_touch_bridge()
    {
        InitializeSdk();
        Bridge.Emit("foreground", $$"""{"eventId":"evt_2","notification":{{Notification}}}""");
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public void Subscription_and_permission_events_fire_only_on_change()
    {
        Bridge.SubscriptionJson = """{"id":null,"token":null,"optedIn":false,"permission":false}""";
        Bridge.PermissionValue = false;
        InitializeSdk();

        var changes = new List<PushSubscriptionChangedEventArgs>();
        var permissions = new List<bool>();
        HeroSignal.User.PushSubscription.Changed += (_, e) => changes.Add(e);
        HeroSignal.Notifications.PermissionChanged += (_, e) => permissions.Add(e.Permission);

        // Başlangıç durumuyla aynı: olay yok.
        Bridge.Emit("subscription", """{"id":null,"token":null,"optedIn":false,"permission":false}""");
        Assert.Empty(changes);

        Bridge.Emit("subscription", """{"id":"sub_1","token":"tok","optedIn":true,"permission":true}""");
        Bridge.Emit("subscription", """{"id":"sub_1","token":"tok","optedIn":true,"permission":true}""");
        Bridge.Emit("subscription", """{"id":"sub_1","token":"tok2","optedIn":true,"permission":true}""");
        Bridge.Emit("subscription", """{"id":"sub_1","token":"tok2","optedIn":false,"permission":false}""");

        Assert.Equal(3, changes.Count);
        Assert.Null(changes[0].Previous.Id);
        Assert.Equal("sub_1", changes[0].Current.Id);
        Assert.Equal("tok2", changes[1].Current.Token);
        Assert.Equal(new[] { true, false }, permissions);
    }

    [Fact]
    public void In_app_click_and_lifecycle_events_are_mapped()
    {
        InitializeSdk();
        InAppMessageClickedEventArgs? click = null;
        var phases = new List<string>();
        HeroSignal.InAppMessages.Clicked += (_, e) => click = e;
        HeroSignal.InAppMessages.WillDisplay += (_, e) => phases.Add("will:" + e.Message.Id);
        HeroSignal.InAppMessages.DidDisplay += (_, e) => phases.Add("did:" + e.Message.Id);
        HeroSignal.InAppMessages.WillDismiss += (_, _) => phases.Add("willDismiss");
        HeroSignal.InAppMessages.DidDismiss += (_, e) => phases.Add("didDismiss:" + e.Message.Json);

        Bridge.Emit("inAppClick", """{"messageId":"iam_1","actionId":"buy","url":"https://shop.example.com"}""");
        Bridge.Emit("inAppLifecycle", """{"event":"willDisplay","messageId":"iam_1","json":"{\"id\":\"iam_1\"}"}""");
        Bridge.Emit("inAppLifecycle", """{"event":"didDisplay","messageId":"iam_1","json":null}""");
        Bridge.Emit("inAppLifecycle", """{"event":"willDismiss","messageId":"iam_1"}""");
        Bridge.Emit("inAppLifecycle", """{"event":"didDismiss","messageId":"iam_1","json":"{}"}""");
        Bridge.Emit("inAppLifecycle", """{"event":"somethingNew","messageId":"iam_1"}""");

        Assert.Equal("iam_1", click!.MessageId);
        Assert.Equal("buy", click.ActionId);
        Assert.Equal("https://shop.example.com", click.Url);
        Assert.Equal(new[] { "will:iam_1", "did:iam_1", "willDismiss", "didDismiss:{}" }, phases);
    }

    [Fact]
    public void Faulty_handlers_and_payloads_never_reach_native_callback()
    {
        InitializeSdk();
        var reached = false;
        HeroSignal.Notifications.Clicked += (_, _) => throw new InvalidOperationException("uygulama hatası");
        HeroSignal.Notifications.Clicked += (_, _) => reached = true;

        Bridge.Emit("click", $$"""{"notification":{{Notification}}}""");
        Assert.True(reached);

        // Bozuk JSON, eksik alan ve bilinmeyen tür köprüye istisna olarak dönmez.
        Bridge.Emit("click", "{bozuk");
        Bridge.Emit("inAppClick", """{"actionId":"x"}""");
        Bridge.Emit("unknownEvent", "{}");
    }

    [Fact]
    public async Task Request_permission_returns_bridge_result()
    {
        InitializeSdk();
        Bridge.PermissionResult = true;
        Assert.True(await HeroSignal.Notifications.RequestPermissionAsync());
        Bridge.PermissionResult = false;
        Assert.False(await HeroSignal.Notifications.RequestPermissionAsync(fallbackToSettings: true));
        Assert.Equal(new[] { "requestPermission:False", "requestPermission:True" }, Bridge.Calls);

        HeroSignal.Notifications.ClearAll();
        Assert.Equal("clearAll", Bridge.Calls[^1]);
    }
}

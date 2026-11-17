using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>
/// Yerel SDK eşitliği: profille giriş, reklam rızası ve reklam kimliği (ADR-0037), rozet, konum izin durumu ve arka plan
/// konum izni, ADM kaydı, sohbet dinleme durumu, kullanıcı özelliği, özellik bayrağı ve bildirim kutusu gözlemcileri,
/// olay tekilleştirme kimliği, hazır sohbet arayüzü. Köprüye iletim, doğrulama ve desteklenmeyen hedefler sınanır.
/// </summary>
public sealed class NativeParityTests : PushizoTestBase
{
    [Fact]
    public void Login_with_profile_normalizes_and_reaches_bridge()
    {
        InitializeSdk();

        Pushizo.Login(
            "user_42",
            new PushizoUserProfile
            {
                Email = " Ali@Ornek.com ",
                Sms = "+90 (532) 123-45-67",
                Tags = new Dictionary<string, string> { ["plan"] = "pro" },
                Aliases = new Dictionary<string, string> { ["crm_id"] = "42" },
            },
            jwt: "token");

        // JSON yazıcısı "+" işaretini \u002B olarak kaçışlar; içerik ayrıştırılarak karşılaştırılır.
        var call = Assert.Single(Bridge.Calls);
        Assert.StartsWith("loginWithProfile:user_42:{", call);
        Assert.EndsWith("}:<null>:token", call);
        using var profile = System.Text.Json.JsonDocument.Parse(call["loginWithProfile:user_42:".Length..^":<null>:token".Length]);
        var root = profile.RootElement;
        Assert.Equal("ali@ornek.com", root.GetProperty("email").GetString());
        Assert.Equal("+905321234567", root.GetProperty("sms").GetString());
        Assert.Equal("pro", root.GetProperty("tags").GetProperty("plan").GetString());
        Assert.Equal("42", root.GetProperty("aliases").GetProperty("crm_id").GetString());
    }

    [Fact]
    public void Login_with_empty_profile_sends_empty_object_and_plain_login_is_unchanged()
    {
        InitializeSdk();

        Pushizo.Login("user_1", new PushizoUserProfile(), identityHash: "hash");
        // Mevcut çağrı biçimleri (null kanıt dahil) profil aşırı yüklemesine kaymaz.
        Pushizo.Login("user_2", null, "token");

        Assert.Equal(new[] { "loginWithProfile:user_1:{}:hash:<null>", "login:user_2:<null>:jwt=token" }, Bridge.Calls);
    }

    [Fact]
    public void Login_with_invalid_profile_fails_before_login()
    {
        InitializeSdk();

        Assert.Throws<ArgumentException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Email = "gecersiz" }));
        Assert.Throws<ArgumentException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Sms = "abc" }));
        Assert.Throws<ArgumentException>(() => Pushizo.Login(" ", new PushizoUserProfile { Email = "ali@ornek.com" }));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public void Login_with_profile_surfaces_native_error()
    {
        InitializeSdk();
        Bridge.LoginError = "identityHash gerekli";

        var error = Assert.Throws<PushizoException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" }));

        Assert.Equal(PushizoException.NotReady, error.Code);
    }

    [Fact]
    public async Task Ads_consent_returns_preferences_and_consents_are_parsed()
    {
        InitializeSdk();
        Bridge.PreferencesResponse = ("""{"preferences":[{"category":"ads_personalization","channel":null,"optedIn":true,"explicit":true,"source":"sdk","updatedAt":1759838400000}],"categories":[]}""", null, null);
        Bridge.ConsentsResponse = ("""
            {"consents":[
              {"purpose":"ads_personalization","granted":true,"source":"sdk","updatedAt":1759838400000},
              {"purpose":"other","granted":false,"source":null,"updatedAt":null},
              {"granted":true}
            ]}
            """, null, null);

        var preferences = await Pushizo.User.SetAdsPersonalizationConsentAsync(true);
        var consents = await Pushizo.User.GetConsentsAsync();

        Assert.Equal(new[] { "setAdsPersonalizationConsent:True", "getConsents" }, Bridge.Calls);
        Assert.Equal("ads_personalization", Assert.Single(preferences.Preferences).Category);
        // Amaçsız kayıt atlanır.
        Assert.Equal(2, consents.Count);
        Assert.Equal("ads_personalization", consents[0].Purpose);
        Assert.True(consents[0].Granted);
        Assert.Equal("sdk", consents[0].Source);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759838400000), consents[0].UpdatedAt);
        Assert.False(consents[1].Granted);
        Assert.Null(consents[1].Source);
        Assert.Null(consents[1].UpdatedAt);
    }

    [Fact]
    public async Task Consent_errors_keep_native_code()
    {
        InitializeSdk();
        Bridge.ConsentsResponse = (null, "NOT_READY", "Abonelik henüz kaydedilmedi");
        Bridge.PreferencesResponse = (null, "NOT_READY", "Abonelik henüz kaydedilmedi");

        var get = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetConsentsAsync());
        var set = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.SetAdsPersonalizationConsentAsync(false));

        Assert.Equal(PushizoException.NotReady, get.Code);
        Assert.Equal(PushizoException.NotReady, set.Code);
    }

    [Theory]
    [InlineData("38400000-8cf0-11bd-b23e-10b96e40000d", "38400000-8cf0-11bd-b23e-10b96e40000d")]
    [InlineData("  38400000-8cf0-11bd-b23e-10b96e40000d ", "38400000-8cf0-11bd-b23e-10b96e40000d")]
    [InlineData(null, "<null>")]
    [InlineData("  ", "<null>")]
    public void Advertising_id_reaches_bridge(string? id, string expected)
    {
        InitializeSdk();

        Pushizo.Ads.SetAdvertisingId(id);

        Assert.Equal(new[] { $"advertisingId:{expected}" }, Bridge.Calls);
    }

    [Fact]
    public void Badge_count_is_validated_and_clear_sends_zero()
    {
        InitializeSdk();

        Pushizo.Notifications.SetBadgeCount(3);
        Pushizo.Notifications.ClearBadge();
        Assert.Throws<ArgumentOutOfRangeException>(() => Pushizo.Notifications.SetBadgeCount(-1));

        Assert.Equal(new[] { "badge:3", "badge:0" }, Bridge.Calls);
    }

    [Fact]
    public void Try_set_badge_count_returns_native_result_and_badge_count_reads_native_value()
    {
        Assert.Equal(0, Pushizo.Notifications.BadgeCount);
        InitializeSdk();

        Assert.True(Pushizo.Notifications.TrySetBadgeCount(5));
        Assert.Equal(5, Pushizo.Notifications.BadgeCount);
        // Android: rozet gösterilemiyor (bildirimler kapalı, Android 8 öncesi) ama sayı yine saklanır.
        Bridge.BadgeResult = false;
        Assert.False(Pushizo.Notifications.TrySetBadgeCount(2));
        Assert.Equal(2, Pushizo.Notifications.BadgeCount);
        // Sonuç döndürmeyen mevcut çağrı aynı köprü yoluna gider ve false sonuçta hata fırlatmaz.
        Pushizo.Notifications.SetBadgeCount(7);
        Pushizo.Notifications.ClearBadge();
        Assert.Equal(0, Pushizo.Notifications.BadgeCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => Pushizo.Notifications.TrySetBadgeCount(-1));

        Assert.Equal(new[] { "badge:5", "badge:2", "badge:7", "badge:0" }, Bridge.Calls);
    }

    [Fact]
    public void Set_property_normalizes_value_and_reaches_bridge()
    {
        InitializeSdk();

        Pushizo.User.SetProperty(" plan ", "pro");
        Pushizo.User.SetProperty("age", 34);
        Pushizo.User.SetProperty("score", 4.5);
        Pushizo.User.SetProperty("vip", true);
        Pushizo.User.SetProperty("interests", new[] { "spor", "müzik" });
        Pushizo.User.SetProperty("address", new Dictionary<string, object?> { ["city"] = "İzmir", ["zip"] = 35000 });
        Pushizo.User.SetProperty("plan", null);

        Assert.Equal(7, Bridge.Calls.Count);
        Assert.Equal("""setUserProperty:plan:{"value":"pro"}""", Bridge.Calls[0]);
        Assert.Equal("""setUserProperty:age:{"value":34}""", Bridge.Calls[1]);
        Assert.Equal("""setUserProperty:score:{"value":4.5}""", Bridge.Calls[2]);
        Assert.Equal("""setUserProperty:vip:{"value":true}""", Bridge.Calls[3]);
        using (var list = System.Text.Json.JsonDocument.Parse(Bridge.Calls[4]["setUserProperty:interests:".Length..]))
        {
            Assert.Equal(new[] { "spor", "müzik" }, list.RootElement.GetProperty("value").EnumerateArray().Select(e => e.GetString()));
        }
        using (var map = System.Text.Json.JsonDocument.Parse(Bridge.Calls[5]["setUserProperty:address:".Length..]))
        {
            Assert.Equal("İzmir", map.RootElement.GetProperty("value").GetProperty("city").GetString());
            Assert.Equal(35000, map.RootElement.GetProperty("value").GetProperty("zip").GetInt32());
        }
        // null özelliği siler: köprüye JSON null gider.
        Assert.Equal("""setUserProperty:plan:{"value":null}""", Bridge.Calls[6]);
    }

    [Fact]
    public void Set_property_rejects_invalid_key_and_value_and_maps_native_error()
    {
        InitializeSdk();

        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty("  ", "x"));
        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty(null!, "x"));
        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty(new string('k', UserManager.MaxPropertyKeyLength + 1), "x"));
        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty("obj", new object()));
        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty("nan", double.NaN));
        // İç içe null iOS'ta desteklenmez; iki platformda da reddedilir.
        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty("list", new object?[] { "a", null }));
        Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty("map", new Dictionary<string, object?> { ["a"] = null }));
        Assert.Empty(Bridge.Calls);

        Pushizo.User.SetProperty(new string('k', UserManager.MaxPropertyKeyLength), "x");
        // Android yerel SDK'sının anahtar hatası istisna olarak taşınır.
        Bridge.UserPropertyError = "Özellik anahtarı boş olamaz.";
        var error = Assert.Throws<ArgumentException>(() => Pushizo.User.SetProperty("plan", "pro"));
        Assert.StartsWith("Özellik anahtarı boş olamaz.", error.Message);
        Assert.Equal(2, Bridge.Calls.Count);
    }

    [Fact]
    public void Feature_flags_changed_event_follows_native_observer()
    {
        InitializeSdk();
        var changes = 0;
        EventHandler<FeatureFlagsChangedEventArgs> handler = (_, _) => changes++;
        Pushizo.FeatureFlags.Changed += handler;
        // Bir işleyicinin hatası diğerlerini ve yerel geri çağrıyı bozmaz.
        Pushizo.FeatureFlags.Changed += (_, _) => throw new InvalidOperationException("işleyici hatası");

        Bridge.Emit("featureFlags", "{}");
        Bridge.Emit("featureFlags", "{}");
        Assert.Equal(2, changes);

        // Gözlemciyi kaldırma (yerel removeObserver karşılığı).
        Pushizo.FeatureFlags.Changed -= handler;
        Bridge.Emit("featureFlags", "{}");
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Inbox_changed_event_parses_native_change_events()
    {
        InitializeSdk();
        var events = new List<InboxChangedEventArgs>();
        EventHandler<InboxChangedEventArgs> handler = (_, e) => events.Add(e);
        Pushizo.Inbox.Changed += handler;

        // Android `HsInboxChangeEvent.toMap()` itemId'yi null yazar; iOS `dictionary` alanı hiç yazmaz.
        Bridge.Emit("inbox", """{"type":"listed","itemId":null,"unreadCount":4}""");
        Bridge.Emit("inbox", """{"type":"read","itemId":"inb_1","unreadCount":3}""");
        Bridge.Emit("inbox", """{"type":"read_all","unreadCount":0}""");
        Bridge.Emit("inbox", """{"type":"archived","itemId":"inb_2","unreadCount":0}""");
        Bridge.Emit("inbox", """{"type":"count","unreadCount":2}""");
        // Bilinmeyen tür (daha yeni yerel SDK) ve bozuk yük yok sayılır.
        Bridge.Emit("inbox", """{"type":"pinned","unreadCount":9}""");
        Bridge.Emit("inbox", "bozuk");

        Assert.Equal(
            new[] { InboxChangeType.Listed, InboxChangeType.Read, InboxChangeType.ReadAll, InboxChangeType.Archived, InboxChangeType.Count },
            events.Select(e => e.Type));
        Assert.Equal(new string?[] { null, "inb_1", null, "inb_2", null }, events.Select(e => e.ItemId));
        Assert.Equal(new[] { 4, 3, 0, 0, 2 }, events.Select(e => e.UnreadCount));

        Pushizo.Inbox.Changed -= handler;
        Bridge.Emit("inbox", """{"type":"count","unreadCount":1}""");
        Assert.Equal(5, events.Count);
    }

    [Fact]
    public void Send_event_passes_optional_event_id()
    {
        InitializeSdk();

        Pushizo.Session.SendEvent("purchase", new Dictionary<string, object?> { ["sku"] = "A1" }, 9.99, " ord_2024-0001 ");
        Assert.Equal("ord_2024-0001", Bridge.LastEventId);
        var (name, json, value, hasValue) = Bridge.LastEvent!.Value;
        Assert.Equal("purchase", name);
        Assert.Equal("""{"sku":"A1"}""", json);
        Assert.Equal(9.99, value);
        Assert.True(hasValue);

        // Boş kimlik gönderilmez; biçim denetimi yerel SDK'dadır (geçersizse kendi kimliğini üretir), köprü olduğu gibi iletir.
        Pushizo.Session.SendEvent("purchase", null, null, "  ");
        Assert.Null(Bridge.LastEventId);
        Pushizo.Session.SendEvent("purchase", null, null, "kısa");
        Assert.Equal("kısa", Bridge.LastEventId);
        // Mevcut çağrı biçimleri kimliksiz kalır.
        Pushizo.Session.SendEvent("purchase", value: 1);
        Assert.Null(Bridge.LastEventId);
        Pushizo.Session.SendEvent("purchase", new Dictionary<string, object?>(), 2);
        Assert.Null(Bridge.LastEventId);

        Assert.Throws<ArgumentException>(() => Pushizo.Session.SendEvent(" ", null, null, "ord_2024-0001"));
        Assert.Throws<ArgumentException>(() => Pushizo.Session.SendEvent("x", null, double.NaN, "ord_2024-0001"));
        Assert.Equal(5, Bridge.Calls.Count);
    }

    [Fact]
    public void Chat_show_hide_and_is_presented_reach_bridge()
    {
        Assert.False(Pushizo.Chat.IsPresented);
        InitializeSdk();

        Assert.False(Pushizo.Chat.Hide());
        Pushizo.Chat.Show();
        Assert.True(Pushizo.Chat.IsPresented);
        Assert.True(Pushizo.Chat.Hide());
        Assert.False(Pushizo.Chat.IsPresented);

        Assert.Equal(new[] { "chatHide", "chatShow", "chatHide" }, Bridge.Calls);
    }

    [Fact]
    public async Task Location_permission_state_and_background_request_reach_bridge()
    {
        Assert.False(Pushizo.Location.PermissionGranted);
        Assert.False(Pushizo.Location.BackgroundPermissionGranted);
        InitializeSdk();
        Bridge.LocationPermissionGrantedValue = true;
        Assert.True(Pushizo.Location.PermissionGranted);
        Assert.False(Pushizo.Location.BackgroundPermissionGranted);
        Bridge.LocationBackgroundPermissionGrantedValue = true;
        Assert.True(Pushizo.Location.BackgroundPermissionGranted);

        Bridge.BackgroundLocationPermissionResult = false;
        Assert.False(await Pushizo.Location.RequestBackgroundPermissionAsync("  Mağazaya vardığınızda haber vermek için  "));
        Assert.Equal("Mağazaya vardığınızda haber vermek için", Bridge.LastLocationRationale);
        Bridge.BackgroundLocationPermissionResult = true;
        // "Her zaman" izni aynı kavramdır: aynı köprü çağrısına gider, açıklama metni yoktur.
        Assert.True(await Pushizo.Location.RequestAlwaysPermissionAsync());
        Assert.Null(Bridge.LastLocationRationale);

        Assert.Equal(new[] { "requestBackgroundLocationPermission", "requestBackgroundLocationPermission" }, Bridge.Calls);
    }

    [Fact]
    public void Register_adm_trims_validates_and_maps_native_error()
    {
        InitializeSdk();

        Pushizo.User.RegisterAdm("  amzn1.adm-registration.v3.abc  ");
        Assert.Throws<ArgumentException>(() => Pushizo.User.RegisterAdm("  "));
        Bridge.RegisterAdmError = "ADM registration id";
        Assert.Throws<ArgumentException>(() => Pushizo.User.RegisterAdm("x"));

        Assert.Equal(new[] { "registerAdm:amzn1.adm-registration.v3.abc", "registerAdm:x" }, Bridge.Calls);
    }

    [Fact]
    public async Task Chat_is_listening_reads_native_state()
    {
        InitializeSdk();
        Bridge.ChatResponses["isListening"] = ("""{"listening":true}""", null, null);

        Assert.True(await Pushizo.Chat.IsListeningAsync());
        Assert.Equal("isListening", Bridge.ChatCalls[^1].Method);

        Bridge.ChatResponses["isListening"] = ("""{"listening":false}""", null, null);
        Assert.False(await Pushizo.Chat.IsListeningAsync());
    }

    [Fact]
    public void New_calls_require_initialize()
    {
        Assert.Throws<InvalidOperationException>(() => Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" }));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Ads.SetAdvertisingId("id"));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Notifications.SetBadgeCount(1));
        Assert.Throws<InvalidOperationException>(() => Pushizo.User.RegisterAdm("id"));
        Assert.Throws<InvalidOperationException>(() => { _ = Pushizo.Location.RequestBackgroundPermissionAsync(); });
        Assert.Throws<InvalidOperationException>(() => Pushizo.User.SetProperty("plan", "pro"));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Notifications.TrySetBadgeCount(1));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Session.SendEvent("purchase", null, null, "ord_2024-0001"));
        Assert.Throws<InvalidOperationException>(() => Pushizo.Chat.Show());
        Assert.Throws<InvalidOperationException>(() => Pushizo.Chat.Hide());
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_is_a_safe_no_op()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Pushizo.Login("user_42", new PushizoUserProfile { Email = "ali@ornek.com" });
        Pushizo.Ads.SetAdvertisingId("38400000-8cf0-11bd-b23e-10b96e40000d");
        Pushizo.Notifications.SetBadgeCount(2);
        Pushizo.Notifications.ClearBadge();
        Pushizo.User.RegisterAdm("amzn1.adm-registration.v3.abc");
        Assert.False(Pushizo.Location.PermissionGranted);
        Assert.False(Pushizo.Location.BackgroundPermissionGranted);
        Assert.False(await Pushizo.Location.RequestBackgroundPermissionAsync("açıklama"));
        Assert.False(await Pushizo.Location.RequestAlwaysPermissionAsync());
        Assert.False(await Pushizo.Chat.IsListeningAsync());
        Pushizo.User.SetProperty("plan", "pro");
        Assert.False(Pushizo.Notifications.TrySetBadgeCount(2));
        Assert.Equal(0, Pushizo.Notifications.BadgeCount);
        Pushizo.Session.SendEvent("purchase", null, 9.99, "ord_2024-0001");
        Pushizo.Chat.Show();
        Assert.False(Pushizo.Chat.Hide());
        Assert.False(Pushizo.Chat.IsPresented);
        // Gözlemci olayları abone olunabilir kalır; yerel SDK olmadığından tetiklenmez.
        Pushizo.FeatureFlags.Changed += (_, _) => Assert.Fail("yerel SDK olmayan hedefte tetiklenmemeli");
        Pushizo.Inbox.Changed += (_, _) => Assert.Fail("yerel SDK olmayan hedefte tetiklenmemeli");

        // Rıza, tercihlerle aynı uçtadır: yerel SDK olmayan hedefte aynı hata.
        var set = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.SetAdsPersonalizationConsentAsync(true));
        Assert.Equal(PushizoException.PlatformNotSupported, set.Code);
        var get = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.User.GetConsentsAsync());
        Assert.Equal(PushizoException.PlatformNotSupported, get.Code);
    }
}

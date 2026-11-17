using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>
/// Android teslimat güçlendirme (ADR-0092): yerel durumun modele çevrilmesi, köprüye iletim ve desteklenmeyen
/// hedeflerde zararsız davranış (durum "desteklenmiyor", çağrılar false; istisna yok).
/// </summary>
public sealed class DeliveryTests : PushizoTestBase
{
    private const string StatusJson =
        """{"manufacturer":"xiaomi","brand":"redmi","vendor":"xiaomi","aggressiveVendor":true,"rom":"hyperos","romVersion":"1.0.2.0","notificationsEnabled":true,"channelBlocked":false,"batteryOptimizationExempt":false,"backgroundRestricted":false,"standbyBucket":"rare","powerSaveMode":true,"dataSaverEnabled":false,"restricted":true,"risk":"elevated","recommendedSettings":["auto_start","battery_optimization","gelecek_ekran"]}""";

    [Fact]
    public void Status_is_parsed_from_native_json()
    {
        Bridge.DeliveryStatusJson = StatusJson;
        InitializeSdk();

        var status = Pushizo.Delivery.GetStatus();

        Assert.True(status.Supported);
        Assert.Equal("xiaomi", status.Manufacturer);
        Assert.Equal("redmi", status.Brand);
        Assert.Equal("xiaomi", status.Vendor);
        Assert.True(status.AggressiveVendor);
        Assert.Equal("hyperos", status.Rom);
        Assert.Equal("1.0.2.0", status.RomVersion);
        Assert.True(status.NotificationsEnabled);
        Assert.False(status.ChannelBlocked);
        Assert.False(status.BatteryOptimizationExempt);
        Assert.False(status.BackgroundRestricted);
        Assert.Equal("rare", status.StandbyBucket);
        Assert.True(status.PowerSaveMode);
        Assert.False(status.DataSaverEnabled);
        Assert.True(status.Restricted);
        Assert.Equal(DeliveryRisk.Elevated, status.Risk);
        // Bu sürümün tanımadığı ayar ekranı atlanır.
        Assert.Equal(new[] { DeliverySetting.AutoStart, DeliverySetting.BatteryOptimization }, status.RecommendedSettings);
        Assert.Equal(new[] { "deliveryStatus" }, Bridge.Calls);
    }

    [Theory]
    [InlineData("high", DeliveryRisk.High)]
    [InlineData("elevated", DeliveryRisk.Elevated)]
    [InlineData("none", DeliveryRisk.None)]
    [InlineData("bilinmeyen", DeliveryRisk.None)]
    public void Risk_wire_values_map_to_enum(string wire, DeliveryRisk expected)
    {
        var status = BridgeJson.ParseDeliveryStatus($$"""{"risk":"{{wire}}","romVersion":null,"standbyBucket":null}""");

        Assert.True(status.Supported);
        Assert.Equal(expected, status.Risk);
        Assert.Null(status.RomVersion);
        Assert.Null(status.StandbyBucket);
        Assert.Empty(status.RecommendedSettings);
    }

    [Fact]
    public async Task Calls_reach_bridge_with_wire_values()
    {
        InitializeSdk();

        Assert.True(Pushizo.Delivery.ShouldShowGuidance());
        Assert.True(Pushizo.Delivery.ShouldShowGuidance(30));
        Assert.True(Pushizo.Delivery.ShouldShowGuidance(-5));
        Assert.True(Pushizo.Delivery.OpenSettings());
        Assert.True(Pushizo.Delivery.OpenSettings(DeliverySetting.AutoStart));
        Assert.True(Pushizo.Delivery.OpenSettings(DeliverySetting.AppDetails));
        Assert.True(await Pushizo.Delivery.ShowGuidanceAsync());
        Assert.True(await Pushizo.Delivery.ShowGuidanceAsync(DeliverySetting.BatteryOptimization, " Bildirimleri kaçırmayın "));
        // Boş açıklama null olarak iletilir; yerel SDK kendi yerelleştirilmiş metnini kullanır.
        Bridge.DeliveryResult = false;
        Assert.False(await Pushizo.Delivery.ShowGuidanceAsync(DeliverySetting.Notifications, "  "));
        Assert.False(Pushizo.Delivery.OpenSettings());

        Assert.Equal(new[]
        {
            "deliveryShouldShowGuidance:14",
            "deliveryShouldShowGuidance:30",
            "deliveryShouldShowGuidance:0",
            "deliveryOpenSettings:auto",
            "deliveryOpenSettings:auto_start",
            "deliveryOpenSettings:app_details",
            "deliveryShowGuidance:auto:<null>",
            "deliveryShowGuidance:battery_optimization:Bildirimleri kaçırmayın",
            "deliveryShowGuidance:notifications:<null>",
            "deliveryOpenSettings:auto",
        }, Bridge.Calls);
    }

    [Fact]
    public async Task Before_initialize_everything_is_inert()
    {
        Bridge.DeliveryStatusJson = StatusJson;

        Assert.Same(DeliveryStatus.Unsupported, Pushizo.Delivery.GetStatus());
        Assert.False(Pushizo.Delivery.ShouldShowGuidance());
        Assert.False(Pushizo.Delivery.OpenSettings());
        Assert.False(await Pushizo.Delivery.ShowGuidanceAsync());
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public void Missing_native_status_is_unsupported_without_risk()
    {
        // iOS köprüsü ve başlatılmamış yerel SDK null döndürür.
        InitializeSdk();

        var status = Pushizo.Delivery.GetStatus();

        Assert.False(status.Supported);
        Assert.Equal(DeliveryRisk.None, status.Risk);
        Assert.False(status.Restricted);
        Assert.Empty(status.RecommendedSettings);
        Assert.Equal("other", status.Vendor);
    }

    [Fact]
    public async Task Native_failures_never_throw()
    {
        InitializeSdk();
        Bridge.DeliveryFailure = new InvalidOperationException("jni");

        Assert.Same(DeliveryStatus.Unsupported, Pushizo.Delivery.GetStatus());
        Assert.False(Pushizo.Delivery.ShouldShowGuidance());
        Assert.False(Pushizo.Delivery.OpenSettings());
        Assert.False(await Pushizo.Delivery.ShowGuidanceAsync());

        Bridge.DeliveryFailure = null;
        Bridge.DeliveryStatusJson = "bozuk";
        Assert.Same(DeliveryStatus.Unsupported, Pushizo.Delivery.GetStatus());
    }

    [Fact]
    public async Task Unsupported_platform_is_a_safe_no_op()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Assert.Same(DeliveryStatus.Unsupported, Pushizo.Delivery.GetStatus());
        Assert.False(Pushizo.Delivery.ShouldShowGuidance());
        Assert.False(Pushizo.Delivery.OpenSettings(DeliverySetting.BatteryOptimization));
        Assert.False(await Pushizo.Delivery.ShowGuidanceAsync(message: "açıklama"));
    }
}

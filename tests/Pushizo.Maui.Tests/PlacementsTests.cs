using PushizoSdk.Maui.Internal;

namespace PushizoSdk.Maui.Tests;

/// <summary>Gömülü yerleşimler (ADR-0068): köprüye iletim, mesaj ayrıştırma, doğrulama ve desteklenmeyen hedefler.</summary>
public sealed class PlacementsTests : PushizoTestBase
{
    [Fact]
    public async Task List_parses_messages_and_passes_defaults()
    {
        InitializeSdk();
        Bridge.PlacementsResponse = ("""
            {"messages":[
              {"messageId":"iam_1","json":"{\"id\":\"iam_1\",\"display\":{\"title\":\"Kampanya\"}}"},
              {"messageId":"iam_2","json":null}
            ]}
            """, null, null);

        var messages = await Pushizo.Placements.ListAsync("ana_sayfa_ust");

        Assert.Equal("placementsList:ana_sayfa_ust:1", Bridge.Calls[^1]);
        Assert.Equal(2, messages.Count);
        Assert.Equal("iam_1", messages[0].Id);
        Assert.Contains("\"title\":\"Kampanya\"", messages[0].Json);
        Assert.Equal("iam_2", messages[1].Id);
        Assert.Null(messages[1].Json);

        await Pushizo.Placements.ListAsync("sepet.alt-1", limit: 10);
        Assert.Equal("placementsList:sepet.alt-1:10", Bridge.Calls[^1]);
    }

    [Fact]
    public async Task List_returns_empty_list_without_content()
    {
        InitializeSdk();

        Assert.Empty(await Pushizo.Placements.ListAsync("ana_sayfa_ust"));

        Bridge.PlacementsResponse = ("{}", null, null);
        Assert.Empty(await Pushizo.Placements.ListAsync("ana_sayfa_ust"));
    }

    [Fact]
    public async Task List_errors_carry_native_code()
    {
        InitializeSdk();

        Bridge.PlacementsResponse = (null, "INVALID_ARGUMENT", "Geçersiz yerleşim anahtarı");
        var invalidArgument = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Placements.ListAsync("ana_sayfa"));
        Assert.Equal(PushizoException.InvalidArgument, invalidArgument.Code);

        Bridge.PlacementsResponse = ("""{"messages":[{"json":"{}"}]}""", null, null);
        var missingId = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Placements.ListAsync("ana_sayfa"));
        Assert.Equal(PushizoException.InvalidResponse, missingId.Code);

        Bridge.PlacementsResponse = (null, null, null);
        var empty = await Assert.ThrowsAsync<PushizoException>(() => Pushizo.Placements.ListAsync("ana_sayfa"));
        Assert.Equal(PushizoException.InvalidResponse, empty.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ana_sayfa")]
    [InlineData("1sayfa")]
    [InlineData("ana sayfa")]
    [InlineData("ana/sayfa")]
    [InlineData("_ust")]
    public async Task List_rejects_invalid_key(string key)
    {
        InitializeSdk();

        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Placements.ListAsync(key));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task List_validates_key_length_and_limit_like_native_sdk()
    {
        InitializeSdk();

        await Pushizo.Placements.ListAsync("a" + new string('b', 63));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Placements.ListAsync("a" + new string('b', 64)));
        await Assert.ThrowsAsync<ArgumentException>(() => Pushizo.Placements.ListAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Placements.ListAsync("ana_sayfa", limit: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Pushizo.Placements.ListAsync("ana_sayfa", limit: 11));
        Assert.Single(Bridge.Calls);
    }

    [Fact]
    public void Reports_pass_message_id_to_bridge()
    {
        InitializeSdk();
        var message = new InAppMessage("iam_1", "{}");

        Pushizo.Placements.ReportImpression(message);
        Pushizo.Placements.ReportClick(message, "cta");
        Pushizo.Placements.ReportDismiss(message);

        Assert.Equal(new[] { "placementImpression:iam_1", "placementClick:iam_1:cta", "placementDismiss:iam_1" }, Bridge.Calls);
    }

    [Fact]
    public void Reports_validate_arguments()
    {
        InitializeSdk();
        var message = new InAppMessage("iam_1", null);

        Assert.Throws<ArgumentNullException>(() => Pushizo.Placements.ReportImpression(null!));
        Assert.Throws<ArgumentException>(() => Pushizo.Placements.ReportDismiss(new InAppMessage(" ", null)));
        Assert.Throws<ArgumentException>(() => Pushizo.Placements.ReportClick(message, ""));
        Assert.Throws<ArgumentException>(() => Pushizo.Placements.ReportClick(message, "  "));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public void Calls_before_initialize_throw()
    {
        Assert.Throws<InvalidOperationException>(() => { _ = Pushizo.Placements.ListAsync("ana_sayfa"); });
        Assert.Throws<InvalidOperationException>(() => Pushizo.Placements.ReportImpression(new InAppMessage("iam_1", null)));
        Assert.Empty(Bridge.Calls);
    }

    [Fact]
    public async Task Unsupported_platform_returns_empty_list_and_ignores_reports()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        InitializeSdk();

        Assert.Empty(await Pushizo.Placements.ListAsync("ana_sayfa_ust", limit: 3));
        Pushizo.Placements.ReportImpression(new InAppMessage("iam_1", null));
        Pushizo.Placements.ReportClick(new InAppMessage("iam_1", null), "cta");
        Pushizo.Placements.ReportDismiss(new InAppMessage("iam_1", null));
    }
}

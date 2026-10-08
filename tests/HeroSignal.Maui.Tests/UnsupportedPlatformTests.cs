using HeroSignalSdk.Maui.Platforms;

namespace HeroSignalSdk.Maui.Tests;

/// <summary>net9.0 (Windows/Mac Catalyst) hedefinde çağrılar etkisizdir ve hata vermez.</summary>
public sealed class UnsupportedPlatformTests : IDisposable
{
    public UnsupportedPlatformTests() => HeroSignal.ResetForTests(new UnsupportedHeroSignalPlatform());

    public void Dispose() => HeroSignal.ResetForTests(new FakePlatform());

    [Fact]
    public async Task Calls_are_no_ops_and_inbox_reports_unsupported()
    {
        HeroSignal.Initialize("app_1");
        Assert.False(HeroSignal.IsSupported);

        HeroSignal.Login("user");
        HeroSignal.User.AddTag("a", "b");
        HeroSignal.User.AddEmail("a@example.com");
        HeroSignal.User.PushSubscription.OptIn();
        HeroSignal.Notifications.ClearAll();
        HeroSignal.Notifications.RemoveNotification(1);
        HeroSignal.Notifications.RemoveGroupedNotifications("g");
        HeroSignal.InAppMessages.AddTrigger("k", "v");
        HeroSignal.InAppMessages.Paused = true;
        HeroSignal.Session.SendEvent("opened");
        HeroSignal.Logout();

        Assert.False(HeroSignal.InAppMessages.Paused);
        Assert.Empty(HeroSignal.InAppMessages.GetTriggers());
        Assert.Null(HeroSignal.User.PushSubscription.Id);
        Assert.False(await HeroSignal.Notifications.RequestPermissionAsync());
        var e = await Assert.ThrowsAsync<HeroSignalException>(() => HeroSignal.Inbox.UnreadCountAsync());
        Assert.Equal(HeroSignalException.PlatformNotSupported, e.Code);
    }
}

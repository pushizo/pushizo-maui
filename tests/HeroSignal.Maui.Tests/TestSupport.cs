using HeroSignalSdk.Maui.Internal;

// HeroSignal statik bir cephedir; testler sırayla çalışır.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HeroSignalSdk.Maui.Tests;

/// <summary>Her test için temiz durum: sahte köprülü platform kurulur, SDK sıfırlanır.</summary>
public abstract class HeroSignalTestBase : IDisposable
{
    internal FakeNativeBridge Bridge { get; } = new();

    protected HeroSignalTestBase()
    {
        HeroSignal.ResetForTests(new BridgePlatform(Bridge));
    }

    protected void InitializeSdk(HeroSignalOptions? options = null) =>
        HeroSignal.Initialize("app_test", options ?? new HeroSignalOptions { ApiUrl = "https://api.example.com" });

    public void Dispose()
    {
        HeroSignal.ResetForTests(new UnsupportedPlatform());
        GC.SuppressFinalize(this);
    }
}

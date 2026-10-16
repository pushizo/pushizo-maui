using PushizoSdk.Maui.Internal;

// Pushizo statik bir cephedir; testler sırayla çalışır.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PushizoSdk.Maui.Tests;

/// <summary>Her test için temiz durum: sahte köprülü platform kurulur, SDK sıfırlanır.</summary>
public abstract class PushizoTestBase : IDisposable
{
    internal FakeNativeBridge Bridge { get; } = new();

    protected PushizoTestBase()
    {
        Pushizo.ResetForTests(new BridgePlatform(Bridge));
    }

    protected void InitializeSdk(PushizoOptions? options = null) =>
        Pushizo.Initialize("app_test", options ?? new PushizoOptions { ApiUrl = "https://api.example.com" });

    public void Dispose()
    {
        Pushizo.ResetForTests(new UnsupportedPlatform());
        GC.SuppressFinalize(this);
    }
}

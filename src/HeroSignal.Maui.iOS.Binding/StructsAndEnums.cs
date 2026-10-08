using ObjCRuntime;

namespace HeroSignalSdk.Native.iOS;

/// <summary>Köprü günlük seviyesi (<c>HSLog.Level</c> ham değerleri; <c>setLogLevel:</c> NSInteger).</summary>
[Native]
public enum HSMauiLogLevel : long
{
    None = 0,
    Error = 1,
    Warn = 2,
    Info = 3,
    Debug = 4,
}

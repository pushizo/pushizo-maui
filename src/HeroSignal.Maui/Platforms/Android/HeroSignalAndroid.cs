using HeroSignalSdk.Native.Android;

namespace HeroSignalSdk.Maui;

/// <summary>
/// Android'e özgü bağlantı noktaları. <c>builder.UseHeroSignal(...)</c> kullanılıyorsa çağrılar otomatik bağlanır;
/// yalnızca <see cref="HeroSignal.Initialize"/> kullanan uygulamalar <c>MainActivity</c>'den iletir:
/// <code>
/// public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
/// {
///     base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
///     HeroSignalAndroid.OnRequestPermissionsResult(requestCode);
/// }
/// </code>
/// </summary>
public static class HeroSignalAndroid
{
    /// <summary>
    /// Bildirim ve konum izni isteklerinin sonucunu yerel SDK'ya iletir (HeroSignal dışı istek kodları yok sayılır).
    /// </summary>
    public static void OnRequestPermissionsResult(int requestCode)
    {
        if (!HeroSignal.IsInitialized) return;
        HeroSignalMauiBridge.OnRequestPermissionsResult(requestCode);
    }
}

using PushizoSdk.Native.Android;

namespace PushizoSdk.Maui;

/// <summary>
/// Android'e özgü bağlantı noktaları. <c>builder.UsePushizo(...)</c> kullanılıyorsa çağrılar otomatik bağlanır;
/// yalnızca <see cref="Pushizo.Initialize"/> kullanan uygulamalar <c>MainActivity</c>'den iletir:
/// <code>
/// public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
/// {
///     base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
///     PushizoAndroid.OnRequestPermissionsResult(requestCode);
/// }
/// </code>
/// </summary>
public static class PushizoAndroid
{
    /// <summary>
    /// Bildirim ve konum izni isteklerinin sonucunu yerel SDK'ya iletir (Pushizo dışı istek kodları yok sayılır).
    /// </summary>
    public static void OnRequestPermissionsResult(int requestCode)
    {
        if (!Pushizo.IsInitialized) return;
        PushizoMauiBridge.OnRequestPermissionsResult(requestCode);
    }
}

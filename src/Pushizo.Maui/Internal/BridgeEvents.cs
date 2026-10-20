using System.Text.Json;

namespace PushizoSdk.Maui.Internal;

/// <summary>
/// Yerel köprünün <c>(tür, json)</c> olaylarını ortak katmana yönlendirir. Türler ve yükler:
/// <list type="bullet">
/// <item><c>click</c>: <c>{"notification":{…},"actionId","url"}</c></item>
/// <item><c>foreground</c>: <c>{"eventId","notification":{…}}</c> — işleyiciler eşzamanlı çalışır, karar köprüye iletilir</item>
/// <item><c>subscription</c>: <c>{"id","token","optedIn","permission"}</c></item>
/// <item><c>permission</c>: <c>{"permission"}</c> — yerel SDK izin gözlemcisinden (yalnızca gerçek değişiklikte)</item>
/// <item><c>inAppClick</c>: <c>{"messageId","actionId","url"}</c></item>
/// <item><c>inAppLifecycle</c>: <c>{"event":"willDisplay|didDisplay|willDismiss|didDismiss","messageId","json"}</c></item>
/// <item><c>chat</c>: <c>{"type":"message|typing|read|unread|conversation|connection|error",…}</c> (canlı sohbet)</item>
/// <item><c>chatOpen</c>: <c>{"conversationId","seq"}</c> — sohbet bildirimine tıklama</item>
/// </list>
/// Bilinmeyen tür (daha yeni yerel köprü) yok sayılır.
/// </summary>
internal static class BridgeEvents
{
    public const string Click = "click";
    public const string Foreground = "foreground";
    public const string Subscription = "subscription";
    public const string Permission = "permission";
    public const string InAppClick = "inAppClick";
    public const string InAppLifecycle = "inAppLifecycle";
    public const string Chat = "chat";
    public const string ChatOpen = "chatOpen";

    public static void Dispatch(IPushizoEventSink sink, string type, string json, INativeBridge bridge)
    {
        switch (type)
        {
            case Click:
                sink.OnNotificationClicked(BridgeJson.ParseNotificationClick(json));
                break;
            case Foreground:
            {
                var (eventId, notification) = BridgeJson.ParseForeground(json);
                var args = new NotificationWillDisplayEventArgs(
                    notification,
                    () => bridge.ForegroundPreventDefault(eventId),
                    () => bridge.ForegroundDisplay(eventId));
                sink.OnNotificationWillDisplay(args);
                break;
            }
            case Subscription:
                // İzin değişikliği de abonelik durumunun parçasıdır (PushSubscriptionManager iletir).
                sink.OnPushSubscriptionChanged(BridgeJson.ParseSubscriptionState(json));
                break;
            case Permission:
                // Yinelenen bildirim (abonelik olayıyla birlikte) NotificationsManager'da elenir.
                sink.OnPermissionChanged(BridgeJson.ParsePermission(json));
                break;
            case InAppClick:
                sink.OnInAppClicked(BridgeJson.ParseInAppClick(json));
                break;
            case InAppLifecycle:
            {
                var phase = ParsePhase(json);
                if (phase is { } p) sink.OnInAppLifecycle(p, BridgeJson.ParseInAppMessage(json));
                break;
            }
            case Chat:
                if (ChatJson.ParseEvent(json) is { } chat) sink.OnChatEvent(chat);
                break;
            case ChatOpen:
                sink.OnChatOpened(ChatJson.ParseOpened(json));
                break;
            default:
                SdkLog.Write(LogLevel.Debug, $"bilinmeyen yerel olay türü yok sayıldı: {type}");
                break;
        }
    }

    private static InAppLifecyclePhase? ParsePhase(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var name = doc.RootElement.TryGetProperty("event", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        return name switch
        {
            "willDisplay" => InAppLifecyclePhase.WillDisplay,
            "didDisplay" => InAppLifecyclePhase.DidDisplay,
            "willDismiss" => InAppLifecyclePhase.WillDismiss,
            "didDismiss" => InAppLifecyclePhase.DidDismiss,
            _ => null,
        };
    }
}

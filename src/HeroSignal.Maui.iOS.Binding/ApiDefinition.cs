using System;
using Foundation;
using ObjCRuntime;
using UserNotifications;

namespace HeroSignalSdk.Native.iOS;

/// <summary>Köprü olayı: tür (<c>click</c>, <c>foreground</c>, <c>subscription</c>, <c>permission</c>, <c>inAppClick</c>, <c>inAppLifecycle</c>) ve JSON yükü.</summary>
delegate void HSMauiEventHandler(string type, string json);

/// <summary>Bildirim kutusu tamamlaması: JSON sonuç ya da hata (domain <c>HeroSignal</c>, userInfo <c>code</c>/<c>status</c>).</summary>
delegate void HSMauiJsonCompletion([NullAllowed] string json, [NullAllowed] NSError error);

delegate void HSMauiBoolCompletion(bool value);

/// <summary>
/// <c>HSMauiBridge</c> (native/Sources/HeroSignalMauiBridge/HSMauiBridge.swift). Tüm üyeler sınıf (statik) üyeleridir;
/// karmaşık değerler JSON metniyle taşınır (biçimler HeroSignal.Maui/Internal/BridgeJson.cs).
/// </summary>
[BaseType(typeof(NSObject), Name = "HSMauiBridge")]
[DisableDefaultCtor]
interface HSMauiBridge
{
    [Static]
    [Export("setEventHandler:")]
    void SetEventHandler(HSMauiEventHandler handler);

    [Static]
    [Export("startWithAppId:optionsJson:")]
    void Start(string appId, string optionsJson);

    [Static]
    [Export("setConsentGiven:")]
    void SetConsentGiven(bool given);

    [Static]
    [Export("loginWithExternalId:identityHash:")]
    void Login(string externalId, [NullAllowed] string identityHash);

    [Static]
    [Export("logout")]
    void Logout();

    [Static]
    [Export("setLogLevel:")]
    void SetLogLevel(HSMauiLogLevel level);

    [Static]
    [Export("setDeviceToken:")]
    void SetDeviceToken(NSData token);

    [Static]
    [Export("setDeviceTokenError:")]
    void SetDeviceTokenError(NSError error);

    // Kullanıcı
    [Static, NullAllowed]
    [Export("externalId")]
    string ExternalId { get; }

    [Static, NullAllowed]
    [Export("userId")]
    string UserId { get; }

    [Static]
    [Export("addTagsJson:")]
    void AddTagsJson(string json);

    [Static]
    [Export("removeTagsJson:")]
    void RemoveTagsJson(string json);

    [Static]
    [Export("tagsJson")]
    string TagsJson { get; }

    [Static]
    [Export("addAliasesJson:")]
    void AddAliasesJson(string json);

    [Static]
    [Export("removeAlias:")]
    void RemoveAlias(string label);

    [Static]
    [Export("setLanguage:")]
    void SetLanguage(string code);

    [Static]
    [Export("addEmail:")]
    void AddEmail(string email);

    [Static]
    [Export("removeEmail:")]
    void RemoveEmail(string email);

    [Static]
    [Export("addSms:")]
    void AddSms(string phoneNumber);

    [Static]
    [Export("removeSms:")]
    void RemoveSms(string phoneNumber);

    [Static]
    [Export("emailsJson")]
    string EmailsJson { get; }

    [Static]
    [Export("smsNumbersJson")]
    string SmsNumbersJson { get; }

    // Push aboneliği
    [Static]
    [Export("pushSubscriptionJson")]
    string PushSubscriptionJson { get; }

    [Static]
    [Export("optIn")]
    void OptIn();

    [Static]
    [Export("optOut")]
    void OptOut();

    // Bildirimler
    [Static]
    [Export("notificationPermission")]
    bool NotificationPermission { get; }

    [Static]
    [Export("requestPermissionWithFallbackToSettings:completion:")]
    void RequestPermission(bool fallbackToSettings, HSMauiBoolCompletion completion);

    [Static]
    [Export("clearAllNotifications")]
    void ClearAllNotifications();

    [Static]
    [Export("removeNotification:")]
    void RemoveNotification(string id);

    [Static]
    [Export("removeGroupedNotifications:")]
    void RemoveGroupedNotifications(string group);

    [Static]
    [Export("foregroundPreventDefault:")]
    void ForegroundPreventDefault(string eventId);

    [Static]
    [Export("foregroundDisplay:")]
    void ForegroundDisplay(string eventId);

    // In-app
    [Static]
    [Export("addTriggersJson:")]
    void AddTriggersJson(string json);

    [Static]
    [Export("removeTriggersJson:")]
    void RemoveTriggersJson(string json);

    [Static]
    [Export("clearTriggers")]
    void ClearTriggers();

    [Static]
    [Export("triggersJson")]
    string TriggersJson { get; }

    [Static]
    [Export("setScreen:")]
    void SetScreen([NullAllowed] string name);

    [Static]
    [Export("inAppPaused")]
    bool InAppPaused { get; set; }

    // Bildirim kutusu
    [Static]
    [Export("inboxListWithLimit:cursor:completion:")]
    void InboxList(nint limit, [NullAllowed] string cursor, HSMauiJsonCompletion completion);

    [Static]
    [Export("inboxMarkRead:completion:")]
    void InboxMarkRead(string id, HSMauiJsonCompletion completion);

    [Static]
    [Export("inboxMarkAllReadWithCompletion:")]
    void InboxMarkAllRead(HSMauiJsonCompletion completion);

    [Static]
    [Export("inboxArchive:completion:")]
    void InboxArchive(string id, HSMauiJsonCompletion completion);

    [Static]
    [Export("inboxUnreadCountWithCompletion:")]
    void InboxUnreadCount(HSMauiJsonCompletion completion);

    // Oturum
    [Static]
    [Export("sendEvent:propertiesJson:value:")]
    void SendEvent(string name, string propertiesJson, [NullAllowed] NSNumber value);
}

/// <summary>
/// <c>HSMauiNotificationService</c> (native/Sources/HeroSignalMauiNotificationService): Notification Service Extension
/// yardımcısı. Uzantı projesinde <c>UNNotificationServiceExtension</c> alt sınıfından çağrılır.
/// </summary>
[BaseType(typeof(NSObject), Name = "HSMauiNotificationService")]
[DisableDefaultCtor]
interface HSMauiNotificationService
{
    [Export("initWithAppId:apiUrl:appGroup:")]
    [DesignatedInitializer]
    NativeHandle Constructor(string appId, string apiUrl, [NullAllowed] string appGroup);

    [Export("didReceiveNotificationRequest:withContentHandler:")]
    void DidReceive(UNNotificationRequest request, Action<UNNotificationContent> contentHandler);

    [Export("serviceExtensionTimeWillExpire")]
    void ServiceExtensionTimeWillExpire();
}

using System;
using Foundation;
using ObjCRuntime;

namespace HeroSignalSdk.Native.iOS
{
    // HSMauiBridge (native/Sources/HeroSignalMauiBridge/HSMauiBridge.swift) için Objective-C bağlama tanımı.
    // Seçiciler Swift tarafındaki @objc(...) adlarıyla birebir aynıdır.

    /// <summary>Yerel olay: (tür, json); eşzamanlı çağrılır.</summary>
    delegate void HSMauiEventHandler (string type, string json);

    /// <summary>Eşzamansız sonuç: başarıda json, hatada errorCode + errorMessage.</summary>
    delegate void HSMauiResultHandler ([NullAllowed] string json, [NullAllowed] string errorCode, [NullAllowed] string errorMessage);

    /// <summary>İzin isteği sonucu.</summary>
    delegate void HSMauiPermissionHandler (bool granted);

    [BaseType (typeof (NSObject), Name = "HSMauiBridge")]
    [DisableDefaultCtor]
    interface HSMauiBridge
    {
        [Static]
        [Export ("startWithAppId:optionsJson:listener:")]
        void Start (string appId, string optionsJson, HSMauiEventHandler listener);

        [Static]
        [Export ("setDeviceToken:")]
        void SetDeviceToken (NSData deviceToken);

        [Static]
        [Export ("setDeviceTokenError:")]
        void SetDeviceTokenError (NSError error);

        [Static]
        [Export ("setConsentGiven:")]
        void SetConsentGiven (bool given);

        [Static]
        [Export ("loginWithExternalId:identityHash:")]
        void Login (string externalId, [NullAllowed] string identityHash);

        [Static]
        [Export ("logout")]
        void Logout ();

        [Static]
        [Export ("applyLogLevel:")]
        void ApplyLogLevel (nint level);

        [Static]
        [Export ("externalId")]
        [return: NullAllowed]
        string ExternalId ();

        [Static]
        [Export ("userId")]
        [return: NullAllowed]
        string UserId ();

        [Static]
        [Export ("addTags:")]
        void AddTags (string tagsJson);

        [Static]
        [Export ("removeTags:")]
        void RemoveTags (string keysJson);

        [Static]
        [Export ("tags")]
        string Tags ();

        [Static]
        [Export ("addAliases:")]
        void AddAliases (string aliasesJson);

        [Static]
        [Export ("removeAlias:")]
        void RemoveAlias (string label);

        [Static]
        [Export ("setLanguage:")]
        void SetLanguage (string code);

        [Static]
        [Export ("addEmail:")]
        void AddEmail (string email);

        [Static]
        [Export ("removeEmail:")]
        void RemoveEmail (string email);

        [Static]
        [Export ("addSms:")]
        void AddSms (string phoneNumber);

        [Static]
        [Export ("removeSms:")]
        void RemoveSms (string phoneNumber);

        [Static]
        [Export ("emails")]
        string Emails ();

        [Static]
        [Export ("smsNumbers")]
        string SmsNumbers ();

        [Static]
        [Export ("pushSubscription")]
        string PushSubscription ();

        [Static]
        [Export ("optIn")]
        void OptIn ();

        [Static]
        [Export ("optOut")]
        void OptOut ();

        [Static]
        [Export ("permission")]
        bool Permission ();

        [Static]
        [Export ("requestPermissionWithFallbackToSettings:completion:")]
        void RequestPermission (bool fallbackToSettings, HSMauiPermissionHandler completion);

        [Static]
        [Export ("clearAllNotifications")]
        void ClearAllNotifications ();

        [Static]
        [Export ("removeNotification:")]
        void RemoveNotification (string identifier);

        [Static]
        [Export ("removeGroupedNotifications:")]
        void RemoveGroupedNotifications (string group);

        [Static]
        [Export ("addTriggers:")]
        void AddTriggers (string triggersJson);

        [Static]
        [Export ("removeTriggers:")]
        void RemoveTriggers (string keysJson);

        [Static]
        [Export ("clearTriggers")]
        void ClearTriggers ();

        [Static]
        [Export ("triggers")]
        string Triggers ();

        [Static]
        [Export ("setScreen:")]
        void SetScreen ([NullAllowed] string name);

        [Static]
        [Export ("inAppPaused")]
        bool InAppPaused ();

        [Static]
        [Export ("updateInAppPaused:")]
        void UpdateInAppPaused (bool paused);

        [Static]
        [Export ("inboxListWithLimit:cursor:completion:")]
        void InboxList (nint limit, [NullAllowed] string cursor, HSMauiResultHandler completion);

        [Static]
        [Export ("inboxMarkRead:completion:")]
        void InboxMarkRead (string id, HSMauiResultHandler completion);

        [Static]
        [Export ("inboxMarkAllReadWithCompletion:")]
        void InboxMarkAllRead (HSMauiResultHandler completion);

        [Static]
        [Export ("inboxArchive:completion:")]
        void InboxArchive (string id, HSMauiResultHandler completion);

        [Static]
        [Export ("inboxUnreadCountWithCompletion:")]
        void InboxUnreadCount (HSMauiResultHandler completion);

        [Static]
        [Export ("getPreferencesWithCompletion:")]
        void GetPreferences (HSMauiResultHandler completion);

        [Static]
        [Export ("setPreferences:completion:")]
        void SetPreferences (string preferencesJson, HSMauiResultHandler completion);

        [Static]
        [Export ("sendEvent:propertiesJson:value:hasValue:")]
        void SendEvent (string name, string propertiesJson, double value, bool hasValue);

        [Static]
        [Export ("foregroundPreventDefault:")]
        void ForegroundPreventDefault (string eventId);

        [Static]
        [Export ("foregroundDisplay:")]
        void ForegroundDisplay (string eventId);
    }
}

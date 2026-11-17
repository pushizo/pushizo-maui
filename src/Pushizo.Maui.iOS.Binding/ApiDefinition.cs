using System;
using Foundation;
using ObjCRuntime;

namespace PushizoSdk.Native.iOS
{
    // HSMauiBridge (native/Sources/PushizoMauiBridge/HSMauiBridge.swift) için Objective-C bağlama tanımı.
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
        [Export ("loginWithExternalId:identityHash:jwt:")]
        [return: NullAllowed]
        string Login (string externalId, [NullAllowed] string identityHash, [NullAllowed] string jwt);

        [Static]
        [Export ("loginWithExternalId:profileJson:identityHash:jwt:")]
        [return: NullAllowed]
        string LoginWithProfile (string externalId, string profileJson, [NullAllowed] string identityHash, [NullAllowed] string jwt);

        [Static]
        [Export ("updateUserJwt:")]
        [return: NullAllowed]
        string UpdateUserJwt (string jwt);

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
        [Export ("chatCall:argsJson:completion:")]
        void ChatCall (string method, string argsJson, HSMauiResultHandler completion);

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
        [Export ("contentCardsListWithLimit:completion:")]
        void ContentCardsList (nint limit, HSMauiResultHandler completion);

        [Static]
        [Export ("contentCardEvent:eventName:completion:")]
        void ContentCardEvent (string id, string eventName, HSMauiResultHandler completion);

        [Static]
        [Export ("getPreferencesWithCompletion:")]
        void GetPreferences (HSMauiResultHandler completion);

        [Static]
        [Export ("setPreferences:completion:")]
        void SetPreferences (string preferencesJson, HSMauiResultHandler completion);

        [Static]
        [Export ("setAdsPersonalizationConsent:completion:")]
        void SetAdsPersonalizationConsent (bool granted, HSMauiResultHandler completion);

        [Static]
        [Export ("getConsentsWithCompletion:")]
        void GetConsents (HSMauiResultHandler completion);

        [Static]
        [Export ("setAdvertisingId:")]
        void SetAdvertisingId ([NullAllowed] string id);

        [Static]
        [Export ("setBadgeCount:")]
        void SetBadgeCount (nint count);

        [Static]
        [Export ("sendEvent:propertiesJson:value:hasValue:")]
        void SendEvent (string name, string propertiesJson, double value, bool hasValue);

        [Static]
        [Export ("setupDefaultLiveActivityWithCompletion:")]
        void SetupDefaultLiveActivity (HSMauiResultHandler completion);

        [Static]
        [Export ("startDefaultLiveActivity:attributesJson:contentStateJson:completion:")]
        void StartDefaultLiveActivity (string activityId, string attributesJson, string contentStateJson, HSMauiResultHandler completion);

        [Static]
        [Export ("locationShared")]
        bool LocationShared ();

        [Static]
        [Export ("updateLocationShared:")]
        void UpdateLocationShared (bool shared);

        [Static]
        [Export ("requestLocationPermissionWithRationale:completion:")]
        void RequestLocationPermission ([NullAllowed] string rationale, HSMauiPermissionHandler completion);

        [Static]
        [Export ("locationPermissionGranted")]
        bool LocationPermissionGranted ();

        [Static]
        [Export ("locationBackgroundPermissionGranted")]
        bool LocationBackgroundPermissionGranted ();

        [Static]
        [Export ("requestBackgroundLocationPermissionWithCompletion:")]
        void RequestBackgroundLocationPermission (HSMauiPermissionHandler completion);

        [Static]
        [Export ("geofencingEnabled")]
        bool GeofencingEnabled ();

        [Static]
        [Export ("updateGeofencingEnabled:")]
        void UpdateGeofencingEnabled (bool enabled);

        [Static]
        [Export ("featureFlagsRefreshWithCompletion:")]
        void FeatureFlagsRefresh (HSMauiResultHandler completion);

        [Static]
        [Export ("featureFlagEnabled:fallback:")]
        bool FeatureFlagEnabled (string key, bool fallback);

        [Static]
        [Export ("featureFlagValue:")]
        [return: NullAllowed]
        string FeatureFlagValue (string key);

        [Static]
        [Export ("placementsList:limit:completion:")]
        void PlacementsList (string key, nint limit, HSMauiResultHandler completion);

        [Static]
        [Export ("placementsReportImpression:")]
        void PlacementsReportImpression (string messageId);

        [Static]
        [Export ("placementsReportClick:buttonId:")]
        void PlacementsReportClick (string messageId, string buttonId);

        [Static]
        [Export ("placementsReportDismiss:")]
        void PlacementsReportDismiss (string messageId);

        [Static]
        [Export ("foregroundPreventDefault:")]
        void ForegroundPreventDefault (string eventId);

        [Static]
        [Export ("foregroundDisplay:")]
        void ForegroundDisplay (string eventId);
    }
}

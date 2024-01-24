//==========================  Open Steamworks  ================================
//
// This file is part of the Open Steamworks project. All individuals associated
// with this project do not claim ownership of the contents
// 
// The code, comments, and all related files, projects, resources,
// redistributables included with this project are Copyright Valve Corporation.
// Additionally, Valve, the Valve logo, Half-Life, the Half-Life logo, the
// Lambda logo, Steam, the Steam logo, Team Fortress, the Team Fortress logo,
// Opposing Force, Day of Defeat, the Day of Defeat logo, Counter-Strike, the
// Counter-Strike logo, Source, the Source logo, and Counter-Strike Condition
// Zero are trademarks and or registered trademarks of Valve Corporation.
// All other trademarks are property of their respective owners.
//
//=============================================================================

#ifndef ERESULT_H
#define ERESULT_H
#ifdef _WIN32
#pragma once
#endif


// General result codes
typedef enum EResult
{
	k_EResultNoResult = 0,
    k_EResultOK = 1,
    k_EResultFailure = 2,
    k_EResultNoConnection = 3,
    k_EResultInvalidPassword = 5,
    k_EResultLoggedInElsewhere = 6,
    k_EResultInvalidProtocol = 7,
    k_EResultInvalidParameter = 8,
    k_EResultFileNotFound = 9,
    k_EResultBusy = 10,
    k_EResultInvalidState = 11,
    k_EResultInvalidName = 12,
    k_EResultInvalidEmail = 13,
    k_EResultDuplicateName = 14,
    k_EResultAccessDenied = 15,
    k_EResultTimeout = 16,
    k_EResultBanned = 17,
    k_EResultAccountNotFound = 18,
    k_EResultInvalidSteamID = 19,
    k_EResultServiceUnavailable = 20,
    k_EResultNotLoggedOn = 21,
    k_EResultPending = 22,
    k_EResultEncryptionFailure = 23,
    k_EResultInsufficientPrivilege = 24,
    k_EResultLimitExceeded = 25,
    k_EResultRequestRevoked = 26,
    k_EResultExpired = 27,
    k_EResultAlreadyRedeemed = 28,
    k_EResultDuplicatedRequest = 29,
    k_EResultAlreadyOwned = 30,
    k_EResultIPAddressNotFound = 31,
    k_EResultPersistenceFailed = 32,
    k_EResultLockingFailed = 33,
    k_EResultSessionReplaced = 34,
    k_EResultConnectionFailed = 35,
    k_EResultHandshakeFailed = 36,
    k_EResultIOOperationFailed = 37,
    k_EResultDisconnectedByRemoteHost = 38,
    k_EResultShoppingCartNotFound = 39,
    k_EResultBlocked = 40,
    k_EResultIgnored = 41,
    k_EResultNoMatch = 42,
    k_EResultAccountDisabled = 43,
    k_EResultServiceReadOnly = 44,
    k_EResultAccountNotFeatured = 45,
    k_EResultAdministratorOK = 46,
    k_EResultContentVersion = 47,
    k_EResultTryAnotherCM = 48,
    k_EResultPasswordRequiredToKickSession = 49,
    k_EResultAlreadyLoggedInElsewhere = 50,
    k_EResultRequestSuspendedpaused = 51,
    k_EResultRequestHasBeenCanceled = 52,
    k_EResultCorruptedOrUnrecoverableDataError = 53,
    k_EResultNotEnoughFreeDiskSpace = 54,
    k_EResultRemoteCallFailed = 55,
    k_EResultPasswordIsNotSet = 56,
    k_EResultExternalAccountIsNotLinkedToASteamAccount = 57,
    k_EResultPSNTicketIsInvalid = 58,
    k_EResultExternalAccountLinkedToAnotherSteamAccount = 59,
    k_EResultRemoteFileConflict = 60,
    k_EResultIllegalPassword = 61,
    k_EResultSameAsPreviousValue = 62,
    k_EResultAccountLogonDenied = 63,
    k_EResultCannotUseOldPassword = 64,
    k_EResultInvalidLoginAuthCode = 65,
    k_EResultAccountLogonDeniedNoMailSent = 66,
    k_EResultHardwareNotCapableOfIPT = 67,
    k_EResultIPTInitError = 68,
    k_EResultOperationFailedDueToParentalControlRestrictionsForCurrentUser = 69,
    k_EResultFacebookQueryReturnedAnError = 70,
    k_EResultExpiredLoginAuthCode = 71,
    k_EResultIPLoginRestrictionFailed = 72,
    k_EResultAccountLockedDown = 73,
    k_EResultAccountLogonDeniedVerifiedEmailRequired = 74,
    k_EResultNoMatchingURL = 75,
    k_EResultBadResponse = 76,
    k_EResultPasswordReentryRequired = 77,
    k_EResultValueIsOutOfRange = 78,
    k_EResultUnexpectedError = 79,
    k_EResultFeatureDisabled = 80,
    k_EResultInvalidCEGSubmission = 81,
    k_EResultRestrictedDevice = 82,
    k_EResultRegionLocked = 83,
    k_EResultRateLimitExceeded = 84,
    k_EResultAccountLogonDeniedNeedTwofactorCode = 85,
    k_EResultItemOrEntryHasBeenDeleted = 86,
    k_EResultTooManyLogonAttempts = 87,
    k_EResultTwofactorCodeMismatch = 88,
    k_EResultTwofactorActivationCodeMismatch = 89,
    k_EResultAccountAssociatedWithMultiplePlayers = 90,
    k_EResultNotModified = 91,
    k_EResultNoMobileDeviceAvailable = 92,
    k_EResultTimeIsOutOfSync = 93,
    k_EResultSMSCodeFailed = 94,
    k_EResultTooManyAccountsAccessThisResource = 95,
    k_EResultTooManyChangesToThisAccount = 96,
    k_EResultTooManyChangesToThisPhoneNumber = 97,
    k_EResultYouMustRefundThisTransactionToWallet = 98,
    k_EResultSendingOfAnEmailFailed = 99,
    k_EResultPurchaseNotYetSettled = 100,
    k_EResultNeedsCaptcha = 101,
    k_EResultGameserverLoginTokenDenied = 102,
    k_EResultGameserverLoginTokenOwnerDenied = 103,
    k_EResultInvalidItemType = 104,
    k_EResultIPAddressBanned = 105,
    k_EResultGameserverLoginTokenExpired = 106,
    k_EResultInsufficientFunds = 107,
    k_EResultTooManyPending = 108,
    k_EResultNoSiteLicensesFound = 109,
    k_EResultNetworkSendExceeded = 110,
    k_EResultAccountsNotFriends = 111,
    k_EResultLimitedUserAccount = 112,
    k_EResultCantRemoveItem = 113,
    k_EResultAccountHasBeenDeleted = 114,
    k_EResultAccountHasAnExistingUserCancelledLicense = 115,
    k_EResultDeniedDueToCommunityCooldown = 116,
    k_EResultNoLauncherSpecified = 117,
    k_EResultMustAgreeToSSA = 118,
    k_EResultClientNoLongerSupported = 119,
    k_EResultTheCurrentSteamRealmDoesNotMatchTheRequestedResource = 120,
    k_EResultSignatureCheckFailed = 121,
    k_EResultFailedToParseInput = 122,
    k_EResultNoVerifiedPhoneNumber = 123,
    k_EResultInsufficientBatteryCharge = 124,
    k_EResultChargerRequired = 125,
    k_EResultCachedCredentialIsInvalid = 126,
    k_EResultPhoneNumberIsVoiceOverIP = 127,
} EResult;

#endif // ERESULT_H
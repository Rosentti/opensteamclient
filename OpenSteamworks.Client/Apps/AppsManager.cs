
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenSteamworks;
using OpenSteamworks.Attributes;
using OpenSteamworks.Callbacks.Structs;
using OpenSteamworks.Client.Apps.Assets;
using OpenSteamworks.Client.Apps.Compat;
using OpenSteamworks.Client.Config;
using OpenSteamworks.Client.Managers;
using OpenSteamworks.Client.Utils;
using OpenSteamClient.DI;
using OpenSteamworks.ClientInterfaces;
using OpenSteamworks.Data.Enums;
using OpenSteamworks.Generated;
using OpenSteamworks.KeyValue;
using OpenSteamworks.KeyValue.ObjectGraph;
using OpenSteamworks.KeyValue.Deserializers;
using OpenSteamworks.KeyValue.Serializers;
using OpenSteamworks.Messaging;
using OpenSteamworks.Data;
using OpenSteamworks.Data.Structs;
using OpenSteamworks.Utils;
using static OpenSteamworks.Callbacks.CallbackManager;
using OpenSteamClient.DI.Lifetime;
using OpenSteamworks.Callbacks;
using OpenSteamClient.Logging;

namespace OpenSteamworks.Client.Apps;

public class AppPlaytimeChangedEventArgs : EventArgs {
    public AppPlaytimeChangedEventArgs(uint appid, AppPlaytime_t playtime) { AppID = appid; PlaytimeAllTime = TimeSpan.FromMinutes(playtime.AllTime); PlaytimeLastTwoWeeks = TimeSpan.FromMinutes(playtime.LastTwoWeeks); }
    public uint AppID { get; }
    public TimeSpan PlaytimeAllTime { get; }
    public TimeSpan PlaytimeLastTwoWeeks { get; }
}

public class AppLastPlayedChangedEventArgs : EventArgs {
    public AppLastPlayedChangedEventArgs(uint appid, UInt32 lastPlayed) { AppID = appid; LastPlayed = DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime; }
    public uint AppID { get; }
    public DateTime LastPlayed { get; }
}

public class AppsManager : ILogonLifetime
{
    private readonly ISteamClient steamClient;
    private readonly ILogger logger;
    private readonly InstallManager installManager;
    private readonly IContainer container;
    private CompatManager compatManager => container.Get<CompatManager>();

    public readonly ClientApps ClientApps;

    public EventHandler<AppPlaytimeChangedEventArgs>? AppPlaytimeChanged;
    public EventHandler<AppLastPlayedChangedEventArgs>? AppLastPlayedChanged;

    /// <summary>
    /// Gets ALL owned AppIDs of the current user. Includes all configs. Will probably show 1000+ apps.
    /// </summary>
    public HashSet<AppId_t> OwnedAppIDs => ClientApps.OwnedAppIDs;

    /// <summary>
    /// Gets all the user's Games, Tools, etc. Does not include DLC, Configs and other backend types
    /// </summary>
    public HashSet<AppId_t> OwnedApps {
        get {
            return GetAllUserApps().Where(a => steamClient.IClientUser.BIsSubscribedApp(a.AppID)).Select(a => a.AppID).ToHashSet();
        }
    }

    public HashSet<CGameID> OwnedAppsAsGameIDs => OwnedApps.Select(a => new CGameID(a)).ToHashSet();

    public HashSet<AppId_t> InstalledApps {
        get {
            var len = this.steamClient.IClientAppManager.GetNumInstalledApps();
            var arr = new uint[len];
            this.steamClient.IClientAppManager.GetInstalledApps(arr, len);
            return arr.Select(a => (AppId_t)a).ToHashSet();
        }
    }

    //TODO: streamable apps
    public HashSet<AppId_t> ReadyToPlayApps {
        get {
            var apps = InstalledApps;
            apps = apps.Where(this.ClientApps.BIsAppUpToDate).ToHashSet();
            return apps;
        }
    }
    
    private Dictionary<AppId_t, RTime32> appLastPlayedMap = new();
    private Dictionary<AppId_t, AppPlaytime_t> appPlaytimeMap = new();

    public HashSet<AppId_t> PlayedApps {
        get {
            HashSet<AppId_t> apps = new();
            //TODO: There's probably a better way
            foreach (var item in appLastPlayedMap)
            {
                if (item.Value == 0) {
                    continue;
                }

                apps.Add(item.Key);
            }

            return apps;
        }
    }

    public HashSet<AppId_t> UnplayedApps {
        get {
            HashSet<AppId_t> apps = OwnedApps;
            apps = apps.Except(PlayedApps).ToHashSet();
            return apps;
        }
    }

    public AppsManager(ISteamClient steamClient, IContainer container, ClientApps clientApps, InstallManager installManager, ILoggerFactory loggerFactory) {
        this.logger = loggerFactory.CreateLogger("AppsManager");
        this.container = container;
        this.ClientApps = clientApps;
        this.steamClient = steamClient;
        this.installManager = installManager;
        steamClient.CallbackManager.Register<AppMinutesPlayedDataNotice_t>(OnAppMinutesPlayedDataNotice);
        steamClient.CallbackManager.Register<AppLastPlayedTimeChanged_t>(OnAppLastPlayedTimeChanged);
    }

    public void OnAppMinutesPlayedDataNotice(ICallbackHandler handler, AppMinutesPlayedDataNotice_t notice) {
        UInt32 allTime = 0;
        UInt32 lastTwoWeeks = 0;
        if (steamClient.IClientUser.BGetAppMinutesPlayed(notice.m_nAppID, ref allTime, ref lastTwoWeeks))
        {
            this.AppPlaytimeChanged?.Invoke(this, new AppPlaytimeChangedEventArgs(notice.m_nAppID, new AppPlaytime_t(allTime, lastTwoWeeks)));
        }
    }

    public void OnAppLastPlayedTimeChanged(ICallbackHandler handler, AppLastPlayedTimeChanged_t lastPlayedTimeChanged) {
        AppLastPlayedChanged?.Invoke(this, new AppLastPlayedChangedEventArgs(lastPlayedTimeChanged.m_nAppID, lastPlayedTimeChanged.m_lastPlayed));
    }

    public async Task RunLogon(IProgress<OperationProgress> progress) {
        await Task.Run(() =>
        {
            var ownedApps = OwnedAppIDs;
            lock (appsLock)
            {
                for (int i = 0; i < ownedApps.Count; i++)
                {
					progress.Report(new("Loading apps", string.Empty, (i / ownedApps.Count) * 100));
                    var item = ownedApps.ElementAt(i);

                    try
                    {
                        GetApp(item);
                    }
                    catch (System.Exception e2)
                    {
                        ClientApps.AppsFilter.Add(item);
                        logger.Warning("Failed to initialize owned app " + item + " at logon time");
                        logger.Warning(e2);
                    }
                }
            }

            if (steamClient.ConnectedWith == ConnectionType.NewClient) {
                unsafe {
                    CUtlMap<AppId_t, RTime32> mapn = new(0, 4096);
                    if (steamClient.IClientUser.BGetAppsLastPlayedMap(&mapn)) {
                        appLastPlayedMap = mapn.ToManagedAndFree();
                    } else {
                        mapn.Free();
                    }
                }

                unsafe {
                    CUtlMap<AppId_t, AppPlaytime_t> mapn = new(0, 4096);
                    if (steamClient.IClientUser.BGetAppPlaytimeMap(&mapn)) {
                        appPlaytimeMap = mapn.ToManagedAndFree();
                    } else {
                        mapn.Free();
                    }
                }
            } else {
                foreach (var ownedAppID in OwnedApps)
                {
                    var lastPlayed = steamClient.IClientUser.GetAppLastPlayedTime(ownedAppID);
                    if (lastPlayed != 0) {
                        appLastPlayedMap[ownedAppID] = lastPlayed;
                    }

                    var playtime = this.steamClient.ClientConfigStore.GetInt(EConfigStore.UserLocal, $"Software\\Valve\\Steam\\Apps\\{ownedAppID}\\Playtime") ?? 0;
                    var playtime2wks = this.steamClient.ClientConfigStore.GetInt(EConfigStore.UserLocal, $"Software\\Valve\\Steam\\Apps\\{ownedAppID}\\Playtime2wks") ?? 0;
                    if (playtime != 0 || playtime2wks != 0) {
                        appPlaytimeMap[ownedAppID] = new AppPlaytime_t((uint)playtime, (uint)playtime2wks);
                    }
                }
            }
        });
    }

    private readonly object appsLock = new();
        
    /// <summary>
    /// Gets all "user apps", which includes games, apps, betas, tools
    /// </summary>
    public IEnumerable<AppBase> GetAllUserApps() {
        return GetAllOwnedAppsOfTypes([EAppType.Application, EAppType.Beta, EAppType.Game, EAppType.Tool]);
    }

    public IEnumerable<AppBase> GetAllOwnedAppsOfTypes(EAppType[] types) {
        return GetAllOwnedApps().Where(a => types.Contains(a.Type));
    }

    public IEnumerable<AppBase> GetAllOwnedApps() {
        return OwnedAppIDs.Select(GetApp);
    }

    private readonly List<AppBase> appCache = new();

    /// <summary>
    /// Gets an app by CGameID.
    /// </summary>
    /// <param name="gameid"></param>
    /// <returns></returns>
    public AppBase GetApp(CGameID gameid) {
        var existing = appCache.Find(a => a.GameID == gameid);
        if (existing != null) {
            return existing;
        }

        if (gameid.IsShortcut()) {
            AppId_t shortcutAppID = steamClient.IClientShortcuts.GetAppIDForGameID(gameid);
            if (shortcutAppID == AppId_t.Invalid) {
                throw new InvalidOperationException("Shortcut GameID is not registered to IClientShortcuts or it is invalid");
            }

            Logger.GeneralLogger.Trace("GetAppIDForGameID ret: " + shortcutAppID);

            var app = GetShortcutApp(shortcutAppID);
            appCache.Add(app);
            return app;
        }

        if (gameid.IsSteamApp()) {
            return GetApp(gameid.AppID);
        }

        if (gameid.IsMod()) {
            return GetModApp(gameid);
        }

        throw new NotImplementedException("GameID type is not supported, gameid is type: " + gameid.Type + ", val: " + ((ulong)gameid));
    }

    /// <summary>
    /// Gets an app by AppID.
    /// </summary>
    /// <param name="gameid"></param>
    /// <returns></returns>
    public AppBase GetApp(AppId_t appid) {
        var existing = appCache.Find(a => a.AppID == appid);
        if (existing != null) {
            return existing;
        }
        
// #if !_WINDOWS
//         CGameID shortcutGameID = SteamClient.GetInstance().IPCClientShortcuts.GetGameIDForAppID(appid);
// #else
//         CGameID shortcutGameID = CGameID.Zero;
// #endif
        AppBase app;
        var shortcutGameID = CGameID.Zero;
        if (shortcutGameID.IsValid() && shortcutGameID.IsShortcut()) {
            // Handle non-steam appids
            app = GetShortcutApp(appid);
        } else {
            app = AppBase.CreateSteamApp(appid);
        }

        appCache.Add(app);
        return app;
    }

    private AppBase GetShortcutApp(AppId_t shortcutAppID) {
        return AppBase.CreateShortcut(shortcutAppID);
    }

    private AppBase GetModApp(CGameID gameid) {
        return AppBase.CreateSourcemod(gameid, "fakesrcmod-" + Random.Shared.Next());
    }

    /// <summary>
    /// Creates and registers a shortcut app, which you can then customize from the returned object.
    /// </summary>
    /// <returns></returns>
    public ShortcutApp CreateShortcut(string name, string executable, string icon, string shortcutPath, string launchOptions) {
        var createdAppId = steamClient.IClientShortcuts.AddShortcut(name, executable, icon, shortcutPath, launchOptions);
        if (createdAppId == AppId_t.Invalid) {
            throw new Exception("Creating shortcut failed");
        }

        return new ShortcutApp(createdAppId);
    }

    /// <summary>
    /// Creates and registers a temporary shortcut app, which you can then customize from the returned object.
    /// A temporary shortcut will only persist during the current login session and it will disappear when logging out or closing the client.
    /// </summary>
    /// <returns></returns>
    public ShortcutApp CreateTemporaryShortcut(string name, string exepath, string icon) {
        var createdAppId = steamClient.IClientShortcuts.AddTemporaryShortcut(name, exepath, icon);
        if (createdAppId == AppId_t.Invalid) {
            throw new Exception("Creating shortcut failed");
        }

        return new ShortcutApp(createdAppId);
    }

    public async Task RunLogoff(IProgress<OperationProgress> progress) {
        lock (appsLock)
        {
            appLastPlayedMap.Clear();
            appPlaytimeMap.Clear();
        }

        await Task.CompletedTask;
    }

    public void Kill(CGameID gameid) {
        this.steamClient.IClientUser.TerminateGame(gameid, true);
    }

    /// <summary>
    /// May return '' or 'public' depending on the phase of the moon, angle of the sun and some other unknown factors (public seems to be the correct behaviour, does '' stand for failure?)
    /// </summary>
    public string GetBetaForApp(AppId_t appid) {
        IncrementingStringBuilder betaName = new();
        betaName.RunUntilFits(() => steamClient.IClientAppManager.GetActiveBeta(appid, betaName.Data, betaName.Length));
        return betaName.ToString();
    }

    /// <summary>
    /// Gets all owned apps for a SteamID. 
    /// Will not work in offline mode. Yet. TODO: we need a robust caching system.
    /// </summary>
    /// <param name="steamid">SteamID of the user to request apps for</param>
    public async Task<HashSet<AppId_t>> GetAppsForSteamID(CSteamID steamid, bool includeSteamPackageGames = false, bool includeFreeGames = true) {
        logger.Debug("Attempting to get owned apps for " + steamid);
        ProtoMsg<Protobuf.CPlayer_GetOwnedGames_Request> request = new("Player.GetOwnedGames#1");
        request.Body.Steamid = steamid;
        request.Body.IncludeAppinfo = false;
        request.Body.IncludeExtendedAppinfo = false;
        request.Body.IncludeFreeSub = includeSteamPackageGames;
        request.Body.IncludePlayedFreeGames = includeFreeGames;

        ProtoMsg<Protobuf.CPlayer_GetOwnedGames_Response> response;
        HashSet<AppId_t> ownedApps = new();
        using (var conn = SharedConnection.AllocateConnection())
        {
            response = await conn.SendAndWaitForServiceResponse<Protobuf.CPlayer_GetOwnedGames_Response, Protobuf.CPlayer_GetOwnedGames_Request>(request);
        }

        foreach (var protoApp in response.Body.Games)
        {
            // Why the fuck is the AppID field an int here?????
            ownedApps.Add((uint)protoApp.Appid);
        }

        logger.Debug(steamid + " owns " + ownedApps.Count + " games");
        return ownedApps;
    }

    public async Task RunInstallScriptAsync(AppId_t appid, bool uninstall = false) {
        await Task.Run(() => RunInstallScriptSync(appid, uninstall));
    }

    public void RunInstallScriptSync(AppId_t appid, bool uninstall = false) {
        //TODO: we still aren't 100% sure about the second arg.
        steamClient.IClientUser.RunInstallScript(appid, "english", false);
        while (steamClient.IClientUser.IsInstallScriptRunning() != 0)
        {
            Thread.Sleep(30);
        }
    }

    public void SetDefaultCompatToolForApp(CGameID gameid) {
        string defaultCompatTool = this.steamClient.IClientCompat.GetCompatToolName(0);
        if (string.IsNullOrEmpty(defaultCompatTool)) {
            throw new InvalidOperationException("Can't set default config tool for app " + gameid + ", since no default compat tool has been specified!");
        }

        this.SetCompatToolForApp(gameid, defaultCompatTool);
    }

    public void DisableCompatToolForApp(CGameID gameid) {
        this.steamClient.IClientCompat.SpecifyCompatTool(gameid.AppID, "", "", 250);
    }

    public string GetCurrentCompatToolForApp(CGameID gameid) {
        if (!this.steamClient.IClientCompat.BIsCompatibilityToolEnabled(gameid.AppID)) {
            return "";
        }
        
        return this.steamClient.IClientCompat.GetCompatToolName(gameid.AppID);
    }

    public void SetCompatToolForApp(CGameID gameid, string compatToolName) {
        this.steamClient.IClientCompat.SpecifyCompatTool(gameid.AppID, compatToolName, "", 250);
    }

    public void SetDefaultCompatTool(string compatToolName) {
        this.steamClient.IClientCompat.SpecifyCompatTool(0, compatToolName, "", 250);
    }

    public bool IsAppInstalled(AppId_t appid) {
        return InstalledApps.Contains(appid);
    }

    public async Task<EAppError> LaunchApp(AppId_t app, int launchOption = -1, string userLaunchOptions = "", ELaunchSource launchSource = ELaunchSource.None) {
        return await LaunchApp(GetApp(app), launchOption, userLaunchOptions, launchSource);
    }

    public async Task<EAppError> LaunchApp(AppBase app, int launchOption, string userLaunchOptions, ELaunchSource launchSource = ELaunchSource.None) {
        return await app.Launch(userLaunchOptions, launchOption, launchSource);
    }
    
    public ILogger GetLoggerForApp(AppBase app) {
		if (logger is Logger impl) {
			// Create a sub logger.
			return impl.CreateSubLogger(app.GameID.ToString());
		}

		// Can't do it.
		return logger;
	}

    public UInt64 StartCompatSession(AppId_t appID) => this.steamClient.IClientCompat.StartSession(appID);

    /// <summary>
    /// Gets the current effective OS for an app. 
    /// If the game has compat tools enabled, it will use the compat tool's target OS, otherwise the current os.
    /// </summary>
    /// <param name="appID"></param>
    /// <returns></returns>
    public string GetCurrentEffectiveOSForApp(AppId_t appID)
    {
        if (compatManager.IsCompatEnabled && compatManager.IsCompatEnabledForApp(appID)) {
            return compatManager.GetPlatformStringForCompatTool(compatManager.GetCompatToolForApp(appID));
        }

        return UtilityFunctions.GetSteamPlatformString();
    }
}
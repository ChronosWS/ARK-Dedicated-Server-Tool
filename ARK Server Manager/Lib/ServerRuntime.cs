using ARK_Server_Manager.Lib.Model;
using ArkServerManager.Plugin.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WPFSharp.Globalizer;

namespace ARK_Server_Manager.Lib
{
    public class ServerRuntime : DependencyObject, IDisposable
    {
        private const int DIRECTORIES_PER_LINE = 200;
        private const int MOD_STATUS_QUERY_DELAY = 900000; // milliseconds

        public event EventHandler StatusUpdate;

        public enum ServerStatus
        {
            Unknown,
            Stopping,
            Stopped,
            Initializing,
            Running,
            Updating,
            Uninstalled
        }

        public enum SteamStatus
        {
            Unknown,
            NeedPublicIP,
            Unavailable,
            WaitingForPublication,
            Available
        }

        private readonly GlobalizedApplication _globalizer = GlobalizedApplication.Instance;
        private readonly List<PropertyChangeNotifier> profileNotifiers = new List<PropertyChangeNotifier>();
        private Process serverProcess;
        private IAsyncDisposable updateRegistration;
        private DateTime lastModStatusQuery = DateTime.MinValue;

        #region Properties

        public static readonly DependencyProperty SteamProperty = DependencyProperty.Register(nameof(Steam), typeof(SteamStatus), typeof(ServerRuntime), new PropertyMetadata(SteamStatus.Unknown));
        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(ServerStatus), typeof(ServerRuntime), new PropertyMetadata(ServerStatus.Unknown));
        public static readonly DependencyProperty StatusStringProperty = DependencyProperty.Register(nameof(StatusString), typeof(string), typeof(ServerRuntime), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty MaxPlayersProperty = DependencyProperty.Register(nameof(MaxPlayers), typeof(int), typeof(ServerRuntime), new PropertyMetadata(0));
        public static readonly DependencyProperty PlayersProperty = DependencyProperty.Register(nameof(Players), typeof(int), typeof(ServerRuntime), new PropertyMetadata(0));
        public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(nameof(Version), typeof(Version), typeof(ServerRuntime), new PropertyMetadata(new Version()));
        public static readonly DependencyProperty ProfileSnapshotProperty = DependencyProperty.Register(nameof(ProfileSnapshot), typeof(ServerProfileSnapshot), typeof(ServerRuntime), new PropertyMetadata(null));
        public static readonly DependencyProperty TotalModCountProperty = DependencyProperty.Register(nameof(TotalModCount), typeof(int), typeof(ServerRuntime), new PropertyMetadata(0));
        public static readonly DependencyProperty OutOfDateModCountProperty = DependencyProperty.Register(nameof(OutOfDateModCount), typeof(int), typeof(ServerRuntime), new PropertyMetadata(0));

        public SteamStatus Steam
        {
            get { return (SteamStatus)GetValue(SteamProperty); }
            protected set { SetValue(SteamProperty, value); }
        }

        public ServerStatus Status
        {
            get { return (ServerStatus)GetValue(StatusProperty); }
            protected set { SetValue(StatusProperty, value); }
        }

        public string StatusString
        {
            get { return (string)GetValue(StatusStringProperty); }
            protected set { SetValue(StatusStringProperty, value); }
        }

        public int MaxPlayers
        {
            get { return (int)GetValue(MaxPlayersProperty); }
            protected set { SetValue(MaxPlayersProperty, value); }
        }

        public int Players
        {
            get { return (int)GetValue(PlayersProperty); }
            protected set { SetValue(PlayersProperty, value); }
        }

        public Version Version
        {
            get { return (Version)GetValue(VersionProperty); }
            protected set { SetValue(VersionProperty, value); }
        }

        public ServerProfileSnapshot ProfileSnapshot
        {
            get { return (ServerProfileSnapshot)GetValue(ProfileSnapshotProperty); }
            set { SetValue(ProfileSnapshotProperty, value); }
        }

        public int TotalModCount
        {
            get { return (int)GetValue(TotalModCountProperty); }
            protected set { SetValue(TotalModCountProperty, value); }
        }

        public int OutOfDateModCount
        {
            get { return (int)GetValue(OutOfDateModCountProperty); }
            protected set { SetValue(OutOfDateModCountProperty, value); }
        }

        #endregion

        public void Dispose()
        {
            this.updateRegistration?.DisposeAsync().DoNotWait();
        }

        public Task AttachToProfile(ServerProfile profile)
        {
            AttachToProfileCore(profile);
            GetProfilePropertyChanges(profile);
            return TaskUtils.FinishedTask;
        }

        private void AttachToProfileCore(ServerProfile profile)
        {
            UnregisterForUpdates();

            this.ProfileSnapshot = ServerProfileSnapshot.Create(profile);

            if (Version.TryParse(profile.LastInstalledVersion, out Version lastInstalled))
            {
                this.Version = lastInstalled;
            }

            this.lastModStatusQuery = DateTime.MinValue;

            RegisterForUpdates();
        }

        private void GetProfilePropertyChanges(ServerProfile profile)
        {
            foreach(var notifier in profileNotifiers)
            {
                notifier.Dispose();
            }

            profileNotifiers.Clear();
            profileNotifiers.AddRange(PropertyChangeNotifier.GetNotifiers(
                profile,
                new[] {
                    ServerProfile.ProfileNameProperty,
                    ServerProfile.InstallDirectoryProperty,
                    ServerProfile.QueryPortProperty,
                    ServerProfile.ServerPortProperty,
                    ServerProfile.ServerIPProperty,
                    ServerProfile.MaxPlayersProperty,

                    ServerProfile.ServerMapProperty,
                    ServerProfile.ServerModIdsProperty,
                    ServerProfile.TotalConversionModIdProperty,
                },
                (s, p) =>
                {
                    if (Status == ServerStatus.Stopped || Status == ServerStatus.Uninstalled || Status == ServerStatus.Unknown)
                    {
                        AttachToProfileCore(profile);
                    }
                }));
        }

        private void GetServerEndpoints(out IPEndPoint localServerQueryEndPoint, out IPEndPoint steamServerQueryEndPoint)
        {
            localServerQueryEndPoint = null;
            steamServerQueryEndPoint = null;

            //
            // Get the local endpoint for querying the local network
            //
            if (!ushort.TryParse(this.ProfileSnapshot.QueryPort.ToString(), out ushort port))
            {
                Debug.WriteLine($"Port is out of range ({this.ProfileSnapshot.QueryPort})");
                return;
            }

            IPAddress localServerIpAddress;
            if (!String.IsNullOrWhiteSpace(this.ProfileSnapshot.ServerIP) && IPAddress.TryParse(this.ProfileSnapshot.ServerIP, out localServerIpAddress))
            {
                // Use the explicit Server IP
                localServerQueryEndPoint = new IPEndPoint(localServerIpAddress, Convert.ToUInt16(this.ProfileSnapshot.QueryPort));
            }
            else
            {
                // No Server IP specified, use Loopback
                localServerQueryEndPoint = new IPEndPoint(IPAddress.Loopback, Convert.ToUInt16(this.ProfileSnapshot.QueryPort));
            }

            //
            // Get the public endpoint for querying Steam
            //
            steamServerQueryEndPoint = null;
            if (!String.IsNullOrWhiteSpace(Config.Default.MachinePublicIP))
            {
                IPAddress steamServerIpAddress;
                if (IPAddress.TryParse(Config.Default.MachinePublicIP, out steamServerIpAddress))
                {
                    // Use the Public IP explicitly specified
                    steamServerQueryEndPoint = new IPEndPoint(steamServerIpAddress, Convert.ToUInt16(this.ProfileSnapshot.QueryPort));
                }
                else
                {
                    // Resolve the IP from the DNS name provided
                    try
                    {
                        var addresses = Dns.GetHostAddresses(Config.Default.MachinePublicIP);
                        if (addresses.Length > 0)
                        {
                            steamServerQueryEndPoint = new IPEndPoint(addresses[0], Convert.ToUInt16(this.ProfileSnapshot.QueryPort));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Failed to resolve DNS address {0}: {1}\r\n{2}", Config.Default.MachinePublicIP, ex.Message, ex.StackTrace);
                    }
                }
            }
        }

        public string GetServerExe()
        {
            return Path.Combine(this.ProfileSnapshot.InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerExe);
        }

        public string GetServerLauncherFile()
        {
            return Path.Combine(this.ProfileSnapshot.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.LauncherFile);
        }

        private void ProcessStatusUpdate(IAsyncDisposable registration, ServerStatusWatcher.ServerStatusUpdate update)
        {
            if(!Object.ReferenceEquals(registration, this.updateRegistration))
            {
                return;
            }

            TaskUtils.RunOnUIThreadAsync(() =>
            {
                var oldStatus = this.Status;
                switch (update.Status)
                {
                    case ServerStatusWatcher.ServerStatus.NotInstalled:
                        if (oldStatus != ServerStatus.Updating)
                            UpdateServerStatus(ServerStatus.Uninstalled, SteamStatus.Unavailable, false);
                        break;

                    case ServerStatusWatcher.ServerStatus.Initializing:
                        if (oldStatus != ServerStatus.Stopping)
                            UpdateServerStatus(ServerStatus.Initializing, SteamStatus.Unavailable, oldStatus != ServerStatus.Initializing && oldStatus != ServerStatus.Unknown);
                        break;

                    case ServerStatusWatcher.ServerStatus.Stopped:
                        if (oldStatus != ServerStatus.Updating)
                            UpdateServerStatus(ServerStatus.Stopped, SteamStatus.Unavailable, oldStatus == ServerStatus.Initializing || oldStatus == ServerStatus.Running || oldStatus == ServerStatus.Stopping);
                        break;

                    case ServerStatusWatcher.ServerStatus.Unknown:
                        if (oldStatus != ServerStatus.Updating)
                            UpdateServerStatus(ServerStatus.Unknown, SteamStatus.Unknown, false);
                        break;

                    case ServerStatusWatcher.ServerStatus.RunningLocalCheck:
                        if (oldStatus != ServerStatus.Stopping)
                            UpdateServerStatus(ServerStatus.Running, this.Steam != SteamStatus.Available ? SteamStatus.WaitingForPublication : this.Steam, oldStatus != ServerStatus.Running && oldStatus != ServerStatus.Unknown);
                        break;

                    case ServerStatusWatcher.ServerStatus.RunningExternalCheck:
                        if (oldStatus != ServerStatus.Stopping)
                            UpdateServerStatus(ServerStatus.Running, SteamStatus.WaitingForPublication, oldStatus != ServerStatus.Running && oldStatus != ServerStatus.Unknown);
                        break;

                    case ServerStatusWatcher.ServerStatus.Published:
                        if (oldStatus != ServerStatus.Stopping)
                            UpdateServerStatus(ServerStatus.Running, SteamStatus.Available, oldStatus != ServerStatus.Running && oldStatus != ServerStatus.Unknown);
                        break;
                }

                this.Players = update.Players?.Count ?? 0;
                this.MaxPlayers = update.ServerInfo?.MaxPlayers ?? this.ProfileSnapshot.MaxPlayerCount;

                if (update.ServerInfo != null)
                {
                    var match = Regex.Match(update.ServerInfo.Name, @"\(v([0-9]+\.[0-9]*)\)");
                    if (match.Success && match.Groups.Count >= 2)
                    {
                        var serverVersion = match.Groups[1].Value;
                        if (!String.IsNullOrWhiteSpace(serverVersion) && Version.TryParse(serverVersion, out Version temp))
                        {
                            this.Version = temp;
                        }
                    }
                }

                UpdateModStatus();

                this.serverProcess = update.Process;

                StatusUpdate?.Invoke(this, EventArgs.Empty);
            }).DoNotWait();
        }

        private async void UpdateModStatus()
        {
            if (DateTime.Now < this.lastModStatusQuery.AddMilliseconds(MOD_STATUS_QUERY_DELAY))
                return;

            var totalModCount = 0;
            var outOfdateModCount = 0;

            var modIdList = new List<string>();
            if (!string.IsNullOrWhiteSpace(this.ProfileSnapshot.ServerMapModId))
                modIdList.Add(this.ProfileSnapshot.ServerMapModId);
            if (!string.IsNullOrWhiteSpace(this.ProfileSnapshot.TotalConversionModId))
                modIdList.Add(this.ProfileSnapshot.TotalConversionModId);
            modIdList.AddRange(this.ProfileSnapshot.ServerModIds);

            var newModIdList = ModUtils.ValidateModList(modIdList);
            totalModCount = newModIdList.Count;

            if (totalModCount > 0)
            {
                var response = await Task.Run(() => SteamUtils.GetSteamModDetails(newModIdList));

                var modDetails = ModDetailList.GetModDetails(response, Path.Combine(this.ProfileSnapshot.InstallDirectory, Config.Default.ServerModsRelativePath), newModIdList);
                outOfdateModCount = modDetails.Count(m => !m.UpToDate);
            }

            this.TotalModCount = totalModCount;
            this.OutOfDateModCount = outOfdateModCount;
            this.lastModStatusQuery = DateTime.Now;

            Debug.WriteLine($"UpdateModStatus performed - {this.ProfileSnapshot.ProfileName} - {outOfdateModCount} / {totalModCount}");
        }

        private void RegisterForUpdates()
        {
            if (this.updateRegistration == null)
            {
                GetServerEndpoints(out IPEndPoint localServerQueryEndPoint, out IPEndPoint steamServerQueryEndPoint);
                if (localServerQueryEndPoint == null || steamServerQueryEndPoint == null)
                    return;

                this.updateRegistration = ServerStatusWatcher.Instance.RegisterForUpdates(this.ProfileSnapshot.InstallDirectory, this.ProfileSnapshot.ProfileId, localServerQueryEndPoint, steamServerQueryEndPoint, ProcessStatusUpdate);
            }
        }

        private void UnregisterForUpdates()
        {
            this.updateRegistration?.DisposeAsync().DoNotWait();
            this.updateRegistration = null;
        }


        private void CheckServerWorldFileExists()
        {
            var serverApp = new ServerApp()
            {
                BackupWorldFile = false,
                DeleteOldServerBackupFiles = false,
                SendAlerts = false,
                SendEmails = false,
                OutputLogs = false
            };
            serverApp.CheckServerWorldFileExists(ProfileSnapshot);
        }

        public Task StartAsync()
        {
            if(!Environment.Is64BitOperatingSystem)
            {
                MessageBox.Show("ARK: Survival Evolved(tm) Server requires a 64-bit operating system to run.  Your operating system is 32-bit and therefore the Ark Server Manager cannot start the server.  You may still load and save profiles and settings files for use on other machines.", "64-bit OS Required", MessageBoxButton.OK, MessageBoxImage.Error);
                return TaskUtils.FinishedTask;
            }

            switch(this.Status)
            {
                case ServerStatus.Running:
                case ServerStatus.Initializing:
                case ServerStatus.Stopping:
                    Debug.WriteLine("Server {0} already running.", this.ProfileSnapshot.ProfileName);
                    return TaskUtils.FinishedTask;
            }

            UnregisterForUpdates();

            var serverExe = GetServerExe();
            var launcherExe = GetServerLauncherFile();

            if (Config.Default.ManageFirewallAutomatically)
            {
                var ports = new List<int>() { this.ProfileSnapshot.ServerPort , this.ProfileSnapshot.QueryPort };
                if(this.ProfileSnapshot.UseRawSockets)
                {
                    ports.Add(this.ProfileSnapshot.ServerPort + 1);
                }
                if (this.ProfileSnapshot.RCONEnabled)
                {
                    ports.Add(this.ProfileSnapshot.RCONPort);
                }

                if (!FirewallUtils.EnsurePortsOpen(serverExe, ports.ToArray(), $"{Config.Default.FirewallRulePrefix} {this.ProfileSnapshot.ServerName}"))
                {
                    var result = MessageBox.Show("Failed to automatically set firewall rules.  If you are running custom firewall software, you may need to set your firewall rules manually.  You may turn off automatic firewall management in Settings.\r\n\r\nWould you like to continue running the server anyway?", "Automatic Firewall Management Error", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        return TaskUtils.FinishedTask;
                    }
                }
            }

            CheckServerWorldFileExists();

            try
            {
                var startInfo = new ProcessStartInfo()
                {
                    FileName = launcherExe
                };

                var process = Process.Start(startInfo);
                process.EnableRaisingEvents = true;
            }
            catch (Win32Exception ex)
            {
                throw new FileNotFoundException(String.Format("Unable to find {0} at {1}.  Server Install Directory: {2}", Config.Default.LauncherFile, launcherExe, this.ProfileSnapshot.InstallDirectory), launcherExe, ex);
            }
            finally
            {
                RegisterForUpdates();
            }
            
            return TaskUtils.FinishedTask;            
        }

        public async Task StopAsync()
        {
            switch(this.Status)
            {
                case ServerStatus.Running:
                case ServerStatus.Initializing:
                    try
                    {
                        if (this.serverProcess != null)
                        {
                            UpdateServerStatus(ServerStatus.Stopping, SteamStatus.Unavailable, true);

                            await ProcessUtils.SendStopAsync(this.serverProcess)
                                .ContinueWith(async t1 =>
                                {
                                    if (this.serverProcess.HasExited)
                                    {
                                        await TaskUtils.RunOnUIThreadAsync(() => CheckServerWorldFileExists());
                                    }
                                })
                                .ContinueWith(async t2 =>
                                {
                                    await TaskUtils.RunOnUIThreadAsync(() => UpdateServerStatus(ServerStatus.Stopped, SteamStatus.Unavailable, false));
                                });
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        UpdateServerStatus(ServerStatus.Unknown, SteamStatus.Unavailable, false);
                    }
                    break;
            }            
        }


        public async Task<bool> UpgradeAsync(CancellationToken cancellationToken, bool updateServer, ServerBranchSnapshot branch, bool validate, bool updateMods, ProgressDelegate progressCallback)
        {
            return await UpgradeAsync(cancellationToken, updateServer, branch, validate, updateMods, null, progressCallback);
        }

        public async Task<bool> UpgradeAsync(CancellationToken cancellationToken, bool updateServer, ServerBranchSnapshot branch, bool validate, bool updateMods, string[] updateModIds, ProgressDelegate progressCallback)
        {
            if (updateServer && !Environment.Is64BitOperatingSystem)
            {
                var result = MessageBox.Show("The ARK server requires a 64-bit operating system to run. Your operating system is 32-bit and therefore the Ark Server Manager will be unable to start the server, but you may still install it or load and save profiles and settings files for use on other machines.\r\n\r\nDo you wish to continue?", "64-bit OS Required", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return false;
                }
            }

            try
            {
                await StopAsync();

                bool isNewInstallation = this.Status == ServerStatus.Uninstalled;

                UpdateServerStatus(ServerStatus.Updating, Steam, false);

                // Run the SteamCMD to install the server
                var steamCmdFile = SteamCmdUpdater.GetSteamCmdFile();
                if (string.IsNullOrWhiteSpace(steamCmdFile) || !File.Exists(steamCmdFile))
                {
                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***********************************");
                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: SteamCMD could not be found. Expected location is {steamCmdFile}");
                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***********************************");
                    return false;
                }

                // record the start time of the process, this is used to determine if any files changed in the download process.
                var startTime = DateTime.Now;

                var gotNewVersion = false;
                var downloadSuccessful = false;
                var success = false;

                if (updateServer)
                {
                    // *********************
                    // Server Update Section
                    // *********************

                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Starting server update.");
                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Server branch: {ServerApp.GetBranchName(branch?.BranchName)}.");

                    // create the branch arguments
                    var steamCmdInstallServerBetaArgs = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(branch?.BranchName))
                    {
                        steamCmdInstallServerBetaArgs.AppendFormat(Config.Default.SteamCmdInstallServerBetaNameArgsFormat, branch.BranchName);
                        if (!string.IsNullOrWhiteSpace(branch?.BranchPassword))
                        {
                            steamCmdInstallServerBetaArgs.Append(" ");
                            steamCmdInstallServerBetaArgs.AppendFormat(Config.Default.SteamCmdInstallServerBetaPasswordArgsFormat, branch?.BranchPassword);
                        }
                    }

                    // Check if this is a new server installation.
                    if (isNewInstallation && !this.ProfileSnapshot.SotFEnabled && Config.Default.AutoUpdate_EnableUpdate && !string.IsNullOrWhiteSpace(Config.Default.AutoUpdate_CacheDir))
                    {
                        var branchName = string.IsNullOrWhiteSpace(branch?.BranchName) ? Config.Default.DefaultServerBranchName : branch.BranchName;
                        var cacheFolder = IOUtils.NormalizePath(Path.Combine(Config.Default.AutoUpdate_CacheDir, $"{Config.Default.ServerBranchFolderPrefix}{branchName}"));

                        // check if the auto-update facility is enabled and the cache folder defined.
                        if (!string.IsNullOrWhiteSpace(cacheFolder) && Directory.Exists(cacheFolder))
                        {
                            // Auto-Update enabled and cache foldler exists.
                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Installing server from local cache...may take a while to copy all the files.");

                            // Install the server files from the cache.
                            var installationFolder = this.ProfileSnapshot.InstallDirectory;
                            int count = 0;
                            await Task.Run(() =>
                                ServerApp.DirectoryCopy(cacheFolder, installationFolder, true, Config.Default.AutoUpdate_UseSmartCopy, (p, m, n) =>
                                    {
                                        count++;
                                        progressCallback?.Invoke(0, ".", count % DIRECTORIES_PER_LINE == 0);
                                    }), cancellationToken);
                        }
                    }

                    progressCallback?.Invoke(0, "\r\n");
                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Updating server from steam.\r\n");

                    downloadSuccessful = !Config.Default.SteamCmdRedirectOutput;
                    DataReceivedEventHandler serverOutputHandler = (s, e) =>
                    {
                        var dataValue = e.Data ?? string.Empty;
                        progressCallback?.Invoke(0, dataValue);
                        if (!gotNewVersion && dataValue.Contains("downloading,"))
                        {
                            gotNewVersion = true;
                        }
                        if (dataValue.StartsWith("Success!"))
                        {
                            downloadSuccessful = true;
                        }
                    };

                    var steamCmdInstallServerArgsFormat = this.ProfileSnapshot.SotFEnabled ? Config.Default.SteamCmdInstallServerArgsFormat_SotF : Config.Default.SteamCmdInstallServerArgsFormat;
                    var steamCmdArgs = String.Format(steamCmdInstallServerArgsFormat, this.ProfileSnapshot.InstallDirectory, steamCmdInstallServerBetaArgs, validate ? "validate" : string.Empty);

                    success = await ServerUpdater.UpgradeServerAsync(steamCmdFile, steamCmdArgs, this.ProfileSnapshot.InstallDirectory, Config.Default.SteamCmdRedirectOutput ? serverOutputHandler : null, cancellationToken, ProcessWindowStyle.Minimized);
                    if (success && downloadSuccessful)
                    {
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Finished server update.");

                        if (Directory.Exists(this.ProfileSnapshot.InstallDirectory))
                        {
                            if (!Config.Default.SteamCmdRedirectOutput)
                                // check if any of the server files have changed.
                                gotNewVersion = ServerApp.HasNewServerVersion(this.ProfileSnapshot.InstallDirectory, startTime);

                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} New server version - {gotNewVersion.ToString().ToUpperInvariant()}.");
                        }

                        progressCallback?.Invoke(0, "\r\n");
                    }
                    else
                    {
                        success = false;
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ****************************");
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Failed server update.");
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ****************************\r\n");

                        if (Config.Default.SteamCmdRedirectOutput)
                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} If the server update keeps failing try disabling the '{_globalizer.GetResourceString("GlobalSettings_SteamCmdRedirectOutputLabel")}' option in the settings window.\r\n");
                    }
                }
                else
                    success = true;

                if (success)
                {
                    if (updateMods)
                    {
                        // ******************
                        // Mod Update Section
                        // ******************

                        // build a list of mods to be processed
                        var modIdList = new List<string>();
                        if (updateModIds == null || updateModIds.Length == 0)
                        {
                            if (!string.IsNullOrWhiteSpace(this.ProfileSnapshot.ServerMapModId))
                                modIdList.Add(this.ProfileSnapshot.ServerMapModId);
                            if (!string.IsNullOrWhiteSpace(this.ProfileSnapshot.TotalConversionModId))
                                modIdList.Add(this.ProfileSnapshot.TotalConversionModId);
                            modIdList.AddRange(this.ProfileSnapshot.ServerModIds);
                        }
                        else
                        {
                            modIdList.AddRange(updateModIds);
                        }

                        modIdList = ModUtils.ValidateModList(modIdList);

                        // get the details of the mods to be processed.
                        var modDetails = SteamUtils.GetSteamModDetails(modIdList);

                        // check if the mod details were retrieved
                        if (modDetails == null && Config.Default.ServerUpdate_ForceUpdateModsIfNoSteamInfo)
                        {
                            modDetails = new Model.PublishedFileDetailsResponse();
                        }

                        if (modDetails != null)
                        {
                            // create a new list for any failed mod updates
                            var failedMods = new List<string>(modIdList.Count);

                            for (var index = 0; index < modIdList.Count; index++)
                            {
                                var modId = modIdList[index];
                                var modTitle = modId;
                                var modSuccess = false;
                                gotNewVersion = false;
                                downloadSuccessful = false;

                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Started processing mod {index + 1} of {modIdList.Count}.");
                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Mod {modId}.");

                                // check if the steam information was downloaded
                                var modDetail = modDetails.publishedfiledetails?.FirstOrDefault(m => m.publishedfileid.Equals(modId, StringComparison.OrdinalIgnoreCase));
                                modTitle = $"{modId} - {modDetail?.title ?? "<unknown>"}";

                                if (modDetail != null)
                                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} {modDetail.title}.\r\n");

                                var modCachePath = ModUtils.GetModCachePath(modId, this.ProfileSnapshot.SotFEnabled);
                                var cacheTimeFile = ModUtils.GetLatestModCacheTimeFile(modId, this.ProfileSnapshot.SotFEnabled);
                                var modPath = ModUtils.GetModPath(this.ProfileSnapshot.InstallDirectory, modId);
                                var modTimeFile = ModUtils.GetLatestModTimeFile(this.ProfileSnapshot.InstallDirectory, modId);

                                var modCacheLastUpdated = 0;
                                var downloadMod = true;
                                var copyMod = true;
                                var updateError = false;

                                if (downloadMod)
                                {
                                    // check if the mod needs to be downloaded, or force the download.
                                    if (Config.Default.ServerUpdate_ForceUpdateMods)
                                    {
                                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Forcing mod download - ASM setting is TRUE.");
                                    }
                                    else if (modDetail == null)
                                    {
                                        if (Config.Default.ServerUpdate_ForceUpdateModsIfNoSteamInfo)
                                        {
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Forcing mod download - Mod details not available and ASM setting is TRUE.");
                                        }
                                        else
                                        {
                                            // no steam information downloaded, display an error, mod might no longer be available
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} *******************************************************************");
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Mod cannot be updated, unable to download steam information.");
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} *******************************************************************");

                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} If the mod update keeps failing try enabling the '{_globalizer.GetResourceString("GlobalSettings_ForceUpdateModsIfNoSteamInfoLabel")}' option in the settings window.\r\n");

                                            downloadMod = false;
                                            copyMod = false;
                                            updateError = true;
                                        }
                                    }
                                    else
                                    {
                                        // check if the mod detail record is valid (private mod).
                                        if (modDetail.time_updated <= 0)
                                        {
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Forcing mod download - mod is private.");
                                        }
                                        else
                                        {
                                            modCacheLastUpdated = ModUtils.GetModLatestTime(cacheTimeFile);
                                            if (modCacheLastUpdated <= 0)
                                            {
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Forcing mod download - mod cache is not versioned.");
                                            }
                                            else
                                            {
                                                var steamLastUpdated = modDetail.time_updated;
                                                if (steamLastUpdated <= modCacheLastUpdated)
                                                {
                                                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Skipping mod download - mod cache has the latest version.");
                                                    downloadMod = false;
                                                }
                                            }
                                        }
                                    }

                                    if (downloadMod)
                                    {
                                        // mod will be downloaded
                                        downloadSuccessful = !Config.Default.SteamCmdRedirectOutput;
                                        DataReceivedEventHandler modOutputHandler = (s, e) =>
                                        {
                                            var dataValue = e.Data ?? string.Empty;
                                            progressCallback?.Invoke(0, dataValue);
                                            if (dataValue.StartsWith("Success."))
                                            {
                                                downloadSuccessful = true;
                                            }
                                        };

                                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Starting mod download.\r\n");

                                        var steamCmdArgs = string.Empty;
                                        if (this.ProfileSnapshot.SotFEnabled)
                                        {
                                            if (Config.Default.SteamCmd_UseAnonymousCredentials)
                                                steamCmdArgs = string.Format(Config.Default.SteamCmdInstallModArgsFormat_SotF, Config.Default.SteamCmd_AnonymousUsername, modId);
                                            else
                                                steamCmdArgs = string.Format(Config.Default.SteamCmdInstallModArgsFormat_SotF, Config.Default.SteamCmd_Username, modId);
                                        }
                                        else
                                        {
                                            if (Config.Default.SteamCmd_UseAnonymousCredentials)
                                                steamCmdArgs = string.Format(Config.Default.SteamCmdInstallModArgsFormat, Config.Default.SteamCmd_AnonymousUsername, modId);
                                            else
                                                steamCmdArgs = string.Format(Config.Default.SteamCmdInstallModArgsFormat, Config.Default.SteamCmd_Username, modId);
                                        }

                                        modSuccess = await ServerUpdater.UpgradeModsAsync(steamCmdFile, steamCmdArgs, Config.Default.SteamCmdRedirectOutput ? modOutputHandler : null, cancellationToken, ProcessWindowStyle.Minimized);
                                        if (modSuccess && downloadSuccessful)
                                        {
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Finished mod download.");
                                            copyMod = true;

                                            if (Directory.Exists(modCachePath))
                                            {
                                                // check if any of the mod files have changed.
                                                gotNewVersion = new DirectoryInfo(modCachePath).GetFiles("*.*", SearchOption.AllDirectories).Any(file => file.LastWriteTime >= startTime);

                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} New mod version - {gotNewVersion.ToString().ToUpperInvariant()}.");

                                                var steamLastUpdated = modDetail?.time_updated.ToString() ?? string.Empty;
                                                if (modDetail == null || modDetail.time_updated <= 0)
                                                {
                                                    // get the version number from the steamcmd workshop file.
                                                    steamLastUpdated = ModUtils.GetSteamWorkshopLatestTime(ModUtils.GetSteamWorkshopFile(this.ProfileSnapshot.SotFEnabled), modId).ToString();
                                                }

                                                // update the last updated file with the steam updated time.
                                                File.WriteAllText(cacheTimeFile, steamLastUpdated);

                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Mod Cache version: {steamLastUpdated}\r\n");
                                            }
                                        }
                                        else
                                        {
                                            modSuccess = false;
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***************************");
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Mod download failed.");
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***************************\r\n");

                                            if (Config.Default.SteamCmdRedirectOutput)
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} If the mod update keeps failing try disabling the '{_globalizer.GetResourceString("GlobalSettings_SteamCmdRedirectOutputLabel")}' option in the settings window.\r\n");
                                            copyMod = false;
                                        }
                                    }
                                    else
                                        modSuccess = !updateError;
                                }
                                else
                                    modSuccess = !updateError;

                                if (copyMod)
                                {
                                    // check if the mod needs to be copied, or force the copy.
                                    if (Config.Default.ServerUpdate_ForceCopyMods)
                                    {
                                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Forcing mod copy - ASM setting is TRUE.");
                                    }
                                    else
                                    {
                                        // check the mod version against the cache version.
                                        var modLastUpdated = ModUtils.GetModLatestTime(modTimeFile);
                                        if (modLastUpdated <= 0)
                                        {
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Forcing mod copy - mod is not versioned.");
                                        }
                                        else
                                        {
                                            modCacheLastUpdated = ModUtils.GetModLatestTime(cacheTimeFile);
                                            if (modCacheLastUpdated <= modLastUpdated)
                                            {
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Skipping mod copy - mod has the latest version.");
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Mod version: {modLastUpdated}");
                                                copyMod = false;
                                            }
                                        }
                                    }

                                    if (copyMod)
                                    {
                                        try
                                        {
                                            if (Directory.Exists(modCachePath))
                                            {
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Started mod copy.");
                                                int count = 0;
                                                await Task.Run(() => ModUtils.CopyMod(modCachePath, modPath, modId, (p, m, n) =>
                                                                                                                    {
                                                                                                                        count++;
                                                                                                                        progressCallback?.Invoke(0, ".", count % DIRECTORIES_PER_LINE == 0);
                                                                                                                    }), cancellationToken);
                                                progressCallback?.Invoke(0, "\r\n");
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Finished mod copy.");

                                                var modLastUpdated = ModUtils.GetModLatestTime(modTimeFile);
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Mod version: {modLastUpdated}");
                                            }
                                            else
                                            {
                                                modSuccess = false;
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ****************************************************");
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Mod cache was not found, mod was not updated.");
                                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ****************************************************");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            modSuccess = false;
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***********************");
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Failed mod copy.\r\n{ex.Message}");
                                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***********************");
                                        }
                                    }
                                }

                                if (!modSuccess)
                                {
                                    success = false;
                                    failedMods.Add($"{index + 1} of {modIdList.Count} - {modTitle}");
                                }

                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Finished processing mod {modId}.\r\n");
                            }

                            if (failedMods.Count > 0)
                            {
                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} **************************************************************************");
                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: The following mods failed the update, check above for more details.");
                                foreach (var failedMod in failedMods)
                                    progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} {failedMod}");
                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} **************************************************************************\r\n");
                            }
                        }
                        else
                        {
                            success = false;
                            // no steam information downloaded, display an error
                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ********************************************************************");
                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Mods cannot be updated, unable to download steam information.");
                            progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ********************************************************************\r\n");

                            if (!Config.Default.ServerUpdate_ForceUpdateModsIfNoSteamInfo)
                                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} If the mod update keeps failing try enabling the '{_globalizer.GetResourceString("GlobalSettings_ForceUpdateModsIfNoSteamInfoLabel")}' option in the settings window.\r\n");
                        }
                    }
                }
                else
                {
                    if (updateServer && updateMods)
                    {
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***********************************************************");
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ERROR: Mods were not processed as server update had errors.");
                        progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} ***********************************************************\r\n");
                    }
                }

                progressCallback?.Invoke(0, $"{SteamCmdUpdater.OUTPUT_PREFIX} Finished upgrade process.");
                return success;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            finally
            {
                this.lastModStatusQuery = DateTime.MinValue;
                UpdateServerStatus(ServerStatus.Stopped, Steam, false);
            }
        }

        public void ResetModCheckTimer()
        {
            this.lastModStatusQuery = DateTime.MinValue;
        }

        private void UpdateServerStatus(ServerStatus serverStatus, SteamStatus steamStatus, bool sendAlert)
        {
            this.Status = serverStatus;
            this.Steam = steamStatus;

            UpdateServerStatusString();

            if (!string.IsNullOrWhiteSpace(Config.Default.Alert_ServerStatusChange) && sendAlert)
                PluginHelper.Instance.ProcessAlert(AlertType.ServerStatusChange, this.ProfileSnapshot.ProfileName, $"{Config.Default.Alert_ServerStatusChange} {Status}");
        }

        public void UpdateServerStatusString()
        {
            switch (Status)
            {
                case ServerStatus.Initializing:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusInitializingLabel");
                    break;
                case ServerStatus.Running:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusRunningLabel");
                    break;
                case ServerStatus.Stopped:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusStoppedLabel");
                    break;
                case ServerStatus.Stopping:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusStoppingLabel");
                    break;
                case ServerStatus.Uninstalled:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusUninstalledLabel");
                    break;
                case ServerStatus.Unknown:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusUnknownLabel");
                    break;
                case ServerStatus.Updating:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusUpdatingLabel");
                    break;
                default:
                    StatusString = _globalizer.GetResourceString("ServerSettings_RuntimeStatusUnknownLabel");
                    break;
            }
        }
    }
}

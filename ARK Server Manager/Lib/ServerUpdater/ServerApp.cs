using QueryMaster;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Reflection;
using WPFSharp.Globalizer;
using ARK_Server_Manager.Lib.Utils;
using System.Net.Mail;

namespace ARK_Server_Manager.Lib
{
    internal class ServerApp
    {
        private readonly GlobalizedApplication _globalizer = GlobalizedApplication.Instance;

        internal class ProfileSnapshot
        {
            public string ProfileName;
            public string InstallDirectory;
            public string AltSaveDirectoryName;
            public bool PGM_Enabled;
            public string PGM_Name;
            public string AdminPassword;
            public string ServerIP;
            public int ServerPort;
            public bool RCONEnabled;
            public int RCONPort;
            public string ServerMap;
            public string ServerMapModId;
            public string TotalConversionModId;
            public List<string> ServerModIds;
            public string LastInstalledVersion;
            public int MotDDuration;

            public string SchedulerKey;
            public bool EnableAutoBackup;
            public bool EnableAutoUpdate;
            public bool EnableAutoShutdown1;
            public bool RestartAfterShutdown1;
            public bool EnableAutoShutdown2;
            public bool RestartAfterShutdown2;
            public bool AutoRestartIfShutdown;

            public bool SotFEnabled;

            public bool ServerUpdated;

            public static ProfileSnapshot Create(ServerProfile profile)
            {
                return new ProfileSnapshot
                {
                    ProfileName = profile.ProfileName,
                    InstallDirectory = profile.InstallDirectory,
                    AltSaveDirectoryName = profile.AltSaveDirectoryName,
                    PGM_Enabled = profile.PGM_Enabled,
                    PGM_Name = profile.PGM_Name,
                    AdminPassword = profile.AdminPassword,
                    ServerIP = string.IsNullOrWhiteSpace(profile.ServerIP) ? IPAddress.Loopback.ToString() : profile.ServerIP.Trim(),
                    ServerPort = profile.ServerPort,
                    RCONEnabled = profile.RCONEnabled,
                    RCONPort = profile.RCONPort,
                    ServerMap = ServerProfile.GetProfileMapName(profile),
                    ServerMapModId = ServerProfile.GetProfileMapModId(profile),
                    TotalConversionModId = profile.TotalConversionModId ?? string.Empty,
                    ServerModIds = ModUtils.GetModIdList(profile.ServerModIds),
                    LastInstalledVersion = profile.LastInstalledVersion ?? new Version(0, 0).ToString(),
                    MotDDuration = Math.Max(profile.MOTDDuration, 10),

                    SchedulerKey = profile.GetProfileKey(),
                    EnableAutoBackup = profile.EnableAutoBackup,
                    EnableAutoUpdate = profile.EnableAutoUpdate,
                    EnableAutoShutdown1 = profile.EnableAutoShutdown1,
                    RestartAfterShutdown1 = profile.RestartAfterShutdown1,
                    EnableAutoShutdown2 = profile.EnableAutoShutdown2,
                    RestartAfterShutdown2 = profile.RestartAfterShutdown2,
                    AutoRestartIfShutdown = profile.AutoRestartIfShutdown,

                    SotFEnabled = profile.SOTF_Enabled,

                    ServerUpdated = false,
                };
            }
        }

        public enum ServerProcessType
        {
            Unknown = 0,
            AutoBackup,
            AutoUpdate,
            AutoShutdown1,
            AutoShutdown2,
            Backup,
            Shutdown,
            Restart,
        }

        public const int MUTEX_TIMEOUT = 5;         // 5 minutes
        public const int MUTEX_ATTEMPTDELAY = 5000; // 5 seconds

        private const int STEAM_MAXRETRIES = 10;
        private const int RCON_MAXRETRIES = 3;

        public const int EXITCODE_NORMALEXIT = 0;
        private const int EXITCODE_EXITWITHERRORS = 98;
        public const int EXITCODE_CANCELLED = 99;
        // generic codes
        private const int EXITCODE_UNKNOWNERROR = 991;
        private const int EXITCODE_UNKNOWNTHREADERROR = 992;
        private const int EXITCODE_BADPROFILE = 993;
        private const int EXITCODE_PROFILENOTFOUND = 994;
        private const int EXITCODE_BADARGUMENT = 995;

        private const int EXITCODE_AUTOUPDATENOTENABLED = 1001;
        private const int EXITCODE_AUTOSHUTDOWNNOTENABLED = 1002;
        private const int EXITCODE_AUTOBACKUPNOTENABLED = 1003;

        private const int EXITCODE_PROCESSALREADYRUNNING = 1011;
        private const int EXITCODE_INVALIDDATADIRECTORY = 1012;
        private const int EXITCODE_INVALIDCACHEDIRECTORY = 1013;
        private const int EXITCODE_CACHENOTFOUND = 1005;
        private const int EXITCODE_STEAMCMDNOTFOUND = 1006;
        // update cache codes
        private const int EXITCODE_CACHESERVERUPDATEFAILED = 2001;

        private const int EXITCODE_CACHEMODUPDATEFAILED = 2101;
        private const int EXITCODE_CACHEMODDETAILSDOWNLOADFAILED = 2102;
        // update file codes
        private const int EXITCODE_SERVERUPDATEFAILED = 3001;
        private const int EXITCODE_MODUPDATEFAILED = 3002;
        // shutdown codes
        private const int EXITCODE_SHUTDOWN_GETCMDLINEFAILED = 4001;
        private const int EXITCODE_SHUTDOWN_TIMEOUT = 4002;
        private const int EXITCODE_SHUTDOWN_BADENDPOINT = 4003;
        private const int EXITCODE_SHUTDOWN_SERVERNOTFOUND = 4004;
        // restart code
        private const int EXITCODE_RESTART_FAILED = 5001;
        private const int EXITCODE_RESTART_BADLAUNCHER = 5002;

        public const string LOGPREFIX_AUTOBACKUP = "#AutoBackupLogs";
        public const string LOGPREFIX_AUTOSHUTDOWN = "#AutoShutdownLogs";
        public const string LOGPREFIX_AUTOUPDATE = "#AutoUpdateLogs";

        private static readonly object LockObject = new object();
        private static DateTime _startTime = DateTime.Now;
        private static string _logPrefix = "";
        private static Dictionary<ProfileSnapshot, ServerProfile> _profiles = null;

        private ProfileSnapshot _profile = null;
        private Rcon _rconConsole = null;
        private bool _serverRunning = false;

        public bool BackupWorldFile = true;
        public bool DeleteOldBackupWorldFiles = false;
        public int ExitCode = EXITCODE_NORMALEXIT;
        public bool OutputLogs = true;
        public bool SendEmails = false;
        public string ShutdownReason = null;
        public string UpdateReason = null;
        public ServerProcessType ServerProcess = ServerProcessType.Unknown;
        public int ShutdownInterval = Config.Default.ServerShutdown_GracePeriod;
        public ProgressDelegate ProgressCallback = null;

        private void BackupServer()
        {
            if (_profile == null || _profile.SotFEnabled)
            {
                ExitCode = EXITCODE_BADPROFILE;
                return;
            }

            var emailMessage = new StringBuilder();

            LogProfileMessage("------------------------");
            LogProfileMessage("Started server backup...");
            LogProfileMessage("------------------------");
            LogProfileMessage($"ASM version: {App.Version}");

            emailMessage.AppendLine("ASM Backup Summary:");
            emailMessage.AppendLine();
            emailMessage.AppendLine($"ASM version: {App.Version}");

            emailMessage.AppendLine();
            emailMessage.AppendLine("Backup:");

            // Find the server process.
            Process process = GetServerProcess();
            if (process != null)
            {
                _serverRunning = true;
                LogProfileMessage($"Server process found PID {process.Id}.");
            }

            if (_serverRunning)
            {
                // check if RCON is enabled
                if (_profile.RCONEnabled)
                {
                    QueryMaster.Server gameServer = null;

                    try
                    {
                        // create a connection to the server
                        var endPoint = new IPEndPoint(IPAddress.Parse(_profile.ServerIP), _profile.ServerPort);
                        gameServer = ServerQuery.GetServerInstance(EngineType.Source, endPoint);

                        try
                        {
                            // perform a world save
                            if (!string.IsNullOrWhiteSpace(Config.Default.ServerBackup_WorldSaveMessage))
                            {
                                SendMessage(Config.Default.ServerBackup_WorldSaveMessage);
                                emailMessage.AppendLine("sent worldsave message.");

                                Task.Delay(2000).Wait();
                            }

                            SendCommand("saveworld", false);
                            emailMessage.AppendLine("sent saveworld command.");

                            Task.Delay(10000).Wait();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"RCON> saveworld command.\r\n{ex.Message}");
                        }
                    }
                    finally
                    {
                        if (gameServer != null)
                        {
                            gameServer.Dispose();
                            gameServer = null;
                        }

                        CloseRconConsole();
                    }
                }
                else
                {
                    LogProfileMessage("RCON not enabled.");
                }
            }

            if (ExitCode != EXITCODE_NORMALEXIT)
                return;

            // make a backup of the current world file.
            var worldFile = GetServerWorldFile();
            if (File.Exists(worldFile))
            {
                emailMessage.AppendLine();

                try
                {
                    LogProfileMessage("Backing up world file...");

                    var backupFile = GetServerWorldBackupFile();
                    File.Copy(worldFile, backupFile, true);

                    File.SetCreationTime(backupFile, _startTime);
                    File.SetLastWriteTime(backupFile, _startTime);
                    File.SetLastAccessTime(backupFile, _startTime);

                    LogProfileMessage($"Backed up world file '{worldFile}'.");

                    emailMessage.AppendLine("Backed up world file.");
                    emailMessage.AppendLine(worldFile);
                }
                catch (Exception ex)
                {
                    LogProfileError($"Unable to back up world file - {worldFile}.\r\n{ex.Message}", false);

                    emailMessage.AppendLine("Unable to back up world file.");
                    emailMessage.AppendLine(worldFile);
                    emailMessage.AppendLine(ex.Message);
                }
            }
            else
            {
                LogProfileMessage($"Unable to back up world file - '{worldFile}'\r\nFile could not be found.");

                emailMessage.AppendLine("Unable to back up world file.");
                emailMessage.AppendLine(worldFile);
                emailMessage.AppendLine("File could not be found.");
            }

            if (ExitCode != EXITCODE_NORMALEXIT)
                return;

            // delete the old backup files
            if (DeleteOldBackupWorldFiles)
            {
                var profileSaveFolder = ServerProfile.GetProfileSavePath(_profile.InstallDirectory, _profile.AltSaveDirectoryName, _profile.PGM_Enabled, _profile.PGM_Name);
                var mapName = ServerProfile.GetProfileMapFileName(_profile.ServerMap, _profile.PGM_Enabled, _profile.PGM_Name);
                var backupFileFilter = $"{mapName}_ASMBackup_*{Config.Default.BackupExtension}";
                var backupDateFilter = DateTime.Now.AddDays(-Config.Default.AutoBackup_DeleteInterval);

                var backupFiles = new DirectoryInfo(profileSaveFolder).GetFiles(backupFileFilter).Where(f => f.LastWriteTime < backupDateFilter);
                foreach (var backupFile in backupFiles)
                {
                    try
                    {
                        LogProfileMessage($"{backupFile.Name} was deleted, last updated {backupFile.CreationTime.ToString()}.");
                        backupFile.Delete();
                    }
                    catch
                    {
                        // if unable to delete, do not bother
                    }
                }
            }

            if (Config.Default.EmailNotify_AutoBackup)
            {
                emailMessage.AppendLine();
                emailMessage.AppendLine("See attached log file more details.");
                SendEmail($"{_profile.ProfileName} auto backup finished", emailMessage.ToString(), true);
            }

            LogProfileMessage("-----------------------");
            LogProfileMessage("Finished server backup.");
            LogProfileMessage("-----------------------");

            ExitCode = EXITCODE_NORMALEXIT;
        }

        private void ShutdownServer(bool restartServer, CancellationToken cancellationToken)
        {
            if (_profile == null)
            {
                ExitCode = EXITCODE_BADPROFILE;
                return;
            }

            if (restartServer)
            {
                LogProfileMessage("-------------------------");
                LogProfileMessage("Started server restart...");
                LogProfileMessage("-------------------------");
            }
            else
            {
                LogProfileMessage("--------------------------");
                LogProfileMessage("Started server shutdown...");
                LogProfileMessage("--------------------------");
            }
            LogProfileMessage($"ASM version: {App.Version}");

            // stop the server
            StopServer(cancellationToken);

            if (ExitCode != EXITCODE_NORMALEXIT)
                return;
            if (cancellationToken.IsCancellationRequested)
            {
                ExitCode = EXITCODE_CANCELLED;
                return;
            }

            if (BackupWorldFile)
            {
                // make a backup of the current world file.
                var worldFile = GetServerWorldFile();
                if (File.Exists(worldFile))
                {
                    try
                    {
                        LogProfileMessage("Backing up world save file...");

                        var backupFile = GetServerWorldBackupFile();
                        File.Copy(worldFile, backupFile, true);

                        LogProfileMessage($"Backed up world save file '{worldFile}'.");
                    }
                    catch (Exception ex)
                    {
                        LogProfileError($"Unable to back up world save file - {worldFile}.\r\n{ex.Message}", false);
                    }
                }
                else
                {
                    LogProfileMessage($"Unable to back up world save file - '{worldFile}'\r\nWorld save file could not be found.");
                }

                if (ExitCode != EXITCODE_NORMALEXIT)
                    return;
            }

            // make a backup of the current profile and config files.
            try
            {
                LogProfileMessage("Backing up profile and config files...");

                var profileFile = Updater.NormalizePath(Path.Combine(Config.Default.ConfigDirectory, $"{_profile.ProfileName}{Config.Default.ProfileExtension}"));
                var gameIniFile = Updater.NormalizePath(Path.Combine(_profile.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameConfigFile));
                var gusIniFile = Updater.NormalizePath(Path.Combine(_profile.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameUserSettingsConfigFile));

                var profileBackupFolder = Updater.NormalizePath(Path.Combine(Config.Default.ConfigDirectory, Config.Default.BackupDir, _profile.ProfileName, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
                var profileBackupFile = Updater.NormalizePath(Path.Combine(profileBackupFolder, $"{_profile.ProfileName}{Config.Default.ProfileExtension}"));
                var gameIniBackupFile = Updater.NormalizePath(Path.Combine(profileBackupFolder, Config.Default.ServerGameConfigFile));
                var gusIniBackupFile = Updater.NormalizePath(Path.Combine(profileBackupFolder, Config.Default.ServerGameUserSettingsConfigFile));

                if (!Directory.Exists(profileBackupFolder))
                    Directory.CreateDirectory(profileBackupFolder);

                if (File.Exists(profileFile))
                    File.Copy(profileFile, profileBackupFile, true);

                if (File.Exists(gameIniFile))
                    File.Copy(gameIniFile, gameIniBackupFile, true);

                if (File.Exists(gusIniFile))
                    File.Copy(gusIniFile, gusIniBackupFile, true);

                LogProfileMessage("Backed up profile and config files.");
            }
            catch (Exception ex)
            {
                LogProfileError($"Unable to back up profile and config files.\r\n{ex.Message}", false);
            }

            if (ExitCode != EXITCODE_NORMALEXIT)
                return;

            // check if this is a shutdown only, or a shutdown and restart.
            if (restartServer)
            {
                StartServer();

                if (ExitCode != EXITCODE_NORMALEXIT)
                    return;

                LogProfileMessage("------------------------");
                LogProfileMessage("Finished server restart.");
                LogProfileMessage("------------------------");
            }
            else
            {
                LogProfileMessage("-------------------------");
                LogProfileMessage("Finished server shutdown.");
                LogProfileMessage("-------------------------");
            }

            ExitCode = EXITCODE_NORMALEXIT;
        }

        private void StartServer()
        {
            if (_profile == null)
            {
                ExitCode = EXITCODE_BADPROFILE;
                return;
            }

            // check if the server was previously running before the update.
            if (!_serverRunning && !_profile.AutoRestartIfShutdown)
            {
                LogProfileMessage("Server was not running, server will not be started.");

                ExitCode = EXITCODE_NORMALEXIT;
                return;
            }
            if (!_serverRunning && _profile.AutoRestartIfShutdown)
            {
                LogProfileMessage("Server was not running, server will be started as the setting to restart if shutdown is TRUE.");
            }

            // Find the server process.
            Process process = GetServerProcess();

            if (process == null)
            {
                LogProfileMessage("");
                LogProfileMessage("Starting server...");

                var startInfo = new ProcessStartInfo()
                {
                    FileName = GetLauncherFile(),
                    UseShellExecute = true,
                };

                process = Process.Start(startInfo);
                if (process == null)
                {
                    LogProfileError("Starting server failed.");
                    ExitCode = EXITCODE_RESTART_FAILED;
                    return;
                }

                LogProfileMessage("Started server successfully.");
                LogProfileMessage("");

                if (Config.Default.EmailNotify_ShutdownRestart)
                    SendEmail($"{_profile.ProfileName} server started", $"The server has been started.", false);
            }
            ExitCode = EXITCODE_NORMALEXIT;
        }

        private void StopServer(CancellationToken cancellationToken)
        {
            _serverRunning = false;

            if (_profile == null)
            {
                ExitCode = EXITCODE_BADPROFILE;
                return;
            }

            // Find the server process.
            Process process = GetServerProcess();

            // check if the process was found
            if (process == null)
            {
                LogProfileMessage("Server process not found, server not started.");

                // process not found, server is not running
                ExitCode = EXITCODE_NORMALEXIT;
                return;
            }

            _serverRunning = true;
            LogProfileMessage($"Server process found PID {process.Id}.");

            // check if RCON is enabled
            if (_profile.RCONEnabled)
            {
                QueryMaster.Server gameServer = null;

                try
                {
                    // create a connection to the server
                    var endPoint = new IPEndPoint(IPAddress.Parse(_profile.ServerIP), _profile.ServerPort);
                    gameServer = ServerQuery.GetServerInstance(EngineType.Source, endPoint);

                    // check if there is a shutdown reason
                    if (!string.IsNullOrWhiteSpace(ShutdownReason))
                    {
                        LogProfileMessage("Sending shutdown reason...");

                        SendMessage(ShutdownReason);

                        Task.Delay(_profile.MotDDuration * 1000, cancellationToken).Wait(cancellationToken);
                    }

                    LogProfileMessage("Starting shutdown timer...");

                    var minutesLeft = ShutdownInterval;
                    while (minutesLeft > 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            LogProfileMessage("Cancelling shutdown...");

                            if (!string.IsNullOrWhiteSpace(Config.Default.ServerShutdown_CancelMessage))
                                SendMessage(Config.Default.ServerShutdown_CancelMessage);

                            ExitCode = EXITCODE_CANCELLED;
                            return;
                        }

                        try
                        {
                            var playerInfo = gameServer?.GetPlayers()?.Where(p => !string.IsNullOrWhiteSpace(p.Name?.Trim())).ToList();

                            // check if anyone is logged into the server
                            var playerCount = playerInfo?.Count ?? -1;
                            if (playerCount <= 0)
                            {
                                LogProfileMessage("No online players, shutdown timer cancelled.");
                                break;
                            }

                            LogProfileMessage($"Online players: {playerCount}.");
                            if (playerInfo != null)
                            {
                                foreach (var player in playerInfo)
                                {
                                    LogProfileMessage($"{player.Name}; joined {player.Time} ago");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error getting/displaying online players.\r\n{ex.Message}");
                        }

                        var message = string.Empty;
                        if (minutesLeft >= 5)
                        {
                            // check if the we have just started the countdown
                            if (minutesLeft == ShutdownInterval)
                            {
                                message = Config.Default.ServerShutdown_GraceMessage1.Replace("{minutes}", minutesLeft.ToString());
                                if (!string.IsNullOrWhiteSpace(UpdateReason))
                                    message += $"\n\n{UpdateReason}";
                            }
                            else
                            {
                                int remainder;
                                Math.DivRem(minutesLeft, 5, out remainder);

                                if (remainder == 0)
                                {
                                    message = Config.Default.ServerShutdown_GraceMessage1.Replace("{minutes}", minutesLeft.ToString());
                                    if (!string.IsNullOrWhiteSpace(UpdateReason))
                                        message += $"\n\n{UpdateReason}";
                                }
                            }
                        }
                        else if (minutesLeft > 1)
                        {
                            message = Config.Default.ServerShutdown_GraceMessage1.Replace("{minutes}", minutesLeft.ToString());
                            if (!string.IsNullOrWhiteSpace(UpdateReason))
                                message += $"\n\n{UpdateReason}";
                        }
                        else
                        {
                            message = Config.Default.ServerShutdown_GraceMessage2;
                            if (!string.IsNullOrWhiteSpace(UpdateReason))
                                message += $"\n\n{UpdateReason}";
                        }

                        if (!string.IsNullOrWhiteSpace(message))
                            SendMessage(message);

                        minutesLeft--;
                        Task.Delay(60000, cancellationToken).Wait(cancellationToken);
                    }

                    // check if we need to perform a world save (not required for SotF servers)
                    if (Config.Default.ServerShutdown_EnableWorldSave && !_profile.SotFEnabled)
                    {
                        try
                        {
                            // perform a world save
                            if (!string.IsNullOrWhiteSpace(Config.Default.ServerShutdown_WorldSaveMessage))
                            {
                                SendMessage(Config.Default.ServerShutdown_WorldSaveMessage);

                                Task.Delay(2000).Wait();
                            }

                            SendCommand("saveworld", false);

                            Task.Delay(10000, cancellationToken).Wait(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"RCON> saveworld command.\r\n{ex.Message}");
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        LogProfileMessage("Cancelling shutdown...");

                        if (!string.IsNullOrWhiteSpace(Config.Default.ServerShutdown_CancelMessage))
                            SendMessage(Config.Default.ServerShutdown_CancelMessage);

                        ExitCode = EXITCODE_CANCELLED;
                        return;
                    }

                    // send the final shutdown message
                    if (!string.IsNullOrWhiteSpace(Config.Default.ServerShutdown_GraceMessage3))
                        SendMessage(Config.Default.ServerShutdown_GraceMessage3);
                }
                finally
                {
                    if (gameServer != null)
                    {
                        gameServer.Dispose();
                        gameServer = null;
                    }

                    CloseRconConsole();
                }
            }
            else
            {
                LogProfileMessage("RCON not enabled.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                LogProfileMessage("Cancelling shutdown...");

                ExitCode = EXITCODE_CANCELLED;
                return;
            }

            try
            {
                // Stop the server
                LogProfileMessage("");
                LogProfileMessage("Stopping server...");

                TaskCompletionSource<bool> ts = new TaskCompletionSource<bool>();
                EventHandler handler = (s, e) => ts.TrySetResult(true);
                process.EnableRaisingEvents = true;
                process.Exited += handler;

                // Method 1 - RCON Command
                if (_profile.RCONEnabled)
                {
                    try
                    {
                        SendCommand("doexit", false);

                        Task.Delay(10000).Wait();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"RCON> doexit command.\r\n{ex.Message}");
                    }

                    if (!process.HasExited)
                    {
                        ts.Task.Wait(60000);   // 1 minute
                    }

                    if (process.HasExited)
                    {
                        LogProfileMessage($"Exited server successfully.");
                        LogProfileMessage("");
                        ExitCode = EXITCODE_NORMALEXIT;
                        return;
                    }

                    LogProfileMessage("Exiting server timed out, attempting to close the server.");
                }

                // Method 2 - Close the process
                process.CloseMainWindow();

                if (!process.HasExited)
                {
                    ts.Task.Wait(60000);   // 1 minute
                }

                if (process.HasExited)
                {
                    LogProfileMessage("Closed server successfully.");
                    LogProfileMessage("");
                    ExitCode = EXITCODE_NORMALEXIT;
                    return;
                }

                // Attempt 3 - Send CNTL-C
                LogProfileMessage("Closing server timed out, attempting to stop the server.");

                ProcessUtils.SendStop(process).Wait();

                if (!process.HasExited)
                {
                    ts.Task.Wait(60000);   // 1 minute
                }

                if (ts.Task.Result)
                {
                    LogProfileMessage("Stopped server successfully.");
                    LogProfileMessage("");
                    ExitCode = EXITCODE_NORMALEXIT;
                    return;
                }

                // Attempt 4 - Kill the process
                LogProfileMessage("Stopping server timed out, attempting to kill the server.");

                // try to kill the server
                process.Kill();

                if (!process.HasExited)
                {
                    ts.Task.Wait(60000);   // 1 minute
                }

                if (ts.Task.Result)
                {
                    LogProfileMessage("Killed server successfully.");
                    LogProfileMessage("");
                    ExitCode = EXITCODE_NORMALEXIT;
                    return;
                }
            }
            finally
            {
                if (process.HasExited)
                {
                    if (Config.Default.EmailNotify_ShutdownRestart)
                        SendEmail($"{_profile.ProfileName} server shutdown", $"The server has been shutdown to perform the {ServerProcess.ToString()} process.", false);
                }
            }

            // killing the server did not work, cancel the update
            LogProfileError("Killing server timed out.");
            ExitCode = EXITCODE_SHUTDOWN_TIMEOUT;
        }

        private void UpdateFiles()
        {
            if (_profile == null)
            {
                ExitCode = EXITCODE_BADPROFILE;
                return;
            }

            var emailMessage = new StringBuilder();

            LogProfileMessage("------------------------");
            LogProfileMessage("Started server update...");
            LogProfileMessage("------------------------");
            LogProfileMessage($"ASM version: {App.Version}");

            // check if the server needs to be updated
            var serverCacheLastUpdated = GetServerLatestTime(GetServerCacheTimeFile());
            var serverLastUpdated = GetServerLatestTime(GetServerTimeFile());
            var updateServer = serverCacheLastUpdated > serverLastUpdated;

            // check if any of the mods need to be updated
            var updateModIds = new List<string>();
            var modIdList = GetModList();

            // cycle through each mod.
            foreach (var modId in modIdList)
            {
                // check if the mod needs to be updated.
                var modCacheLastUpdated = ModUtils.GetModLatestTime(ModUtils.GetLatestModCacheTimeFile(modId, false));
                var modLastUpdated = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(_profile.InstallDirectory, modId));
                if (modCacheLastUpdated > modLastUpdated || modLastUpdated == 0)
                    updateModIds.Add(modId);
            }

            if (ExitCode != EXITCODE_NORMALEXIT)
                return;

            if (updateServer || updateModIds.Count > 0)
            {
                var modDetails = SteamUtils.GetSteamModDetails(updateModIds);

                UpdateReason = string.Empty;
                if (Config.Default.AutoUpdate_ShowUpdateReason)
                {
                    // create the update message to broadcast 
                    var delimiter = string.Empty;
                    if (updateServer)
                    {
                        UpdateReason += $"{delimiter}Ark Server";
                        delimiter = ", ";
                    }
                    if (updateModIds.Count > 0)
                    {
                        for (var index = 0; index < updateModIds.Count; index++)
                        {
                            if (index == 5)
                            {
                                if (updateModIds.Count - index == 1)
                                    UpdateReason += $" and 1 other mod...";
                                else
                                    UpdateReason += $" and {updateModIds.Count - index} other mods...";
                                break;
                            }

                            var modId = updateModIds[index];
                            var modName = modDetails?.publishedfiledetails?.FirstOrDefault(m => m.publishedfileid == modId)?.title ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(modName))
                                UpdateReason += $"{delimiter}{modId}";
                            else
                                UpdateReason += $"{delimiter}{modName}";
                            delimiter = ", ";
                        }
                    }
                }

                // stop the server
                StopServer(CancellationToken.None);

                if (ExitCode != EXITCODE_NORMALEXIT)
                    return;

                emailMessage.AppendLine("ASM Update Summary:");
                emailMessage.AppendLine();
                emailMessage.AppendLine($"ASM version: {App.Version}");

                if (BackupWorldFile)
                {
                    emailMessage.AppendLine();
                    emailMessage.AppendLine("Backup:");

                    // make a backup of the current world file.
                    var worldFile = GetServerWorldFile();
                    if (File.Exists(worldFile))
                    {
                        emailMessage.AppendLine();

                        try
                        {
                            LogProfileMessage("Backing up world file...");

                            var backupFile = GetServerWorldBackupFile();
                            File.Copy(worldFile, backupFile, true);

                            LogProfileMessage($"Backed up world file '{worldFile}'.");

                            emailMessage.AppendLine("Backed up world file.");
                            emailMessage.AppendLine(worldFile);
                        }
                        catch (Exception ex)
                        {
                            LogProfileError($"Unable to back up world file - {worldFile}.\r\n{ex.Message}", false);

                            emailMessage.AppendLine("Unable to back up world file.");
                            emailMessage.AppendLine(worldFile);
                            emailMessage.AppendLine(ex.Message);
                        }
                    }
                    else
                    {
                        LogProfileMessage($"Unable to back up world file - '{worldFile}'\r\nFile could not be found.");

                        emailMessage.AppendLine("Unable to back up world file.");
                        emailMessage.AppendLine(worldFile);
                        emailMessage.AppendLine("File could not be found.");
                    }

                    if (ExitCode != EXITCODE_NORMALEXIT)
                        return;
                }

                Mutex mutex = null;
                bool createdNew = false;

                // check if the server needs to be updated
                if (updateServer)
                {
                    LogProfileMessage("Updating server from cache...");

                    emailMessage.AppendLine();
                    emailMessage.AppendLine("ARK Server Update:");

                    try
                    {
                        if (Directory.Exists(Config.Default.AutoUpdate_CacheDir))
                        {
                            LogProfileMessage($"Smart cache copy: {Config.Default.AutoUpdate_UseSmartCopy}.");

                            // update the server files from the cache.
                            DirectoryCopy(Config.Default.AutoUpdate_CacheDir, _profile.InstallDirectory, true, Config.Default.AutoUpdate_UseSmartCopy, null);

                            emailMessage.AppendLine();

                            LogProfileMessage("Updated server from cache. See ARK patch notes:");
                            LogProfileMessage(Config.Default.ArkSE_PatchNotesUrl);

                            emailMessage.AppendLine("Updated server from cache. See ARK patch notes:");
                            emailMessage.AppendLine(Config.Default.ArkSE_PatchNotesUrl);

                            _profile.ServerUpdated = true;
                        }
                        else
                        {
                            LogProfileMessage("Server cache was not found, server was not updated from cache.");
                            ExitCode = EXITCODE_SERVERUPDATEFAILED;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogProfileError($"Unable to update the server from cache.\r\n{ex.Message}");
                        ExitCode = EXITCODE_SERVERUPDATEFAILED;
                    }
                }
                else
                {
                    LogProfileMessage("Server is already up to date, no update required.");
                }

                if (ExitCode != EXITCODE_NORMALEXIT)
                    return;

                // check if the mods need to be updated
                if (updateModIds.Count > 0)
                {
                    LogProfileMessage($"Updating {updateModIds.Count} mods from cache...");

                    emailMessage.AppendLine();
                    emailMessage.AppendLine("Mods:");

                    try
                    {
                        // update the mod files from the cache.
                        for (var index = 0; index < updateModIds.Count; index++)
                        {
                            var modId = updateModIds[index];
                            var modCachePath = ModUtils.GetModCachePath(modId, false);
                            var modPath = GetModPath(modId);
                            var modName = modDetails?.publishedfiledetails?.FirstOrDefault(m => m.publishedfileid == modId)?.title ?? string.Empty;

                            try
                            {
                                if (Directory.Exists(modCachePath))
                                {
                                    // try to establish a mutex for the mod cache.
                                    mutex = new Mutex(true, GetMutexName(modCachePath), out createdNew);
                                    if (!createdNew)
                                        createdNew = mutex.WaitOne(new TimeSpan(0, MUTEX_TIMEOUT, 0));

                                    // check if the mutex was established
                                    if (createdNew)
                                    {
                                        LogProfileMessage($"Started mod update from cache {index + 1} of {updateModIds.Count}...");
                                        LogProfileMessage($"Mod Name: {modName} (Mod ID: {modId})");

                                        emailMessage.AppendLine();
                                        emailMessage.AppendLine($"Mod Name: {modName} (Mod ID: {modId})");

                                        ModUtils.CopyMod(modCachePath, modPath, modId, null);

                                        var modLastUpdated = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(_profile.InstallDirectory, modId));
                                        LogProfileMessage($"Mod {modId} version: {modLastUpdated}.");

                                        LogProfileMessage($"Workshop page: http://steamcommunity.com/sharedfiles/filedetails/?id={modId}");
                                        LogProfileMessage($"Change notes: http://steamcommunity.com/sharedfiles/filedetails/changelog/{modId}");

                                        emailMessage.AppendLine($"Workshop page: http://steamcommunity.com/sharedfiles/filedetails/?id={modId}");
                                        emailMessage.AppendLine($"Change notes: http://steamcommunity.com/sharedfiles/filedetails/changelog/{modId}");

                                        LogProfileMessage($"Finished mod {modId} update from cache.");
                                    }
                                    else
                                    {
                                        ExitCode = EXITCODE_PROCESSALREADYRUNNING;
                                        LogProfileMessage("Mod not updated, could not lock mod cache.");
                                    }
                                }
                                else
                                {
                                    LogProfileError($"Mod {modId} cache was not found, mod was not updated from cache.");
                                    ExitCode = EXITCODE_MODUPDATEFAILED;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogProfileError($"Unable to update mod {modId} from cache.\r\n{ex.Message}");
                                ExitCode = EXITCODE_MODUPDATEFAILED;
                            }
                            finally
                            {
                                if (mutex != null)
                                {
                                    if (createdNew)
                                    {
                                        mutex.ReleaseMutex();
                                        mutex.Dispose();
                                    }
                                    mutex = null;
                                }
                            }
                        }

                        if (ExitCode == EXITCODE_NORMALEXIT)
                            LogProfileMessage($"Updated {updateModIds.Count} mods from cache.");
                        else
                            LogProfileMessage($"Updated {updateModIds.Count} mods from cache BUT there were errors.");
                    }
                    catch (Exception ex)
                    {
                        LogProfileError($"Unable to update the mods from cache.\r\n{ex.Message}");
                        ExitCode = EXITCODE_MODUPDATEFAILED;
                    }
                }
                else
                {
                    LogProfileMessage("Mods are already up to date, no updates required.");
                }

                if (ExitCode != EXITCODE_NORMALEXIT)
                    return;

                // restart the server
                StartServer();

                if (Config.Default.EmailNotify_AutoUpdate)
                {
                    emailMessage.AppendLine();
                    emailMessage.AppendLine("See attached log file more details.");
                    SendEmail($"{_profile.ProfileName} auto update finished", emailMessage.ToString(), true);
                }
            }
            else
            {
                if (updateModIds.Count > 0)
                    LogProfileMessage("The server and mods files are already up to date, no updates required.");
                else
                    LogProfileMessage("The server files are already up to date, no updates required.");

                _serverRunning = GetServerProcess() != null;

                // restart the server
                StartServer();
            }

            if (ExitCode != EXITCODE_NORMALEXIT)
                return;

            LogProfileMessage("-----------------------");
            LogProfileMessage("Finished server update.");
            LogProfileMessage("-----------------------");

            ExitCode = EXITCODE_NORMALEXIT;
        }

        private void UpdateModCache()
        {
            // get a list of mods to be processed
            var modIdList = GetModList();

            // check if there are any mods to be processed
            if (modIdList.Count == 0)
            {
                ExitCode = EXITCODE_NORMALEXIT;
                return;
            }

            LogMessage("");
            LogMessage("----------------------------");
            LogMessage("Starting mod cache update...");
            LogMessage("----------------------------");
            LogMessage($"ASM version: {App.Version}");

            LogMessage($"Downloading mod information for {modIdList.Count} mods from steam.");

            // get the details of the mods to be processed.
            var modDetails = SteamUtils.GetSteamModDetails(modIdList);
            if (modDetails == null)
            {
                if (!Config.Default.ServerUpdate_ForceUpdateModsIfNoSteamInfo)
                {
                    LogError("Mods cannot be updated, unable to download steam information.");
                    LogMessage($"If the mod update keeps failing try enabling the '{_globalizer.GetResourceString("GlobalSettings_ForceUpdateModsIfNoSteamInfoLabel")}' option in the settings window.");
                    ExitCode = EXITCODE_CACHEMODDETAILSDOWNLOADFAILED;
                    return;
                }
            }

            LogMessage($"Downloaded mod information for {modIdList.Count} mods from steam.");
            LogMessage("");

            // cycle through each mod finding which needs to be updated.
            var updateModIds = new List<string>();
            if (modDetails == null)
            {
                if (Config.Default.ServerUpdate_ForceUpdateModsIfNoSteamInfo)
                {
                    LogMessage("All mods will be updated - unable to download steam information and force mod update is TRUE.");

                    updateModIds.AddRange(modIdList);
                    modDetails = new Model.PublishedFileDetailsResponse();
                }
            }
            else
            {
                if (Config.Default.ServerUpdate_ForceUpdateMods)
                {
                    LogMessage("All mods will be updated - force mod update is TRUE.");
                    updateModIds.AddRange(modIdList);
                }
                else
                {
                    LogMessage("Mods will be selectively updated - force mod update is FALSE.");

                    foreach (var modId in modIdList)
                    {
                        var modDetail = modDetails.publishedfiledetails?.FirstOrDefault(m => m.publishedfileid.Equals(modId, StringComparison.OrdinalIgnoreCase));
                        if (modDetail == null)
                        {
                            LogMessage($"Mod {modId} will not be updated - unable to download steam information.");
                            continue;
                        }

                        if (modDetail.time_updated == 0)
                        {
                            LogMessage($"Mod {modId} will be updated - mod is private.");
                            updateModIds.Add(modId);
                        }
                        else
                        {
                            var cacheTimeFile = ModUtils.GetLatestModCacheTimeFile(modId, false);

                            // check if the mod needs to be updated
                            var steamLastUpdated = modDetail.time_updated;
                            var modCacheLastUpdated = ModUtils.GetModLatestTime(cacheTimeFile);
                            if (steamLastUpdated > modCacheLastUpdated)
                            {
                                LogMessage($"Mod {modId} will be updated - new version found.");
                                updateModIds.Add(modId);
                            }
                            else if (modCacheLastUpdated == 0)
                            {
                                LogMessage($"Mod {modId} will be updated - cache not versioned.");
                                updateModIds.Add(modId);
                            }
                            else
                            {
                                LogMessage($"Mod {modId} update skipped - cache contains the latest version.");
                            }
                        }
                    }
                }
            }

            var steamCmdFile = Updater.GetSteamCmdFile();
            if (string.IsNullOrWhiteSpace(steamCmdFile) || !File.Exists(steamCmdFile))
            {
                LogError($"SteamCMD could not be found. Expected location is {steamCmdFile}");
                if (Config.Default.SteamCmdRedirectOutput)
                    LogMessage($"If the mod cache update keeps failing try disabling the '{_globalizer.GetResourceString("GlobalSettings_SteamCmdRedirectOutputLabel")}' option in the settings window.\r\n");

                ExitCode = EXITCODE_STEAMCMDNOTFOUND;
                return;
            }

            // cycle through each mod id.
            for (var index = 0; index < updateModIds.Count; index++)
            {
                var modId = updateModIds[index];
                var modDetail = modDetails.publishedfiledetails?.FirstOrDefault(m => m.publishedfileid.Equals(modId, StringComparison.OrdinalIgnoreCase));

                var cacheTimeFile = ModUtils.GetLatestModCacheTimeFile(modId, false);
                var modCachePath = ModUtils.GetModCachePath(modId, false);

                var downloadSuccessful = false;

                DataReceivedEventHandler modOutputHandler = (s, e) =>
                {
                    var dataValue = e.Data ?? string.Empty;
                    LogMessage(dataValue);
                    if (dataValue.StartsWith("Success."))
                    {
                        downloadSuccessful = true;
                    }
                };

                LogMessage("");
                LogMessage($"Started mod cache update {index + 1} of {updateModIds.Count}");
                LogMessage($"{modId} - {modDetail?.title ?? "<unknown>"}");

                var attempt = 0;
                while (true)
                {
                    attempt++;
                    downloadSuccessful = !Config.Default.SteamCmdRedirectOutput;

                    // update the mod cache
                    var steamCmdArgs = string.Empty;
                    if (Config.Default.SteamCmd_UseAnonymousCredentials)
                        steamCmdArgs = string.Format(Config.Default.SteamCmdInstallModArgsFormat, Config.Default.SteamCmd_AnonymousUsername, modId);
                    else
                        steamCmdArgs = string.Format(Config.Default.SteamCmdInstallModArgsFormat, Config.Default.SteamCmd_Username, modId);
                    var success = ServerUpdater.UpgradeModsAsync(steamCmdFile, steamCmdArgs, Config.Default.SteamCmdRedirectOutput ? modOutputHandler : null, CancellationToken.None, ProcessWindowStyle.Hidden).Result;
                    if (success && downloadSuccessful)
                        // download was successful, exit loop and continue.
                        break;

                    // download was not successful, log a failed attempt.
                    var logError = $"Mod {modId} cache update failed";
                    if (Config.Default.AutoUpdate_RetryOnFail)
                        logError += $" - attempt {attempt}.";
                    LogError(logError);

                    // check if we have reached the max failed attempt limit.
                    if (!Config.Default.AutoUpdate_RetryOnFail || attempt >= STEAM_MAXRETRIES)
                    {
                        // failed max limit reached
                        if (Config.Default.SteamCmdRedirectOutput)
                            LogMessage($"If the mod cache update keeps failing try disabling the '{_globalizer.GetResourceString("GlobalSettings_SteamCmdRedirectOutputLabel")}' option in the ASM settings window.");

                        ExitCode = EXITCODE_CACHEMODUPDATEFAILED;
                        return;
                    }

                    Task.Delay(5000).Wait();
                }

                // check if any of the mod files have changed.
                if (Directory.Exists(modCachePath))
                {
                    var gotNewVersion = new DirectoryInfo(modCachePath).GetFiles("*.*", SearchOption.AllDirectories).Any(file => file.LastWriteTime >= _startTime);

                    if (gotNewVersion)
                        LogMessage("***** New version downloaded. *****");
                    else
                        LogMessage("No new version.");

                    var steamLastUpdated = modDetail?.time_updated.ToString() ?? string.Empty;
                    if (modDetail == null || modDetail.time_updated <= 0)
                    {
                        // get the version number from the steamcmd workshop file.
                        steamLastUpdated = ModUtils.GetSteamWorkshopLatestTime(ModUtils.GetSteamWorkshopFile(false), modId).ToString();
                    }

                    File.WriteAllText(cacheTimeFile, steamLastUpdated);
                    LogMessage($"Mod {modId} cache version: {steamLastUpdated}");
                }
                else
                    LogMessage($"Mod {modId} cache does not exist.");

                LogMessage($"Finished mod {modId} cache update.");
            }

            LogMessage("---------------------------");
            LogMessage("Finished mod cache update.");
            LogMessage("---------------------------");
            LogMessage("");
            ExitCode = EXITCODE_NORMALEXIT;
        }

        private void UpdateServerCache()
        {
            LogMessage("-------------------------------");
            LogMessage("Starting server cache update...");
            LogMessage("-------------------------------");
            LogMessage($"ASM version: {App.Version}");

            var gotNewVersion = false;
            var downloadSuccessful = false;

            var steamCmdFile = Updater.GetSteamCmdFile();
            if (string.IsNullOrWhiteSpace(steamCmdFile) || !File.Exists(steamCmdFile))
            {
                LogError($"SteamCMD could not be found. Expected location is {steamCmdFile}");
                ExitCode = EXITCODE_STEAMCMDNOTFOUND;
                return;
            }

            DataReceivedEventHandler serverOutputHandler = (s, e) =>
            {
                var dataValue = e.Data ?? string.Empty;
                LogMessage(dataValue);
                if (!gotNewVersion && dataValue.Contains("downloading,"))
                {
                    gotNewVersion = true;
                }
                if (dataValue.StartsWith("Success!"))
                {
                    downloadSuccessful = true;
                }
            };

            LogMessage("Server update started.");

            var attempt = 0;
            while (true)
            {
                attempt++;
                downloadSuccessful = !Config.Default.SteamCmdRedirectOutput;
                gotNewVersion = false;

                // update the server cache
                var steamCmdArgs = String.Format(Config.Default.SteamCmdInstallServerArgsFormat, Config.Default.AutoUpdate_CacheDir, string.Empty);
                var success = ServerUpdater.UpgradeServerAsync(steamCmdFile, steamCmdArgs, Config.Default.AutoUpdate_CacheDir, Config.Default.SteamCmdRedirectOutput ? serverOutputHandler : null, CancellationToken.None, ProcessWindowStyle.Hidden).Result;
                if (success && downloadSuccessful)
                    // download was successful, exit loop and continue.
                    break;

                // download was not successful, log a failed attempt.
                var logError = "Server cache update failed";
                if (Config.Default.AutoUpdate_RetryOnFail)
                    logError += $" - attempt {attempt}.";
                LogError(logError);

                // check if we have reached the max failed attempt limit.
                if (!Config.Default.AutoUpdate_RetryOnFail || attempt >= STEAM_MAXRETRIES)
                {
                    // failed max limit reached
                    if (Config.Default.SteamCmdRedirectOutput)
                        LogMessage($"If the server cache update keeps failing try disabling the '{_globalizer.GetResourceString("GlobalSettings_SteamCmdRedirectOutputLabel")}' option in the ASM settings window.");

                    ExitCode = EXITCODE_CACHESERVERUPDATEFAILED;
                    return;
                }

                Task.Delay(5000).Wait();
            }

            if (Directory.Exists(Config.Default.AutoUpdate_CacheDir))
            {
                if (!Config.Default.SteamCmdRedirectOutput)
                    // check if any of the server files have changed.
                    gotNewVersion = HasNewServerVersion(Config.Default.AutoUpdate_CacheDir, _startTime);

                if (gotNewVersion)
                {
                    LogMessage("***** New version downloaded. *****");

                    var latestCacheTimeFile = GetServerCacheTimeFile();
                    File.WriteAllText(latestCacheTimeFile, _startTime.ToString("o", CultureInfo.CurrentCulture));
                }
                else
                    LogMessage("No new version.");
            }
            else
                LogMessage($"Server cache does not exist.");

            LogMessage("-----------------------------");
            LogMessage("Finished server cache update.");
            LogMessage("-----------------------------");
            LogMessage("");
            ExitCode = EXITCODE_NORMALEXIT;
        }

        private void CloseRconConsole()
        {
            if (_rconConsole != null)
            {
                _rconConsole.Dispose();
                _rconConsole = null;

                Task.Delay(1000).Wait();
            }
        }

        public static void DirectoryCopy(string sourceFolder, string destinationFolder, bool copySubFolders, bool useSmartCopy, ProgressDelegate progressCallback)
        {
            var directory = new DirectoryInfo(sourceFolder);
            if (!directory.Exists)
                return;

            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubFolders)
            {
                var subDirectories = directory.GetDirectories();

                foreach (var subDirectory in subDirectories)
                {
                    var tempDirectory = Path.Combine(destinationFolder, subDirectory.Name);
                    DirectoryCopy(subDirectory.FullName, tempDirectory, copySubFolders, useSmartCopy, progressCallback);
                }
            }

            progressCallback?.Invoke(0, directory.FullName);

            // Get the files in the directory and copy them to the new location.
            var files = directory.GetFiles();

            foreach (var file in files)
            {
                if (!file.Exists)
                    continue;

                // check if the destination file is newer
                var destFile = new FileInfo(Path.Combine(destinationFolder, file.Name));
                if (useSmartCopy && destFile.Exists && destFile.LastWriteTime >= file.LastWriteTime && destFile.Length == file.Length)
                    continue;

                // destination file does not exist, or is older. Override with the source file.
                file.CopyTo(destFile.FullName, true);
            }
        }

        private string GetLauncherFile() => Updater.NormalizePath(Path.Combine(_profile.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.LauncherFile));

        private static string GetLogFile() => Updater.NormalizePath(Path.Combine(Updater.GetLogFolder(), _logPrefix, $"{_startTime.ToString("yyyyMMdd_HHmmss")}.log"));

        private List<string> GetModList()
        {
            var modIdList = new List<string>();

            // check if we need to update the mods.
            if (Config.Default.ServerUpdate_UpdateModsWhenUpdatingServer)
            {
                if (_profile == null)
                {
                    // get all the mods for all the profiles.
                    foreach (var profile in _profiles.Keys)
                    {
                        // check if the profile is included int he auto update.
                        if (!profile.EnableAutoUpdate)
                            continue;

                        if (!string.IsNullOrWhiteSpace(profile.ServerMapModId))
                            modIdList.Add(profile.ServerMapModId);

                        if (!string.IsNullOrWhiteSpace(profile.TotalConversionModId))
                            modIdList.Add(profile.TotalConversionModId);

                        modIdList.AddRange(profile.ServerModIds);
                    }
                }
                else
                {
                    // get all the mods for only the specified profile.
                    if (!string.IsNullOrWhiteSpace(_profile.ServerMapModId))
                        modIdList.Add(_profile.ServerMapModId);

                    if (!string.IsNullOrWhiteSpace(_profile.TotalConversionModId))
                        modIdList.Add(_profile.TotalConversionModId);

                    modIdList.AddRange(_profile.ServerModIds);
                }
            }

            return ModUtils.ValidateModList(modIdList);
        }

        private string GetProfileLogFile() => _profile != null ? Updater.NormalizePath(Path.Combine(Updater.GetLogFolder(), _profile.ProfileName, _logPrefix, $"{_startTime.ToString("yyyyMMdd_HHmmss")}.log")) : GetLogFile();

        private string GetModPath(string modId) => Updater.NormalizePath(Path.Combine(_profile.InstallDirectory, Config.Default.ServerModsRelativePath, modId));

        public static string GetMutexName(string directory)
        {
            using (var hashAlgo = MD5.Create())
            {
                StringBuilder builder = new StringBuilder();

                var hashStr = Encoding.UTF8.GetBytes(directory ?? Assembly.GetExecutingAssembly().Location);
                var hash = hashAlgo.ComputeHash(hashStr);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static string GetServerCacheTimeFile() => Updater.NormalizePath(Path.Combine(Config.Default.AutoUpdate_CacheDir, Config.Default.LastUpdatedTimeFile));

        private string GetServerExecutableFile() => Updater.NormalizePath(Path.Combine(_profile.InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerExe));

        private DateTime GetServerLatestTime(string timeFile)
        {
            try
            {
                if (!File.Exists(timeFile))
                    return DateTime.MinValue;

                var value = File.ReadAllText(timeFile);
                return DateTime.Parse(value, CultureInfo.CurrentCulture, DateTimeStyles.RoundtripKind);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private Process GetServerProcess()
        {
            // Find the server process.
            var expectedPath = GetServerExecutableFile();
            var runningProcesses = Process.GetProcessesByName(Config.Default.ServerProcessName);

            Process process = null;
            foreach (var runningProcess in runningProcesses)
            {
                var runningPath = ProcessUtils.GetMainModuleFilepath(runningProcess.Id);
                if (string.Equals(expectedPath, runningPath, StringComparison.OrdinalIgnoreCase))
                {
                    process = runningProcess;
                    break;
                }
            }

            return process;
        }

        private string GetServerTimeFile() => Updater.NormalizePath(Path.Combine(_profile.InstallDirectory, Config.Default.LastUpdatedTimeFile));

        private string GetServerWorldFile()
        {
            var profileSaveFolder = ServerProfile.GetProfileSavePath(_profile.InstallDirectory, _profile.AltSaveDirectoryName, _profile.PGM_Enabled, _profile.PGM_Name);
            var mapName = ServerProfile.GetProfileMapFileName(_profile.ServerMap, _profile.PGM_Enabled, _profile.PGM_Name);
            return Updater.NormalizePath(Path.Combine(profileSaveFolder, $"{mapName}.ark"));
        }

        private string GetServerWorldBackupFile()
        {
            var profileSaveFolder = ServerProfile.GetProfileSavePath(_profile.InstallDirectory, _profile.AltSaveDirectoryName, _profile.PGM_Enabled, _profile.PGM_Name);
            var mapName = ServerProfile.GetProfileMapFileName(_profile.ServerMap, _profile.PGM_Enabled, _profile.PGM_Name);
            return Updater.NormalizePath(Path.Combine(profileSaveFolder, $"{mapName}_ASMBackup_{_startTime.ToString("yyyyMMdd_HHmmss")}{Config.Default.BackupExtension}"));
        }

        public static bool HasNewServerVersion(string directory, DateTime checkTime)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            // check if any of the files have changed in the root folder.
            var hasNewVersion = new DirectoryInfo(directory).GetFiles("*.*", SearchOption.TopDirectoryOnly).Any(file => file.LastWriteTime >= checkTime);
            if (!hasNewVersion)
            {
                // get a list of the sub folders.
                var folders = new DirectoryInfo(directory).GetDirectories();
                foreach (var folder in folders)
                {
                    // do not include the steamapps folder in the check
                    if (folder.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                        continue;

                    hasNewVersion = folder.GetFiles("*.*", SearchOption.AllDirectories).Any(file => file.LastWriteTime >= checkTime);
                    if (hasNewVersion)
                        break;
                }
            }

            return hasNewVersion;
        }

        private static void LoadProfiles()
        {
            if (_profiles != null)
            {
                _profiles.Clear();
                _profiles = null;
            }

            _profiles = new Dictionary<ProfileSnapshot, ServerProfile>();

            foreach (var profileFile in Directory.EnumerateFiles(Config.Default.ConfigDirectory, "*" + Config.Default.ProfileExtension))
            {
                try
                {
                    var profile = ServerProfile.LoadFrom(profileFile);
                    _profiles.Add(ProfileSnapshot.Create(profile), profile);
                }
                catch (Exception ex)
                {
                    LogMessage($"The profile at {profileFile} failed to load.\r\n{ex.Message}\r\n{ex.StackTrace}");
                }
            }
        }

        private static void LogError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return;

            LogMessage($"***** ERROR: {error}");
        }

        private static void LogMessage(string message)
        {
            message = message ?? string.Empty;

            var logFile = GetLogFile();
            lock (LockObject)
            {
                if (!Directory.Exists(Path.GetDirectoryName(logFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(logFile));

                File.AppendAllLines(logFile, new[] { $"{DateTime.Now.ToString("o", CultureInfo.CurrentCulture)}: {message}" }, Encoding.Unicode);
            }

            Debug.WriteLine(message);
        }

        private void LogProfileError(string error, bool includeProgressCallback = true)
        {
            if (string.IsNullOrWhiteSpace(error))
                return;

            LogProfileMessage($"***** ERROR: {error}", includeProgressCallback);
        }

        private void LogProfileMessage(string message, bool includeProgressCallback = true)
        {
            message = message ?? string.Empty;

            if (OutputLogs)
            {
                var logFile = GetProfileLogFile();
                if (!Directory.Exists(Path.GetDirectoryName(logFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(logFile));
                File.AppendAllLines(logFile, new[] { $"{DateTime.Now.ToString("o", CultureInfo.CurrentCulture)}: {message}" }, Encoding.Unicode);
            }

            if (includeProgressCallback)
                ProgressCallback?.Invoke(0, message);

            if (_profile != null)
                Debug.WriteLine($"[{_profile?.ProfileName ?? "unknown"}] {message}");
            else
                Debug.WriteLine(message);
        }

        private void SendCommand(string command, bool retryIfFailed)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            int retries = 0;
            int rconRetries = 0;
            int maxRetries = retryIfFailed ? RCON_MAXRETRIES : 1;

            while (retries < maxRetries && rconRetries < RCON_MAXRETRIES)
            {
                SetupRconConsole();

                if (_rconConsole != null)
                {
                    rconRetries = 0;
                    try
                    {
                        _rconConsole.SendCommand(command);
                        LogProfileMessage($"RCON> {command}");

                        return;
                    }
                    catch (Exception ex)
                    {
                        LogProfileMessage($"RCON> {command} - attempt {retries + 1} (b).", false);
#if DEBUG
                        LogProfileMessage($"{ex.Message}", false);
#endif
                    }

                    retries++;
                }
                else
                {
                    LogProfileMessage($"RCON> {command} - attempt {rconRetries + 1} (a).", false);
#if DEBUG
                    LogProfileMessage("RCON connection not created.", false);
#endif
                    rconRetries++;
                }
            }
        }

        private void SendMessage(string message)
        {
            SendCommand($"broadcast {message}", false);
        }

        private void SendEmail(string subject, string body, bool includeLogFile, bool isBodyHtml = false)
        {
            if (!SendEmails)
                return;

            try
            {
                var email = new EmailUtil()
                {
                    EnableSsl = Config.Default.Email_UseSSL,
                    MailServer = Config.Default.Email_Host,
                    Port = Config.Default.Email_Port,
                    UseDefaultCredentials = Config.Default.Email_UseDetaultCredentials,
                    Credentials = Config.Default.Email_UseDetaultCredentials ? null : new NetworkCredential(Config.Default.Email_Username, Config.Default.Email_Password),
                };

                StringBuilder messageBody = new StringBuilder(body);
                Attachment attachment = null;

                if (includeLogFile)
                {
                    var logFile = GetProfileLogFile();
                    if (!string.IsNullOrWhiteSpace(logFile) && File.Exists(logFile))
                    {
                        attachment = new Attachment(logFile);
                    }
                }

                email.SendEmail(Config.Default.Email_From, Config.Default.Email_To?.Split(','), subject, messageBody.ToString(), isBodyHtml, new[] { attachment });

                LogProfileMessage($"Email Sent - {subject}\r\n{body}");
            }
            catch (Exception ex)
            {
                LogProfileError($"Unable to send email.\r\n{ex.Message}", false);
            }
        }

        private void SetupRconConsole()
        {
            CloseRconConsole();

            if (_profile == null)
                return;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Parse(_profile.ServerIP), _profile.RCONPort);
                var server = ServerQuery.GetServerInstance(EngineType.Source, endPoint, sendTimeOut: 10000, receiveTimeOut: 10000);
                if (server == null)
                {
#if DEBUG
                    LogProfileMessage($"FAILED: {nameof(SetupRconConsole)} - ServerQuery could not be created.", false);
#endif
                    return;
                }

#if DEBUG
                LogProfileMessage($"SUCCESS: {nameof(SetupRconConsole)} - ServerQuery was created.", false);
#endif

                Task.Delay(1000).Wait();

                _rconConsole = server.GetControl(_profile.AdminPassword);
                if (_rconConsole == null)
                {
#if DEBUG
                    LogProfileMessage($"FAILED: {nameof(SetupRconConsole)} - RconConsole could not be created ({_profile.AdminPassword}).", false);
#endif
                    return;
                }

#if DEBUG
                LogProfileMessage($"SUCCESS: {nameof(SetupRconConsole)} - RconConsole was created ({_profile.AdminPassword}).", false);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                LogProfileMessage($"ERROR: {nameof(SetupRconConsole)}\r\n{ex.Message}", false);
#endif
            }
        }

        public int PerformProfileBackup(ProfileSnapshot profile)
        {
            _profile = profile;

            if (_profile == null)
                return EXITCODE_NORMALEXIT;

            if (_profile.SotFEnabled)
                return EXITCODE_NORMALEXIT;

            ExitCode = EXITCODE_NORMALEXIT;

            Mutex mutex = null;
            var createdNew = false;

            try
            {
                // try to establish a mutex for the profile.
                mutex = new Mutex(true, GetMutexName(_profile.InstallDirectory), out createdNew);
                if (!createdNew)
                    createdNew = mutex.WaitOne(new TimeSpan(0, MUTEX_TIMEOUT, 0));

                // check if the mutex was established
                if (createdNew)
                {
                    BackupServer();

                    if (ExitCode != EXITCODE_NORMALEXIT)
                    {
                        if (Config.Default.EmailNotify_AutoBackup)
                            SendEmail($"{_profile.ProfileName} server backup", $"The server backup process was performed but an error occurred.", true);
                    }
                }
                else
                {
                    ExitCode = EXITCODE_PROCESSALREADYRUNNING;
                    LogProfileMessage("Cancelled server backup process, could not lock server.");
                }
            }
            catch (Exception ex)
            {
                LogProfileError(ex.Message);
                if (ex.InnerException != null)
                    LogProfileMessage($"InnerException - {ex.InnerException.Message}");
                LogProfileMessage($"StackTrace\r\n{ex.StackTrace}");

                if (Config.Default.EmailNotify_AutoBackup)
                    SendEmail($"{_profile.ProfileName} server update", $"The server backup process was performed but an error occurred.", true);
                ExitCode = EXITCODE_UNKNOWNTHREADERROR;
            }
            finally
            {
                if (mutex != null)
                {
                    if (createdNew)
                    {
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                    }
                    mutex = null;
                }
            }

            LogProfileMessage($"Exitcode = {ExitCode}");
            return ExitCode;
        }

        public int PerformProfileShutdown(ProfileSnapshot profile, bool performRestart, CancellationToken cancellationToken)
        {
            _profile = profile;

            if (_profile == null)
                return EXITCODE_NORMALEXIT;

            ExitCode = EXITCODE_NORMALEXIT;

            Mutex mutex = null;
            var createdNew = false;

            try
            {
                // try to establish a mutex for the profile.
                mutex = new Mutex(true, GetMutexName(_profile.InstallDirectory), out createdNew);
                if (!createdNew)
                    createdNew = mutex.WaitOne(new TimeSpan(0, MUTEX_TIMEOUT, 0));

                // check if the mutex was established
                if (createdNew)
                {
                    ShutdownServer(performRestart, cancellationToken);

                    if (ExitCode != EXITCODE_NORMALEXIT)
                    {
                        if (Config.Default.EmailNotify_AutoRestart)
                        {
                            if (performRestart)
                                SendEmail($"{_profile.ProfileName} server restart", $"The server restart process was performed but an error occurred.", true);
                            else
                                SendEmail($"{_profile.ProfileName} server shutdown", $"The server shutdown process was performed but an error occurred.", true);
                        }
                    }
                }
                else
                {
                    ExitCode = EXITCODE_PROCESSALREADYRUNNING;
                    if (performRestart)
                        LogProfileMessage("Cancelled server restart process, could not lock server.");
                    else
                        LogProfileMessage("Cancelled server shutdown process, could not lock server.");
                }
            }
            catch (Exception ex)
            {
                LogProfileError(ex.Message);
                if (ex.InnerException != null)
                    LogProfileMessage($"InnerException - {ex.InnerException.Message}");
                LogProfileMessage($"StackTrace\r\n{ex.StackTrace}");

                if (Config.Default.EmailNotify_AutoRestart)
                {
                    if (performRestart)
                        SendEmail($"{_profile.ProfileName} server restart", $"The server restart process was performed but an error occurred.", true);
                    else
                        SendEmail($"{_profile.ProfileName} server shutdown", $"The server shutdown process was performed but an error occurred.", true);
                }
                ExitCode = EXITCODE_UNKNOWNTHREADERROR;
            }
            finally
            {
                if (mutex != null)
                {
                    if (createdNew)
                    {
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                    }
                    mutex = null;
                }
            }

            LogProfileMessage($"Exitcode = {ExitCode}");
            return ExitCode;
        }

        public int PerformProfileUpdate(ProfileSnapshot profile)
        {
            _profile = profile;

            if (_profile == null)
                return EXITCODE_NORMALEXIT;

            if (_profile.SotFEnabled)
                return EXITCODE_NORMALEXIT;

            ExitCode = EXITCODE_NORMALEXIT;

            Mutex mutex = null;
            var createdNew = false;

            try
            {
                LogMessage($"[{_profile.ProfileName}] Started server update process.");

                // try to establish a mutex for the profile.
                mutex = new Mutex(true, GetMutexName(_profile.InstallDirectory), out createdNew);
                if (!createdNew)
                    createdNew = mutex.WaitOne(new TimeSpan(0, MUTEX_TIMEOUT, 0));

                // check if the mutex was established
                if (createdNew)
                {
                    UpdateFiles();

                    LogMessage($"[{_profile.ProfileName}] Finished server update process.");

                    if (ExitCode != EXITCODE_NORMALEXIT)
                    {
                        if (Config.Default.EmailNotify_AutoUpdate)
                            SendEmail($"{_profile.ProfileName} server update", $"The server update process was performed but an error occurred.", true);
                    }
                }
                else
                {
                    ExitCode = EXITCODE_PROCESSALREADYRUNNING;
                    LogMessage($"[{_profile.ProfileName}] Cancelled server update process, could not lock server.");
                }
            }
            catch (Exception ex)
            {
                LogProfileError(ex.Message);
                if (ex.InnerException != null)
                    LogProfileMessage($"InnerException - {ex.InnerException.Message}");
                LogProfileMessage($"StackTrace\r\n{ex.StackTrace}");

                if (Config.Default.EmailNotify_AutoUpdate)
                    SendEmail($"{_profile.ProfileName} server update", $"The server update process was performed but an error occurred.", true);
                ExitCode = EXITCODE_UNKNOWNTHREADERROR;
            }
            finally
            {
                if (mutex != null)
                {
                    if (createdNew)
                    {
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                    }
                    mutex = null;
                }
            }

            LogProfileMessage($"Exitcode = {ExitCode}");
            return ExitCode;
        }

        public static int PerformAutoBackup()
        {
            _logPrefix = LOGPREFIX_AUTOBACKUP;

            int exitCode = EXITCODE_NORMALEXIT;

            try
            {
                // check if the server backup has been enabled.
                if (!Config.Default.AutoBackup_EnableBackup)
                    return EXITCODE_AUTOBACKUPNOTENABLED;

                // load all the profiles, do this at the very start in case the user changes one or more while the process is running.
                LoadProfiles();

                var exitCodes = new ConcurrentDictionary<ProfileSnapshot, int>();

                Parallel.ForEach(_profiles.Keys.Where(p => p.EnableAutoBackup), profile => {
                    var app = new ServerApp();
                    app.DeleteOldBackupWorldFiles = Config.Default.AutoBackup_DeleteOldFiles;
                    app.SendEmails = true;
                    app.ServerProcess = ServerProcessType.AutoBackup;
                    exitCodes.TryAdd(profile, app.PerformProfileBackup(profile));
                });

                if (exitCodes.Any(c => !c.Value.Equals(EXITCODE_NORMALEXIT)))
                    exitCode = EXITCODE_EXITWITHERRORS;
            }
            catch (Exception)
            {
                exitCode = EXITCODE_UNKNOWNERROR;
            }

            return exitCode;
        }

        public static int PerformAutoShutdown(string argument, ServerProcessType type)
        {
            _logPrefix = LOGPREFIX_AUTOSHUTDOWN;

            int exitCode = EXITCODE_NORMALEXIT;

            try
            {
                if (string.IsNullOrWhiteSpace(argument) || (!argument.StartsWith(App.ARG_AUTOSHUTDOWN1) && !argument.StartsWith(App.ARG_AUTOSHUTDOWN2)))
                    return EXITCODE_BADARGUMENT;

                // load all the profiles, do this at the very start in case the user changes one or more while the process is running.
                LoadProfiles();

                var profileKey = string.Empty;
                switch (type)
                {
                    case ServerProcessType.AutoShutdown1:
                        profileKey = argument?.Substring(App.ARG_AUTOSHUTDOWN1.Length) ?? string.Empty;
                        break;
                    case ServerProcessType.AutoShutdown2:
                        profileKey = argument?.Substring(App.ARG_AUTOSHUTDOWN2.Length) ?? string.Empty;
                        break;
                    default:
                        return EXITCODE_BADARGUMENT;
                }

                var profile = _profiles?.Keys.FirstOrDefault(p => p.SchedulerKey.Equals(profileKey, StringComparison.Ordinal));
                if (profile == null)
                    return EXITCODE_PROFILENOTFOUND;

                var enableAutoShutdown = false;
                var performRestart = false;
                switch (type)
                {
                    case ServerProcessType.AutoShutdown1:
                        enableAutoShutdown = profile.EnableAutoShutdown1;
                        performRestart = profile.RestartAfterShutdown1;
                        break;
                    case ServerProcessType.AutoShutdown2:
                        enableAutoShutdown = profile.EnableAutoShutdown2;
                        performRestart = profile.RestartAfterShutdown2;
                        break;
                    default:
                        return EXITCODE_BADARGUMENT;
                }

                if (!enableAutoShutdown)
                    return EXITCODE_AUTOSHUTDOWNNOTENABLED;

                var app = new ServerApp();
                app.SendEmails = true;
                app.ServerProcess = type;
                exitCode = app.PerformProfileShutdown(profile, performRestart, CancellationToken.None);
            }
            catch (Exception)
            {
                exitCode = EXITCODE_UNKNOWNERROR;
            }

            return exitCode;
        }

        public static int PerformAutoUpdate()
        {
            _logPrefix = LOGPREFIX_AUTOUPDATE;

            int exitCode = EXITCODE_NORMALEXIT;

            Mutex mutex = null;
            bool createdNew = false;

            try
            {
                // check if the server cache has been enabled.
                if (!Config.Default.AutoUpdate_EnableUpdate)
                    return EXITCODE_AUTOUPDATENOTENABLED;

                // check if a data directory has been setup.
                if (string.IsNullOrWhiteSpace(Config.Default.DataDir))
                    return EXITCODE_INVALIDDATADIRECTORY;

                // check if the server cache folder has been set.
                if (string.IsNullOrWhiteSpace(Config.Default.AutoUpdate_CacheDir))
                    return EXITCODE_INVALIDCACHEDIRECTORY;

                // try to establish a mutex for the application.
                mutex = new Mutex(true, GetMutexName(Config.Default.DataDir), out createdNew);
                if (!createdNew)
                    createdNew = mutex.WaitOne(new TimeSpan(0, MUTEX_TIMEOUT, 0));

                // check if the mutex was established.
                if (createdNew)
                {
                    // load all the profiles, do this at the very start in case the user changes one or more while the process is running.
                    LoadProfiles();

                    ServerApp app = new ServerApp();
                    app.ServerProcess = ServerProcessType.AutoUpdate;
                    app.UpdateServerCache();
                    exitCode = app.ExitCode;

                    if (exitCode == EXITCODE_NORMALEXIT)
                    {
                        app.UpdateModCache();
                        exitCode = app.ExitCode;
                    }

                    if (exitCode == EXITCODE_NORMALEXIT)
                    {
                        var exitCodes = new ConcurrentDictionary<ProfileSnapshot, int>();

                        Parallel.ForEach(_profiles.Keys.Where(p => p.EnableAutoUpdate), profile => {
                            app = new ServerApp();
                            app.SendEmails = true;
                            app.ServerProcess = ServerProcessType.AutoUpdate;
                            exitCodes.TryAdd(profile, app.PerformProfileUpdate(profile));
                        });

                        if (exitCodes.Any(c => !c.Value.Equals(EXITCODE_NORMALEXIT)))
                            exitCode = EXITCODE_EXITWITHERRORS;
                    }
                }
                else
                {
                    LogMessage("Cancelled auto update process, could not lock application.");
                    return EXITCODE_PROCESSALREADYRUNNING;
                }
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                if (ex.InnerException != null)
                    LogMessage($"InnerException - {ex.InnerException.Message}");
                LogMessage($"StackTrace\r\n{ex.StackTrace}");
                exitCode = EXITCODE_UNKNOWNERROR;
            }
            finally
            {
                if (mutex != null)
                {
                    if (createdNew)
                    {
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                    }
                    mutex = null;
                }
            }

            LogMessage("");
            LogMessage($"Exitcode = {exitCode}");
            return exitCode;
        }
    }
}

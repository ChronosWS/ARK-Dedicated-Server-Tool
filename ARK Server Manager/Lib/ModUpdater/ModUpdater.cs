using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace ARK_Server_Manager.Lib
{
    public static class ModUpdater
    {
        public const String MOD_TYPE = "ModType";

        public const String MOD_TYPE_GAME = "1";
        public const String MOD_TYPE_MAP = "2";
        public const String MOD_TYPE_TC = "3";
        public const String MOD_TYPE_ME = "4";

        public static Logger logger = LogManager.GetCurrentClassLogger();

        public static Task<String> CheckServerModsAsync(string serverInstallDirectory, string mapIdString, string tcnIdString, string modIdsString, string serverMap)
        {
            // check if the server installation folder exists.
            if (string.IsNullOrWhiteSpace(serverInstallDirectory) || !Directory.Exists(serverInstallDirectory))
                // server installation folder does not exist, return error.
                return Task.FromResult<String>("Server installation folder does not exist or could not be found.");

            // check if any mods have been entered.
            if (String.IsNullOrWhiteSpace(mapIdString) && String.IsNullOrWhiteSpace(tcnIdString) && String.IsNullOrWhiteSpace(modIdsString))
                // no maps/mods specified, just return.
                return Task.FromResult<String>(String.Empty);

            // check if a server map has been defined.
            if (String.IsNullOrWhiteSpace(serverMap))
                return Task.FromResult<String>("Server map has not been defined.");

            // check if both a custom map and total conversion has been defined.
            if (!String.IsNullOrWhiteSpace(mapIdString) && !String.IsNullOrWhiteSpace(tcnIdString))
                return Task.FromResult<String>("Both custom map and total conversion have been defined. Only one can be specified at a time.");

            // get a list of all the downloaded mods.
            var modDetails = ModUtilities.GetAllModDetails(serverInstallDirectory);

            // check if all the maps and mods have been downloaded.
            if (modDetails == null && !String.IsNullOrWhiteSpace(mapIdString))
                return Task.FromResult<String>($"The specified custom map mod '{mapIdString}' has not been downloaded.");

            if (modDetails == null && !String.IsNullOrWhiteSpace(tcnIdString))
                return Task.FromResult<String>($"The specified total conversion mod '{tcnIdString}' has not been downloaded.");

            if (modDetails == null && !String.IsNullOrWhiteSpace(modIdsString))
                return Task.FromResult<String>("None of the specified mods have been downloaded.");

            if (!String.IsNullOrWhiteSpace(mapIdString))
            {
                if (!modDetails.ContainsKey(mapIdString))
                    return Task.FromResult<String>($"The specified custom map mod '{mapIdString}' has not been downloaded.");
                else
                {
                    // check if the mod type is valid.
                    var modDetail = modDetails[mapIdString];
                    if (modDetail.MetaInformation == null || !modDetail.MetaInformation.ContainsKey(MOD_TYPE))
                        return Task.FromResult<String>($"The specified custom map mod '{mapIdString}' could not be confirmed as a map mod.");
                    else
                    {
                        var modType = modDetail.MetaInformation["ModType"];
                        if (modType != MOD_TYPE_MAP)
                            return Task.FromResult<String>($"The specified custom map mod '{mapIdString}' could not be confirmed as a map mod.");
                    }

                    // check the server map name matches the custom map mod.
                    if (modDetail.MapNames == null || modDetail.MapNames.Count == 0)
                        return Task.FromResult<String>($"The map name of the specified custom map mod '{mapIdString}' could not be found.");

                    var mapName = modDetail.MapNames[0];
                    if (!mapName.Equals(serverMap, StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult<String>($"The map name of the specified custom map mod '{mapIdString}' did not match the specified server map.");
                }
            }

            if (!String.IsNullOrWhiteSpace(tcnIdString))
            {
                if (!modDetails.ContainsKey(tcnIdString))
                    return Task.FromResult<String>($"The specified total conversion mod '{tcnIdString}' has not been downloaded.");
                else
                {
                    // check if the mod type is valid.
                    var modDetail = modDetails[tcnIdString];
                    if (modDetail.MetaInformation == null || !modDetail.MetaInformation.ContainsKey(MOD_TYPE))
                        return Task.FromResult<String>($"The specified total conversion mod '{tcnIdString}' could not be confirmed as a total conversion mod.");
                    else
                    {
                        var modType = modDetail.MetaInformation["ModType"];
                        if (modType != MOD_TYPE_TC)
                            return Task.FromResult<String>($"The specified total conversion mod '{tcnIdString}' could not be confirmed as a total conversion mod.");
                    }

                    // check the server map name matches the total conversion mod.
                    if (modDetail.MapNames == null || modDetail.MapNames.Count == 0)
                        return Task.FromResult<String>($"The map name of the specified total conversion mod '{tcnIdString}' could not be found.");

                    var mapName = modDetail.MapNames[0];
                    if (!mapName.Equals(serverMap, StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult<String>($"The map name of the specified total conversion mod '{tcnIdString}' did not match the specified server map.");
                }
            }

            if (!String.IsNullOrWhiteSpace(modIdsString))
            {
                // break the mod id string into an array.
                var modIdArray = modIdsString?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (modIdArray != null && modIdArray.Length > 0)
                {
                    foreach (var id in modIdArray)
                    {
                        if (!modDetails.ContainsKey(id))
                            return Task.FromResult<String>($"The mod '{id}' has not been downloaded.");
                    }
                }
            }

            return Task.FromResult<String>(String.Empty);
        }

        public static Task<string> GetMapNameAsync(string serverInstallDirectory, string mapIdString, string tcnIdString, DataReceivedEventHandler outputHandler, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(serverInstallDirectory) || !Directory.Exists(serverInstallDirectory))
                return Task.FromResult<string>(null);

            if (string.IsNullOrWhiteSpace(mapIdString) && string.IsNullOrWhiteSpace(tcnIdString))
                return Task.FromResult(String.Empty);

            try
            {
                var mapIdArray = mapIdString?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (mapIdArray != null && mapIdArray.Length > 0)
                {
                    var modDetails = ModUtilities.GetModDetails(serverInstallDirectory, mapIdArray[0]);

                    if (modDetails != null && modDetails.MapNames != null && modDetails.MapNames.Count > 0)
                        return Task.FromResult(modDetails.MapNames[0]);
                }

                var tcnIdArray = tcnIdString?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (tcnIdArray != null && tcnIdArray.Length > 0)
                {
                    var modDetails = ModUtilities.GetModDetails(serverInstallDirectory, tcnIdArray[0]);

                    if (modDetails != null && modDetails.MapNames != null && modDetails.MapNames.Count > 0)
                        return Task.FromResult(modDetails.MapNames[0]);
                }

                return Task.FromResult<string>(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModUpdater.GetMapNameAsync - {ex.Message}");
                logger.Debug($"ModUpdater.GetMapNameAsync - {ex.Message}\n\n{ex.StackTrace}");
                return Task.FromResult<string>(null);
            }
        }

        public static Task<bool> UpgradeAsync(string serverInstallDirectory, string steamCmdFile, string steamCmdArgsFormat, string mapIdString, string tcnIdString, string modIdsString, DataReceivedEventHandler outputHandler, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(steamCmdFile) || !File.Exists(steamCmdFile))
                return Task.FromResult<bool>(false);

            if (string.IsNullOrWhiteSpace(mapIdString) && string.IsNullOrWhiteSpace(tcnIdString) && string.IsNullOrWhiteSpace(modIdsString))
                return Task.FromResult<bool>(true);

            var mapIdArray = mapIdString?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var tcnIdArray = tcnIdString?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var modIdArray = modIdsString?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if ((mapIdArray == null || mapIdArray.Length == 0) && (tcnIdArray == null || tcnIdArray.Length == 0) && (modIdArray == null || modIdArray.Length == 0))
                return Task.FromResult<bool>(true);

            var idList = new List<String>();
            if (mapIdArray != null) idList.AddRange(mapIdArray);
            if (tcnIdArray != null) idList.AddRange(tcnIdArray);
            if (modIdArray != null) idList.AddRange(modIdArray);

            // create a comma delimited string of the mod and map ids, excluding any nulls or empties.
            var idString = String.Join(",", idList.Where(id => !String.IsNullOrWhiteSpace(id)).ToArray());

            try
            {
                foreach (var id in idList)
                {
                    var steamArgs = String.Format(steamCmdArgsFormat, id);

                    var steamCMDInfo = new ProcessStartInfo()
                    {
                        FileName = steamCmdFile,
                        Arguments = steamArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = outputHandler != null,
                    };

                    var steamCMDProcess = Process.Start(steamCMDInfo);
                    steamCMDProcess.EnableRaisingEvents = true;
                    if (outputHandler != null)
                    {
                        steamCMDProcess.OutputDataReceived += outputHandler;
                        steamCMDProcess.BeginOutputReadLine();
                    }

                    var steamCMDTS = new TaskCompletionSource<bool>();
                    using (var cancelRegistration = cancellationToken.Register(() => { try { steamCMDProcess.CloseMainWindow(); } finally { steamCMDTS.TrySetCanceled(); } }))
                    {
                        steamCMDProcess.Exited += (s, e) => steamCMDTS.TrySetResult(steamCMDProcess.ExitCode == 0);
                        steamCMDProcess.ErrorDataReceived += (s, e) => steamCMDTS.TrySetException(new Exception(e.Data));
                        if (!steamCMDTS.Task.Result)
                            return Task.FromResult<bool>(false);
                    }
                }

                var rootFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var copyFile = Path.Combine(rootFolder, @"Lib\ModUpdater", "arkmodcopy.exe");

                var copyInfo = new ProcessStartInfo()
                {
                    FileName = copyFile,
                    Arguments = $"\"{Path.GetDirectoryName(steamCmdFile)}\" \"{serverInstallDirectory}\" \"{idString}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = outputHandler != null,
                };

                var copyProcess = Process.Start(copyInfo);
                copyProcess.EnableRaisingEvents = true;
                if (outputHandler != null)
                {
                    copyProcess.OutputDataReceived += outputHandler;
                    copyProcess.BeginOutputReadLine();
                }

                var copyTS = new TaskCompletionSource<bool>();
                using (var cancelRegistration = cancellationToken.Register(() => { try { copyProcess.CloseMainWindow(); } finally { copyTS.TrySetCanceled(); } }))
                {
                    copyProcess.Exited += (s, e) => copyTS.TrySetResult(copyProcess.ExitCode == 0);
                    copyProcess.ErrorDataReceived += (s, e) => copyTS.TrySetException(new Exception(e.Data));
                    if (!copyTS.Task.Result)
                        return Task.FromResult<bool>(false);
                }

                return Task.FromResult<bool>(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModUpdater.UpgradeAsync - {ex.Message}");
                logger.Debug($"ModUpdater.UpgradeAsync - {ex.Message}\n\n{ex.StackTrace}");
                return Task.FromResult<bool>(false);
            }
        }
    }
}

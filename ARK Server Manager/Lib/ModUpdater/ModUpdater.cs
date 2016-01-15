using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public static class ModUpdater
    {
        public static Task<bool> UpgradeAsync(string serverInstallDirectory, string steamCmdFile, string steamCmdArgsFormat, string modIdsString, string mapIdsString, DataReceivedEventHandler outputHandler, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(steamCmdFile) || !File.Exists(steamCmdFile))
                return Task.FromResult<bool>(false);

            if (string.IsNullOrWhiteSpace(mapIdsString) && string.IsNullOrWhiteSpace(modIdsString))
                return Task.FromResult<bool>(true);

            var mapIdArray = mapIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var modIdArray = modIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (mapIdArray.Length == 0 && modIdArray.Length == 0)
                return Task.FromResult<bool>(true);

            var idList = mapIdArray.ToList();
            idList.AddRange(modIdArray);

            // create a comma delimited string of the mod and map ids, excluding an nulls or empties.
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
                return Task.FromResult<bool>(false);
            }
        }

        public static Task<string> GetMapName(string serverInstallDirectory, string mapIdsString, DataReceivedEventHandler outputHandler, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(serverInstallDirectory) || !Directory.Exists(serverInstallDirectory))
                return Task.FromResult<string>(null);

            if (string.IsNullOrWhiteSpace(mapIdsString))
                return Task.FromResult<string>(String.Empty);

            var mapIdArray = mapIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (mapIdArray.Length == 0)
                return Task.FromResult<string>(String.Empty);

            try
            {
                var id = mapIdArray[0];
                Dictionary<string, string> metaInformation;
                List<string> mapNames;

                ModCopy.GetModDetails(serverInstallDirectory, id, out metaInformation, out mapNames);

                if (mapNames != null && mapNames.Count > 0)
                    return Task.FromResult<string>(mapNames[0]);

                return Task.FromResult<string>(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModUpdater.GetMapName - {ex.Message}");
                return Task.FromResult<string>(null);
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public static class ModUpdater
    {
        public static Task<bool> UpgradeModAsync(string serverInstallDirectory, string steamCmdFile, string steamCmdArgsFormat, string modIdsString, DataReceivedEventHandler outputHandler, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(steamCmdFile) || !File.Exists(steamCmdFile))
                return Task.FromResult<bool>(false);

            if (string.IsNullOrWhiteSpace(modIdsString))
                return Task.FromResult<bool>(true);

            var modIdArray = modIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (modIdArray.Length == 0)
                return Task.FromResult<bool>(true);

            try
            {
                foreach (var modId in modIdArray)
                {
                    var steamArgs = String.Format(steamCmdArgsFormat, modId);

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
                var modCopyFile = Path.Combine(rootFolder, @"Lib\ModUpdater", "arkmodcopy.exe");

                var modCopyInfo = new ProcessStartInfo()
                {
                    FileName = modCopyFile,
                    Arguments = $"\"{Path.GetDirectoryName(steamCmdFile)}\" \"{serverInstallDirectory}\" \"{modIdsString}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = outputHandler != null,
                };

                var modCopyProcess = Process.Start(modCopyInfo);
                modCopyProcess.EnableRaisingEvents = true;
                if (outputHandler != null)
                {
                    modCopyProcess.OutputDataReceived += outputHandler;
                    modCopyProcess.BeginOutputReadLine();
                }

                var modCopyTS = new TaskCompletionSource<bool>();
                using (var cancelRegistration = cancellationToken.Register(() => { try { modCopyProcess.CloseMainWindow(); } finally { modCopyTS.TrySetCanceled(); } }))
                {
                    modCopyProcess.Exited += (s, e) => modCopyTS.TrySetResult(modCopyProcess.ExitCode == 0);
                    modCopyProcess.ErrorDataReceived += (s, e) => modCopyTS.TrySetException(new Exception(e.Data));
                    if (!modCopyTS.Task.Result)
                        return Task.FromResult<bool>(false);
                }

                return Task.FromResult<bool>(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModUpdater.UpgradeModAsync - {ex.Message}");
                return Task.FromResult<bool>(false);
            }
        }
    }
}

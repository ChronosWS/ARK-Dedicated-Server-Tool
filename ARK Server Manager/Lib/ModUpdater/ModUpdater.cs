using System;
using System.Diagnostics;
using System.IO;
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

            foreach (var modId in modIdArray)
            {
                var steamArgs = String.Format(steamCmdArgsFormat, modId);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = steamCmdFile,
                    Arguments = steamArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = outputHandler != null,
                };
            
                var process = Process.Start(startInfo);
                process.EnableRaisingEvents = true;
                if (outputHandler != null)
                {
                    process.OutputDataReceived += outputHandler;
                    process.BeginOutputReadLine();
                }

                var ts = new TaskCompletionSource<bool>(); 
                using (var cancelRegistration = cancellationToken.Register(() => { try { process.CloseMainWindow(); } finally { ts.TrySetCanceled(); } }))
                {
                    process.Exited += (s, e) => ts.TrySetResult(process.ExitCode == 0);
                    process.ErrorDataReceived += (s, e) => ts.TrySetException(new Exception(e.Data));
                    if (!ts.Task.Result)
                        return Task.FromResult<bool>(false);
                }
            }

            // copy the downloaded mod file from the steamcmd folder into the server folder.
            var success = ModCopy.InstallMod(Path.GetDirectoryName(steamCmdFile), serverInstallDirectory, modIdArray);

            return Task.FromResult<bool>(success);
        }
    }
}

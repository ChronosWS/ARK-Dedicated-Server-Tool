using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public static class ProcessUtils
    {
        // Delegate type to be used as the Handler Routine for SCCH
        delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        // Enumerated type for the control messages sent to the handler routine
        enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int IsIconic(IntPtr hWnd);

        public static string FIELD_COMMANDLINE = "CommandLine";
        public static string FIELD_EXECUTABLEPATH = "ExecutablePath";
        public static string FIELD_PROCESSID = "ProcessId";

        private const int SW_RESTORE = 9;

        private static Mutex _mutex;

        public static string GetCommandLineForProcess(int processId)
        {
            var wmiQueryString = $"SELECT {FIELD_COMMANDLINE} FROM Win32_Process WHERE {FIELD_PROCESSID} = {processId}";

            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    ManagementObject mo = results.Cast<ManagementObject>().FirstOrDefault();
                    if (mo != null)
                        return (string)mo[FIELD_COMMANDLINE];
                }
            }

            return null;
        }

        public static string GetMainModuleFilepath(int processId)
        {
            var wmiQueryString = $"SELECT {FIELD_EXECUTABLEPATH} FROM Win32_Process WHERE {FIELD_PROCESSID} = {processId}";

            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    ManagementObject mo = results.Cast<ManagementObject>().FirstOrDefault();
                    if (mo != null)
                        return (string)mo[FIELD_EXECUTABLEPATH];
                }
            }

            return null;
        }

        public static async Task SendStopAsync(Process process)
        {
            if (process == null)
                return;

            var ts = new TaskCompletionSource<bool>();
            EventHandler handler = (s, e) => ts.TrySetResult(true);

            try
            {
                process.Exited += handler;

                //This does not require the console window to be visible.
                if (AttachConsole((uint)process.Id))
                {
                    // Disable Ctrl-C handling for our program
                    SetConsoleCtrlHandler(null, true);
                    GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);

                    // Must wait here. If we don't and re-enable Ctrl-C
                    // handling below too fast, we might terminate ourselves.

                    await TaskUtils.TimeoutAfterAsync(ts.Task, 30000)
                        .ContinueWith(async t1 =>
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();

                                if (!process.HasExited)
                                {
                                    await Task.Delay(5000);
                                }
                            }
                        }, TaskContinuationOptions.NotOnRanToCompletion)
                        .ContinueWith(t2 =>
                        {
                            FreeConsole();

                            //Re-enable Ctrl-C handling or any subsequently started
                            //programs will inherit the disabled state.
                            SetConsoleCtrlHandler(null, false);
                        });
                }
                else
                {
                    process.Kill();
                }
            }
            finally
            {
                process.Exited -= handler;
            }
        }

        public static Task<bool> RunProcessAsync(string fileName, string arguments, string verb, DataReceivedEventHandler outputHandler, CancellationToken cancellationToken, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,
                Verb = verb,
                UseShellExecute = outputHandler == null && windowStyle == ProcessWindowStyle.Minimized,
                RedirectStandardOutput = outputHandler != null,
                CreateNoWindow = outputHandler != null || windowStyle == ProcessWindowStyle.Hidden,
                WindowStyle = windowStyle,
            };

            var process = Process.Start(startInfo);
            process.EnableRaisingEvents = true;
            if (outputHandler != null)
            {
                process.OutputDataReceived += outputHandler;
                process.BeginOutputReadLine();
            }

            var tcs = new TaskCompletionSource<bool>();
            using (var cancelRegistration = cancellationToken.Register(() =>
                {
                    try
                    {
                        process.Kill();
                    }
                    finally
                    {
                        tcs.TrySetCanceled();
                    }
                }))
            {
                process.Exited += ((s, e) =>
                {
                    tcs.TrySetResult(process.ExitCode == 0);
                    process.Close();
                });
                return tcs.Task;
            }
        }

        private static IntPtr GetCurrentInstanceWindowHandle()
        {
            var hWnd = IntPtr.Zero;
            var currentProcess = Process.GetCurrentProcess();

            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var process in processes)
            {
                // Get the first instance that is not this instance, has the same process name and was started from the same file name
                // and location. Also check that the process has a valid window handle in this session to filter out other user's processes.
                if (process.Id != currentProcess.Id && process.MainModule.FileName == currentProcess.MainModule.FileName && process.MainWindowHandle != IntPtr.Zero)
                {
                    hWnd = process.MainWindowHandle;
                    break;
                }
            }

            return hWnd;
        }

        public static bool IsAlreadyRunning()
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var name = $"Global::{Path.GetFileName(assemblyLocation)}";

            bool createdNew;
            _mutex = new Mutex(true, name, out createdNew);
            if (createdNew)
                _mutex.ReleaseMutex();

            return !createdNew;
        }

        public static bool SwitchToCurrentInstance()
        {
            var hWnd = GetCurrentInstanceWindowHandle();
            if (hWnd == IntPtr.Zero)
                return false;

            // Restore window if minimised. Do not restore if already in normal or maximised window state, since we don't want to
            // change the current state of the window.
            if (IsIconic(hWnd) != 0)
                ShowWindow(hWnd, SW_RESTORE);

            // Set foreground window.
            SetForegroundWindow(hWnd);

            return true;
        }
    }
}

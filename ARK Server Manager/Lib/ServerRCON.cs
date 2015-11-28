using ARK_Server_Manager.Lib.ViewModel.RCON;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ARK_Server_Manager.Lib
{
    public class ServerRCON : DependencyObject, IAsyncDisposable
    {        
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(ConsoleStatus), typeof(ServerRCON), new PropertyMetadata(ConsoleStatus.Disconnected));
        public static readonly DependencyProperty PlayersProperty =
            DependencyProperty.Register(nameof(Players), typeof(SortableObservableCollection<PlayerInfo>), typeof(ServerRCON), new PropertyMetadata(null));

        public ConsoleStatus Status
        {
            get { return (ConsoleStatus)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        public SortableObservableCollection<PlayerInfo> Players
        {
            get { return (SortableObservableCollection<PlayerInfo>)GetValue(PlayersProperty); }
            set { SetValue(PlayersProperty, value); }
        }
                
        public enum ConsoleStatus
        {
            Disconnected,
            Connected,
        };

        private Logger chatLogger;
        private Logger allLogger;
        private Logger eventLogger;
        private Logger _logger;

        public class ConsoleCommand
        {
            public ConsoleStatus status;
            public string rawCommand;

            public string command;
            public string args;
            
            public bool suppressCommand;
            public bool suppressOutput;
            public IEnumerable<string> lines = new string[0];
        };

        private const int ListPlayersPeriod = 5000;
        private const int GetChatPeriod = 1000;
        private readonly ActionQueue commandProcessor;
        private readonly ActionQueue outputProcessor;
        private ServerRuntime.RuntimeProfileSnapshot snapshot;
        private readonly PropertyChangeNotifier runtimeChangedNotifier;
        private QueryMaster.Rcon console;

        public ServerRCON(Server server)
        {
            this.runtimeChangedNotifier = new PropertyChangeNotifier(server.Runtime, ServerRuntime.ProfileSnapshotProperty, (s, d) =>
            {
                var oldSnapshot = this.snapshot;
                if (d == null || d.NewValue == null) return;

                var newSnapshot = (ServerRuntime.RuntimeProfileSnapshot)d.NewValue;
                this.snapshot = (ServerRuntime.RuntimeProfileSnapshot)d.NewValue;

                bool reinitLoggers = !String.Equals(this.snapshot.ProfileName, newSnapshot.ProfileName);
                if (reinitLoggers)
                {
                    ReinitializeLoggers();
                }
            });

            this.commandProcessor = new ActionQueue(TaskScheduler.Default);

            // This is on the UI thread so we can do things like update dependency properties and whatnot.
            this.outputProcessor = new ActionQueue(TaskScheduler.FromCurrentSynchronizationContext());

            this.Players = new SortableObservableCollection<PlayerInfo>();

            this.snapshot = server.Runtime.ProfileSnapshot;
            ReinitializeLoggers();
            commandProcessor.PostAction(AutoPlayerList);
            commandProcessor.PostAction(AutoGetChat);
        }

        private void ReinitializeLoggers()
        {
            this.allLogger = App.GetProfileLogger(this.snapshot.ProfileName, "RCON_All");
            this.chatLogger = App.GetProfileLogger(this.snapshot.ProfileName, "RCON_Chat");
            this.eventLogger = App.GetProfileLogger(this.snapshot.ProfileName, "RCON_Event");
            this._logger = App.GetProfileLogger(this.snapshot.ProfileName, "RCON_Debug");
        }

        private enum LogEventType
        {
            All,
            Chat,
            Event
        }

        private void LogEvent(LogEventType eventType, string message)
        {
            switch(eventType)
            {
                case LogEventType.All:
                    this.allLogger.Info(message);
                    return;

                case LogEventType.Chat:
                    this.chatLogger.Info(message);
                    return;

                case LogEventType.Event:
                    this.eventLogger.Info(message);
                    return;
            }
        }

        private Task AutoPlayerList()
        {
            return this.commandProcessor.PostAction(() =>
            {
                ProcessInput(new ConsoleCommand() { rawCommand = "listplayers", suppressCommand = true, suppressOutput = true });
                Task.Delay(ListPlayersPeriod).ContinueWith(t => commandProcessor.PostAction(AutoPlayerList)).DoNotWait();
            });
        }

        private Task AutoGetChat()
        {
            return this.commandProcessor.PostAction(() =>
            {
                ProcessInput(new ConsoleCommand() { rawCommand = "getchat", suppressCommand = true, suppressOutput = true });
                Task.Delay(GetChatPeriod).ContinueWith(t => commandProcessor.PostAction(AutoGetChat)).DoNotWait();
            });
        }

        public Task<bool> IssueCommand(string userCommand)
        {
            return this.commandProcessor.PostAction(() => ProcessInput(new ConsoleCommand() { rawCommand = userCommand }));
        }

        public async Task DisposeAsync()
        {
            await this.commandProcessor.DisposeAsync();
            await this.outputProcessor.DisposeAsync();
            this.runtimeChangedNotifier.Dispose();
        }

        private class CommandListener : IDisposable
        {
            public Action<ConsoleCommand> Callback { get; set; }
            public Action<CommandListener> DisposeAction { get; set; }

            public void Dispose()
            {
                DisposeAction(this);
            }
        }

        List<CommandListener> commandListeners = new List<CommandListener>();

        public IDisposable RegisterCommandListener(Action<ConsoleCommand> callback)
        {
            var listener = new CommandListener { Callback = callback, DisposeAction = UnregisterCommandListener };
            this.commandListeners.Add(listener);
            return listener;
        }

        private void UnregisterCommandListener(CommandListener listener)
        {
            this.commandListeners.Remove(listener);
        }

        //
        // This is bound to the UI thread
        //
        private void ProcessOutput(ConsoleCommand command)
        {
            //
            // Handle results
            //
            HandleCommand(command);
            NotifyCommand(command);
        }

        //
        // This is bound to the UI thread
        //
        private void NotifyCommand(ConsoleCommand command)
        {
            foreach (var listener in commandListeners)
            {
                try
                {
                    listener.Callback(command);
                }
                catch (Exception ex)
                {
                    _logger.Error("Exception in command listener: {0}\n{1}", ex.Message, ex.StackTrace);
                }
            }
        }

        //
        // This is bound to the UI thread
        //
        private void HandleCommand(ConsoleCommand command)
        {
            //
            // Change the connection state as appropriate
            //
            this.Status = command.status;

            //
            // Perform per-command special processing to extract data
            //
            if(command.command.Equals("listplayers", StringComparison.OrdinalIgnoreCase))
            {
                var output = new List<string>();
                //
                // Update the visible player list
                //
                var newPlayerList = new List<PlayerInfo>();                
                foreach(var line in command.lines)
                {                    
                    var elements = line.Split(',');
                    if(elements.Length == 2)
                    {
                        var newPlayer = new ViewModel.RCON.PlayerInfo()
                        {
                            SteamName = elements[0].Substring(elements[0].IndexOf('.') + 1).Trim(),
                            SteamId = Int64.Parse(elements[1]),
                            IsOnline = true
                        };

                        if(newPlayerList.FirstOrDefault(p => p.SteamId == newPlayer.SteamId) != null)
                        {
                            // We received a duplicate.  Ignore it.
                            continue;
                        }

                        newPlayerList.Add(newPlayer);

                        var existingPlayer = this.Players.FirstOrDefault(p => p.SteamId == newPlayer.SteamId);
                        bool playerJoined = existingPlayer == null || existingPlayer.IsOnline == false;

                        if (existingPlayer == null)
                        {
                            this.Players.Add(newPlayer);
                        }
                        else
                        {
                            existingPlayer.IsOnline = true;
                        }

                        if(playerJoined)
                        {
                            var message = $"Player '{newPlayer.SteamName}' joined the game.";
                            output.Add(message);
                            LogEvent(LogEventType.Event, message);
                            LogEvent(LogEventType.All, message);
                        }
                    }
                }
               
                var droppedPlayers = this.Players.Where(p => newPlayerList.FirstOrDefault(np => np.SteamId == p.SteamId) == null).ToArray();
                foreach (var player in droppedPlayers)
                {
                    if(player.IsOnline)
                    {
                        var message = $"Player '{player.SteamName}' left the game.";
                        output.Add(message);
                        LogEvent(LogEventType.Event, message);
                        LogEvent(LogEventType.All, message);
                        player.IsOnline = false;
                    }
                }

                this.Players.Sort(p => !p.IsOnline);

                if (this.Players.Count == 0 || newPlayerList.Count > 0)
                {
                    commandProcessor.PostAction(UpdatePlayerDetails);
                }

                command.suppressOutput = false;
                command.lines = output;
            }
            else if(command.command.Equals("getchat", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Extract the player name from the chat
                var lines = command.lines.Where(l => !String.IsNullOrEmpty(l) && l != NoResponseOutput).ToArray();
                if(lines.Length == 0 && command.suppressCommand)
                {
                    command.suppressOutput = true;
                }
                else
                {
                    command.suppressOutput = false;   
                    command.lines = lines;
                    foreach(var line in lines)
                    {
                        LogEvent(LogEventType.Chat, line);
                        LogEvent(LogEventType.All, line);
                    }
                }
            }
            else if (command.command.Equals("broadcast", StringComparison.OrdinalIgnoreCase))
            {
                LogEvent(LogEventType.Chat, command.rawCommand);
                command.suppressOutput = true;
            }
            else if (command.command.Equals("serverchat", StringComparison.OrdinalIgnoreCase))
            {
                LogEvent(LogEventType.Chat, command.rawCommand);
                command.suppressOutput = true;
            }
        }

        private async Task UpdatePlayerDetails()
        {
            var savedArksPath = Path.Combine(snapshot.InstallDirectory, Config.Default.SavedArksRelativePath);
            var arkData = await ArkData.ArkDataContainer.CreateAsync(savedArksPath);
            await arkData.LoadSteamAsync(Config.Default.SteamAPIKey);
            TaskUtils.RunOnUIThreadAsync(() =>
            {
                foreach (var playerData in arkData.Players)
                {
                    var playerToUpdate = this.Players.FirstOrDefault(p => p.SteamId == Int64.Parse(playerData.SteamId));
                    if (playerToUpdate != null)
                    {
                        playerToUpdate.UpdateArkData(playerData).DoNotWait();
                    }
                    else
                    {
                        var newPlayer = new PlayerInfo() { SteamId = Int64.Parse(playerData.SteamId), SteamName = playerData.SteamName };
                        newPlayer.UpdateArkData(playerData).DoNotWait();
                        this.Players.Add(newPlayer);
                    }
                }
            }).DoNotWait();           
        }

        private static readonly char[] lineSplitChars = new char[] { '\n' };
        private static readonly char[] argsSplitChars = new char[] { ' ' };
        private const string NoResponseMatch = "Server received, But no response!!";
        public const string NoResponseOutput = "NO_RESPONSE";
        
        private bool ProcessInput(ConsoleCommand command)
        {
            try
            {
                if (!command.suppressCommand)
                {
                    LogEvent(LogEventType.All, command.rawCommand);
                }

                var args = command.rawCommand.Split(argsSplitChars, 2);
                command.command = args[0];
                if(args.Length > 1)
                {
                    command.args = args[1];
                }

                string result = String.Empty;

                result = SendCommand(command.rawCommand);

                var lines = result.Split(lineSplitChars, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();

                if (!command.suppressOutput)
                {
                    foreach (var line in lines)
                    {
                        LogEvent(LogEventType.All, line);
                    }
                }

                if(lines.Length == 1 && lines[0].StartsWith(NoResponseMatch))
                {
                    lines[0] = NoResponseOutput;
                }

                command.status = ConsoleStatus.Connected;
                command.lines = lines;

                this.outputProcessor.PostAction(() => ProcessOutput(command));
                return true;
            }
            catch(Exception ex)
            {
                _logger.Debug("Failed to send command '{0}'.  {1}\n{2}", command.rawCommand, ex.Message, ex.ToString());
                command.status = ConsoleStatus.Disconnected;
                this.outputProcessor.PostAction(() => ProcessOutput(command));
                return false;
            }            
        }

        const int MaxCommandRetries = 10;
        const int RetryDelay = 100;
        private string SendCommand(string command)
        {
            int retries = 0;
            Exception lastException = null;        
            while (retries < MaxCommandRetries)
            {
                if (this.console != null)
                {
                    try
                    {
                        var result = this.console.SendCommand(command);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        // Re will simply retry
                        lastException = ex;
                    }

                    Task.Delay(RetryDelay).Wait();
                }

                try
                {
                    Reconnect();
                }
                catch(Exception ex)
                {
                    lastException = ex;
                }

                retries++;
            }

            _logger.Debug("Failed to connect to RCON at {0}:{1} with {2}: {3}\n{4}",
                   this.snapshot.ServerIP,
                   this.snapshot.RCONPort,
                   this.snapshot.AdminPassword,
                   lastException.Message,
                   lastException.StackTrace);

            throw new Exception($"Command failed to send after {MaxCommandRetries} attempts.  Last exception: {lastException.Message}\n{lastException.StackTrace}", lastException);
        }

        private bool Reconnect()
        {
            if(this.console != null)
            {
                this.console.Dispose();
                this.console = null;
            }
          
            var endpoint = new IPEndPoint(IPAddress.Parse(this.snapshot.ServerIP), this.snapshot.RCONPort);    
            var server = QueryMaster.ServerQuery.GetServerInstance(QueryMaster.EngineType.Source, endpoint);
            this.console = server.GetControl(this.snapshot.AdminPassword);
            return true;
        }
    }
}

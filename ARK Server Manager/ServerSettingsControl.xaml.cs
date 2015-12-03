using ARK_Server_Manager.Lib;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for ServerSettings.xaml
    /// </summary>
    partial class ServerSettingsControl : UserControl
    {
        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register(nameof(Settings), typeof(ServerProfile), typeof(ServerSettingsControl));
        public static readonly DependencyProperty RuntimeProperty = 
            DependencyProperty.Register(nameof(Runtime), typeof(ServerRuntime), typeof(ServerSettingsControl));
        public static readonly DependencyProperty NetworkInterfacesProperty = 
            DependencyProperty.Register(nameof(NetworkInterfaces), typeof(List<NetworkAdapterEntry>), typeof(ServerSettingsControl), new PropertyMetadata(new List<NetworkAdapterEntry>()));
        public static readonly DependencyProperty ServerProperty =
            DependencyProperty.Register(nameof(Server), typeof(Server), typeof(ServerSettingsControl), new PropertyMetadata(null, ServerPropertyChanged));

        CancellationTokenSource upgradeCancellationSource;
        RCONWindow rconWindow;

        public ServerManager ServerManager
        {
            get { return (ServerManager)GetValue(ServerManagerProperty); }
            set { SetValue(ServerManagerProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ServerManager.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ServerManagerProperty =
            DependencyProperty.Register(nameof(ServerManager), typeof(ServerManager), typeof(ServerSettingsControl), new PropertyMetadata(null));



        public bool IsAdministrator
        {
            get { return (bool)GetValue(IsAdministratorProperty); }
            set { SetValue(IsAdministratorProperty, value); }
        }

        public static readonly DependencyProperty IsAdministratorProperty = DependencyProperty.Register(nameof(IsAdministrator), typeof(bool), typeof(ServerSettingsControl), new PropertyMetadata(false));



        public Server Server
        {
            get { return (Server)GetValue(ServerProperty); }
            set { SetValue(ServerProperty, value); }
        }

        private static void ServerPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ssc = (ServerSettingsControl)d;
            var oldserver = (Server)e.OldValue;
            var server = (Server)e.NewValue;
            if (server != null)
            {
                TaskUtils.RunOnUIThreadAsync(() =>
                    {
                        if(oldserver != null)
                        {
                            oldserver.Profile.Save();
                        }

                        ssc.Settings = server.Profile;
                        ssc.Runtime = server.Runtime;
                        ssc.ReinitializeNetworkAdapters();
                    }).DoNotWait();
            }
        }
        
        public ServerProfile Settings
        {
            get { return GetValue(SettingsProperty) as ServerProfile; }
            set { SetValue(SettingsProperty, value); }
        }

        public ServerRuntime Runtime
        {
            get { return GetValue(RuntimeProperty) as ServerRuntime; }
            set { SetValue(RuntimeProperty, value); }
        }

        public List<NetworkAdapterEntry> NetworkInterfaces
        {
            get { return (List<NetworkAdapterEntry>)GetValue(NetworkInterfacesProperty); }
            set { SetValue(NetworkInterfacesProperty, value); }
        }

        public ServerSettingsControl()
        {
            InitializeComponent();
            var dictToRemove = this.Resources.MergedDictionaries.FirstOrDefault(d => d.Source.OriginalString.Contains(@"Globalization\en-US\en-US.xaml"));
            if (dictToRemove != null)
            {
                this.Resources.MergedDictionaries.Remove(dictToRemove);
            }

            this.ServerManager = ServerManager.Instance;
            this.IsAdministrator = SecurityUtils.IsAdministrator();
        }

        private void ReinitializeNetworkAdapters()
        {
            var adapters = NetworkUtils.GetAvailableIPV4NetworkAdapters();

            //
            // Filter out self-assigned addresses
            //
            adapters.RemoveAll(a => a.IPAddress.StartsWith("169.254."));
            adapters.Insert(0, new NetworkAdapterEntry(String.Empty, "Let ARK choose"));
            var savedServerIp = this.Settings.ServerIP;
            this.NetworkInterfaces = adapters;
            this.Settings.ServerIP = savedServerIp;


            //
            // If there isn't already an adapter assigned, pick one
            //
            var preferredIP = NetworkUtils.GetPreferredIP(adapters);
            preferredIP.Description = "(Recommended) " + preferredIP.Description;
            if(String.IsNullOrWhiteSpace(this.Settings.ServerIP))
            {
                if(preferredIP != null)
                {
                    this.Settings.ServerIP = preferredIP.IPAddress;
                }
            } 
            else if(adapters.FirstOrDefault(a => String.Equals(a.IPAddress, this.Settings.ServerIP, StringComparison.OrdinalIgnoreCase)) == null) 
            {
                MessageBox.Show(
                    String.Format("Your Local IP address {0} is no longer available.  Please review the available IP addresses and select a valid one.  If you have a server running on the original IP, you will need to stop it first.", this.Settings.ServerIP),
                    "Local IP invalid",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }        

        private async void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            switch(this.Runtime.Status)
            {
                case ServerRuntime.ServerStatus.Stopped:
                case ServerRuntime.ServerStatus.Uninstalled:
                    break;

                case ServerRuntime.ServerStatus.Running:
                case ServerRuntime.ServerStatus.Initializing:
                    var result = MessageBox.Show("The server must be stopped to upgrade.  Do you wish to proceed?", "Server running", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if(result == MessageBoxResult.No)
                    {
                        return;
                    }

                    break;

                case ServerRuntime.ServerStatus.Updating:
                    upgradeCancellationSource.Cancel();
                    upgradeCancellationSource = null;
                    return;
            }

            this.upgradeCancellationSource = new CancellationTokenSource();
            await this.Server.UpgradeAsync(upgradeCancellationSource.Token, validate: true);            
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            switch(this.Runtime.Status)
            {
                case ServerRuntime.ServerStatus.Initializing:
                case ServerRuntime.ServerStatus.Running:
                    var result = MessageBox.Show("This will shut down the server.  Do you wish to proceed?", "Stop the server?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }

                    await this.Server.StopAsync();
                    break;

                case ServerRuntime.ServerStatus.Stopped:
                    this.Settings.Save();
                    await this.Server.StartAsync();
                    break;
            }
        }

        private void SelectInstallDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Title = "Select Install Directory";
            if(!String.IsNullOrWhiteSpace(Settings.InstallDirectory))
            {
                dialog.InitialDirectory = Settings.InstallDirectory;
            }

            var result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                Settings.InstallDirectory = dialog.FileName;
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.EnsureFileExists = true;
            dialog.Multiselect = false;
            dialog.Title = "Load Server Profile or GameUserSettings.ini";
            dialog.Filters.Add(new CommonFileDialogFilter("Profile", Config.Default.LoadProfileExtensionList));
            if(!Directory.Exists(Config.Default.ConfigDirectory))
            {
                System.IO.Directory.CreateDirectory(Config.Default.ConfigDirectory);
            }

            dialog.InitialDirectory = Config.Default.ConfigDirectory;
            var result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                try
                {
                    this.Server.ImportFromPath(dialog.FileName);
                    this.Settings = this.Server.Profile;
                    this.Runtime = this.Server.Runtime;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(String.Format("The profile at {0} failed to load.  The error was: {1}\r\n{2}", dialog.FileName, ex.Message, ex.StackTrace), "Profile failed to load", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }            
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.EnableAutoUpdate && !Updater.IsServerCacheAutoUpdateEnabled)
            {
                var result = MessageBox.Show("Auto-updates is enabled but the Server Cache update is not yet configured.  The server cache downloads server updates in the background automatically to enable faster server updates, particularly when there are multiple servers.  You must first configure the cache, then you may enable automatic updating.  Would you like to configure the cache now?", "Server cache not configured", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if(result == MessageBoxResult.Yes)
                {
                    var settingsWindow = new SettingsWindow();
                    settingsWindow.ShowDialog();
                    if(!Updater.IsServerCacheAutoUpdateEnabled)
                    {
                        MessageBox.Show("The server cache was not configured.  Disabling auto-updates.", "Server cache not configured", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Settings.EnableAutoUpdate = false;
                    }
                }
            }

            Settings.Save();
            if (this.IsAdministrator)
            {
                if (!Settings.UpdateAutoUpdateSettings())
                {
                    MessageBox.Show("Failed to update scheduled tasks.  Ensure you have administrator rights on this machine and try again.  If the problem persists, please report this as a bug.", "Update schedule failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }           
        }

        private void CopyProfile_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ShowCmd_Click(object sender, RoutedEventArgs e)
        {
            var cmdLine = new CommandLine(String.Format("{0} {1}", this.Runtime.GetServerExe(), this.Settings.GetServerArgs()));
            cmdLine.ShowDialog();
        }

        private void RemovePlayerLevel_Click(object sender, RoutedEventArgs e)
        {            
            if(this.Settings.PlayerLevels.Count == 1)
            {
                MessageBox.Show("You can't delete the last level.  If you want to disable the feature, uncheck Enable Custom Level Progressions.", "Can't delete last item", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            else
            {
                var level = ((Level)((Button)e.Source).DataContext);
                this.Settings.PlayerLevels.RemoveLevel(level);            
            }
        }

        private void AddPlayerLevel_Click(object sender, RoutedEventArgs e)
        {
            var level = ((Level)((Button)e.Source).DataContext);
            this.Settings.PlayerLevels.AddNewLevel(level);
        }

        private void RemoveDinoLevel_Click(object sender, RoutedEventArgs e)
        {
            if (this.Settings.DinoLevels.Count == 1)
            {
                MessageBox.Show("You can't delete the last level.  If you want to disable the feature, uncheck Enable Custom Level Progressions.", "Can't delete last item", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            else
            {
                var level = ((Level)((Button)e.Source).DataContext);
                this.Settings.DinoLevels.RemoveLevel(level);
            }
        }

        private void AddDinoLevel_Click(object sender, RoutedEventArgs e)
        {
            var level = ((Level)((Button)e.Source).DataContext);
            this.Settings.DinoLevels.AddNewLevel(level);
        }

        private void PlayerLevels_Recalculate(object sender, RoutedEventArgs e)
        {
            this.Settings.PlayerLevels.UpdateTotals();
            this.CustomPlayerLevelsView.Items.Refresh();
        }

        private void DinoLevels_Recalculate(object sender, RoutedEventArgs e)
        {
            this.Settings.DinoLevels.UpdateTotals();
            this.CustomDinoLevelsView.Items.Refresh();
        }

        private void RefreshLocalIPs_Click(object sender, RoutedEventArgs e)
        {
            ReinitializeNetworkAdapters();
        }

        private void DinoLevels_Clear(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to clear all the current dino levels.", "Confirm Clear Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ClearLevelProgression(ServerProfile.LevelProgression.Dino);
        }

        private void DinoLevels_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current dino levels.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetLevelProgressionToDefault(ServerProfile.LevelProgression.Dino);
        }

        private void DinoLevels_ResetOfficial(object sender, RoutedEventArgs e)
        {  
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current dino levels.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)  
                return;  
  
            this.Settings.ResetLevelProgressionToOfficial(ServerProfile.LevelProgression.Dino);  
  }

        private void PlayerLevels_Clear(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to clear all the current player levels.", "Confirm Clear Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ClearLevelProgression(ServerProfile.LevelProgression.Player);
        }

        private void PlayerLevels_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current player levels.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetLevelProgressionToDefault(ServerProfile.LevelProgression.Player);
        }

        private void PlayerLevels_ResetOfficial(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current player levels.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetLevelProgressionToOfficial(ServerProfile.LevelProgression.Player);
        }

        private void DinoSpawn_Reset(object sender, RoutedEventArgs e)
        {
            this.Settings.DinoSpawnWeightMultipliers.Reset();
        }

        private void TamedDinoClassDamageMultipliers_Reset(object sender, RoutedEventArgs e)
        {
            this.Settings.TamedDinoClassDamageMultipliers.Reset();
        }

        private void TamedDinoClassResistanceMultipliers_Reset(object sender, RoutedEventArgs e)
        {
            this.Settings.TamedDinoClassResistanceMultipliers.Reset();
        }

        private void DinoClassDamageMultipliers_Reset(object sender, RoutedEventArgs e)
        {
            this.Settings.DinoClassDamageMultipliers.Reset();
        }

        private void DinoClassResistanceMultipliers_Reset(object sender, RoutedEventArgs e)
        {
            this.Settings.DinoClassResistanceMultipliers.Reset();
        }

        private void HarvestResourceItemAmountClassMultipliers_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current resource harvest amount multiplier changes.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.HarvestResourceItemAmountClassMultipliers.Reset();
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await ServerManager.Instance.CheckForUpdatesAsync();
        }

        private void OpenRCON_Click(object sender, RoutedEventArgs e)
        {
            var window = RCONWindow.GetRCONForServer(this.Server);
            window.Show();
            if(window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Focus();
        }

        private void Engrams_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current engram changes.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.OverrideNamedEngramEntries.Reset();
        }

        private void HelpSOTF_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Survival of the Fittest is a total conversion mod.  In order to enable it, you will need to first install it (we don't yet support installing it for you.)  Would you like to open the installation instructions web page now?", "Go to SOTF web page?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if(result == MessageBoxResult.Yes)
            {
                Process.Start("http://steamcommunity.com/app/346110/discussions/10/530649887204866610/");
            }
        }

        private void TestUpdater_Click(object sender, RoutedEventArgs e)
        {
            if(!this.Settings.UpdateAutoUpdateSettings())
            {
                
            }
        }

        private void NeedAdmin_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Automatic Management features of the Server Manager use administrator features of Windows to schedule tasks that will run even if the ASM is not running, without installing any separate processes or services.  To do this, the Server Manager must run with administrator privileges.  Restart the Server Manager and 'Run As Administrator' and you will be able to utilize these features.", "Needs Administrator Access", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DinoCustomization_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset all the current dino customizations.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.DinoSettings.Reset();
        }

        private void MaxXPPlayer_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset the Max XP.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetOverrideMaxExperiencePointsPlayer();
        }

        private void MaxXPDino_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset the Max XP.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetOverrideMaxExperiencePointsDino();
        }

        private void ChatAndNotification_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset the Chat and Notification section.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetChatAndNotificationSection();
        }

        private void HUDAndVisuals_Reset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Click 'Yes' to confirm you want to reset the HUD and Visuals section.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            this.Settings.ResetHUDAndVisualsSection();
        }
    }
}

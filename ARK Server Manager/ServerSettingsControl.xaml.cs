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
using ARK_Server_Manager.Lib.ViewModel;

namespace ARK_Server_Manager
{
    public enum ServerSettingsResetAction
    {
        // Sections
        RulesSection,
        ChatAndNotificationsSection,
        HudAndVisualsSection,
        PlayerSettingsSection,
        DinoSettingsSection,
        EnvironmentSection,
        StructuresSection,
        EngramsSection,
        CustomLevelsSection,
        SOTFSection,

        // Properties
        PlayerMaxXpProperty,
        DinoMaxXpProperty,
        PlayerPerLevelStatMultipliers,
        DinoWildPerLevelStatMultipliers,
        DinoTamedPerLevelStatMultipliers,
        DinoTamedAddPerLevelStatMultipliers,
        DinoTamedAffinityPerLevelStatMultipliers,
    }
    public enum UpdateAction
    {
        Server,
        Mods,
        Maps,
    }

    /// <summary>
    /// Interaction logic for ServerSettings.xaml
    /// </summary>
    partial class ServerSettingsControl : UserControl
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(ServerProfile), typeof(ServerSettingsControl));
        public static readonly DependencyProperty RuntimeProperty = DependencyProperty.Register(nameof(Runtime), typeof(ServerRuntime), typeof(ServerSettingsControl));
        public static readonly DependencyProperty NetworkInterfacesProperty = DependencyProperty.Register(nameof(NetworkInterfaces), typeof(List<NetworkAdapterEntry>), typeof(ServerSettingsControl), new PropertyMetadata(new List<NetworkAdapterEntry>()));
        public static readonly DependencyProperty ServerProperty = DependencyProperty.Register(nameof(Server), typeof(Server), typeof(ServerSettingsControl), new PropertyMetadata(null, ServerPropertyChanged));

        CancellationTokenSource upgradeCancellationSource;

        public ServerManager ServerManager
        {
            get { return (ServerManager)GetValue(ServerManagerProperty); }
            set { SetValue(ServerManagerProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ServerManager.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ServerManagerProperty = DependencyProperty.Register(nameof(ServerManager), typeof(ServerManager), typeof(ServerSettingsControl), new PropertyMetadata(null));



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

                    // check if the maps and mods are valid
                    var checkResult = await this.Server.CheckServerModsAsync();
                    if (!String.IsNullOrWhiteSpace(checkResult))
                    {
                        // mods are not considered valid, prompt user to continue.
                        if (MessageBox.Show($"{checkResult}\n\nDo you want to continue starting the server?", "Mod Check Failed", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }

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

        // REVIEW: This is a sample Command implementation which replaces the original Save_Click command, for reference when refactoring.
        public ICommand SaveCommand
        {
            get
            {
                return new RelayCommand<object>(
                    execute: (parameter) =>
                    {
                        // NOTE: This parameter is of type object and must be cast in most cases before use.
                        var settings = (Server)parameter;
                        if (settings.Profile.EnableAutoUpdate && !Updater.IsServerCacheAutoUpdateEnabled)
                        {
                            var result = MessageBox.Show("Auto-updates is enabled but the Server Cache update is not yet configured.  The server cache downloads server updates in the background automatically to enable faster server updates, particularly when there are multiple servers.  You must first configure the cache, then you may enable automatic updating.  Would you like to configure the cache now?", "Server cache not configured", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result == MessageBoxResult.Yes)
                            {
                                var settingsWindow = new SettingsWindow();
                                settingsWindow.ShowDialog();
                                if (!Updater.IsServerCacheAutoUpdateEnabled)
                                {
                                    MessageBox.Show("The server cache was not configured.  Disabling auto-updates.", "Server cache not configured", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    settings.Profile.EnableAutoUpdate = false;
                                }
                            }
                        }

                        settings.Profile.Save();

                        // NOTE: Ideally a command would not depend on this control object, so IsAdministrator would need to be some globally accessible value, much like Updater's properties are.  Then
                        //       command's implementation becomes context-free and we can move its implementation to a separate class of commands, and bind it in the Xaml using a StaticResource.
                        if (this.IsAdministrator)
                        {
                            if (!settings.Profile.UpdateAutoUpdateSettings())
                            {
                                MessageBox.Show("Failed to update scheduled tasks.  Ensure you have administrator rights on this machine and try again.  If the problem persists, please report this as a bug.", "Update schedule failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    },
                    canExecute: (parameter) =>
                    {
                        bool canSave = true;

                        // NOTE: Some logic if necessary.  If this return's false, the associated object to which this command is bound (like the Save button in this case) will be automatically disabled,
                        // eliminating any extra Xaml binding for the IsEnabled property.
                        return canSave;
                    }
                );
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
            cmdLine.Owner = Window.GetWindow(this);
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

        public ICommand ResetActionCommand
        {
            get
            {
                return new RelayCommand<ServerSettingsResetAction>(
                    execute: (action) =>
                    {
                        if (MessageBox.Show("Click 'Yes' to confirm you want to perform the reset.", "Confirm Reset Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;

                        switch (action)
                        {
                            // sections
                            case ServerSettingsResetAction.ChatAndNotificationsSection:
                                this.Settings.ResetChatAndNotificationSection();
                                break;

                            case ServerSettingsResetAction.CustomLevelsSection:
                                this.Settings.ResetCustomLevelsSection();
                                break;

                            case ServerSettingsResetAction.DinoSettingsSection:
                                this.Settings.ResetDinoSettings();
                                break;

                            case ServerSettingsResetAction.EngramsSection:
                                this.Settings.ResetEngramsSection();
                                break;

                            case ServerSettingsResetAction.EnvironmentSection:
                                this.Settings.ResetEnvironmentSection();
                                break;

                            case ServerSettingsResetAction.HudAndVisualsSection:
                                this.Settings.ResetHUDAndVisualsSection();
                                break;

                            case ServerSettingsResetAction.PlayerSettingsSection:
                                this.Settings.ResetPlayerSettings();
                                break;

                            case ServerSettingsResetAction.RulesSection:
                                this.Settings.ResetRulesSection();
                                break;

                            case ServerSettingsResetAction.SOTFSection:
                                this.Settings.ResetSOTFSection();
                                break;

                            case ServerSettingsResetAction.StructuresSection:
                                this.Settings.ResetStructuresSection();
                                break;

                            // Properties
                            case ServerSettingsResetAction.PlayerMaxXpProperty:
                                this.Settings.ResetOverrideMaxExperiencePointsPlayer();
                                break;

                            case ServerSettingsResetAction.DinoMaxXpProperty:
                                this.Settings.ResetOverrideMaxExperiencePointsDino();
                                break;

                            case ServerSettingsResetAction.PlayerPerLevelStatMultipliers:
                                this.Settings.PerLevelStatsMultiplier_Player.Reset();
                                break;

                            case ServerSettingsResetAction.DinoWildPerLevelStatMultipliers:
                                this.Settings.PerLevelStatsMultiplier_DinoWild.Reset();
                                break;

                            case ServerSettingsResetAction.DinoTamedPerLevelStatMultipliers:
                                this.Settings.PerLevelStatsMultiplier_DinoTamed.Reset();
                                break;

                            case ServerSettingsResetAction.DinoTamedAddPerLevelStatMultipliers:
                                this.Settings.PerLevelStatsMultiplier_DinoTamed_Add.Reset();
                                break;

                            case ServerSettingsResetAction.DinoTamedAffinityPerLevelStatMultipliers:
                                this.Settings.PerLevelStatsMultiplier_DinoTamed_Affinity.Reset();
                                break;
                        }
                    },
                    canExecute: (action) => true
                );
            }
        }

        public ICommand UpdateActionCommand
        {
            get
            {
                return new RelayCommand<UpdateAction>(
                    execute: async (action) =>
                    {
                        switch (this.Runtime.Status)
                        {
                            case ServerRuntime.ServerStatus.Stopped:
                            case ServerRuntime.ServerStatus.Uninstalled:
                                break;

                            case ServerRuntime.ServerStatus.Running:
                            case ServerRuntime.ServerStatus.Initializing:
                                var result = MessageBox.Show("The server must be stopped to perform an update. Do you wish to proceed?", "Server running", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                                if (result == MessageBoxResult.No)
                                {
                                    return;
                                }

                                break;

                            case ServerRuntime.ServerStatus.Updating:
                                if (upgradeCancellationSource != null)
                                    upgradeCancellationSource.Cancel();
                                upgradeCancellationSource = null;
                                return;
                        }

                        this.upgradeCancellationSource = new CancellationTokenSource();
                        ServerRuntime.UpdateResult updateResult;

                        switch (action)
                        {
                            // sections
                            case UpdateAction.Server:
                                if (MessageBox.Show("Click 'Yes' to confirm you want to perform the update.", "Confirm Update Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                                    return;

                                bool updateMods = Config.Default.UpdateModsWhenUpdatingServer;
                                updateResult = await this.Server.UpgradeAsync(upgradeCancellationSource.Token, validate: true, updateMods: updateMods);

                                if (!updateResult.ServerUpdated)
                                    MessageBox.Show("The update process was not successful, the server was not updated. If this is the first time, please try the update again. If you are unable to successfully update the server then please log a bug.", "Update Server Failed");
                                else if (!updateResult.ModsUpdated && updateMods)
                                    MessageBox.Show("The update process was not successful, one or more mods were not updated. If this is the first time, please try again. If you are unable to successfully update the mods then please log a bug.", "Update Mods Failed");
                                break;

                            case UpdateAction.Mods:
                                if (MessageBox.Show("Click 'Yes' to confirm you want to perform the update.", "Confirm Update Action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                                    return;

                                updateResult = await this.Server.UpgradeModsAsync(upgradeCancellationSource.Token);

                                if (!updateResult.ModsUpdated)
                                    MessageBox.Show("The update process was not successful, one or more mods were not updated. If this is the first time, please try again. If you are unable to successfully update the mods then please log a bug.", "Update Mods Failed");
                                break;

                            case UpdateAction.Maps:
                                var serverMap = await this.Server.GetServerMapAsync(upgradeCancellationSource.Token);

                                if (serverMap == null)
                                    MessageBox.Show("The fetch process was not successful, one or more maps could not be fetched. If this is the first time, please try again. If you are unable to successfully fetch the maps then please log a bug.", "Fetch Maps Failed");
                                else
                                    Settings.ServerMap = serverMap;
                                break;
                        }
                    },
                    canExecute: (action) => true
                );
            }
        }

        public ICommand MapSourceTypeSelectionCommand
        {
            get
            {
                return new RelayCommand<MapSourceType>(
                    execute: (mapType) =>
                    {
                        switch (mapType)
                        {
                            case MapSourceType.Default:
                                this.Settings.ResetServerMap();
                                break;

                            case MapSourceType.Custom:
                                this.Settings.ClearTotalConversion();
                                break;

                            case MapSourceType.TotalConversion:
                                this.Settings.ClearCustomMap();
                                break;
                        }
                    },
                    canExecute: (mapType) => true
                );
            }
        }
    }
}

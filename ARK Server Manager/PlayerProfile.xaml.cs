using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using ARK_Server_Manager.Lib.ViewModel.RCON;
using ARK_Server_Manager.Properties;
using ArkData;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for PlayerProfile.xaml
    /// </summary>
    public partial class PlayerProfile : Window
    {
        public PlayerProfile(PlayerInfo player, String serverFolder)
        {
            InitializeComponent();

            this.Player = player;
            this.ServerFolder = serverFolder;
            this.DataContext = this;
        }

        public PlayerInfo Player
        {
            get;
            private set;
        }

        public Player ArkDataPlayer
        {
            get
            {
                return Player?.ArkData;
            }
        }

        public Tribe ArkDataTribe
        {
            get
            {
                return Player?.ArkData?.Tribe;
            }
        }

        public String CreatedDate
        {
            get
            {
                return ArkDataPlayer?.FileCreated.ToString("G");
            }
        }

        public Boolean IsTribeOwner
        {
            get
            {
                return ArkDataPlayer != null && ArkDataTribe != null && ArkDataTribe.OwnerId == ArkDataPlayer.Id;
            }
        }

        public String PlayerLink
        {
            get
            {
                if (String.IsNullOrWhiteSpace(ServerFolder))
                    return null;

                return String.Format("/select, {0}", Path.Combine(ServerFolder, Config.Default.SavedArksRelativePath, String.Format("{0}.arkprofile", Player.SteamId.ToString())));
            }
        }

        public String ProfileUrl
        {
            get
            {
                return ArkDataPlayer?.ProfileUrl;
            }
        }

        public String ServerFolder
        {
            get;
            private set;
        }

        public String TribeLink
        {
            get
            {
                if (String.IsNullOrWhiteSpace(ServerFolder))
                    return null;
                
                return ArkDataTribe == null ? null : String.Format("/select, {0}", Path.Combine(ServerFolder, Config.Default.SavedArksRelativePath, String.Format("{0}.arktribe", ArkDataTribe.Id.ToString())));
            }
        }

        public String TribeOwner
        {
            get
            {
                return ArkDataTribe != null && ArkDataTribe.Owner != null ? string.Format("{0} ({1})", ArkDataTribe.Owner.SteamName, ArkDataTribe.Owner.CharacterName) : null;
            }
        }

        public String UpdatedDate
        {
            get
            {
                return ArkDataPlayer?.FileUpdated.ToString("G");
            }
        }

        public String WindowTitle
        {
            get
            {
                return String.Format("Player Profile - {0}", Player.SteamName);
            }
        }

        private void SteamName_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (sender.GetType() != typeof(Hyperlink)) return;

            string link = ((Hyperlink)sender).NavigateUri.ToString();
            if (String.IsNullOrWhiteSpace(link)) return;

            Process.Start(link);
            e.Handled = true;
        }

        private void PlayerId_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (sender.GetType() != typeof(Hyperlink)) return;

            string link = ((Hyperlink)sender).NavigateUri.ToString();
            if (String.IsNullOrWhiteSpace(link)) return;

            Process.Start("explorer.exe", link);
            e.Handled = true;
        }

        private void TribeId_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (sender.GetType() != typeof(Hyperlink)) return;

            string link = ((Hyperlink)sender).NavigateUri.ToString();
            if (String.IsNullOrWhiteSpace(link)) return;

            Process.Start("explorer.exe", link);
            e.Handled = true;
        }
    }
}

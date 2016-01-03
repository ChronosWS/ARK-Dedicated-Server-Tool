using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using ARK_Server_Manager.Lib.ViewModel.RCON;
using ArkData;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for PlayerProfile.xaml
    /// </summary>
    public partial class PlayerProfile : Window
    {
        public PlayerProfile(PlayerInfo player)
        {
            InitializeComponent();

            this.Player = player;
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

        public String ProfileUrl
        {
            get
            {
                return ArkDataPlayer?.ProfileUrl;
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
    }
}

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
    /// Interaction logic for TribeProfile.xaml
    /// </summary>
    public partial class TribeProfile : Window
    {
        public TribeProfile(PlayerInfo player)
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
                return ArkDataTribe?.FileCreated.ToString("G");
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
                return ArkDataTribe?.FileUpdated.ToString("G");
            }
        }

        public String WindowTitle
        {
            get
            {
                return String.Format("Tribe Profile - {0}", Player.TribeName);
            }
        }
    }
}

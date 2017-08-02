﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ARK_Server_Manager.Lib;
using ARK_Server_Manager.Lib.ViewModel;
using ARK_Server_Manager.Lib.ViewModel.RCON;
using ArkData;
using WPFSharp.Globalizer;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for TribeProfileWindow.xaml
    /// </summary>
    public partial class TribeProfileWindow : Window
    {
        private readonly GlobalizedApplication _globalizer = GlobalizedApplication.Instance;

        public TribeProfileWindow(PlayerInfo player, ICollection<PlayerInfo> players, String serverFolder)
        {
            InitializeComponent();
            WindowUtils.RemoveDefaultResourceDictionary(this);

            this.Player = player;
            this.Players = players;
            this.ServerFolder = serverFolder;
            this.DataContext = this;
        }

        public PlayerInfo Player
        {
            get;
            private set;
        }

        public ICollection<PlayerInfo> Players
        {
            get;
            private set;
        }

        public String ServerFolder
        {
            get;
            private set;
        }

        public Player ArkDataPlayer => Player?.ArkData;

        public Tribe ArkDataTribe => Player?.ArkData?.Tribe;

        public String CreatedDate => ArkDataTribe?.FileCreated.ToString("G");

        public String TribeLink => String.IsNullOrWhiteSpace(ServerFolder) || ArkDataTribe == null ? null : $"/select, {Path.Combine(ServerFolder, $"{ArkDataTribe.Id}{Config.Default.TribeFileExtension}")}";

        public String TribeOwner => ArkDataTribe != null && ArkDataTribe.Owner != null ? string.Format("{0} ({1})", ArkDataTribe.Owner.SteamName, ArkDataTribe.Owner.CharacterName) : null;

        public ICollection<PlayerInfo> TribePlayers
        {
            get
            {
                if (ArkDataTribe == null) return null;

                ICollection<PlayerInfo> players = new List<PlayerInfo>();
                foreach (var tribePlayer in ArkDataTribe.Players)
                {
                    var player = Players.FirstOrDefault(p => p.SteamId.ToString() == tribePlayer.SteamId);
                    if (player != null)
                        players.Add(player);
                }
                return players;
            }
        }

        public String UpdatedDate => ArkDataTribe?.FileUpdated.ToString("G");

        public String WindowTitle => String.Format(_globalizer.GetResourceString("Profile_WindowTitle_Tribe"), Player.TribeName);

        public ICommand ExplorerLinkCommand
        {
            get
            {
                return new RelayCommand<String>(
                    execute: (action) =>
                    {
                        if (String.IsNullOrWhiteSpace(action)) return;
                        Process.Start("explorer.exe", action);
                    },
                    canExecute: (action) => true
                );
            }
        }
    }
}

using ARK_Server_Manager.Lib.Model;
using ARK_Server_Manager.Lib.Utils;
using ARK_Server_Manager.Lib.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using TinyCsvParser;

namespace ARK_Server_Manager.Lib
{
    [XmlRoot("ArkServerProfile")]
    [DataContract]
    public class ServerProfile : DependencyObject
    {
        public enum ServerProfileSection
        {
            AdministrationSection,
            AutomaticManagement,
            RulesSection,
            ChatAndNotificationsSection,
            HudAndVisualsSection,
            PlayerSettingsSection,
            DinoSettingsSection,
            EnvironmentSection,
            StructuresSection,
            EngramsSection,
            CustomSettingsSection,
            CustomLevelsSection,
            MapSpawnerOverridesSection,
            CraftingOverridesSection,
            SupplyCrateOverridesSection,
            PGMSection,
            SOTFSection,
        }

        public enum LevelProgression
        {
            Player,
            Dino
        };

        private const char CSV_DELIMITER = ';';

        [XmlIgnore]
        private string _lastSaveLocation = String.Empty;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private ServerProfile()
        {
            ServerPassword = SecurityUtils.GeneratePassword(16);
            AdminPassword = SecurityUtils.GeneratePassword(16);

            this.DinoSpawnWeightMultipliers = new AggregateIniValueList<DinoSpawn>(nameof(DinoSpawnWeightMultipliers), GameData.GetDinoSpawns);
            this.PreventDinoTameClassNames = new StringIniValueList(nameof(PreventDinoTameClassNames), () => new string[0] );
            this.NPCReplacements = new AggregateIniValueList<NPCReplacement>(nameof(NPCReplacements), GameData.GetNPCReplacements);
            this.TamedDinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.TamedDinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);

            this.HarvestResourceItemAmountClassMultipliers = new AggregateIniValueList<ResourceClassMultiplier>(nameof(HarvestResourceItemAmountClassMultipliers), GameData.GetStandardResourceMultipliers);
            this.OverrideNamedEngramEntries = new EngramEntryList<EngramEntry>(nameof(OverrideNamedEngramEntries), GameData.GetStandardEngramOverrides);

            this.DinoLevels = new LevelList();
            this.PlayerLevels = new LevelList();
            this.PlayerBaseStatMultipliers = new StatsMultiplierArray(nameof(PlayerBaseStatMultipliers), GameData.GetBaseStatMultipliers_Player, GameData.GetStatMultiplierInclusions_PlayerBase());
            this.PerLevelStatsMultiplier_Player = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_Player), GameData.GetPerLevelStatsMultipliers_Player, GameData.GetStatMultiplierInclusions_PlayerPerLevel());
            this.PerLevelStatsMultiplier_DinoWild = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_DinoWild, GameData.GetStatMultiplierInclusions_DinoWildPerLevel());
            this.PerLevelStatsMultiplier_DinoTamed = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed, GameData.GetStatMultiplierInclusions_DinoTamedPerLevel());
            this.PerLevelStatsMultiplier_DinoTamed_Add = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamedAdd, GameData.GetStatMultiplierInclusions_DinoTamedAdd());
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamedAffinity, GameData.GetStatMultiplierInclusions_DinoTamedAffinity());

            this.ConfigOverrideItemCraftingCosts = new AggregateIniValueList<CraftingOverride>(nameof(ConfigOverrideItemCraftingCosts), null);

            this.CustomGameUserSettingsSections = new CustomSectionList();

            this.PGM_Terrain = new PGMTerrain();

            this.ConfigAddNPCSpawnEntriesContainer = new NPCSpawnContainerList<NPCSpawnContainer>(nameof(ConfigAddNPCSpawnEntriesContainer), NPCSpawnContainerType.Add);
            this.ConfigSubtractNPCSpawnEntriesContainer = new NPCSpawnContainerList<NPCSpawnContainer>(nameof(ConfigSubtractNPCSpawnEntriesContainer), NPCSpawnContainerType.Subtract);
            this.ConfigOverrideNPCSpawnEntriesContainer = new NPCSpawnContainerList<NPCSpawnContainer>(nameof(ConfigOverrideNPCSpawnEntriesContainer), NPCSpawnContainerType.Override);
            this.NPCSpawnSettings = new NPCSpawnSettingsList(this.ConfigAddNPCSpawnEntriesContainer, this.ConfigSubtractNPCSpawnEntriesContainer, this.ConfigOverrideNPCSpawnEntriesContainer);

            this.ConfigOverrideSupplyCrateItems = new SupplyCrateOverrideList(nameof(ConfigOverrideSupplyCrateItems));

            GetDefaultDirectories();
        }

        #region Properties
        public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.Register(nameof(IsDirty), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [XmlIgnore]
        public bool IsDirty
        {
            get { return (bool)GetValue(IsDirtyProperty); }
            set { SetValue(IsDirtyProperty, value); }
        }

        public static readonly DependencyProperty ProfileIDProperty = DependencyProperty.Register(nameof(ProfileID), typeof(string), typeof(ServerProfile), new PropertyMetadata(Guid.NewGuid().ToString()));
        [DataMember]
        public string ProfileID
        {
            get { return (string)GetValue(ProfileIDProperty); }
            set { SetValue(ProfileIDProperty, value); }
        }

        public static readonly DependencyProperty ProfileNameProperty = DependencyProperty.Register(nameof(ProfileName), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerProfileName));
        [DataMember]
        public string ProfileName
        {
            get { return (string)GetValue(ProfileNameProperty); }
            set { SetValue(ProfileNameProperty, value); }
        }

        public static readonly DependencyProperty InstallDirectoryProperty = DependencyProperty.Register(nameof(InstallDirectory), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string InstallDirectory
        {
            get { return (string)GetValue(InstallDirectoryProperty); }
            set { SetValue(InstallDirectoryProperty, value); }
        }

        public static readonly DependencyProperty LastInstalledVersionProperty = DependencyProperty.Register(nameof(LastInstalledVersion), typeof(string), typeof(ServerProfile), new PropertyMetadata(new Version(0, 0).ToString()));
        [DataMember]
        public string LastInstalledVersion
        {
            get { return (string)GetValue(LastInstalledVersionProperty); }
            set { SetValue(LastInstalledVersionProperty, value); }
        }

        #region Administration
        public static readonly DependencyProperty ServerNameProperty = DependencyProperty.Register(nameof(ServerName), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerName));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "SessionName")]
        public string ServerName
        {
            get { return (string)GetValue(ServerNameProperty); }
            set
            {
                SetValue(ServerNameProperty, value);
                ValidateServerName();
            }
        }

        public static readonly DependencyProperty ServerNameLengthProperty = DependencyProperty.Register(nameof(ServerNameLength), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [XmlIgnore]
        public int ServerNameLength
        {
            get { return (int)GetValue(ServerNameLengthProperty); }
            set { SetValue(ServerNameLengthProperty, value); }
        }

        public static readonly DependencyProperty ServerNameLengthToLongProperty = DependencyProperty.Register(nameof(ServerNameLengthToLong), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [XmlIgnore]
        public bool ServerNameLengthToLong
        {
            get { return (bool)GetValue(ServerNameLengthToLongProperty); }
            set { SetValue(ServerNameLengthToLongProperty, value); }
        }

        public static readonly DependencyProperty ServerPasswordProperty = DependencyProperty.Register(nameof(ServerPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerPassword")]
        public string ServerPassword
        {
            get { return (string)GetValue(ServerPasswordProperty); }
            set { SetValue(ServerPasswordProperty, value); }
        }

        public static readonly DependencyProperty AdminPasswordProperty = DependencyProperty.Register(nameof(AdminPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerAdminPassword")]
        public string AdminPassword
        {
            get { return (string)GetValue(AdminPasswordProperty); }
            set { SetValue(AdminPasswordProperty, value); }
        }

        public static readonly DependencyProperty SpectatorPasswordProperty = DependencyProperty.Register(nameof(SpectatorPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public string SpectatorPassword
        {
            get { return (string)GetValue(SpectatorPasswordProperty); }
            set { SetValue(SpectatorPasswordProperty, value); }
        }

        public static readonly DependencyProperty ServerConnectionPortProperty = DependencyProperty.Register(nameof(ServerConnectionPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(7777));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "Port")]
        public int ServerConnectionPort
        {
            get { return (int)GetValue(ServerConnectionPortProperty); }
            set { SetValue(ServerConnectionPortProperty, value); }
        }

        public static readonly DependencyProperty ServerPortProperty = DependencyProperty.Register(nameof(ServerPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(27015));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "QueryPort")]
        public int ServerPort
        {
            get { return (int)GetValue(ServerPortProperty); }
            set { SetValue(ServerPortProperty, value); }
        }

        public static readonly DependencyProperty ServerIPProperty = DependencyProperty.Register(nameof(ServerIP), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "MultiHome")]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MultiHome, "MultiHome", WriteBoolValueIfNonEmpty = true)]
        public string ServerIP
        {
            get { return (string)GetValue(ServerIPProperty); }
            set { SetValue(ServerIPProperty, value); }
        }

        public static readonly DependencyProperty UseRawSocketsProperty = DependencyProperty.Register(nameof(UseRawSockets), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseRawSockets
        {
            get { return (bool)GetValue(UseRawSocketsProperty); }
            set { SetValue(UseRawSocketsProperty, value); }
        }

        public static readonly DependencyProperty NoNetThreadingProperty = DependencyProperty.Register(nameof(NoNetThreading), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool NoNetThreading
        {
            get { return (bool)GetValue(NoNetThreadingProperty); }
            set { SetValue(NoNetThreadingProperty, value); }
        }

        public static readonly DependencyProperty ForceNetThreadingProperty = DependencyProperty.Register(nameof(ForceNetThreading), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceNetThreading
        {
            get { return (bool)GetValue(ForceNetThreadingProperty); }
            set { SetValue(ForceNetThreadingProperty, value); }
        }

        public static readonly DependencyProperty EnableBanListURLProperty = DependencyProperty.Register(nameof(EnableBanListURL), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableBanListURL
        {
            get { return (bool)GetValue(EnableBanListURLProperty); }
            set { SetValue(EnableBanListURLProperty, value); }
        }

        public static readonly DependencyProperty BanListURLProperty = DependencyProperty.Register(nameof(BanListURL), typeof(string), typeof(ServerProfile), new PropertyMetadata("http://arkdedicated.com/banlist.txt"));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableBanListURL), QuotedString = QuotedStringType.True)]
        public string BanListURL
        {
            get { return (string)GetValue(BanListURLProperty); }
            set { SetValue(BanListURLProperty, value); }
        }

        public static readonly DependencyProperty MaxPlayersProperty = DependencyProperty.Register(nameof(MaxPlayers), typeof(int), typeof(ServerProfile), new PropertyMetadata(70));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.GameSession, "MaxPlayers")]
        public int MaxPlayers
        {
            get { return (int)GetValue(MaxPlayersProperty); }
            set { SetValue(MaxPlayersProperty, value); }
        }

        public static readonly DependencyProperty EnableKickIdlePlayersProperty = DependencyProperty.Register(nameof(EnableKickIdlePlayers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableKickIdlePlayers
        {
            get { return (bool)GetValue(EnableKickIdlePlayersProperty); }
            set { SetValue(EnableKickIdlePlayersProperty, value); }
        }

        public static readonly DependencyProperty KickIdlePlayersPeriodProperty = DependencyProperty.Register(nameof(KickIdlePlayersPeriod), typeof(int), typeof(ServerProfile), new PropertyMetadata(3600));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableKickIdlePlayers))]
        public int KickIdlePlayersPeriod
        {
            get { return (int)GetValue(KickIdlePlayersPeriodProperty); }
            set { SetValue(KickIdlePlayersPeriodProperty, value); }
        }

        public static readonly DependencyProperty RCONEnabledProperty = DependencyProperty.Register(nameof(RCONEnabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool RCONEnabled
        {
            get { return (bool)GetValue(RCONEnabledProperty); }
            set { SetValue(RCONEnabledProperty, value); }
        }

        public static readonly DependencyProperty RCONPortProperty = DependencyProperty.Register(nameof(RCONPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(32330));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int RCONPort
        {
            get { return (int)GetValue(RCONPortProperty); }
            set { SetValue(RCONPortProperty, value); }
        }

        public static readonly DependencyProperty RCONServerGameLogBufferProperty = DependencyProperty.Register(nameof(RCONServerGameLogBuffer), typeof(int), typeof(ServerProfile), new PropertyMetadata(600));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int RCONServerGameLogBuffer
        {
            get { return (int)GetValue(RCONServerGameLogBufferProperty); }
            set { SetValue(RCONServerGameLogBufferProperty, value); }
        }

        public static readonly DependencyProperty AdminLoggingProperty = DependencyProperty.Register(nameof(AdminLogging), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AdminLogging
        {
            get { return (bool)GetValue(AdminLoggingProperty); }
            set { SetValue(AdminLoggingProperty, value); }
        }

        public static readonly DependencyProperty ServerMapProperty = DependencyProperty.Register(nameof(ServerMap), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerMap));
        [DataMember]
        public string ServerMap
        {
            get { return (string)GetValue(ServerMapProperty); }
            set { SetValue(ServerMapProperty, value); }
        }

        public static readonly DependencyProperty TotalConversionModIdProperty = DependencyProperty.Register(nameof(TotalConversionModId), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string TotalConversionModId
        {
            get { return (string)GetValue(TotalConversionModIdProperty); }
            set { SetValue(TotalConversionModIdProperty, value); }
        }

        public static readonly DependencyProperty ServerModIdsProperty = DependencyProperty.Register(nameof(ServerModIds), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "ActiveMods")]
        public string ServerModIds
        {
            get { return (string)GetValue(ServerModIdsProperty); }
            set { SetValue(ServerModIdsProperty, value); }
        }

        public static readonly DependencyProperty EnableExtinctionEventProperty = DependencyProperty.Register(nameof(EnableExtinctionEvent), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableExtinctionEvent
        {
            get { return (bool)GetValue(EnableExtinctionEventProperty); }
            set { SetValue(EnableExtinctionEventProperty, value); }
        }

        public static readonly DependencyProperty ExtinctionEventTimeIntervalProperty = DependencyProperty.Register(nameof(ExtinctionEventTimeInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(2592000));
        [XmlIgnore]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableExtinctionEvent))]
        public int ExtinctionEventTimeInterval
        {
            get { return (int)GetValue(ExtinctionEventTimeIntervalProperty); }
            set { SetValue(ExtinctionEventTimeIntervalProperty, value); }
        }

        public static readonly DependencyProperty ExtinctionEventUTCProperty = DependencyProperty.Register(nameof(ExtinctionEventUTC), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "NextExtinctionEventUTC", ClearWhenOff = nameof(EnableExtinctionEvent))]
        public int ExtinctionEventUTC
        {
            get { return (int)GetValue(ExtinctionEventUTCProperty); }
            set { SetValue(ExtinctionEventUTCProperty, value); }
        }

        public static readonly DependencyProperty AutoSavePeriodMinutesProperty = DependencyProperty.Register(nameof(AutoSavePeriodMinutes), typeof(float), typeof(ServerProfile), new PropertyMetadata(15.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float AutoSavePeriodMinutes
        {
            get { return (float)GetValue(AutoSavePeriodMinutesProperty); }
            set { SetValue(AutoSavePeriodMinutesProperty, value); }
        }

        public static readonly DependencyProperty MOTDProperty = DependencyProperty.Register(nameof(MOTD), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MessageOfTheDay, "Message", ClearSection = true, Multiline = true, QuotedString = QuotedStringType.Remove)]
        public string MOTD
        {
            get { return (string)GetValue(MOTDProperty); }
            set
            {
                SetValue(MOTDProperty, value);
                ValidateMOTD();
            }
        }

        public static readonly DependencyProperty MOTDLengthProperty = DependencyProperty.Register(nameof(MOTDLength), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [XmlIgnore]
        public int MOTDLength
        {
            get { return (int)GetValue(MOTDLengthProperty); }
            set { SetValue(MOTDLengthProperty, value); }
        }

        public static readonly DependencyProperty MOTDLengthToLongProperty = DependencyProperty.Register(nameof(MOTDLengthToLong), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [XmlIgnore]
        public bool MOTDLengthToLong
        {
            get { return (bool)GetValue(MOTDLengthToLongProperty); }
            set { SetValue(MOTDLengthToLongProperty, value); }
        }

        public static readonly DependencyProperty MOTDDurationProperty = DependencyProperty.Register(nameof(MOTDDuration), typeof(int), typeof(ServerProfile), new PropertyMetadata(20));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MessageOfTheDay, "Duration")]
        public int MOTDDuration
        {
            get { return (int)GetValue(MOTDDurationProperty); }
            set { SetValue(MOTDDurationProperty, value); }
        }

        public static readonly DependencyProperty DisableValveAntiCheatSystemProperty = DependencyProperty.Register(nameof(DisableValveAntiCheatSystem), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool DisableValveAntiCheatSystem
        {
            get { return (bool)GetValue(DisableValveAntiCheatSystemProperty); }
            set { SetValue(DisableValveAntiCheatSystemProperty, value); }
        }

        public static readonly DependencyProperty DisablePlayerMovePhysicsOptimizationProperty = DependencyProperty.Register(nameof(DisablePlayerMovePhysicsOptimization), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool DisablePlayerMovePhysicsOptimization
        {
            get { return (bool)GetValue(DisablePlayerMovePhysicsOptimizationProperty); }
            set { SetValue(DisablePlayerMovePhysicsOptimizationProperty, value); }
        }

        public static readonly DependencyProperty DisableAntiSpeedHackDetectionProperty = DependencyProperty.Register(nameof(DisableAntiSpeedHackDetection), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool DisableAntiSpeedHackDetection
        {
            get { return (bool)GetValue(DisableAntiSpeedHackDetectionProperty); }
            set { SetValue(DisableAntiSpeedHackDetectionProperty, value); }
        }

        public static readonly DependencyProperty SpeedHackBiasProperty = DependencyProperty.Register(nameof(SpeedHackBias), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        public float SpeedHackBias
        {
            get { return (float)GetValue(SpeedHackBiasProperty); }
            set { SetValue(SpeedHackBiasProperty, value); }
        }

        public static readonly DependencyProperty UseBattlEyeProperty = DependencyProperty.Register(nameof(UseBattlEye), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseBattlEye
        {
            get { return (bool)GetValue(UseBattlEyeProperty); }
            set { SetValue(UseBattlEyeProperty, value); }
        }

        public static readonly DependencyProperty ForceRespawnDinosProperty = DependencyProperty.Register(nameof(ForceRespawnDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceRespawnDinos
        {
            get { return (bool)GetValue(ForceRespawnDinosProperty); }
            set { SetValue(ForceRespawnDinosProperty, value); }
        }

        public static readonly DependencyProperty EnableServerAutoForceRespawnWildDinosIntervalProperty = DependencyProperty.Register(nameof(EnableServerAutoForceRespawnWildDinosInterval), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableServerAutoForceRespawnWildDinosInterval
        {
            get { return (bool)GetValue(EnableServerAutoForceRespawnWildDinosIntervalProperty); }
            set { SetValue(EnableServerAutoForceRespawnWildDinosIntervalProperty, value); }
        }

        public static readonly DependencyProperty ServerAutoForceRespawnWildDinosIntervalProperty = DependencyProperty.Register(nameof(ServerAutoForceRespawnWildDinosInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(86400));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableServerAutoForceRespawnWildDinosInterval))]
        public int ServerAutoForceRespawnWildDinosInterval
        {
            get { return (int)GetValue(ServerAutoForceRespawnWildDinosIntervalProperty); }
            set { SetValue(ServerAutoForceRespawnWildDinosIntervalProperty, value); }
        }

        public static readonly DependencyProperty EnableServerAdminLogsProperty = DependencyProperty.Register(nameof(EnableServerAdminLogs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableServerAdminLogs
        {
            get { return (bool)GetValue(EnableServerAdminLogsProperty); }
            set { SetValue(EnableServerAdminLogsProperty, value); }
        }

        public static readonly DependencyProperty ServerAdminLogsIncludeTribeLogsProperty = DependencyProperty.Register(nameof(ServerAdminLogsIncludeTribeLogs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ServerAdminLogsIncludeTribeLogs
        {
            get { return (bool)GetValue(ServerAdminLogsIncludeTribeLogsProperty); }
            set { SetValue(ServerAdminLogsIncludeTribeLogsProperty, value); }
        }

        public static readonly DependencyProperty ServerRCONOutputTribeLogsProperty = DependencyProperty.Register(nameof(ServerRCONOutputTribeLogs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ServerRCONOutputTribeLogs
        {
            get { return (bool)GetValue(ServerRCONOutputTribeLogsProperty); }
            set { SetValue(ServerRCONOutputTribeLogsProperty, value); }
        }

        public static readonly DependencyProperty NotifyAdminCommandsInChatProperty = DependencyProperty.Register(nameof(NotifyAdminCommandsInChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool NotifyAdminCommandsInChat
        {
            get { return (bool)GetValue(NotifyAdminCommandsInChatProperty); }
            set { SetValue(NotifyAdminCommandsInChatProperty, value); }
        }

        public static readonly DependencyProperty MaxTribeLogsProperty = DependencyProperty.Register(nameof(MaxTribeLogs), typeof(int), typeof(ServerProfile), new PropertyMetadata(100));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int MaxTribeLogs
        {
            get { return (int)GetValue(MaxTribeLogsProperty); }
            set { SetValue(MaxTribeLogsProperty, value); }
        }

        public static readonly DependencyProperty TribeLogDestroyedEnemyStructuresProperty = DependencyProperty.Register(nameof(TribeLogDestroyedEnemyStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool TribeLogDestroyedEnemyStructures
        {
            get { return (bool)GetValue(TribeLogDestroyedEnemyStructuresProperty); }
            set { SetValue(TribeLogDestroyedEnemyStructuresProperty, value); }
        }

        public static readonly DependencyProperty ForceDirectX10Property = DependencyProperty.Register(nameof(ForceDirectX10), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceDirectX10
        {
            get { return (bool)GetValue(ForceDirectX10Property); }
            set { SetValue(ForceDirectX10Property, value); }
        }

        public static readonly DependencyProperty ForceShaderModel4Property = DependencyProperty.Register(nameof(ForceShaderModel4), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceShaderModel4
        {
            get { return (bool)GetValue(ForceShaderModel4Property); }
            set { SetValue(ForceShaderModel4Property, value); }
        }

        public static readonly DependencyProperty ForceLowMemoryProperty = DependencyProperty.Register(nameof(ForceLowMemory), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceLowMemory
        {
            get { return (bool)GetValue(ForceLowMemoryProperty); }
            set { SetValue(ForceLowMemoryProperty, value); }
        }

        public static readonly DependencyProperty ForceNoManSkyProperty = DependencyProperty.Register(nameof(ForceNoManSky), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceNoManSky
        {
            get { return (bool)GetValue(ForceNoManSkyProperty); }
            set { SetValue(ForceNoManSkyProperty, value); }
        }

        public static readonly DependencyProperty UseAllAvailableCoresProperty = DependencyProperty.Register(nameof(UseAllAvailableCores), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseAllAvailableCores
        {
            get { return (bool)GetValue(UseAllAvailableCoresProperty); }
            set { SetValue(UseAllAvailableCoresProperty, value); }
        }

        public static readonly DependencyProperty UseCacheProperty = DependencyProperty.Register(nameof(UseCache), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseCache
        {
            get { return (bool)GetValue(UseCacheProperty); }
            set { SetValue(UseCacheProperty, value); }
        }

        public static readonly DependencyProperty AltSaveDirectoryNameProperty = DependencyProperty.Register(nameof(AltSaveDirectoryName), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string AltSaveDirectoryName
        {
            get { return (string)GetValue(AltSaveDirectoryNameProperty); }
            set { SetValue(AltSaveDirectoryNameProperty, value); }
        }

        public static readonly DependencyProperty EnableWebAlarmProperty = DependencyProperty.Register(nameof(EnableWebAlarm), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableWebAlarm
        {
            get { return (bool)GetValue(EnableWebAlarmProperty); }
            set { SetValue(EnableWebAlarmProperty, value); }
        }

        public static readonly DependencyProperty WebAlarmKeyProperty = DependencyProperty.Register(nameof(WebAlarmKey), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string WebAlarmKey
        {
            get { return (string)GetValue(WebAlarmKeyProperty); }
            set { SetValue(WebAlarmKeyProperty, value); }
        }

        public static readonly DependencyProperty WebAlarmUrlProperty = DependencyProperty.Register(nameof(WebAlarmUrl), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string WebAlarmUrl
        {
            get { return (string)GetValue(WebAlarmUrlProperty); }
            set { SetValue(WebAlarmUrlProperty, value); }
        }

        public static readonly DependencyProperty UseOldSaveFormatProperty = DependencyProperty.Register(nameof(UseOldSaveFormat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseOldSaveFormat
        {
            get { return (bool)GetValue(UseOldSaveFormatProperty); }
            set { SetValue(UseOldSaveFormatProperty, value); }
        }

        public static readonly DependencyProperty UseNoMemoryBiasProperty = DependencyProperty.Register(nameof(UseNoMemoryBias), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseNoMemoryBias
        {
            get { return (bool)GetValue(UseNoMemoryBiasProperty); }
            set { SetValue(UseNoMemoryBiasProperty, value); }
        }

        public static readonly DependencyProperty StasisKeepControllersProperty = DependencyProperty.Register(nameof(StasisKeepControllers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool StasisKeepControllers
        {
            get { return (bool)GetValue(StasisKeepControllersProperty); }
            set { SetValue(StasisKeepControllersProperty, value); }
        }

        public static readonly DependencyProperty UseNoHangDetectionProperty = DependencyProperty.Register(nameof(UseNoHangDetection), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UseNoHangDetection
        {
            get { return (bool)GetValue(UseNoHangDetectionProperty); }
            set { SetValue(UseNoHangDetectionProperty, value); }
        }

        public static readonly DependencyProperty CrossArkClusterIdProperty = DependencyProperty.Register(nameof(CrossArkClusterId), typeof(string), typeof(ServerProfile), new PropertyMetadata(string.Empty));
        [DataMember]
        public string CrossArkClusterId
        {
            get { return (string)GetValue(CrossArkClusterIdProperty); }
            set { SetValue(CrossArkClusterIdProperty, value); }
        }

        public static readonly DependencyProperty ClusterDirOverrideProperty = DependencyProperty.Register(nameof(ClusterDirOverride), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ClusterDirOverride
        {
            get { return (bool)GetValue(ClusterDirOverrideProperty); }
            set { SetValue(ClusterDirOverrideProperty, value); }
        }

        public static readonly DependencyProperty AdditionalArgsProperty = DependencyProperty.Register(nameof(AdditionalArgs), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string AdditionalArgs
        {
            get { return (string)GetValue(AdditionalArgsProperty); }
            set { SetValue(AdditionalArgsProperty, value); }
        }

        public static readonly DependencyProperty LauncherArgsOverrideProperty = DependencyProperty.Register(nameof(LauncherArgsOverride), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool LauncherArgsOverride
        {
            get { return (bool)GetValue(LauncherArgsOverrideProperty); }
            set { SetValue(LauncherArgsOverrideProperty, value); }
        }

        public static readonly DependencyProperty LauncherArgsProperty = DependencyProperty.Register(nameof(LauncherArgs), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [DataMember]
        public string LauncherArgs
        {
            get { return (string)GetValue(LauncherArgsProperty); }
            set { SetValue(LauncherArgsProperty, value); }
        }

        public static readonly DependencyProperty AllowHideDamageSourceFromLogsProperty = DependencyProperty.Register(nameof(AllowHideDamageSourceFromLogs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowHideDamageSourceFromLogs
        {
            get { return (bool)GetValue(AllowHideDamageSourceFromLogsProperty); }
            set { SetValue(AllowHideDamageSourceFromLogsProperty, value); }
        }

        public static readonly DependencyProperty ServerAllowAnselProperty = DependencyProperty.Register(nameof(ServerAllowAnsel), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ServerAllowAnsel
        {
            get { return (bool)GetValue(ServerAllowAnselProperty); }
            set { SetValue(ServerAllowAnselProperty, value); }
        }

        public static readonly DependencyProperty NoDinosProperty = DependencyProperty.Register(nameof(NoDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool NoDinos
        {
            get { return (bool)GetValue(NoDinosProperty); }
            set { SetValue(NoDinosProperty, value); }
        }
        #endregion

        #region Automatic Management
        public static readonly DependencyProperty EnableAutoBackupProperty = DependencyProperty.Register(nameof(EnableAutoBackup), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAutoBackup
        {
            get { return (bool)GetValue(EnableAutoBackupProperty); }
            set { SetValue(EnableAutoBackupProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoStartProperty = DependencyProperty.Register(nameof(EnableAutoStart), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAutoStart
        {
            get { return (bool)GetValue(EnableAutoStartProperty); }
            set { SetValue(EnableAutoStartProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoUpdateProperty = DependencyProperty.Register(nameof(EnableAutoUpdate), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAutoUpdate
        {
            get { return (bool)GetValue(EnableAutoUpdateProperty); }
            set { SetValue(EnableAutoUpdateProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoShutdown1Property = DependencyProperty.Register(nameof(EnableAutoShutdown1), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAutoShutdown1
        {
            get { return (bool)GetValue(EnableAutoShutdown1Property); }
            set { SetValue(EnableAutoShutdown1Property, value); }
        }

        public static readonly DependencyProperty AutoShutdownTime1Property = DependencyProperty.Register(nameof(AutoShutdownTime1), typeof(string), typeof(ServerProfile), new PropertyMetadata("00:00"));
        [DataMember]
        public string AutoShutdownTime1
        {
            get { return (string)GetValue(AutoShutdownTime1Property); }
            set { SetValue(AutoShutdownTime1Property, value); }
        }

        public static readonly DependencyProperty RestartAfterShutdown1Property = DependencyProperty.Register(nameof(RestartAfterShutdown1), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        public bool RestartAfterShutdown1
        {
            get { return (bool)GetValue(RestartAfterShutdown1Property); }
            set { SetValue(RestartAfterShutdown1Property, value); }
        }

        public static readonly DependencyProperty UpdateAfterShutdown1Property = DependencyProperty.Register(nameof(UpdateAfterShutdown1), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UpdateAfterShutdown1
        {
            get { return (bool)GetValue(UpdateAfterShutdown1Property); }
            set { SetValue(UpdateAfterShutdown1Property, value); }
        }

        public static readonly DependencyProperty EnableAutoShutdown2Property = DependencyProperty.Register(nameof(EnableAutoShutdown2), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAutoShutdown2
        {
            get { return (bool)GetValue(EnableAutoShutdown2Property); }
            set { SetValue(EnableAutoShutdown2Property, value); }
        }

        public static readonly DependencyProperty AutoShutdownTime2Property = DependencyProperty.Register(nameof(AutoShutdownTime2), typeof(string), typeof(ServerProfile), new PropertyMetadata("00:00"));
        [DataMember]
        public string AutoShutdownTime2
        {
            get { return (string)GetValue(AutoShutdownTime2Property); }
            set { SetValue(AutoShutdownTime2Property, value); }
        }

        public static readonly DependencyProperty RestartAfterShutdown2Property = DependencyProperty.Register(nameof(RestartAfterShutdown2), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        public bool RestartAfterShutdown2
        {
            get { return (bool)GetValue(RestartAfterShutdown2Property); }
            set { SetValue(RestartAfterShutdown2Property, value); }
        }

        public static readonly DependencyProperty UpdateAfterShutdown2Property = DependencyProperty.Register(nameof(UpdateAfterShutdown2), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool UpdateAfterShutdown2
        {
            get { return (bool)GetValue(UpdateAfterShutdown2Property); }
            set { SetValue(UpdateAfterShutdown2Property, value); }
        }

        public static readonly DependencyProperty AutoRestartIfShutdownProperty = DependencyProperty.Register(nameof(AutoRestartIfShutdown), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool AutoRestartIfShutdown
        {
            get { return (bool)GetValue(AutoRestartIfShutdownProperty); }
            set { SetValue(AutoRestartIfShutdownProperty, value); }
        }
        #endregion

        #region Rules
        public static readonly DependencyProperty EnableHardcoreProperty = DependencyProperty.Register(nameof(EnableHardcore), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerHardcore")]
        public bool EnableHardcore
        {
            get { return (bool)GetValue(EnableHardcoreProperty); }
            set { SetValue(EnableHardcoreProperty, value); }
        }

        public static readonly DependencyProperty EnablePVPProperty = DependencyProperty.Register(nameof(EnablePVP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerPVE", InvertBoolean = true)]
        public bool EnablePVP
        {
            get { return (bool)GetValue(EnablePVPProperty); }
            set { SetValue(EnablePVPProperty, value); }
        }

        public static readonly DependencyProperty AllowCaveBuildingPvEProperty = DependencyProperty.Register(nameof(AllowCaveBuildingPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowCaveBuildingPvE
        {
            get { return (bool)GetValue(AllowCaveBuildingPvEProperty); }
            set { SetValue(AllowCaveBuildingPvEProperty, value); }
        }

        public static readonly DependencyProperty DisableFriendlyFirePvPProperty = DependencyProperty.Register(nameof(DisableFriendlyFirePvP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableFriendlyFire")]
        public bool DisableFriendlyFirePvP
        {
            get { return (bool)GetValue(DisableFriendlyFirePvPProperty); }
            set { SetValue(DisableFriendlyFirePvPProperty, value); }
        }

        public static readonly DependencyProperty DisableFriendlyFirePvEProperty = DependencyProperty.Register(nameof(DisableFriendlyFirePvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEDisableFriendlyFire")]
        public bool DisableFriendlyFirePvE
        {
            get { return (bool)GetValue(DisableFriendlyFirePvEProperty); }
            set { SetValue(DisableFriendlyFirePvEProperty, value); }
        }

        public static readonly DependencyProperty DisableLootCratesProperty = DependencyProperty.Register(nameof(DisableLootCrates), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableLootCrates")]
        public bool DisableLootCrates
        {
            get { return (bool)GetValue(DisableLootCratesProperty); }
            set { SetValue(DisableLootCratesProperty, value); }
        }

        public static readonly DependencyProperty AllowCrateSpawnsOnTopOfStructuresProperty = DependencyProperty.Register(nameof(AllowCrateSpawnsOnTopOfStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        public bool AllowCrateSpawnsOnTopOfStructures
        {
            get { return (bool)GetValue(AllowCrateSpawnsOnTopOfStructuresProperty); }
            set { SetValue(AllowCrateSpawnsOnTopOfStructuresProperty, value); }
        }

        public static readonly DependencyProperty EnableExtraStructurePreventionVolumesProperty = DependencyProperty.Register(nameof(EnableExtraStructurePreventionVolumes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool EnableExtraStructurePreventionVolumes
        {
            get { return (bool)GetValue(EnableExtraStructurePreventionVolumesProperty); }
            set { SetValue(EnableExtraStructurePreventionVolumesProperty, value); }
        }

        public static readonly DependencyProperty EnableDifficultyOverrideProperty = DependencyProperty.Register(nameof(EnableDifficultyOverride), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableDifficultyOverride
        {
            get { return (bool)GetValue(EnableDifficultyOverrideProperty); }
            set { SetValue(EnableDifficultyOverrideProperty, value); }
        }

        public static readonly DependencyProperty OverrideOfficialDifficultyProperty = DependencyProperty.Register(nameof(OverrideOfficialDifficulty), typeof(float), typeof(ServerProfile), new PropertyMetadata(4.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableDifficultyOverride))]
        public float OverrideOfficialDifficulty
        {
            get { return (float)GetValue(OverrideOfficialDifficultyProperty); }
            set { SetValue(OverrideOfficialDifficultyProperty, value); }
        }

        public static readonly DependencyProperty DifficultyOffsetProperty = DependencyProperty.Register(nameof(DifficultyOffset), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableDifficultyOverride))]
        public float DifficultyOffset
        {
            get { return (float)GetValue(DifficultyOffsetProperty); }
            set { SetValue(DifficultyOffsetProperty, value); }
        }

        public static readonly DependencyProperty MaxNumberOfPlayersInTribeProperty = DependencyProperty.Register(nameof(MaxNumberOfPlayersInTribe), typeof(int), typeof(ServerProfile), new PropertyMetadata(70));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int MaxNumberOfPlayersInTribe
        {
            get { return (int)GetValue(MaxNumberOfPlayersInTribeProperty); }
            set { SetValue(MaxNumberOfPlayersInTribeProperty, value); }
        }

        public static readonly DependencyProperty EnableTributeDownloadsProperty = DependencyProperty.Register(nameof(EnableTributeDownloads), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "NoTributeDownloads", InvertBoolean = true)]
        public bool EnableTributeDownloads
        {
            get { return (bool)GetValue(EnableTributeDownloadsProperty); }
            set { SetValue(EnableTributeDownloadsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadSurvivorsProperty = DependencyProperty.Register(nameof(PreventDownloadSurvivors), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableTributeDownloads))]
        public bool PreventDownloadSurvivors
        {
            get { return (bool)GetValue(PreventDownloadSurvivorsProperty); }
            set { SetValue(PreventDownloadSurvivorsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadItemsProperty = DependencyProperty.Register(nameof(PreventDownloadItems), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableTributeDownloads))]
        public bool PreventDownloadItems
        {
            get { return (bool)GetValue(PreventDownloadItemsProperty); }
            set { SetValue(PreventDownloadItemsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadDinosProperty = DependencyProperty.Register(nameof(PreventDownloadDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableTributeDownloads))]
        public bool PreventDownloadDinos
        {
            get { return (bool)GetValue(PreventDownloadDinosProperty); }
            set { SetValue(PreventDownloadDinosProperty, value); }
        }

        public static readonly DependencyProperty PreventUploadSurvivorsProperty = DependencyProperty.Register(nameof(PreventUploadSurvivors), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventUploadSurvivors
        {
            get { return (bool)GetValue(PreventUploadSurvivorsProperty); }
            set { SetValue(PreventUploadSurvivorsProperty, value); }
        }

        public static readonly DependencyProperty PreventUploadItemsProperty = DependencyProperty.Register(nameof(PreventUploadItems), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventUploadItems
        {
            get { return (bool)GetValue(PreventUploadItemsProperty); }
            set { SetValue(PreventUploadItemsProperty, value); }
        }

        public static readonly DependencyProperty PreventUploadDinosProperty = DependencyProperty.Register(nameof(PreventUploadDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventUploadDinos
        {
            get { return (bool)GetValue(PreventUploadDinosProperty); }
            set { SetValue(PreventUploadDinosProperty, value); }
        }

        public static readonly DependencyProperty NoTransferFromFilteringProperty = DependencyProperty.Register(nameof(NoTransferFromFiltering), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool NoTransferFromFiltering
        {
            get { return (bool)GetValue(NoTransferFromFilteringProperty); }
            set { SetValue(NoTransferFromFilteringProperty, value); }
        }

        public static readonly DependencyProperty OverrideTributeCharacterExpirationSecondsProperty = DependencyProperty.Register(nameof(OverrideTributeCharacterExpirationSeconds), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool OverrideTributeCharacterExpirationSeconds
        {
            get { return (bool)GetValue(OverrideTributeCharacterExpirationSecondsProperty); }
            set { SetValue(OverrideTributeCharacterExpirationSecondsProperty, value); }
        }

        public static readonly DependencyProperty OverrideTributeItemExpirationSecondsProperty = DependencyProperty.Register(nameof(OverrideTributeItemExpirationSeconds), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool OverrideTributeItemExpirationSeconds
        {
            get { return (bool)GetValue(OverrideTributeItemExpirationSecondsProperty); }
            set { SetValue(OverrideTributeItemExpirationSecondsProperty, value); }
        }

        public static readonly DependencyProperty OverrideTributeDinoExpirationSecondsProperty = DependencyProperty.Register(nameof(OverrideTributeDinoExpirationSeconds), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool OverrideTributeDinoExpirationSeconds
        {
            get { return (bool)GetValue(OverrideTributeDinoExpirationSecondsProperty); }
            set { SetValue(OverrideTributeDinoExpirationSecondsProperty, value); }
        }

        public static readonly DependencyProperty OverrideMinimumDinoReuploadIntervalProperty = DependencyProperty.Register(nameof(OverrideMinimumDinoReuploadInterval), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool OverrideMinimumDinoReuploadInterval
        {
            get { return (bool)GetValue(OverrideMinimumDinoReuploadIntervalProperty); }
            set { SetValue(OverrideMinimumDinoReuploadIntervalProperty, value); }
        }

        [XmlIgnore]
        public bool SaveTributeCharacterExpirationSeconds
        {
            get { return !string.IsNullOrWhiteSpace(this.CrossArkClusterId) && OverrideTributeCharacterExpirationSeconds; }
            set { value = value; }
        }

        [XmlIgnore]
        public bool SaveTributeItemExpirationSeconds
        {
            get { return !string.IsNullOrWhiteSpace(this.CrossArkClusterId) && OverrideTributeItemExpirationSeconds; }
            set { value = value; }
        }

        [XmlIgnore]
        public bool SaveTributeDinoExpirationSeconds
        {
            get { return !string.IsNullOrWhiteSpace(this.CrossArkClusterId) && OverrideTributeDinoExpirationSeconds; }
            set { value = value; }
        }

        [XmlIgnore]
        public bool SaveMinimumDinoReuploadInterval
        {
            get { return !string.IsNullOrWhiteSpace(this.CrossArkClusterId) && OverrideMinimumDinoReuploadInterval; }
            set { value = value; }
        }

        public static readonly DependencyProperty TributeCharacterExpirationSecondsProperty = DependencyProperty.Register(nameof(TributeCharacterExpirationSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(86400));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(SaveTributeCharacterExpirationSeconds))]
        public int TributeCharacterExpirationSeconds
        {
            get { return (int)GetValue(TributeCharacterExpirationSecondsProperty); }
            set { SetValue(TributeCharacterExpirationSecondsProperty, value); }
        }

        public static readonly DependencyProperty TributeItemExpirationSecondsProperty = DependencyProperty.Register(nameof(TributeItemExpirationSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(86400));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(SaveTributeItemExpirationSeconds))]
        public int TributeItemExpirationSeconds
        {
            get { return (int)GetValue(TributeItemExpirationSecondsProperty); }
            set { SetValue(TributeItemExpirationSecondsProperty, value); }
        }

        public static readonly DependencyProperty TributeDinoExpirationSecondsProperty = DependencyProperty.Register(nameof(TributeDinoExpirationSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(86400));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(SaveTributeDinoExpirationSeconds))]
        public int TributeDinoExpirationSeconds
        {
            get { return (int)GetValue(TributeDinoExpirationSecondsProperty); }
            set { SetValue(TributeDinoExpirationSecondsProperty, value); }
        }

        public static readonly DependencyProperty MinimumDinoReuploadIntervalProperty = DependencyProperty.Register(nameof(MinimumDinoReuploadInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(43200));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(SaveMinimumDinoReuploadInterval))]
        public int MinimumDinoReuploadInterval
        {
            get { return (int)GetValue(MinimumDinoReuploadIntervalProperty); }
            set { SetValue(MinimumDinoReuploadIntervalProperty, value); }
        }

        public static readonly DependencyProperty CrossARKAllowForeignDinoDownloadsProperty = DependencyProperty.Register(nameof(CrossARKAllowForeignDinoDownloads), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool CrossARKAllowForeignDinoDownloads
        {
            get { return (bool)GetValue(CrossARKAllowForeignDinoDownloadsProperty); }
            set { SetValue(CrossARKAllowForeignDinoDownloadsProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnInterval), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bIncreasePvPRespawnInterval")]
        public bool IncreasePvPRespawnInterval
        {
            get { return (bool)GetValue(IncreasePvPRespawnIntervalProperty); }
            set { SetValue(IncreasePvPRespawnIntervalProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalCheckPeriodProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalCheckPeriod), typeof(int), typeof(ServerProfile), new PropertyMetadata(300));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public int IncreasePvPRespawnIntervalCheckPeriod
        {
            get { return (int)GetValue(IncreasePvPRespawnIntervalCheckPeriodProperty); }
            set { SetValue(IncreasePvPRespawnIntervalCheckPeriodProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalMultiplierProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public float IncreasePvPRespawnIntervalMultiplier
        {
            get { return (float)GetValue(IncreasePvPRespawnIntervalMultiplierProperty); }
            set { SetValue(IncreasePvPRespawnIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalBaseAmountProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalBaseAmount), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public int IncreasePvPRespawnIntervalBaseAmount
        {
            get { return (int)GetValue(IncreasePvPRespawnIntervalBaseAmountProperty); }
            set { SetValue(IncreasePvPRespawnIntervalBaseAmountProperty, value); }
        }

        public static readonly DependencyProperty PreventOfflinePvPProperty = DependencyProperty.Register(nameof(PreventOfflinePvP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventOfflinePvP
        {
            get { return (bool)GetValue(PreventOfflinePvPProperty); }
            set { SetValue(PreventOfflinePvPProperty, value); }
        }

        public static readonly DependencyProperty PreventOfflinePvPIntervalProperty = DependencyProperty.Register(nameof(PreventOfflinePvPInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(900));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(PreventOfflinePvP))]
        public int PreventOfflinePvPInterval
        {
            get { return (int)GetValue(PreventOfflinePvPIntervalProperty); }
            set { SetValue(PreventOfflinePvPIntervalProperty, value); }
        }

        public static readonly DependencyProperty PreventOfflinePvPConnectionInvincibleIntervalProperty = DependencyProperty.Register(nameof(PreventOfflinePvPConnectionInvincibleInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(5));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(PreventOfflinePvP))]
        public int PreventOfflinePvPConnectionInvincibleInterval
        {
            get { return (int)GetValue(PreventOfflinePvPConnectionInvincibleIntervalProperty); }
            set { SetValue(PreventOfflinePvPConnectionInvincibleIntervalProperty, value); }
        }

        public static readonly DependencyProperty AutoPvETimerProperty = DependencyProperty.Register(nameof(AutoPvETimer), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bAutoPvETimer")]
        public bool AutoPvETimer
        {
            get { return (bool)GetValue(AutoPvETimerProperty); }
            set { SetValue(AutoPvETimerProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEUseSystemTimeProperty = DependencyProperty.Register(nameof(AutoPvEUseSystemTime), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bAutoPvEUseSystemTime", ConditionedOn = nameof(AutoPvETimer))]
        public bool AutoPvEUseSystemTime
        {
            get { return (bool)GetValue(AutoPvEUseSystemTimeProperty); }
            set { SetValue(AutoPvEUseSystemTimeProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEStartTimeSecondsProperty = DependencyProperty.Register(nameof(AutoPvEStartTimeSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AutoPvETimer))]
        public int AutoPvEStartTimeSeconds
        {
            get { return (int)GetValue(AutoPvEStartTimeSecondsProperty); }
            set { SetValue(AutoPvEStartTimeSecondsProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEStopTimeSecondsProperty = DependencyProperty.Register(nameof(AutoPvEStopTimeSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AutoPvETimer))]
        public int AutoPvEStopTimeSeconds
        {
            get { return (int)GetValue(AutoPvEStopTimeSecondsProperty); }
            set { SetValue(AutoPvEStopTimeSecondsProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeWarPvEProperty = DependencyProperty.Register(nameof(AllowTribeWarPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEAllowTribeWar")]
        public bool AllowTribeWarPvE
        {
            get { return (bool)GetValue(AllowTribeWarPvEProperty); }
            set { SetValue(AllowTribeWarPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeWarCancelPvEProperty = DependencyProperty.Register(nameof(AllowTribeWarCancelPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEAllowTribeWarCancel")]
        public bool AllowTribeWarCancelPvE
        {
            get { return (bool)GetValue(AllowTribeWarCancelPvEProperty); }
            set { SetValue(AllowTribeWarCancelPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeAlliancesProperty = DependencyProperty.Register(nameof(AllowTribeAlliances), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "PreventTribeAlliances", InvertBoolean = true)]
        public bool AllowTribeAlliances
        {
            get { return (bool)GetValue(AllowTribeAlliancesProperty); }
            set { SetValue(AllowTribeAlliancesProperty, value); }
        }

        public static readonly DependencyProperty MaxAlliancesPerTribeProperty = DependencyProperty.Register(nameof(MaxAlliancesPerTribe), typeof(int), typeof(ServerProfile), new PropertyMetadata(10));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AllowTribeAlliances))]
        public int MaxAlliancesPerTribe
        {
            get { return (int)GetValue(MaxAlliancesPerTribeProperty); }
            set { SetValue(MaxAlliancesPerTribeProperty, value); }
        }

        public static readonly DependencyProperty MaxTribesPerAllianceProperty = DependencyProperty.Register(nameof(MaxTribesPerAlliance), typeof(int), typeof(ServerProfile), new PropertyMetadata(10));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AllowTribeAlliances))]
        public int MaxTribesPerAlliance
        {
            get { return (int)GetValue(MaxTribesPerAllianceProperty); }
            set { SetValue(MaxTribesPerAllianceProperty, value); }
        }

        public static readonly DependencyProperty AllowCustomRecipesProperty = DependencyProperty.Register(nameof(AllowCustomRecipes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bAllowCustomRecipes")]
        public bool AllowCustomRecipes
        {
            get { return (bool)GetValue(AllowCustomRecipesProperty); }
            set { SetValue(AllowCustomRecipesProperty, value); }
        }

        public static readonly DependencyProperty CustomRecipeEffectivenessMultiplierProperty = DependencyProperty.Register(nameof(CustomRecipeEffectivenessMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CustomRecipeEffectivenessMultiplier
        {
            get { return (float)GetValue(CustomRecipeEffectivenessMultiplierProperty); }
            set { SetValue(CustomRecipeEffectivenessMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CustomRecipeSkillMultiplierProperty = DependencyProperty.Register(nameof(CustomRecipeSkillMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CustomRecipeSkillMultiplier
        {
            get { return (float)GetValue(CustomRecipeSkillMultiplierProperty); }
            set { SetValue(CustomRecipeSkillMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EnableDiseasesProperty = DependencyProperty.Register(nameof(EnableDiseases), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "PreventDiseases", InvertBoolean = true)]
        public bool EnableDiseases
        {
            get { return (bool)GetValue(EnableDiseasesProperty); }
            set { SetValue(EnableDiseasesProperty, value); }
        }

        public static readonly DependencyProperty NonPermanentDiseasesProperty = DependencyProperty.Register(nameof(NonPermanentDiseases), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableDiseases))]
        public bool NonPermanentDiseases
        {
            get { return (bool)GetValue(NonPermanentDiseasesProperty); }
            set { SetValue(NonPermanentDiseasesProperty, value); }
        }

        public static readonly DependencyProperty OverrideNPCNetworkStasisRangeScaleProperty = DependencyProperty.Register(nameof(OverrideNPCNetworkStasisRangeScale), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool OverrideNPCNetworkStasisRangeScale
        {
            get { return (bool)GetValue(OverrideNPCNetworkStasisRangeScaleProperty); }
            set { SetValue(OverrideNPCNetworkStasisRangeScaleProperty, value); }
        }

        public static readonly DependencyProperty NPCNetworkStasisRangeScalePlayerCountStartProperty = DependencyProperty.Register(nameof(NPCNetworkStasisRangeScalePlayerCountStart), typeof(int), typeof(ServerProfile), new PropertyMetadata(70));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(OverrideNPCNetworkStasisRangeScale))]
        public int NPCNetworkStasisRangeScalePlayerCountStart
        {
            get { return (int)GetValue(NPCNetworkStasisRangeScalePlayerCountStartProperty); }
            set { SetValue(NPCNetworkStasisRangeScalePlayerCountStartProperty, value); }
        }

        public static readonly DependencyProperty NPCNetworkStasisRangeScalePlayerCountEndProperty = DependencyProperty.Register(nameof(NPCNetworkStasisRangeScalePlayerCountEnd), typeof(int), typeof(ServerProfile), new PropertyMetadata(120));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(OverrideNPCNetworkStasisRangeScale))]
        public int NPCNetworkStasisRangeScalePlayerCountEnd
        {
            get { return (int)GetValue(NPCNetworkStasisRangeScalePlayerCountEndProperty); }
            set { SetValue(NPCNetworkStasisRangeScalePlayerCountEndProperty, value); }
        }

        public static readonly DependencyProperty NPCNetworkStasisRangeScalePercentEndProperty = DependencyProperty.Register(nameof(NPCNetworkStasisRangeScalePercentEnd), typeof(float), typeof(ServerProfile), new PropertyMetadata(0.5f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(OverrideNPCNetworkStasisRangeScale))]
        public float NPCNetworkStasisRangeScalePercentEnd
        {
            get { return (float)GetValue(NPCNetworkStasisRangeScalePercentEndProperty); }
            set { SetValue(NPCNetworkStasisRangeScalePercentEndProperty, value); }
        }

        public static readonly DependencyProperty UseCorpseLocatorProperty = DependencyProperty.Register(nameof(UseCorpseLocator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bUseCorpseLocator")]
        public bool UseCorpseLocator
        {
            get { return (bool)GetValue(UseCorpseLocatorProperty); }
            set { SetValue(UseCorpseLocatorProperty, value); }
        }

        public static readonly DependencyProperty PreventSpawnAnimationsProperty = DependencyProperty.Register(nameof(PreventSpawnAnimations), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventSpawnAnimations
        {
            get { return (bool)GetValue(PreventSpawnAnimationsProperty); }
            set { SetValue(PreventSpawnAnimationsProperty, value); }
        }

        public static readonly DependencyProperty AllowUnlimitedRespecsProperty = DependencyProperty.Register(nameof(AllowUnlimitedRespecs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bAllowUnlimitedRespecs")]
        public bool AllowUnlimitedRespecs
        {
            get { return (bool)GetValue(AllowUnlimitedRespecsProperty); }
            set { SetValue(AllowUnlimitedRespecsProperty, value); }
        }

        public static readonly DependencyProperty AllowPlatformSaddleMultiFloorsProperty = DependencyProperty.Register(nameof(AllowPlatformSaddleMultiFloors), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bAllowPlatformSaddleMultiFloors")]
        public bool AllowPlatformSaddleMultiFloors
        {
            get { return (bool)GetValue(AllowPlatformSaddleMultiFloorsProperty); }
            set { SetValue(AllowPlatformSaddleMultiFloorsProperty, value); }
        }

        public static readonly DependencyProperty OxygenSwimSpeedStatMultiplierProperty = DependencyProperty.Register(nameof(OxygenSwimSpeedStatMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float OxygenSwimSpeedStatMultiplier
        {
            get { return (float)GetValue(OxygenSwimSpeedStatMultiplierProperty); }
            set { SetValue(OxygenSwimSpeedStatMultiplierProperty, value); }
        }

        public static readonly DependencyProperty SupplyCrateLootQualityMultiplierProperty = DependencyProperty.Register(nameof(SupplyCrateLootQualityMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float SupplyCrateLootQualityMultiplier
        {
            get { return (float)GetValue(SupplyCrateLootQualityMultiplierProperty); }
            set { SetValue(SupplyCrateLootQualityMultiplierProperty, value); }
        }

        public static readonly DependencyProperty FishingLootQualityMultiplierProperty = DependencyProperty.Register(nameof(FishingLootQualityMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float FishingLootQualityMultiplier
        {
            get { return (float)GetValue(FishingLootQualityMultiplierProperty); }
            set { SetValue(FishingLootQualityMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EnableNoFishLootProperty = DependencyProperty.Register(nameof(EnableNoFishLoot), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableNoFishLoot
        {
            get { return (bool)GetValue(EnableNoFishLootProperty); }
            set { SetValue(EnableNoFishLootProperty, value); }
        }

        public static readonly DependencyProperty UseCorpseLifeSpanMultiplierProperty = DependencyProperty.Register(nameof(UseCorpseLifeSpanMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float UseCorpseLifeSpanMultiplier
        {
            get { return (float)GetValue(UseCorpseLifeSpanMultiplierProperty); }
            set { SetValue(UseCorpseLifeSpanMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GlobalPoweredBatteryDurabilityDecreasePerSecondProperty = DependencyProperty.Register(nameof(GlobalPoweredBatteryDurabilityDecreasePerSecond), typeof(float), typeof(ServerProfile), new PropertyMetadata(4.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalPoweredBatteryDurabilityDecreasePerSecond
        {
            get { return (float)GetValue(GlobalPoweredBatteryDurabilityDecreasePerSecondProperty); }
            set { SetValue(GlobalPoweredBatteryDurabilityDecreasePerSecondProperty, value); }
        }

        public static readonly DependencyProperty TribeNameChangeCooldownProperty = DependencyProperty.Register(nameof(TribeNameChangeCooldown), typeof(int), typeof(ServerProfile), new PropertyMetadata(15));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int TribeNameChangeCooldown
        {
            get { return (int)GetValue(TribeNameChangeCooldownProperty); }
            set { SetValue(TribeNameChangeCooldownProperty, value); }
        }

        public static readonly DependencyProperty RandomSupplyCratePointsProperty = DependencyProperty.Register(nameof(RandomSupplyCratePoints), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public bool RandomSupplyCratePoints
        {
            get { return (bool)GetValue(RandomSupplyCratePointsProperty); }
            set { SetValue(RandomSupplyCratePointsProperty, value); }
        }
        #endregion

        #region Chat and Notifications
        public static readonly DependencyProperty EnableGlobalVoiceChatProperty = DependencyProperty.Register(nameof(EnableGlobalVoiceChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "globalVoiceChat")]
        public bool EnableGlobalVoiceChat
        {
            get { return (bool)GetValue(EnableGlobalVoiceChatProperty); }
            set { SetValue(EnableGlobalVoiceChatProperty, value); }
        }

        public static readonly DependencyProperty EnableProximityChatProperty = DependencyProperty.Register(nameof(EnableProximityChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "proximityChat")]
        public bool EnableProximityChat
        {
            get { return (bool)GetValue(EnableProximityChatProperty); }
            set { SetValue(EnableProximityChatProperty, value); }
        }

        public static readonly DependencyProperty EnablePlayerLeaveNotificationsProperty = DependencyProperty.Register(nameof(EnablePlayerLeaveNotifications), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "alwaysNotifyPlayerLeft")]
        public bool EnablePlayerLeaveNotifications
        {
            get { return (bool)GetValue(EnablePlayerLeaveNotificationsProperty); }
            set { SetValue(EnablePlayerLeaveNotificationsProperty, value); }
        }

        public static readonly DependencyProperty EnablePlayerJoinedNotificationsProperty = DependencyProperty.Register(nameof(EnablePlayerJoinedNotifications), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "alwaysNotifyPlayerJoined")]
        public bool EnablePlayerJoinedNotifications
        {
            get { return (bool)GetValue(EnablePlayerJoinedNotificationsProperty); }
            set { SetValue(EnablePlayerJoinedNotificationsProperty, value); }
        }
        #endregion

        #region HUD and Visuals
        public static readonly DependencyProperty AllowCrosshairProperty = DependencyProperty.Register(nameof(AllowCrosshair), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerCrosshair")]
        public bool AllowCrosshair
        {
            get { return (bool)GetValue(AllowCrosshairProperty); }
            set { SetValue(AllowCrosshairProperty, value); }
        }

        public static readonly DependencyProperty AllowHUDProperty = DependencyProperty.Register(nameof(AllowHUD), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerForceNoHud", InvertBoolean = true)]
        public bool AllowHUD
        {
            get { return (bool)GetValue(AllowHUDProperty); }
            set { SetValue(AllowHUDProperty, value); }
        }

        public static readonly DependencyProperty AllowThirdPersonViewProperty = DependencyProperty.Register(nameof(AllowThirdPersonView), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "AllowThirdPersonPlayer")]
        public bool AllowThirdPersonView
        {
            get { return (bool)GetValue(AllowThirdPersonViewProperty); }
            set { SetValue(AllowThirdPersonViewProperty, value); }
        }

        public static readonly DependencyProperty AllowMapPlayerLocationProperty = DependencyProperty.Register(nameof(AllowMapPlayerLocation), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ShowMapPlayerLocation")]
        public bool AllowMapPlayerLocation
        {
            get { return (bool)GetValue(AllowMapPlayerLocationProperty); }
            set { SetValue(AllowMapPlayerLocationProperty, value); }
        }

        public static readonly DependencyProperty AllowPVPGammaProperty = DependencyProperty.Register(nameof(AllowPVPGamma), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "EnablePVPGamma")]
        public bool AllowPVPGamma
        {
            get { return (bool)GetValue(AllowPVPGammaProperty); }
            set { SetValue(AllowPVPGammaProperty, value); }
        }

        public static readonly DependencyProperty AllowPvEGammaProperty = DependencyProperty.Register(nameof(AllowPvEGamma), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DisablePvEGamma", InvertBoolean = true)]
        public bool AllowPvEGamma
        {
            get { return (bool)GetValue(AllowPvEGammaProperty); }
            set { SetValue(AllowPvEGammaProperty, value); }
        }

        public static readonly DependencyProperty ShowFloatingDamageTextProperty = DependencyProperty.Register(nameof(ShowFloatingDamageText), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool ShowFloatingDamageText
        {
            get { return (bool)GetValue(ShowFloatingDamageTextProperty); }
            set { SetValue(ShowFloatingDamageTextProperty, value); }
        }

        public static readonly DependencyProperty AllowHitMarkersProperty = DependencyProperty.Register(nameof(AllowHitMarkers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowHitMarkers
        {
            get { return (bool)GetValue(AllowHitMarkersProperty); }
            set { SetValue(AllowHitMarkersProperty, value); }
        }
        #endregion

        #region Player Settings
        public static readonly DependencyProperty EnableFlyerCarryProperty = DependencyProperty.Register(nameof(EnableFlyerCarry), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "AllowFlyerCarryPVE")]
        public bool EnableFlyerCarry
        {
            get { return (bool)GetValue(EnableFlyerCarryProperty); }
            set { SetValue(EnableFlyerCarryProperty, value); }
        }

        public static readonly DependencyProperty XPMultiplierProperty = DependencyProperty.Register(nameof(XPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float XPMultiplier
        {
            get { return (float)GetValue(XPMultiplierProperty); }
            set { SetValue(XPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty OverrideMaxExperiencePointsPlayerProperty = DependencyProperty.Register(nameof(OverrideMaxExperiencePointsPlayer), typeof(int), typeof(ServerProfile), new PropertyMetadata(GameData.DEFAULT_MAX_EXPERIENCE_POINTS_PLAYER));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int OverrideMaxExperiencePointsPlayer
        {
            get { return (int)GetValue(OverrideMaxExperiencePointsPlayerProperty); }
            set { SetValue(OverrideMaxExperiencePointsPlayerProperty, value); }
        }

        public static readonly DependencyProperty PlayerDamageMultiplierProperty = DependencyProperty.Register(nameof(PlayerDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerDamageMultiplier
        {
            get { return (float)GetValue(PlayerDamageMultiplierProperty); }
            set { SetValue(PlayerDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerResistanceMultiplierProperty = DependencyProperty.Register(nameof(PlayerResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerResistanceMultiplier
        {
            get { return (float)GetValue(PlayerResistanceMultiplierProperty); }
            set { SetValue(PlayerResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterWaterDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterWaterDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterWaterDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterWaterDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterWaterDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterFoodDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterStaminaDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterStaminaDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterStaminaDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterStaminaDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterStaminaDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterHealthRecoveryMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterHealthRecoveryMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterHealthRecoveryMultiplier
        {
            get { return (float)GetValue(PlayerCharacterHealthRecoveryMultiplierProperty); }
            set { SetValue(PlayerCharacterHealthRecoveryMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestingDamageMultiplierPlayerProperty = DependencyProperty.Register(nameof(PlayerHarvestingDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PlayerHarvestingDamageMultiplier
        {
            get { return (float)GetValue(HarvestingDamageMultiplierPlayerProperty); }
            set { SetValue(HarvestingDamageMultiplierPlayerProperty, value); }
        }

        public static readonly DependencyProperty PlayerBaseStatMultipliersProperty = DependencyProperty.Register(nameof(PlayerBaseStatMultipliers), typeof(StatsMultiplierArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StatsMultiplierArray PlayerBaseStatMultipliers
        {
            get { return (StatsMultiplierArray)GetValue(PlayerBaseStatMultipliersProperty); }
            set { SetValue(PlayerBaseStatMultipliersProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_PlayerProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_Player), typeof(StatsMultiplierArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StatsMultiplierArray PerLevelStatsMultiplier_Player
        {
            get { return (StatsMultiplierArray)GetValue(PerLevelStatsMultiplier_PlayerProperty); }
            set { SetValue(PerLevelStatsMultiplier_PlayerProperty, value); }
        }

        public static readonly DependencyProperty CraftingSkillBonusMultiplierProperty = DependencyProperty.Register(nameof(CraftingSkillBonusMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CraftingSkillBonusMultiplier
        {
            get { return (float)GetValue(CraftingSkillBonusMultiplierProperty); }
            set { SetValue(CraftingSkillBonusMultiplierProperty, value); }
        }
        #endregion

        #region Dino Settings
        public static readonly DependencyProperty OverrideMaxExperiencePointsDinoProperty = DependencyProperty.Register(nameof(OverrideMaxExperiencePointsDino), typeof(int), typeof(ServerProfile), new PropertyMetadata(GameData.DEFAULT_MAX_EXPERIENCE_POINTS_DINO));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int OverrideMaxExperiencePointsDino
        {
            get { return (int)GetValue(OverrideMaxExperiencePointsDinoProperty); }
            set { SetValue(OverrideMaxExperiencePointsDinoProperty, value); }
        }

        public static readonly DependencyProperty DinoDamageMultiplierProperty = DependencyProperty.Register(nameof(DinoDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoDamageMultiplier
        {
            get { return (float)GetValue(DinoDamageMultiplierProperty); }
            set { SetValue(DinoDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoDamageMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamedDinoDamageMultiplier
        {
            get { return (float)GetValue(TamedDinoDamageMultiplierProperty); }
            set { SetValue(TamedDinoDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoResistanceMultiplierProperty = DependencyProperty.Register(nameof(DinoResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoResistanceMultiplier
        {
            get { return (float)GetValue(DinoResistanceMultiplierProperty); }
            set { SetValue(DinoResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoResistanceMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamedDinoResistanceMultiplier
        {
            get { return (float)GetValue(TamedDinoResistanceMultiplierProperty); }
            set { SetValue(TamedDinoResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty MaxTamedDinosProperty = DependencyProperty.Register(nameof(MaxTamedDinos), typeof(int), typeof(ServerProfile), new PropertyMetadata(4000));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int MaxTamedDinos
        {
            get { return (int)GetValue(MaxTamedDinosProperty); }
            set { SetValue(MaxTamedDinosProperty, value); }
        }

        public static readonly DependencyProperty DinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(DinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(DinoCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoCharacterStaminaDrainMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterStaminaDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterStaminaDrainMultiplier
        {
            get { return (float)GetValue(DinoCharacterStaminaDrainMultiplierProperty); }
            set { SetValue(DinoCharacterStaminaDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoCharacterHealthRecoveryMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterHealthRecoveryMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterHealthRecoveryMultiplier
        {
            get { return (float)GetValue(DinoCharacterHealthRecoveryMultiplierProperty); }
            set { SetValue(DinoCharacterHealthRecoveryMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoCountMultiplierProperty = DependencyProperty.Register(nameof(DinoCountMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCountMultiplier
        {
            get { return (float)GetValue(DinoCountMultiplierProperty); }
            set { SetValue(DinoCountMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestingDamageMultiplierDinoProperty = DependencyProperty.Register(nameof(DinoHarvestingDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(3.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float DinoHarvestingDamageMultiplier
        {
            get { return (float)GetValue(HarvestingDamageMultiplierDinoProperty); }
            set { SetValue(HarvestingDamageMultiplierDinoProperty, value); }
        }

        public static readonly DependencyProperty TurretDamageMultiplierDinoProperty = DependencyProperty.Register(nameof(DinoTurretDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float DinoTurretDamageMultiplier
        {
            get { return (float)GetValue(TurretDamageMultiplierDinoProperty); }
            set { SetValue(TurretDamageMultiplierDinoProperty, value); }
        }

        public static readonly DependencyProperty AllowRaidDinoFeedingProperty = DependencyProperty.Register(nameof(AllowRaidDinoFeeding), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowRaidDinoFeeding
        {
            get { return (bool)GetValue(AllowRaidDinoFeedingProperty); }
            set { SetValue(AllowRaidDinoFeedingProperty, value); }
        }

        public static readonly DependencyProperty RaidDinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(RaidDinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float RaidDinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(RaidDinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(RaidDinoCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EnableAllowCaveFlyersProperty = DependencyProperty.Register(nameof(EnableAllowCaveFlyers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAllowCaveFlyers
        {
            get { return (bool)GetValue(EnableAllowCaveFlyersProperty); }
            set { SetValue(EnableAllowCaveFlyersProperty, value); }
        }

        public static readonly DependencyProperty AllowFlyingStaminaRecoveryProperty = DependencyProperty.Register(nameof(AllowFlyingStaminaRecovery), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(AllowFlyingStaminaRecovery))]
        public bool AllowFlyingStaminaRecovery
        {
            get { return (bool)GetValue(AllowFlyingStaminaRecoveryProperty); }
            set { SetValue(AllowFlyingStaminaRecoveryProperty, value); }
        }

        public static readonly DependencyProperty PreventMateBoostProperty = DependencyProperty.Register(nameof(PreventMateBoost), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(PreventMateBoost))]
        public bool PreventMateBoost
        {
            get { return (bool)GetValue(PreventMateBoostProperty); }
            set { SetValue(PreventMateBoostProperty, value); }
        }

        public static readonly DependencyProperty DisableDinoDecayPvEProperty = DependencyProperty.Register(nameof(DisableDinoDecayPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DisableDinoDecayPvE
        {
            get { return (bool)GetValue(DisableDinoDecayPvEProperty); }
            set { SetValue(DisableDinoDecayPvEProperty, value); }
        }

        public static readonly DependencyProperty DisableDinoDecayPvPProperty = DependencyProperty.Register(nameof(DisableDinoDecayPvP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "PvPDinoDecay", InvertBoolean=true)]
        public bool DisableDinoDecayPvP
        {
            get { return (bool)GetValue(DisableDinoDecayPvPProperty); }
            set { SetValue(DisableDinoDecayPvPProperty, value); }
        }

        public static readonly DependencyProperty AutoDestroyDecayedDinosProperty = DependencyProperty.Register(nameof(AutoDestroyDecayedDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AutoDestroyDecayedDinos
        {
            get { return (bool)GetValue(AutoDestroyDecayedDinosProperty); }
            set { SetValue(AutoDestroyDecayedDinosProperty, value); }
        }

        public static readonly DependencyProperty PvEDinoDecayPeriodMultiplierProperty = DependencyProperty.Register(nameof(PvEDinoDecayPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEDinoDecayPeriodMultiplier
        {
            get { return (float)GetValue(PvEDinoDecayPeriodMultiplierProperty); }
            set { SetValue(PvEDinoDecayPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ForceFlyerExplosivesProperty = DependencyProperty.Register(nameof(ForceFlyerExplosives), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool ForceFlyerExplosives
        {
            get { return (bool)GetValue(ForceFlyerExplosivesProperty); }
            set { SetValue(ForceFlyerExplosivesProperty, value); }
        }

        public static readonly DependencyProperty AllowMultipleAttachedC4Property = DependencyProperty.Register(nameof(AllowMultipleAttachedC4), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(AllowMultipleAttachedC4))]
        public bool AllowMultipleAttachedC4
        {
            get { return (bool)GetValue(AllowMultipleAttachedC4Property); }
            set { SetValue(AllowMultipleAttachedC4Property, value); }
        }

        public static readonly DependencyProperty DisableDinoRidingProperty = DependencyProperty.Register(nameof(DisableDinoRiding), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableDinoRiding", ConditionedOn = nameof(DisableDinoRiding))]
        public bool DisableDinoRiding
        {
            get { return (bool)GetValue(DisableDinoRidingProperty); }
            set { SetValue(DisableDinoRidingProperty, value); }
        }

        public static readonly DependencyProperty DisableDinoTamingProperty = DependencyProperty.Register(nameof(DisableDinoTaming), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableDinoTaming", ConditionedOn = nameof(DisableDinoTaming))]
        public bool DisableDinoTaming
        {
            get { return (bool)GetValue(DisableDinoTamingProperty); }
            set { SetValue(DisableDinoTamingProperty, value); }
        }

        public static readonly DependencyProperty MaxPersonalTamedDinosProperty = DependencyProperty.Register(nameof(MaxPersonalTamedDinos), typeof(float), typeof(ServerProfile), new PropertyMetadata(40.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float MaxPersonalTamedDinos
        {
            get { return (float)GetValue(MaxPersonalTamedDinosProperty); }
            set { SetValue(MaxPersonalTamedDinosProperty, value); }
        }

        public static readonly DependencyProperty PersonalTamedDinosSaddleStructureCostProperty = DependencyProperty.Register(nameof(PersonalTamedDinosSaddleStructureCost), typeof(int), typeof(ServerProfile), new PropertyMetadata(19));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int PersonalTamedDinosSaddleStructureCost
        {
            get { return (int)GetValue(PersonalTamedDinosSaddleStructureCostProperty); }
            set { SetValue(PersonalTamedDinosSaddleStructureCostProperty, value); }
        }

        public static readonly DependencyProperty UseTameLimitForStructuresOnlyProperty = DependencyProperty.Register(nameof(UseTameLimitForStructuresOnly), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bUseTameLimitForStructuresOnly", ConditionedOn = nameof(UseTameLimitForStructuresOnly))]
        public bool UseTameLimitForStructuresOnly
        {
            get { return (bool)GetValue(UseTameLimitForStructuresOnlyProperty); }
            set { SetValue(UseTameLimitForStructuresOnlyProperty, value); }
        }

        public static readonly DependencyProperty DinoSettingsProperty = DependencyProperty.Register(nameof(DinoSettings), typeof(DinoSettingsList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        public DinoSettingsList DinoSettings
        {
            get { return (DinoSettingsList)GetValue(DinoSettingsProperty); }
            set { SetValue(DinoSettingsProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoWildProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoWild), typeof(StatsMultiplierArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StatsMultiplierArray PerLevelStatsMultiplier_DinoWild
        {
            get { return (StatsMultiplierArray)GetValue(PerLevelStatsMultiplier_DinoWildProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoWildProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamedProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed), typeof(StatsMultiplierArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StatsMultiplierArray PerLevelStatsMultiplier_DinoTamed
        {
            get { return (StatsMultiplierArray)GetValue(PerLevelStatsMultiplier_DinoTamedProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamedProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamed_AddProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed_Add), typeof(StatsMultiplierArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StatsMultiplierArray PerLevelStatsMultiplier_DinoTamed_Add
        {
            get { return (StatsMultiplierArray)GetValue(PerLevelStatsMultiplier_DinoTamed_AddProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamed_AddProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamed_AffinityProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), typeof(StatsMultiplierArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StatsMultiplierArray PerLevelStatsMultiplier_DinoTamed_Affinity
        {
            get { return (StatsMultiplierArray)GetValue(PerLevelStatsMultiplier_DinoTamed_AffinityProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamed_AffinityProperty, value); }
        }

        public static readonly DependencyProperty MatingIntervalMultiplierProperty = DependencyProperty.Register(nameof(MatingIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float MatingIntervalMultiplier
        {
            get { return (float)GetValue(MatingIntervalMultiplierProperty); }
            set { SetValue(MatingIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EggHatchSpeedMultiplierProperty = DependencyProperty.Register(nameof(EggHatchSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float EggHatchSpeedMultiplier
        {
            get { return (float)GetValue(EggHatchSpeedMultiplierProperty); }
            set { SetValue(EggHatchSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyMatureSpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyMatureSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyMatureSpeedMultiplier
        {
            get { return (float)GetValue(BabyMatureSpeedMultiplierProperty); }
            set { SetValue(BabyMatureSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyFoodConsumptionSpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyFoodConsumptionSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyFoodConsumptionSpeedMultiplier
        {
            get { return (float)GetValue(BabyFoodConsumptionSpeedMultiplierProperty); }
            set { SetValue(BabyFoodConsumptionSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DisableImprintDinoBuffProperty = DependencyProperty.Register(nameof(DisableImprintDinoBuff), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DisableImprintDinoBuff
        {
            get { return (bool)GetValue(DisableImprintDinoBuffProperty); }
            set { SetValue(DisableImprintDinoBuffProperty, value); }
        }

        public static readonly DependencyProperty AllowAnyoneBabyImprintCuddleProperty = DependencyProperty.Register(nameof(AllowAnyoneBabyImprintCuddle), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowAnyoneBabyImprintCuddle
        {
            get { return (bool)GetValue(AllowAnyoneBabyImprintCuddleProperty); }
            set { SetValue(AllowAnyoneBabyImprintCuddleProperty, value); }
        }

        public static readonly DependencyProperty BabyImprintingStatScaleMultiplierProperty = DependencyProperty.Register(nameof(BabyImprintingStatScaleMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyImprintingStatScaleMultiplier
        {
            get { return (float)GetValue(BabyImprintingStatScaleMultiplierProperty); }
            set { SetValue(BabyImprintingStatScaleMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyCuddleIntervalMultiplierProperty = DependencyProperty.Register(nameof(BabyCuddleIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyCuddleIntervalMultiplier
        {
            get { return (float)GetValue(BabyCuddleIntervalMultiplierProperty); }
            set { SetValue(BabyCuddleIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyCuddleGracePeriodMultiplierProperty = DependencyProperty.Register(nameof(BabyCuddleGracePeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyCuddleGracePeriodMultiplier
        {
            get { return (float)GetValue(BabyCuddleGracePeriodMultiplierProperty); }
            set { SetValue(BabyCuddleGracePeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyCuddleLoseImprintQualitySpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyCuddleLoseImprintQualitySpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyCuddleLoseImprintQualitySpeedMultiplier
        {
            get { return (float)GetValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty); }
            set { SetValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty, value); }
        }



        public static readonly DependencyProperty DinoSpawnsProperty = DependencyProperty.Register(nameof(DinoSpawnWeightMultipliers), typeof(AggregateIniValueList<DinoSpawn>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<DinoSpawn> DinoSpawnWeightMultipliers
        {
            get { return (AggregateIniValueList<DinoSpawn>)GetValue(DinoSpawnsProperty); }
            set { SetValue(DinoSpawnsProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoClassDamageMultipliersProperty = DependencyProperty.Register(nameof(TamedDinoClassDamageMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> TamedDinoClassDamageMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(TamedDinoClassDamageMultipliersProperty); }
            set { SetValue(TamedDinoClassDamageMultipliersProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoClassResistanceMultipliersProperty = DependencyProperty.Register(nameof(TamedDinoClassResistanceMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> TamedDinoClassResistanceMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(TamedDinoClassResistanceMultipliersProperty); }
            set { SetValue(TamedDinoClassResistanceMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DinoClassDamageMultipliersProperty = DependencyProperty.Register(nameof(DinoClassDamageMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> DinoClassDamageMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(DinoClassDamageMultipliersProperty); }
            set { SetValue(DinoClassDamageMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DinoClassResistanceMultipliersProperty = DependencyProperty.Register(nameof(DinoClassResistanceMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> DinoClassResistanceMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(DinoClassResistanceMultipliersProperty); }
            set { SetValue(DinoClassResistanceMultipliersProperty, value); }
        }

        public static readonly DependencyProperty NPCReplacementsProperty = DependencyProperty.Register(nameof(NPCReplacements), typeof(AggregateIniValueList<NPCReplacement>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<NPCReplacement> NPCReplacements
        {
            get { return (AggregateIniValueList<NPCReplacement>)GetValue(NPCReplacementsProperty); }
            set { SetValue(NPCReplacementsProperty, value); }
        }

        public static readonly DependencyProperty PreventDinoTameClassNamesProperty = DependencyProperty.Register(nameof(PreventDinoTameClassNames), typeof(StringIniValueList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StringIniValueList PreventDinoTameClassNames
        {
            get { return (StringIniValueList)GetValue(PreventDinoTameClassNamesProperty); }
            set { SetValue(PreventDinoTameClassNamesProperty, value); }
        }

        public static readonly DependencyProperty WildDinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(WildDinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float WildDinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(WildDinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(WildDinoCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float TamedDinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(TamedDinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(TamedDinoCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty WildDinoTorporDrainMultiplierProperty = DependencyProperty.Register(nameof(WildDinoTorporDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float WildDinoTorporDrainMultiplier
        {
            get { return (float)GetValue(WildDinoTorporDrainMultiplierProperty); }
            set { SetValue(WildDinoTorporDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoTorporDrainMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoTorporDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float TamedDinoTorporDrainMultiplier
        {
            get { return (float)GetValue(TamedDinoTorporDrainMultiplierProperty); }
            set { SetValue(TamedDinoTorporDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PassiveTameIntervalMultiplierProperty = DependencyProperty.Register(nameof(PassiveTameIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PassiveTameIntervalMultiplier
        {
            get { return (float)GetValue(PassiveTameIntervalMultiplierProperty); }
            set { SetValue(PassiveTameIntervalMultiplierProperty, value); }
        }
        #endregion

        #region Environment
        public static readonly DependencyProperty TamingSpeedMultiplierProperty = DependencyProperty.Register(nameof(TamingSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamingSpeedMultiplier
        {
            get { return (float)GetValue(TamingSpeedMultiplierProperty); }
            set { SetValue(TamingSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestAmountMultiplierProperty = DependencyProperty.Register(nameof(HarvestAmountMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float HarvestAmountMultiplier
        {
            get { return (float)GetValue(HarvestAmountMultiplierProperty); }
            set { SetValue(HarvestAmountMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ResourcesRespawnPeriodMultiplierProperty = DependencyProperty.Register(nameof(ResourcesRespawnPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float ResourcesRespawnPeriodMultiplier
        {
            get { return (float)GetValue(ResourcesRespawnPeriodMultiplierProperty); }
            set { SetValue(ResourcesRespawnPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ResourceNoReplenishRadiusPlayersProperty = DependencyProperty.Register(nameof(ResourceNoReplenishRadiusPlayers), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float ResourceNoReplenishRadiusPlayers
        {
            get { return (float)GetValue(ResourceNoReplenishRadiusPlayersProperty); }
            set { SetValue(ResourceNoReplenishRadiusPlayersProperty, value); }
        }

        public static readonly DependencyProperty ResourceNoReplenishRadiusStructuresProperty = DependencyProperty.Register(nameof(ResourceNoReplenishRadiusStructures), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float ResourceNoReplenishRadiusStructures
        {
            get { return (float)GetValue(ResourceNoReplenishRadiusStructuresProperty); }
            set { SetValue(ResourceNoReplenishRadiusStructuresProperty, value); }
        }

        public static readonly DependencyProperty HarvestHealthMultiplierProperty = DependencyProperty.Register(nameof(HarvestHealthMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float HarvestHealthMultiplier
        {
            get { return (float)GetValue(HarvestHealthMultiplierProperty); }
            set { SetValue(HarvestHealthMultiplierProperty, value); }
        }

        public static readonly DependencyProperty UseOptimizedHarvestingHealthProperty = DependencyProperty.Register(nameof(UseOptimizedHarvestingHealth), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(UseOptimizedHarvestingHealth))]
        public bool UseOptimizedHarvestingHealth
        {
            get { return (bool)GetValue(UseOptimizedHarvestingHealthProperty); }
            set { SetValue(UseOptimizedHarvestingHealthProperty, value); }
        }

        public static readonly DependencyProperty ClampResourceHarvestDamageProperty = DependencyProperty.Register(nameof(ClampResourceHarvestDamage), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(ClampResourceHarvestDamage))]
        public bool ClampResourceHarvestDamage
        {
            get { return (bool)GetValue(ClampResourceHarvestDamageProperty); }
            set { SetValue(ClampResourceHarvestDamageProperty, value); }
        }

        public static readonly DependencyProperty ClampItemSpoilingTimesProperty = DependencyProperty.Register(nameof(ClampItemSpoilingTimes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(ClampItemSpoilingTimes))]
        public bool ClampItemSpoilingTimes
        {
            get { return (bool)GetValue(ClampItemSpoilingTimesProperty); }
            set { SetValue(ClampItemSpoilingTimesProperty, value); }
        }

        public static readonly DependencyProperty HarvestResourceItemAmountClassMultipliersProperty = DependencyProperty.Register(nameof(HarvestResourceItemAmountClassMultipliers), typeof(AggregateIniValueList<ResourceClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ResourceClassMultiplier> HarvestResourceItemAmountClassMultipliers
        {
            get { return (AggregateIniValueList<ResourceClassMultiplier>)GetValue(HarvestResourceItemAmountClassMultipliersProperty); }
            set { SetValue(HarvestResourceItemAmountClassMultipliersProperty, value); }
        }

        public static readonly DependencyProperty BaseTemperatureMultiplierProperty = DependencyProperty.Register(nameof(BaseTemperatureMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BaseTemperatureMultiplier
        {
            get { return (float)GetValue(BaseTemperatureMultiplierProperty); }
            set { SetValue(BaseTemperatureMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DayCycleSpeedScaleProperty = DependencyProperty.Register(nameof(DayCycleSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DayCycleSpeedScale
        {
            get { return (float)GetValue(DayCycleSpeedScaleProperty); }
            set { SetValue(DayCycleSpeedScaleProperty, value); }
        }

        public static readonly DependencyProperty DayTimeSpeedScaleProperty = DependencyProperty.Register(nameof(DayTimeSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DayTimeSpeedScale
        {
            get { return (float)GetValue(DayTimeSpeedScaleProperty); }
            set { SetValue(DayTimeSpeedScaleProperty, value); }
        }

        public static readonly DependencyProperty NightTimeSpeedScaleProperty = DependencyProperty.Register(nameof(NightTimeSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float NightTimeSpeedScale
        {
            get { return (float)GetValue(NightTimeSpeedScaleProperty); }
            set { SetValue(NightTimeSpeedScaleProperty, value); }
        }

        public static readonly DependencyProperty GlobalSpoilingTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalSpoilingTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalSpoilingTimeMultiplier
        {
            get { return (float)GetValue(GlobalSpoilingTimeMultiplierProperty); }
            set { SetValue(GlobalSpoilingTimeMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GlobalCorpseDecompositionTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalCorpseDecompositionTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalItemDecompositionTimeMultiplier
        {
            get { return (float)GetValue(GlobalItemDecompositionTimeMultiplierProperty); }
            set { SetValue(GlobalItemDecompositionTimeMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GlobalItemDecompositionTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalItemDecompositionTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalCorpseDecompositionTimeMultiplier
        {
            get { return (float)GetValue(GlobalCorpseDecompositionTimeMultiplierProperty); }
            set { SetValue(GlobalCorpseDecompositionTimeMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CropDecaySpeedMultiplierProperty = DependencyProperty.Register(nameof(CropDecaySpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CropDecaySpeedMultiplier
        {
            get { return (float)GetValue(CropDecaySpeedMultiplierProperty); }
            set { SetValue(CropDecaySpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CropGrowthSpeedMultiplierProperty = DependencyProperty.Register(nameof(CropGrowthSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CropGrowthSpeedMultiplier
        {
            get { return (float)GetValue(CropGrowthSpeedMultiplierProperty); }
            set { SetValue(CropGrowthSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty LayEggIntervalMultiplierProperty = DependencyProperty.Register(nameof(LayEggIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float LayEggIntervalMultiplier
        {
            get { return (float)GetValue(LayEggIntervalMultiplierProperty); }
            set { SetValue(LayEggIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PoopIntervalMultiplierProperty = DependencyProperty.Register(nameof(PoopIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PoopIntervalMultiplier
        {
            get { return (float)GetValue(PoopIntervalMultiplierProperty); }
            set { SetValue(PoopIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HairGrowthSpeedMultiplierProperty = DependencyProperty.Register(nameof(HairGrowthSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float HairGrowthSpeedMultiplier
        {
            get { return (float)GetValue(HairGrowthSpeedMultiplierProperty); }
            set { SetValue(HairGrowthSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CraftXPMultiplierProperty = DependencyProperty.Register(nameof(CraftXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CraftXPMultiplier
        {
            get { return (float)GetValue(CraftXPMultiplierProperty); }
            set { SetValue(CraftXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GenericXPMultiplierProperty = DependencyProperty.Register(nameof(GenericXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GenericXPMultiplier
        {
            get { return (float)GetValue(GenericXPMultiplierProperty); }
            set { SetValue(GenericXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestXPMultiplierProperty = DependencyProperty.Register(nameof(HarvestXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float HarvestXPMultiplier
        {
            get { return (float)GetValue(HarvestXPMultiplierProperty); }
            set { SetValue(HarvestXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty KillXPMultiplierProperty = DependencyProperty.Register(nameof(KillXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float KillXPMultiplier
        {
            get { return (float)GetValue(KillXPMultiplierProperty); }
            set { SetValue(KillXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty SpecialXPMultiplierProperty = DependencyProperty.Register(nameof(SpecialXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float SpecialXPMultiplier
        {
            get { return (float)GetValue(SpecialXPMultiplierProperty); }
            set { SetValue(SpecialXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DisableWeatherFogProperty = DependencyProperty.Register(nameof(DisableWeatherFog), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DisableWeatherFog
        {
            get { return (bool)GetValue(DisableWeatherFogProperty); }
            set { SetValue(DisableWeatherFogProperty, value); }
        }
        #endregion

        #region Structures
        public static readonly DependencyProperty StructureResistanceMultiplierProperty = DependencyProperty.Register(nameof(StructureResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float StructureResistanceMultiplier
        {
            get { return (float)GetValue(StructureResistanceMultiplierProperty); }
            set { SetValue(StructureResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty StructureDamageMultiplierProperty = DependencyProperty.Register(nameof(StructureDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float StructureDamageMultiplier
        {
            get { return (float)GetValue(StructureDamageMultiplierProperty); }
            set { SetValue(StructureDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty StructureDamageRepairCooldownProperty = DependencyProperty.Register(nameof(StructureDamageRepairCooldown), typeof(int), typeof(ServerProfile), new PropertyMetadata(180));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int StructureDamageRepairCooldown
        {
            get { return (int)GetValue(StructureDamageRepairCooldownProperty); }
            set { SetValue(StructureDamageRepairCooldownProperty, value); }
        }

        public static readonly DependencyProperty PvPStructureDecayProperty = DependencyProperty.Register(nameof(PvPStructureDecay), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PvPStructureDecay
        {
            get { return (bool)GetValue(PvPStructureDecayProperty); }
            set { SetValue(PvPStructureDecayProperty, value); }
        }

        public static readonly DependencyProperty PvPZoneStructureDamageMultiplierProperty = DependencyProperty.Register(nameof(PvPZoneStructureDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(6.0f));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PvPZoneStructureDamageMultiplier
        {
            get { return (float)GetValue(PvPZoneStructureDamageMultiplierProperty); }
            set { SetValue(PvPZoneStructureDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty MaxStructuresVisibleProperty = DependencyProperty.Register(nameof(MaxStructuresVisible), typeof(float), typeof(ServerProfile), new PropertyMetadata(10500f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "TheMaxStructuresInRange")]
        public float MaxStructuresVisible
        {
            get { return (float)GetValue(MaxStructuresVisibleProperty); }
            set { SetValue(MaxStructuresVisibleProperty, value); }
        }

        public static readonly DependencyProperty PerPlatformMaxStructuresMultiplierProperty = DependencyProperty.Register(nameof(PerPlatformMaxStructuresMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PerPlatformMaxStructuresMultiplier
        {
            get { return (float)GetValue(PerPlatformMaxStructuresMultiplierProperty); }
            set { SetValue(PerPlatformMaxStructuresMultiplierProperty, value); }
        }

        public static readonly DependencyProperty MaxPlatformSaddleStructureLimitProperty = DependencyProperty.Register(nameof(MaxPlatformSaddleStructureLimit), typeof(int), typeof(ServerProfile), new PropertyMetadata(50));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int MaxPlatformSaddleStructureLimit
        {
            get { return (int)GetValue(MaxPlatformSaddleStructureLimitProperty); }
            set { SetValue(MaxPlatformSaddleStructureLimitProperty, value); }
        }

        public static readonly DependencyProperty OverrideStructurePlatformPreventionProperty = DependencyProperty.Register(nameof(OverrideStructurePlatformPrevention), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool OverrideStructurePlatformPrevention
        {
            get { return (bool)GetValue(OverrideStructurePlatformPreventionProperty); }
            set { SetValue(OverrideStructurePlatformPreventionProperty, value); }
        }

        public static readonly DependencyProperty FlyerPlatformAllowUnalignedDinoBasingProperty = DependencyProperty.Register(nameof(FlyerPlatformAllowUnalignedDinoBasing), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bFlyerPlatformAllowUnalignedDinoBasing")]
        public bool FlyerPlatformAllowUnalignedDinoBasing
        {
            get { return (bool)GetValue(FlyerPlatformAllowUnalignedDinoBasingProperty); }
            set { SetValue(FlyerPlatformAllowUnalignedDinoBasingProperty, value); }
        }

        public static readonly DependencyProperty PvEAllowStructuresAtSupplyDropsProperty = DependencyProperty.Register(nameof(PvEAllowStructuresAtSupplyDrops), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PvEAllowStructuresAtSupplyDrops
        {
            get { return (bool)GetValue(PvEAllowStructuresAtSupplyDropsProperty); }
            set { SetValue(PvEAllowStructuresAtSupplyDropsProperty, value); }
        }

        public static readonly DependencyProperty EnableStructureDecayPvEProperty = DependencyProperty.Register(nameof(EnableStructureDecayPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DisableStructureDecayPVE", InvertBoolean = true)]
        public bool EnableStructureDecayPvE
        {
            get { return (bool)GetValue(EnableStructureDecayPvEProperty); }
            set { SetValue(EnableStructureDecayPvEProperty, value); }
        }

        public static readonly DependencyProperty PvEStructureDecayDestructionPeriodProperty = DependencyProperty.Register(nameof(PvEStructureDecayDestructionPeriod), typeof(float), typeof(ServerProfile), new PropertyMetadata(0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableStructureDecayPvE))]
        public float PvEStructureDecayDestructionPeriod
        {
            get { return (float)GetValue(PvEStructureDecayDestructionPeriodProperty); }
            set { SetValue(PvEStructureDecayDestructionPeriodProperty, value); }
        }

        public static readonly DependencyProperty PvEStructureDecayPeriodMultiplierProperty = DependencyProperty.Register(nameof(PvEStructureDecayPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableStructureDecayPvE))]
        public float PvEStructureDecayPeriodMultiplier
        {
            get { return (float)GetValue(PvEStructureDecayPeriodMultiplierProperty); }
            set { SetValue(PvEStructureDecayPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty AutoDestroyOldStructuresMultiplierProperty = DependencyProperty.Register(nameof(AutoDestroyOldStructuresMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(0.0f));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float AutoDestroyOldStructuresMultiplier
        {
            get { return (float)GetValue(AutoDestroyOldStructuresMultiplierProperty); }
            set { SetValue(AutoDestroyOldStructuresMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ForceAllStructureLockingProperty = DependencyProperty.Register(nameof(ForceAllStructureLocking), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool ForceAllStructureLocking
        {
            get { return (bool)GetValue(ForceAllStructureLockingProperty); }
            set { SetValue(ForceAllStructureLockingProperty, value); }
        }

        public static readonly DependencyProperty PassiveDefensesDamageRiderlessDinosProperty = DependencyProperty.Register(nameof(PassiveDefensesDamageRiderlessDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPassiveDefensesDamageRiderlessDinos")]
        public bool PassiveDefensesDamageRiderlessDinos
        {
            get { return (bool)GetValue(PassiveDefensesDamageRiderlessDinosProperty); }
            set { SetValue(PassiveDefensesDamageRiderlessDinosProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoDestroyStructuresProperty = DependencyProperty.Register(nameof(EnableAutoDestroyStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableAutoDestroyStructures
        {
            get { return (bool)GetValue(EnableAutoDestroyStructuresProperty); }
            set { SetValue(EnableAutoDestroyStructuresProperty, value); }
        }

        public static readonly DependencyProperty OnlyAutoDestroyCoreStructuresProperty = DependencyProperty.Register(nameof(OnlyAutoDestroyCoreStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool OnlyAutoDestroyCoreStructures
        {
            get { return (bool)GetValue(OnlyAutoDestroyCoreStructuresProperty); }
            set { SetValue(OnlyAutoDestroyCoreStructuresProperty, value); }
        }

        public static readonly DependencyProperty OnlyDecayUnsnappedCoreStructuresProperty = DependencyProperty.Register(nameof(OnlyDecayUnsnappedCoreStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool OnlyDecayUnsnappedCoreStructures
        {
            get { return (bool)GetValue(OnlyDecayUnsnappedCoreStructuresProperty); }
            set { SetValue(OnlyDecayUnsnappedCoreStructuresProperty, value); }
        }

        public static readonly DependencyProperty FastDecayUnsnappedCoreStructuresProperty = DependencyProperty.Register(nameof(FastDecayUnsnappedCoreStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool FastDecayUnsnappedCoreStructures
        {
            get { return (bool)GetValue(FastDecayUnsnappedCoreStructuresProperty); }
            set { SetValue(FastDecayUnsnappedCoreStructuresProperty, value); }
        }

        public static readonly DependencyProperty DestroyUnconnectedWaterPipesProperty = DependencyProperty.Register(nameof(DestroyUnconnectedWaterPipes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DestroyUnconnectedWaterPipes
        {
            get { return (bool)GetValue(DestroyUnconnectedWaterPipesProperty); }
            set { SetValue(DestroyUnconnectedWaterPipesProperty, value); }
        }

        public static readonly DependencyProperty DisableStructurePlacementCollisionProperty = DependencyProperty.Register(nameof(DisableStructurePlacementCollision), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableStructurePlacementCollision")]
        public bool DisableStructurePlacementCollision
        {
            get { return (bool)GetValue(DisableStructurePlacementCollisionProperty); }
            set { SetValue(DisableStructurePlacementCollisionProperty, value); }
        }

        public static readonly DependencyProperty EnableFastDecayIntervalProperty = DependencyProperty.Register(nameof(EnableFastDecayInterval), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableFastDecayInterval
        {
            get { return (bool)GetValue(EnableFastDecayIntervalProperty); }
            set { SetValue(EnableFastDecayIntervalProperty, value); }
        }

        public static readonly DependencyProperty FastDecayIntervalProperty = DependencyProperty.Register(nameof(FastDecayInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(43200));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(EnableFastDecayInterval))]
        public int FastDecayInterval
        {
            get { return (int)GetValue(FastDecayIntervalProperty); }
            set { SetValue(FastDecayIntervalProperty, value); }
        }

        public static readonly DependencyProperty LimitTurretsInRangeProperty = DependencyProperty.Register(nameof(LimitTurretsInRange), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bLimitTurretsInRange")]
        public bool LimitTurretsInRange
        {
            get { return (bool)GetValue(LimitTurretsInRangeProperty); }
            set { SetValue(LimitTurretsInRangeProperty, value); }
        }

        public static readonly DependencyProperty LimitTurretsRangeProperty = DependencyProperty.Register(nameof(LimitTurretsRange), typeof(int), typeof(ServerProfile), new PropertyMetadata(10000));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(LimitTurretsInRange))]
        public int LimitTurretsRange
        {
            get { return (int)GetValue(LimitTurretsRangeProperty); }
            set { SetValue(LimitTurretsRangeProperty, value); }
        }

        public static readonly DependencyProperty LimitTurretsNumProperty = DependencyProperty.Register(nameof(LimitTurretsNum), typeof(int), typeof(ServerProfile), new PropertyMetadata(100));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(LimitTurretsInRange))]
        public int LimitTurretsNum
        {
            get { return (int)GetValue(LimitTurretsNumProperty); }
            set { SetValue(LimitTurretsNumProperty, value); }
        }

        public static readonly DependencyProperty HardLimitTurretsInRangeProperty = DependencyProperty.Register(nameof(HardLimitTurretsInRange), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bHardLimitTurretsInRange")]
        public bool HardLimitTurretsInRange
        {
            get { return (bool)GetValue(HardLimitTurretsInRangeProperty); }
            set { SetValue(HardLimitTurretsInRangeProperty, value); }
        }
        #endregion

        #region Engrams
        public static readonly DependencyProperty OnlyAllowSpecifiedEngramsProperty = DependencyProperty.Register(nameof(OnlyAllowSpecifiedEngrams), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bOnlyAllowSpecifiedEngrams", ConditionedOn = nameof(OnlyAllowSpecifiedEngrams))]
        public bool OnlyAllowSpecifiedEngrams
        {
            get { return (bool)GetValue(OnlyAllowSpecifiedEngramsProperty); }
            set { SetValue(OnlyAllowSpecifiedEngramsProperty, value); }
        }

        public static readonly DependencyProperty OverrideNamedEngramEntriesProperty = DependencyProperty.Register(nameof(OverrideNamedEngramEntries), typeof(EngramEntryList<EngramEntry>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public EngramEntryList<EngramEntry> OverrideNamedEngramEntries
        {
            get { return (EngramEntryList<EngramEntry>)GetValue(OverrideNamedEngramEntriesProperty); }
            set { SetValue(OverrideNamedEngramEntriesProperty, value); }
        }

        public static readonly DependencyProperty AutoUnlockAllEngramsProperty = DependencyProperty.Register(nameof(AutoUnlockAllEngrams), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bAutoUnlockAllEngrams", ConditionedOn = nameof(AutoUnlockAllEngrams))]
        public bool AutoUnlockAllEngrams
        {
            get { return (bool)GetValue(AutoUnlockAllEngramsProperty); }
            set { SetValue(AutoUnlockAllEngramsProperty, value); }
        }
        #endregion

        #region Crafting Overrides
        public static readonly DependencyProperty ConfigOverrideItemCraftingCostsProperty = DependencyProperty.Register(nameof(ConfigOverrideItemCraftingCosts), typeof(AggregateIniValueList<CraftingOverride>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<CraftingOverride> ConfigOverrideItemCraftingCosts
        {
            get { return (AggregateIniValueList<CraftingOverride>)GetValue(ConfigOverrideItemCraftingCostsProperty); }
            set { SetValue(ConfigOverrideItemCraftingCostsProperty, value); }
        }
        #endregion

        #region Custom Levels
        public static readonly DependencyProperty EnableLevelProgressionsProperty = DependencyProperty.Register(nameof(EnableLevelProgressions), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [XmlIgnore]
        //[DataMember]
        public bool EnableLevelProgressions
        {
            get { return (bool)GetValue(EnableLevelProgressionsProperty); }
            set { SetValue(EnableLevelProgressionsProperty, value); }
        }

        public static readonly DependencyProperty PlayerLevelsProperty = DependencyProperty.Register(nameof(PlayerLevels), typeof(LevelList), typeof(ServerProfile), new PropertyMetadata());
        [XmlIgnore]
        //[DataMember]
        public LevelList PlayerLevels
        {
            get { return (LevelList)GetValue(PlayerLevelsProperty); }
            set { SetValue(PlayerLevelsProperty, value); }
        }

        public static readonly DependencyProperty DinoLevelsProperty = DependencyProperty.Register(nameof(DinoLevels), typeof(LevelList), typeof(ServerProfile), new PropertyMetadata());
        [XmlIgnore]
        //[DataMember]
        public LevelList DinoLevels
        {
            get { return (LevelList)GetValue(DinoLevelsProperty); }
            set { SetValue(DinoLevelsProperty, value); }
        }
        #endregion

        #region Custom Settings
        public static readonly DependencyProperty CustomGameUserSettingsSectionsProperty = DependencyProperty.Register(nameof(CustomGameUserSettingsSections), typeof(CustomSectionList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Custom)]
        public CustomSectionList CustomGameUserSettingsSections
        {
            get { return (CustomSectionList)GetValue(CustomGameUserSettingsSectionsProperty); }
            set { SetValue(CustomGameUserSettingsSectionsProperty, value); }
        }
        #endregion

        #region Server Files
        public static readonly DependencyProperty EnableExclusiveJoinProperty = DependencyProperty.Register(nameof(EnableExclusiveJoin), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool EnableExclusiveJoin
        {
            get { return (bool)GetValue(EnableExclusiveJoinProperty); }
            set { SetValue(EnableExclusiveJoinProperty, value); }
        }
        #endregion

        #region Procedurally Generated ARKS
        // ReSharper disable InconsistentNaming
        public static readonly DependencyProperty PGM_EnabledProperty = DependencyProperty.Register(nameof(PGM_Enabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool PGM_Enabled
        {
            get { return (bool)GetValue(PGM_EnabledProperty); }
            set { SetValue(PGM_EnabledProperty, value); }
        }

        public static readonly DependencyProperty PGM_NameProperty = DependencyProperty.Register(nameof(PGM_Name), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultPGMapName));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "PGMapName", ConditionedOn = nameof(PGM_Enabled))]
        public string PGM_Name
        {
            get { return (string)GetValue(PGM_NameProperty); }
            set { SetValue(PGM_NameProperty, value); }
        }

        public static readonly DependencyProperty PGM_TerrainProperty = DependencyProperty.Register(nameof(PGM_Terrain), typeof(PGMTerrain), typeof(ServerProfile), new PropertyMetadata(null));
        [DataMember]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "PGTerrainPropertiesString", ConditionedOn = nameof(PGM_Enabled))]
        public PGMTerrain PGM_Terrain
        {
            get { return (PGMTerrain)GetValue(PGM_TerrainProperty); }
            set { SetValue(PGM_TerrainProperty, value); }
        }
        // ReSharper restore InconsistentNaming
        #endregion

        #region NPC Spawn Overrides
        public static readonly DependencyProperty ConfigAddNPCSpawnEntriesContainerProperty = DependencyProperty.Register(nameof(ConfigAddNPCSpawnEntriesContainer), typeof(NPCSpawnContainerList<NPCSpawnContainer>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public NPCSpawnContainerList<NPCSpawnContainer> ConfigAddNPCSpawnEntriesContainer
        {
            get { return (NPCSpawnContainerList<NPCSpawnContainer>)GetValue(ConfigAddNPCSpawnEntriesContainerProperty); }
            set { SetValue(ConfigAddNPCSpawnEntriesContainerProperty, value); }
        }

        public static readonly DependencyProperty ConfigSubtractNPCSpawnEntriesContainerProperty = DependencyProperty.Register(nameof(ConfigSubtractNPCSpawnEntriesContainer), typeof(NPCSpawnContainerList<NPCSpawnContainer>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public NPCSpawnContainerList<NPCSpawnContainer> ConfigSubtractNPCSpawnEntriesContainer
        {
            get { return (NPCSpawnContainerList<NPCSpawnContainer>)GetValue(ConfigSubtractNPCSpawnEntriesContainerProperty); }
            set { SetValue(ConfigSubtractNPCSpawnEntriesContainerProperty, value); }
        }

        public static readonly DependencyProperty ConfigOverrideNPCSpawnEntriesContainerProperty = DependencyProperty.Register(nameof(ConfigOverrideNPCSpawnEntriesContainer), typeof(NPCSpawnContainerList<NPCSpawnContainer>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public NPCSpawnContainerList<NPCSpawnContainer> ConfigOverrideNPCSpawnEntriesContainer
        {
            get { return (NPCSpawnContainerList<NPCSpawnContainer>)GetValue(ConfigOverrideNPCSpawnEntriesContainerProperty); }
            set { SetValue(ConfigOverrideNPCSpawnEntriesContainerProperty, value); }
        }

        public static readonly DependencyProperty NPCSpawnSettingsProperty = DependencyProperty.Register(nameof(NPCSpawnSettings), typeof(NPCSpawnSettingsList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        public NPCSpawnSettingsList NPCSpawnSettings
        {
            get { return (NPCSpawnSettingsList)GetValue(NPCSpawnSettingsProperty); }
            set { SetValue(NPCSpawnSettingsProperty, value); }
        }
        #endregion

        #region Supply Drop Overrides
        public static readonly DependencyProperty ConfigOverrideSupplyCrateItemsProperty = DependencyProperty.Register(nameof(ConfigOverrideSupplyCrateItems), typeof(SupplyCrateOverrideList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public SupplyCrateOverrideList ConfigOverrideSupplyCrateItems
        {
            get { return (SupplyCrateOverrideList)GetValue(ConfigOverrideSupplyCrateItemsProperty); }
            set { SetValue(ConfigOverrideSupplyCrateItemsProperty, value); }
        }
        #endregion

        #region Survival of the Fittest
        public static readonly DependencyProperty SOTF_EnabledProperty = DependencyProperty.Register(nameof(SOTF_Enabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_Enabled
        {
            get { return (bool)GetValue(SOTF_EnabledProperty); }
            set { SetValue(SOTF_EnabledProperty, value); }
        }

        public static readonly DependencyProperty SOTF_DisableDeathSPectatorProperty = DependencyProperty.Register(nameof(SOTF_DisableDeathSPectator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_DisableDeathSPectator
        {
            get { return (bool)GetValue(SOTF_DisableDeathSPectatorProperty); }
            set { SetValue(SOTF_DisableDeathSPectatorProperty, value); }
        }

        public static readonly DependencyProperty SOTF_OnlyAdminRejoinAsSpectatorProperty = DependencyProperty.Register(nameof(SOTF_OnlyAdminRejoinAsSpectator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_OnlyAdminRejoinAsSpectator
        {
            get { return (bool)GetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty); }
            set { SetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty, value); }
        }

        public static readonly DependencyProperty SOTF_GamePlayLoggingProperty = DependencyProperty.Register(nameof(SOTF_GamePlayLogging), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_GamePlayLogging
        {
            get { return (bool)GetValue(SOTF_GamePlayLoggingProperty); }
            set { SetValue(SOTF_GamePlayLoggingProperty, value); }
        }

        public static readonly DependencyProperty SOTF_OutputGameReportProperty = DependencyProperty.Register(nameof(SOTF_OutputGameReport), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_OutputGameReport
        {
            get { return (bool)GetValue(SOTF_OutputGameReportProperty); }
            set { SetValue(SOTF_OutputGameReportProperty, value); }
        }

        public static readonly DependencyProperty SOTF_MaxNumberOfPlayersInTribeProperty = DependencyProperty.Register(nameof(SOTF_MaxNumberOfPlayersInTribe), typeof(int), typeof(ServerProfile), new PropertyMetadata(2));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "MaxNumberOfPlayersInTribe", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_MaxNumberOfPlayersInTribe
        {
            get { return (int)GetValue(SOTF_MaxNumberOfPlayersInTribeProperty); }
            set { SetValue(SOTF_MaxNumberOfPlayersInTribeProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleNumOfTribesToStartGameProperty = DependencyProperty.Register(nameof(SOTF_BattleNumOfTribesToStartGame), typeof(int), typeof(ServerProfile), new PropertyMetadata(15));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleNumOfTribesToStartGame", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleNumOfTribesToStartGame
        {
            get { return (int)GetValue(SOTF_BattleNumOfTribesToStartGameProperty); }
            set { SetValue(SOTF_BattleNumOfTribesToStartGameProperty, value); }
        }

        public static readonly DependencyProperty SOTF_TimeToCollapseRODProperty = DependencyProperty.Register(nameof(SOTF_TimeToCollapseROD), typeof(int), typeof(ServerProfile), new PropertyMetadata(9000));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "TimeToCollapseROD", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_TimeToCollapseROD
        {
            get { return (int)GetValue(SOTF_TimeToCollapseRODProperty); }
            set { SetValue(SOTF_TimeToCollapseRODProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleAutoStartGameIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleAutoStartGameInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleAutoStartGameInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleAutoStartGameInterval
        {
            get { return (int)GetValue(SOTF_BattleAutoStartGameIntervalProperty); }
            set { SetValue(SOTF_BattleAutoStartGameIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleAutoRestartGameIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleAutoRestartGameInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(45));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleAutoRestartGameInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleAutoRestartGameInterval
        {
            get { return (int)GetValue(SOTF_BattleAutoRestartGameIntervalProperty); }
            set { SetValue(SOTF_BattleAutoRestartGameIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleSuddenDeathIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleSuddenDeathInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(300));
        [DataMember]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleSuddenDeathInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleSuddenDeathInterval
        {
            get { return (int)GetValue(SOTF_BattleSuddenDeathIntervalProperty); }
            set { SetValue(SOTF_BattleSuddenDeathIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_NoEventsProperty = DependencyProperty.Register(nameof(SOTF_NoEvents), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_NoEvents
        {
            get { return (bool)GetValue(SOTF_NoEventsProperty); }
            set { SetValue(SOTF_NoEventsProperty, value); }
        }

        public static readonly DependencyProperty SOTF_NoBossesProperty = DependencyProperty.Register(nameof(SOTF_NoBosses), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_NoBosses
        {
            get { return (bool)GetValue(SOTF_NoBossesProperty); }
            set { SetValue(SOTF_NoBossesProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BothBossesProperty = DependencyProperty.Register(nameof(SOTF_BothBosses), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool SOTF_BothBosses
        {
            get { return (bool)GetValue(SOTF_BothBossesProperty); }
            set { SetValue(SOTF_BothBossesProperty, value); }
        }

        public static readonly DependencyProperty SOTF_EvoEventIntervalProperty = DependencyProperty.Register(nameof(SOTF_EvoEventInterval), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        public float SOTF_EvoEventInterval
        {
            get { return (float)GetValue(SOTF_EvoEventIntervalProperty); }
            set { SetValue(SOTF_EvoEventIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_RingStartTimeProperty = DependencyProperty.Register(nameof(SOTF_RingStartTime), typeof(float), typeof(ServerProfile), new PropertyMetadata(1000.0f));
        [DataMember]
        public float SOTF_RingStartTime
        {
            get { return (float)GetValue(SOTF_RingStartTimeProperty); }
            set { SetValue(SOTF_RingStartTimeProperty, value); }
        }
        #endregion

        #region Ragnarok
        public static readonly DependencyProperty Ragnarok_EnabledProperty = DependencyProperty.Register(nameof(Ragnarok_Enabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        public bool Ragnarok_Enabled
        {
            get { return (bool)GetValue(Ragnarok_EnabledProperty); }
            set { SetValue(Ragnarok_EnabledProperty, value); }
        }

        public static readonly DependencyProperty Ragnarok_AllowMultipleTamedUnicornsProperty = DependencyProperty.Register(nameof(Ragnarok_AllowMultipleTamedUnicorns), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        //[IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Ragnarok, Key = "AllowMultipleTamedUnicorns", ConditionedOn = nameof(Ragnarok_Enabled))]
        public bool Ragnarok_AllowMultipleTamedUnicorns
        {
            get { return (bool)GetValue(Ragnarok_AllowMultipleTamedUnicornsProperty); }
            set { SetValue(Ragnarok_AllowMultipleTamedUnicornsProperty, value); }
        }

        public static readonly DependencyProperty Ragnarok_UnicornSpawnIntervalProperty = DependencyProperty.Register(nameof(Ragnarok_UnicornSpawnInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(24));
        [DataMember]
        //[IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Ragnarok, Key = "UnicornSpawnInterval", ConditionedOn = nameof(Ragnarok_Enabled))]
        public int Ragnarok_UnicornSpawnInterval
        {
            get { return (int)GetValue(Ragnarok_UnicornSpawnIntervalProperty); }
            set { SetValue(Ragnarok_UnicornSpawnIntervalProperty, value); }
        }

        public static readonly DependencyProperty Ragnarok_DisableVolcanoProperty = DependencyProperty.Register(nameof(Ragnarok_DisableVolcano), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        //[IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Ragnarok, Key = "DisableVolcano", ConditionedOn = nameof(Ragnarok_Enabled))]
        public bool Ragnarok_DisableVolcano
        {
            get { return (bool)GetValue(Ragnarok_DisableVolcanoProperty); }
            set { SetValue(Ragnarok_DisableVolcanoProperty, value); }
        }

        public static readonly DependencyProperty Ragnarok_VolcanoIntensityProperty = DependencyProperty.Register(nameof(Ragnarok_VolcanoIntensity), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [DataMember]
        //[IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Ragnarok, Key = "VolcanoIntensity", ConditionedOn = nameof(Ragnarok_Enabled))]
        public float Ragnarok_VolcanoIntensity
        {
            get { return (float)GetValue(Ragnarok_VolcanoIntensityProperty); }
            set { SetValue(Ragnarok_VolcanoIntensityProperty, value); }
        }

        public static readonly DependencyProperty Ragnarok_VolcanoIntervalProperty = DependencyProperty.Register(nameof(Ragnarok_VolcanoInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [DataMember]
        //[IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Ragnarok, Key = "VolcanoInterval", ConditionedOn = nameof(Ragnarok_Enabled))]
        public int Ragnarok_VolcanoInterval
        {
            get { return (int)GetValue(Ragnarok_VolcanoIntervalProperty); }
            set { SetValue(Ragnarok_VolcanoIntervalProperty, value); }
        }

        public static readonly DependencyProperty Ragnarok_EnableDevelopmentZonesProperty = DependencyProperty.Register(nameof(Ragnarok_EnableDevelopmentZones), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [DataMember]
        //[IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Ragnarok, Key = "EnableDevelopmentZones", ConditionedOn = nameof(Ragnarok_Enabled))]
        public bool Ragnarok_EnableDevelopmentZones
        {
            get { return (bool)GetValue(Ragnarok_EnableDevelopmentZonesProperty); }
            set { SetValue(Ragnarok_EnableDevelopmentZonesProperty, value); }
        }
        #endregion

        #region RCON
        public static readonly DependencyProperty RCONWindowExtentsProperty = DependencyProperty.Register(nameof(RCONWindowExtents), typeof(Rect), typeof(ServerProfile), new PropertyMetadata(new Rect(0f, 0f, 0f, 0f)));
        [DataMember]
        public Rect RCONWindowExtents
        {
            get { return (Rect)GetValue(RCONWindowExtentsProperty); }
            set { SetValue(RCONWindowExtentsProperty, value); }
        }
        #endregion

        #endregion

        #region Methods
        internal static ServerProfile FromDefaults()
        {
            var settings = new ServerProfile();
            settings.DinoSpawnWeightMultipliers.Reset();
            settings.TamedDinoClassResistanceMultipliers.Reset();
            settings.TamedDinoClassDamageMultipliers.Reset();
            settings.DinoClassResistanceMultipliers.Reset();
            settings.DinoClassDamageMultipliers.Reset();
            settings.HarvestResourceItemAmountClassMultipliers.Reset();
            settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
            settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
            settings.PerLevelStatsMultiplier_DinoTamed.Reset();
            settings.PerLevelStatsMultiplier_DinoTamed_Add.Reset();
            settings.PerLevelStatsMultiplier_DinoTamed_Affinity.Reset();
            settings.PerLevelStatsMultiplier_DinoWild.Reset();
            settings.PerLevelStatsMultiplier_Player.Reset();
            settings.PlayerBaseStatMultipliers.Reset();
            return settings;
        }

        private void GetDefaultDirectories()
        {
            if (!string.IsNullOrWhiteSpace(InstallDirectory))
                return;

            // get the root servers folder
            var installDirectory = Path.IsPathRooted(Config.Default.ServersInstallDir)
                                       ? Path.Combine(Config.Default.ServersInstallDir)
                                       : Path.Combine(Config.Default.DataDir, Config.Default.ServersInstallDir);
            var index = 1;
            while (true)
            {
                // create a test profile folder name
                var profileFolder = $"{Config.Default.DefaultServerFolderName}{index}";
                // get the test profile directory
                var profileDirectory = Path.Combine(installDirectory, profileFolder);

                // check if the profile directory exists
                if (!Directory.Exists(profileDirectory))
                {
                    // profile directory does not exist, assign the test profile directory to the profile
                    InstallDirectory = profileDirectory;
                    break;
                }

                index++;
            }
        }

        private static string[] GetExclusions()
        {
            var exclusions = new List<string>();

            if (!Config.Default.SectionCraftingOverridesEnabled)
            {
                exclusions.Add(nameof(ConfigOverrideItemCraftingCosts));
            }

            if (!Config.Default.SectionMapSpawnerOverridesEnabled)
            {
                exclusions.Add(nameof(ConfigAddNPCSpawnEntriesContainer));
                exclusions.Add(nameof(ConfigSubtractNPCSpawnEntriesContainer));
                exclusions.Add(nameof(ConfigOverrideNPCSpawnEntriesContainer));
                exclusions.Add(nameof(NPCSpawnSettings));
            }

            if (!Config.Default.SectionSupplyCrateOverridesEnabled)
            {
                exclusions.Add(nameof(ConfigOverrideSupplyCrateItems));
            }

            return exclusions.ToArray();
        }

        private LevelList GetLevelList(LevelProgression levelProgression)
        {
            LevelList list = null;
            switch (levelProgression)
            {
                case LevelProgression.Player:
                    list = this.PlayerLevels;
                    break;

                case LevelProgression.Dino:
                    list = this.DinoLevels;
                    break;

                default:
                    throw new ArgumentException("Invalid level progression type specified.");
            }
            return list;
        }

        public string GetLauncherFile()
        {
            return Path.Combine(this.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.LauncherFile);
        }

        public string GetProfileIniDir()
        {
            return Path.Combine(Path.GetDirectoryName(GetProfileFile()), this.ProfileName);
        }

        public string GetProfileKey()
        {
            return TaskSchedulerUtils.ComputeKey(this.InstallDirectory);
        }

        public string GetProfileFile()
        {
            return Path.Combine(Config.Default.ConfigDirectory, Path.ChangeExtension(this.ProfileName, Config.Default.ProfileExtension));
        }

        public string GetProfileFileNew()
        {
            return Path.Combine(Config.Default.ConfigDirectory, Path.ChangeExtension(this.ProfileName, Config.Default.ProfileExtensionNew));
        }

        public string GetServerAppId()
        {
            try
            {
                var appFile = Path.Combine(InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerAppIdFile);
                return File.Exists(appFile) ? File.ReadAllText(appFile).Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetServerExeFile()
        {
            return Path.Combine(this.InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerExe);
        }

        public string GetServerArgs()
        {
            var serverArgs = new StringBuilder();

            serverArgs.Append(GetProfileMapName(this));

            serverArgs.Append("?listen");

            // These are used to match the server to the profile.
            if (!string.IsNullOrWhiteSpace(this.ServerIP))
            {
                serverArgs.Append("?MultiHome=").Append(this.ServerIP);
            }
            serverArgs.Append("?Port=").Append(this.ServerConnectionPort);
            serverArgs.Append("?QueryPort=").Append(this.ServerPort);
            serverArgs.Append("?MaxPlayers=").Append(this.MaxPlayers);

            if (this.UseRawSockets)
            {
                serverArgs.Append("?bRawSockets");
            }

            if (this.ForceFlyerExplosives)
            {
                serverArgs.Append("?ForceFlyerExplosives=true");
            }

            if (!string.IsNullOrWhiteSpace(this.AltSaveDirectoryName))
            {
                serverArgs.Append($"?AltSaveDirectoryName={this.AltSaveDirectoryName}");
            }

            serverArgs.Append($"?AllowCrateSpawnsOnTopOfStructures={this.AllowCrateSpawnsOnTopOfStructures.ToString()}");

            if (this.SOTF_Enabled)
            {
                serverArgs.Append("?EvoEventInterval=").Append(this.SOTF_EvoEventInterval);
                serverArgs.Append("?RingStartTime=").Append(this.SOTF_RingStartTime);
            }

            if (!string.IsNullOrWhiteSpace(this.AdditionalArgs))
            {
                var addArgs = this.AdditionalArgs.TrimStart();
                if (!addArgs.StartsWith("?"))
                    serverArgs.Append(" ");
                serverArgs.Append(addArgs);
            }

            if (!string.IsNullOrWhiteSpace(this.TotalConversionModId))
            {
                serverArgs.Append($" -TotalConversionMod={this.TotalConversionModId}");
            }

            if (this.UseRawSockets && this.NoNetThreading)
            {
                serverArgs.Append(" -nonetthreading");
            }

            if (this.UseRawSockets && this.ForceNetThreading)
            {
                serverArgs.Append(" -forcenetthreading");
            }

            if (this.EnableAllowCaveFlyers)
            {
                serverArgs.Append(" -ForceAllowCaveFlyers");
            }

            if (this.EnableAutoDestroyStructures)
            {
                serverArgs.Append(" -AutoDestroyStructures");
            }

            if (this.EnableNoFishLoot)
            {
                serverArgs.Append(" -nofishloot");
            }

            if(this.SOTF_Enabled)
            {
                if (this.SOTF_OutputGameReport)
                {
                    serverArgs.Append(" -OutputGameReport");
                }

                if (this.SOTF_GamePlayLogging)
                {
                    serverArgs.Append(" -gameplaylogging");
                }

                if (this.SOTF_DisableDeathSPectator)
                {
                    serverArgs.Append(" -DisableDeathSpectator");
                }

                if(this.SOTF_OnlyAdminRejoinAsSpectator)
                {
                    serverArgs.Append(" -OnlyAdminRejoinAsSpectator");
                }

                if (this.SOTF_NoEvents)
                {
                    serverArgs.Append(" -noevents");
                }

                if (this.SOTF_NoBosses)
                {
                    serverArgs.Append(" -nobosses");
                }
                else if (this.SOTF_BothBosses)
                {
                    serverArgs.Append(" -bothbosses");
                }
            }

            if (!string.IsNullOrWhiteSpace(this.CrossArkClusterId))
            {
                serverArgs.Append($" -clusterid={this.CrossArkClusterId}");

                if (this.ClusterDirOverride)
                {
                    serverArgs.Append($" -ClusterDirOverride=\"{Config.Default.DataDir}\"");
                }

                if (this.NoTransferFromFiltering)
                {
                    serverArgs.Append(" -NoTransferFromFiltering");
                }
            }

            if (this.EnableWebAlarm)
            {
                serverArgs.Append(" -webalarm");
            }

            if (this.UseBattlEye)
            {
                serverArgs.Append(" -UseBattlEye");
            }

            if (!this.UseBattlEye)
            {
                serverArgs.Append(" -NoBattlEye");
            }

            if (this.DisableValveAntiCheatSystem)
            {
                serverArgs.Append(" -insecure");
            }

            if (this.DisableAntiSpeedHackDetection || this.SpeedHackBias == 0.0f)
            {
                serverArgs.Append(" -noantispeedhack");
            }
            else if (this.SpeedHackBias != 1.0f)
            {
                serverArgs.Append($" -speedhackbias={this.SpeedHackBias}f");
            }

            if (this.DisablePlayerMovePhysicsOptimization)
            {
                serverArgs.Append(" -nocombineclientmoves");
            }

            if (this.ForceRespawnDinos)
            {
                serverArgs.Append(" -forcerespawndinos");
            }

            if (this.EnableServerAdminLogs)
            {
                serverArgs.Append(" -servergamelog");
            }

            if (this.ServerAdminLogsIncludeTribeLogs)
            {
                serverArgs.Append(" -servergamelogincludetribelogs");
            }

            if (this.ServerRCONOutputTribeLogs)
            {
                serverArgs.Append(" -ServerRCONOutputTribeLogs");
            }

            if (this.NotifyAdminCommandsInChat)
            {
                serverArgs.Append(" -NotifyAdminCommandsInChat");
            }

            if (this.ForceDirectX10)
            {
                serverArgs.Append(" -d3d10");
            }

            if (this.ForceShaderModel4)
            {
                serverArgs.Append(" -sm4");
            }

            if (this.ForceLowMemory)
            {
                serverArgs.Append(" -lowmemory");
            }

            if (this.ForceNoManSky)
            {
                serverArgs.Append(" -nomansky");
            }

            if (this.UseAllAvailableCores)
            {
                serverArgs.Append(" -useallavailablecores");
            }

            if (this.UseCache)
            {
                serverArgs.Append(" -usecache");
            }

            if (this.UseOldSaveFormat)
            {
                serverArgs.Append(" -oldsaveformat");
            }

            if (this.UseNoMemoryBias)
            {
                serverArgs.Append(" -nomemorybias");
            }

            if (this.StasisKeepControllers)
            {
                serverArgs.Append(" -StasisKeepControllers");
            }

            if (this.UseNoHangDetection)
            {
                serverArgs.Append(" -NoHangDetection");
            }

            if (this.EnableExclusiveJoin)
            {
                serverArgs.Append(" -exclusivejoin");
            }

            if (this.ServerAllowAnsel)
            {
                serverArgs.Append(" -ServerAllowAnsel");
            }

            if (this.NoDinos)
            {
                serverArgs.Append(" -NoDinos");
            }

            serverArgs.Append(' ');
            serverArgs.Append(Config.Default.ServerCommandLineStandardArgs);

            return serverArgs.ToString();
        }

        public static ServerProfile LoadFrom(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                return null;

            if (Path.GetExtension(file) == Config.Default.ProfileExtension)
                return LoadFromProfileFile(file);

            if (Path.GetExtension(file) == Config.Default.ProfileExtensionNew)
                return LoadFromProfileFileNew(file);

            var filePath = Path.GetDirectoryName(file);
            if (!filePath.EndsWith(Config.Default.ServerConfigRelativePath))
                return null;

            ServerProfile settings = null;
            settings = LoadFromINIFiles(file, settings);
            settings.InstallDirectory = filePath.Replace(Config.Default.ServerConfigRelativePath, string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (settings.PlayerLevels.Count == 0)
            {
                settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
                settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
                settings.EnableLevelProgressions = false;
            }

            //
            // Since these are not inserted the normal way, we force a recomputation here.
            //
            settings.PlayerLevels.UpdateTotals();
            settings.DinoLevels.UpdateTotals();
            settings.DinoSettings.RenderToView();
            if (Config.Default.SectionMapSpawnerOverridesEnabled)
                settings.NPCSpawnSettings.RenderToView();
            if (Config.Default.SectionSupplyCrateOverridesEnabled)
                settings.ConfigOverrideSupplyCrateItems.RenderToView();

            settings._lastSaveLocation = file;
            settings.IsDirty = true;
            return settings;
        }

        private static ServerProfile LoadFromINIFiles(string file, ServerProfile settings)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                return null;

            var exclusions = GetExclusions();

            var iniPath = Path.GetDirectoryName(file);
            var iniFile = new SystemIniFile(iniPath);
            settings = settings ?? new ServerProfile();
            iniFile.Deserialize(settings, exclusions);

            var values = iniFile.ReadSection(IniFiles.Game, IniFileSections.GameMode);

            var levelRampOverrides = values.Where(s => s.StartsWith("LevelExperienceRampOverrides=")).ToArray();
            if (levelRampOverrides.Length > 0)
            {
                var engramPointOverrides = values.Where(s => s.StartsWith("OverridePlayerLevelEngramPoints="));

                settings.EnableLevelProgressions = true;
                settings.PlayerLevels = LevelList.FromINIValues(levelRampOverrides[0], engramPointOverrides);

                if (levelRampOverrides.Length > 1)
                {
                    settings.DinoLevels = LevelList.FromINIValues(levelRampOverrides[1], null);
                }
            }

            return settings;
        }

        public static ServerProfile LoadFromProfileFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                return null;

            if (Path.GetExtension(file) != Config.Default.ProfileExtension)
                return null;

            ServerProfile settings = null;
            XmlSerializer serializer = new XmlSerializer(typeof(ServerProfile));
            using (var reader = File.OpenRead(file))
            {
                settings = (ServerProfile)serializer.Deserialize(reader);
            }

            var profileIniPath = Path.Combine(Path.ChangeExtension(file, null), Config.Default.ServerGameUserSettingsConfigFile);
            var configIniPath = Path.Combine(settings.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameUserSettingsConfigFile);
            if (File.Exists(configIniPath))
            {
                settings = LoadFromINIFiles(configIniPath, settings);
            }
            else if (File.Exists(profileIniPath))
            {
                settings = LoadFromINIFiles(profileIniPath, settings);
            }

            if (settings.PlayerLevels.Count == 0)
            {
                settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
                settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
                settings.EnableLevelProgressions = false;
            }

            //
            // Since these are not inserted the normal way, we force a recomputation here.
            //
            settings.PlayerLevels.UpdateTotals();
            settings.DinoLevels.UpdateTotals();
            settings.DinoSettings.RenderToView();
            if (Config.Default.SectionMapSpawnerOverridesEnabled)
                settings.NPCSpawnSettings.RenderToView();
            if (Config.Default.SectionSupplyCrateOverridesEnabled)
                settings.ConfigOverrideSupplyCrateItems.RenderToView();

            settings._lastSaveLocation = file;
            settings.IsDirty = false;
            return settings;
        }

        public static ServerProfile LoadFromProfileFileNew(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                return null;

            if (Path.GetExtension(file) != Config.Default.ProfileExtensionNew)
                return null;

            ServerProfile settings = null;
            settings = JsonUtils.DeserializeFromFile<ServerProfile>(file);

            var profileIniPath = Path.Combine(Path.ChangeExtension(file, null), Config.Default.ServerGameUserSettingsConfigFile);
            var configIniPath = Path.Combine(settings.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameUserSettingsConfigFile);
            if (File.Exists(configIniPath))
            {
                settings = LoadFromINIFiles(configIniPath, settings);
            }
            else if (File.Exists(profileIniPath))
            {
                settings = LoadFromINIFiles(profileIniPath, settings);
            }

            if (settings.PlayerLevels.Count == 0)
            {
                settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
                settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
                settings.EnableLevelProgressions = false;
            }

            //
            // Since these are not inserted the normal way, we force a recomputation here.
            //
            settings.PlayerLevels.UpdateTotals();
            settings.DinoLevels.UpdateTotals();
            settings.DinoSettings.RenderToView();
            if (Config.Default.SectionMapSpawnerOverridesEnabled)
                settings.NPCSpawnSettings.RenderToView();
            if (Config.Default.SectionSupplyCrateOverridesEnabled)
                settings.ConfigOverrideSupplyCrateItems.RenderToView();

            settings._lastSaveLocation = file;
            settings.IsDirty = false;
            return settings;
        }

        public void Save(bool updateFolderPermissions, bool updateSchedules, ProgressDelegate progressCallback)
        {
            if (string.IsNullOrWhiteSpace(Config.Default.DataDir))
                return;

            progressCallback?.Invoke(0, "Saving...");

            if (SOTF_Enabled)
            {
                // ensure that the auto settings are switched off for SotF servers
                EnableAutoBackup = false;
                EnableAutoShutdown1 = false;
                RestartAfterShutdown1 = true;
                EnableAutoShutdown2 = false;
                RestartAfterShutdown2 = true;
                EnableAutoUpdate = false;
                AutoRestartIfShutdown = false;

                // ensure the procedurally generated settings are switched off for SotF servers
                PGM_Enabled = false;
            }

            if (!OverrideNamedEngramEntries.IsEnabled)
                OnlyAllowSpecifiedEngrams = false;
            OverrideNamedEngramEntries.OnlyAllowSelectedEngrams = OnlyAllowSpecifiedEngrams;

            // check if the launcher args override is enabled, but nothing has been defined in the launcher args
            if (LauncherArgsOverride && string.IsNullOrWhiteSpace(LauncherArgs))
                LauncherArgsOverride = false;

            // ensure that the extinction event date is cleared if the extinction event is disabled
            if (!EnableExtinctionEvent)
            {
                ClearValue(ExtinctionEventUTCProperty);
            }

            //// ensure that the Difficulty Override is reset when override is enabled
            //if (EnableDifficultyOverride)
            //{
            //    ClearValue(DifficultyOffsetProperty);
            //}

            // ensure that the MAX XP settings for player and dinos are set to the last custom level
            if (EnableLevelProgressions)
            {
                progressCallback?.Invoke(0, "Checking Player and Dino Max XP...");

                // dinos
                var list = GetLevelList(LevelProgression.Dino);
                var lastxp = (list == null || list.Count == 0) ? 0 : list[list.Count - 1].XPRequired;

                if (lastxp > OverrideMaxExperiencePointsDino)
                    OverrideMaxExperiencePointsDino = lastxp;

                // players
                list = GetLevelList(LevelProgression.Player);
                lastxp = (list == null || list.Count == 0) ? 0 : list[list.Count - 1].XPRequired;

                if (lastxp > OverrideMaxExperiencePointsPlayer)
                    OverrideMaxExperiencePointsPlayer = lastxp;
            }

            progressCallback?.Invoke(0, "Constructing Dino Information...");
            this.DinoSettings.RenderToModel();

            if (Config.Default.SectionMapSpawnerOverridesEnabled)
            {
                progressCallback?.Invoke(0, "Constructing Map Spawner Information...");
                this.NPCSpawnSettings.RenderToModel();
            }

            if (Config.Default.SectionSupplyCrateOverridesEnabled)
            {
                progressCallback?.Invoke(0, "Constructing Supply Crate Information...");
                this.ConfigOverrideSupplyCrateItems.RenderToModel();
            }

            //
            // Save the profile
            //
            progressCallback?.Invoke(0, "Saving Profile File...");
            SaveProfile();

            //
            // Write the INI files
            //
            progressCallback?.Invoke(0, "Saving Config Files...");
            SaveINIFiles();

            //
            // If this was a rename, remove the old profile after writing the new one.
            //
            if (!String.Equals(GetProfileFileNew(), this._lastSaveLocation, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(this._lastSaveLocation))
                        File.Delete(this._lastSaveLocation);

                    var profileFile = Path.ChangeExtension(this._lastSaveLocation, Config.Default.ProfileExtension);
                    if (File.Exists(profileFile))
                        File.Delete(profileFile);

                    var iniDir = Path.ChangeExtension(this._lastSaveLocation, null);
                    if (Directory.Exists(iniDir))
                        Directory.Delete(iniDir, recursive: true);
                }
                catch (IOException)
                {
                    // We tried...
                }

                this._lastSaveLocation = GetProfileFileNew();
            }

            progressCallback?.Invoke(0, "Saving Launcher File...");
            SaveLauncher();

            progressCallback?.Invoke(0, "Checking Web Alarm File...");
            UpdateWebAlarm();

            if (updateFolderPermissions)
            {
                progressCallback?.Invoke(0, "Checking Directory Permissions...");
                UpdateDirectoryPermissions();
            }

            if (updateSchedules)
            {
                progressCallback?.Invoke(0, "Checking Scheduled Tasks...");
                UpdateSchedules();
            }
        }

        public void SaveProfile()
        {
            //
            // Save the profile
            //
            var serializer = new XmlSerializer(this.GetType());
            using (var stream = File.Open(GetProfileFile(), FileMode.Create))
            {
                serializer.Serialize(stream, this);
            }

            JsonUtils.Serialize(this, GetProfileFileNew());
            this.IsDirty = false;
        }

        private void SaveLauncher()
        {
            var commandArgs = new StringBuilder();

            if (this.LauncherArgsOverride)
            {
                commandArgs.Append(this.LauncherArgs);
            }
            else
            {
                commandArgs.Append("start");
                commandArgs.Append($" \"{this.ProfileName}\"");

                if (string.IsNullOrWhiteSpace(this.LauncherArgs))
                {
                    commandArgs.Append(" /normal");
                }
                else
                {
                    var args = this.LauncherArgs.Trim();
                    commandArgs.Append(" ");
                    commandArgs.Append(args);
                }

                commandArgs.Append($" \"{GetServerExeFile()}\"");
                commandArgs.Append($" {GetServerArgs()}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(GetLauncherFile()));
            File.WriteAllText(GetLauncherFile(), commandArgs.ToString());
        }

        public void SaveINIFiles()
        {
            //
            // Save alongside the .profile
            //
            string profileIniDir = GetProfileIniDir();
            Directory.CreateDirectory(profileIniDir);
            SaveINIFile(profileIniDir);

            //
            // Save to the installation location
            //
            string configDir = Path.Combine(this.InstallDirectory, Config.Default.ServerConfigRelativePath);
            Directory.CreateDirectory(configDir);
            SaveINIFile(configDir);
        }

        private void SaveINIFile(string profileIniDir)
        {
            var exclusions = GetExclusions();

            var iniFile = new SystemIniFile(profileIniDir);
            iniFile.Serialize(this, exclusions);

            var values = iniFile.ReadSection(IniFiles.Game, IniFileSections.GameMode);

            var filteredValues = values.Where(s => !s.StartsWith("LevelExperienceRampOverrides=") && !s.StartsWith("OverridePlayerLevelEngramPoints=")).ToList();
            if (this.EnableLevelProgressions)
            {
                //
                // These must be added in this order: Player, then Dinos, per the ARK INI file format.
                //
                filteredValues.Add(this.PlayerLevels.ToINIValueForXP());
                filteredValues.Add(this.DinoLevels.ToINIValueForXP());
                filteredValues.AddRange(this.PlayerLevels.ToINIValuesForEngramPoints());
            }

            iniFile.WriteSection(IniFiles.Game, IniFileSections.GameMode, filteredValues.ToArray());
        }

        public bool UpdateDirectoryPermissions()
        {
            if (!SecurityUtils.IsAdministrator())
                return true;

            if (!SecurityUtils.SetDirectoryOwnershipForAllUsers(this.InstallDirectory))
            {
                Logger.Error($"Unable to set directory permissions for {this.InstallDirectory}.");
                return false;
            }

            return true;
        }

        public bool UpdateSchedules()
        {
            SaveLauncher();

            if (!SecurityUtils.IsAdministrator())
                return true;

            var taskKey = GetProfileKey();

            if(!TaskSchedulerUtils.ScheduleAutoStart(taskKey, null, this.EnableAutoStart, GetLauncherFile(), ProfileName, true))
            {
                return false;
            }

            TimeSpan shutdownTime;
            var command = Assembly.GetEntryAssembly().Location;
            if (!TaskSchedulerUtils.ScheduleAutoShutdown(taskKey, "#1", command, this.EnableAutoShutdown1 ? (TimeSpan.TryParseExact(this.AutoShutdownTime1, "g", null, out shutdownTime) ? shutdownTime : (TimeSpan?)null) : null, ProfileName, TaskSchedulerUtils.ShutdownType.Shutdown1))
            {
                return false;
            }

            if (!TaskSchedulerUtils.ScheduleAutoShutdown(taskKey, "#2", command, this.EnableAutoShutdown2 ? (TimeSpan.TryParseExact(this.AutoShutdownTime2, "g", null, out shutdownTime) ? shutdownTime : (TimeSpan?)null) : null, ProfileName, TaskSchedulerUtils.ShutdownType.Shutdown2))
            {
                return false;
            }

            return true;
        }

        private void UpdateWebAlarm()
        {
            var alarmPostCredentialsFile = Path.Combine(this.InstallDirectory, Config.Default.SavedRelativePath, Config.Default.WebAlarmFile);

            try
            {
                // check if the web alarm option is enabled.
                if (this.EnableWebAlarm)
                {
                    // check if the directory exists.
                    if (!Directory.Exists(Path.GetDirectoryName(alarmPostCredentialsFile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(alarmPostCredentialsFile));

                    var contents = new StringBuilder();
                    contents.AppendLine($"{this.WebAlarmKey}");
                    contents.AppendLine($"{this.WebAlarmUrl}");
                    File.WriteAllText(alarmPostCredentialsFile, contents.ToString());
                }
                else
                {
                    // check if the files exists and delete it.
                    if (File.Exists(alarmPostCredentialsFile))
                        File.Delete(alarmPostCredentialsFile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Web Alarm Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool Validate(bool forceValidate, out string validationMessage)
        {
            validationMessage = string.Empty;
            StringBuilder result = new StringBuilder();

            var appId = SOTF_Enabled ? Config.Default.AppId_SotF : Config.Default.AppId;

            if (forceValidate || Config.Default.ValidateProfileOnServerStart)
            {
                // build a list of mods to be processed
                var serverMapModId = GetProfileMapModId(this);
                var serverMapName = GetProfileMapName(this);
                var modIds = ModUtils.GetModIdList(ServerModIds);
                modIds = ModUtils.ValidateModList(modIds);

                var modIdList = new List<string>();
                if (!string.IsNullOrWhiteSpace(serverMapModId))
                    modIdList.Add(serverMapModId);
                if (!string.IsNullOrWhiteSpace(TotalConversionModId))
                    modIdList.Add(TotalConversionModId);
                modIdList.AddRange(modIds);

                modIdList = ModUtils.ValidateModList(modIdList);

                var modDetails = SteamUtils.GetSteamModDetails(modIdList);

                // check for map name.
                if (string.IsNullOrWhiteSpace(ServerMap))
                    result.AppendLine("The map name has not been entered.");

                // check if the server executable exists
                var serverFolder = Path.Combine(InstallDirectory, Config.Default.ServerBinaryRelativePath);
                var serverFile = Path.Combine(serverFolder, Config.Default.ServerExe);
                if (!Directory.Exists(serverFolder))
                    result.AppendLine("Server files have not been downloaded, server folder does not exist.");
                else if (!File.Exists(serverFile))
                    result.AppendLine($"Server files have not been downloaded properly, server executable file ({Config.Default.ServerExe}) does not exist.");
                else
                {
                    var serverAppId = GetServerAppId();
                    if (!serverAppId.Equals(appId))
                        result.AppendLine("The server files are for a different Ark application.");
                }

                // check if the map is a mod and confirm the map name.
                if (!string.IsNullOrWhiteSpace(serverMapModId))
                {
                    var modFolder = ModUtils.GetModPath(InstallDirectory, serverMapModId);
                    if (!Directory.Exists(modFolder))
                        result.AppendLine("Map mod has not been downloaded, mod folder does not exist.");
                    else if (!File.Exists($"{modFolder}.mod"))
                        result.AppendLine("Map mod has not been downloaded properly, mod file does not exist.");
                    else
                    {
                        var modType = ModUtils.GetModType(InstallDirectory, serverMapModId);
                        if (modType == ModUtils.MODTYPE_UNKNOWN)
                            result.AppendLine("Map mod has not been downloaded properly, mod file is invalid.");
                        else if (modType != ModUtils.MODTYPE_MAP)
                            result.AppendLine("The map mod is not a valid map mod.");
                        else
                        {
                            // do not process any mods that are not included in the mod list.
                            if (modIdList.Contains(serverMapModId))
                            {
                                var mapName = ModUtils.GetMapName(InstallDirectory, serverMapModId);
                                if (string.IsNullOrWhiteSpace(mapName))
                                    result.AppendLine("Map mod file does not exist or is invalid.");
                                else if (!mapName.Equals(serverMapName))
                                    result.AppendLine("The map name does not match the map mod's map name.");
                                else
                                {
                                    var modDetail = modDetails?.publishedfiledetails?.FirstOrDefault(d => d.publishedfileid.Equals(serverMapModId));
                                    if (modDetail != null)
                                    {
                                        if (!modDetail.consumer_app_id.Equals(appId))
                                            result.AppendLine("The map mod is for a different Ark application.");
                                        else
                                        {
                                            var modVersion = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(InstallDirectory, serverMapModId));
                                            if (!modVersion.Equals(modDetail.time_updated))
                                                result.AppendLine("The map mod is outdated.");
                                        }
                                    }
                                    else
                                        result.AppendLine("The map mod details could not be retrieved from steam.");
                                }
                            }
                        }
                    }
                }

                // check for a total conversion mod
                if (!string.IsNullOrWhiteSpace(TotalConversionModId))
                {
                    var modFolder = ModUtils.GetModPath(InstallDirectory, TotalConversionModId);
                    if (!Directory.Exists(modFolder))
                        result.AppendLine("Total conversion mod has not been downloaded, mod folder does not exist.");
                    else if (!File.Exists($"{modFolder}.mod"))
                        result.AppendLine("Total conversion mod has not been downloaded properly, mod file does not exist.");
                    else
                    {
                        var modType = ModUtils.GetModType(InstallDirectory, TotalConversionModId);
                        if (modType == ModUtils.MODTYPE_UNKNOWN)
                            result.AppendLine("Total conversion mod has not been downloaded properly, mod file is invalid.");
                        else if (modType != ModUtils.MODTYPE_TOTCONV)
                            result.AppendLine("The total conversion mod is not a valid total conversion mod.");
                        else
                        {
                            // do not process any mods that are not included in the mod list.
                            if (modIdList.Contains(TotalConversionModId))
                            {
                                var mapName = ModUtils.GetMapName(InstallDirectory, TotalConversionModId);
                                if (string.IsNullOrWhiteSpace(mapName))
                                    result.AppendLine("Total conversion mod file does not exist or is invalid.");
                                else if (!mapName.Equals(serverMapName))
                                    result.AppendLine("The map name does not match the total conversion mod's map name.");
                                else
                                {
                                    var modDetail = modDetails?.publishedfiledetails?.FirstOrDefault(d => d.publishedfileid.Equals(TotalConversionModId));
                                    if (modDetail != null)
                                    {
                                        if (!modDetail.consumer_app_id.Equals(appId))
                                            result.AppendLine("The total conversion mod is for a different Ark application.");
                                        else
                                        {
                                            var modVersion = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(InstallDirectory, TotalConversionModId));
                                            if (!modVersion.Equals(modDetail.time_updated))
                                                result.AppendLine("The total conversion mod is outdated.");
                                        }
                                    }
                                    else
                                        result.AppendLine("The total conversion mod details could not be retrieved from steam.");
                                }
                            }
                        }
                    }
                }

                // check for the mods
                foreach (var modId in modIds)
                {
                    var modFolder = ModUtils.GetModPath(InstallDirectory, modId);
                    if (!Directory.Exists(modFolder))
                        result.AppendLine($"Mod {modId} has not been downloaded, mod folder does not exist.");
                    else if (!File.Exists($"{modFolder}.mod"))
                        result.AppendLine($"Mod {modId} has not been downloaded properly, mod file does not exist.");
                    else
                    {
                        var modDetail = modDetails?.publishedfiledetails?.FirstOrDefault(d => d.publishedfileid.Equals(modId));
                        if (modDetail != null)
                        {
                            if (!modDetail.consumer_app_id.Equals(appId))
                                result.AppendLine($"Mod {modId} is for a different Ark application.");
                            else
                            {
                                var modVersion = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(InstallDirectory, modId));
                                if (modVersion == 0 || !modVersion.Equals(modDetail.time_updated))
                                    result.AppendLine($"Mod {modId} is outdated.");
                            }
                        }
                        else
                            result.AppendLine($"Mod {modId} details could not be retrieved from steam.");
                    }
                }

                //// check for cluster settings
                //if (!string.IsNullOrWhiteSpace(CrossArkClusterId) && !string.IsNullOrWhiteSpace(ServerPassword))
                //{
                //    // cluster server configured, and server has a password
                //    result.AppendLine("This server is setup in a cluster, but has a server password defined, this will prevent your players transfers. To setup correctly, remove the password and use the exclusive join option in the Server File Details.");
                //}
            }

            validationMessage = result.ToString();
            return string.IsNullOrWhiteSpace(validationMessage);
        }

        public string ToOutputString()
        {
            //
            // serializes the profile to a string
            //
            var result = new StringBuilder();
            var serializer = new XmlSerializer(this.GetType());
            using (var stream = new StringWriter(result))
            {
                serializer.Serialize(stream, this);
            }
            return result.ToString();
        }

        public string ToOutputStringNew()
        {
            //
            // serializes the profile to a string
            //
            return JsonUtils.Serialize<ServerProfile>(this);
        }

        public int RestoreSaveFiles(string restoreFile, bool isArchiveFile, bool restoreAll)
        {
            if (string.IsNullOrWhiteSpace(restoreFile) || !File.Exists(restoreFile))
                throw new FileNotFoundException("Backup file could not be found or does not exist.", restoreFile);

            var saveFolder = GetProfileSavePath(this);
            if (!Directory.Exists(saveFolder))
                throw new DirectoryNotFoundException($"The server save folder could not be found or does not exist.\r\n{saveFolder}");

            var mapName = GetProfileMapFileName(this);
            var worldFileName = $"{mapName}{Config.Default.MapExtension}";

            // check if the archive file contains the world save file at minimum
            if (isArchiveFile)
            {
                if (!ZipUtils.DoesFileExist(restoreFile, worldFileName))
                {
                    throw new Exception("The backup file does not contain the world save file.");
                }
            }

            // create a backup of the existing save folder
            var app = new ServerApp(true)
            {
                BackupWorldFile = false,
                DeleteOldServerBackupFiles = false,
                SendAlerts = false,
                SendEmails = false,
                OutputLogs = false
            };
            app.CreateServerBackupArchiveFile(null, ServerProfileSnapshot.Create(this));

            var worldFile = IOUtils.NormalizePath(Path.Combine(saveFolder, worldFileName));
            var restoreFileInfo = new FileInfo(restoreFile);
            var restoredFileCount = 0;

            if (isArchiveFile)
            {
                // create a list of files to be deleted
                var files = new List<string>();
                files.Add(worldFile);

                if (restoreAll)
                {
                    var saveFolderInfo = new DirectoryInfo(saveFolder);

                    // get the player files
                    var playerFileFilter = $"*{Config.Default.PlayerFileExtension}";
                    var playerFiles = saveFolderInfo.GetFiles(playerFileFilter, SearchOption.TopDirectoryOnly);
                    foreach (var playerFile in playerFiles)
                    {
                        files.Add(playerFile.FullName);
                    }

                    // get the tribe files
                    var tribeFileFilter = $"*{Config.Default.TribeFileExtension}";
                    var tribeFiles = saveFolderInfo.GetFiles(tribeFileFilter, SearchOption.TopDirectoryOnly);
                    foreach (var tribeFile in tribeFiles)
                    {
                        files.Add(tribeFile.FullName);
                    }

                    //// get the player images files
                    //var playerImageFileFilter = $"*{Config.Default.PlayerImageFileExtension}";
                    //var playerImageFiles = saveFolderInfo.GetFiles(playerImageFileFilter, SearchOption.TopDirectoryOnly);
                    //foreach (var playerImageFile in playerImageFiles)
                    //{
                    //    files.Add(playerImageFile.FullName);
                    //}
                }

                // delete the selected files
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // if unable to delete, do not bother
                    }
                }

                // restore the files from the backup
                if (restoreAll)
                {
                    restoredFileCount = ZipUtils.ExtractAllFiles(restoreFile, saveFolder);
                }
                else
                {
                    restoredFileCount = ZipUtils.ExtractAFile(restoreFile, worldFileName, saveFolder);
                }
            }
            else
            {
                // copy the selected file
                File.Copy(restoreFile, worldFile, true);
                File.SetCreationTime(worldFile, restoreFileInfo.CreationTime);
                File.SetLastWriteTime(worldFile, restoreFileInfo.LastWriteTime);
                File.SetLastAccessTime(worldFile, restoreFileInfo.LastAccessTime);

                restoredFileCount = 1;
            }

            return restoredFileCount;
        }

        public void ValidateServerName()
        {
            ServerNameLength = Encoding.UTF8.GetByteCount(ServerName);
            ServerNameLengthToLong = ServerNameLength > 50;
        }

        public void ValidateMOTD()
        {
            MOTDLength = Encoding.UTF8.GetByteCount(MOTD);
            MOTDLengthToLong = MOTDLength > 1000;
        }

        #region Export Methods
        public void ExportDinoLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            LevelList list = GetLevelList(LevelProgression.Dino);

            StringBuilder output = new StringBuilder();
            foreach (var level in list)
            {
                output.AppendLine($"{level.LevelIndex}{CSV_DELIMITER}{level.XPRequired}");
            }

            File.WriteAllText(fileName, output.ToString());
        }

        public void ExportPlayerLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            LevelList list = GetLevelList(LevelProgression.Player);

            StringBuilder output = new StringBuilder();
            foreach (var level in list)
            {
                output.AppendLine($"{level.LevelIndex}{CSV_DELIMITER}{level.XPRequired}{CSV_DELIMITER}{level.EngramPoints}");
            }

            File.WriteAllText(fileName, output.ToString());
        }
        #endregion

        #region Import Methods
        public void ImportDinoLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            if (!File.Exists(fileName))
                return;

            CsvParserOptions csvParserOptions = new CsvParserOptions(false, new[] { CSV_DELIMITER });
            CsvDinoLevelMapping csvMapper = new CsvDinoLevelMapping();
            CsvParser<ImportLevel> csvParser = new CsvParser<ImportLevel>(csvParserOptions, csvMapper);

            var result = csvParser.ReadFromFile(fileName, Encoding.ASCII).ToList();
            if (result.Any(r => !r.IsValid))
            {
                var error = result.First(r => r.Error != null);
                throw new Exception($"Import error occured in column {error.Error.ColumnIndex} with a value of {error.Error.Value}");
            }

            LevelList list = GetLevelList(LevelProgression.Dino);
            list.Clear();

            foreach (var level in result)
            {
                list.Add(level.Result.AsLevel());
            }

            list.UpdateTotals();
        }

        public void ImportPlayerLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            if (!File.Exists(fileName))
                return;

            CsvParserOptions csvParserOptions = new CsvParserOptions(false, new[] { CSV_DELIMITER });
            CsvPlayerLevelMapping csvMapper = new CsvPlayerLevelMapping();
            CsvParser<ImportLevel> csvParser = new CsvParser<ImportLevel>(csvParserOptions, csvMapper);

            var result = csvParser.ReadFromFile(fileName, Encoding.ASCII).ToList();
            if (result.Any(r => !r.IsValid))
            {
                var error = result.First(r => r.Error != null);
                throw new Exception($"Import error occured in column {error.Error.ColumnIndex} with a value of {error.Error.Value}");
            }

            LevelList list = GetLevelList(LevelProgression.Player);
            list.Clear();

            foreach (var level in result)
            {
                list.Add(level.Result.AsLevel());
            }

            list.UpdateTotals();
        }
        #endregion

        #region Reset Methods
        public void ClearLevelProgression(LevelProgression levelProgression)
        {
            var list = GetLevelList(levelProgression);
            list.Clear();
            list.Add(new Level { LevelIndex = 0, XPRequired = 1, EngramPoints = 0 });
            list.UpdateTotals();
        }

        public void ResetLevelProgressionToOfficial(LevelProgression levelProgression)
        {
            LevelList list = GetLevelList(levelProgression);

            list.Clear();

            switch (levelProgression)
            {
                case LevelProgression.Player:
                    list.AddRange(GameData.LevelProgressionPlayerOfficial);
                    break;
                case LevelProgression.Dino:
                    list.AddRange(GameData.LevelProgressionDinoOfficial);
                    break;
            }
        }

        public void ResetProfileId()
        {
            this.ProfileID = Guid.NewGuid().ToString();
        }

        public void RandomizePGMSettings()
        {
            var random = new Random(DateTime.Now.Millisecond);

            this.PGM_Terrain.MapSeed = random.Next(1, 999);

            this.PGM_Terrain.WaterFrequency = (float)Math.Round(random.NextDouble() * 5f, 5);
            this.PGM_Terrain.MountainsFrequency = (float)Math.Round(random.NextDouble() * 10f, 5);
            if (this.PGM_Terrain.MountainsFrequency < 3.0f)
                this.PGM_Terrain.MountainsFrequency += 3.0f;

            this.PGM_Terrain.MountainsSlope = (float)Math.Round(random.NextDouble() + 0.3f, 5);
            if (this.PGM_Terrain.MountainsSlope < 0.5f)
                this.PGM_Terrain.MountainsSlope += 0.5f;
            this.PGM_Terrain.MountainsHeight = (float)Math.Round(random.NextDouble() + 0.3f, 5);
            if (this.PGM_Terrain.MountainsHeight < 0.5f)
                this.PGM_Terrain.MountainsHeight += 0.5f;

            this.PGM_Terrain.SnowBiomeSize = (float)Math.Round(random.NextDouble(), 5);
            this.PGM_Terrain.RedWoodBiomeSize = (float)Math.Round(random.NextDouble(), 5);
            if (this.PGM_Terrain.RedWoodBiomeSize > 0.5f)
                this.PGM_Terrain.RedWoodBiomeSize -= 0.5f;

            this.PGM_Terrain.MountainBiomeStart = -(float)Math.Round(random.NextDouble(), 5);
            this.PGM_Terrain.JungleBiomeStart = -(float)Math.Round(random.NextDouble(), 5);

            this.PGM_Terrain.GrassDensity = (float)Math.Round(random.Next(80, 100) / 100.1f, 3);
            this.PGM_Terrain.JungleGrassDensity = (float)Math.Round(random.Next(2, 9) / 100.1f, 3);
            this.PGM_Terrain.MountainGrassDensity = (float)Math.Round(random.Next(3, 10) / 100.1f, 3);
            this.PGM_Terrain.RedwoodGrassDensity = (float)Math.Round(random.Next(5, 15) / 100.1f, 3);
            this.PGM_Terrain.SnowGrassDensity = (float)Math.Round(random.Next(10, 30) / 100.1f, 3);
            this.PGM_Terrain.SnowMountainGrassDensity = (float)Math.Round(random.Next(10, 20) / 100.1f, 3);

            this.PGM_Terrain.TreeDensity = (float)Math.Round(random.Next(11, 135) / 10000.1f, 3);
            this.PGM_Terrain.JungleTreeDensity = (float)Math.Round(random.Next(48, 83) / 100.1f, 3);
            this.PGM_Terrain.MountainsTreeDensity = (float)Math.Round(random.Next(9, 16) / 1000.1f, 3);
            this.PGM_Terrain.RedWoodTreeDensity = (float)Math.Round(random.Next(23, 51) / 100.1f, 3);
            this.PGM_Terrain.SnowTreeDensity = (float)Math.Round(random.Next(80, 100) / 100.1f, 3);
            this.PGM_Terrain.SnowMountainsTreeDensity = (float)Math.Round(random.Next(9, 16) / 1000.1f, 3);
            this.PGM_Terrain.ShoreTreeDensity = (float)Math.Round(random.Next(48, 83) / 1000.1f, 3);
            this.PGM_Terrain.SnowShoreTreeDensity = (float)Math.Round(random.Next(14, 31) / 1000.1f, 3);

            this.PGM_Terrain.InlandWaterObjectsDensity = (float)Math.Round(random.Next(36, 67) / 100.1f, 3);
            this.PGM_Terrain.UnderwaterObjectsDensity = (float)Math.Round(random.Next(36, 67) / 100.1f, 3);
        }

        // individual value reset methods
        public void ResetBanlist()
        {
            this.ClearValue(EnableBanListURLProperty);
            this.ClearValue(BanListURLProperty);
        }

        public void ResetMapName(string mapName)
        {
            this.ServerMap = mapName;
        }

        public void ResetOverrideMaxExperiencePointsPlayer()
        {
            this.ClearValue(OverrideMaxExperiencePointsPlayerProperty);
        }

        public void ResetOverrideMaxExperiencePointsDino()
        {
            this.ClearValue(OverrideMaxExperiencePointsDinoProperty);
        }

        public void ResetRCONWindowExtents()
        {
            this.ClearValue(RCONWindowExtentsProperty);
        }

        public void ResetServerOptions()
        {
            this.ClearValue(DisableValveAntiCheatSystemProperty);
            this.ClearValue(DisablePlayerMovePhysicsOptimizationProperty);
            this.ClearValue(DisableAntiSpeedHackDetectionProperty);
            this.ClearValue(SpeedHackBiasProperty);
            this.ClearValue(UseBattlEyeProperty);
            this.ClearValue(UseAllAvailableCoresProperty);
            this.ClearValue(UseCacheProperty);
            this.ClearValue(UseOldSaveFormatProperty);

            this.ClearValue(ForceRespawnDinosProperty);
            this.ClearValue(EnableServerAutoForceRespawnWildDinosIntervalProperty);
            this.ClearValue(ServerAutoForceRespawnWildDinosIntervalProperty);
            this.ClearValue(EnableServerAdminLogsProperty);
            this.ClearValue(MaxTribeLogsProperty);
            this.ClearValue(ForceDirectX10Property);
            this.ClearValue(ForceShaderModel4Property);
            this.ClearValue(ForceLowMemoryProperty);
            this.ClearValue(ForceNoManSkyProperty);
            this.ClearValue(UseNoMemoryBiasProperty);
            this.ClearValue(UseNoHangDetectionProperty);
            this.ClearValue(ServerAllowAnselProperty);
            this.ClearValue(NoDinosProperty);

            this.ClearValue(AltSaveDirectoryNameProperty);
            this.ClearValue(CrossArkClusterIdProperty);
            this.ClearValue(ClusterDirOverrideProperty);
        }

        public void ResetServerLogOptions()
        {
            this.ClearValue(EnableServerAdminLogsProperty);
            this.ClearValue(MaxTribeLogsProperty);
            this.ClearValue(ServerAdminLogsIncludeTribeLogsProperty);
            this.ClearValue(ServerRCONOutputTribeLogsProperty);
            this.ClearValue(NotifyAdminCommandsInChatProperty);
            this.ClearValue(TribeLogDestroyedEnemyStructuresProperty);
            this.ClearValue(AdminLoggingProperty);
            this.ClearValue(AllowHideDamageSourceFromLogsProperty);
        }

        // section reset methods
        public void ResetAdministrationSection()
        {
            this.ClearValue(ServerNameProperty);
            this.ClearValue(ServerPasswordProperty);
            this.ClearValue(AdminPasswordProperty);
            this.ClearValue(SpectatorPasswordProperty);

            this.ClearValue(ServerConnectionPortProperty);
            this.ClearValue(ServerPortProperty);
            this.ClearValue(ServerIPProperty);
            this.ClearValue(UseRawSocketsProperty);
            this.ClearValue(NoNetThreadingProperty);
            this.ClearValue(ForceNetThreadingProperty);

            this.ClearValue(EnableBanListURLProperty);
            this.ClearValue(BanListURLProperty);
            this.ClearValue(MaxPlayersProperty);
            this.ClearValue(EnableKickIdlePlayersProperty);
            this.ClearValue(KickIdlePlayersPeriodProperty);

            this.ClearValue(RCONEnabledProperty);
            this.ClearValue(RCONPortProperty);
            this.ClearValue(RCONServerGameLogBufferProperty);

            this.ClearValue(ServerMapProperty);
            this.ClearValue(TotalConversionModIdProperty);
            this.ClearValue(ServerModIdsProperty);

            this.ClearValue(EnableExtinctionEventProperty);
            this.ClearValue(ExtinctionEventTimeIntervalProperty);
            this.ClearValue(ExtinctionEventUTCProperty);

            this.ClearValue(AutoSavePeriodMinutesProperty);

            this.ClearValue(MOTDProperty);
            this.ClearValue(MOTDDurationProperty);

            ResetServerOptions();
            ResetServerLogOptions();

            this.ClearValue(EnableWebAlarmProperty);
            this.ClearValue(WebAlarmKeyProperty);
            this.ClearValue(WebAlarmUrlProperty);

            this.ClearValue(AdditionalArgsProperty);
        }

        public void ResetChatAndNotificationSection()
        {
            this.ClearValue(EnableGlobalVoiceChatProperty);
            this.ClearValue(EnableProximityChatProperty);
            this.ClearValue(EnablePlayerLeaveNotificationsProperty);
            this.ClearValue(EnablePlayerJoinedNotificationsProperty);
        }

        public void ResetCraftingOverridesSection()
        {
            this.ConfigOverrideItemCraftingCosts = new AggregateIniValueList<CraftingOverride>(nameof(ConfigOverrideItemCraftingCosts), null);
            this.ConfigOverrideItemCraftingCosts.Reset();
        }

        public void ResetCustomLevelsSection()
        {
            this.ClearValue(EnableLevelProgressionsProperty);

            this.PlayerLevels = new LevelList();
            this.ResetLevelProgressionToOfficial(LevelProgression.Player);

            this.DinoLevels = new LevelList();
            this.ResetLevelProgressionToOfficial(LevelProgression.Dino);
        }

        public void ResetDinoSettingsSection()
        {
            this.ClearValue(OverrideMaxExperiencePointsDinoProperty);
            this.ClearValue(DinoDamageMultiplierProperty);
            this.ClearValue(TamedDinoDamageMultiplierProperty);
            this.ClearValue(DinoResistanceMultiplierProperty);
            this.ClearValue(TamedDinoResistanceMultiplierProperty);
            this.ClearValue(MaxTamedDinosProperty);
            this.ClearValue(MaxPersonalTamedDinosProperty);
            this.ClearValue(PersonalTamedDinosSaddleStructureCostProperty);
            this.ClearValue(DinoCharacterFoodDrainMultiplierProperty);
            this.ClearValue(DinoCharacterStaminaDrainMultiplierProperty);
            this.ClearValue(DinoCharacterHealthRecoveryMultiplierProperty);
            this.ClearValue(DinoCountMultiplierProperty);
            this.ClearValue(HarvestingDamageMultiplierDinoProperty);
            this.ClearValue(TurretDamageMultiplierDinoProperty);

            this.ClearValue(AllowRaidDinoFeedingProperty);
            this.ClearValue(RaidDinoCharacterFoodDrainMultiplierProperty);

            this.ClearValue(EnableAllowCaveFlyersProperty);
            this.ClearValue(DisableDinoDecayPvEProperty);
            this.ClearValue(DisableDinoDecayPvPProperty);
            this.ClearValue(AutoDestroyDecayedDinosProperty);
            this.ClearValue(PvEDinoDecayPeriodMultiplierProperty);
            this.ClearValue(ForceFlyerExplosivesProperty);

            this.DinoSpawnWeightMultipliers = new AggregateIniValueList<DinoSpawn>(nameof(DinoSpawnWeightMultipliers), GameData.GetDinoSpawns);
            this.PreventDinoTameClassNames = new StringIniValueList(nameof(PreventDinoTameClassNames), () => new string[0]);
            this.NPCReplacements = new AggregateIniValueList<NPCReplacement>(nameof(NPCReplacements), GameData.GetNPCReplacements);
            this.TamedDinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.TamedDinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);
            this.DinoSettings.RenderToView();

            this.PerLevelStatsMultiplier_DinoWild = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_DinoWild, GameData.GetStatMultiplierInclusions_DinoWildPerLevel());
            this.PerLevelStatsMultiplier_DinoTamed = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed, GameData.GetStatMultiplierInclusions_DinoTamedPerLevel());
            this.PerLevelStatsMultiplier_DinoTamed_Add = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamedAdd, GameData.GetStatMultiplierInclusions_DinoTamedAdd());
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamedAffinity, GameData.GetStatMultiplierInclusions_DinoTamedAffinity());

            this.ClearValue(MatingIntervalMultiplierProperty);
            this.ClearValue(EggHatchSpeedMultiplierProperty);
            this.ClearValue(BabyMatureSpeedMultiplierProperty);
            this.ClearValue(BabyFoodConsumptionSpeedMultiplierProperty);

            this.ClearValue(DisableImprintDinoBuffProperty);
            this.ClearValue(AllowAnyoneBabyImprintCuddleProperty);
            this.ClearValue(BabyImprintingStatScaleMultiplierProperty);
            this.ClearValue(BabyCuddleIntervalMultiplierProperty);
            this.ClearValue(BabyCuddleGracePeriodMultiplierProperty);
            this.ClearValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty);
        }

        public void ResetEngramsSection()
        {
            this.ClearValue(AutoUnlockAllEngramsProperty);
            this.ClearValue(OnlyAllowSpecifiedEngramsProperty);

            this.OverrideNamedEngramEntries = new EngramEntryList<EngramEntry>(nameof(OverrideNamedEngramEntries), GameData.GetStandardEngramOverrides);
            this.OverrideNamedEngramEntries.Reset();
        }

        public void ResetEnvironmentSection()
        {
            this.ClearValue(TamingSpeedMultiplierProperty);
            this.ClearValue(HarvestAmountMultiplierProperty);
            this.ClearValue(ResourcesRespawnPeriodMultiplierProperty);
            this.ClearValue(ResourceNoReplenishRadiusPlayersProperty);
            this.ClearValue(ResourceNoReplenishRadiusStructuresProperty);
            this.ClearValue(ClampResourceHarvestDamageProperty);
            this.ClearValue(HarvestHealthMultiplierProperty);

            this.HarvestResourceItemAmountClassMultipliers = new AggregateIniValueList<ResourceClassMultiplier>(nameof(HarvestResourceItemAmountClassMultipliers), GameData.GetStandardResourceMultipliers);
            this.HarvestResourceItemAmountClassMultipliers.Reset();

            this.ClearValue(BaseTemperatureMultiplierProperty);
            this.ClearValue(DayCycleSpeedScaleProperty);
            this.ClearValue(DayTimeSpeedScaleProperty);
            this.ClearValue(NightTimeSpeedScaleProperty);
            this.ClearValue(DisableWeatherFogProperty);

            this.ClearValue(GlobalSpoilingTimeMultiplierProperty);
            this.ClearValue(GlobalItemDecompositionTimeMultiplierProperty);
            this.ClearValue(GlobalCorpseDecompositionTimeMultiplierProperty);
            this.ClearValue(CropDecaySpeedMultiplierProperty);
            this.ClearValue(CropGrowthSpeedMultiplierProperty);
            this.ClearValue(LayEggIntervalMultiplierProperty);
            this.ClearValue(PoopIntervalMultiplierProperty);
            this.ClearValue(HairGrowthSpeedMultiplierProperty);

            this.ClearValue(CraftXPMultiplierProperty);
            this.ClearValue(GenericXPMultiplierProperty);
            this.ClearValue(HarvestXPMultiplierProperty);
            this.ClearValue(KillXPMultiplierProperty);
            this.ClearValue(SpecialXPMultiplierProperty);
        }

        public void ResetHUDAndVisualsSection()
        {
            this.ClearValue(AllowCrosshairProperty);
            this.ClearValue(AllowHUDProperty);
            this.ClearValue(AllowThirdPersonViewProperty);
            this.ClearValue(AllowMapPlayerLocationProperty);
            this.ClearValue(AllowPVPGammaProperty);
            this.ClearValue(AllowPvEGammaProperty);
            this.ClearValue(ShowFloatingDamageTextProperty);
            this.ClearValue(AllowHitMarkersProperty);
        }

        public void ResetNPCSpawnOverridesSection()
        {
            this.ConfigAddNPCSpawnEntriesContainer = new NPCSpawnContainerList<NPCSpawnContainer>(nameof(ConfigAddNPCSpawnEntriesContainer), NPCSpawnContainerType.Add);
            this.ConfigSubtractNPCSpawnEntriesContainer = new NPCSpawnContainerList<NPCSpawnContainer>(nameof(ConfigSubtractNPCSpawnEntriesContainer), NPCSpawnContainerType.Subtract);
            this.ConfigOverrideNPCSpawnEntriesContainer = new NPCSpawnContainerList<NPCSpawnContainer>(nameof(ConfigOverrideNPCSpawnEntriesContainer), NPCSpawnContainerType.Override);
            this.ConfigAddNPCSpawnEntriesContainer.Reset();
            this.ConfigSubtractNPCSpawnEntriesContainer.Reset();
            this.ConfigOverrideNPCSpawnEntriesContainer.Reset();
            this.NPCSpawnSettings = new NPCSpawnSettingsList(this.ConfigAddNPCSpawnEntriesContainer, this.ConfigSubtractNPCSpawnEntriesContainer, this.ConfigOverrideNPCSpawnEntriesContainer);
        }

        public void ResetPGMSection()
        {
            this.ClearValue(PGM_EnabledProperty);
            this.ClearValue(PGM_NameProperty);
            this.PGM_Terrain = new PGMTerrain();
        }

        public void ResetPlayerSettings()
        {
            this.ClearValue(EnableFlyerCarryProperty);
            this.ClearValue(XPMultiplierProperty);
            this.ClearValue(OverrideMaxExperiencePointsPlayerProperty);
            this.ClearValue(PlayerDamageMultiplierProperty);
            this.ClearValue(PlayerResistanceMultiplierProperty);
            this.ClearValue(PlayerCharacterWaterDrainMultiplierProperty);
            this.ClearValue(PlayerCharacterFoodDrainMultiplierProperty);
            this.ClearValue(PlayerCharacterStaminaDrainMultiplierProperty);
            this.ClearValue(PlayerCharacterHealthRecoveryMultiplierProperty);
            this.ClearValue(HarvestingDamageMultiplierPlayerProperty);
            this.ClearValue(CraftingSkillBonusMultiplierProperty);

            this.PlayerBaseStatMultipliers = new StatsMultiplierArray(nameof(PlayerBaseStatMultipliers), GameData.GetBaseStatMultipliers_Player, GameData.GetStatMultiplierInclusions_PlayerBase());
            this.PerLevelStatsMultiplier_Player = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_Player), GameData.GetPerLevelStatsMultipliers_Player, GameData.GetStatMultiplierInclusions_PlayerPerLevel());
        }

        public void ResetRagnarokSection()
        {
            this.ClearValue(Ragnarok_EnabledProperty);
            this.ClearValue(Ragnarok_AllowMultipleTamedUnicornsProperty);
            this.ClearValue(Ragnarok_UnicornSpawnIntervalProperty);
            this.ClearValue(Ragnarok_DisableVolcanoProperty);
            this.ClearValue(Ragnarok_VolcanoIntensityProperty);
            this.ClearValue(Ragnarok_VolcanoIntervalProperty);
            this.ClearValue(Ragnarok_EnableDevelopmentZonesProperty);
        }

        public void ResetRulesSection()
        {
            this.ClearValue(EnableHardcoreProperty);
            this.ClearValue(EnablePVPProperty);
            this.ClearValue(AllowCaveBuildingPvEProperty);
            this.ClearValue(DisableFriendlyFirePvPProperty);
            this.ClearValue(DisableFriendlyFirePvEProperty);
            this.ClearValue(DisableLootCratesProperty);
            this.ClearValue(EnableExtraStructurePreventionVolumesProperty);

            this.ClearValue(EnableDifficultyOverrideProperty);
            this.ClearValue(OverrideOfficialDifficultyProperty);
            this.ClearValue(DifficultyOffsetProperty);
            this.ClearValue(MaxNumberOfPlayersInTribeProperty);

            this.ClearValue(EnableTributeDownloadsProperty);
            this.ClearValue(PreventDownloadSurvivorsProperty);
            this.ClearValue(PreventDownloadItemsProperty);
            this.ClearValue(PreventDownloadDinosProperty);
            this.ClearValue(PreventUploadSurvivorsProperty);
            this.ClearValue(PreventUploadItemsProperty);
            this.ClearValue(PreventUploadDinosProperty);

            this.ClearValue(NoTransferFromFilteringProperty);
            this.ClearValue(OverrideTributeCharacterExpirationSecondsProperty);
            this.ClearValue(OverrideTributeItemExpirationSecondsProperty);
            this.ClearValue(OverrideTributeDinoExpirationSecondsProperty);
            this.ClearValue(OverrideMinimumDinoReuploadIntervalProperty);
            this.ClearValue(TributeCharacterExpirationSecondsProperty);
            this.ClearValue(TributeItemExpirationSecondsProperty);
            this.ClearValue(TributeDinoExpirationSecondsProperty);
            this.ClearValue(MinimumDinoReuploadIntervalProperty);
            this.ClearValue(CrossARKAllowForeignDinoDownloadsProperty);

            this.ClearValue(IncreasePvPRespawnIntervalProperty);
            this.ClearValue(IncreasePvPRespawnIntervalCheckPeriodProperty);
            this.ClearValue(IncreasePvPRespawnIntervalMultiplierProperty);
            this.ClearValue(IncreasePvPRespawnIntervalBaseAmountProperty);

            this.ClearValue(PreventOfflinePvPProperty);
            this.ClearValue(PreventOfflinePvPIntervalProperty);
            this.ClearValue(PreventOfflinePvPConnectionInvincibleIntervalProperty);

            this.ClearValue(AutoPvETimerProperty);
            this.ClearValue(AutoPvEUseSystemTimeProperty);
            this.ClearValue(AutoPvEStartTimeSecondsProperty);
            this.ClearValue(AutoPvEStopTimeSecondsProperty);

            this.ClearValue(AllowTribeWarPvEProperty);
            this.ClearValue(AllowTribeWarCancelPvEProperty);
            this.ClearValue(AllowTribeAlliancesProperty);
            this.ClearValue(MaxAlliancesPerTribeProperty);
            this.ClearValue(MaxTribesPerAllianceProperty);

            this.ClearValue(AllowCustomRecipesProperty);
            this.ClearValue(CustomRecipeEffectivenessMultiplierProperty);
            this.ClearValue(CustomRecipeSkillMultiplierProperty);

            this.ClearValue(EnableDiseasesProperty);
            this.ClearValue(NonPermanentDiseasesProperty);

            this.ClearValue(OverrideNPCNetworkStasisRangeScaleProperty);
            this.ClearValue(NPCNetworkStasisRangeScalePlayerCountStartProperty);
            this.ClearValue(NPCNetworkStasisRangeScalePlayerCountEndProperty);
            this.ClearValue(NPCNetworkStasisRangeScalePercentEndProperty);

            this.ClearValue(UseCorpseLocatorProperty);
            this.ClearValue(PreventSpawnAnimationsProperty);
            this.ClearValue(AllowUnlimitedRespecsProperty);
            this.ClearValue(AllowPlatformSaddleMultiFloorsProperty);
            this.ClearValue(SupplyCrateLootQualityMultiplierProperty);
            this.ClearValue(FishingLootQualityMultiplierProperty);
            this.ClearValue(EnableNoFishLootProperty);
            this.ClearValue(UseCorpseLifeSpanMultiplierProperty);
            this.ClearValue(GlobalPoweredBatteryDurabilityDecreasePerSecondProperty);
            this.ClearValue(TribeNameChangeCooldownProperty);
            this.ClearValue(RandomSupplyCratePointsProperty);
        }

        public void ResetSOTFSection()
        {
            this.ClearValue(SOTF_EnabledProperty);
            this.ClearValue(SOTF_OutputGameReportProperty);
            this.ClearValue(SOTF_GamePlayLoggingProperty);
            this.ClearValue(SOTF_DisableDeathSPectatorProperty);
            this.ClearValue(SOTF_OnlyAdminRejoinAsSpectatorProperty);
            this.ClearValue(SOTF_MaxNumberOfPlayersInTribeProperty);
            this.ClearValue(SOTF_BattleNumOfTribesToStartGameProperty);
            this.ClearValue(SOTF_TimeToCollapseRODProperty);
            this.ClearValue(SOTF_BattleAutoStartGameIntervalProperty);
            this.ClearValue(SOTF_BattleAutoRestartGameIntervalProperty);
            this.ClearValue(SOTF_BattleSuddenDeathIntervalProperty);

            this.ClearValue(SOTF_NoEventsProperty);
            this.ClearValue(SOTF_NoBossesProperty);
            this.ClearValue(SOTF_BothBossesProperty);
            this.ClearValue(SOTF_EvoEventIntervalProperty);
            this.ClearValue(SOTF_RingStartTimeProperty);
        }

        public void ResetStructuresSection()
        {
            this.ClearValue(DisableStructurePlacementCollisionProperty);
            this.ClearValue(StructureResistanceMultiplierProperty);
            this.ClearValue(StructureDamageMultiplierProperty);
            this.ClearValue(StructureDamageRepairCooldownProperty);
            this.ClearValue(PvPStructureDecayProperty);
            this.ClearValue(PvPZoneStructureDamageMultiplierProperty);
            this.ClearValue(MaxStructuresVisibleProperty);
            this.ClearValue(PerPlatformMaxStructuresMultiplierProperty);
            this.ClearValue(MaxPlatformSaddleStructureLimitProperty);
            this.ClearValue(OverrideStructurePlatformPreventionProperty);
            this.ClearValue(FlyerPlatformAllowUnalignedDinoBasingProperty);
            this.ClearValue(PvEAllowStructuresAtSupplyDropsProperty);
            this.ClearValue(EnableStructureDecayPvEProperty);
            this.ClearValue(PvEStructureDecayDestructionPeriodProperty);
            this.ClearValue(PvEStructureDecayPeriodMultiplierProperty);
            this.ClearValue(AutoDestroyOldStructuresMultiplierProperty);
            this.ClearValue(ForceAllStructureLockingProperty);
            this.ClearValue(PassiveDefensesDamageRiderlessDinosProperty);
            this.ClearValue(OnlyAutoDestroyCoreStructuresProperty);
            this.ClearValue(OnlyDecayUnsnappedCoreStructuresProperty);
            this.ClearValue(FastDecayUnsnappedCoreStructuresProperty);
            this.ClearValue(DestroyUnconnectedWaterPipesProperty);
            this.ClearValue(EnableFastDecayIntervalProperty);
            this.ClearValue(FastDecayIntervalProperty);
            this.ClearValue(LimitTurretsInRangeProperty);
            this.ClearValue(LimitTurretsRangeProperty);
            this.ClearValue(LimitTurretsNumProperty);
            this.ClearValue(HardLimitTurretsInRangeProperty);
        }

        public void ResetSupplyCreateOverridesSection()
        {
            this.ConfigOverrideSupplyCrateItems = new SupplyCrateOverrideList(nameof(ConfigOverrideSupplyCrateItems));
            this.ConfigOverrideSupplyCrateItems.Reset();
        }

        public void UpdateOverrideMaxExperiencePointsDino()
        {
            LevelList list = GetLevelList(LevelProgression.Dino);
            if (list == null || list.Count == 0)
                return;

            OverrideMaxExperiencePointsDino = list[list.Count - 1].XPRequired;
        }

        public void UpdateOverrideMaxExperiencePointsPlayer()
        {
            LevelList list = GetLevelList(LevelProgression.Player);
            if (list == null || list.Count == 0)
                return;

            OverrideMaxExperiencePointsPlayer = list[list.Count - 1].XPRequired;
        }
        #endregion

        #region Sync Methods
        public void SyncSettings(ServerProfileSection section, ServerProfile sourceProfile)
        {
            if (sourceProfile == null)
                return;

            switch (section)
            {
                case ServerProfileSection.AdministrationSection:
                    SyncAdministrationSection(sourceProfile);
                    break;
                case ServerProfileSection.AutomaticManagement:
                    SyncAutomaticManagement(sourceProfile);
                    break;
                case ServerProfileSection.RulesSection:
                    SyncRulesSection(sourceProfile);
                    break;
                case ServerProfileSection.ChatAndNotificationsSection:
                    SyncChatAndNotificationsSection(sourceProfile);
                    break;
                case ServerProfileSection.HudAndVisualsSection:
                    SyncHudAndVisualsSection(sourceProfile);
                    break;
                case ServerProfileSection.PlayerSettingsSection:
                    SyncPlayerSettingsSection(sourceProfile);
                    break;
                case ServerProfileSection.DinoSettingsSection:
                    SyncDinoSettingsSection(sourceProfile);
                    break;
                case ServerProfileSection.EnvironmentSection:
                    SyncEnvironmentSection(sourceProfile);
                    break;
                case ServerProfileSection.StructuresSection:
                    SyncStructuresSection(sourceProfile);
                    break;
                case ServerProfileSection.EngramsSection:
                    SyncEngramsSection(sourceProfile);
                    break;
                case ServerProfileSection.CustomSettingsSection:
                    SyncCustomSettingsSection(sourceProfile);
                    break;
                case ServerProfileSection.CustomLevelsSection:
                    SyncCustomLevelsSection(sourceProfile);
                    break;
                case ServerProfileSection.MapSpawnerOverridesSection:
                    SyncNPCSpawnOverridesSection(sourceProfile);
                    break;
                case ServerProfileSection.CraftingOverridesSection:
                    SyncCraftingOverridesSection(sourceProfile);
                    break;
                case ServerProfileSection.SupplyCrateOverridesSection:
                    SyncSupplyCrateOverridesSection(sourceProfile);
                    break;
                case ServerProfileSection.PGMSection:
                    SyncPGMSection(sourceProfile);
                    break;
                case ServerProfileSection.SOTFSection:
                    SyncSOTFSection(sourceProfile);
                    break;
            }
        }

        private void SyncAdministrationSection(ServerProfile sourceProfile)
        {
            this.SetValue(ServerPasswordProperty, sourceProfile.ServerPassword);
            this.SetValue(AdminPasswordProperty, sourceProfile.AdminPassword);
            this.SetValue(SpectatorPasswordProperty, sourceProfile.SpectatorPassword);
            this.SetValue(ServerConnectionPortProperty, sourceProfile.ServerConnectionPort);
            this.SetValue(ServerPortProperty, sourceProfile.ServerPort);
            this.SetValue(ServerIPProperty, sourceProfile.ServerIP);
            this.SetValue(UseRawSocketsProperty, sourceProfile.UseRawSockets);
            this.SetValue(NoNetThreadingProperty, sourceProfile.NoNetThreading);
            this.SetValue(ForceNetThreadingProperty, sourceProfile.ForceNetThreading);

            this.SetValue(EnableBanListURLProperty, sourceProfile.EnableBanListURL);
            this.SetValue(BanListURLProperty, sourceProfile.BanListURL);
            this.SetValue(MaxPlayersProperty, sourceProfile.MaxPlayers);
            this.SetValue(EnableKickIdlePlayersProperty, sourceProfile.EnableKickIdlePlayers);
            this.SetValue(KickIdlePlayersPeriodProperty, sourceProfile.KickIdlePlayersPeriod);

            this.SetValue(RCONEnabledProperty, sourceProfile.RCONEnabled);
            this.SetValue(RCONPortProperty, sourceProfile.RCONPort);
            this.SetValue(RCONServerGameLogBufferProperty, sourceProfile.RCONServerGameLogBuffer);
            this.SetValue(AdminLoggingProperty, sourceProfile.AdminLogging);

            this.SetValue(ServerMapProperty, sourceProfile.ServerMap);
            this.SetValue(TotalConversionModIdProperty, sourceProfile.TotalConversionModId);
            this.SetValue(ServerModIdsProperty, sourceProfile.ServerModIds);

            this.SetValue(EnableExtinctionEventProperty, sourceProfile.EnableExtinctionEvent);
            this.SetValue(ExtinctionEventTimeIntervalProperty, sourceProfile.ExtinctionEventTimeInterval);
            this.SetValue(ExtinctionEventUTCProperty, sourceProfile.ExtinctionEventUTC);

            this.SetValue(AutoSavePeriodMinutesProperty, sourceProfile.AutoSavePeriodMinutes);
            this.SetValue(MOTDProperty, sourceProfile.MOTD);
            this.SetValue(MOTDDurationProperty, sourceProfile.MOTDDuration);

            this.SetValue(DisableValveAntiCheatSystemProperty, sourceProfile.DisableValveAntiCheatSystem);
            this.SetValue(DisablePlayerMovePhysicsOptimizationProperty, sourceProfile.DisablePlayerMovePhysicsOptimization);
            this.SetValue(DisableAntiSpeedHackDetectionProperty, sourceProfile.DisableAntiSpeedHackDetection);
            this.SetValue(SpeedHackBiasProperty, sourceProfile.SpeedHackBias);
            this.SetValue(UseBattlEyeProperty, sourceProfile.UseBattlEye);
            this.SetValue(ForceRespawnDinosProperty, sourceProfile.ForceRespawnDinos);
            this.SetValue(EnableServerAutoForceRespawnWildDinosIntervalProperty, sourceProfile.EnableServerAutoForceRespawnWildDinosInterval);
            this.SetValue(ServerAutoForceRespawnWildDinosIntervalProperty, sourceProfile.ServerAutoForceRespawnWildDinosInterval);
            this.SetValue(EnableServerAdminLogsProperty, sourceProfile.EnableServerAdminLogs);
            this.SetValue(ServerAdminLogsIncludeTribeLogsProperty, sourceProfile.ServerAdminLogsIncludeTribeLogs);
            this.SetValue(ServerRCONOutputTribeLogsProperty, sourceProfile.ServerRCONOutputTribeLogs);
            this.SetValue(NotifyAdminCommandsInChatProperty, sourceProfile.NotifyAdminCommandsInChat);
            this.SetValue(MaxTribeLogsProperty, sourceProfile.MaxTribeLogs);
            this.SetValue(TribeLogDestroyedEnemyStructuresProperty, sourceProfile.TribeLogDestroyedEnemyStructures);
            this.SetValue(ForceDirectX10Property, sourceProfile.ForceDirectX10);
            this.SetValue(ForceShaderModel4Property, sourceProfile.ForceShaderModel4);
            this.SetValue(ForceLowMemoryProperty, sourceProfile.ForceLowMemory);
            this.SetValue(ForceNoManSkyProperty, sourceProfile.ForceNoManSky);
            this.SetValue(UseAllAvailableCoresProperty, sourceProfile.UseAllAvailableCores);
            this.SetValue(UseCacheProperty, sourceProfile.UseCache);
            this.SetValue(UseOldSaveFormatProperty, sourceProfile.UseOldSaveFormat);
            this.SetValue(UseNoMemoryBiasProperty, sourceProfile.UseNoMemoryBias);
            this.SetValue(StasisKeepControllersProperty, sourceProfile.StasisKeepControllers);
            this.SetValue(UseNoHangDetectionProperty, sourceProfile.UseNoHangDetection);
            this.SetValue(ServerAllowAnselProperty, sourceProfile.ServerAllowAnsel);
            this.SetValue(AllowHideDamageSourceFromLogsProperty, sourceProfile.AllowHideDamageSourceFromLogs);
            this.SetValue(NoDinosProperty, sourceProfile.NoDinos);

            this.SetValue(AltSaveDirectoryNameProperty, sourceProfile.AltSaveDirectoryName);
            this.SetValue(EnableWebAlarmProperty, sourceProfile.EnableWebAlarm);
            this.SetValue(WebAlarmKeyProperty, sourceProfile.WebAlarmKey);
            this.SetValue(WebAlarmUrlProperty, sourceProfile.WebAlarmUrl);

            this.SetValue(CrossArkClusterIdProperty, sourceProfile.CrossArkClusterId);
            this.SetValue(ClusterDirOverrideProperty, sourceProfile.ClusterDirOverride);

            this.SetValue(AdditionalArgsProperty, sourceProfile.AdditionalArgs);
            this.SetValue(LauncherArgsOverrideProperty, sourceProfile.LauncherArgsOverride);
            this.SetValue(LauncherArgsProperty, sourceProfile.LauncherArgs);
        }

        private void SyncAutomaticManagement(ServerProfile sourceProfile)
        {
            this.SetValue(EnableAutoBackupProperty, sourceProfile.EnableAutoBackup);
            this.SetValue(EnableAutoStartProperty, sourceProfile.EnableAutoStart);
            this.SetValue(EnableAutoUpdateProperty, sourceProfile.EnableAutoUpdate);
            this.SetValue(EnableAutoShutdown1Property, sourceProfile.EnableAutoShutdown1);
            this.SetValue(AutoShutdownTime1Property, sourceProfile.AutoShutdownTime1);
            this.SetValue(RestartAfterShutdown1Property, sourceProfile.RestartAfterShutdown1);
            this.SetValue(UpdateAfterShutdown1Property, sourceProfile.UpdateAfterShutdown1);
            this.SetValue(EnableAutoShutdown2Property, sourceProfile.EnableAutoShutdown2);
            this.SetValue(AutoShutdownTime2Property, sourceProfile.AutoShutdownTime2);
            this.SetValue(RestartAfterShutdown2Property, sourceProfile.RestartAfterShutdown2);
            this.SetValue(UpdateAfterShutdown2Property, sourceProfile.UpdateAfterShutdown2);
            this.SetValue(AutoRestartIfShutdownProperty, sourceProfile.AutoRestartIfShutdown);
        }

        private void SyncChatAndNotificationsSection(ServerProfile sourceProfile)
        {
            this.SetValue(EnableGlobalVoiceChatProperty, sourceProfile.EnableGlobalVoiceChat);
            this.SetValue(EnableProximityChatProperty, sourceProfile.EnableProximityChat);
            this.SetValue(EnablePlayerLeaveNotificationsProperty, sourceProfile.EnablePlayerLeaveNotifications);
            this.SetValue(EnablePlayerJoinedNotificationsProperty, sourceProfile.EnablePlayerJoinedNotifications);
        }

        private void SyncCraftingOverridesSection(ServerProfile sourceProfile)
        {
            this.ConfigOverrideItemCraftingCosts.Clear();
            this.ConfigOverrideItemCraftingCosts.FromIniValues(sourceProfile.ConfigOverrideItemCraftingCosts.ToIniValues());
            this.ConfigOverrideItemCraftingCosts.IsEnabled = this.ConfigOverrideItemCraftingCosts.Count > 0;
        }

        private void SyncCustomLevelsSection(ServerProfile sourceProfile)
        {
            this.SetValue(EnableLevelProgressionsProperty, sourceProfile.EnableLevelProgressions);

            this.PlayerLevels = LevelList.FromINIValues(sourceProfile.PlayerLevels.ToINIValueForXP(), sourceProfile.PlayerLevels.ToINIValuesForEngramPoints());
            this.DinoLevels = LevelList.FromINIValues(sourceProfile.DinoLevels.ToINIValueForXP(), sourceProfile.DinoLevels.ToINIValuesForEngramPoints());
        }

        private void SyncCustomSettingsSection(ServerProfile sourceProfile)
        {
            this.CustomGameUserSettingsSections.Clear();
            foreach (var section in sourceProfile.CustomGameUserSettingsSections)
            {
                this.CustomGameUserSettingsSections.Add(section.SectionName, section.ToIniValues().ToArray());
            }
        }

        private void SyncDinoSettingsSection(ServerProfile sourceProfile)
        {
            this.SetValue(OverrideMaxExperiencePointsDinoProperty, sourceProfile.OverrideMaxExperiencePointsDino);
            this.SetValue(MaxTamedDinosProperty, sourceProfile.MaxTamedDinos);
            this.SetValue(MaxPersonalTamedDinosProperty, sourceProfile.MaxPersonalTamedDinos);
            this.SetValue(DinoDamageMultiplierProperty, sourceProfile.DinoDamageMultiplier);
            this.SetValue(TamedDinoDamageMultiplierProperty, sourceProfile.TamedDinoDamageMultiplier);
            this.SetValue(TamedDinoResistanceMultiplierProperty, sourceProfile.TamedDinoResistanceMultiplier);
            this.SetValue(WildDinoCharacterFoodDrainMultiplierProperty, sourceProfile.WildDinoCharacterFoodDrainMultiplier);
            this.SetValue(TamedDinoCharacterFoodDrainMultiplierProperty, sourceProfile.TamedDinoCharacterFoodDrainMultiplier);
            this.SetValue(WildDinoTorporDrainMultiplierProperty, sourceProfile.WildDinoTorporDrainMultiplier);
            this.SetValue(TamedDinoTorporDrainMultiplierProperty, sourceProfile.TamedDinoTorporDrainMultiplier);
            this.SetValue(PassiveTameIntervalMultiplierProperty, sourceProfile.PassiveTameIntervalMultiplier);
            this.SetValue(PersonalTamedDinosSaddleStructureCostProperty, sourceProfile.PersonalTamedDinosSaddleStructureCost);
            this.SetValue(DinoCharacterFoodDrainMultiplierProperty, sourceProfile.DinoCharacterFoodDrainMultiplier);
            this.SetValue(DinoCharacterStaminaDrainMultiplierProperty, sourceProfile.DinoCharacterStaminaDrainMultiplier);
            this.SetValue(DinoCharacterHealthRecoveryMultiplierProperty, sourceProfile.DinoCharacterHealthRecoveryMultiplier);
            this.SetValue(DinoCountMultiplierProperty, sourceProfile.DinoCountMultiplier);
            this.SetValue(HarvestingDamageMultiplierDinoProperty, sourceProfile.DinoHarvestingDamageMultiplier);
            this.SetValue(TurretDamageMultiplierDinoProperty, sourceProfile.DinoTurretDamageMultiplier);

            this.SetValue(AllowRaidDinoFeedingProperty, sourceProfile.AllowRaidDinoFeeding);
            this.SetValue(RaidDinoCharacterFoodDrainMultiplierProperty, sourceProfile.RaidDinoCharacterFoodDrainMultiplier);

            this.SetValue(EnableAllowCaveFlyersProperty, sourceProfile.EnableAllowCaveFlyers);
            this.SetValue(AllowFlyingStaminaRecoveryProperty, sourceProfile.AllowFlyingStaminaRecovery);
            this.SetValue(PreventMateBoostProperty, sourceProfile.PreventMateBoost);
            this.SetValue(DisableDinoDecayPvEProperty, sourceProfile.DisableDinoDecayPvE);
            this.SetValue(DisableDinoDecayPvPProperty, sourceProfile.DisableDinoDecayPvP);
            this.SetValue(AutoDestroyDecayedDinosProperty, sourceProfile.AutoDestroyDecayedDinos);
            this.SetValue(PvEDinoDecayPeriodMultiplierProperty, sourceProfile.PvEDinoDecayPeriodMultiplier);
            this.SetValue(ForceFlyerExplosivesProperty, sourceProfile.ForceFlyerExplosives);
            this.SetValue(AllowMultipleAttachedC4Property, sourceProfile.AllowMultipleAttachedC4);
            this.SetValue(DisableDinoRidingProperty, sourceProfile.DisableDinoRiding);
            this.SetValue(DisableDinoTamingProperty, sourceProfile.DisableDinoTaming);

            sourceProfile.DinoSettings.RenderToModel();

            this.DinoSpawnWeightMultipliers.Clear();
            this.DinoSpawnWeightMultipliers.FromIniValues(sourceProfile.DinoSpawnWeightMultipliers.ToIniValues());
            this.DinoSpawnWeightMultipliers.IsEnabled = sourceProfile.DinoSpawnWeightMultipliers.IsEnabled;
            this.PreventDinoTameClassNames.Clear();
            this.PreventDinoTameClassNames.FromIniValues(sourceProfile.PreventDinoTameClassNames.ToIniValues());
            this.PreventDinoTameClassNames.IsEnabled = sourceProfile.PreventDinoTameClassNames.IsEnabled;
            this.NPCReplacements.Clear();
            this.NPCReplacements.FromIniValues(sourceProfile.NPCReplacements.ToIniValues());
            this.NPCReplacements.IsEnabled = sourceProfile.NPCReplacements.IsEnabled;
            this.TamedDinoClassDamageMultipliers.Clear();
            this.TamedDinoClassDamageMultipliers.FromIniValues(sourceProfile.TamedDinoClassDamageMultipliers.ToIniValues());
            this.TamedDinoClassDamageMultipliers.IsEnabled = sourceProfile.TamedDinoClassDamageMultipliers.IsEnabled;
            this.TamedDinoClassResistanceMultipliers.Clear();
            this.TamedDinoClassResistanceMultipliers.FromIniValues(sourceProfile.TamedDinoClassResistanceMultipliers.ToIniValues());
            this.TamedDinoClassResistanceMultipliers.IsEnabled = sourceProfile.TamedDinoClassResistanceMultipliers.IsEnabled;
            this.DinoClassDamageMultipliers.Clear();
            this.DinoClassDamageMultipliers.FromIniValues(sourceProfile.DinoClassDamageMultipliers.ToIniValues());
            this.DinoClassDamageMultipliers.IsEnabled = sourceProfile.DinoClassDamageMultipliers.IsEnabled;
            this.DinoClassResistanceMultipliers.Clear();
            this.DinoClassResistanceMultipliers.FromIniValues(sourceProfile.DinoClassResistanceMultipliers.ToIniValues());
            this.DinoClassResistanceMultipliers.IsEnabled = sourceProfile.DinoClassResistanceMultipliers.IsEnabled;
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);
            this.DinoSettings.RenderToView();

            this.PerLevelStatsMultiplier_DinoWild = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_DinoWild, GameData.GetStatMultiplierInclusions_DinoWildPerLevel());
            this.PerLevelStatsMultiplier_DinoWild.FromIniValues(sourceProfile.PerLevelStatsMultiplier_DinoWild.ToIniValues());
            this.PerLevelStatsMultiplier_DinoWild.IsEnabled = sourceProfile.PerLevelStatsMultiplier_DinoWild.IsEnabled;
            this.PerLevelStatsMultiplier_DinoTamed = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed, GameData.GetStatMultiplierInclusions_DinoTamedPerLevel());
            this.PerLevelStatsMultiplier_DinoTamed.FromIniValues(sourceProfile.PerLevelStatsMultiplier_DinoTamed.ToIniValues());
            this.PerLevelStatsMultiplier_DinoTamed.IsEnabled = sourceProfile.PerLevelStatsMultiplier_DinoTamed.IsEnabled;
            this.PerLevelStatsMultiplier_DinoTamed_Add = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamedAdd, GameData.GetStatMultiplierInclusions_DinoTamedAdd());
            this.PerLevelStatsMultiplier_DinoTamed_Add.FromIniValues(sourceProfile.PerLevelStatsMultiplier_DinoTamed_Add.ToIniValues());
            this.PerLevelStatsMultiplier_DinoTamed_Add.IsEnabled = sourceProfile.PerLevelStatsMultiplier_DinoTamed_Add.IsEnabled;
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new StatsMultiplierArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamedAffinity, GameData.GetStatMultiplierInclusions_DinoTamedAffinity());
            this.PerLevelStatsMultiplier_DinoTamed_Affinity.FromIniValues(sourceProfile.PerLevelStatsMultiplier_DinoTamed_Affinity.ToIniValues());
            this.PerLevelStatsMultiplier_DinoTamed_Affinity.IsEnabled = sourceProfile.PerLevelStatsMultiplier_DinoTamed_Affinity.IsEnabled;

            this.SetValue(MatingIntervalMultiplierProperty, sourceProfile.MatingIntervalMultiplier);
            this.SetValue(EggHatchSpeedMultiplierProperty, sourceProfile.EggHatchSpeedMultiplier);
            this.SetValue(BabyMatureSpeedMultiplierProperty, sourceProfile.BabyMatureSpeedMultiplier);
            this.SetValue(BabyFoodConsumptionSpeedMultiplierProperty, sourceProfile.BabyFoodConsumptionSpeedMultiplier);

            this.SetValue(DisableImprintDinoBuffProperty, sourceProfile.DisableImprintDinoBuff);
            this.SetValue(AllowAnyoneBabyImprintCuddleProperty, sourceProfile.AllowAnyoneBabyImprintCuddle);
            this.SetValue(BabyImprintingStatScaleMultiplierProperty, sourceProfile.BabyImprintingStatScaleMultiplier);
            this.SetValue(BabyCuddleIntervalMultiplierProperty, sourceProfile.BabyCuddleIntervalMultiplier);
            this.SetValue(BabyCuddleGracePeriodMultiplierProperty, sourceProfile.BabyCuddleGracePeriodMultiplier);
            this.SetValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty, sourceProfile.BabyCuddleLoseImprintQualitySpeedMultiplier);
        }

        private void SyncEngramsSection(ServerProfile sourceProfile)
        {
            this.SetValue(AutoUnlockAllEngramsProperty, sourceProfile.AutoUnlockAllEngrams);
            this.SetValue(OnlyAllowSpecifiedEngramsProperty, sourceProfile.OnlyAllowSpecifiedEngrams);

            this.OverrideNamedEngramEntries.Clear();
            foreach (var ee in sourceProfile.OverrideNamedEngramEntries)
            {
                this.OverrideNamedEngramEntries.Add(ee.Clone());
            }
            this.OverrideNamedEngramEntries.IsEnabled = sourceProfile.OverrideNamedEngramEntries.IsEnabled;
        }

        private void SyncEnvironmentSection(ServerProfile sourceProfile)
        {
            this.SetValue(TamingSpeedMultiplierProperty, sourceProfile.TamingSpeedMultiplier);
            this.SetValue(HarvestAmountMultiplierProperty, sourceProfile.HarvestAmountMultiplier);
            this.SetValue(ResourcesRespawnPeriodMultiplierProperty, sourceProfile.ResourcesRespawnPeriodMultiplier);
            this.SetValue(ResourceNoReplenishRadiusPlayersProperty, sourceProfile.ResourceNoReplenishRadiusPlayers);
            this.SetValue(ResourceNoReplenishRadiusStructuresProperty, sourceProfile.ResourceNoReplenishRadiusStructures);
            this.SetValue(ClampResourceHarvestDamageProperty, sourceProfile.ClampResourceHarvestDamage);
            this.SetValue(UseOptimizedHarvestingHealthProperty, sourceProfile.UseOptimizedHarvestingHealth);
            this.SetValue(HarvestHealthMultiplierProperty, sourceProfile.HarvestHealthMultiplier);

            this.HarvestResourceItemAmountClassMultipliers.Clear();
            this.HarvestResourceItemAmountClassMultipliers.FromIniValues(sourceProfile.HarvestResourceItemAmountClassMultipliers.ToIniValues());
            this.HarvestResourceItemAmountClassMultipliers.IsEnabled = sourceProfile.HarvestResourceItemAmountClassMultipliers.IsEnabled;
            
            this.SetValue(BaseTemperatureMultiplierProperty, sourceProfile.BaseTemperatureMultiplier);
            this.SetValue(DayCycleSpeedScaleProperty, sourceProfile.DayCycleSpeedScale);
            this.SetValue(DayTimeSpeedScaleProperty, sourceProfile.DayTimeSpeedScale);
            this.SetValue(NightTimeSpeedScaleProperty, sourceProfile.NightTimeSpeedScale);
            this.SetValue(DisableWeatherFogProperty, sourceProfile.DisableWeatherFog);

            this.SetValue(GlobalSpoilingTimeMultiplierProperty, sourceProfile.GlobalSpoilingTimeMultiplier);
            this.SetValue(ClampItemSpoilingTimesProperty, sourceProfile.ClampItemSpoilingTimes);
            this.SetValue(GlobalItemDecompositionTimeMultiplierProperty, sourceProfile.GlobalItemDecompositionTimeMultiplier);
            this.SetValue(GlobalCorpseDecompositionTimeMultiplierProperty, sourceProfile.GlobalCorpseDecompositionTimeMultiplier);
            this.SetValue(CropDecaySpeedMultiplierProperty, sourceProfile.CropDecaySpeedMultiplier);
            this.SetValue(CropGrowthSpeedMultiplierProperty, sourceProfile.CropGrowthSpeedMultiplier);
            this.SetValue(LayEggIntervalMultiplierProperty, sourceProfile.LayEggIntervalMultiplier);
            this.SetValue(PoopIntervalMultiplierProperty, sourceProfile.PoopIntervalMultiplier);
            this.SetValue(HairGrowthSpeedMultiplierProperty, sourceProfile.HairGrowthSpeedMultiplier);

            this.SetValue(CraftXPMultiplierProperty, sourceProfile.CraftXPMultiplier);
            this.SetValue(GenericXPMultiplierProperty, sourceProfile.GenericXPMultiplier);
            this.SetValue(HarvestXPMultiplierProperty, sourceProfile.HarvestXPMultiplier);
            this.SetValue(KillXPMultiplierProperty, sourceProfile.KillXPMultiplier);
            this.SetValue(SpecialXPMultiplierProperty, sourceProfile.SpecialXPMultiplier);
        }

        private void SyncHudAndVisualsSection(ServerProfile sourceProfile)
        {
            this.SetValue(AllowCrosshairProperty, sourceProfile.AllowCrosshair);
            this.SetValue(AllowHUDProperty, sourceProfile.AllowHUD);
            this.SetValue(AllowThirdPersonViewProperty, sourceProfile.AllowThirdPersonView);
            this.SetValue(AllowMapPlayerLocationProperty, sourceProfile.AllowMapPlayerLocation);
            this.SetValue(AllowPVPGammaProperty, sourceProfile.AllowPVPGamma);
            this.SetValue(AllowPvEGammaProperty, sourceProfile.AllowPvEGamma);
            this.SetValue(ShowFloatingDamageTextProperty, sourceProfile.ShowFloatingDamageText);
            this.SetValue(AllowHitMarkersProperty, sourceProfile.AllowHitMarkers);
        }

        private void SyncNPCSpawnOverridesSection(ServerProfile sourceProfile)
        {
            sourceProfile.NPCSpawnSettings.RenderToModel();

            this.ConfigAddNPCSpawnEntriesContainer.Clear();
            this.ConfigAddNPCSpawnEntriesContainer.FromIniValues(sourceProfile.ConfigAddNPCSpawnEntriesContainer.ToIniValues());
            this.ConfigAddNPCSpawnEntriesContainer.IsEnabled = this.ConfigAddNPCSpawnEntriesContainer.Count > 0;
            this.ConfigSubtractNPCSpawnEntriesContainer.Clear();
            this.ConfigSubtractNPCSpawnEntriesContainer.FromIniValues(sourceProfile.ConfigSubtractNPCSpawnEntriesContainer.ToIniValues());
            this.ConfigSubtractNPCSpawnEntriesContainer.IsEnabled = this.ConfigSubtractNPCSpawnEntriesContainer.Count > 0;
            this.ConfigOverrideNPCSpawnEntriesContainer.Clear();
            this.ConfigOverrideNPCSpawnEntriesContainer.FromIniValues(sourceProfile.ConfigOverrideNPCSpawnEntriesContainer.ToIniValues());
            this.ConfigOverrideNPCSpawnEntriesContainer.IsEnabled = this.ConfigOverrideNPCSpawnEntriesContainer.Count > 0;

            this.NPCSpawnSettings = new NPCSpawnSettingsList(this.ConfigAddNPCSpawnEntriesContainer, this.ConfigSubtractNPCSpawnEntriesContainer, this.ConfigOverrideNPCSpawnEntriesContainer);
            this.NPCSpawnSettings.RenderToView();
        }

        private void SyncPGMSection(ServerProfile sourceProfile)
        {
            this.SetValue(PGM_EnabledProperty, sourceProfile.PGM_Enabled);
            this.SetValue(PGM_NameProperty, sourceProfile.PGM_Name);

            this.PGM_Terrain.InitializeFromINIValue(sourceProfile.PGM_Terrain.ToINIValue());
        }

        private void SyncPlayerSettingsSection(ServerProfile sourceProfile)
        {
            this.SetValue(EnableFlyerCarryProperty, sourceProfile.EnableFlyerCarry);
            this.SetValue(XPMultiplierProperty, sourceProfile.XPMultiplier);
            this.SetValue(OverrideMaxExperiencePointsPlayerProperty, sourceProfile.OverrideMaxExperiencePointsPlayer);
            this.SetValue(PlayerDamageMultiplierProperty, sourceProfile.PlayerDamageMultiplier);
            this.SetValue(PlayerResistanceMultiplierProperty, sourceProfile.PlayerResistanceMultiplier);
            this.SetValue(PlayerCharacterWaterDrainMultiplierProperty, sourceProfile.PlayerCharacterWaterDrainMultiplier);
            this.SetValue(PlayerCharacterFoodDrainMultiplierProperty, sourceProfile.PlayerCharacterFoodDrainMultiplier);
            this.SetValue(PlayerCharacterStaminaDrainMultiplierProperty, sourceProfile.PlayerCharacterStaminaDrainMultiplier);
            this.SetValue(PlayerCharacterHealthRecoveryMultiplierProperty, sourceProfile.PlayerCharacterHealthRecoveryMultiplier);
            this.SetValue(HarvestingDamageMultiplierPlayerProperty, sourceProfile.PlayerHarvestingDamageMultiplier);
            this.SetValue(CraftingSkillBonusMultiplierProperty, sourceProfile.CraftingSkillBonusMultiplier);

            this.PlayerBaseStatMultipliers.Clear();
            this.PlayerBaseStatMultipliers.FromIniValues(sourceProfile.PlayerBaseStatMultipliers.ToIniValues());
            this.PlayerBaseStatMultipliers.IsEnabled = sourceProfile.PlayerBaseStatMultipliers.IsEnabled;

            this.PerLevelStatsMultiplier_Player.Clear();
            this.PerLevelStatsMultiplier_Player.FromIniValues(sourceProfile.PerLevelStatsMultiplier_Player.ToIniValues());
            this.PerLevelStatsMultiplier_Player.IsEnabled = sourceProfile.PerLevelStatsMultiplier_Player.IsEnabled;
        }

        private void SyncRagnarokSection(ServerProfile sourceProfile)
        {
            this.SetValue(Ragnarok_EnabledProperty, sourceProfile.Ragnarok_Enabled);
            this.SetValue(Ragnarok_AllowMultipleTamedUnicornsProperty, sourceProfile.Ragnarok_AllowMultipleTamedUnicorns);
            this.SetValue(Ragnarok_UnicornSpawnIntervalProperty, sourceProfile.Ragnarok_UnicornSpawnInterval);
            this.SetValue(Ragnarok_DisableVolcanoProperty, sourceProfile.Ragnarok_DisableVolcano);
            this.SetValue(Ragnarok_VolcanoIntensityProperty, sourceProfile.Ragnarok_VolcanoIntensity);
            this.SetValue(Ragnarok_VolcanoIntervalProperty, sourceProfile.Ragnarok_VolcanoInterval);
            this.SetValue(Ragnarok_EnableDevelopmentZonesProperty, sourceProfile.Ragnarok_EnableDevelopmentZones);
        }

        private void SyncRulesSection(ServerProfile sourceProfile)
        {
            this.SetValue(EnableHardcoreProperty, sourceProfile.EnableHardcore);
            this.SetValue(EnablePVPProperty, sourceProfile.EnablePVP);
            this.SetValue(AllowCaveBuildingPvEProperty, sourceProfile.AllowCaveBuildingPvE);
            this.SetValue(DisableFriendlyFirePvPProperty, sourceProfile.DisableFriendlyFirePvP);
            this.SetValue(DisableFriendlyFirePvEProperty, sourceProfile.DisableFriendlyFirePvE);
            this.SetValue(DisableLootCratesProperty, sourceProfile.DisableLootCrates);
            this.SetValue(AllowCrateSpawnsOnTopOfStructuresProperty, sourceProfile.AllowCrateSpawnsOnTopOfStructures);
            this.SetValue(EnableExtraStructurePreventionVolumesProperty, sourceProfile.EnableExtraStructurePreventionVolumes);

            this.SetValue(EnableDifficultyOverrideProperty, sourceProfile.EnableDifficultyOverride);
            this.SetValue(OverrideOfficialDifficultyProperty, sourceProfile.OverrideOfficialDifficulty);
            this.SetValue(DifficultyOffsetProperty, sourceProfile.DifficultyOffset);
            this.SetValue(MaxNumberOfPlayersInTribeProperty, sourceProfile.MaxNumberOfPlayersInTribe);

            this.SetValue(EnableTributeDownloadsProperty, sourceProfile.EnableTributeDownloads);
            this.SetValue(PreventDownloadSurvivorsProperty, sourceProfile.PreventDownloadSurvivors);
            this.SetValue(PreventDownloadItemsProperty, sourceProfile.PreventDownloadItems);
            this.SetValue(PreventDownloadDinosProperty, sourceProfile.PreventDownloadDinos);
            this.SetValue(PreventUploadSurvivorsProperty, sourceProfile.PreventUploadSurvivors);
            this.SetValue(PreventUploadItemsProperty, sourceProfile.PreventUploadItems);
            this.SetValue(PreventUploadDinosProperty, sourceProfile.PreventUploadDinos);

            this.SetValue(NoTransferFromFilteringProperty, sourceProfile.NoTransferFromFiltering);
            this.SetValue(OverrideTributeCharacterExpirationSecondsProperty, sourceProfile.OverrideTributeCharacterExpirationSeconds);
            this.SetValue(OverrideTributeItemExpirationSecondsProperty, sourceProfile.OverrideTributeItemExpirationSeconds);
            this.SetValue(OverrideTributeDinoExpirationSecondsProperty, sourceProfile.OverrideTributeDinoExpirationSeconds);
            this.SetValue(OverrideMinimumDinoReuploadIntervalProperty, sourceProfile.OverrideMinimumDinoReuploadInterval);
            this.SetValue(TributeCharacterExpirationSecondsProperty, sourceProfile.TributeCharacterExpirationSeconds);
            this.SetValue(TributeItemExpirationSecondsProperty, sourceProfile.TributeItemExpirationSeconds);
            this.SetValue(TributeDinoExpirationSecondsProperty, sourceProfile.TributeDinoExpirationSeconds);
            this.SetValue(MinimumDinoReuploadIntervalProperty, sourceProfile.MinimumDinoReuploadInterval);
            this.SetValue(CrossARKAllowForeignDinoDownloadsProperty, sourceProfile.CrossARKAllowForeignDinoDownloads);

            this.SetValue(IncreasePvPRespawnIntervalProperty, sourceProfile.IncreasePvPRespawnInterval);
            this.SetValue(IncreasePvPRespawnIntervalCheckPeriodProperty, sourceProfile.IncreasePvPRespawnIntervalCheckPeriod);
            this.SetValue(IncreasePvPRespawnIntervalMultiplierProperty, sourceProfile.IncreasePvPRespawnIntervalMultiplier);
            this.SetValue(IncreasePvPRespawnIntervalBaseAmountProperty, sourceProfile.IncreasePvPRespawnIntervalBaseAmount);

            this.SetValue(PreventOfflinePvPProperty, sourceProfile.PreventOfflinePvP);
            this.SetValue(PreventOfflinePvPIntervalProperty, sourceProfile.PreventOfflinePvPInterval);
            this.SetValue(PreventOfflinePvPConnectionInvincibleIntervalProperty, sourceProfile.PreventOfflinePvPConnectionInvincibleInterval);

            this.SetValue(AutoPvETimerProperty, sourceProfile.AutoPvETimer);
            this.SetValue(AutoPvEUseSystemTimeProperty, sourceProfile.AutoPvEUseSystemTime);
            this.SetValue(AutoPvEStartTimeSecondsProperty, sourceProfile.AutoPvEStartTimeSeconds);
            this.SetValue(AutoPvEStopTimeSecondsProperty, sourceProfile.AutoPvEStopTimeSeconds);

            this.SetValue(AllowTribeWarPvEProperty, sourceProfile.AllowTribeWarPvE);
            this.SetValue(AllowTribeWarCancelPvEProperty, sourceProfile.AllowTribeWarCancelPvE);
            this.SetValue(AllowTribeAlliancesProperty, sourceProfile.AllowTribeAlliances);
            this.SetValue(MaxAlliancesPerTribeProperty, sourceProfile.MaxAlliancesPerTribe);
            this.SetValue(MaxTribesPerAllianceProperty, sourceProfile.MaxTribesPerAlliance);

            this.SetValue(AllowCustomRecipesProperty, sourceProfile.AllowCustomRecipes);
            this.SetValue(CustomRecipeEffectivenessMultiplierProperty, sourceProfile.CustomRecipeEffectivenessMultiplier);
            this.SetValue(CustomRecipeSkillMultiplierProperty, sourceProfile.CustomRecipeSkillMultiplier);

            this.SetValue(EnableDiseasesProperty, sourceProfile.EnableDiseases);
            this.SetValue(NonPermanentDiseasesProperty, sourceProfile.NonPermanentDiseases);

            this.SetValue(OverrideNPCNetworkStasisRangeScaleProperty, sourceProfile.OverrideNPCNetworkStasisRangeScale);
            this.SetValue(NPCNetworkStasisRangeScalePlayerCountStartProperty, sourceProfile.NPCNetworkStasisRangeScalePlayerCountStart);
            this.SetValue(NPCNetworkStasisRangeScalePlayerCountEndProperty, sourceProfile.NPCNetworkStasisRangeScalePlayerCountEnd);
            this.SetValue(NPCNetworkStasisRangeScalePercentEndProperty, sourceProfile.NPCNetworkStasisRangeScalePercentEnd);

            this.SetValue(UseCorpseLocatorProperty, sourceProfile.UseCorpseLocator);
            this.SetValue(PreventSpawnAnimationsProperty, sourceProfile.PreventSpawnAnimations);
            this.SetValue(AllowUnlimitedRespecsProperty, sourceProfile.AllowUnlimitedRespecs);
            this.SetValue(AllowPlatformSaddleMultiFloorsProperty, sourceProfile.AllowPlatformSaddleMultiFloors);
            this.SetValue(SupplyCrateLootQualityMultiplierProperty, sourceProfile.SupplyCrateLootQualityMultiplier);
            this.SetValue(FishingLootQualityMultiplierProperty, sourceProfile.FishingLootQualityMultiplier);
            this.SetValue(EnableNoFishLootProperty, sourceProfile.EnableNoFishLoot);
            this.SetValue(OxygenSwimSpeedStatMultiplierProperty, sourceProfile.OxygenSwimSpeedStatMultiplier);
            this.SetValue(UseCorpseLifeSpanMultiplierProperty, sourceProfile.UseCorpseLifeSpanMultiplier);
            this.SetValue(GlobalPoweredBatteryDurabilityDecreasePerSecondProperty, sourceProfile.GlobalPoweredBatteryDurabilityDecreasePerSecond);
            this.SetValue(TribeNameChangeCooldownProperty, sourceProfile.TribeNameChangeCooldown);
            this.SetValue(RandomSupplyCratePointsProperty, sourceProfile.RandomSupplyCratePoints);
        }

        private void SyncSOTFSection(ServerProfile sourceProfile)
        {
            this.SetValue(SOTF_EnabledProperty, sourceProfile.SOTF_Enabled);
            this.SetValue(SOTF_OutputGameReportProperty, sourceProfile.SOTF_OutputGameReport);
            this.SetValue(SOTF_GamePlayLoggingProperty, sourceProfile.SOTF_GamePlayLogging);
            this.SetValue(SOTF_DisableDeathSPectatorProperty, sourceProfile.SOTF_DisableDeathSPectator);
            this.SetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty, sourceProfile.SOTF_OnlyAdminRejoinAsSpectator);
            this.SetValue(SOTF_MaxNumberOfPlayersInTribeProperty, sourceProfile.SOTF_MaxNumberOfPlayersInTribe);
            this.SetValue(SOTF_BattleNumOfTribesToStartGameProperty, sourceProfile.SOTF_BattleNumOfTribesToStartGame);
            this.SetValue(SOTF_TimeToCollapseRODProperty, sourceProfile.SOTF_TimeToCollapseROD);
            this.SetValue(SOTF_BattleAutoStartGameIntervalProperty, sourceProfile.SOTF_BattleAutoStartGameInterval);
            this.SetValue(SOTF_BattleAutoRestartGameIntervalProperty, sourceProfile.SOTF_BattleAutoRestartGameInterval);
            this.SetValue(SOTF_BattleSuddenDeathIntervalProperty, sourceProfile.SOTF_BattleSuddenDeathInterval);

            this.SetValue(SOTF_NoEventsProperty, sourceProfile.SOTF_NoEvents);
            this.SetValue(SOTF_NoBossesProperty, sourceProfile.SOTF_NoBosses);
            this.SetValue(SOTF_BothBossesProperty, sourceProfile.SOTF_BothBosses);
            this.SetValue(SOTF_EvoEventIntervalProperty, sourceProfile.SOTF_EvoEventInterval);
            this.SetValue(SOTF_RingStartTimeProperty, sourceProfile.SOTF_RingStartTime);
        }

        private void SyncStructuresSection(ServerProfile sourceProfile)
        {
            this.SetValue(DisableStructurePlacementCollisionProperty, sourceProfile.DisableStructurePlacementCollision);
            this.SetValue(StructureResistanceMultiplierProperty, sourceProfile.StructureResistanceMultiplier);
            this.SetValue(StructureDamageMultiplierProperty, sourceProfile.StructureDamageMultiplier);
            this.SetValue(StructureDamageRepairCooldownProperty, sourceProfile.StructureDamageRepairCooldown);
            this.SetValue(PvPStructureDecayProperty, sourceProfile.PvPStructureDecay);
            this.SetValue(PvPZoneStructureDamageMultiplierProperty, sourceProfile.PvPZoneStructureDamageMultiplier);
            this.SetValue(MaxStructuresVisibleProperty, sourceProfile.MaxStructuresVisible);
            this.SetValue(PerPlatformMaxStructuresMultiplierProperty, sourceProfile.PerPlatformMaxStructuresMultiplier);
            this.SetValue(MaxPlatformSaddleStructureLimitProperty, sourceProfile.MaxPlatformSaddleStructureLimit);
            this.SetValue(OverrideStructurePlatformPreventionProperty, sourceProfile.OverrideStructurePlatformPrevention);
            this.SetValue(FlyerPlatformAllowUnalignedDinoBasingProperty, sourceProfile.FlyerPlatformAllowUnalignedDinoBasing);
            this.SetValue(PvEAllowStructuresAtSupplyDropsProperty, sourceProfile.PvEAllowStructuresAtSupplyDrops);
            this.SetValue(EnableStructureDecayPvEProperty, sourceProfile.EnableStructureDecayPvE);
            this.SetValue(PvEStructureDecayDestructionPeriodProperty, sourceProfile.PvEStructureDecayDestructionPeriod);
            this.SetValue(PvEStructureDecayPeriodMultiplierProperty, sourceProfile.PvEStructureDecayPeriodMultiplier);
            this.SetValue(AutoDestroyOldStructuresMultiplierProperty, sourceProfile.AutoDestroyOldStructuresMultiplier);
            this.SetValue(ForceAllStructureLockingProperty, sourceProfile.ForceAllStructureLocking);
            this.SetValue(PassiveDefensesDamageRiderlessDinosProperty, sourceProfile.PassiveDefensesDamageRiderlessDinos);
            this.SetValue(OnlyAutoDestroyCoreStructuresProperty, sourceProfile.OnlyAutoDestroyCoreStructures);
            this.SetValue(OnlyDecayUnsnappedCoreStructuresProperty, sourceProfile.OnlyDecayUnsnappedCoreStructures);
            this.SetValue(FastDecayUnsnappedCoreStructuresProperty, sourceProfile.FastDecayUnsnappedCoreStructures);
            this.SetValue(DestroyUnconnectedWaterPipesProperty, sourceProfile.DestroyUnconnectedWaterPipes);
            this.SetValue(EnableFastDecayIntervalProperty, sourceProfile.EnableFastDecayInterval);
            this.SetValue(FastDecayIntervalProperty, sourceProfile.FastDecayInterval);
            this.SetValue(LimitTurretsInRangeProperty, sourceProfile.LimitTurretsInRange);
            this.SetValue(LimitTurretsRangeProperty, sourceProfile.LimitTurretsRange);
            this.SetValue(LimitTurretsNumProperty, sourceProfile.LimitTurretsNum);
            this.SetValue(HardLimitTurretsInRangeProperty, sourceProfile.HardLimitTurretsInRange);
        }

        private void SyncSupplyCrateOverridesSection(ServerProfile sourceProfile)
        {
            sourceProfile.ConfigOverrideSupplyCrateItems.RenderToModel();

            this.ConfigOverrideSupplyCrateItems.Clear();
            this.ConfigOverrideSupplyCrateItems.FromIniValues(sourceProfile.ConfigOverrideSupplyCrateItems.ToIniValues());
            this.ConfigOverrideSupplyCrateItems.IsEnabled = this.ConfigOverrideSupplyCrateItems.Count > 0;
            this.ConfigOverrideSupplyCrateItems.RenderToView();
        }
        #endregion

        public static string GetProfileMapName(ServerProfile profile)
        {
            return GetProfileMapName(profile?.ServerMap, profile?.PGM_Enabled ?? false);
        }

        public static string GetProfileMapName(string serverMap, bool pgmEnabled)
        {
            if (pgmEnabled)
                return Config.Default.DefaultServerMap_PGM;

            return ModUtils.GetMapName(serverMap);
        }

        public static string GetProfileMapModId(ServerProfile profile)
        {
            return GetProfileMapModId(profile?.ServerMap, profile?.PGM_Enabled ?? false);
        }

        public static string GetProfileMapModId(string serverMap, bool pgmEnabled)
        {
            if (pgmEnabled)
                return string.Empty;

            return ModUtils.GetMapModId(serverMap);
        }

        public static string GetProfileSavePath(ServerProfile profile)
        {
            return GetProfileSavePath(profile?.InstallDirectory, profile?.AltSaveDirectoryName, profile?.PGM_Enabled ?? false, profile?.PGM_Name);
        }

        public static string GetProfileSavePath(string installDirectory, string altSaveDirectoryName, bool pgmEnabled, string pgmName)
        {
            if (!string.IsNullOrWhiteSpace(altSaveDirectoryName))
            {
                if (pgmEnabled)
                    return Path.Combine(installDirectory ?? string.Empty, Config.Default.SavedRelativePath, altSaveDirectoryName, Config.Default.SavedPGMRelativePath, pgmName ?? string.Empty);
                return Path.Combine(installDirectory ?? string.Empty, Config.Default.SavedRelativePath, altSaveDirectoryName);
            }

            if (pgmEnabled)
                return Path.Combine(installDirectory ?? string.Empty, Config.Default.SavedArksRelativePath, Config.Default.SavedPGMRelativePath, pgmName ?? string.Empty);
            return Path.Combine(installDirectory ?? string.Empty, Config.Default.SavedArksRelativePath);
        }

        public static string GetProfileMapFileName(ServerProfile profile)
        {
            return GetProfileMapFileName(profile?.ServerMap, profile?.PGM_Enabled ?? false, profile?.PGM_Name);
        }

        public static string GetProfileMapFileName(string serverMap, bool pgmEnabled, string pgmName)
        {
            if (pgmEnabled)
                return $"{pgmName ?? string.Empty}_V2";

            return ModUtils.GetMapName(serverMap);
        }
        #endregion
    }
}

using ARK_Server_Manager.Lib.Serialization;
using ARK_Server_Manager.Lib.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Xml;
using System.Xml.Serialization;

namespace ARK_Server_Manager.Lib
{
    public interface ISettingsBag
    {
        object this[string propertyName] { get; set; }
    }

    [XmlRoot("ArkServerProfile")]
    [Serializable()]
    public class ServerProfile : DependencyObject
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public enum MapSourceType
        {
            ByName,
            ById,
        };

        public static readonly DependencyProperty ProfileNameProperty = DependencyProperty.Register(nameof(ProfileName), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerProfileName));
        public static readonly DependencyProperty InstallDirectoryProperty = DependencyProperty.Register(nameof(InstallDirectory), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty LastInstalledVersionProperty = DependencyProperty.Register(nameof(LastInstalledVersion), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty AdditionalArgsProperty = DependencyProperty.Register(nameof(AdditionalArgs), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty RCONEnabledProperty = DependencyProperty.Register(nameof(RCONEnabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty RCONPortProperty = DependencyProperty.Register(nameof(RCONPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(32330));
        public static readonly DependencyProperty ServerMapProperty = DependencyProperty.Register(nameof(ServerMap), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerMap));


        public MapSourceType MapSource
        {
            get { return (MapSourceType)GetValue(MapSourceProperty); }
            set { SetValue(MapSourceProperty, value); }
        }

        public static readonly DependencyProperty MapSourceProperty = DependencyProperty.Register(nameof(MapSource), typeof(MapSourceType), typeof(ServerProfile), new PropertyMetadata(MapSourceType.ByName));

        public string ProfileName
        {
            get { return (string)GetValue(ProfileNameProperty); }
            set { SetValue(ProfileNameProperty, value); }
        }

        public string InstallDirectory
        {
            get { return (string)GetValue(InstallDirectoryProperty); }
            set { SetValue(InstallDirectoryProperty, value); }
        }

        public string LastInstalledVersion
        {
            get { return (string)GetValue(LastInstalledVersionProperty); }
            set { SetValue(LastInstalledVersionProperty, value); }
        }

        public string AdditionalArgs
        {
            get { return (string)GetValue(AdditionalArgsProperty); }
            set { SetValue(AdditionalArgsProperty, value); }
        }

        public bool RCONEnabled
        {
            get { return (bool)GetValue(RCONEnabledProperty); }
            set { SetValue(RCONEnabledProperty, value); }
        }

        public int RCONPort
        {
            get { return (int)GetValue(RCONPortProperty); }
            set { SetValue(RCONPortProperty, value); }
        }

        public string ServerMap
        {
            get { return (string)GetValue(ServerMapProperty); }
            set { SetValue(ServerMapProperty, value); }
        }

        public int ServerMapModId
        {
            get { return (int)GetValue(ServerMapModIdProperty); }
            set { SetValue(ServerMapModIdProperty, value); }
        }

        public static readonly DependencyProperty ServerMapModIdProperty = DependencyProperty.Register(nameof(ServerMapModId), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));


        #region Server properties

        public static readonly DependencyProperty EnableGlobalVoiceChatProperty = DependencyProperty.Register(nameof(EnableGlobalVoiceChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty EnableProximityChatProperty = DependencyProperty.Register(nameof(EnableProximityChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty EnableTributeDownloadsProperty = DependencyProperty.Register(nameof(EnableTributeDownloads), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty EnableFlyerCarryProperty = DependencyProperty.Register(nameof(EnableFlyerCarry), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty EnableAllowCaveFlyersProperty = DependencyProperty.Register(nameof(EnableAllowCaveFlyers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty EnableStructureDecayProperty = DependencyProperty.Register(nameof(EnableStructureDecay), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty EnablePlayerLeaveNotificationsProperty = DependencyProperty.Register(nameof(EnablePlayerLeaveNotifications), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty EnablePlayerJoinedNotificationsProperty = DependencyProperty.Register(nameof(EnablePlayerJoinedNotifications), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty EnableHardcoreProperty = DependencyProperty.Register(nameof(EnableHardcore), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty EnablePVPProperty = DependencyProperty.Register(nameof(EnablePVP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty AllowCrosshairProperty = DependencyProperty.Register(nameof(AllowCrosshair), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty AllowHUDProperty = DependencyProperty.Register(nameof(AllowHUD), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty AllowThirdPersonViewProperty = DependencyProperty.Register(nameof(AllowThirdPersonView), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty AllowMapPlayerLocationProperty = DependencyProperty.Register(nameof(AllowMapPlayerLocation), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        public static readonly DependencyProperty AllowPVPGammaProperty = DependencyProperty.Register(nameof(AllowPVPGamma), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty ServerPasswordProperty = DependencyProperty.Register(nameof(ServerPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty AdminPasswordProperty = DependencyProperty.Register(nameof(AdminPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty MaxPlayersProperty = DependencyProperty.Register(nameof(MaxPlayers), typeof(int), typeof(ServerProfile), new PropertyMetadata(5));
        public static readonly DependencyProperty DifficultyOffsetProperty = DependencyProperty.Register(nameof(DifficultyOffset), typeof(float), typeof(ServerProfile), new PropertyMetadata(0.25f));
        public static readonly DependencyProperty MaxStructuresVisibleProperty = DependencyProperty.Register(nameof(MaxStructuresVisible), typeof(float), typeof(ServerProfile), new PropertyMetadata(1300f));
        public static readonly DependencyProperty ServerNameProperty = DependencyProperty.Register(nameof(ServerName), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerName));
        public static readonly DependencyProperty ServerPortProperty = DependencyProperty.Register(nameof(ServerPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(27015));
        public static readonly DependencyProperty ServerConnectionPortProperty = DependencyProperty.Register(nameof(ServerConnectionPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(7777));
        public static readonly DependencyProperty ServerIPProperty = DependencyProperty.Register(nameof(ServerIP), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty MOTDProperty = DependencyProperty.Register(nameof(MOTD), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty MOTDDurationProperty = DependencyProperty.Register(nameof(MOTDDuration), typeof(int), typeof(ServerProfile), new PropertyMetadata(20));
        public static readonly DependencyProperty EnableKickIdlePlayersProperty = DependencyProperty.Register(nameof(EnableKickIdlePlayers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty KickIdlePlayersPeriodProperty = DependencyProperty.Register(nameof(KickIdlePlayersPeriod), typeof(float), typeof(ServerProfile), new PropertyMetadata(2400.0f));
        public static readonly DependencyProperty AutoSavePeriodMinutesProperty = DependencyProperty.Register(nameof(AutoSavePeriodMinutes), typeof(float), typeof(ServerProfile), new PropertyMetadata(15.0f));
        public static readonly DependencyProperty TamingSpeedMultiplierProperty = DependencyProperty.Register(nameof(TamingSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty HarvestAmountMultiplierProperty = DependencyProperty.Register(nameof(HarvestAmountMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PlayerCharacterWaterDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterWaterDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PlayerCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PlayerCharacterStaminaDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterStaminaDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DinoCharacterStaminaDrainMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterStaminaDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PlayerCharacterHealthRecoveryMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterHealthRecoveryMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DinoCharacterHealthRecoveryMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterHealthRecoveryMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DinoCountMultiplierProperty = DependencyProperty.Register(nameof(DinoCountMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty HarvestHealthMultiplierProperty = DependencyProperty.Register(nameof(HarvestHealthMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PvEStructureDecayDestructionPeriodProperty = DependencyProperty.Register(nameof(PvEStructureDecayDestructionPeriod), typeof(float), typeof(ServerProfile), new PropertyMetadata(0f));
        public static readonly DependencyProperty PvEStructureDecayPeriodMultiplierProperty = DependencyProperty.Register(nameof(PvEStructureDecayPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty ResourcesRespawnPeriodMultiplierProperty = DependencyProperty.Register(nameof(ResourcesRespawnPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty ClampResourceHarvestDamageProperty = DependencyProperty.Register(nameof(ClampResourceHarvestDamage), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty DayCycleSpeedScaleProperty = DependencyProperty.Register(nameof(DayCycleSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty NightTimeSpeedScaleProperty = DependencyProperty.Register(nameof(NightTimeSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DayTimeSpeedScaleProperty = DependencyProperty.Register(nameof(DayTimeSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DinoDamageMultiplierProperty = DependencyProperty.Register(nameof(DinoDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty TamedDinoDamageMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PlayerDamageMultiplierProperty = DependencyProperty.Register(nameof(PlayerDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty StructureDamageMultiplierProperty = DependencyProperty.Register(nameof(StructureDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PlayerResistanceMultiplierProperty = DependencyProperty.Register(nameof(PlayerResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty DinoResistanceMultiplierProperty = DependencyProperty.Register(nameof(DinoResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty TamedDinoResistanceMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty StructureResistanceMultiplierProperty = DependencyProperty.Register(nameof(StructureResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty XPMultiplierProperty = DependencyProperty.Register(nameof(XPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        public static readonly DependencyProperty DinoSpawnsProperty = DependencyProperty.Register(nameof(DinoSpawnWeightMultipliers), typeof(AggregateIniValueList<DinoSpawn>), typeof(ServerProfile), new PropertyMetadata(null));
        public static readonly DependencyProperty EnableLevelProgressionsProperty = DependencyProperty.Register(nameof(EnableLevelProgressions), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty PlayerLevelsProperty = DependencyProperty.Register(nameof(PlayerLevels), typeof(LevelList), typeof(ServerProfile), new PropertyMetadata());
        public static readonly DependencyProperty DinoLevelsProperty = DependencyProperty.Register(nameof(DinoLevels), typeof(LevelList), typeof(ServerProfile), new PropertyMetadata());
        public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.Register(nameof(IsDirty), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public static readonly DependencyProperty GlobalSpoilingTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalSpoilingTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty GlobalCorpseDecompositionTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalCorpseDecompositionTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty OverrideMaxExperiencePointsDinoProperty = DependencyProperty.Register(nameof(OverrideMaxExperiencePointsDino), typeof(int), typeof(ServerProfile), new PropertyMetadata(GameData.DEFAULT_MAX_EXPERIENCE_POINTS_DINO));
        public static readonly DependencyProperty OverrideMaxExperiencePointsPlayerProperty = DependencyProperty.Register(nameof(OverrideMaxExperiencePointsPlayer), typeof(int), typeof(ServerProfile), new PropertyMetadata(GameData.DEFAULT_MAX_EXPERIENCE_POINTS_PLAYER));
        public static readonly DependencyProperty GlobalItemDecompositionTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalItemDecompositionTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        public static readonly DependencyProperty CropDecaySpeedMultiplierProperty = DependencyProperty.Register(nameof(CropDecaySpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty CropGrowthSpeedMultiplierProperty = DependencyProperty.Register(nameof(CropGrowthSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty LayEggIntervalMultiplierProperty = DependencyProperty.Register(nameof(LayEggIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty PoopIntervalMultiplierProperty = DependencyProperty.Register(nameof(PoopIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty FlyerPlatformAllowUnalignedDinoBasingProperty = DependencyProperty.Register(nameof(FlyerPlatformAllowUnalignedDinoBasing), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public static readonly DependencyProperty MatingIntervalMultiplierProperty = DependencyProperty.Register(nameof(MatingIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty EggHatchSpeedMultiplierProperty = DependencyProperty.Register(nameof(EggHatchSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty BabyMatureSpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyMatureSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty BabyFoodConsumptionSpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyFoodConsumptionSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        public static readonly DependencyProperty CustomRecipeEffectivenessMultiplierProperty = DependencyProperty.Register(nameof(CustomRecipeEffectivenessMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public static readonly DependencyProperty CustomRecipeSkillMultiplierProperty = DependencyProperty.Register(nameof(CustomRecipeSkillMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "GlobalVoiceChat", ClearIfDefault = true)]
        public bool EnableGlobalVoiceChat
        {
            get { return (bool)GetValue(EnableGlobalVoiceChatProperty); }
            set { SetValue(EnableGlobalVoiceChatProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ProximityVoiceChat")]
        public bool EnableProximityChat
        {
            get { return (bool)GetValue(EnableProximityChatProperty); }
            set { SetValue(EnableProximityChatProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "NoTributeDownloads", InvertBoolean = true)]
        public bool EnableTributeDownloads
        {
            get { return (bool)GetValue(EnableTributeDownloadsProperty); }
            set { SetValue(EnableTributeDownloadsProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "bAllowFlyerCarryPVE")]
        public bool EnableFlyerCarry
        {
            get { return (bool)GetValue(EnableFlyerCarryProperty); }
            set { SetValue(EnableFlyerCarryProperty, value); }
        }

        public bool EnableAllowCaveFlyers
        {
            get { return (bool)GetValue(EnableAllowCaveFlyersProperty); }
            set { SetValue(EnableAllowCaveFlyersProperty, value); }
        }



        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEDisableFriendlyFire")]
        public bool DisableFriendlyFirePvE
        {
            get { return (bool)GetValue(DisableFriendlyFirePvEProperty); }
            set { SetValue(DisableFriendlyFirePvEProperty, value); }
        }

        public static readonly DependencyProperty DisableFriendlyFirePvEProperty = DependencyProperty.Register(nameof(DisableFriendlyFirePvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableFriendlyFire")]
        public bool DisableFriendlyFirePvP
        {
            get { return (bool)GetValue(DisableFriendlyFirePvPProperty); }
            set { SetValue(DisableFriendlyFirePvPProperty, value); }
        }

        public static readonly DependencyProperty DisableFriendlyFirePvPProperty = DependencyProperty.Register(nameof(DisableFriendlyFirePvP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableLootCrates")]
        public bool DisableLootCrates
        {
            get { return (bool)GetValue(DisableLootCratesProperty); }
            set { SetValue(DisableLootCratesProperty, value); }
        }

        public static readonly DependencyProperty DisableLootCratesProperty = DependencyProperty.Register(nameof(DisableLootCrates), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowCaveBuildingPvE
        {
            get { return (bool)GetValue(AllowCaveBuildingPvEProperty); }
            set { SetValue(AllowCaveBuildingPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowCaveBuildingPvEProperty = DependencyProperty.Register(nameof(AllowCaveBuildingPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "bDisableStructureDecayPVE", InvertBoolean = true)]
        public bool EnableStructureDecay
        {
            get { return (bool)GetValue(EnableStructureDecayProperty); }
            set { SetValue(EnableStructureDecayProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PvPStructureDecay
        {
            get { return (bool)GetValue(PvPStructureDecayProperty); }
            set { SetValue(PvPStructureDecayProperty, value); }
        }

        public static readonly DependencyProperty PvPStructureDecayProperty = DependencyProperty.Register(nameof(PvPStructureDecay), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEDinoDecayPeriodMultiplier
        {
            get { return (float)GetValue(PvEDinoDecayPeriodMultiplierProperty); }
            set { SetValue(PvEDinoDecayPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PvEDinoDecayPeriodMultiplierProperty = DependencyProperty.Register(nameof(PvEDinoDecayPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AdminLogging
        {
            get { return (bool)GetValue(AdminLoggingProperty); }
            set { SetValue(AdminLoggingProperty, value); }
        }

        public static readonly DependencyProperty AdminLoggingProperty = DependencyProperty.Register(nameof(AdminLogging), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableBanListURL), QuotedString = true)]
        public string BanListURL
        {
            get { return (string)GetValue(BanListURLProperty); }
            set { SetValue(BanListURLProperty, value); }
        }

        public static readonly DependencyProperty BanListURLProperty = DependencyProperty.Register(nameof(BanListURL), typeof(string), typeof(ServerProfile), new PropertyMetadata("\"http://playark.com/banlist.txt\""));

        public bool EnableBanListURL
        {
            get { return (bool)GetValue(EnableBanListURLProperty); }
            set { SetValue(EnableBanListURLProperty, value); }
        }

        public static readonly DependencyProperty EnableBanListURLProperty = DependencyProperty.Register(nameof(EnableBanListURL), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));



        public bool UseRawSockets
        {
            get { return (bool)GetValue(UseRawSocketsProperty); }
            set { SetValue(UseRawSocketsProperty, value); }
        }

        public static readonly DependencyProperty UseRawSocketsProperty = DependencyProperty.Register(nameof(UseRawSockets), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool UseBattlEye
        {
            get { return (bool)GetValue(UseBattlEyeProperty); }
            set { SetValue(UseBattlEyeProperty, value); }
        }

        public static readonly DependencyProperty UseBattlEyeProperty = DependencyProperty.Register(nameof(UseBattlEye), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));


        public static readonly DependencyProperty EnableExtinctionEventProperty = DependencyProperty.Register(nameof(EnableExtinctionEvent), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

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


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PerPlatformMaxStructuresMultiplier
        {
            get { return (float)GetValue(PerPlatformMaxStructuresMultiplierProperty); }
            set { SetValue(PerPlatformMaxStructuresMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PerPlatformMaxStructuresMultiplierProperty = DependencyProperty.Register(nameof(PerPlatformMaxStructuresMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.25f));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int MaxPlatformSaddleStructureLimit
        {
            get { return (int)GetValue(MaxPlatformSaddleStructureLimitProperty); }
            set { SetValue(MaxPlatformSaddleStructureLimitProperty, value); }
        }

        public static readonly DependencyProperty MaxPlatformSaddleStructureLimitProperty = DependencyProperty.Register(nameof(MaxPlatformSaddleStructureLimit), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DisableDinoDecayPvE
        {
            get { return (bool)GetValue(DisableDinoDecayPvEProperty); }
            set { SetValue(DisableDinoDecayPvEProperty, value); }
        }

        public static readonly DependencyProperty DisableDinoDecayPvEProperty = DependencyProperty.Register(nameof(DisableDinoDecayPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "AlwaysNotifyPlayerLeft")]
        public bool EnablePlayerLeaveNotifications
        {
            get { return (bool)GetValue(EnablePlayerLeaveNotificationsProperty); }
            set { SetValue(EnablePlayerLeaveNotificationsProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DontAlwaysNotifyPlayerJoined", InvertBoolean = true)]
        public bool EnablePlayerJoinedNotifications
        {
            get { return (bool)GetValue(EnablePlayerJoinedNotificationsProperty); }
            set { SetValue(EnablePlayerJoinedNotificationsProperty, value); }
        }
        
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerHardcore")]        
        public bool EnableHardcore
        {
            get { return (bool)GetValue(EnableHardcoreProperty); }
            set { SetValue(EnableHardcoreProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerPVE", InvertBoolean = true)]
        public bool EnablePVP
        {
            get { return (bool)GetValue(EnablePVPProperty); }
            set { SetValue(EnablePVPProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerCrosshair")]
        public bool AllowCrosshair
        {
            get { return (bool)GetValue(AllowCrosshairProperty); }
            set { SetValue(AllowCrosshairProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerForceNoHud", InvertBoolean = true)]
        public bool AllowHUD
        {
            get { return (bool)GetValue(AllowHUDProperty); }
            set { SetValue(AllowHUDProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "AllowThirdPersonPlayer")]
        public bool AllowThirdPersonView
        {
            get { return (bool)GetValue(AllowThirdPersonViewProperty); }
            set { SetValue(AllowThirdPersonViewProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ShowMapPlayerLocation")]
        public bool AllowMapPlayerLocation
        {
            get { return (bool)GetValue(AllowMapPlayerLocationProperty); }
            set { SetValue(AllowMapPlayerLocationProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "EnablePVPGamma")]
        public bool AllowPVPGamma
        {
            get { return (bool)GetValue(AllowPVPGammaProperty); }
            set { SetValue(AllowPVPGammaProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DisablePvEGamma", InvertBoolean = true)]
        public bool AllowPvEGamma
        {
            get { return (bool)GetValue(AllowPvEGammaProperty); }
            set { SetValue(AllowPvEGammaProperty, value); }
        }

        public static readonly DependencyProperty AllowPvEGammaProperty = DependencyProperty.Register(nameof(AllowPvEGamma), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerPassword")]        
        public string ServerPassword
        {
            get { return (string)GetValue(ServerPasswordProperty); }
            set { SetValue(ServerPasswordProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerAdminPassword")]
        public string AdminPassword
        {
            get { return (string)GetValue(AdminPasswordProperty); }
            set { SetValue(AdminPasswordProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.GameSession, "MaxPlayers")]        
        public int MaxPlayers
        {
            get { return (int)GetValue(MaxPlayersProperty); }
            set { SetValue(MaxPlayersProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DifficultyOffset")]
        public float DifficultyOffset
        {
            get { return (float)GetValue(DifficultyOffsetProperty); }
            set { SetValue(DifficultyOffsetProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "NewMaxStructuresInRange")]
        public float MaxStructuresVisible
        {
            get { return (float)GetValue(MaxStructuresVisibleProperty); }
            set { SetValue(MaxStructuresVisibleProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "SessionName")]  
        public string ServerName
        {
            get { return (string)GetValue(ServerNameProperty); }
            set { SetValue(ServerNameProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "QueryPort")]
        public int ServerPort
        {
            get { return (int)GetValue(ServerPortProperty); }
            set { SetValue(ServerPortProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "Port")]
        public int ServerConnectionPort
        {
            get { return (int)GetValue(ServerConnectionPortProperty); }
            set { SetValue(ServerConnectionPortProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "MultiHome")]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MultiHome, "MultiHome", WriteBoolValueIfNonEmpty = true)]
        public string ServerIP
        {
            get { return (string)GetValue(ServerIPProperty); }
            set { SetValue(ServerIPProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MessageOfTheDay, "Message", ClearSection = true, Multiline = true)]
        public string MOTD
        {
            get { return (string)GetValue(MOTDProperty); }
            set { SetValue(MOTDProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MessageOfTheDay, "Duration")]
        public int MOTDDuration
        {
            get { return (int)GetValue(MOTDDurationProperty); }
            set { SetValue(MOTDDurationProperty, value); }
        }

        public bool EnableKickIdlePlayers
        {
            get { return (bool)GetValue(EnableKickIdlePlayersProperty); }
            set { SetValue(EnableKickIdlePlayersProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableKickIdlePlayers))]
        public float KickIdlePlayersPeriod
        {
            get { return (float)GetValue(KickIdlePlayersPeriodProperty); }
            set { SetValue(KickIdlePlayersPeriodProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float AutoSavePeriodMinutes
        {
            get { return (float)GetValue(AutoSavePeriodMinutesProperty); }
            set { SetValue(AutoSavePeriodMinutesProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamingSpeedMultiplier
        {
            get { return (float)GetValue(TamingSpeedMultiplierProperty); }
            set { SetValue(TamingSpeedMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float HarvestAmountMultiplier
        {
            get { return (float)GetValue(HarvestAmountMultiplierProperty); }
            set { SetValue(HarvestAmountMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterWaterDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterWaterDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterWaterDrainMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterFoodDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterFoodDrainMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(DinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(DinoCharacterFoodDrainMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterStaminaDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterStaminaDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterStaminaDrainMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterStaminaDrainMultiplier
        {
            get { return (float)GetValue(DinoCharacterStaminaDrainMultiplierProperty); }
            set { SetValue(DinoCharacterStaminaDrainMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterHealthRecoveryMultiplier
        {
            get { return (float)GetValue(PlayerCharacterHealthRecoveryMultiplierProperty); }
            set { SetValue(PlayerCharacterHealthRecoveryMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterHealthRecoveryMultiplier
        {
            get { return (float)GetValue(DinoCharacterHealthRecoveryMultiplierProperty); }
            set { SetValue(DinoCharacterHealthRecoveryMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCountMultiplier
        {
            get { return (float)GetValue(DinoCountMultiplierProperty); }
            set { SetValue(DinoCountMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float HarvestHealthMultiplier
        {
            get { return (float)GetValue(HarvestHealthMultiplierProperty); }
            set { SetValue(HarvestHealthMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEStructureDecayDestructionPeriod
        {
            get { return (float)GetValue(PvEStructureDecayDestructionPeriodProperty); }
            set { SetValue(PvEStructureDecayDestructionPeriodProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEStructureDecayPeriodMultiplier
        {
            get { return (float)GetValue(PvEStructureDecayPeriodMultiplierProperty); }
            set { SetValue(PvEStructureDecayPeriodMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float ResourcesRespawnPeriodMultiplier
        {
            get { return (float)GetValue(ResourcesRespawnPeriodMultiplierProperty); }
            set { SetValue(ResourcesRespawnPeriodMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool ClampResourceHarvestDamage
        {
            get { return (bool)GetValue(ClampResourceHarvestDamageProperty); }
            set { SetValue(ClampResourceHarvestDamageProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DayCycleSpeedScale
        {
            get { return (float)GetValue(DayCycleSpeedScaleProperty); }
            set { SetValue(DayCycleSpeedScaleProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float NightTimeSpeedScale
        {
            get { return (float)GetValue(NightTimeSpeedScaleProperty); }
            set { SetValue(NightTimeSpeedScaleProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DayTimeSpeedScale
        {
            get { return (float)GetValue(DayTimeSpeedScaleProperty); }
            set { SetValue(DayTimeSpeedScaleProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoDamageMultiplier
        {
            get { return (float)GetValue(DinoDamageMultiplierProperty); }
            set { SetValue(DinoDamageMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamedDinoDamageMultiplier
        {
            get { return (float)GetValue(TamedDinoDamageMultiplierProperty); }
            set { SetValue(TamedDinoDamageMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerDamageMultiplier
        {
            get { return (float)GetValue(PlayerDamageMultiplierProperty); }
            set { SetValue(PlayerDamageMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float StructureDamageMultiplier
        {
            get { return (float)GetValue(StructureDamageMultiplierProperty); }
            set { SetValue(StructureDamageMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerResistanceMultiplier
        {
            get { return (float)GetValue(PlayerResistanceMultiplierProperty); }
            set { SetValue(PlayerResistanceMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoResistanceMultiplier
        {
            get { return (float)GetValue(DinoResistanceMultiplierProperty); }
            set { SetValue(DinoResistanceMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamedDinoResistanceMultiplier
        {
            get { return (float)GetValue(TamedDinoResistanceMultiplierProperty); }
            set { SetValue(TamedDinoResistanceMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float StructureResistanceMultiplier
        {
            get { return (float)GetValue(StructureResistanceMultiplierProperty); }
            set { SetValue(StructureResistanceMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PvPZoneStructureDamageMultiplier
        {
            get { return (float)GetValue(PvPZoneStructureDamageMultiplierProperty); }
            set { SetValue(PvPZoneStructureDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PvPZoneStructureDamageMultiplierProperty = DependencyProperty.Register(nameof(PvPZoneStructureDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(6.0f));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventDownloadSurvivors
        {
            get { return (bool)GetValue(PreventDownloadSurvivorsProperty); }
            set { SetValue(PreventDownloadSurvivorsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadSurvivorsProperty = DependencyProperty.Register(nameof(PreventDownloadSurvivors), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));



        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventDownloadItems
        {
            get { return (bool)GetValue(PreventDownloadItemsProperty); }
            set { SetValue(PreventDownloadItemsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadItemsProperty = DependencyProperty.Register(nameof(PreventDownloadItems), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));



        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventDownloadDinos
        {
            get { return (bool)GetValue(PreventDownloadDinosProperty); }
            set { SetValue(PreventDownloadDinosProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadDinosProperty = DependencyProperty.Register(nameof(PreventDownloadDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float XPMultiplier
        {
            get { return (float)GetValue(XPMultiplierProperty); }
            set { SetValue(XPMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalSpoilingTimeMultiplier
        {
            get { return (float)GetValue(GlobalSpoilingTimeMultiplierProperty); }
            set { SetValue(GlobalSpoilingTimeMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalItemDecompositionTimeMultiplier
        {
            get { return (float)GetValue(GlobalItemDecompositionTimeMultiplierProperty); }
            set { SetValue(GlobalItemDecompositionTimeMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalCorpseDecompositionTimeMultiplier
        {
            get { return (float)GetValue(GlobalCorpseDecompositionTimeMultiplierProperty); }
            set { SetValue(GlobalCorpseDecompositionTimeMultiplierProperty, value); }
        }


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CropDecaySpeedMultiplier
        {
            get { return (float)GetValue(CropDecaySpeedMultiplierProperty); }
            set { SetValue(CropDecaySpeedMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CropGrowthSpeedMultiplier
        {
            get { return (float)GetValue(CropGrowthSpeedMultiplierProperty); }
            set { SetValue(CropGrowthSpeedMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float LayEggIntervalMultiplier
        {
            get { return (float)GetValue(LayEggIntervalMultiplierProperty); }
            set { SetValue(LayEggIntervalMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PoopIntervalMultiplier
        {
            get { return (float)GetValue(PoopIntervalMultiplierProperty); }
            set { SetValue(PoopIntervalMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bFlyerPlatformAllowUnalignedDinoBasing")]
        public bool FlyerPlatformAllowUnalignedDinoBasing
        {
            get { return (bool)GetValue(FlyerPlatformAllowUnalignedDinoBasingProperty); }
            set { SetValue(FlyerPlatformAllowUnalignedDinoBasingProperty, value); }
        }


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float MatingIntervalMultiplier
        {
            get { return (float)GetValue(MatingIntervalMultiplierProperty); }
            set { SetValue(MatingIntervalMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float EggHatchSpeedMultiplier
        {
            get { return (float)GetValue(EggHatchSpeedMultiplierProperty); }
            set { SetValue(EggHatchSpeedMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyMatureSpeedMultiplier
        {
            get { return (float)GetValue(BabyMatureSpeedMultiplierProperty); }
            set { SetValue(BabyMatureSpeedMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyFoodConsumptionSpeedMultiplier
        {
            get { return (float)GetValue(BabyFoodConsumptionSpeedMultiplierProperty); }
            set { SetValue(BabyFoodConsumptionSpeedMultiplierProperty, value); }
        }


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CustomRecipeEffectivenessMultiplier
        {
            get { return (float)GetValue(CustomRecipeEffectivenessMultiplierProperty); }
            set { SetValue(CustomRecipeEffectivenessMultiplierProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CustomRecipeSkillMultiplier
        {
            get { return (float)GetValue(CustomRecipeSkillMultiplierProperty); }
            set { SetValue(CustomRecipeSkillMultiplierProperty, value); }
        }


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int OverrideMaxExperiencePointsPlayer
        {
            get { return (int)GetValue(OverrideMaxExperiencePointsPlayerProperty); }
            set { SetValue(OverrideMaxExperiencePointsPlayerProperty, value); }
        }

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int OverrideMaxExperiencePointsDino
        {
            get { return (int)GetValue(OverrideMaxExperiencePointsDinoProperty); }
            set { SetValue(OverrideMaxExperiencePointsDinoProperty, value); }
        }


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float ResourceNoReplenishRadiusPlayers
        {
            get { return (float)GetValue(ResourceNoReplenishRadiusPlayersProperty); }
            set { SetValue(ResourceNoReplenishRadiusPlayersProperty, value); }
        }

        public static readonly DependencyProperty ResourceNoReplenishRadiusPlayersProperty = DependencyProperty.Register(nameof(ResourceNoReplenishRadiusPlayers), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float ResourceNoReplenishRadiusStructures
        {
            get { return (float)GetValue(ResourceNoReplenishRadiusStructuresProperty); }
            set { SetValue(ResourceNoReplenishRadiusStructuresProperty, value); }
        }

        public static readonly DependencyProperty ResourceNoReplenishRadiusStructuresProperty = DependencyProperty.Register(nameof(ResourceNoReplenishRadiusStructures), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bIncreasePvPRespawnInterval")]
        public bool IncreasePvPRespawnInterval
        {
            get { return (bool)GetValue(IncreasePvPRespawnIntervalProperty); }
            set { SetValue(IncreasePvPRespawnIntervalProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnInterval), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public int IncreasePvPRespawnIntervalCheckPeriod
        {
            get { return (int)GetValue(IncreasePvPRespawnIntervalCheckPeriodProperty); }
            set { SetValue(IncreasePvPRespawnIntervalCheckPeriodProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalCheckPeriodProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalCheckPeriod), typeof(int), typeof(ServerProfile), new PropertyMetadata(300));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public float IncreasePvPRespawnIntervalMultiplier
        {
            get { return (float)GetValue(IncreasePvPRespawnIntervalMultiplierProperty); }
            set { SetValue(IncreasePvPRespawnIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalMultiplierProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public int IncreasePvPRespawnIntervalBaseAmount
        {
            get { return (int)GetValue(IncreasePvPRespawnIntervalBaseAmountProperty); }
            set { SetValue(IncreasePvPRespawnIntervalBaseAmountProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalBaseAmountProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalBaseAmount), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bAutoPvETimer")]
        public bool AutoPvETimer
        {
            get { return (bool)GetValue(AutoPvETimerProperty); }
            set { SetValue(AutoPvETimerProperty, value); }
        }

        public static readonly DependencyProperty AutoPvETimerProperty = DependencyProperty.Register(nameof(AutoPvETimer), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bAutoPvEUseSystemTime", ConditionedOn = nameof(AutoPvETimer))]
        public bool AutoPvEUseSystemTime
        {
            get { return (bool)GetValue(AutoPvEUseSystemTimeProperty); }
            set { SetValue(AutoPvEUseSystemTimeProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEUseSystemTimeProperty = DependencyProperty.Register(nameof(AutoPvEUseSystemTime), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AutoPvETimer))]
        public int AutoPvEStartTimeSeconds
        {
            get { return (int)GetValue(AutoPvEStartTimeSecondsProperty); }
            set { SetValue(AutoPvEStartTimeSecondsProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEStartTimeSecondsProperty = DependencyProperty.Register(nameof(AutoPvEStartTimeSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AutoPvETimer))]
        public int AutoPvEStopTimeSeconds
        {
            get { return (int)GetValue(AutoPvEStopTimeSecondsProperty); }
            set { SetValue(AutoPvEStopTimeSecondsProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEStopTimeSecondsProperty = DependencyProperty.Register(nameof(AutoPvEStopTimeSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));      

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int MaxTamedDinos
        {
            get { return (int)GetValue(MaxTamedDinosProperty); }
            set { SetValue(MaxTamedDinosProperty, value); }
        }

        public static readonly DependencyProperty MaxTamedDinosProperty = DependencyProperty.Register(nameof(MaxTamedDinos), typeof(int), typeof(ServerProfile), new PropertyMetadata(4000));


        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public string SpectatorPassword
        {
            get { return (string)GetValue(SpectatorPasswordProperty); }
            set { SetValue(SpectatorPasswordProperty, value); }
        }

        public static readonly DependencyProperty SpectatorPasswordProperty = DependencyProperty.Register(nameof(SpectatorPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "ActiveMods")]
        public string ServerModIds
        {
            get { return (string)GetValue(ServerModIdsProperty); }
            set { SetValue(ServerModIdsProperty, value); }
        }

        public static readonly DependencyProperty ServerModIdsProperty = DependencyProperty.Register(nameof(ServerModIds), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));


        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "DinoHarvestingDamageMultiplier")]
        public float HarvestingDamageMultiplierDino
        {
            get { return (float)GetValue(HarvestingDamageMultiplierDinoProperty); }
            set { SetValue(HarvestingDamageMultiplierDinoProperty, value); }
        }

        public static readonly DependencyProperty HarvestingDamageMultiplierDinoProperty = DependencyProperty.Register(nameof(HarvestingDamageMultiplierDino), typeof(float), typeof(ServerProfile), new PropertyMetadata(3.0f));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "PlayerHarvestingDamageMultiplier")]
        public float HarvestingDamageMultiplierPlayer
        {
            get { return (float)GetValue(HarvestingDamageMultiplierPlayerProperty); }
            set { SetValue(HarvestingDamageMultiplierPlayerProperty, value); }
        }

        public static readonly DependencyProperty HarvestingDamageMultiplierPlayerProperty = DependencyProperty.Register(nameof(HarvestingDamageMultiplierPlayer), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "DinoTurretDamageMultiplier")]
        public float TurretDamageMultiplierDino
        {
            get { return (float)GetValue(TurretDamageMultiplierDinoProperty); }
            set { SetValue(TurretDamageMultiplierDinoProperty, value); }
        }

        public static readonly DependencyProperty TurretDamageMultiplierDinoProperty = DependencyProperty.Register(nameof(TurretDamageMultiplierDino), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int MaxTribeLogs
        {
            get { return (int)GetValue(MaxTribeLogsProperty); }
            set { SetValue(MaxTribeLogsProperty, value); }
        }

        public static readonly DependencyProperty MaxTribeLogsProperty = DependencyProperty.Register(nameof(MaxTribeLogs), typeof(int), typeof(ServerProfile), new PropertyMetadata(100));


        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<DinoSpawn> DinoSpawnWeightMultipliers
        {
            get { return (AggregateIniValueList<DinoSpawn>)GetValue(DinoSpawnsProperty); }
            set { SetValue(DinoSpawnsProperty, value); }
        }

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]        
        public AggregateIniValueList<ClassMultiplier> TamedDinoClassDamageMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(TamedDinoClassDamageMultipliersProperty); }
            set { SetValue(TamedDinoClassDamageMultipliersProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoClassDamageMultipliersProperty = DependencyProperty.Register(nameof(TamedDinoClassDamageMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));


        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> TamedDinoClassResistanceMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(TamedDinoClassResistanceMultipliersProperty); }
            set { SetValue(TamedDinoClassResistanceMultipliersProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoClassResistanceMultipliersProperty = DependencyProperty.Register(nameof(TamedDinoClassResistanceMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));



        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> DinoClassDamageMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(DinoClassDamageMultipliersProperty); }
            set { SetValue(DinoClassDamageMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DinoClassDamageMultipliersProperty = DependencyProperty.Register(nameof(DinoClassDamageMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));


        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<NPCReplacement> NPCReplacements
        {
            get { return (AggregateIniValueList<NPCReplacement>)GetValue(NPCReplacementsProperty); }
            set { SetValue(NPCReplacementsProperty, value); }
        }

        public static readonly DependencyProperty NPCReplacementsProperty = DependencyProperty.Register(nameof(NPCReplacements), typeof(AggregateIniValueList<NPCReplacement>), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> DinoClassResistanceMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(DinoClassResistanceMultipliersProperty); }
            set { SetValue(DinoClassResistanceMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DinoClassResistanceMultipliersProperty = DependencyProperty.Register(nameof(DinoClassResistanceMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> HarvestResourceItemAmountClassMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(HarvestResourceItemAmountClassMultipliersProperty); }
            set { SetValue(HarvestResourceItemAmountClassMultipliersProperty, value); }
        }

        public static readonly DependencyProperty HarvestResourceItemAmountClassMultipliersProperty = DependencyProperty.Register(nameof(HarvestResourceItemAmountClassMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<EngramEntry> OverrideNamedEngramEntries
        {
            get { return (AggregateIniValueList<EngramEntry>)GetValue(OverrideNamedEngramEntriesProperty); }
            set { SetValue(OverrideNamedEngramEntriesProperty, value); }
        }

        public static readonly DependencyProperty OverrideNamedEngramEntriesProperty = DependencyProperty.Register(nameof(OverrideNamedEngramEntries), typeof(AggregateIniValueList<EngramEntry>), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StringIniValueList PreventDinoTameClassNames
        {
            get { return (StringIniValueList)GetValue(PreventDinoTameClassNamesProperty); }
            set { SetValue(PreventDinoTameClassNamesProperty, value); }
        }

        public static readonly DependencyProperty PreventDinoTameClassNamesProperty = DependencyProperty.Register(nameof(PreventDinoTameClassNames), typeof(StringIniValueList), typeof(ServerProfile), new PropertyMetadata(null));

        public bool EnableLevelProgressions
        {
            get { return (bool)GetValue(EnableLevelProgressionsProperty); }
            set { SetValue(EnableLevelProgressionsProperty, value); }
        }

        public LevelList PlayerLevels
        {
            get { return (LevelList)GetValue(PlayerLevelsProperty); }
            set { SetValue(PlayerLevelsProperty, value); }
        }

        public LevelList DinoLevels
        {
            get { return (LevelList)GetValue(DinoLevelsProperty); }
            set { SetValue(DinoLevelsProperty, value); }
        }

        [XmlIgnore]
        public DinoSettingsList DinoSettings
        {
            get { return (DinoSettingsList)GetValue(DinoSettingsProperty); }
            set { SetValue(DinoSettingsProperty, value); }
        }

        public static readonly DependencyProperty DinoSettingsProperty = DependencyProperty.Register(nameof(DinoSettings), typeof(DinoSettingsList), typeof(ServerProfile), new PropertyMetadata(null));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float StructureDamageRepairCooldown
        {
            get { return (float)GetValue(StructureDamageRepairCooldownProperty); }
            set { SetValue(StructureDamageRepairCooldownProperty, value); }
        }

        public static readonly DependencyProperty StructureDamageRepairCooldownProperty = DependencyProperty.Register(nameof(StructureDamageRepairCooldown), typeof(float), typeof(ServerProfile), new PropertyMetadata(0.0f));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEAllowTribeWar")]
        public bool AllowTribeWarPvE
        {
            get { return (bool)GetValue(AllowTribeWarPvEProperty); }
            set { SetValue(AllowTribeWarPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeWarPvEProperty = DependencyProperty.Register(nameof(AllowTribeWarPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEAllowTribeWarCancel")]
        public bool AllowTribeWarCancelPvE
        {
            get { return (bool)GetValue(AllowTribeWarCancelPvEProperty); }
            set { SetValue(AllowTribeWarCancelPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeWarCancelPvEProperty = DependencyProperty.Register(nameof(AllowTribeWarCancelPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bAllowCustomRecipes")]
        public bool AllowCustomRecipes
        {
            get { return (bool)GetValue(AllowCustomRecipesProperty); }
            set { SetValue(AllowCustomRecipesProperty, value); }
        }

        public static readonly DependencyProperty AllowCustomRecipesProperty = DependencyProperty.Register(nameof(AllowCustomRecipes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));

        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPassiveDefensesDamageRiderlessDinos")]
        public bool PassiveDefensesDamageRiderlessDinos
        {
            get { return (bool)GetValue(PassiveDefensesDamageRiderlessDinosProperty); }
            set { SetValue(PassiveDefensesDamageRiderlessDinosProperty, value); }
        }

        public static readonly DependencyProperty PassiveDefensesDamageRiderlessDinosProperty = DependencyProperty.Register(nameof(PassiveDefensesDamageRiderlessDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool EnableAutoUpdate
        {
            get { return (bool)GetValue(EnableAutoUpdateProperty); }
            set { SetValue(EnableAutoUpdateProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoUpdateProperty = DependencyProperty.Register(nameof(EnableAutoUpdate), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public int AutoUpdatePeriod
        {
            get { return (int)GetValue(AutoUpdatePeriodProperty); }
            set { SetValue(AutoUpdatePeriodProperty, value); }
        }

        public static readonly DependencyProperty AutoUpdatePeriodProperty = DependencyProperty.Register(nameof(AutoUpdatePeriod), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));

        public bool EnableAutoStart
        {
            get { return (bool)GetValue(EnableAutoStartProperty); }
            set { SetValue(EnableAutoStartProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoStartProperty = DependencyProperty.Register(nameof(EnableAutoStart), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public int ServerUpdateGraceMinutes
        {
            get { return (int)GetValue(ServerUpdateGraceMinutesProperty); }
            set { SetValue(ServerUpdateGraceMinutesProperty, value); }
        }

        public static readonly DependencyProperty ServerUpdateGraceMinutesProperty = DependencyProperty.Register(nameof(ServerUpdateGraceMinutes), typeof(int), typeof(ServerProfile), new PropertyMetadata(15));

        public bool ServerForceUpdate
        {
            get { return (bool)GetValue(ServerForceUpdateProperty); }
            set { SetValue(ServerForceUpdateProperty, value); }
        }

        public static readonly DependencyProperty ServerForceUpdateProperty = DependencyProperty.Register(nameof(ServerForceUpdate), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public string ServerForceUpdateTime
        {
            get { return (string)GetValue(ServerForceUpdateTimeProperty); }
            set { SetValue(ServerForceUpdateTimeProperty, value); }
        }

        public static readonly DependencyProperty ServerForceUpdateTimeProperty = DependencyProperty.Register(nameof(ServerForceUpdateTime), typeof(string), typeof(ServerProfile), new PropertyMetadata("00:00"));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_Player
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_PlayerProperty); }
            set { SetValue(PerLevelStatsMultiplier_PlayerProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_PlayerProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_Player), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoTamed
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoTamedProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamedProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamedProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoTamed_Add
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoTamed_AddProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamed_AddProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamed_AddProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed_Add), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoTamed_Affinity
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoTamed_AffinityProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamed_AffinityProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamed_AffinityProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));

        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoWild
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoWildProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoWildProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoWildProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoWild), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));

        public static readonly DependencyProperty DisableAntiSpeedHackDetectionProperty = DependencyProperty.Register(nameof(DisableAntiSpeedHackDetection), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool DisableAntiSpeedHackDetection
        {
            get { return (bool)GetValue(DisableAntiSpeedHackDetectionProperty); }
            set { SetValue(DisableAntiSpeedHackDetectionProperty, value); }
        }

        public static readonly DependencyProperty SpeedHackBiasProperty = DependencyProperty.Register(nameof(SpeedHackBias), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        public float SpeedHackBias
        {
            get { return (float)GetValue(SpeedHackBiasProperty); }
            set { SetValue(SpeedHackBiasProperty, value); }
        }

        public static readonly DependencyProperty DisablePlayerMovePhysicsOptimizationProperty = DependencyProperty.Register(nameof(DisablePlayerMovePhysicsOptimization), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool DisablePlayerMovePhysicsOptimization
        {
            get { return (bool)GetValue(DisablePlayerMovePhysicsOptimizationProperty); }
            set { SetValue(DisablePlayerMovePhysicsOptimizationProperty, value); }
        }

        public static readonly DependencyProperty DisableValveAntiCheatSystemProperty = DependencyProperty.Register(nameof(DisableValveAntiCheatSystem), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool DisableValveAntiCheatSystem
        {
            get { return (bool)GetValue(DisableValveAntiCheatSystemProperty); }
            set { SetValue(DisableValveAntiCheatSystemProperty, value); }
        }

        public static readonly DependencyProperty ForceAllStructureLockingProperty = DependencyProperty.Register(nameof(ForceAllStructureLocking), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool ForceAllStructureLocking
        {
            get { return (bool)GetValue(ForceAllStructureLockingProperty); }
            set { SetValue(ForceAllStructureLockingProperty, value); }
        }

        public static readonly DependencyProperty AutoDestroyOldStructuresMultiplierProperty = DependencyProperty.Register(nameof(AutoDestroyOldStructuresMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(0.0f));

        public float AutoDestroyOldStructuresMultiplier
        {
            get { return (float)GetValue(AutoDestroyOldStructuresMultiplierProperty); }
            set { SetValue(AutoDestroyOldStructuresMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EnableServerAdminLogsProperty = DependencyProperty.Register(nameof(EnableServerAdminLogs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool EnableServerAdminLogs
        {
            get { return (bool)GetValue(EnableServerAdminLogsProperty); }
            set { SetValue(EnableServerAdminLogsProperty, value); }
        }

        public static readonly DependencyProperty RCONServerGameLogBufferProperty = DependencyProperty.Register(nameof(RCONServerGameLogBuffer), typeof(int), typeof(ServerProfile), new PropertyMetadata(600));

        public int RCONServerGameLogBuffer
        {
            get { return (int)GetValue(RCONServerGameLogBufferProperty); }
            set { SetValue(RCONServerGameLogBufferProperty, value); }
        }

        #endregion

        #region Survival of the Fittest Options

        public bool SOTF_Enabled
        {
            get { return (bool)GetValue(SOTF_EnabledProperty); }
            set { SetValue(SOTF_EnabledProperty, value); }
        }

        public static readonly DependencyProperty SOTF_EnabledProperty = DependencyProperty.Register(nameof(SOTF_Enabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool SOTF_DisableDeathSPectator
        {
            get { return (bool)GetValue(SOTF_DisableDeathSPectatorProperty); }
            set { SetValue(SOTF_DisableDeathSPectatorProperty, value); }
        }

        public static readonly DependencyProperty SOTF_DisableDeathSPectatorProperty = DependencyProperty.Register(nameof(SOTF_DisableDeathSPectator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool SOTF_OnlyAdminRejoinAsSpectator
        {
            get { return (bool)GetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty); }
            set { SetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty, value); }
        }

        public static readonly DependencyProperty SOTF_OnlyAdminRejoinAsSpectatorProperty = DependencyProperty.Register(nameof(SOTF_OnlyAdminRejoinAsSpectator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool SOTF_GamePlayLogging
        {
            get { return (bool)GetValue(SOTF_GamePlayLoggingProperty); }
            set { SetValue(SOTF_GamePlayLoggingProperty, value); }
        }

        public static readonly DependencyProperty SOTF_GamePlayLoggingProperty = DependencyProperty.Register(nameof(SOTF_GamePlayLogging), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "MaxNumberOfPlayersInTribe", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_MaxNumberOfPlayersInTribe
        {
            get { return (int)GetValue(SOTF_MaxNumberOfPlayersInTribeProperty); }
            set { SetValue(SOTF_MaxNumberOfPlayersInTribeProperty, value); }
        }

        public static readonly DependencyProperty SOTF_MaxNumberOfPlayersInTribeProperty = DependencyProperty.Register(nameof(SOTF_MaxNumberOfPlayersInTribe), typeof(int), typeof(ServerProfile), new PropertyMetadata(2));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleNumOfTribesToStartGame", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleNumOfTribesToStartGame
        {
            get { return (int)GetValue(SOTF_BattleNumOfTribesToStartGameProperty); }
            set { SetValue(SOTF_BattleNumOfTribesToStartGameProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleNumOfTribesToStartGameProperty = DependencyProperty.Register(nameof(SOTF_BattleNumOfTribesToStartGame), typeof(int), typeof(ServerProfile), new PropertyMetadata(15));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "TimeToCollapseROD", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_TimeToCollapseROD
        {
            get { return (int)GetValue(SOTF_TimeToCollapseRODProperty); }
            set { SetValue(SOTF_TimeToCollapseRODProperty, value); }
        }

        public static readonly DependencyProperty SOTF_TimeToCollapseRODProperty = DependencyProperty.Register(nameof(SOTF_TimeToCollapseROD), typeof(int), typeof(ServerProfile), new PropertyMetadata(9000));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleAutoStartGameInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleAutoStartGameInterval
        {
            get { return (int)GetValue(SOTF_BattleAutoStartGameIntervalProperty); }
            set { SetValue(SOTF_BattleAutoStartGameIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleAutoStartGameIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleAutoStartGameInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleAutoRestartGameInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleAutoRestartGameInterval
        {
            get { return (int)GetValue(SOTF_BattleAutoRestartGameIntervalProperty); }
            set { SetValue(SOTF_BattleAutoRestartGameIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleAutoRestartGameIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleAutoRestartGameInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(45));

        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleSuddenDeathInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleSuddenDeathInterval
        {
            get { return (int)GetValue(SOTF_BattleSuddenDeathIntervalProperty); }
            set { SetValue(SOTF_BattleSuddenDeathIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleSuddenDeathIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleSuddenDeathInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(300));

        public bool SOTF_NoEvents
        {
            get { return (bool)GetValue(SOTF_NoEventsProperty); }
            set { SetValue(SOTF_NoEventsProperty, value); }
        }

        public static readonly DependencyProperty SOTF_NoEventsProperty = DependencyProperty.Register(nameof(SOTF_NoEvents), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool SOTF_NoBosses
        {
            get { return (bool)GetValue(SOTF_NoBossesProperty); }
            set { SetValue(SOTF_NoBossesProperty, value); }
        }

        public static readonly DependencyProperty SOTF_NoBossesProperty = DependencyProperty.Register(nameof(SOTF_NoBosses), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public bool SOTF_BothBosses
        {
            get { return (bool)GetValue(SOTF_BothBossesProperty); }
            set { SetValue(SOTF_BothBossesProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BothBossesProperty = DependencyProperty.Register(nameof(SOTF_BothBosses), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));

        public float SOTF_EvoEventInterval
        {
            get { return (float)GetValue(SOTF_EvoEventIntervalProperty); }
            set { SetValue(SOTF_EvoEventIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_EvoEventIntervalProperty = DependencyProperty.Register(nameof(SOTF_EvoEventInterval), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));

        public float SOTF_RingStartTime
        {
            get { return (float)GetValue(SOTF_RingStartTimeProperty); }
            set { SetValue(SOTF_RingStartTimeProperty, value); }
        }

        public static readonly DependencyProperty SOTF_RingStartTimeProperty = DependencyProperty.Register(nameof(SOTF_RingStartTime), typeof(float), typeof(ServerProfile), new PropertyMetadata(1000.0f));

        #endregion

        #region RCON Settings

        public Rect RCONWindowExtents
        {
            get { return (Rect)GetValue(RCONWindowExtentsProperty); }
            set { SetValue(RCONWindowExtentsProperty, value); }
        }

        public static readonly DependencyProperty RCONWindowExtentsProperty = DependencyProperty.Register(nameof(RCONWindowExtents), typeof(Rect), typeof(ServerProfile), new PropertyMetadata(new Rect(0f, 0f, 0f, 0f)));

        #endregion

        [XmlIgnore()]
        public bool IsDirty
        {
            get { return (bool)GetValue(IsDirtyProperty); }
            set { SetValue(IsDirtyProperty, value); }
        }

        [XmlIgnore()]
        private string LastSaveLocation = String.Empty;

        private ServerProfile()
        {
            ServerPassword = SecurityUtils.GeneratePassword(16);
            AdminPassword = SecurityUtils.GeneratePassword(16);
            this.DinoSpawnWeightMultipliers = new AggregateIniValueList<DinoSpawn>(nameof(DinoSpawnWeightMultipliers), GameData.GetDinoSpawns);
            this.TamedDinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.TamedDinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.PreventDinoTameClassNames = new StringIniValueList(nameof(PreventDinoTameClassNames), () => new string[0] );
            this.NPCReplacements = new AggregateIniValueList<NPCReplacement>(nameof(NPCReplacements), GameData.GetNPCReplacements);
            this.HarvestResourceItemAmountClassMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(HarvestResourceItemAmountClassMultipliers), GameData.GetStandardResourceMultipliers);
            this.OverrideNamedEngramEntries = new AggregateIniValueList<EngramEntry>(nameof(OverrideNamedEngramEntries), GameData.GetStandardEngramOverrides);
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);
            this.DinoLevels = new LevelList();
            this.PlayerLevels = new LevelList();
            this.PerLevelStatsMultiplier_Player = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_Player), GameData.GetPerLevelStatsMultipliers_Default);
            this.PerLevelStatsMultiplier_DinoWild = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_Default);
            this.PerLevelStatsMultiplier_DinoTamed = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed);
            this.PerLevelStatsMultiplier_DinoTamed_Add = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamed_Add);
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamed_Affinity);

            GetDefaultDirectories();
        }

        public enum LevelProgression
        {
            Player,
            Dino
        };

        public void ResetLevelProgressionToDefault(LevelProgression levelProgression)
        {
            LevelList list = GetLevelList(levelProgression);

            list.Clear();
            list.AddRange(GameData.LevelProgression);
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

        public void ClearLevelProgression(LevelProgression levelProgression)
        {
            var list = GetLevelList(levelProgression);
            list.Clear();
            list.Add(new Level { LevelIndex = 0, XPRequired = 1, EngramPoints = 0 });
            list.UpdateTotals();
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

        public static ServerProfile LoadFrom(string path)
        {            
            ServerProfile settings = null;
            if (Path.GetExtension(path) == Config.Default.ProfileExtension)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ServerProfile));
                
                using (var reader = File.OpenRead(path))
                {
                    settings = (ServerProfile)serializer.Deserialize(reader);
                    settings.IsDirty = false;
                }

                var profileIniPath = Path.Combine(Path.ChangeExtension(path, null), Config.Default.ServerGameUserSettingsFile);
                var configIniPath = Path.Combine(settings.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameUserSettingsFile);
                if (File.Exists(configIniPath))                    
                {
                    settings = LoadFromINIFiles(configIniPath, settings);
                }
                else if (File.Exists(profileIniPath))
                {                    
                    settings = LoadFromINIFiles(profileIniPath, settings);
                }
            }
            else
            {
                settings = LoadFromINIFiles(path, settings);
                settings.InstallDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))));
            }

            //
            // TODO: Refactor this out
            //
            if (settings.PlayerLevels.Count == 0)
            {
                settings.ResetLevelProgressionToDefault(LevelProgression.Player);
                settings.ResetLevelProgressionToDefault(LevelProgression.Dino);
                settings.EnableLevelProgressions = false;
            }

            //
            // Since these are not inserted the normal way, we force a recomputation here.
            //
            settings.PlayerLevels.UpdateTotals();
            settings.DinoLevels.UpdateTotals();
            settings.DinoSettings.RenderToView();
            settings.LastSaveLocation = path;
            return settings;
        }

        private static ServerProfile LoadFromINIFiles(string path, ServerProfile settings)
        {         
            SystemIniFile iniFile = new SystemIniFile(Path.GetDirectoryName(path));
            settings = settings ?? new ServerProfile();
            iniFile.Deserialize(settings);

            var strings = iniFile.IniReadSection(IniFileSections.GameMode, IniFiles.Game);

            // 
            // Levels
            //
            var levelRampOverrides = strings.Where(s => s.StartsWith("LevelExperienceRampOverrides=")).ToArray();
            var engramPointOverrides = strings.Where(s => s.StartsWith("OverridePlayerLevelEngramPoints="));
            if (levelRampOverrides.Length > 0)
            {
                settings.EnableLevelProgressions = true;
                settings.PlayerLevels = LevelList.FromINIValues(levelRampOverrides[0], engramPointOverrides);

                if(levelRampOverrides.Length > 1)
                {
                    settings.DinoLevels = LevelList.FromINIValues(levelRampOverrides[1], null);
                }
            }
                      
            return settings;
        }

        public void Save()
        {
            this.DinoSettings.RenderToModel();

            //
            // Save the profile
            //
            XmlSerializer serializer = new XmlSerializer(this.GetType());
            using (var stream = File.Open(GetProfilePath(), FileMode.Create))
            {
                serializer.Serialize(stream, this);
            }                        

            //
            // Write the INI files
            //
            SaveINIFiles();

            //
            // If this was a rename, remove the old profile after writing the new one.
            //
            if(!String.Equals(GetProfilePath(), this.LastSaveLocation))
            {
                try
                {
                    if (File.Exists(this.LastSaveLocation))
                    {
                        File.Delete(this.LastSaveLocation);
                    }

                    var iniDir = Path.ChangeExtension(this.LastSaveLocation, null);
                    if (Directory.Exists(iniDir))
                    {
                        Directory.Delete(iniDir, recursive: true);
                    }
                }
                catch(IOException)
                {
                    // We tried...
                }

                this.LastSaveLocation = GetProfilePath();
            }

            SaveLauncher();
        }

        private void SaveLauncher()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetLauncherPath()));
            File.WriteAllText(GetLauncherPath(), $"start \"\" \"{GetServerExe()}\" {GetServerArgs()}");
        }

        public string GetProfilePath()
        {
            return Path.Combine(Config.Default.ConfigDirectory, Path.ChangeExtension(this.ProfileName, Config.Default.ProfileExtension));
        }

        public string GetProfileIniDir()
        {
            return Path.Combine(Path.GetDirectoryName(GetProfilePath()), this.ProfileName);
        }

        public string GetLauncherPath()
        {
            return Path.Combine(this.InstallDirectory, Config.Default.ServerConfigRelativePath, "RunServer.cmd");
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
            var iniFile = new SystemIniFile(profileIniDir);
            iniFile.Serialize(this);

            //
            // TODO: Refactor this into SystemIniFile
            //
            var values = iniFile.IniReadSection(IniFileSections.GameMode, IniFiles.Game);
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

            iniFile.IniWriteSection(IniFileSections.GameMode, filteredValues.ToArray(), IniFiles.Game);
        }

        public string GetServerExe()
        {
            return Path.Combine(this.InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerExe);
        }

        public string GetServerArgs()
        {
            var serverArgs = new StringBuilder();

            if (this.SOTF_Enabled)
            {
                serverArgs.Append(Config.Default.DefaultServerMap);

                serverArgs.Append("?EvoEventInterval=").Append(this.SOTF_EvoEventInterval);
                serverArgs.Append("?RingStartTime=").Append(this.SOTF_RingStartTime);
            }
            else
            {
#if false
                if (this.MapSource == MapSourceType.ByName)
                {
                    serverArgs.Append(this.ServerMap);
                }
                else
                {
                    serverArgs.Append($"-MapModID={this.ServerMapModId}");
                }
#else
                serverArgs.Append(this.ServerMap);
#endif
            }

            // This flag is broken in the INI        
            if(this.EnableFlyerCarry)
            {
                serverArgs.Append("?AllowFlyerCarryPVE=True");
            }
            // These are used to match the server to the profile.
            serverArgs.Append("?QueryPort=").Append(this.ServerPort);
            if (!String.IsNullOrEmpty(this.ServerIP))
            {
                serverArgs.Append("?MultiHome=").Append(this.ServerIP);
            }

            if(this.RCONEnabled)
            {
                serverArgs.Append("?RCONEnabled=true");
                serverArgs.Append("?RCONPort=").Append(this.RCONPort);
                if (this.EnableServerAdminLogs)
                {
                    serverArgs.Append("?RCONServerGameLogBuffer=").Append(this.RCONServerGameLogBuffer);
                }
            }

            if (this.UseRawSockets)
            {
                serverArgs.Append("?bRawSockets");
            }

            if (this.ForceAllStructureLocking)
            {
                serverArgs.Append("?ForceAllStructureLocking=true");
            }

            serverArgs.Append("?AutoDestroyOldStructuresMultiplier=").Append(this.AutoDestroyOldStructuresMultiplier);

            // Currently this setting does not seem to get picked up from the INI file.
            serverArgs.Append("?MaxPlayers=").Append(this.MaxPlayers);
            serverArgs.Append("?Port=").Append(this.ServerConnectionPort);

            serverArgs.Append("?listen");

            if (!this.SOTF_Enabled && !String.IsNullOrEmpty(this.ServerModIds))
            {
                serverArgs.Append($"?GameModIds={this.ServerModIds}");
            }

            if (!String.IsNullOrWhiteSpace(this.AdditionalArgs))
            {
                var addArgs = this.AdditionalArgs.TrimStart();
                if (!addArgs.StartsWith("?"))
                    serverArgs.Append(" ");
                serverArgs.Append(addArgs);
            }

            if(this.EnableAllowCaveFlyers)
            {
                serverArgs.Append(" -ForceAllowCaveFlyers");
            }

            if(this.SOTF_Enabled)
            {
                serverArgs.Append(" -TotalConversionMod=496735411");

                if (this.SOTF_GamePlayLogging)
                {
                    serverArgs.Append(" -gameplaylogging");
                }

                if(this.SOTF_DisableDeathSPectator)
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

            if (this.UseBattlEye)
            {
                serverArgs.Append(" -UseBattlEye");
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

            if (this.EnableServerAdminLogs)
            {
                serverArgs.Append(" -servergamelog");
            }

            serverArgs.Append(' ');
            serverArgs.Append(Config.Default.ServerCommandLineStandardArgs);

            return serverArgs.ToString();
        }

        public bool UpdateAutoUpdateSettings()
        {
            SaveLauncher();

            if (!ServerScheduler.SetDirectoryOwnershipForAllUsers(this.InstallDirectory))
            {
               _logger.Error($"Unable to set directory permissions for {this.InstallDirectory}.");
                return false;
            }


            var schedulerKey = GetSchedulerKey();
            if(!ServerScheduler.ScheduleAutoStart(schedulerKey, this.EnableAutoStart, GetLauncherPath(), String.Empty))
            {
                return false;
            }

            TimeSpan serverForceUpdateTime;
            if (!ServerScheduler.ScheduleUpdates(
                    schedulerKey,
                    this.EnableAutoUpdate ? this.AutoUpdatePeriod : 0,
                    Config.Default.ServerCacheDir,
                    this.InstallDirectory,
                    this.ServerIP,
                    this.RCONPort,
                    this.AdminPassword,
                    this.ServerUpdateGraceMinutes,
                    this.ServerForceUpdate ? (TimeSpan.TryParseExact(this.ServerForceUpdateTime, "g", null, out serverForceUpdateTime) ? serverForceUpdateTime : (TimeSpan?)null)
                                           : null
                ))
            {
                return false;
            }

            return true;
        }

        private string GetSchedulerKey()
        {
            using (var hashAlgo = MD5.Create())
            {
                var hashStr = Encoding.UTF8.GetBytes(this.InstallDirectory);
                var hash = hashAlgo.ComputeHash(hashStr);
                StringBuilder builder = new StringBuilder();
                foreach(var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                var outputStr = builder.ToString();
                
                return outputStr;
            }
        }

        private void GetDefaultDirectories()
        {
            if (String.IsNullOrWhiteSpace(InstallDirectory))
            {
                InstallDirectory = Path.IsPathRooted(Config.Default.ServersInstallDir) ? Path.Combine(Config.Default.ServersInstallDir)
                                                                                       : Path.Combine(Config.Default.DataDir, Config.Default.ServersInstallDir);
            }
        }

        internal static ServerProfile FromDefaults()
        {
            var settings = new ServerProfile();
            settings.DinoSpawnWeightMultipliers.Reset();
            settings.TamedDinoClassResistanceMultipliers.Reset();
            settings.TamedDinoClassDamageMultipliers.Reset();
            settings.DinoClassResistanceMultipliers.Reset();
            settings.DinoClassDamageMultipliers.Reset();
            settings.HarvestResourceItemAmountClassMultipliers.Reset();
            settings.ResetLevelProgressionToDefault(LevelProgression.Player);
            settings.ResetLevelProgressionToDefault(LevelProgression.Dino);
            settings.PerLevelStatsMultiplier_DinoTamed.Reset();
            settings.PerLevelStatsMultiplier_DinoTamed_Add.Reset();
            settings.PerLevelStatsMultiplier_DinoTamed_Affinity.Reset();
            settings.PerLevelStatsMultiplier_DinoWild.Reset();
            settings.PerLevelStatsMultiplier_Player.Reset();
            return settings;
        }

        #region Reset Methods
        // individual value reset methods
        public void ResetMapName()
        {
            this.ClearValue(ServerMapProperty);
        }

        public void ResetOverrideMaxExperiencePointsPlayer()
        {
            this.ClearValue(OverrideMaxExperiencePointsPlayerProperty);
        }

        public void ResetOverrideMaxExperiencePointsDino()
        {
            this.ClearValue(OverrideMaxExperiencePointsDinoProperty);
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

            this.ClearValue(EnableBanListURLProperty);
            this.ClearValue(BanListURLProperty);
            this.ClearValue(MaxPlayersProperty);
            this.ClearValue(EnableKickIdlePlayersProperty);
            this.ClearValue(KickIdlePlayersPeriodProperty);

            this.ClearValue(RCONEnabledProperty);
            this.ClearValue(RCONPortProperty);
            this.ClearValue(AdminLoggingProperty);
            this.ClearValue(EnableServerAdminLogsProperty);
            this.ClearValue(RCONServerGameLogBufferProperty);

            this.ClearValue(ServerMapProperty);
            this.ClearValue(ServerModIdsProperty);

            this.ClearValue(EnableExtinctionEventProperty);
            this.ClearValue(ExtinctionEventTimeIntervalProperty);
            this.ClearValue(ExtinctionEventUTCProperty);

            this.ClearValue(AutoSavePeriodMinutesProperty);

            this.ClearValue(MOTDProperty);
            this.ClearValue(MOTDDurationProperty);

            this.ClearValue(DisableValveAntiCheatSystemProperty);
            this.ClearValue(DisablePlayerMovePhysicsOptimizationProperty);
            this.ClearValue(DisableAntiSpeedHackDetectionProperty);
            this.ClearValue(SpeedHackBiasProperty);
            this.ClearValue(UseBattlEyeProperty);
            this.ClearValue(MaxTribeLogsProperty);

            this.ClearValue(AdditionalArgsProperty);
        }

        public void ResetChatAndNotificationSection()
        {
            this.ClearValue(EnableGlobalVoiceChatProperty);
            this.ClearValue(EnableProximityChatProperty);
            this.ClearValue(EnablePlayerLeaveNotificationsProperty);
            this.ClearValue(EnablePlayerJoinedNotificationsProperty);
        }

        public void ResetCustomLevelsSection()
        {
            this.ClearValue(EnableLevelProgressionsProperty);

            this.PlayerLevels = new LevelList();
            this.ResetLevelProgressionToOfficial(LevelProgression.Player);

            this.DinoLevels = new LevelList();
            this.ResetLevelProgressionToOfficial(LevelProgression.Dino);
        }

        public void ResetDinoSettings()
        {
            this.ClearValue(OverrideMaxExperiencePointsDinoProperty);
            this.ClearValue(DinoDamageMultiplierProperty);
            this.ClearValue(TamedDinoDamageMultiplierProperty);
            this.ClearValue(DinoResistanceMultiplierProperty);
            this.ClearValue(TamedDinoResistanceMultiplierProperty);
            this.ClearValue(MaxTamedDinosProperty);
            this.ClearValue(DinoCharacterFoodDrainMultiplierProperty);
            this.ClearValue(DinoCharacterStaminaDrainMultiplierProperty);
            this.ClearValue(DinoCharacterHealthRecoveryMultiplierProperty);
            this.ClearValue(DinoCountMultiplierProperty);
            this.ClearValue(HarvestingDamageMultiplierDinoProperty);
            this.ClearValue(TurretDamageMultiplierDinoProperty);
            this.ClearValue(EnableAllowCaveFlyersProperty);
            this.ClearValue(DisableDinoDecayPvEProperty);
            this.ClearValue(PvEDinoDecayPeriodMultiplierProperty);

            this.DinoSpawnWeightMultipliers = new AggregateIniValueList<DinoSpawn>(nameof(DinoSpawnWeightMultipliers), GameData.GetDinoSpawns);
            this.PreventDinoTameClassNames = new StringIniValueList(nameof(PreventDinoTameClassNames), () => new string[0]);
            this.NPCReplacements = new AggregateIniValueList<NPCReplacement>(nameof(NPCReplacements), GameData.GetNPCReplacements);
            this.TamedDinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.TamedDinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);

            this.PerLevelStatsMultiplier_DinoWild = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_Default);
            this.PerLevelStatsMultiplier_DinoTamed = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed);
            this.PerLevelStatsMultiplier_DinoTamed_Add = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamed_Add);
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamed_Affinity);

            this.ClearValue(MatingIntervalMultiplierProperty);
            this.ClearValue(EggHatchSpeedMultiplierProperty);
            this.ClearValue(BabyMatureSpeedMultiplierProperty);
            this.ClearValue(BabyFoodConsumptionSpeedMultiplierProperty);
        }

        public void ResetEngramsSection()
        {
            this.OverrideNamedEngramEntries = new AggregateIniValueList<EngramEntry>(nameof(OverrideNamedEngramEntries), GameData.GetStandardEngramOverrides);
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

            this.HarvestResourceItemAmountClassMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(HarvestResourceItemAmountClassMultipliers), GameData.GetStandardResourceMultipliers);
            this.HarvestResourceItemAmountClassMultipliers.Reset();

            this.ClearValue(DayCycleSpeedScaleProperty);
            this.ClearValue(DayTimeSpeedScaleProperty);
            this.ClearValue(NightTimeSpeedScaleProperty);
            this.ClearValue(GlobalSpoilingTimeMultiplierProperty);
            this.ClearValue(GlobalItemDecompositionTimeMultiplierProperty);
            this.ClearValue(GlobalCorpseDecompositionTimeMultiplierProperty);
            this.ClearValue(CropDecaySpeedMultiplierProperty);
            this.ClearValue(CropGrowthSpeedMultiplierProperty);
            this.ClearValue(LayEggIntervalMultiplierProperty);
            this.ClearValue(PoopIntervalMultiplierProperty);
        }

        public void ResetHUDAndVisualsSection()
        {
            this.ClearValue(AllowCrosshairProperty);
            this.ClearValue(AllowHUDProperty);
            this.ClearValue(AllowThirdPersonViewProperty);
            this.ClearValue(AllowMapPlayerLocationProperty);
            this.ClearValue(AllowPVPGammaProperty);
            this.ClearValue(AllowPvEGammaProperty);
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

            this.PerLevelStatsMultiplier_Player = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_Player), GameData.GetPerLevelStatsMultipliers_Default);
        }

        public void ResetRulesSection()
        {
            this.ClearValue(EnableHardcoreProperty);
            this.ClearValue(EnablePVPProperty);
            this.ClearValue(AllowCaveBuildingPvEProperty);
            this.ClearValue(DisableFriendlyFirePvPProperty);
            this.ClearValue(DisableFriendlyFirePvEProperty);
            this.ClearValue(DisableLootCratesProperty);
            this.ClearValue(DifficultyOffsetProperty);
            this.ClearValue(EnableTributeDownloadsProperty);
            this.ClearValue(PreventDownloadSurvivorsProperty);
            this.ClearValue(PreventDownloadItemsProperty);
            this.ClearValue(PreventDownloadDinosProperty);
            this.ClearValue(IncreasePvPRespawnIntervalProperty);
            this.ClearValue(IncreasePvPRespawnIntervalCheckPeriodProperty);
            this.ClearValue(IncreasePvPRespawnIntervalMultiplierProperty);
            this.ClearValue(IncreasePvPRespawnIntervalBaseAmountProperty);
            this.ClearValue(AutoPvETimerProperty);
            this.ClearValue(AutoPvEUseSystemTimeProperty);
            this.ClearValue(AutoPvEStartTimeSecondsProperty);
            this.ClearValue(AutoPvEStopTimeSecondsProperty);
            this.ClearValue(AllowTribeWarPvEProperty);
            this.ClearValue(AllowTribeWarCancelPvEProperty);
            this.ClearValue(AllowCustomRecipesProperty);
            this.ClearValue(CustomRecipeEffectivenessMultiplierProperty);
            this.ClearValue(CustomRecipeSkillMultiplierProperty);
        }

        public void ResetSOTFSection()
        {
            this.ClearValue(SOTF_EnabledProperty);
            this.ClearValue(SOTF_DisableDeathSPectatorProperty);
            this.ClearValue(SOTF_OnlyAdminRejoinAsSpectatorProperty);
            this.ClearValue(SOTF_MaxNumberOfPlayersInTribeProperty);
            this.ClearValue(SOTF_BattleNumOfTribesToStartGameProperty);
            this.ClearValue(SOTF_TimeToCollapseRODProperty);
            this.ClearValue(SOTF_BattleAutoStartGameIntervalProperty);
            this.ClearValue(SOTF_BattleAutoRestartGameIntervalProperty);
            this.ClearValue(SOTF_BattleSuddenDeathIntervalProperty);
            this.ClearValue(SOTF_GamePlayLoggingProperty);
        }

        public void ResetStructuresSection()
        {
            this.ClearValue(StructureResistanceMultiplierProperty);
            this.ClearValue(StructureDamageMultiplierProperty);
            this.ClearValue(StructureDamageRepairCooldownProperty);
            this.ClearValue(PvPStructureDecayProperty);
            this.ClearValue(PvPZoneStructureDamageMultiplierProperty);
            this.ClearValue(MaxStructuresVisibleProperty);
            this.ClearValue(PerPlatformMaxStructuresMultiplierProperty);
            this.ClearValue(MaxPlatformSaddleStructureLimitProperty);
            this.ClearValue(FlyerPlatformAllowUnalignedDinoBasingProperty);
            this.ClearValue(EnableStructureDecayProperty);
            this.ClearValue(PvEStructureDecayDestructionPeriodProperty);
            this.ClearValue(PvEStructureDecayPeriodMultiplierProperty);
            this.ClearValue(AutoDestroyOldStructuresMultiplierProperty);
            this.ClearValue(ForceAllStructureLockingProperty);
            this.ClearValue(PassiveDefensesDamageRiderlessDinosProperty);
        }
        #endregion
    }
}

Imports System
Imports System.IO
Imports System.IO.Compression
Imports System.IO.Compression.ZipArchive
Imports System.Deployment
Imports System.Deployment.Application
Imports System.Diagnostics
Imports System.Threading
Imports System.Windows.Forms
Imports System.Configuration
Imports System.ComponentModel
Imports System.Net

Public Class Main
    ' Vars
    Dim configINI As New IniFile
    Dim serversINI As New IniFile
    Dim configFileName As String = "GameUserSettings.ini"
    Dim serversFileName As String = ".\Servers.ini"
    Dim path As String
    Dim versionNum As String = My.Application.Info.Version.ToString
    Dim updateThread As New System.Threading.Thread(AddressOf ScheduledUpdate)
    Dim src As String = getSrc("https://www.dropbox.com/s/a6v1obnqigu2bpu/version.txt?dl=1")
    Dim updaterFile As String = "http://hellsguard.site11.com/ARK_Server_Tool/updater.zip"

    'Main
    Private Sub Main_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'Auto Update Check
        AutoUpdate()

        If Not My.Computer.FileSystem.FileExists(serversFileName) Then
            serversINI.Save(serversFileName)
        End If
        lblVersion.Text = "Version: " + versionNum

        If My.Settings.firstTimeRan = True Then
            If My.Settings.defaultSetup = True Then
                txtMultiServerName.Enabled = False
                lbServerSelect.Enabled = False
                btnSaveMultiServer.Enabled = False
                Label11.Enabled = False
                Label10.Enabled = False
            End If

            'Load Config
            LoadConfig()

            'Set Version & Server
            lblVersion.Text = "Version: " + versionNum
            lblCurrentServer.Text = "Current Server: " + My.Settings.currentServer

            'Scheduled Update - Disabled for Current Version
            'updateThread.Start()

            MultiServerPopulate()
        Else
            FirstSetup.ShowDialog()
        End If
    End Sub

    'Launch Server Control
    Private Sub btnLaunchServer_Click(sender As Object, e As EventArgs) Handles btnLaunchServer.Click
        Dim parameters As String

        If Not My.Computer.FileSystem.FileExists(My.Settings.runningServerDirectory + "ShooterGameServer.exe") Then
            MsgBox("Server file not found, run an update.", , "Error")
        Else
            If chkMultiHome.Checked = True Then
                parameters = "TheIsland?QueryPort=" + txtServerPort.Text + "?MaxPlayers=" + txtMaxPlayers.Text + "?MultiHome=" + txtMultiHome.Text + "?listen -nosteamclient -game -server -log"
            Else
                parameters = "TheIsland?QueryPort=" + txtServerPort.Text + "?MaxPlayers=" + txtMaxPlayers.Text + "?listen -nosteamclient -game -server -log"
            End If

            If My.Settings.defaultSetup = True Then
                Process.Start(My.Settings.runningServerDirectory + "ShooterGameServer.exe", parameters)
                MsgBox("Server Started", , "Notice")
                SaveConfig()
            Else
                Process.Start(My.Settings.runningServerDirectory + "ShooterGameServer.exe", parameters)
                MsgBox("Server Started", , "Notice")
                SaveConfig()
            End If
        End If

    End Sub

    'Stop Server Control
    Private Sub btnStopServer_Click(sender As Object, e As EventArgs) Handles btnStopServer.Click
        MsgBox("Be sure to use the saveworld command in your in-game console before proceeding.", , "Warning")
        Dim p() As Process
        p = Process.GetProcessesByName("ShooterGameServer")

        If p.Count > 0 Then
            Process.GetProcessesByName("ShooterGameServer")(0).Kill()
        Else
            MsgBox("Running server not found.", , "Error")
        End If

        'For Each p As Process In Process.GetProcesses
        '    Dim file As String
        '    Try
        '        file = p.Modules(0).FileName
        '    Catch ex As Win32Exception
        '        file = "n/a"
        '    End Try
        '
        '    If p.ProcessName = "ShooterGameServer" Then
        '        MsgBox(String.Format("Process {0}: {1}", p.ProcessName, file))
        '    End If
        'Next
    End Sub

    'Update Server Control
    Private Sub btnUpdateServer_Click(sender As Object, e As EventArgs) Handles btnUpdateServer.Click
        MsgBox("Be sure your server is stopped before pressing OK.", , "Warning")

        Dim parameters As String

        If My.Settings.defaultSetup = True Then
            parameters = "+login anonymous +force_install_dir ..\ARK_Server +app_update 376030 validate +quit"
            Process.Start("SteamCMD\steamcmd.exe", parameters)
        Else
            parameters = "+login anonymous +force_install_dir " + Chr(34) + My.Settings.advancedInstallDirectory + Chr(34) + " +app_update 376030 validate +quit"
            Process.Start(".\SteamCMD\steamcmd.exe", parameters)
        End If

    End Sub

    'Install SteamCMD Control
    Private Sub btnInstallSteamCMD_Click(sender As Object, e As EventArgs) Handles btnInstallSteamCMD.Click
        Dim zipPath As String = "steamCMD.zip"
        Dim extractPath As String = ".\SteamCMD"
        Dim parameters As String

        Do Until My.Computer.FileSystem.FileExists(".\SteamCMD\SteamCMD.exe")
            'Download SteamCMD if doesn't exist
            If Not My.Computer.FileSystem.FileExists("SteamCMD.zip") Then
                My.Computer.Network.DownloadFile("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip", "steamCMD.zip", True, 500)
            End If

            'Extract SteamCMD
            If My.Computer.FileSystem.FileExists("SteamCMD.zip") Then
                ZipFile.ExtractToDirectory(zipPath, extractPath)
            End If
        Loop

        'Clean up
        If My.Computer.FileSystem.FileExists("SteamCMD.zip") And My.Computer.FileSystem.FileExists(".\SteamCMD\SteamCMD.exe") Then
            My.Computer.FileSystem.DeleteFile("SteamCMD.zip")
        End If

        parameters = "+login anonymous +quit"

        Process.Start("SteamCMD\steamcmd.exe", parameters)
        MsgBox("SteamCMD is Installing, it will close itself and you can move on.", , "SteamCMD")
    End Sub

    'Save Config
    Private Sub btnSaveConfig_Click(sender As Object, e As EventArgs) Handles btnSaveConfig.Click
        My.Settings.Reload()

        SaveConfig()
        MsgBox("Config has been saved.", , "Config")
    End Sub

    'Setup Directories
    Public Sub ConfigDir(path As String)
        Try
            ' Determine whether the directory exists. 
            If Directory.Exists(path) Then
                Console.WriteLine("That path exists already.")
            End If

            ' Try to create the directory. 
            Dim di As DirectoryInfo = Directory.CreateDirectory(path)
            Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(path))

        Catch e As Exception
            Console.WriteLine("The process failed: {0}.", e.ToString())
        End Try
    End Sub

    'Scheduled Update Check
    Private Sub ScheduledUpdate()
        Dim updateTime As DateTime
        updateTime = txtTime.Text + cmbTimeAMPM.Text
        Dim parameters As String
        parameters = "+login anonymous +force_install_dir ..\ARK_Server +app_update 376030 validate +quit"

        Do While chkScheduledUpdate.Checked = True
            If TimeOfDay = updateTime Then
                'DEBUGGING / TESTING ONLY
                MsgBox(updateTime + " " + TimeOfDay, , "DEBUG")

                'Disabled until fully functional
                'Process.Start("SteamCMD\steamcmd.exe", parameters)
            End If
        Loop
    End Sub

    'Default Setup
    Public Sub DefaultSetup()
        lblVersion.Text = "Version: " + versionNum

        My.Settings.currentServer = "Default"
        My.Settings.userSetDirectory = Directory.GetCurrentDirectory & "\ark_server\ShooterGame\Saved\Config\WindowsServer\"
        My.Settings.runningServerDirectory = Directory.GetCurrentDirectory & "\ark_server\ShooterGame\Binaries\Win64\"
        My.Settings.Save()
        My.Settings.Reload()

        lblCurrentServer.Text = "Current Server: " + My.Settings.currentServer
        txtMultiServerName.Enabled = False
        lbServerSelect.Enabled = False
        btnSaveMultiServer.Enabled = False
        Label11.Enabled = False
        Label10.Enabled = False

        path = My.Settings.userSetDirectory
        ConfigDir(path)
        SaveConfig()
        serversINI.Save(serversFileName)
    End Sub

    'Advanced Setup
    Public Sub AdvancedSetup()
        lblVersion.Text = "Version: " + versionNum
        MsgBox("Select your current Server Folder, or Create a new one.", , "Initial Setup")
        If setDirectoryDialog.ShowDialog() = DialogResult.OK Then
            My.Settings.userSetDirectoryBool = True
            My.Settings.userSetDirectory = setDirectoryDialog.SelectedPath & "\ShooterGame\Saved\Config\WindowsServer\"
            My.Settings.runningServerDirectory = setDirectoryDialog.SelectedPath & "\ShooterGame\Binaries\Win64\"
            My.Settings.advancedInstallDirectory = setDirectoryDialog.SelectedPath
            My.Settings.currentServer = "Initial Server"
            My.Settings.Save()
            My.Settings.Reload()

            lblCurrentServer.Text = "Current Server: " + My.Settings.currentServer
            path = My.Settings.userSetDirectory
            ConfigDir(path)

            'Config Check
            If Not My.Computer.FileSystem.FileExists(My.Settings.userSetDirectory + configFileName) Then
                SaveConfig()
            Else
                MsgBox("Server Config detected, config will be set according to those options.", , "Multi Server")
            End If

            serversINI.AddSection("Initial Server").AddKey("Name").Value = "Initial Server"
            serversINI.AddSection("Initial Server").AddKey("Directory").Value = path
            serversINI.AddSection("Initial Server").AddKey("InstallDIR").Value = setDirectoryDialog.SelectedPath
            serversINI.AddSection("MultiHome").AddKey("MultiHome").Value = False

            serversINI.Save(serversFileName)
            LoadConfig()
            MultiServerPopulate()
        ElseIf DialogResult.Cancel Then
            Me.Close()
        End If
    End Sub

    'Save Config Function
    Public Sub SaveConfig()
        'Config Check
        If My.Computer.FileSystem.FileExists(My.Settings.userSetDirectory + configFileName) Then
            configINI.Load(My.Settings.userSetDirectory + configFileName)
        End If

        'Servers Check
        If My.Computer.FileSystem.FileExists(serversFileName) Then
            serversINI.Load(serversFileName)
        End If

        'Write all Boolean Type Values
        configINI.AddSection("ServerSettings").AddKey("ShowMapPlayerLocation").Value = chkShowPlayerMarker.Checked
        configINI.AddSection("ServerSettings").AddKey("ServerHardcore").Value = chkServerHardcore.Checked
        configINI.AddSection("ServerSettings").AddKey("GlobalVoiceChat").Value = chkGlobalVoiceChat.Checked
        configINI.AddSection("ServerSettings").AddKey("ProximityChat").Value = chkProximityChat.Checked
        configINI.AddSection("ServerSettings").AddKey("NoTributeDownloads").Value = chkAllowTributeDownloads.Checked
        configINI.AddSection("ServerSettings").AddKey("AllowThirdPersonPlayer").Value = chkAllowThirdPersonPlayer.Checked
        configINI.AddSection("ServerSettings").AddKey("AlwaysNotifyPlayerLeft").Value = chkAlwaysNotifyPlayerJoined.Checked
        configINI.AddSection("ServerSettings").AddKey("DontAlwaysNotifyPlayerJoined").Value = chkDontAlwaysNotifyPlayerJoined.Checked
        configINI.AddSection("ServerSettings").AddKey("ServerPVE").Value = chkServerPVE.Checked
        configINI.AddSection("ServerSettings").AddKey("ServerCrosshair").Value = chkServerCrosshair.Checked
        configINI.AddSection("ServerSettings").AddKey("ServerForceNoHUD").Value = chkServerForceNoHUD.Checked
        configINI.AddSection("ServerSettings").AddKey("bDisableStructureDecayPvE").Value = chkDisableStructureDecayPvE.Checked
        configINI.AddSection("ServerSettings").AddKey("bAllowFlyerCarryPvE").Value = chkAllowFlyerCarryPvE.Checked

        'Write all String Type Values
        configINI.AddSection("MessageOfTheDay").AddKey("Message").Value = txtMessageOfTheDay.Text
        configINI.AddSection("ServerSettings").AddKey("ServerPassword").Value = txtServerPassword.Text
        configINI.AddSection("ServerSettings").AddKey("ServerAdminPassword").Value = txtAdminPassword.Text
        configINI.AddSection("SessionSettings").AddKey("SessionName").Value = txtServerName.Text
        configINI.AddSection("SessionSettings").AddKey("QueryPort").Value = txtServerPort.Text
        configINI.AddSection("ServerSettings").AddKey("MaxStructuresInRange").Value = txtMaxStructuresInRange.Text

        'Write all Float Type Values
        configINI.AddSection("ServerSettings").AddKey("DifficultyOffset").Value = numDifficulty.Value

        configINI.AddSection("ServerSettings").AddKey("DayCycleSpeedScale").Value = numDayCycle.Value
        configINI.AddSection("ServerSettings").AddKey("NightTimeSpeedScale").Value = numNightTime.Value
        configINI.AddSection("ServerSettings").AddKey("DayTimeSpeedScale").Value = numDayTime.Value

        configINI.AddSection("ServerSettings").AddKey("PlayerDamageMultiplier").Value = numPlayerDamage.Value
        configINI.AddSection("ServerSettings").AddKey("DinoDamageMultiplier").Value = numDinoDamage.Value
        configINI.AddSection("ServerSettings").AddKey("StructureDamageMultiplier").Value = numStructureDamage.Value

        configINI.AddSection("ServerSettings").AddKey("PlayerResistanceMultiplier").Value = numPlayerResist.Value
        configINI.AddSection("ServerSettings").AddKey("DinoResistanceMultiplier").Value = numDinoResist.Value
        configINI.AddSection("ServerSettings").AddKey("StructureResistanceMultiplier").Value = numStructureResist.Value

        configINI.AddSection("ServerSettings").AddKey("XPMultiplier").Value = numExperience.Value
        configINI.AddSection("ServerSettings").AddKey("TamingSpeedMultiplier").Value = numTamingSpeed.Value
        configINI.AddSection("ServerSettings").AddKey("HarvestAmountMultiplier").Value = numHarvestAmount.Value

        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterWaterDrainMultiplier").Value = numPlayerWater.Value
        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterFoodDrainMultiplier").Value = numPlayerFood.Value
        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterStaminaDrainMultiplier").Value = numPlayerStamina.Value
        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterHealthRecoveryMultiplier").Value = numPlayerHealth.Value

        configINI.AddSection("ServerSettings").AddKey("DinoCharacterWaterDrainMultiplier").Value = numDinoWater.Value
        configINI.AddSection("ServerSettings").AddKey("DinoCharacterFoodDrainMultiplier").Value = numDinoFood.Value
        configINI.AddSection("ServerSettings").AddKey("DinoCharacterStaminaDrainMultiplier").Value = numDinoStamina.Value
        configINI.AddSection("ServerSettings").AddKey("DinoCharacterHealthRecoveryMultiplier").Value = numDinoHealth.Value

        If chkMultiHome.Checked = True Then
            configINI.AddSection("SessionSettings").AddKey("MultiHome").Value = txtMultiHome.Text
        End If

        serversINI.AddSection("MultiHome").AddKey("MultiHome").Value = chkMultiHome.Checked

        'Write all Integer Type Values
        configINI.AddSection("/Script/Engine.GameSession").AddKey("MaxPlayers").Value = txtMaxPlayers.Text

        configINI.Save(My.Settings.userSetDirectory + configFileName)
        serversINI.Save(serversFileName)
    End Sub

    'Load Config Function
    Private Sub LoadConfig()
        'Config Check
        If My.Computer.FileSystem.FileExists(My.Settings.userSetDirectory + configFileName) Then
            configINI.Load(My.Settings.userSetDirectory + configFileName)
        End If

        'Servers Check
        If My.Computer.FileSystem.FileExists(serversFileName) Then
            serversINI.Load(serversFileName)
        End If

        'New Parameter Checks
        If configINI.GetKeyValue("ServerSettings", "bDisableStructureDecayPvE") = "" Then
            configINI.AddSection("ServerSettings").AddKey("bDisableStructureDecayPvE").Value = False
            configINI.Save(My.Settings.userSetDirectory + configFileName)
        Else
            chkDisableStructureDecayPvE.Checked = configINI.GetSection("ServerSettings").GetKey("bDisableStructureDecayPvE").GetValue()
        End If

        If configINI.GetKeyValue("ServerSettings", "bAllowFlyerCarryPvE") = "" Then
            configINI.AddSection("ServerSettings").AddKey("bAllowFlyerCarryPvE").Value = False
            configINI.Save(My.Settings.userSetDirectory + configFileName)
        Else
            chkAllowFlyerCarryPvE.Checked = configINI.GetSection("ServerSettings").GetKey("bAllowFlyerCarryPvE").GetValue()
        End If

        If configINI.GetKeyValue("ServerSettings", "MaxStructuresInRange") = "" Then
            configINI.AddSection("ServerSettings").AddKey("MaxStructuresInRange").Value = "1300"
            configINI.Save(My.Settings.userSetDirectory + configFileName)
        Else
            txtMaxStructuresInRange.Text = configINI.GetSection("ServerSettings").GetKey("MaxStructuresInRange").GetValue()
        End If
        'End New Parameter Checks

        'Get & Set all Boolean Types
        chkShowPlayerMarker.Checked = configINI.GetSection("ServerSettings").GetKey("ShowMapPlayerLocation").GetValue()
        chkServerHardcore.Checked = configINI.GetSection("ServerSettings").GetKey("ServerHardcore").GetValue()
        chkGlobalVoiceChat.Checked = configINI.GetSection("ServerSettings").GetKey("GlobalVoiceChat").GetValue()
        chkProximityChat.Checked = configINI.GetSection("ServerSettings").GetKey("ProximityChat").GetValue()
        chkAllowTributeDownloads.Checked = configINI.GetSection("ServerSettings").GetKey("NoTributeDownloads").GetValue()
        chkAllowThirdPersonPlayer.Checked = configINI.GetSection("ServerSettings").GetKey("AllowThirdPersonPlayer").GetValue()
        chkAlwaysNotifyPlayerJoined.Checked = configINI.GetSection("ServerSettings").GetKey("AlwaysNotifyPlayerLeft").GetValue()
        chkDontAlwaysNotifyPlayerJoined.Checked = configINI.GetSection("ServerSettings").GetKey("DontAlwaysNotifyPlayerJoined").GetValue()
        chkServerPVE.Checked = configINI.GetSection("ServerSettings").GetKey("ServerPVE").GetValue()
        chkServerCrosshair.Checked = configINI.GetSection("ServerSettings").GetKey("ServerCrosshair").GetValue()
        chkServerForceNoHUD.Checked = configINI.GetSection("ServerSettings").GetKey("ServerForceNoHUD").GetValue()
        'chkDisableStructureDecayPvE.Checked = configINI.GetSection("ServerSettings").GetKey("bDisableStructureDecayPvE").GetValue()
        'chkAllowFlyerCarryPvE.Checked = configINI.GetSection("ServerSettings").GetKey("bAllowFlyerCarryPvE").GetValue()

        'Get & Set all String Types
        txtMessageOfTheDay.Text = configINI.GetSection("MessageOfTheDay").GetKey("Message").GetValue()
        txtServerPassword.Text = configINI.GetSection("ServerSettings").GetKey("ServerPassword").GetValue()
        txtAdminPassword.Text = configINI.GetSection("ServerSettings").GetKey("ServerAdminPassword").GetValue()
        txtServerName.Text = configINI.GetSection("SessionSettings").GetKey("SessionName").GetValue()
        txtServerPort.Text = configINI.GetSection("SessionSettings").GetKey("QueryPort").GetValue()
        'txtMaxStructuresInRange.Text = configINI.GetSection("ServerSettings").GetKey("MaxStructuresInRange").GetValue()

        'Get & Set all Float Type Values
        numDifficulty.Value = configINI.GetSection("ServerSettings").GetKey("DifficultyOffset").GetValue()

        numDayCycle.Value = configINI.GetSection("ServerSettings").GetKey("DayCycleSpeedScale").GetValue()
        numNightTime.Value = configINI.GetSection("ServerSettings").GetKey("NightTimeSpeedScale").GetValue()
        numDayTime.Value = configINI.GetSection("ServerSettings").GetKey("DayTimeSpeedScale").GetValue()

        numPlayerDamage.Value = configINI.GetSection("ServerSettings").GetKey("PlayerDamageMultiplier").GetValue()
        numDinoDamage.Value = configINI.GetSection("ServerSettings").GetKey("DinoDamageMultiplier").GetValue()
        numStructureDamage.Value = configINI.GetSection("ServerSettings").GetKey("StructureDamageMultiplier").GetValue()

        numPlayerResist.Value = configINI.GetSection("ServerSettings").GetKey("PlayerResistanceMultiplier").GetValue()
        numDinoResist.Value = configINI.GetSection("ServerSettings").GetKey("DinoResistanceMultiplier").GetValue()
        numStructureResist.Value = configINI.GetSection("ServerSettings").GetKey("StructureResistanceMultiplier").GetValue()

        numExperience.Value = configINI.GetSection("ServerSettings").GetKey("XPMultiplier").GetValue()
        numTamingSpeed.Value = configINI.GetSection("ServerSettings").GetKey("TamingSpeedMultiplier").GetValue()
        numHarvestAmount.Value = configINI.GetSection("ServerSettings").GetKey("HarvestAmountMultiplier").GetValue()

        numPlayerWater.Value = configINI.GetSection("ServerSettings").GetKey("PlayerCharacterWaterDrainMultiplier").GetValue()
        numPlayerFood.Value = configINI.GetSection("ServerSettings").GetKey("PlayerCharacterFoodDrainMultiplier").GetValue()
        numPlayerStamina.Value = configINI.GetSection("ServerSettings").GetKey("PlayerCharacterStaminaDrainMultiplier").GetValue()
        numPlayerHealth.Value = configINI.GetSection("ServerSettings").GetKey("PlayerCharacterHealthRecoveryMultiplier").GetValue()

        numDinoWater.Value = configINI.GetSection("ServerSettings").GetKey("DinoCharacterWaterDrainMultiplier").GetValue()
        numDinoFood.Value = configINI.GetSection("ServerSettings").GetKey("DinoCharacterFoodDrainMultiplier").GetValue()
        numDinoStamina.Value = configINI.GetSection("ServerSettings").GetKey("DinoCharacterStaminaDrainMultiplier").GetValue()
        numDinoHealth.Value = configINI.GetSection("ServerSettings").GetKey("DinoCharacterHealthRecoveryMultiplier").GetValue()

        chkMultiHome.Checked = serversINI.GetSection("MultiHome").GetKey("MultiHome").GetValue()
        If chkMultiHome.Checked = True Then
            txtMultiHome.Text = configINI.GetSection("SessionSettings").GetKey("MultiHome").GetValue()
        End If

        'Get & Set all Integer Types
        txtMaxPlayers.Text = configINI.GetSection("/Script/Engine.GameSession").GetKey("MaxPlayers").GetValue()
    End Sub

    'Empty Config Function
    Public Sub EmptyConfig()
        'Config Check
        If My.Computer.FileSystem.FileExists(My.Settings.userSetDirectory + configFileName) Then
            configINI.Load(My.Settings.userSetDirectory + configFileName)
        End If

        'Servers Check
        If My.Computer.FileSystem.FileExists(serversFileName) Then
            serversINI.Load(serversFileName)
        End If

        'Write all Boolean Type Values
        configINI.AddSection("ServerSettings").AddKey("ShowMapPlayerLocation").Value = False
        configINI.AddSection("ServerSettings").AddKey("ServerHardcore").Value = False
        configINI.AddSection("ServerSettings").AddKey("GlobalVoiceChat").Value = False
        configINI.AddSection("ServerSettings").AddKey("ProximityChat").Value = False
        configINI.AddSection("ServerSettings").AddKey("NoTributeDownloads").Value = False
        configINI.AddSection("ServerSettings").AddKey("AllowThirdPersonPlayer").Value = False
        configINI.AddSection("ServerSettings").AddKey("AlwaysNotifyPlayerLeft").Value = False
        configINI.AddSection("ServerSettings").AddKey("DontAlwaysNotifyPlayerJoined").Value = False
        configINI.AddSection("ServerSettings").AddKey("ServerPVE").Value = False
        configINI.AddSection("ServerSettings").AddKey("ServerCrosshair").Value = False
        configINI.AddSection("ServerSettings").AddKey("ServerForceNoHUD").Value = False
        configINI.AddSection("ServerSettings").AddKey("bDisableStructureDecayPvE").Value = False
        configINI.AddSection("ServerSettings").AddKey("bAllowFlyerCarryPvE").Value = False

        'Write all String Type Values
        configINI.AddSection("MessageOfTheDay").AddKey("Message").Value = ""
        configINI.AddSection("ServerSettings").AddKey("ServerPassword").Value = ""
        configINI.AddSection("ServerSettings").AddKey("ServerAdminPassword").Value = ""
        configINI.AddSection("SessionSettings").AddKey("SessionName").Value = ""
        configINI.AddSection("SessionSettings").AddKey("QueryPort").Value = "27015"
        configINI.AddSection("ServerSettings").AddKey("DifficultyOffset").Value = "1.0"
        configINI.AddSection("ServerSettings").AddKey("MaxStructuresInRange").Value = "1300"

        'Write all Float Type Values
        configINI.AddSection("ServerSettings").AddKey("DifficultyOffset").Value = 1.0

        configINI.AddSection("ServerSettings").AddKey("DayCycleSpeedScale").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("NightTimeSpeedScale").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("DayTimeSpeedScale").Value = 1.0

        configINI.AddSection("ServerSettings").AddKey("PlayerDamageMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("DinoDamageMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("StructureDamageMultiplier").Value = 1.0

        configINI.AddSection("ServerSettings").AddKey("PlayerResistanceMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("DinoResistanceMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("StructureResistanceMultiplier").Value = 1.0

        configINI.AddSection("ServerSettings").AddKey("XPMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("TamingSpeedMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("HarvestAmountMultiplier").Value = 1.0

        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterWaterDrainMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterFoodDrainMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterStaminaDrainMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("PlayerCharacterHealthRecoveryMultiplier").Value = 1.0

        configINI.AddSection("ServerSettings").AddKey("DinoCharacterWaterDrainMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("DinoCharacterFoodDrainMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("DinoCharacterStaminaDrainMultiplier").Value = 1.0
        configINI.AddSection("ServerSettings").AddKey("DinoCharacterHealthRecoveryMultiplier").Value = 1.0

        If chkMultiHome.Checked = True Then
            configINI.AddSection("SessionSettings").AddKey("MultiHome").Value = "127.0.0.1"
        End If

        serversINI.AddSection("MultiHome").AddKey("MultiHome").Value = False

        'Write all Integer Type Values
        configINI.AddSection("/Script/Engine.GameSession").AddKey("MaxPlayers").Value = "70"

        configINI.Save(My.Settings.userSetDirectory + configFileName)
        serversINI.Save(serversFileName)
    End Sub

    'Multi Server Selection
    Private Sub lbServerSelect_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbServerSelect.SelectedIndexChanged
        serversINI.Load(serversFileName)
        Dim curSelection As String = lbServerSelect.SelectedItem.ToString()
        Dim serverName As String
        Dim serverDirectory As String
        Dim serverInstallDIR As String

        serverName = serversINI.GetSection(curSelection).GetKey("Name").GetValue()
        serverDirectory = serversINI.GetSection(curSelection).GetKey("Directory").GetValue()
        serverInstallDIR = serversINI.GetSection(curSelection).GetKey("InstallDIR").GetValue()

        Dim response = MsgBox("Are you sure you want to switch control to the server named" & vbCrLf & vbCrLf & serverName, MsgBoxStyle.YesNo, "Multi Server")

        If response = MsgBoxResult.Yes Then
            My.Settings.userSetDirectory = serverDirectory
            My.Settings.runningServerDirectory = serverDirectory
            My.Settings.advancedInstallDirectory = serverInstallDIR
            My.Settings.currentServer = serverName
            My.Settings.Save()
            My.Settings.Reload()
            LoadConfig()
            lblCurrentServer.Text = "Current Server: " + My.Settings.currentServer
            MsgBox("Changed Control to " + serverName, , "Multi Server")
        ElseIf response = MsgBoxResult.No Then
            'Nothing
        End If
    End Sub

    'Multi Server Save
    Private Sub btnSaveMultiServer_Click(sender As Object, e As EventArgs) Handles btnSaveMultiServer.Click
        If txtMultiServerName.Text = Nothing Or txtMultiServerName.Text = "" Then
            MsgBox("Server name cannot be empty.", , "Multi Server")
        Else
            Dim response = MsgBox("Are you sure you want to add the server named" & vbCrLf & vbCrLf & txtMultiServerName.Text, MsgBoxStyle.YesNo, "Multi Server")
            If response = MsgBoxResult.Yes Then
                If setDirectoryDialog.ShowDialog() = DialogResult.OK Then
                    My.Settings.userSetDirectoryBool = True
                    My.Settings.userSetDirectory = setDirectoryDialog.SelectedPath & "\ShooterGame\Saved\Config\WindowsServer\"
                    My.Settings.advancedInstallDirectory = setDirectoryDialog.SelectedPath
                    My.Settings.currentServer = txtMultiServerName.Text
                    My.Settings.Save()
                    My.Settings.Reload()

                    path = My.Settings.userSetDirectory
                    ConfigDir(path)

                    'Config Check
                    If Not My.Computer.FileSystem.FileExists(My.Settings.userSetDirectory + configFileName) Then
                        EmptyConfig()
                    Else
                        MsgBox("Server Config detected, config will be set according to those options.", , "Multi Server")
                    End If

                    serversINI.AddSection(txtMultiServerName.Text).AddKey("Name").Value = txtMultiServerName.Text
                    serversINI.AddSection(txtMultiServerName.Text).AddKey("Directory").Value = path
                    serversINI.AddSection(txtMultiServerName.Text).AddKey("InstallDIR").Value = setDirectoryDialog.SelectedPath
                    serversINI.Save(serversFileName)
                    lbServerSelect.Items.Add(txtMultiServerName.Text)

                    txtMultiServerName.Text = ""
                    MsgBox("Server added successfully, control has been switched to your new server.", , "Multi Server")
                    LoadConfig()
                    lblCurrentServer.Text = "Current Server: " + My.Settings.currentServer
                ElseIf DialogResult.Cancel Then
                    MsgBox("You must select a directory.", , "Error")
                End If
            ElseIf response = MsgBoxResult.No Then
                'Nothin
            End If
        End If
    End Sub

    'Multi Server LB Fill
    Private Sub MultiServerPopulate()
        Dim ini As New IniFile
        ini.Load(serversFileName)

        For Each s As IniFile.IniSection In ini.Sections
            If Not s.Name = "MultiHome" Then
                lbServerSelect.Items.Add(s.Name)
            End If
        Next
    End Sub

    'Auto-Update Function
    Public Sub AutoUpdate()
        Dim zipPath As String = "updater.zip"
        Dim extractPath As String = ".\"

        If My.Computer.FileSystem.FileExists("Server Tool Updater.exe") Then
            My.Computer.FileSystem.DeleteFile("Server Tool Updater.exe")
        End If

        If My.Computer.FileSystem.FileExists("updater.zip") Then
            My.Computer.FileSystem.DeleteFile("updater.zip")
        End If

        If My.Computer.FileSystem.FileExists("update.zip") Then
            My.Computer.FileSystem.DeleteFile("update.zip")
        End If

        If src.Contains(versionNum) Then
            MsgBox("Server tool is up to date.", , "Auto Update")
        Else
            MsgBox("Server tool is outdated, performing auto update.", , "Auto Update")
            Try
                Do While Not My.Computer.FileSystem.FileExists("updater.zip")
                    My.Computer.Network.DownloadFile(updaterFile, "updater.zip", True, 500)

                    If My.Computer.FileSystem.FileExists("updater.zip") Then
                        ZipFile.ExtractToDirectory(zipPath, extractPath)
                    End If

                    Process.Start("Server Tool Updater.exe")
                    Me.Close()
                Loop
            Catch ex As Exception
                MsgBox("Updated file not found.")
            End Try
        End If
    End Sub

    'Auto-Update getSrc
    Private Function getSrc(ByVal url As String)
        Dim r As httpwebrequest = httpwebrequest.create(url)
        Dim re As httpwebresponse = r.getresponse()
        Dim src As String = New streamreader(re.getresponsestream()).readtoend()
        Return src
    End Function
End Class

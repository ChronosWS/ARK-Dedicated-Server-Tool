Param
(
    [Parameter(Mandatory = $False)]
    [string]$ServerCache,

    [Parameter(Mandatory = $False)]
    [string]$InstallDirectory,

    [Parameter(Mandatory = $False)]
    [string]$rconIP,

    [Parameter(Mandatory = $False)]
    [string]$rconPort,

    [Parameter(Mandatory = $False)]
    [string]$rconPass,

    [Parameter(Mandatory = $False)]
    $graceMinutes,

    [Parameter(Mandatory = $False)]
    [string]$mcrconPath = "mcrcon.exe",

    [Parameter(Mandatory = $False)]
    [bool]$force = $False,

    [Parameter(Mandatory = $False)]
    [string]$WorldFileRoot = "TheIsland",

    [Parameter(Mandatory = $False)]
    [string]$LastUpdatedTimeFileName = "LastUpdated.txt",

    [Parameter(Mandatory = $False)]
    [string]$LastCheckedTimeFileName = "LastChecked.txt",

    [Parameter(Mandatory = $False)]
    [string]$CacheUpdateInProgressFileName = "UpdateInProgress.txt"
)

& whoami

$forceUpdateFile = "$($InstallDirectory)\ForceUpdate.txt"

function Send-RCON($message)
{
    Write-Host "RCON: $($message)"
    & $ServerCache\$mcrconPath -c -H $rconIP -P $rconPort -p $rconPass $message
}

function Write-Server($minutes)
{    
    Send-RCON "broadcast [SERVER] Update available.  Rebooting in $($minutes) minutes..."
}
     
try
{
    $lockFilePath = "$($ServerCache)\$($CacheUpdateInProgressFileName)"
    while($True) { try { $lockFile = [System.IO.File]::Open($lockFilePath, 'Create', 'Write', 'None');  break; } catch { Write-Host "Waiting for lock file"; Start-Sleep -Seconds 10; } }

    if(Test-Path $forceUpdateFile)
    {
        $force = $True
        Remove-Item $forceUpdateFile -Force
    }

    $localLastUpdatedFile = "$($InstallDirectory)\$($LastUpdatedTimeFileName)"
    if(Test-Path $localLastUpdatedFile)
    {
        [string]$timeString = Get-Content $localLastUpdatedFile
        $localTime = [System.DateTime]::Parse($timeString, [System.Globalization.CultureInfo]::CurrentCulture, [System.Globalization.DateTimeStyles]::RoundtripKind);
    }
    else
    {
        $localTime = [System.DateTime]::MinValue
    }

    $cacheLastUpdatedFile = "$($ServerCache)\$($LastUpdatedTimeFileName)"
    [string]$timeString = Get-Content $cacheLastUpdatedFile
    $cacheTime = [System.DateTime]::Parse($timeString, [System.Globalization.CultureInfo]::CurrentCulture, [System.Globalization.DateTimeStyles]::RoundtripKind);

    Write-Host "Cache Time: $($cacheTime)"
    Write-Host "Local Time: $($localTime)"

    if($force -or ($cacheTime -gt $localTime))
    {
        # Find the process
        $process = Get-WmiObject Win32_Process -Filter "name = 'ShooterGameServer.exe'" | Where {$_.CommandLine -match "$($InstallDirectory -replace "\\", "\\")" }
        if($process -ne $null)
        {
            $minutesLeft = $graceMinutes
            while($minutesLeft -gt 0)
            {
                Write-Server($minutesLeft);
                if($minutesLeft -gt 10)
                {
                    Start-Sleep -Seconds 300
                    $minutesLeft = $minutesLeft - 5
                }
                else
                {
                    Start-Sleep -Seconds 60
                    $minutesLeft = $minutesLeft - 1
                }
            }

            Send-RCON "saveworld"
            Start-Sleep -Seconds 15
            Send-RCON "broadcast [SERVER] Rebooting for upgrade..."                

            Stop-Process -Id $process.ProcessId -Force                
        }

        $worldSource = "$($InstallDirectory)\ShooterGame\Saved\SavedArks\$($WorldFileRoot).ark"
        if(Test-Path $worldSource)
        {
            Write-Host "Creating World Backup"
            try
            {                
                $backupFile = "$($InstallDirectory)\ShooterGame\Saved\SavedArks\$($WorldFileRoot)_$(Get-Date -format dd.MM.yyyy_HH.mm.ss)_ASMBackup.ark"
                Copy-Item -LiteralPath $worldSource -Destination $backupFile
            }
            catch
            {
                Write-Host "Unable to create backup."
            }
        }

        Write-Host "Updating..."
        New-Item "$($ServerCache)\ShooterGame\Saved" -ItemType Directory -Force
        New-Item "$($ServerCache)\ShooterGame\Content\Mods" -ItemType Directory -Force
        
        & robocopy `"$ServerCache`" `"$InstallDirectory`" /E /XD ShooterGame\Saved ShooterGame\Content\Mods steamapps /XF UpdateCache.CMD $CacheUpdateInProgressFileName
        Write-Host "Update complete.  Restarting..."
            
        runas /trustlevel:0x20000 "$InstallDirectory\ShooterGame\Saved\Config\WindowsServer\RunServer.cmd"
    }
    else
    {
        Write-Host "No update needed."
    }
}
finally
{
    $lockFile.Close()
    Remove-Item $lockFilePath -Force
}

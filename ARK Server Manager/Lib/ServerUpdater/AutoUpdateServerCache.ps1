Param
(
    [Parameter(Mandatory = $False)]
    [string]$ServerCache,

    [Parameter(Mandatory = $False)]
    [string]$SteamCmd,

    [Parameter(Mandatory = $False)]
    [string]$LastUpdatedTimeFileName = "LastUpdated.txt",

    [Parameter(Mandatory = $False)]
    [string]$LastCheckedTimeFileName = "LastChecked.txt",

    [Parameter(Mandatory = $False)]
    [string]$CacheUpdateInProgressFileName = "UpdateInProgress.txt"
)

try
{
    $lockFilePath = "$($ServerCache)\$($CacheUpdateInProgressFileName)"
    while($True) { try { $lockFile = [System.IO.File]::Open($lockFilePath, 'Create', 'Write', 'None');  break; } catch { Write-Host "Waiting for lock file"; Start-Sleep -Seconds 10; } }

    New-Item $ServerCache -ItemType Directory -Force

    $steamCmdOutput = & $SteamCmd +login anonymous +force_install_dir `"$ServerCache`" `"+app_update 376030`" +quit
    $downloadMatch = $steamCmdOutput | Select-String "downloading,"
    $successMatch = $steamCmdOutput | Select-String "Success!"

    Write-Host "Full Log:"
    Write-Host $steamCmdOutput
    Write-Host "Downloading match:"
    Write-Host $downloadMatch
    Write-Host "Install success match:"
    Write-Host $successMatch

    $downloaded = $False
    $successful = $False
    if($downloadMatch -ne $null)
    {
        $downloaded = $True
    }

    if($successMatch -ne $null)
    {
        $successful = $True
    }

    Write-Host Download: $downloaded
    Write-Host Success: $successful

    if($successful)
    {
        $lastCheckedFilePath = "$($ServerCache)\$($LastCheckedTimeFileName)"
        $lastUpdatedFilePath = "$($ServerCache)\$($LastUpdatedTimeFileName)"

        $time = [System.DateTime]::Now.ToString("o") 
        $time | Set-Content $lastCheckedFilePath

        if($downloaded -or -not (Test-Path $lastUpdatedFilePath))
        {
            $time | Set-Content $lastUpdatedFilePath
        }
    }
}
finally
{
    $lockFile.Close()
    Remove-Item $lockFilePath -Force
}


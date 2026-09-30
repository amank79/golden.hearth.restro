<#
  Installs or updates Restaurant POS on this computer.
  - Copies the app to C:\Program Files\RestaurantPos
  - Registers the "RestaurantPos" Windows service (starts with Windows, restarts itself if it crashes)
  - Keeps all data in C:\ProgramData\RestaurantPos (never touched by updates)
  - Adds "Restaurant POS" shortcuts to the desktop and Start menu

  Run Install.cmd (it asks for administrator permission). Running it again updates the app.
  Optional: -BackupFolder "E:\RestaurantPOS-Backup" adds an extra backup copy (e.g. a pen drive).
#>
param(
    [string]$BackupFolder,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'RestaurantPos'
$AppDir = Join-Path $env:ProgramFiles 'RestaurantPos'
$DataRoot = Join-Path $env:ProgramData 'RestaurantPos'
$Url = 'http://localhost:5080'
$Source = Join-Path $PSScriptRoot 'app'

# --- Ask for administrator rights if needed ---
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($BackupFolder) { $argList += @('-BackupFolder', "`"$BackupFolder`"") }
    if ($NoPause) { $argList += '-NoPause' }
    $p = Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $p.ExitCode
}

function Step($text) { Write-Host ''; Write-Host "==> $text" -ForegroundColor Cyan }
function Done($text) { Write-Host "    $text" -ForegroundColor Green }

$exitCode = 0
New-Item -ItemType Directory -Force (Join-Path $DataRoot 'logs') | Out-Null
Start-Transcript -Path (Join-Path $DataRoot ("logs\install-{0:yyyy-MM-dd_HHmmss}.log" -f (Get-Date))) | Out-Null
try {
    if (-not (Test-Path (Join-Path $Source 'RestaurantPos.Api.exe'))) {
        throw "App files not found in $Source. Run this from the extracted installer folder."
    }
    $version = 'unknown'
    $versionFile = Join-Path $PSScriptRoot 'version.txt'
    if (Test-Path $versionFile) { $version = (Get-Content $versionFile -Raw).Trim() }
    Write-Host "Restaurant POS installer - version $version" -ForegroundColor White

    # 1. Stop the running app (an update). The app makes a backup as it stops.
    $service = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Step 'Stopping the running app (it saves a backup first)'
        if ($service.Status -ne 'Stopped') {
            Stop-Service $ServiceName -Force
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
        }
        Get-Process 'RestaurantPos.Api' -ErrorAction SilentlyContinue | Stop-Process -Force
        Done 'Stopped'
    }

    # 2. Copy the app files.
    Step "Copying the app to $AppDir"
    robocopy $Source $AppDir /MIR /R:3 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying the app files failed (robocopy code $LASTEXITCODE)." }
    # Support files live next to the app so the Start menu shortcuts keep working after the zip is deleted.
    foreach ($f in @('version.txt', 'collect-logs.ps1', 'restore.ps1', 'README.txt')) {
        $p = Join-Path $PSScriptRoot $f
        if (Test-Path $p) { Copy-Item $p $AppDir -Force }
    }
    Get-ChildItem $AppDir -Recurse -File | Unblock-File
    Done 'Copied'

    # 3. Data folder and per-installation settings (kept across updates).
    Step "Preparing the data folder $DataRoot"
    New-Item -ItemType Directory -Force (Join-Path $DataRoot 'data'), (Join-Path $DataRoot 'backups') | Out-Null
    $settingsPath = Join-Path $DataRoot 'settings.json'
    if (Test-Path $settingsPath) {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    } else {
        $settings = [pscustomobject]@{ Backup = [pscustomobject]@{ ExtraFolders = @() } }
    }
    if (-not $settings.PSObject.Properties['Backup']) {
        $settings | Add-Member -NotePropertyName Backup -NotePropertyValue ([pscustomobject]@{ ExtraFolders = @() })
    }
    if (-not $settings.Backup.PSObject.Properties['ExtraFolders']) {
        $settings.Backup | Add-Member -NotePropertyName ExtraFolders -NotePropertyValue @()
    }
    if ($BackupFolder -and (@($settings.Backup.ExtraFolders) -notcontains $BackupFolder)) {
        $settings.Backup.ExtraFolders = @(@($settings.Backup.ExtraFolders) + $BackupFolder)
    }
    $json = $settings | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($settingsPath, $json, (New-Object Text.UTF8Encoding($false)))
    Done "Backups: $DataRoot\backups"
    foreach ($f in @($settings.Backup.ExtraFolders)) { Done "Extra backup copy: $f" }

    # 4. Windows service: starts with Windows, restarts after a crash.
    Step 'Registering the Windows service'
    $exe = Join-Path $AppDir 'RestaurantPos.Api.exe'
    if (-not $service) {
        New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Restaurant POS' `
            -Description 'Restaurant billing and menu. Keep this running.' -StartupType Automatic | Out-Null
    } else {
        sc.exe config $ServiceName start= auto binPath= "`"$exe`"" | Out-Null
    }
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null
    Done 'Registered (starts automatically with Windows)'

    # 5. Start and wait until it answers.
    Step 'Starting the app'
    Start-Service $ServiceName
    $ok = $false
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "$Url/api/health" -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { $ok = $true; break }
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ok) { throw "The app did not start. See Windows Event Viewer > Application, or run: `"$exe`" in a console." }
    Done "Running at $Url"

    # 6. Shortcuts that open the app in its own window (Edge app mode: no address bar or tabs).
    Step 'Creating shortcuts'
    $edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
              "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    $shell = New-Object -ComObject WScript.Shell
    $startMenu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
    foreach ($lnkPath in @((Join-Path $env:PUBLIC 'Desktop\Restaurant POS.lnk'), (Join-Path $startMenu 'Restaurant POS.lnk'))) {
        $lnk = $shell.CreateShortcut($lnkPath)
        if ($edge) {
            $lnk.TargetPath = $edge
            $lnk.Arguments = "--app=$Url --no-first-run"
        } else {
            $lnk.TargetPath = Join-Path $env:WINDIR 'explorer.exe'
            $lnk.Arguments = $Url
        }
        $lnk.IconLocation = Join-Path $AppDir 'app.ico'
        $lnk.Description = 'Restaurant billing'
        $lnk.Save()
    }
    $lnk = $shell.CreateShortcut((Join-Path $startMenu 'Restaurant POS backups.lnk'))
    $lnk.TargetPath = Join-Path $DataRoot 'backups'
    $lnk.Save()
    $lnk = $shell.CreateShortcut((Join-Path $startMenu 'Restaurant POS logs.lnk'))
    $lnk.TargetPath = Join-Path $DataRoot 'logs'
    $lnk.Save()
    $lnk = $shell.CreateShortcut((Join-Path $startMenu 'Restaurant POS - collect logs for support.lnk'))
    $lnk.TargetPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $lnk.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$AppDir\collect-logs.ps1`""
    $lnk.IconLocation = Join-Path $AppDir 'app.ico'
    $lnk.Save()
    if (-not $edge) { Write-Host '    Microsoft Edge not found: the shortcut opens the default browser.' -ForegroundColor Yellow }
    Done 'Desktop and Start menu: "Restaurant POS"'

    Write-Host ''
    Write-Host "Restaurant POS $version is installed and running." -ForegroundColor Green
    if (-not $NoPause) { Start-Process (Join-Path $env:PUBLIC 'Desktop\Restaurant POS.lnk') }
} catch {
    $exitCode = 1
    Write-Host ''
    Write-Host "INSTALL FAILED: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}

if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode

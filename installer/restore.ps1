<#
  Restores the Restaurant POS data from a backup.
  The current data is kept as data\pos.before-restore-<time>.db, so a restore can itself be undone.

  Run Restore.cmd and pick a backup from the list, or:
  restore.ps1 -BackupFile "E:\RestaurantPOS-Backup\pos-2026-10-11_223015.db" -Yes
#>
param(
    [string]$BackupFile,
    [switch]$Yes,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'RestaurantPos'
$DataRoot = Join-Path $env:ProgramData 'RestaurantPos'
$Db = Join-Path $DataRoot 'data\pos.db'
$Url = 'http://localhost:5080'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($BackupFile) { $argList += @('-BackupFile', "`"$BackupFile`"") }
    if ($Yes) { $argList += '-Yes' }
    if ($NoPause) { $argList += '-NoPause' }
    $p = Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $p.ExitCode
}

$exitCode = 0
New-Item -ItemType Directory -Force (Join-Path $DataRoot 'logs') | Out-Null
Start-Transcript -Path (Join-Path $DataRoot ("logs\restore-{0:yyyy-MM-dd_HHmmss}.log" -f (Get-Date))) | Out-Null
try {
    if (-not $BackupFile) {
        $backups = @(Get-ChildItem (Join-Path $DataRoot 'backups') -Filter 'pos-*.db' -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | Select-Object -First 20)
        if ($backups.Count -eq 0) { throw "No backups found in $DataRoot\backups. Use -BackupFile to restore from a pen drive." }
        Write-Host 'Newest backups:' -ForegroundColor White
        for ($i = 0; $i -lt $backups.Count; $i++) {
            Write-Host ('  {0,2}. {1}   {2:N0} KB' -f ($i + 1), $backups[$i].Name, ($backups[$i].Length / 1KB))
        }
        $choice = Read-Host 'Type the number of the backup to restore (or press Enter to cancel)'
        if (-not $choice) { Write-Host 'Cancelled.'; exit 0 }
        $BackupFile = $backups[[int]$choice - 1].FullName
    }
    if (-not (Test-Path $BackupFile)) { throw "Backup file not found: $BackupFile" }

    if (-not $Yes) {
        Write-Host ''
        Write-Host "This replaces the current data with: $BackupFile" -ForegroundColor Yellow
        Write-Host 'Bills made after that backup will not be in the app (they stay in the saved copy).' -ForegroundColor Yellow
        if ((Read-Host 'Type YES to continue') -ne 'YES') { Write-Host 'Cancelled.'; exit 0 }
    }

    Write-Host 'Stopping the app...'
    $service = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Stop-Service $ServiceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
    }

    $stamp = '{0:yyyy-MM-dd_HHmmss}' -f (Get-Date)
    $saved = Join-Path $DataRoot "data\pos.before-restore-$stamp.db"
    foreach ($suffix in @('', '-wal', '-shm')) {
        if (Test-Path "$Db$suffix") { Move-Item "$Db$suffix" "$saved$suffix" }
    }
    Copy-Item $BackupFile $Db
    Write-Host "Current data saved as $saved"

    if ($service) {
        Write-Host 'Starting the app...'
        Start-Service $ServiceName
        $ok = $false
        for ($i = 0; $i -lt 60; $i++) {
            try {
                if ((Invoke-WebRequest -Uri "$Url/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $ok = $true; break }
            } catch { Start-Sleep -Seconds 1 }
        }
        if (-not $ok) { throw "The app did not start after the restore. The previous data is in $saved." }
    }
    Write-Host ''
    Write-Host "Restored from $(Split-Path $BackupFile -Leaf)." -ForegroundColor Green
} catch {
    $exitCode = 1
    Write-Host "RESTORE FAILED: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}

if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode

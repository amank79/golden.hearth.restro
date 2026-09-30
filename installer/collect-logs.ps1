<#
  Collects Restaurant POS logs into one zip on the desktop, to send to the developer (e.g. on WhatsApp).
  Includes: app logs (last 14 days), install/restore logs, backup history, settings, service status and
  recent Windows errors about the app. Does NOT include the database (no bills or customer data).
#>
param([switch]$NoPause)

$ErrorActionPreference = 'Stop'
$DataRoot = Join-Path $env:ProgramData 'RestaurantPos'
$AppDir = Join-Path $env:ProgramFiles 'RestaurantPos'
$stamp = '{0:yyyy-MM-dd_HHmm}' -f (Get-Date)
$work = Join-Path $env:TEMP "RestaurantPOS-logs-$stamp"
$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "RestaurantPOS-logs-$stamp.zip"

$exitCode = 0
try {
    New-Item -ItemType Directory -Force $work | Out-Null
    $cutoff = (Get-Date).AddDays(-14)

    $logs = Join-Path $DataRoot 'logs'
    if (Test-Path $logs) {
        Get-ChildItem $logs -File | Where-Object { $_.LastWriteTime -ge $cutoff } |
            ForEach-Object { Copy-Item $_.FullName (Join-Path $work $_.Name) }
    }
    foreach ($f in @('backups\backup-log.txt', 'settings.json')) {
        $p = Join-Path $DataRoot $f
        if (Test-Path $p) { Copy-Item $p (Join-Path $work (Split-Path $p -Leaf)) }
    }

    # A short summary of the computer and the app.
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Collected: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $lines.Add("Computer: $env:COMPUTERNAME, Windows $([Environment]::OSVersion.Version)")
    $versionFile = Join-Path $AppDir 'version.txt'
    if (Test-Path $versionFile) { $lines.Add("App version: $((Get-Content $versionFile -Raw).Trim())") }
    $svc = Get-Service 'RestaurantPos' -ErrorAction SilentlyContinue
    if ($svc) { $lines.Add("Service: $($svc.Status), start type $($svc.StartType)") } else { $lines.Add('Service: NOT INSTALLED') }
    try {
        $health = Invoke-WebRequest 'http://localhost:5080/api/health' -UseBasicParsing -TimeoutSec 3
        $lines.Add("Health: $($health.Content)")
        $backup = Invoke-WebRequest 'http://localhost:5080/api/backup/status' -UseBasicParsing -TimeoutSec 3
        $lines.Add("Backup status: $($backup.Content)")
    } catch { $lines.Add("Health: app not answering ($($_.Exception.Message))") }
    $drive = Get-PSDrive C -ErrorAction SilentlyContinue
    if ($drive) { $lines.Add(('Disk C: {0:N1} GB free' -f ($drive.Free / 1GB))) }
    $db = Join-Path $DataRoot 'data\pos.db'
    if (Test-Path $db) { $lines.Add(('Database size: {0:N0} KB' -f ((Get-Item $db).Length / 1KB))) }
    $lines.Add('')
    $lines.Add('Newest backups:')
    Get-ChildItem (Join-Path $DataRoot 'backups') -Filter 'pos-*.db' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 10 |
        ForEach-Object { $lines.Add(('  {0}  {1:N0} KB' -f $_.Name, ($_.Length / 1KB))) }
    $lines | Set-Content (Join-Path $work 'summary.txt') -Encoding UTF8

    # Recent Windows errors about the app (crashes, service failures).
    $since = (Get-Date).AddDays(-7)
    $events = @()
    $events += Get-WinEvent -FilterHashtable @{ LogName = 'Application'; Level = 1, 2, 3; StartTime = $since } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -match 'RestaurantPos|\.NET Runtime|Application Error' -and $_.Message -match 'RestaurantPos' }
    $events += Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'Service Control Manager'; StartTime = $since } -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'Restaurant POS' }
    $events | Sort-Object TimeCreated | ForEach-Object { "{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}: {3}" -f $_.TimeCreated, $_.LevelDisplayName, $_.ProviderName, ($_.Message -replace '\s+', ' ') } |
        Set-Content (Join-Path $work 'windows-events.txt') -Encoding UTF8

    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $work '*') -DestinationPath $zip
    Write-Host ''
    Write-Host 'Logs saved on the desktop:' -ForegroundColor Green
    Write-Host "  $zip"
    Write-Host 'Send this file to the developer (WhatsApp or email). It contains no bills or customer data.'
    if (-not $NoPause) { Start-Process explorer.exe "/select,`"$zip`"" }
} catch {
    $exitCode = 1
    Write-Host "Could not collect the logs: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}

if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode

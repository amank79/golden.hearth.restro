<#
  Removes the Restaurant POS app, service and shortcuts.
  The data and backups in C:\ProgramData\RestaurantPos are KEPT unless -RemoveData is given.
#>
param(
    [switch]$RemoveData,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'RestaurantPos'
$AppDir = Join-Path $env:ProgramFiles 'RestaurantPos'
$DataRoot = Join-Path $env:ProgramData 'RestaurantPos'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($RemoveData) { $argList += '-RemoveData' }
    if ($NoPause) { $argList += '-NoPause' }
    $p = Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $p.ExitCode
}

$exitCode = 0
try {
    $service = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -ne 'Stopped') {
            Stop-Service $ServiceName -Force
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
        }
        sc.exe delete $ServiceName | Out-Null
        Write-Host 'Service removed.'
    }
    Get-Process 'RestaurantPos.Api' -ErrorAction SilentlyContinue | Stop-Process -Force

    $startMenu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
    foreach ($lnk in @((Join-Path $env:PUBLIC 'Desktop\Restaurant POS.lnk'),
                       (Join-Path $startMenu 'Restaurant POS.lnk'),
                       (Join-Path $startMenu 'Restaurant POS backups.lnk'),
                       (Join-Path $startMenu 'Restaurant POS logs.lnk'),
                       (Join-Path $startMenu 'Restaurant POS - collect logs for support.lnk'))) {
        if (Test-Path $lnk) { Remove-Item $lnk -Force }
    }
    if (Test-Path $AppDir) { Remove-Item $AppDir -Recurse -Force }
    Write-Host 'App and shortcuts removed.'

    if ($RemoveData) {
        if (Test-Path $DataRoot) { Remove-Item $DataRoot -Recurse -Force }
        Write-Host 'Data and backups removed.' -ForegroundColor Yellow
    } else {
        Write-Host "Data and backups kept in $DataRoot" -ForegroundColor Green
    }
} catch {
    $exitCode = 1
    Write-Host "UNINSTALL FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode

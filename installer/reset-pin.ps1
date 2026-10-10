<#
  Removes a forgotten owner PIN. Bills, menu and settings are not touched.
  Needs administrator permission (Windows asks). Afterwards set a new PIN in Restaurant POS > Settings.
#>
param([switch]$NoPause)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $env:ProgramFiles 'RestaurantPos\RestaurantPos.Api.exe'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($NoPause) { $argList += '-NoPause' }
    $p = Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $p.ExitCode
}

$exitCode = 0
try {
    if (-not (Test-Path $exe)) { throw "Restaurant POS is not installed ($exe not found)." }
    Write-Host 'Removing the owner PIN...' -ForegroundColor Cyan
    & $exe --reset-owner-pin
    if ($LASTEXITCODE -ne 0) { throw "The reset did not finish (code $LASTEXITCODE). See C:\ProgramData\RestaurantPos\logs." }
    Write-Host ''
    Write-Host 'Done. Open Restaurant POS > Settings > Owner PIN to set a new PIN.' -ForegroundColor Green
} catch {
    $exitCode = 1
    Write-Host "PIN NOT RESET: $($_.Exception.Message)" -ForegroundColor Red
}

if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode

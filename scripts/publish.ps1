<#
  Builds the installer package: dist\RestaurantPos-<version>.zip
  Contents: app\ (self-contained, no .NET needed on the target laptop), Install/Restore/Uninstall scripts, README.txt.

  Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
#>
param([string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Version) { $Version = Get-Date -Format 'yyyy.M.d.Hmm' }

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }

$out = Join-Path $root 'dist\RestaurantPos'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

Write-Host "Building frontend..." -ForegroundColor Cyan
Push-Location (Join-Path $root 'src\web')
try {
    npm ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'npm run build failed' }
} finally { Pop-Location }

Write-Host "Publishing app $Version..." -ForegroundColor Cyan
& $dotnet publish (Join-Path $root 'src\RestaurantPos.Api') -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -o (Join-Path $out 'app') --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
if (-not (Test-Path (Join-Path $out 'app\wwwroot\index.html'))) { throw 'wwwroot\index.html missing from the published app' }

Copy-Item (Join-Path $root 'installer\*') $out
Set-Content (Join-Path $out 'version.txt') $Version -Encoding ASCII

$zip = Join-Path $root "dist\RestaurantPos-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "Package: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)" -ForegroundColor Green

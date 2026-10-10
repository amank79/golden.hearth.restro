<#
  Adds the dishes in menu.csv to the running Restaurant POS (http://localhost:5080).
  - Categories are created in the order they first appear in the file.
  - A dish that is already on the menu (same name) is skipped, so running this twice is safe.
  - Prices are in rupees. A dish with sizes leaves Price empty and fills Size 1/Price 1, Size 2/Price 2.
  - Every dish is added as Veg, available today, at the restaurant's GST rate. Change any dish later in Menu.

  Usage: double-click "Load menu.cmd", or:  load-menu.ps1 [-MenuFile path] [-Url http://localhost:5080]
#>
param(
    [string]$MenuFile = (Join-Path $PSScriptRoot 'menu.csv'),
    [string]$Url = 'http://localhost:5080',
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$headers = @{}

function Send($method, $path, $body) {
    $json = $body | ConvertTo-Json -Depth 5
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    Invoke-RestMethod -Method $method -Uri "$Url/api$path" -Body $bytes -ContentType 'application/json; charset=utf-8' -Headers $headers
}

function Paise([string]$rupees, [string]$what) {
    $value = [decimal]0
    if (-not [decimal]::TryParse($rupees.Trim(), [Globalization.NumberStyles]::Number, [Globalization.CultureInfo]::InvariantCulture, [ref]$value) -or $value -le 0) {
        throw "$what has no valid price: '$rupees'"
    }
    [long][decimal]::Round($value * 100)
}

$exitCode = 0
try {
    if (-not (Test-Path $MenuFile)) { throw "Menu file not found: $MenuFile" }
    $rows = @(Import-Csv $MenuFile -Encoding UTF8 | Where-Object { $_.Dish -and $_.Dish.Trim() })
    Write-Host "Loading $($rows.Count) dishes from $(Split-Path $MenuFile -Leaf) into $Url" -ForegroundColor Cyan

    try { $menu = Invoke-RestMethod "$Url/api/menu?includeInactive=true" }
    catch { throw "Restaurant POS is not running at $Url. Install it first (Install.cmd)." }

    # Menu changes need the owner PIN once one is set.
    if ((Invoke-RestMethod "$Url/api/owner/status").pinSet) {
        $secure = Read-Host 'Owner PIN' -AsSecureString
        $pin = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
        try { $unlock = Send 'Post' '/owner/unlock' @{ pin = $pin } }
        catch { throw "The PIN was not accepted: $(if ($_.ErrorDetails.Message) { ($_.ErrorDetails.Message | ConvertFrom-Json).title } else { $_.Exception.Message })" }
        $headers['X-Owner-Token'] = $unlock.token
    }

    # Check the whole file before adding anything, so a mistake does not leave half a menu.
    $plans = foreach ($r in $rows) {
        $name = $r.Dish.Trim()
        $variants = @()
        if ($r.Price -and $r.Price.Trim()) {
            $variants += @{ name = $null; pricePaise = (Paise $r.Price $name) }
        } else {
            foreach ($n in 1, 2) {
                $size = $r."Size $n"; $price = $r."Price $n"
                if ($size -and $size.Trim()) { $variants += @{ name = $size.Trim(); pricePaise = (Paise $price "$name ($size)") } }
            }
            if ($variants.Count -eq 0) { throw "$name has no Price and no sizes." }
        }
        [pscustomobject]@{ Category = $r.Category.Trim(); Name = $name; Code = $r.Code.Trim(); Variants = $variants }
    }

    $categories = @{}
    foreach ($c in $menu.categories | Where-Object { $_.isActive }) { $categories[$c.name.ToLower()] = $c.id }
    $existing = @{}
    foreach ($i in $menu.items | Where-Object { $_.isActive }) { $existing[$i.name.ToLower()] = $true }

    $added = 0; $skipped = 0
    foreach ($p in $plans) {
        if (-not $categories.ContainsKey($p.Category.ToLower())) {
            $c = Send 'Post' '/menu/categories' @{ name = $p.Category }
            $categories[$p.Category.ToLower()] = $c.id
            Write-Host "  + category $($p.Category)" -ForegroundColor Green
        }
        if ($existing.ContainsKey($p.Name.ToLower())) {
            $skipped++
            Write-Host "    already on the menu: $($p.Name)" -ForegroundColor DarkGray
            continue
        }
        $body = @{
            categoryId = $categories[$p.Category.ToLower()]
            name = $p.Name
            shortCode = $(if ($p.Code) { $p.Code } else { $null })
            foodType = 'Veg'
            isAvailable = $true
            variants = @($p.Variants)
        }
        try { Send 'Post' '/menu/items' $body | Out-Null }
        catch {
            $detail = $_.ErrorDetails.Message
            throw "Could not add $($p.Name): $(if ($detail) { $detail } else { $_.Exception.Message })"
        }
        $existing[$p.Name.ToLower()] = $true
        $added++
        Write-Host "    $($p.Name)"
    }
    Write-Host ''
    Write-Host "Done: $added dishes added, $skipped already on the menu." -ForegroundColor Green
} catch {
    $exitCode = 1
    Write-Host ''
    Write-Host "MENU NOT LOADED: $($_.Exception.Message)" -ForegroundColor Red
}

if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode

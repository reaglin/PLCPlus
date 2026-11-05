<#
.SYNOPSIS
    Draws every PLC+ icon, tile and Store listing image.

.DESCRIPTION
    Copied from Author+ (the family's model): the mark is drawn here once at 2048 px and everything
    else is a downscale of it, so the small sizes stay sharp and a change to the artwork is a change
    to this file and one command.

    The mark is one rung of ladder logic in white on steel blue: the two power rails, a normally-open
    contact -| |-, and in the coil's place the suite's amber "+" (the "+" theme, CLAUDE.md). Two
    versions are drawn:
      * the titled mark - the rung smaller, with "PLC+" in white under it - for every tile of 71 px
        and up, the listing images and the .ico's large frames;
      * the plain mark - the rung alone - for 16-48 px (taskbar, Start list, the .ico's small frames),
        where lettering cannot be read.
    The wide tile, the splash screen and the hero images set the name beside the plain glyph.

    Every package asset is written under WACK's 204800-byte limit for logo images (256 colours first,
    then fewer until it fits). The Partner Center uploads are not capped.

    Outputs:
      packaging/Assets/                    MSIX tiles - unqualified (scale-100) plus scale-* variants
      store/images/                        Partner Center upload images
      src/Learning.Shell/PLCPlus.ico       the .exe icon (skip with -SkipIco)

    Requires ImageMagick 7 (magick.exe) on PATH.

.EXAMPLE
    ./packaging/make-store-assets.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipIco
)

$ErrorActionPreference = 'Stop'

$repoRoot  = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $repoRoot 'packaging/Assets'
$storeDir  = Join-Path $repoRoot 'store/images'
$workDir   = Join-Path ([System.IO.Path]::GetTempPath()) 'plcplus-store-assets'

foreach ($d in @($assetsDir, $storeDir, $workDir)) { New-Item -ItemType Directory -Force $d | Out-Null }

function Invoke-Magick {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    & magick @Arguments
    if ($LASTEXITCODE -ne 0) { throw "magick failed: $($Arguments -join ' ')" }
}

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw 'ImageMagick 7 (magick.exe) is not on PATH. Install it, or run this on a machine that has it.'
}

# --- palette -----------------------------------------------------------------------------------
$navy    = '#14324A'   # the ground (steel blue), and the splash screen colour in AppxManifest.xml
$navyLo  = '#0B1E2E'   # the hero gradient's far end
$accent  = '#F2A93B'   # the plus (amber, like a pilot light)
$white   = '#FFFFFF'
$muted   = '#A9C1D6'
$shadow  = '#0A1A28'
# magick strips backslashes out of -font paths, so keep it forward-slashed.
$font    = $env:WINDIR.Replace('\', '/') + '/Fonts/segoeuib.ttf'

# WACK fails a package whose logo images exceed this; see Write-Capped.
$MaxAssetBytes = 204800

# --- the mark ----------------------------------------------------------------------------------
function Get-PlusArt([int]$cx, [int]$cy, [int]$len, [int]$bar) {
    $h = [int]($len / 2); $b = [int]($bar / 2); $o = [int]($bar * 0.22)
    @('-fill', $shadow, '-stroke', 'none',
      '-draw', "roundrectangle $($cx-$b+$o),$($cy-$h+$o) $($cx+$b+$o),$($cy+$h+$o) $b,$b",
      '-draw', "roundrectangle $($cx-$h+$o),$($cy-$b+$o) $($cx+$h+$o),$($cy+$b+$o) $b,$b",
      '-fill', $accent,
      '-draw', "roundrectangle $($cx-$b),$($cy-$h) $($cx+$b),$($cy+$h) $b,$b",
      '-draw', "roundrectangle $($cx-$h),$($cy-$b) $($cx+$h),$($cy+$b) $b,$b")
}

# One rung: rails at the sides, the wire across, a contact (two tall bars) on the left, the "+" as the coil.
# Drawn thick, so it still reads as a ladder at 24 px.
$plainArt = @(
    '-fill', $white, '-stroke', 'none',
    '-draw', 'roundrectangle 360,400 470,1648 40,40',      # left rail
    '-draw', 'roundrectangle 1578,400 1688,1648 40,40',    # right rail
    '-draw', 'rectangle 470,986 780,1062',                  # wire to the contact
    '-draw', 'roundrectangle 780,780 872,1268 30,30',      # contact, left bar
    '-draw', 'roundrectangle 1000,780 1092,1268 30,30',    # contact, right bar
    '-draw', 'rectangle 1092,986 1170,1062',                # wire to the coil
    '-draw', 'rectangle 1500,986 1578,1062'                 # wire from the coil to the right rail
) + (Get-PlusArt 1335 1024 400 124)

$mark   = Join-Path $workDir 'mark-2048.png'         # plain, on the ground: 16-48 px
$titled = Join-Path $workDir 'titled-2048.png'       # titled, on the ground: 71 px and up
$glyph  = Join-Path $workDir 'glyph-2048.png'        # plain, transparent, for compositing beside the name

Invoke-Magick -size 2048x2048 "xc:$navy" @plainArt -strip $mark
Invoke-Magick -size 2048x2048 'xc:none'  @plainArt -strip $glyph
# Titled: the rung at 70% high on the tile, "PLC+" in white under it.
Invoke-Magick -size 2048x2048 "xc:$navy" '(' $glyph -filter Lanczos -resize 1430x1430 ')' `
    -gravity north -geometry '+0+40' -compose over -composite `
    -font $font -fill $white -pointsize 360 -gravity south -annotate '+0+190' 'PLC+' -strip $titled

# --- emitters ----------------------------------------------------------------------------------
function Write-Capped {
    # Write Src at Width x Height, quantised only as far as it must be to clear the byte cap.
    param([string]$Src, [int]$Width, [int]$Height, [string]$Dest, [string]$Mode = 'Fit')

    foreach ($colors in 256, 128, 64, 32) {
        $cmd = if ($Mode -eq 'Pad') {
            @('-size', "${Width}x${Height}", "xc:$navy",
              '(', $Src, '-filter', 'Lanczos', '-resize', "${Width}x${Height}", ')',
              '-gravity', 'center', '-composite')
        } else {
            @($Src, '-filter', 'Lanczos', '-resize', "${Width}x${Height}^",
              '-gravity', 'center', '-extent', "${Width}x${Height}")
        }
        Invoke-Magick @cmd -colors $colors -strip $Dest
        if ((Get-Item $Dest).Length -le $MaxAssetBytes) { return }
    }
    Write-Warning ("{0} is {1} bytes, over WACK's {2} limit even at 32 colours." -f `
        (Split-Path -Leaf $Dest), (Get-Item $Dest).Length, $MaxAssetBytes)
}

function Write-Plain {
    # Uncapped, full colour - for the Partner Center uploads, which are not in the package.
    param([string]$Src, [int]$Width, [int]$Height, [string]$Dest, [string]$Mode = 'Fit')
    if ($Mode -eq 'Pad') {
        Invoke-Magick -size "${Width}x${Height}" "xc:$navy" `
            '(' $Src -filter Lanczos -resize "${Width}x${Height}" ')' `
            -gravity center -composite -strip $Dest
    } else {
        Invoke-Magick $Src -filter Lanczos -resize "${Width}x${Height}^" `
            -gravity center -extent "${Width}x${Height}" -strip $Dest
    }
}

$scales = @(
    @{ Suffix = '.scale-100'; Factor = 1.00 },
    @{ Suffix = '.scale-125'; Factor = 1.25 },
    @{ Suffix = '.scale-150'; Factor = 1.50 },
    @{ Suffix = '.scale-200'; Factor = 2.00 },
    @{ Suffix = '.scale-400'; Factor = 4.00 }
)

function Write-TileSet {
    # Each scale from the titled mark when it comes out at 71 px or more, else from the plain one.
    param([string]$Name, [int]$Width, [int]$Height, [string]$Src, [string]$Mode = 'Fit')
    foreach ($s in $scales) {
        $w = [int][math]::Round($Width  * $s.Factor)
        $h = [int][math]::Round($Height * $s.Factor)
        $dest = Join-Path $assetsDir "$Name$($s.Suffix).png"
        $from = if ($Src -eq $mark -and [math]::Min($w, $h) -ge 71) { $titled } else { $Src }
        Write-Capped $from $w $h $dest $Mode
        if ($s.Suffix -eq '.scale-100') { Copy-Item $dest (Join-Path $assetsDir "$Name.png") -Force }
    }
    Write-Host "  $Name" -ForegroundColor DarkGray
}

function Write-TargetSizes {
    param([string]$Name, [string]$Src)
    foreach ($t in 16, 24, 32, 48, 256) {
        Write-Capped $Src $t $t (Join-Path $assetsDir "$Name.targetsize-$t.png")
        Write-Capped $Src $t $t (Join-Path $assetsDir ("$Name.targetsize-$t" + "_altform-unplated.png"))
    }
}

function Write-WideTile {
    # The glyph beside the name, on flat navy.
    param([int]$Width, [int]$Height, [string]$Dest)
    $margin = [int]($Width * 0.05)
    $artW   = [int]($Height * 0.78)
    $textX  = $margin + $artW + [int]($Width * 0.05)
    Invoke-Magick -size "${Width}x${Height}" "xc:$navy" `
        '(' $glyph -filter Lanczos -resize "${artW}x${artW}" ')' `
        -gravity west -geometry "+$margin+0" -compose over -composite `
        -gravity northwest -font $font -pointsize ([int]($Height * 0.21)) -fill $white `
        -annotate "+$textX+$([int]($Height * 0.37))" 'PLC+' `
        -colors 256 -strip $Dest
}

function Write-Splash {
    # The mark centred with the name under it, on the navy the manifest declares.
    param([int]$Width, [int]$Height, [string]$Dest)
    $artH = [int]($Height * 0.52)
    Invoke-Magick -size "${Width}x${Height}" "xc:$navy" `
        '(' $glyph -filter Lanczos -resize "${artH}x${artH}" ')' `
        -gravity center -geometry "+0-$([int]($Height * 0.08))" -compose over -composite `
        -gravity center -font $font -pointsize ([int]($Height * 0.15)) -fill $white `
        -annotate "+0+$([int]($Height * 0.30))" 'PLC+' `
        -colors 256 -strip $Dest
}

function Write-Hero {
    # The mark left, the name and a line of copy right. caption: fits the point size to the
    # column, so the wording can change without overflowing.
    param([int]$Width, [int]$Height, [string]$Dest)
    $margin = [int]($Width * 0.06)
    $artW   = [int]($Height * 0.62)
    $textX  = $margin + $artW + [int]($Width * 0.05)
    $textW  = $Width - $textX - $margin

    $name = Join-Path $workDir "hero-name-$Width.png"
    $tag  = Join-Path $workDir "hero-tag-$Width.png"
    Invoke-Magick -background none -fill $white -font $font `
        -size "${textW}x$([int]($Height * 0.13))" -gravity west 'caption:PLC+' -strip $name
    Invoke-Magick -background none -fill $muted -font $font `
        -size "${textW}x$([int]($Height * 0.055))" -gravity west `
        'caption:Learn PLC programming with simulated machines' -strip $tag

    Invoke-Magick -size "${Width}x${Height}" "gradient:$navy-$navyLo" `
        '(' $glyph -filter Lanczos -resize "${artW}x${artW}" ')' `
        -gravity west -geometry "+$margin+0" -compose over -composite `
        -gravity northwest `
        $name -geometry "+$textX+$([int]($Height * 0.33))" -composite `
        $tag  -geometry "+$textX+$([int]($Height * 0.53))" -composite `
        -strip $Dest
    Write-Host "  $(Split-Path -Leaf $Dest)" -ForegroundColor DarkGray
}

# --- MSIX tiles ----------------------------------------------------------------------------------
Write-Host 'MSIX tiles -> packaging/Assets' -ForegroundColor Cyan
# Square44x44 is the taskbar / Start list icon: plain at every scale, so it matches the 16-48 px target sizes.
foreach ($s in $scales) {
    $px = [int][math]::Round(44 * $s.Factor)
    Write-Capped $mark $px $px (Join-Path $assetsDir "Square44x44Logo$($s.Suffix).png")
}
Copy-Item (Join-Path $assetsDir 'Square44x44Logo.scale-100.png') (Join-Path $assetsDir 'Square44x44Logo.png') -Force
Write-Host '  Square44x44Logo' -ForegroundColor DarkGray
Write-TileSet 'Square71x71Logo'   71  71  $mark
Write-TileSet 'Square150x150Logo' 150 150 $mark
Write-TileSet 'Square310x310Logo' 310 310 $mark
Write-TileSet 'StoreLogo'         50  50  $mark
Write-TargetSizes 'Square44x44Logo' $mark

foreach ($s in $scales) {
    Write-WideTile ([int][math]::Round(310 * $s.Factor)) ([int][math]::Round(150 * $s.Factor)) `
        (Join-Path $assetsDir "Wide310x150Logo$($s.Suffix).png")
    Write-Splash   ([int][math]::Round(620 * $s.Factor)) ([int][math]::Round(300 * $s.Factor)) `
        (Join-Path $assetsDir "SplashScreen$($s.Suffix).png")
}
Copy-Item (Join-Path $assetsDir 'Wide310x150Logo.scale-100.png') (Join-Path $assetsDir 'Wide310x150Logo.png') -Force
Copy-Item (Join-Path $assetsDir 'SplashScreen.scale-100.png')    (Join-Path $assetsDir 'SplashScreen.png') -Force
Write-Host '  Wide310x150Logo' -ForegroundColor DarkGray
Write-Host '  SplashScreen' -ForegroundColor DarkGray

# --- Partner Center listing images -----------------------------------------------------------------
Write-Host 'Store listing -> store/images' -ForegroundColor Cyan
Write-Plain $titled 300  300  (Join-Path $storeDir 'StoreLogo-300x300.png')
Write-Plain $titled 1080 1080 (Join-Path $storeDir 'BoxArt-1080x1080.png')
Write-Plain $titled 720  1080 (Join-Path $storeDir 'PosterArt-720x1080.png') 'Pad'
Write-Hero 2400 1200 (Join-Path $storeDir 'SuperHeroArt-2400x1200.png')
Write-Hero 1920 1080 (Join-Path $storeDir 'HeroImage-1920x1080.png')

# --- the .exe icon ------------------------------------------------------------------------------
if (-not $SkipIco) {
    Write-Host 'Application icon -> src/Learning.Shell/PLCPlus.ico' -ForegroundColor Cyan
    # Titled frames where the name can be read (64 px and up), plain ones below.
    $frames = foreach ($px in 256, 128, 64, 48, 40, 32, 24, 20, 16) {
        $f = Join-Path $workDir "ico-$px.png"
        Invoke-Magick $(if ($px -ge 64) { $titled } else { $mark }) -filter Lanczos -resize "${px}x${px}" -strip $f
        $f
    }
    Invoke-Magick @frames (Join-Path $repoRoot 'src/Learning.Shell/PLCPlus.ico')

    # The same titled mark for the program's tile on softwareplus.ai, when that repo is beside this one.
    $siteIcon = Join-Path (Split-Path -Parent $repoRoot) 'SoftwarePlus/site/images/icons/plcplus.png'
    if (Test-Path (Split-Path -Parent $siteIcon)) {
        Invoke-Magick $titled -filter Lanczos -resize 360x360 -strip $siteIcon
        Write-Host "  softwareplus.ai tile -> $siteIcon (deploy it from that repo)" -ForegroundColor DarkGray
    }
}

# --- contact sheet ------------------------------------------------------------------------------
$sheet = Join-Path $storeDir 'asset-contact-sheet.png'
Push-Location $assetsDir
try {
    & magick montage `
        'Square44x44Logo.targetsize-16.png' 'Square44x44Logo.targetsize-32.png' `
        'Square44x44Logo.png' 'Square71x71Logo.png' 'Square150x150Logo.png' `
        'Square310x310Logo.png' 'Wide310x150Logo.png' 'SplashScreen.png' `
        -tile 4x2 -geometry '+10+10<' -background '#909090' $sheet
    if ($LASTEXITCODE -ne 0) { throw 'montage failed.' }
}
finally { Pop-Location }

$pngs = Get-ChildItem $assetsDir -Filter *.png
$over = @($pngs | Where-Object Length -gt $MaxAssetBytes)
Write-Host ''
Write-Host ("Assets: {0} files, {1:N1} MB, {2} over the {3}-byte cap" -f `
    $pngs.Count, (($pngs | Measure-Object Length -Sum).Sum / 1MB), $over.Count, $MaxAssetBytes) `
    -ForegroundColor Green
Write-Host "Contact sheet: $sheet" -ForegroundColor Green

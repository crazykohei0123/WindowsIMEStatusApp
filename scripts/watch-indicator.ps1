# watch-indicator.ps1
# Samples the IME mode indicator (あ / A) region, located via UI Automation
# exactly like Indicator.TryFindIndicatorRect, and reports each distinct
# observed glyph together with its mean-absolute-difference distances to the
# calibrated templates (%APPDATA%\ImeStatusOverlay\templates.json).
# Use the reported normal-group/unknown-group distances to tune
# Classifier.DefaultAcceptanceThreshold.
#
# Examples:
#   pwsh scripts/watch-indicator.ps1                          # 30s watch
#   pwsh scripts/watch-indicator.ps1 -DurationSec 60          # longer session
#   pwsh scripts/watch-indicator.ps1 -AcceptanceThreshold 15  # preview with another threshold
param(
    [int]$DurationSec = 30,
    [int]$IntervalMs = 200,
    [string]$TemplatesPath = (Join-Path $env:APPDATA 'ImeStatusOverlay\templates.json'),
    [double]$AcceptanceThreshold = 20.0  # Classifier.DefaultAcceptanceThreshold (preview only)
)

$ErrorActionPreference = 'Stop'

# --- DPI awareness so UIA rects and CopyFromScreen both use physical pixels. ---
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class DpiHelper {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
}
"@
foreach ($try in @(
    { [DpiHelper]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) },  # PerMonitorV2
    { [DpiHelper]::SetProcessDpiAwareness(2) -eq 0 },                   # PerMonitor (S_OK = 0)
    { [DpiHelper]::SetProcessDPIAware() }                                # System
)) { try { if (& $try) { break } } catch { } }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$ae = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]

# C#-style (int) cast: truncation toward zero, not rounding.
function Convert-ToCsInt([double]$v) {
    if ($v -ge 0) { [int][Math]::Floor($v) } else { -[int][Math]::Floor(-$v) }
}

# Port of Indicator.GlyphRect: the glyph is the child <Image>; use its bounds,
# else the centered square of the button.
function Get-GlyphRect([System.Windows.Automation.AutomationElement]$button) {
    $imgCond = New-Object System.Windows.Automation.PropertyCondition(
        $ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Image)
    $image = $button.FindFirst($scope::Children, $imgCond)
    if ($null -ne $image) {
        $r = $image.Current.BoundingRectangle
        if ($r.Width -le 0 -or $r.Height -le 0) { return $null }
        return [PSCustomObject]@{
            X = Convert-ToCsInt $r.X; Y = Convert-ToCsInt $r.Y
            W = Convert-ToCsInt $r.Width; H = Convert-ToCsInt $r.Height
        }
    }
    $r = $button.Current.BoundingRectangle
    $side = [int](Convert-ToCsInt ([Math]::Min($r.Width, $r.Height)) / 2)
    if ($side -le 0) { return $null }
    $x = Convert-ToCsInt ($r.X + ($r.Width - $side) / 2)
    $y = Convert-ToCsInt ($r.Y + ($r.Height - $side) / 2)
    return [PSCustomObject]@{ X = $x; Y = $y; W = $side; H = $side }
}

# Port of Indicator.FindInTaskbar / TryFindIndicatorRect.
function Find-IndicatorRegion {
    foreach ($cls in @('Shell_TrayWnd', 'Shell_SecondaryTrayWnd')) {
        $clsCond = New-Object System.Windows.Automation.PropertyCondition(
            $ae::ClassNameProperty, $cls)
        $taskbar = $ae::RootElement.FindFirst($scope::Children, $clsCond)
        if ($null -eq $taskbar) { continue }
        $btnCond = New-Object System.Windows.Automation.PropertyCondition(
            $ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
        $buttons = $taskbar.FindAll($scope::Descendants, $btnCond)
        $fallback = $null
        foreach ($b in $buttons) {
            $name = $b.Current.Name
            if ([string]::IsNullOrEmpty($name) -or
                -not $name.Contains('トレイ入力インジケーター')) { continue }
            if ($name.Contains('IME のオプション')) { return Get-GlyphRect $b }
            if ($null -eq $fallback) { $fallback = $b }
        }
        if ($null -ne $fallback) { return Get-GlyphRect $fallback }
    }
    return $null
}

# Grayscale capture with the exact formula of Indicator.CaptureGray
# (BT.601 luma, integer truncation), so distances match the app bit-for-bit.
function Get-Gray([int]$x, [int]$y, [int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    } finally { $g.Dispose() }
    $data = New-Object 'byte[]' ($w * $h)
    for ($yy = 0; $yy -lt $h; $yy++) {
        for ($xx = 0; $xx -lt $w; $xx++) {
            $p = $bmp.GetPixel($xx, $yy)
            $data[$yy * $w + $xx] = [byte][Math]::Floor(($p.R * 299 + $p.G * 587 + $p.B * 114) / 1000)
        }
    }
    $bmp.Dispose()
    return ,$data
}

# Port of Glyph.Signature: binarize against the corner background.
function Get-Signature([byte[]]$gray, [int]$w, [int]$h) {
    $bg = [Math]::Floor(($gray[0] + $gray[$w - 1] + $gray[($h - 1) * $w] + $gray[($h - 1) * $w + $w - 1]) / 4)
    $sb = New-Object System.Text.StringBuilder
    $ink = 0
    for ($i = 0; $i -lt $gray.Length; $i++) {
        if ([Math]::Abs($gray[$i] - $bg) -gt 40) { [void]$sb.Append('#'); $ink++ }
        else { [void]$sb.Append('.') }
    }
    return @($sb.ToString(), $ink)
}

# Port of Classifier.MeanAbsDiff.
function Get-MeanAbsDiff([byte[]]$a, [byte[]]$b) {
    $n = [Math]::Min($a.Length, $b.Length)
    if ($n -eq 0) { return [double]::MaxValue }
    [long]$sum = 0
    for ($i = 0; $i -lt $n; $i++) {
        $sum += [Math]::Abs([int]$a[$i] - [int]$b[$i])
    }
    return [double]$sum / $n
}

# --- Load templates (optional: without them the script only shows glyphs). ---
$hasTemplates = Test-Path $TemplatesPath
$offT = $null; $onT = $null; $tw = 0; $th = 0
if ($hasTemplates) {
    $json = Get-Content $TemplatesPath -Raw | ConvertFrom-Json
    $tw = [int]$json.W; $th = [int]$json.H
    $offT = [Convert]::FromBase64String($json.Off)
    $onT = [Convert]::FromBase64String($json.On)
} else {
    Write-Host "NOTE: templates not found ($TemplatesPath) - glyph-only mode."
    Write-Host "      Run the app's calibration first to measure distances."
}


# --- Locate the indicator and watch it. ---
$region = Find-IndicatorRegion
if ($null -eq $region) {
    Write-Host 'ERROR: IME indicator region not found in the taskbar.'
    exit 1
}
Write-Host ("Indicator region: X={0} Y={1} {2}x{3}" -f $region.X, $region.Y, $region.W, $region.H)
if ($hasTemplates -and ($region.W -ne $tw -or $region.H -ne $th)) {
    Write-Host ("WARNING: region size differs from template size {0}x{1} -" -f $tw, $th)
    Write-Host "         distances are not comparable. Re-calibrate the app first."
}
if ($hasTemplates) {
    Write-Host ("Template mutual distance MAD(A,あ) = {0:F2}" -f (Get-MeanAbsDiff $offT $onT))
}
Write-Host "Watching for $DurationSec sec - toggle IME ON/OFF several times NOW ..."
Write-Host "(hovering the indicator also captures its highlight state as a variant)"

$states = @{}   # signature -> @{ Count; Ink; Gray }
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$nextProgress = 5
while ($sw.Elapsed.TotalSeconds -lt $DurationSec) {
    $gray = Get-Gray $region.X $region.Y $region.W $region.H
    $res = Get-Signature $gray $region.W $region.H
    $sig = $res[0]
    if (-not $states.ContainsKey($sig)) {
        $states[$sig] = @{ Count = 0; Ink = $res[1]; Gray = $gray }
    }
    $states[$sig].Count++
    if ($sw.Elapsed.TotalSeconds -ge $nextProgress) {
        Write-Host ("  ... {0}s elapsed, {1} distinct patterns so far" -f [int]$sw.Elapsed.TotalSeconds, $states.Count)
        $nextProgress += 5
    }
    Start-Sleep -Milliseconds $IntervalMs
}

# --- Report. ---
$margin = 2.0   # Classifier.SeparationMargin
$patterns = foreach ($kv in $states.GetEnumerator()) {
    $dOff = $null; $dOn = $null; $best = $null; $class = '-'
    if ($hasTemplates) {
        $dOff = Get-MeanAbsDiff $kv.Value.Gray $offT
        $dOn = Get-MeanAbsDiff $kv.Value.Gray $onT
        $best = [Math]::Min($dOff, $dOn)
        if ($best -le $AcceptanceThreshold -and [Math]::Abs($dOff - $dOn) -gt $margin) {
            $class = if ($dOff -lt $dOn) { 'Off' } else { 'On' }
        } else {
            $class = 'Unknown'
        }
    }
    [PSCustomObject]@{
        Count = $kv.Value.Count; Ink = $kv.Value.Ink; Sig = $kv.Key
        DOff = $dOff; DOn = $dOn; Best = $best; Class = $class
    }
}
$patterns = $patterns | Sort-Object Count -Descending


Write-Host ''
Write-Host ("=== Observed {0} distinct pattern(s), acceptance threshold = {1} ===" -f $patterns.Count, $AcceptanceThreshold)
if ($hasTemplates) {
    '{0,4} {1,6} {2,5} {3,9} {4,9} {5,9} {6,9}' -f 'idx', 'seen', 'ink', 'dOff', 'dOn', 'best', 'class'
    $i = 0
    foreach ($p in $patterns) {
        $i++
        '{0,4} {1,6} {2,5} {3,9:F2} {4,9:F2} {5,9:F2} {6,9}' -f $i, $p.Count, $p.Ink, $p.DOff, $p.DOn, $p.Best, $p.Class
    }
} else {
    $i = 0
    foreach ($p in $patterns) {
        $i++
        '{0,4} {1,6} {2,5}' -f $i, $p.Count, $p.Ink
    }
}

Write-Host ''
$rank = 0
foreach ($p in ($patterns | Select-Object -First 6)) {
    $rank++
    $extra = if ($hasTemplates) { ", dOff={0:F2}, dOn={1:F2}, class={2}" -f $p.DOff, $p.DOn, $p.Class } else { '' }
    Write-Host ("--- pattern #{0}: seen {1}x, ink={2}{3} ---" -f $rank, $p.Count, $p.Ink, $extra)
    for ($y = 0; $y -lt $region.H; $y++) {
        Write-Host ($p.Sig.Substring($y * $region.W, $region.W))
    }
}

if (-not $hasTemplates) { exit 0 }

# --- Threshold guidance. ---
$normal = @($patterns | Where-Object { $_.Class -eq 'Off' -or $_.Class -eq 'On' })
$unknown = @($patterns | Where-Object { $_.Class -eq 'Unknown' })
Write-Host ''
Write-Host '=== Threshold guidance ==='
if ($normal.Count -gt 0) {
    $normalMax = ($normal | Measure-Object Best -Maximum).Maximum
    $normalMin = ($normal | Measure-Object Best -Minimum).Minimum
    Write-Host ("Normal group ({0} patterns): distance to own template min={1:F2} max={2:F2}" -f $normal.Count, $normalMin, $normalMax)
} else {
    $normalMax = 0
    Write-Host 'Normal group: NO あ/A patterns captured - toggle IME during the watch.'
}
if ($unknown.Count -gt 0) {
    $unknownMin = ($unknown | Measure-Object Best -Minimum).Minimum
    $unknownMax = ($unknown | Measure-Object Best -Maximum).Maximum
    Write-Host ("Unknown group ({0} patterns): best distance min={1:F2} max={2:F2}" -f $unknown.Count, $unknownMin, $unknownMax)
    if ($normal.Count -gt 0) {
        Write-Host ("Classifier.DefaultAcceptanceThreshold must satisfy: {0:F2} < T < {1:F2}" -f $normalMax, $unknownMin)
        $candidate = [Math]::Round($normalMax * 2, 1)
        if ($candidate -ge $unknownMin) { $candidate = [Math]::Round($unknownMin - 1, 1) }
        if ($candidate -le $normalMax) { $candidate = [Math]::Round($normalMax + 0.5, 1) }
        Write-Host ("Candidate (2x normal max, kept below unknown min): T = {0}" -f $candidate)
    }
} else {
    Write-Host 'Unknown group: none observed - also measure "×" / blank / other-icon states before tuning.'
    if ($normal.Count -gt 0) {
        Write-Host ("For reference: 2x normal max would be {0:F2}" -f ($normalMax * 2))
    }
}


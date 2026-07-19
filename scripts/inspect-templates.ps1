# inspect-templates.ps1
# Reads %APPDATA%\ImeStatusOverlay\templates.json and renders each template as
# ASCII art plus their mutual difference, to verify calibration captured
# distinct A and あ glyphs.

$path = Join-Path $env:APPDATA 'ImeStatusOverlay\templates.json'
if (-not (Test-Path $path)) { Write-Host "NOT FOUND: $path"; exit }

$json = Get-Content $path -Raw | ConvertFrom-Json
$w = $json.W; $h = $json.H
Write-Host "Template size: ${w}x${h}"

function Show-Template($b64, $label) {
    if (-not $b64) { Write-Host "$label : (none)"; return $null }
    $data = [Convert]::FromBase64String($b64)
    Write-Host "--- $label ---"
    # Determine background as corner average.
    $bg = ($data[0] + $data[$w-1] + $data[($h-1)*$w] + $data[($h-1)*$w + $w-1]) / 4.0
    for ($y = 0; $y -lt $h; $y++) {
        $line = ""
        for ($x = 0; $x -lt $w; $x++) {
            $v = $data[$y*$w + $x]
            if ([math]::Abs($v - $bg) -gt 40) { $line += '#' } else { $line += '.' }
        }
        Write-Host $line
    }
    return $data
}

$off = Show-Template $json.Off "OFF (A)"
Write-Host ""
$on  = Show-Template $json.On  "ON (あ)"
Write-Host ""

if ($off -and $on) {
    $sum = 0; for ($i = 0; $i -lt $off.Length; $i++) { $sum += [math]::Abs($off[$i] - $on[$i]) }
    $mad = $sum / $off.Length
    Write-Host ("MeanAbsDiff(A,あ) = {0:F2}  (larger = more distinct)" -f $mad)
    if ($mad -lt 5) { Write-Host "WARNING: templates are nearly identical - calibration likely captured the same glyph twice (wrong region?)" }
}
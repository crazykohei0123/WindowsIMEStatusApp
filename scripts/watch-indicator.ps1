# watch-indicator.ps1
# Samples the candidate indicator regions for a while and reports which one
# *changes* (the real mode indicator), showing the distinct glyphs observed.
param(
    [int]$DurationSec = 20,
    [int]$IntervalMs = 250
)
Add-Type -AssemblyName System.Drawing

$regions = @(
    @{ Label = 'R1'; X = 1653; Y = 1048; W = 16; H = 16 },
    @{ Label = 'R2'; X = 1691; Y = 1048; W = 16; H = 16 }
)

function Grab($x, $y, $w, $h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $g.Dispose()
    $data = New-Object byte[] ($w*$h)
    for ($i = 0; $i -lt $h; $i++) {
        for ($j = 0; $j -lt $w; $j++) {
            $c = $bmp.GetPixel($j, $i)
            $data[$i*$w + $j] = [byte](($c.R + $c.G + $c.B) / 3)
        }
    }
    $bmp.Dispose()
    return $data
}

function Signature($data, $w, $h) {
    # Binarize against corner background and join into a string key.
    $bg = ($data[0] + $data[$w-1] + $data[($h-1)*$w] + $data[($h-1)*$w+$w-1]) / 4.0
    $sb = New-Object System.Text.StringBuilder
    $ink = 0
    for ($i = 0; $i -lt $data.Length; $i++) {
        $d = [math]::Abs($data[$i] - $bg)
        if ($d -gt 40) { [void]$sb.Append('#'); $ink++ } else { [void]$sb.Append('.') }
    }
    return @($sb.ToString(), $ink)
}

function Render($sig, $w, $h) {
    for ($y = 0; $y -lt $h; $y++) { Write-Host ($sig.Substring($y*$w, $w)) }
}

foreach ($r in $regions) { $r['States'] = @{}; $r['Samples'] = @{}; $r['Ink'] = @{}; $r['Changes'] = 0; $r['Prev'] = '' }

Write-Host "Watching for $DurationSec sec - toggle IME now ..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $DurationSec) {
    foreach ($r in $regions) {
        $d = Grab $r.X $r.Y $r.W $r.H
        $res = Signature $d $r.W $r.H
        $sig = $res[0]; $ink = $res[1]
        if ($r.Prev -ne '' -and $sig -ne $r.Prev) { $r.Changes++ }
        $r.Prev = $sig
        if (-not $r.States.ContainsKey($sig)) { $r.States[$sig] = 0; $r.Samples[$sig] = $sig; $r.Ink[$sig] = $ink }
        $r.States[$sig]++
    }
    Start-Sleep -Milliseconds $IntervalMs
}

foreach ($r in $regions) {
    Write-Host ""
    Write-Host ("=== {0} @({1},{2}) : changes={3}, distinct={4} ===" -f $r.Label, $r.X, $r.Y, $r.Changes, $r.States.Count)
    $top = $r.States.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 3
    foreach ($kv in $top) {
        Write-Host ("--- seen {0}x, ink={1} ---" -f $kv.Value, $r.Ink[$kv.Key])
        Render $kv.Key $r.W $r.H
    }
}
# show-glyph.ps1
# Captures small screen regions and prints a binarized ASCII-art rendering so
# the glyph shape (あ vs A) can be recognized in plain text.
param(
    [int]$Threshold = 60
)
Add-Type -AssemblyName System.Drawing

function Show-Region($x, $y, $w, $h, $label) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $g.Dispose()

    # Background = corner pixel brightness (average of 4 corners).
    $corners = @($bmp.GetPixel(0,0), $bmp.GetPixel($w-1,0), $bmp.GetPixel(0,$h-1), $bmp.GetPixel($w-1,$h-1))
    $bg = 0; foreach ($c in $corners) { $bg += ($c.R + $c.G + $c.B) / 3 }; $bg /= 4

    Write-Host "--- $label @($x,$y ${w}x${h})  bg brightness=$([int]$bg) ---"
    for ($yy = 0; $yy -lt $h; $yy++) {
        $line = ""
        for ($xx = 0; $xx -lt $w; $xx++) {
            $p = $bmp.GetPixel($xx, $yy)
            $b = ($p.R + $p.G + $p.B) / 3
            if ([math]::Abs($b - $bg) -gt $Threshold) { $line += '#' } else { $line += '.' }
        }
        Write-Host $line
    }
    $bmp.Dispose()
    Write-Host ""
}

# Candidate indicator image regions from the UIA probe.
Show-Region 1653 1048 16 16 "Indicator#1 (right-click IME options)"
Show-Region 1691 1048 16 16 "Indicator#2 (Japanese)"
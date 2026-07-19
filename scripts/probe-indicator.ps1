# probe-indicator.ps1
# Locates the IME input indicator (あ / A) in the taskbar via UI Automation
# and prints candidate elements with bounding rectangles.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class W32 {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
}
'@

$ae = [System.Windows.Automation.AutomationElement]

function Dump($el, $depth, [ref]$out) {
    if ($null -eq $el -or $depth -gt 14) { return }
    try {
        $c = $el.Current
        $r = $c.BoundingRectangle
        $out.Value.Add([PSCustomObject]@{
            Depth = $depth
            Type = $c.ControlType.ProgrammaticName
            Name = $c.Name
            AId = $c.AutomationId
            Class = $c.ClassName
            X = [int]$r.X; Y = [int]$r.Y; W = [int]$r.Width; H = [int]$r.Height
        })
        $kids = $el.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($k in $kids) { Dump $k ($depth+1) $out }
    } catch { }
}

foreach ($cls in @('Shell_TrayWnd','Shell_SecondaryTrayWnd')) {
    $hwnd = [W32]::FindWindow($cls, $null)
    if ($hwnd -eq [IntPtr]::Zero) { Write-Host "$cls : not found"; continue }
    Write-Host "=== $cls (hwnd=$hwnd) ==="
    $el = $ae::FromHandle($hwnd)
    $list = New-Object System.Collections.Generic.List[object]
    Dump $el 0 ([ref]$list)
    # Candidates: has a name OR is small (icon-sized).
    $list | Where-Object {
        ($_.Name -ne '') -or ($_.W -gt 4 -and $_.W -le 64 -and $_.H -gt 4 -and $_.H -le 64)
    } | Format-Table Depth,Type,Name,AId,X,Y,W,H -AutoSize | Out-String -Width 260 | Write-Host
}
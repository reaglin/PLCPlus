# (Copied from LMS-2-Website; adds -Invoke.) Launch the app, capture its own window with PrintWindow (never a screen grab), close it.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Args = "",
    [int]$WaitMs = 3000,
    # Names of buttons to press first, through UI Automation (never synthetic input).
    [string[]]$Invoke = @(),
    # "Combo name=Item text": chooses an item in a drop-down first, through UI Automation.
    [string[]]$Select = @(),
    # Names of check boxes or toggle buttons to switch, through UI Automation, after -Select.
    [string[]]$Toggle = @(),
    # How long to let the window run after the clicks, before the picture.
    [int]$SettleMs = 300,
    # Steps run in order after -Select/-Toggle: "value:Name=Text" types into a box (then focus moves on, which
    # commits it), "press:Name" invokes a button. Through UI Automation only.
    [string[]]$Steps = @()
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Win {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    /// The biggest visible top-level window belonging to the process.
    public static IntPtr MainWindow(uint pid, out string title) {
        IntPtr best = IntPtr.Zero; int bestArea = 0; string bestTitle = "";
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != pid || !IsWindowVisible(h)) return true;
            RECT r; if (!GetWindowRect(h, out r)) return true;
            int area = (r.Right - r.Left) * (r.Bottom - r.Top);
            if (area <= bestArea) return true;
            var sb = new StringBuilder(500); GetWindowText(h, sb, 500);
            best = h; bestArea = area; bestTitle = sb.ToString();
            return true;
        }, IntPtr.Zero);
        title = bestTitle; return best;
    }
}
"@

$proc = if ($Args) { Start-Process -FilePath $Exe -ArgumentList $Args -PassThru } else { Start-Process -FilePath $Exe -PassThru }
Start-Sleep -Milliseconds $WaitMs

$title = ""
$h = [Win]::MainWindow([uint]$proc.Id, [ref]$title)
if ($h -eq [IntPtr]::Zero) {
    Write-Output "NO WINDOW (exited: $($proc.HasExited))"
    if (-not $proc.HasExited) { $proc.Kill() }
    exit 1
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
foreach ($pair in $Select) {
    $comboName, $itemText = $pair -split '=', 2
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $comboName)
    $combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $combo) { Write-Output "NOT FOUND combo '$comboName'"; continue }
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 300
    $itemCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $itemText)
    $item = $combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $itemCond)
    if ($item) { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Write-Output "chose '$itemText'" }
    else { Write-Output "NOT FOUND item '$itemText'" }
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    Start-Sleep -Milliseconds 500
}
foreach ($name in $Toggle) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($el) { $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Write-Output "toggled '$name'" }
    else { Write-Output "NOT FOUND toggle '$name'" }
    Start-Sleep -Milliseconds 300
}
if ($Invoke.Count -gt 0) {
    foreach ($name in $Invoke) {
        $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output "pressed '$name'" }
        else { Write-Output "NOT FOUND '$name'" }
        Start-Sleep -Milliseconds 700
    }
}
function Find-Named($name) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
foreach ($step in $Steps) {
    $kind, $rest = $step -split ':', 2
    if ($kind -eq 'value') {
        $name, $text = $rest -split '=', 2
        $el = Find-Named $name
        if (-not $el) { Write-Output "NOT FOUND box '$name'"; continue }
        $el.SetFocus()
        $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
        Start-Sleep -Milliseconds 200
        $away = Find-Named 'New rung'
        if ($away) { $away.SetFocus() }
        Write-Output "typed '$text' into '$name'"
    } elseif ($kind -eq 'press') {
        $el = Find-Named $rest
        if ($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output "pressed '$rest'" }
        else { Write-Output "NOT FOUND '$rest'" }
    }
    Start-Sleep -Milliseconds 600
}
Start-Sleep -Milliseconds $SettleMs
$r = New-Object Win+RECT
[Win]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$ht = $r.Bottom - $r.Top
Write-Output "window '$title' ${w}x${ht}"

$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win]::PrintWindow($h, $hdc, 2) | Out-Null   # 2 = PW_RENDERFULLCONTENT, needed for WPF
$g.ReleaseHdc($hdc)
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output "saved $Out"
if (-not $proc.HasExited) { $proc.Kill() }

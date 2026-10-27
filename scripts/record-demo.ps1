# Records the README's demo: the sample crashes, and at its next launch the window shows the report. It moves the real
# mouse and clicks, so it runs on a CI runner's own desktop (.github/workflows/demo.yml), never on someone's PC.
# Needs ffmpeg on PATH. Usage: pwsh scripts/record-demo.ps1 <the sample's executable> <out.gif>
param(
    [Parameter(Mandatory)][string]$Sample,
    [Parameter(Mandatory)][string]$Gif
)
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Pointer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X, Y; }

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    public static void Click()
    {
        mouse_event(0x2, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x4, 0, 0, 0, UIntPtr.Zero);
    }
}
'@

$auto = [System.Windows.Automation.AutomationElement]

# The named button in any of the process's windows, once it's on screen.
function Find-Button([int]$processId, [string]$name) {
    $ofProcess = [System.Windows.Automation.PropertyCondition]::new($auto::ProcessIdProperty, $processId)
    $button = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new($auto::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button),
        [System.Windows.Automation.PropertyCondition]::new($auto::NameProperty, $name))
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        foreach ($top in $auto::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $ofProcess)) {
            $found = $top.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $button)
            if ($found -and -not $found.Current.BoundingRectangle.IsEmpty) {
                # Avalonia lists a window it owns under its owner, so the button's own window is its nearest window.
                $window = $found
                while ($window.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
                    $window = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($window)
                }
                [Pointer]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) | Out-Null
                return @{ Element = $found; Window = $window }
            }
        }
        Start-Sleep -Milliseconds 200
    }
    throw "No '$name' button appeared."
}

# Eases the pointer to the middle of an element, as a hand would.
function Move-To($element) {
    $rect = $element.Current.BoundingRectangle
    $toX = $rect.X + $rect.Width / 2
    $toY = $rect.Y + $rect.Height / 2
    $from = [Pointer+Point]::new()
    [Pointer]::GetCursorPos([ref]$from) | Out-Null
    foreach ($step in 1..30) {
        $t = $step / 30
        $eased = $t * $t * (3 - 2 * $t)
        [Pointer]::SetCursorPos([int]($from.X + ($toX - $from.X) * $eased), [int]($from.Y + ($toY - $from.Y) * $eased)) | Out-Null
        Start-Sleep -Milliseconds 20
    }
}

Remove-Item -Recurse -Force (Join-Path $env:LOCALAPPDATA 'Oopsling Sample\crashes') -ErrorAction SilentlyContinue

# The runner's own console would fill the background, and the crash's text would land in it.
(New-Object -ComObject Shell.Application).MinimizeAll()
Start-Sleep -Seconds 1
$quiet = @{ RedirectStandardOutput = (Join-Path ([IO.Path]::GetTempPath()) 'sample-out.txt'); RedirectStandardError = (Join-Path ([IO.Path]::GetTempPath()) 'sample-err.txt') }
$clip = Join-Path ([IO.Path]::GetTempPath()) 'oopsling-demo.mkv'
$record = [System.Diagnostics.ProcessStartInfo]::new('ffmpeg', "-y -loglevel error -f gdigrab -framerate 20 -draw_mouse 1 -i desktop -c:v libx264rgb -crf 0 -preset ultrafast `"$clip`"")
$record.RedirectStandardInput = $true
$record.UseShellExecute = $false

# Recording starts once the first window has drawn, so the GIF doesn't open on an empty frame.
$first = Start-Process $Sample -PassThru @quiet
$crash = Find-Button $first.Id 'Crash'
Start-Sleep -Milliseconds 800
$ffmpeg = [System.Diagnostics.Process]::Start($record)

try {
    Start-Sleep -Milliseconds 1500
    Move-To $crash.Element
    Start-Sleep -Milliseconds 400
    [Pointer]::Click()
    if (-not $first.WaitForExit(15000)) { throw 'The sample didn''t crash.' }
    Start-Sleep -Milliseconds 1500

    $second = Start-Process $Sample -PassThru @quiet
    $report = Find-Button $second.Id 'Report on GitHub'
    Start-Sleep -Seconds 4
    Move-To $report.Element
    Start-Sleep -Seconds 2
    $frame = $report.Window.Current.BoundingRectangle
}
finally {
    $ffmpeg.StandardInput.Write('q')
    $ffmpeg.WaitForExit()
    Get-Process -Name 'Oopsling.Sample' -ErrorAction SilentlyContinue | Stop-Process -Force
}

# Cropped to the report window with a margin, which holds the main window too, since the report opens over it.
$pad = 32
$x = [Math]::Max(0, [int]$frame.X - $pad)
$y = [Math]::Max(0, [int]$frame.Y - $pad)
$crop = "crop=$([int]$frame.Width + 2 * $pad):$([int]$frame.Height + 2 * $pad):${x}:${y}"
& ffmpeg -y -loglevel error -i $clip -vf "$crop,fps=12,scale=760:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle" $Gif
if ($LASTEXITCODE -ne 0) { throw "ffmpeg exited with $LASTEXITCODE" }
Write-Output "Wrote $Gif"

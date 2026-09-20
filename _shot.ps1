param([string]$Out = "$env:TEMP\hvtx")

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
}
"@

New-Item -ItemType Directory -Force $Out | Out-Null
$env:__COMPAT_LAYER = 'RunAsInvoker'
$exe = 'Y:\HyperVToolsX\HyperVToolsX.App\bin\Debug\net10.0-windows10.0.17763.0\win-x64\HyperVToolsX.exe'
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 6

function Shot($name) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
    $bmp.Save("$Out\$name.png")
    $g.Dispose(); $bmp.Dispose()
}

if ($p.HasExited) { "process exited early: $($p.ExitCode)"; exit 1 }
Shot 'target-manager'

# close the modal Target Manager (Alt+F4) so the main window shows
[Win]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
[System.Windows.Forms.SendKeys]::SendWait('%{F4}')
Start-Sleep -Seconds 2
Shot 'main'

if (-not $p.HasExited) { Stop-Process $p -Force }
"done"

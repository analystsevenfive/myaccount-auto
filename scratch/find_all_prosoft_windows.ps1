Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinHelper {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

[WinHelper]::EnumWindows({
    param($hwnd, $lparam)
    $pid = 0
    [WinHelper]::GetWindowThreadProcessId($hwnd, [ref]$pid)
    $sbText = New-Object System.Text.StringBuilder 256
    [WinHelper]::GetWindowText($hwnd, $sbText, 256)
    $title = $sbText.ToString()
    
    $sbClass = New-Object System.Text.StringBuilder 256
    [WinHelper]::GetClassName($hwnd, $sbClass, 256)
    $cls = $sbClass.ToString()
    
    $vis = [WinHelper]::IsWindowVisible($hwnd)

    if ($title -or $cls -like "*FN*" -or $cls -like "*PB*" -or $pid -eq 9812) {
        $pName = (Get-Process -Id $pid -ErrorAction SilentlyContinue).ProcessName
        Write-Host ("HWND: 0x{0:X} | PID: {1} ({2}) | Vis: {3} | Class: {4} | Title: {5}" -f $hwnd.ToInt64(), $pid, $pName, $vis, $cls, $title)
    }
    return $true
}, [IntPtr]::Zero)

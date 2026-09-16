Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public class Win32Helper {
    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern int GetDlgCtrlID(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

[Win32Helper]::EnumChildWindows([IntPtr]0x90FE8, {
    param($hwnd, $lparam)
    $sbC = New-Object System.Text.StringBuilder 256
    [Win32Helper]::GetClassName($hwnd, $sbC, 256)
    $cls = $sbC.ToString()
    $sbT = New-Object System.Text.StringBuilder 256
    [Win32Helper]::GetWindowText($hwnd, $sbT, 256)
    $txt = $sbT.ToString()
    $rect = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($hwnd, [ref]$rect)
    $vis = [Win32Helper]::IsWindowVisible($hwnd)
    $id = [Win32Helper]::GetDlgCtrlID($hwnd)
    if ($rect.Right -gt $rect.Left -and $rect.Bottom -gt $rect.Top) {
        Write-Host "HWND: 0x$($hwnd.ToString('X')) | Id: $id | Vis: $vis | Class: '$cls' | Text: '$txt' | Rect: ($($rect.Left),$($rect.Top),$($rect.Right),$($rect.Bottom))"
    }
    return $true
}, [IntPtr]::Zero)

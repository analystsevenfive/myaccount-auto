Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class WinInspector3 {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWnd, EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Ansi)] public static extern IntPtr SendMessageA(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    public static string InspectAll(int pid) {
        var sb = new StringBuilder();
        EnumWindows((h, _) => {
            uint p = 0;
            GetWindowThreadProcessId(h, out p);
            if (p == (uint)pid) {
                var title = new StringBuilder(256);
                GetWindowText(h, title, 256);
                var cls = new StringBuilder(256);
                GetClassName(h, cls, 256);
                RECT r;
                GetWindowRect(h, out r);
                bool vis = IsWindowVisible(h);

                sb.AppendLine(string.Format("Top HWND=0x{0:X} Vis={1} Class='{2}' Title='{3}' Rect=({4},{5},{6},{7})", h.ToInt64(), vis, cls, title, r.Left, r.Top, r.Right, r.Bottom));

                if (vis && (r.Right - r.Left) > 100) {
                    EnumChildWindows(h, (ch, __) => {
                        var cCls = new StringBuilder(256);
                        GetClassName(ch, cCls, 256);
                        var cClsStr = cCls.ToString();
                        var id = GetDlgCtrlID(ch);
                        var cvis = IsWindowVisible(ch);
                        RECT cr;
                        GetWindowRect(ch, out cr);

                        if (cClsStr == "ComboBox" || cClsStr == "Edit" || cClsStr == "Button") {
                            var cTitle = new StringBuilder(256);
                            GetWindowText(ch, cTitle, 256);
                            sb.AppendLine(string.Format("  Child HWND=0x{0:X} Class='{1}' Id={2} Vis={3} Rect=({4},{5},{6},{7}) Text='{8}'", ch.ToInt64(), cClsStr, id, cvis, cr.Left, cr.Top, cr.Right, cr.Bottom, cTitle));

                            if (cClsStr == "ComboBox") {
                                int cnt = (int)SendMessage(ch, 0x0146, IntPtr.Zero, IntPtr.Zero);
                                int cur = (int)SendMessage(ch, 0x0147, IntPtr.Zero, IntPtr.Zero);
                                sb.AppendLine(string.Format("    --> ComboBox ItemsCount={0}, CurSel={1}", cnt, cur));
                                for (int i = 0; i < cnt; i++) {
                                    var itemSb = new StringBuilder(256);
                                    SendMessageA(ch, 0x0148, (IntPtr)i, itemSb);
                                    sb.AppendLine(string.Format("        [{0}]='{1}'", i, itemSb));
                                }
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }
            }
            return true;
        }, IntPtr.Zero);
        return sb.ToString();
    }
}
'@

$proc = Get-Process myaccount -ErrorAction SilentlyContinue
if ($proc) {
    [WinInspector3]::InspectAll($proc.Id)
} else {
    "myaccount not running"
}

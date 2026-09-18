using System;
using System.Runtime.InteropServices;
using System.Text;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProsoftAutoLogin.Tests")]

namespace ProsoftAutoLogin.Automation;

internal static class Win32Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    public const uint KLF_ACTIVATE = 1;

    public static void EnsureEnglishKeyboardLayout(IntPtr hWnd)
    {
        try
        {
            var englishHkl = LoadKeyboardLayout("00000409", KLF_ACTIVATE);
            ActivateKeyboardLayout(englishHkl, KLF_ACTIVATE);
            if (hWnd != IntPtr.Zero)
            {
                PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, englishHkl);
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[EnsureEnglishKeyboardLayout] Notice: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public const uint KEYEVENTF_KEYDOWN = 0x0000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public const byte VK_BACK = 0x08;
    public const byte VK_TAB = 0x09;
    public const byte VK_RETURN = 0x0D;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU = 0x12; // Alt key
    public const byte VK_SPACE = 0x20;
    public const byte VK_END = 0x23;
    public const byte VK_HOME = 0x24;
    public const byte VK_UP = 0x26;
    public const byte VK_DOWN = 0x28;
    public const byte VK_DELETE = 0x2E;
    public const byte VK_A = 0x41;
    public const byte VK_C = 0x43;
    public const byte VK_N = 0x4E;
    public const byte VK_S = 0x53;
    public const byte VK_V = 0x56;
    public const byte VK_F2 = 0x70;
    public const byte VK_F4 = 0x73;

    public static async Task SendKeyPressAsync(byte vk, CancellationToken cancellationToken = default)
    {
        try
        {
            keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            await Task.Delay(40, cancellationToken);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
    }

    public static async Task SendKeyCombinationAsync(byte modifierVk, byte keyVk, CancellationToken cancellationToken = default)
    {
        try
        {
            keybd_event(modifierVk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            await Task.Delay(30, cancellationToken);
            keybd_event(keyVk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            await Task.Delay(40, cancellationToken);
            keybd_event(keyVk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            await Task.Delay(30, cancellationToken);
            keybd_event(modifierVk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
    }

    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetDlgCtrlID(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetThreadDesktop(IntPtr hDesktop);

    public static void EnsureDefaultDesktop()
    {
        try
        {
            var hDesk = OpenDesktop("Default", 0, false, 0x10000000);
            if (hDesk != IntPtr.Zero)
            {
                SetThreadDesktop(hDesk);
            }
        }
        catch { }
    }

    public const int GWL_STYLE = -16;
    public const int ES_PASSWORD = 0x0020;

    public const int WM_SETTEXT = 0x000C;
    public const int WM_GETTEXT = 0x000D;
    public const int WM_GETTEXTLENGTH = 0x000E;
    public const int WM_COMMAND = 0x0111;
    public const uint WM_CHAR = 0x0102;
    public const uint WM_PASTE = 0x0302;
    public const uint CB_GETCOUNT = 0x0146;
    public const uint CB_GETCURSEL = 0x0147;
    public const uint CB_GETLBTEXT = 0x0148;
    public const uint CB_GETLBTEXTLEN = 0x0149;
    public const int CB_FINDSTRINGEXACT = 0x0158;
    public const int CB_FINDSTRING = 0x014C;
    public const int CB_SETCURSEL = 0x014E;
    public const int CB_SELECTSTRING = 0x014D;
    public const uint CB_SHOWDROPDOWN = 0x014F;
    public const int CBN_SELCHANGE = 1;

    public const uint LB_GETCOUNT = 0x018B;
    public const uint LB_GETCURSEL = 0x0188;
    public const uint LB_GETTEXT = 0x0189;
    public const uint LB_GETTEXTLEN = 0x018A;
    public const int LB_SETCURSEL = 0x0186;
    public const int LB_SELECTSTRING = 0x018C;
    public const uint EM_GETPASSWORDCHAR = 0x00D2;

    public const uint BM_CLICK = 0x00F5;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;

    public const uint TV_FIRST = 0x1100;
    public const uint TVM_GETCOUNT = TV_FIRST + 5;
    public const uint TVM_GETNEXTITEM = TV_FIRST + 10;
    public const uint TVM_EXPAND = TV_FIRST + 2;
    public const uint TVM_SELECTITEM = TV_FIRST + 11;
    public const uint TVE_EXPAND = 0x0002;
    public const uint TVGN_ROOT = 0x0000;
    public const uint TVGN_NEXT = 0x0001;
    public const uint TVGN_CHILD = 0x0004;

    public const uint TCM_FIRST = 0x1300;
    public const uint TCM_GETITEMCOUNT = TCM_FIRST + 4;
    public const uint TCM_GETITEM = TCM_FIRST + 5;
    public const uint TCM_SETCURSEL = TCM_FIRST + 12;
    public const uint TCM_GETCURSEL = TCM_FIRST + 11;
    public const uint TCM_SETCURFOCUS = TCM_FIRST + 48;
    public const uint TVGN_CARET = 0x0009;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    public static int GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return (int)GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        }
        return GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageA")]
    public static extern IntPtr SendMessageA(IntPtr hWnd, uint Msg, IntPtr wParam, byte[] lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageA")]
    public static extern IntPtr SendMessageA(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPStr)] string lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetComboBoxInfo(IntPtr hWnd, ref COMBOBOXINFO pcbi);

    public static string GetEditText(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return string.Empty;
        var sb = new StringBuilder(512);
        SendMessage(hWnd, (uint)WM_GETTEXT, (IntPtr)sb.Capacity, sb);
        return sb.ToString().Trim();
    }

    public static List<string> GetComboBoxItems(IntPtr hCombo)
    {
        var list = new List<string>();
        if (hCombo == IntPtr.Zero) return list;
        int count = (int)SendMessage(hCombo, CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
        for (int i = 0; i < count; i++)
        {
            int len = (int)SendMessage(hCombo, CB_GETLBTEXTLEN, (IntPtr)i, IntPtr.Zero);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 2);
                SendMessage(hCombo, CB_GETLBTEXT, (IntPtr)i, sb);
                list.Add(sb.ToString().Trim());
            }
            else
            {
                list.Add(string.Empty);
            }
        }
        return list;
    }

    public static string GetComboBoxCurrentText(IntPtr hCombo)
    {
        if (hCombo == IntPtr.Zero) return string.Empty;

        // 1. Try WM_GETTEXT on hCombo itself
        var text = GetEditText(hCombo);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        // 2. Try CB_GETCURSEL + CB_GETLBTEXT
        int curSel = (int)SendMessage(hCombo, CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
        if (curSel >= 0)
        {
            int len = (int)SendMessage(hCombo, CB_GETLBTEXTLEN, (IntPtr)curSel, IntPtr.Zero);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 2);
                SendMessage(hCombo, CB_GETLBTEXT, (IntPtr)curSel, sb);
                var lbText = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(lbText)) return lbText;
            }
        }

        // 3. Try COMBOBOXINFO hwndItem (edit control if CBS_DROPDOWN)
        try
        {
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (GetComboBoxInfo(hCombo, ref info))
            {
                if (info.hwndItem != IntPtr.Zero)
                {
                    var itemText = GetEditText(info.hwndItem);
                    if (!string.IsNullOrWhiteSpace(itemText)) return itemText;
                }
            }
        }
        catch { }

        // 4. Try GetWindowText
        return GetText(hCombo).Trim();
    }

    public static void SetClipboardTextSafe(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var thread = new Thread(() =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            System.Windows.Clipboard.SetDataObject(text, true);
                            return;
                        }
                        catch
                        {
                            Thread.Sleep(30);
                        }
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                if (thread.Join(1500))
                {
                    break;
                }
            }
            catch { }
        }
    }

    public static string? GetClipboardTextSafe()
    {
        string? text = null;
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        text = System.Windows.Clipboard.GetText();
                    }
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(1000);
        }
        catch { }
        return text;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetMenu(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll")]
    public static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

    [DllImport("user32.dll")]
    public static extern uint GetMenuItemID(IntPtr hMenu, int nPos);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);

    public const uint MF_BYPOSITION = 0x0400;

    public static bool TriggerMenuItem(IntPtr hWnd, IEnumerable<string> targetActionCandidates)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            var hMenu = GetMenu(hWnd);
            if (hMenu == IntPtr.Zero) return false;

            return SearchAndInvokeMenu(hWnd, hMenu, targetActionCandidates);
        }
        catch
        {
            return false;
        }
    }

    private static bool SearchAndInvokeMenu(IntPtr hWnd, IntPtr hMenu, IEnumerable<string> candidates)
    {
        var count = GetMenuItemCount(hMenu);
        for (int i = 0; i < count; i++)
        {
            var sb = new StringBuilder(256);
            GetMenuString(hMenu, (uint)i, sb, 256, MF_BYPOSITION);
            var text = sb.ToString().Trim();

            var subMenu = GetSubMenu(hMenu, i);
            if (subMenu != IntPtr.Zero)
            {
                if (SearchAndInvokeMenu(hWnd, subMenu, candidates))
                {
                    return true;
                }
            }
            else
            {
                var id = GetMenuItemID(hMenu, i);
                if (id != 0 && id != 0xFFFFFFFF)
                {
                    if (candidates.Any(c => !string.IsNullOrWhiteSpace(c) && text.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        SendMessage(hWnd, (uint)WM_COMMAND, (IntPtr)id, IntPtr.Zero);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public static bool IsChildOfComboBox(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        var parent = GetParent(hWnd);
        if (parent != IntPtr.Zero)
        {
            var parentClass = GetClass(parent);
            if (parentClass.Contains("Combo", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsPasswordEdit(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        if (IsChildOfComboBox(hWnd)) return false;

        var style = GetWindowLong(hWnd, GWL_STYLE);
        if ((style & ES_PASSWORD) != 0) return true;

        var pwChar = SendMessage(hWnd, EM_GETPASSWORDCHAR, IntPtr.Zero, IntPtr.Zero);
        if (pwChar != IntPtr.Zero) return true;

        return false;
    }

    public static bool SelectComboBoxItem(IntPtr hWnd, string text)
    {
        if (hWnd == IntPtr.Zero || string.IsNullOrWhiteSpace(text)) return false;

        var index = (int)SendMessage(hWnd, CB_FINDSTRINGEXACT, (IntPtr)(-1), text);
        if (index < 0)
        {
            index = (int)SendMessage(hWnd, CB_FINDSTRING, (IntPtr)(-1), text);
        }

        if (index >= 0)
        {
            SendMessage(hWnd, CB_SETCURSEL, (IntPtr)index, IntPtr.Zero);
            var parent = GetParent(hWnd);
            if (parent != IntPtr.Zero)
            {
                var ctrlId = GetDlgCtrlID(hWnd);
                var wParam = (IntPtr)((CBN_SELCHANGE << 16) | (ctrlId & 0xFFFF));
                SendMessage(parent, (uint)WM_COMMAND, wParam, hWnd);
            }
            return true;
        }

        var selIdx = (int)SendMessage(hWnd, CB_SELECTSTRING, (IntPtr)(-1), text);
        if (selIdx >= 0)
        {
            var parent = GetParent(hWnd);
            if (parent != IntPtr.Zero)
            {
                var ctrlId = GetDlgCtrlID(hWnd);
                var wParam = (IntPtr)((CBN_SELCHANGE << 16) | (ctrlId & 0xFFFF));
                SendMessage(parent, (uint)WM_COMMAND, wParam, hWnd);
            }
            return true;
        }

        return false;
    }

    public static void ClickAt(IntPtr hWnd, int x, int y)
    {
        var lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
        SendMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)1, lParam);
        SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
    }

    public static void DoubleClickAt(IntPtr hWnd, int x, int y)
    {
        var lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
        SendMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)1, lParam);
        SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        SendMessage(hWnd, WM_LBUTTONDBLCLK, (IntPtr)1, lParam);
        SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
    }

    public static string GetText(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hWnd, sb, 512);
        return sb.ToString();
    }

    public const int WS_VISIBLE = 0x10000000;
    public const int BN_CLICKED = 0;

    public static bool IsStyleVisible(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        return (GetWindowLong(hWnd, GWL_STYLE) & WS_VISIBLE) != 0;
    }

    public sealed record ChildButtonInfo(IntPtr Hwnd, string Text, string ClassName, RECT Rect, int CtrlId);

    public static List<ChildButtonInfo> GetChildButtons(IntPtr parentHwnd)
    {
        var buttons = new List<ChildButtonInfo>();
        if (parentHwnd == IntPtr.Zero) return buttons;

        EnumChildWindows(parentHwnd, (childHwnd, _) =>
        {
            var className = GetClass(childHwnd);
            var text = GetText(childHwnd);
            var isButtonClass = className.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                                className.StartsWith("FN", StringComparison.OrdinalIgnoreCase);

            if (isButtonClass)
            {
                GetWindowRect(childHwnd, out var rect);
                var ctrlId = GetDlgCtrlID(childHwnd);
                buttons.Add(new ChildButtonInfo(childHwnd, text, className, rect, ctrlId));
            }
            return true;
        }, IntPtr.Zero);

        return buttons;
    }

    public static IntPtr FindOkButtonHwnd(IntPtr parentHwnd)
    {
        if (parentHwnd == IntPtr.Zero) return IntPtr.Zero;

        var buttons = GetChildButtons(parentHwnd);
        if (buttons.Count == 0) return IntPtr.Zero;

        // 0. Check control ID 1038 (Prosoft OK button ID)
        var id1038 = buttons.FirstOrDefault(b => b.CtrlId == 1038);
        if (id1038 is not null) return id1038.Hwnd;

        // 1. Check direct text matches: "OK", "&OK", "ตกลง"
        var textMatch = buttons.FirstOrDefault(b =>
        {
            var clean = b.Text.Replace("&", string.Empty).Trim();
            return clean.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("ตกลง", StringComparison.OrdinalIgnoreCase);
        });
        if (textMatch is not null) return textMatch.Hwnd;

        // 2. Check standard IDOK = 1
        var idOkMatch = buttons.FirstOrDefault(b => b.CtrlId == 1);
        if (idOkMatch is not null) return idOkMatch.Hwnd;

        // 3. Look for Cancel button; the OK button is typically positioned to its left
        var cancelBtn = buttons.FirstOrDefault(b =>
        {
            var clean = b.Text.Replace("&", string.Empty).Trim();
            return clean.Equals("Cancel", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("ยกเลิก", StringComparison.OrdinalIgnoreCase);
        });

        if (cancelBtn is not null)
        {
            var leftNeighbor = buttons
                .Where(b => b.Hwnd != cancelBtn.Hwnd &&
                            Math.Abs(b.Rect.Top - cancelBtn.Rect.Top) <= 25 &&
                            b.Rect.Left < cancelBtn.Rect.Left &&
                            (b.Rect.Right - b.Rect.Left) > 30)
                .OrderByDescending(b => b.Rect.Left)
                .FirstOrDefault();

            if (leftNeighbor is not null)
            {
                return leftNeighbor.Hwnd;
            }
        }

        return IntPtr.Zero;
    }

    public static void ClickButtonHwnd(IntPtr buttonHwnd, IntPtr parentHwnd)
    {
        if (buttonHwnd == IntPtr.Zero) return;

        try
        {
            SetFocus(buttonHwnd);
        }
        catch { }

        try
        {
            SendMessage(buttonHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }

        try
        {
            PostMessage(buttonHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }

        try
        {
            SendMessage(buttonHwnd, WM_KEYDOWN, (IntPtr)0x20, IntPtr.Zero); // VK_SPACE
            SendMessage(buttonHwnd, WM_KEYUP, (IntPtr)0x20, IntPtr.Zero);
        }
        catch { }

        if (parentHwnd != IntPtr.Zero)
        {
            try
            {
                var ctrlId = GetDlgCtrlID(buttonHwnd);
                var wParam = (IntPtr)((BN_CLICKED << 16) | (ctrlId & 0xFFFF));
                SendMessage(parentHwnd, (uint)WM_COMMAND, wParam, buttonHwnd);
                PostMessage(parentHwnd, (uint)WM_COMMAND, wParam, buttonHwnd);
            }
            catch { }
        }

        try
        {
            GetWindowRect(buttonHwnd, out var rect);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width > 0 && height > 0)
            {
                ClickAt(buttonHwnd, width / 2, height / 2);
            }
        }
        catch { }
    }

    public static IntPtr FindOkButtonByLayout(IntPtr parentHwnd, RECT pwRect, int profileTop)
    {
        if (parentHwnd == IntPtr.Zero) return IntPtr.Zero;

        var allButtons = GetChildButtons(parentHwnd);

        // Buttons must be strictly between pwRect.Bottom and profileTop (strictly above profile)
        var inBand = allButtons.Where(b =>
            b.Rect.Top >= pwRect.Bottom - 5 &&
            b.Rect.Bottom <= profileTop - 2 &&
            b.Rect.Left >= pwRect.Left - 15 &&
            (b.Rect.Right - b.Rect.Left) >= 45 &&
            (b.Rect.Right - b.Rect.Left) <= 120 &&
            (b.Rect.Bottom - b.Rect.Top) >= 18 &&
            (b.Rect.Bottom - b.Rect.Top) <= 35)
            .ToList();

        // Filter out known non-OK texts
        inBand = inBand.Where(b =>
        {
            var clean = b.Text.Replace("&", string.Empty).Trim();
            return !clean.Contains("Cancel", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("ยกเลิก", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Profile", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Server", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Register", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Delete", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Demo", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("New", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("User", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Database", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("DBMS", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Login", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("Admin", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Contains("picture", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Equals(">>", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Equals("<<", StringComparison.OrdinalIgnoreCase) &&
                   !clean.Equals("...", StringComparison.OrdinalIgnoreCase);
        })
        .OrderBy(b => b.Rect.Left)
        .ToList();

        FileLogger.Log($"[Win32 FindOkButtonByLayout] Found {inBand.Count} button(s) in OK/Cancel zone: " +
            string.Join(", ", inBand.Select(b => $"HWND=0x{b.Hwnd.ToInt64():X} (Id={b.CtrlId}, Rect=({b.Rect.Left},{b.Rect.Top},{b.Rect.Right},{b.Rect.Bottom}), Text='{b.Text}')")));

        if (inBand.Count > 0)
        {
            // Exact ID 1038
            var exactId = inBand.FirstOrDefault(b => b.CtrlId == 1038);
            if (exactId != null) return exactId.Hwnd;

            var okText = inBand.FirstOrDefault(b =>
            {
                var clean = b.Text.Replace("&", string.Empty).Trim();
                return clean.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                       clean.Equals("ตกลง", StringComparison.OrdinalIgnoreCase);
            });
            if (okText != null) return okText.Hwnd;

            var aligned = inBand.FirstOrDefault(b => Math.Abs(b.Rect.Left - pwRect.Left) <= 15);
            if (aligned != null) return aligned.Hwnd;

            return inBand[0].Hwnd;
        }

        return IntPtr.Zero;
    }

    public static void ClickScreenPoint(int x, int y)
    {
        try
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        catch { }
    }

    public static async Task ClickScreenPointAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        try
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(50, cancellationToken);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        catch { }
    }

    public static async Task DoubleClickScreenPointAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        try
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(40, cancellationToken);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(60, cancellationToken);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(40, cancellationToken);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        catch { }
    }

    public static string GetClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, 256);
        return sb.ToString();
    }
}

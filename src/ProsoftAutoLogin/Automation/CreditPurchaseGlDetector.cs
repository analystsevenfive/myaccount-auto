using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ProsoftAutoLogin.Automation;

public enum GlColumn
{
    No,
    AccountCode,
    AccountName,
    Department,
    Debit,
    Credit,
    Job
}

internal static class CreditPurchaseGlDetector
{
    public const int GlTabIndex = 4;

    // Relative vertical offsets from childRect.Top on standard 789x479 Credit Purchase sheet
    public const int TopControlsYOffset = 185;
    public const int FirstRowCenterOffset = 227;
    public const int RowPitch = 17;

    // Horizontal offsets from childRect.Left
    public const int EditGlCheckboxXOffset = 182;
    public const int PostSearchButtonXOffset = 600;
    public const int NewButtonXOffset = 32;
    public const int SaveButtonXOffset = 85;

    // Column horizontal boundaries and centers (offsets from childRect.Left)
    public const int DepartmentColumnLeftOffset = 324;
    public const int DepartmentColumnRightOffset = 424;
    public const int DepartmentColumnCenterOffset = 374;
    public const int DepartmentDropdownArrowXOffset = 416;

    // Relative offsets from detected green arrow button [ > ]
    public const int GreenArrowToDeptDropdownDx = -198;
    public const int GreenArrowToDeptDropdownDy = 42;
    public const int GreenArrowToDeptCellDx = -260;

    /// <summary>
    /// Gets the click point for the "GL" tab at the bottom of the "ซื้อเชื่อ" window.
    /// GL is the 5th tab (index 4) located at:
    /// X = childRect.Left + 300, Y = childRect.Bottom - 52 (or tabRect.Bottom - 14).
    /// </summary>
    public static Point GetDefaultGlTabLocation(Win32Native.RECT childRect)
    {
        return new Point(childRect.Left + 300, childRect.Bottom - 52);
    }

    /// <summary>
    /// Gets the click point for the green search/generate button [ > ] next to "รูปแบบการ Post".
    /// </summary>
    public static Point GetDefaultPostSearchButtonLocation(Win32Native.RECT childRect)
    {
        return new Point(childRect.Left + PostSearchButtonXOffset, childRect.Top + TopControlsYOffset);
    }

    /// <summary>
    /// Gets the click point for the "แก้ไข GL" checkbox.
    /// </summary>
    public static Point GetDefaultEditGlCheckboxLocation(Win32Native.RECT childRect)
    {
        return new Point(childRect.Left + EditGlCheckboxXOffset, childRect.Top + TopControlsYOffset);
    }

    /// <summary>
    /// Gets the center point for a specific column and row in the GL DataWindow table.
    /// rowIndex is 1-based (1 for Row 1, 2 for Row 2, etc.)
    /// </summary>
    public static Point GetCellLocation(Win32Native.RECT childRect, int rowIndex, GlColumn column)
    {
        if (rowIndex < 1) rowIndex = 1;
        int rowY = childRect.Top + FirstRowCenterOffset + (rowIndex - 1) * RowPitch;

        int colX = column switch
        {
            GlColumn.No => childRect.Left + 43,
            GlColumn.AccountCode => childRect.Left + 103,
            GlColumn.AccountName => childRect.Left + 242,
            GlColumn.Department => childRect.Left + DepartmentColumnCenterOffset,
            GlColumn.Debit => childRect.Left + 491,
            GlColumn.Credit => childRect.Left + 613,
            GlColumn.Job => childRect.Left + 718,
            _ => childRect.Left + DepartmentColumnCenterOffset
        };

        return new Point(colX, rowY);
    }

    /// <summary>
    /// Gets the click point for the dropdown arrow [ v ] in column "แผนก" (Department).
    /// </summary>
    public static Point GetDepartmentDropdownLocation(Win32Native.RECT childRect, int rowIndex)
    {
        if (rowIndex < 1) rowIndex = 1;
        int rowY = childRect.Top + FirstRowCenterOffset + (rowIndex - 1) * RowPitch;
        return new Point(childRect.Left + DepartmentDropdownArrowXOffset, rowY);
    }

    /// <summary>
    /// Gets the click point for the dropdown arrow [ v ] in column "แผนก" anchored to the detected green arrow button.
    /// </summary>
    public static Point GetDepartmentDropdownLocationFromGreenArrow(Point greenArrow, int rowIndex)
    {
        if (rowIndex < 1) rowIndex = 1;
        int rowY = greenArrow.Y + GreenArrowToDeptDropdownDy + (rowIndex - 1) * RowPitch;
        return new Point(greenArrow.X + GreenArrowToDeptDropdownDx, rowY);
    }

    /// <summary>
    /// Gets the click point for the text box part in column "แผนก" anchored to the detected green arrow button.
    /// </summary>
    public static Point GetDepartmentCellLocationFromGreenArrow(Point greenArrow, int rowIndex)
    {
        if (rowIndex < 1) rowIndex = 1;
        int rowY = greenArrow.Y + GreenArrowToDeptDropdownDy + (rowIndex - 1) * RowPitch;
        return new Point(greenArrow.X + GreenArrowToDeptCellDx, rowY);
    }

    /// <summary>
    /// Gets the location of the "New" button on the bottom toolbar.
    /// </summary>
    public static Point GetDefaultNewButtonLocation(Win32Native.RECT childRect)
    {
        return new Point(childRect.Left + NewButtonXOffset, childRect.Bottom - 20);
    }

    /// <summary>
    /// Gets the location of the "Save" button on the bottom toolbar.
    /// </summary>
    public static Point GetDefaultSaveButtonLocation(Win32Native.RECT childRect)
    {
        return new Point(childRect.Left + SaveButtonXOffset, childRect.Bottom - 20);
    }

    /// <summary>
    /// Scans a bitmap of the top GL controls area to locate the green arrow [ > ] button.
    /// </summary>
    public static Point? FindGreenArrowButtonInBitmap(
        Bitmap bmp,
        int screenOffsetX = 0,
        int screenOffsetY = 0)
    {
        if (bmp == null) return null;

        int width = bmp.Width;
        int height = bmp.Height;

        int greenSumX = 0;
        int greenSumY = 0;
        int greenCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = bmp.GetPixel(x, y);
                // Bright green color of the [ > ] arrow
                if (p.G > 150 && p.G > p.R + 35 && p.G > p.B + 35)
                {
                    greenSumX += x;
                    greenSumY += y;
                    greenCount++;
                }
            }
        }

        if (greenCount >= 10)
        {
            int avgX = greenSumX / greenCount;
            int avgY = greenSumY / greenCount;
            return new Point(screenOffsetX + avgX, screenOffsetY + avgY);
        }

        return null;
    }

    /// <summary>
    /// Checks whether the "แก้ไข GL" checkbox appears to be checked by inspecting interior pixels.
    /// In unchecked state, the 13x13 box is pure white. In checked state, it has a black checkmark.
    /// </summary>
    public static bool IsCheckboxCheckedInBitmap(Bitmap bmp, int relativeBoxCenterX, int relativeBoxCenterY)
    {
        if (bmp == null) return false;

        int darkPixels = 0;
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                int px = relativeBoxCenterX + dx;
                int py = relativeBoxCenterY + dy;
                if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                {
                    var p = bmp.GetPixel(px, py);
                    if (p.R < 60 && p.G < 60 && p.B < 60)
                    {
                        darkPixels++;
                    }
                }
            }
        }

        return darkPixels >= 5;
    }

    /// <summary>
    /// Determines whether the popup message represents a confirmation prompt (such as "ข้ามเลขที่... ต้องการบันทึกหรือไม่ ?")
    /// rather than a blocking error.
    /// </summary>
    public static bool IsSaveConfirmationPrompt(string message, string title)
    {
        var combined = (title + " " + message).Trim();
        return combined.Contains("ต้องการบันทึกหรือไม่", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("บันทึกหรือไม่", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("ข้ามเลขที่", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("เลขที่เอกสารข้าม", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("ยืนยันการบันทึก", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("ยืนยัน", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the popup message asks about advance payment / deposit deduction:
    /// e.g. "ท่านมีจำนวนเงินตัดมัดจำต้องการใช้หรือไม่"
    /// When this prompt appears, user requirement specifies clicking "No" (IDNO = 7).
    /// </summary>
    public static bool IsDepositDeductionPrompt(string message, string title)
    {
        var combined = (title + " " + message).Trim();
        return combined.Contains("ตัดมัดจำ", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("เงินตัดมัดจำ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the popup message is a blocking error/warning dialog.
    /// Returns false if it is a confirmation prompt or deposit deduction prompt that can be answered.
    /// </summary>
    public static bool IsSaveWarningOrError(string title, string message)
    {
        if (IsSaveConfirmationPrompt(message, title)) return false;
        if (IsDepositDeductionPrompt(message, title)) return false;

        return title.Contains("คำเตือน", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("ข้อผิดพลาด", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("ข้อผิดพลาด", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Selects the best button from a list of child buttons.
    /// When isConfirmPrompt is true, it prioritizes Yes (IDYES=6 or text "Yes"/"ใช่") or the left button.
    /// When isConfirmPrompt is false, it prioritizes OK (IDOK=1 or text "OK"/"ตกลง").
    /// </summary>
    public static Win32Native.ChildButtonInfo? SelectDialogButton(
        IReadOnlyList<Win32Native.ChildButtonInfo> buttons,
        bool isConfirmPrompt)
    {
        if (buttons == null || buttons.Count == 0) return null;

        if (isConfirmPrompt)
        {
            // 1. Look for explicit Yes button (CtrlId == 6 / IDYES or text containing Yes/ใช่)
            var yesBtn = buttons.FirstOrDefault(b =>
                b.CtrlId == 6 ||
                b.Text.Trim().Trim('&').Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("Yes", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("ใช่", StringComparison.OrdinalIgnoreCase));
            if (yesBtn != null) return yesBtn;

            // 2. Standard MessageBox with Yes/No has 2 buttons: Yes on the left, No on the right
            if (buttons.Count == 2)
            {
                return buttons.OrderBy(b => b.Rect.Left).First();
            }

            // 3. Fallback to OK
            var okBtn = buttons.FirstOrDefault(b =>
                b.CtrlId == 1 ||
                b.Text.Trim().Trim('&').Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("OK", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("ตกลง", StringComparison.OrdinalIgnoreCase));
            if (okBtn != null) return okBtn;
        }
        else
        {
            // Standard notification/OK dialog:
            var okBtn = buttons.FirstOrDefault(b =>
                b.CtrlId == 1 ||
                b.Text.Trim().Trim('&').Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("OK", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("ตกลง", StringComparison.OrdinalIgnoreCase));
            if (okBtn != null) return okBtn;

            var yesBtn = buttons.FirstOrDefault(b =>
                b.CtrlId == 6 ||
                b.Text.Contains("Yes", StringComparison.OrdinalIgnoreCase) ||
                b.Text.Contains("ใช่", StringComparison.OrdinalIgnoreCase));
            if (yesBtn != null) return yesBtn;
        }

        if (buttons.Count == 1) return buttons[0];
        return buttons.OrderBy(b => b.Rect.Left).First();
    }

    /// <summary>
    /// Finds the HWND of the "No" button on a confirmation/prompt dialog.
    /// Prioritizes IDNO (7) or text "No" / "ไม่ใช่". On 2-button dialogs, No is typically on the right.
    /// </summary>
    public static IntPtr FindNoButtonHwnd(IntPtr dialogHwnd)
    {
        if (dialogHwnd == IntPtr.Zero) return IntPtr.Zero;
        var buttons = Win32Native.GetChildButtons(dialogHwnd);
        foreach (var b in buttons)
        {
            FileLogger.Log($"[FindNoButton] HWND=0x{b.Hwnd.ToInt64():X} Text='{b.Text}' Id={b.CtrlId} Rect=({b.Rect.Left},{b.Rect.Top},{b.Rect.Right},{b.Rect.Bottom})");
        }

        var noBtn = buttons.FirstOrDefault(b =>
            b.CtrlId == 7 ||
            b.Text.Trim().Trim('&').Equals("No", StringComparison.OrdinalIgnoreCase) ||
            b.Text.Contains("No", StringComparison.OrdinalIgnoreCase) ||
            b.Text.Contains("ไม่ใช่", StringComparison.OrdinalIgnoreCase));
        if (noBtn != null)
        {
            FileLogger.Log($"[FindNoButton] Chosen No button: HWND=0x{noBtn.Hwnd.ToInt64():X} Text='{noBtn.Text}' Id={noBtn.CtrlId}");
            return noBtn.Hwnd;
        }

        if (buttons.Count == 2)
        {
            var rightBtn = buttons.OrderBy(b => b.Rect.Left).Last();
            FileLogger.Log($"[FindNoButton] Selected right button as No: HWND=0x{rightBtn.Hwnd.ToInt64():X} Text='{rightBtn.Text}' Id={rightBtn.CtrlId}");
            return rightBtn.Hwnd;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Finds the HWND of the button to click on a save/confirmation dialog.
    /// </summary>
    public static IntPtr FindDialogButtonHwnd(IntPtr dialogHwnd, bool isConfirmPrompt)
    {
        if (dialogHwnd == IntPtr.Zero) return IntPtr.Zero;
        var buttons = Win32Native.GetChildButtons(dialogHwnd);
        foreach (var b in buttons)
        {
            FileLogger.Log($"[FindDialogButton] HWND=0x{b.Hwnd.ToInt64():X} Text='{b.Text}' Id={b.CtrlId} Rect=({b.Rect.Left},{b.Rect.Top},{b.Rect.Right},{b.Rect.Bottom})");
        }
        var chosen = SelectDialogButton(buttons, isConfirmPrompt);
        if (chosen != null)
        {
            FileLogger.Log($"[FindDialogButton] Chosen: HWND=0x{chosen.Hwnd.ToInt64():X} Text='{chosen.Text}' Id={chosen.CtrlId}");
        }
        return chosen?.Hwnd ?? IntPtr.Zero;
    }
}

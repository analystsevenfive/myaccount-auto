using System;
using System.Drawing;

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
    public const int SaveButtonXOffset = 85;

    // Column horizontal boundaries and centers (offsets from childRect.Left)
    public const int DepartmentColumnLeftOffset = 324;
    public const int DepartmentColumnRightOffset = 424;
    public const int DepartmentColumnCenterOffset = 374;
    public const int DepartmentDropdownArrowXOffset = 416;

    // Relative offsets from detected green arrow button [ > ]
    public const int GreenArrowToDeptDropdownDx = -186;
    public const int GreenArrowToDeptDropdownDy = 42;
    public const int GreenArrowToDeptCellDx = -235;

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
}

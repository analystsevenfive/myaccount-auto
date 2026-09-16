using System.Drawing;

namespace ProsoftAutoLogin.Automation;

public enum DetailColumn
{
    ItemCode,
    ItemCodeDropdown,
    ItemName,
    Warehouse,
    Location,
    Unit,
    Quantity,
    UnitPrice,
    Discount,
    Amount
}

internal static class CreditPurchaseDetailDetector
{
    public const int HeaderBottomOffset = 181;
    public const int FirstRowCenterOffset = 187;
    public const int RowPitch = 16;

    /// <summary>
    /// Gets the click point for the "Detail" tab at the bottom of the "ซื้อเชื่อ" window.
    /// On standard 789x479 sheet, the Detail tab icon/text is located at:
    /// X = childRect.Left + 45, Y = childRect.Bottom - 20 (or tabRect.Bottom - 14).
    /// </summary>
    public static Point GetDefaultDetailTabLocation(Win32Native.RECT childRect)
    {
        return new Point(childRect.Left + 45, childRect.Bottom - 20);
    }

    /// <summary>
    /// Gets the center point for a specific column and row in the Detail DataWindow table.
    /// rowIndex is 1-based (1 for Row 1, 2 for Row 2, etc.)
    /// </summary>
    public static Point GetCellLocation(Win32Native.RECT childRect, int rowIndex, DetailColumn column)
    {
        if (rowIndex < 1) rowIndex = 1;
        int rowY = childRect.Top + FirstRowCenterOffset + (rowIndex - 1) * RowPitch;

        int colX = column switch
        {
            DetailColumn.ItemCode => childRect.Left + 70,
            DetailColumn.ItemCodeDropdown => childRect.Left + 112,
            DetailColumn.ItemName => childRect.Left + 210,
            DetailColumn.Warehouse => childRect.Left + 322,
            DetailColumn.Location => childRect.Left + 371,
            DetailColumn.Unit => childRect.Left + 420,
            DetailColumn.Quantity => childRect.Left + 484,
            DetailColumn.UnitPrice => childRect.Left + 560,
            DetailColumn.Discount => childRect.Left + 626,
            DetailColumn.Amount => childRect.Left + 702,
            _ => childRect.Left + 70
        };

        return new Point(colX, rowY);
    }
}

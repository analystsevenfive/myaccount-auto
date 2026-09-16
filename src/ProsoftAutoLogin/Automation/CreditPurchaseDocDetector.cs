using System;
using System.Drawing;

namespace ProsoftAutoLogin.Automation;

internal static class CreditPurchaseDocDetector
{
    public sealed record DocFieldLocations(
        Point DocNumber,
        Point TaxInvoice,
        Point DeliveryOrder);

    /// <summary>
    /// Calculates default proportional coordinates for Document No, Tax Invoice No, and Delivery Order No
    /// on the Credit Purchase ("ซื้อเชื่อ") window.
    /// Based on verified measurements on the 789x479 sheet:
    /// - เลขที่เอกสาร: childRect.Left + 510, childRect.Top + 72
    /// - เลขที่ใบกำกับ: childRect.Left + 520, childRect.Top + 91
    /// - เลขที่ใบส่งของ: childRect.Left + 520, childRect.Top + 110
    /// </summary>
    public static DocFieldLocations GetDefaultDocFieldLocations(Win32Native.RECT childRect)
    {
        var docNum = new Point(childRect.Left + 510, childRect.Top + 72);
        var taxInv = new Point(childRect.Left + 520, childRect.Top + 91);
        var delOrd = new Point(childRect.Left + 520, childRect.Top + 110);
        return new DocFieldLocations(docNum, taxInv, delOrd);
    }

    /// <summary>
    /// Attempts to visually detect the 3 edit boxes in a cropped bitmap of the header area.
    /// Searches for horizontal dark border lines of width 95-135px with white interiors.
    /// </summary>
    public static DocFieldLocations? FindDocFieldsInBitmap(
        Bitmap headerBmp,
        int screenOffsetX = 0,
        int screenOffsetY = 0)
    {
        if (headerBmp == null) return null;

        int width = headerBmp.Width;
        int height = headerBmp.Height;

        // Find candidate edit boxes
        var foundBoxes = new System.Collections.Generic.List<(int X, int Y, int W, int H)>();

        for (int y = 5; y < height - 20; y++)
        {
            for (int x = 10; x < width - 110; x++)
            {
                var p = headerBmp.GetPixel(x, y);
                if (p.R < 50 && p.G < 50 && p.B < 50)
                {
                    int hLine = 0;
                    for (int hx = x; hx < Math.Min(x + 140, width); hx++)
                    {
                        var hp = headerBmp.GetPixel(hx, y);
                        if (hp.R < 60 && hp.G < 60 && hp.B < 60) hLine++;
                        else break;
                    }

                    if (hLine >= 95 && hLine <= 140)
                    {
                        int vLine = 0;
                        for (int vy = y; vy < Math.Min(y + 25, height); vy++)
                        {
                            var vp = headerBmp.GetPixel(x, vy);
                            if (vp.R < 60 && vp.G < 60 && vp.B < 60) vLine++;
                            else break;
                        }

                        if (vLine >= 14 && vLine <= 24)
                        {
                            // Verify white inside
                            int midX = x + hLine / 2;
                            int midY = y + vLine / 2;
                            if (midX < width && midY < height)
                            {
                                var inside = headerBmp.GetPixel(midX, midY);
                                if (inside.R > 220 && inside.G > 220 && inside.B > 220)
                                {
                                    // Check if not already in list
                                    if (!foundBoxes.Exists(b => Math.Abs(b.Y - y) < 10))
                                    {
                                        foundBoxes.Add((x, y, hLine, vLine));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // We expect at least 3 vertically stacked boxes separated by ~18-22px
        foundBoxes.Sort((a, b) => a.Y.CompareTo(b.Y));
        if (foundBoxes.Count >= 3)
        {
            var b1 = foundBoxes[0];
            var b2 = foundBoxes[1];
            var b3 = foundBoxes[2];

            if (b2.Y - b1.Y >= 15 && b2.Y - b1.Y <= 26 &&
                b3.Y - b2.Y >= 15 && b3.Y - b2.Y <= 26)
            {
                var docNum = new Point(screenOffsetX + b1.X + b1.W / 2, screenOffsetY + b1.Y + b1.H / 2);
                var taxInv = new Point(screenOffsetX + b2.X + b2.W / 2, screenOffsetY + b2.Y + b2.H / 2);
                var delOrd = new Point(screenOffsetX + b3.X + b3.W / 2, screenOffsetY + b3.Y + b3.H / 2);
                return new DocFieldLocations(docNum, taxInv, delOrd);
            }
        }

        return null;
    }
}

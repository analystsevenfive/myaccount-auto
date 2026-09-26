using System;
using System.Drawing;
using System.Text.RegularExpressions;

namespace ProsoftAutoLogin.Automation;

internal static class CreditPurchaseDocDetector
{
    public sealed record DocFieldLocations(
        Point DocNumber,
        Point TaxInvoice,
        Point DeliveryOrder,
        Point TaxInvoiceDate,
        Point DeliveryOrderDate);

    /// <summary>
    /// Calculates default proportional coordinates for Document No, Tax Invoice No, Delivery Order No,
    /// Tax Invoice Date, and Delivery Order Date on the Credit Purchase ("ซื้อเชื่อ") window.
    /// Based on verified measurements on the 789x479 sheet:
    /// - เลขที่เอกสาร: childRect.Left + 510, childRect.Top + 72
    /// - เลขที่ใบกำกับ: childRect.Left + 520, childRect.Top + 91
    /// - เลขที่ใบส่งของ: childRect.Left + 520, childRect.Top + 110
    /// - วันที่ใบกำกับ: childRect.Left + 700, childRect.Top + 91
    /// - วันที่ใบส่งของ: childRect.Left + 700, childRect.Top + 110
    /// </summary>
    public static DocFieldLocations GetDefaultDocFieldLocations(Win32Native.RECT childRect)
    {
        var docNum = new Point(childRect.Left + 510, childRect.Top + 72);
        var taxInv = new Point(childRect.Left + 520, childRect.Top + 91);
        var delOrd = new Point(childRect.Left + 520, childRect.Top + 110);
        var taxInvDate = new Point(childRect.Left + 700, childRect.Top + 91);
        var delOrdDate = new Point(childRect.Left + 700, childRect.Top + 110);
        return new DocFieldLocations(docNum, taxInv, delOrd, taxInvDate, delOrdDate);
    }

    /// <summary>
    /// Attempts to visually detect the edit boxes in a cropped bitmap of the header area.
    /// Searches for horizontal dark border lines with white interiors.
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
            for (int x = 10; x < width - 75; x++)
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

                    if (hLine >= 70 && hLine <= 140)
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
                                    if (!foundBoxes.Exists(b => Math.Abs(b.X - x) < 10 && Math.Abs(b.Y - y) < 10))
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

        // Separate middle boxes (W >= 95)
        var middleBoxes = foundBoxes.Where(b => b.W >= 95).OrderBy(b => b.Y).ToList();
        if (middleBoxes.Count >= 3)
        {
            var b1 = middleBoxes[0];
            var b2 = middleBoxes[1];
            var b3 = middleBoxes[2];

            if (b2.Y - b1.Y >= 15 && b2.Y - b1.Y <= 26 &&
                b3.Y - b2.Y >= 15 && b3.Y - b2.Y <= 26)
            {
                var docNum = new Point(screenOffsetX + b1.X + b1.W / 2, screenOffsetY + b1.Y + b1.H / 2);
                var taxInv = new Point(screenOffsetX + b2.X + b2.W / 2, screenOffsetY + b2.Y + b2.H / 2);
                var delOrd = new Point(screenOffsetX + b3.X + b3.W / 2, screenOffsetY + b3.Y + b3.H / 2);

                // Find corresponding date boxes (to the right of middle boxes, around x + 150..220)
                var dateBoxes = foundBoxes.Where(b => b.X > b2.X + 150).OrderBy(b => b.Y).ToList();
                Point taxInvDate;
                Point delOrdDate;

                var d2 = dateBoxes.FirstOrDefault(b => Math.Abs(b.Y - b2.Y) <= 8);
                var d3 = dateBoxes.FirstOrDefault(b => Math.Abs(b.Y - b3.Y) <= 8);

                if (d2.W > 0)
                {
                    taxInvDate = new Point(screenOffsetX + d2.X + Math.Min(35, d2.W / 2), screenOffsetY + d2.Y + d2.H / 2);
                }
                else
                {
                    taxInvDate = new Point(taxInv.X + 180, taxInv.Y);
                }

                if (d3.W > 0)
                {
                    delOrdDate = new Point(screenOffsetX + d3.X + Math.Min(35, d3.W / 2), screenOffsetY + d3.Y + d3.H / 2);
                }
                else
                {
                    delOrdDate = new Point(delOrd.X + 180, delOrd.Y);
                }

                return new DocFieldLocations(docNum, taxInv, delOrd, taxInvDate, delOrdDate);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a popup message indicates that the Delivery Order Number (เลขที่ใบส่งของ) is duplicate.
    /// </summary>
    public static bool IsDuplicateDeliveryOrderMessage(string message, string title)
    {
        var combined = (title + " " + message).Trim();
        return (combined.Contains("ใบส่งของ", StringComparison.OrdinalIgnoreCase) && combined.Contains("ค่าซ้ำ", StringComparison.OrdinalIgnoreCase)) ||
               combined.Contains("ใบส่งของ เป็นค่าซ้ำ", StringComparison.OrdinalIgnoreCase) ||
               (combined.Contains("Delivery", StringComparison.OrdinalIgnoreCase) && combined.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Computes the next delivery order number candidate with suffix /1, /2, etc.
    /// </summary>
    public static string GenerateNextDeliveryOrderNumber(string baseDeliveryOrder, int suffixIndex)
    {
        if (string.IsNullOrWhiteSpace(baseDeliveryOrder)) return "";
        var cleanBase = Regex.Replace(baseDeliveryOrder.Trim(), @"/\d+$", "");
        return suffixIndex <= 0 ? cleanBase : $"{cleanBase}/{suffixIndex}";
    }

    /// <summary>
    /// Normalizes raw date string (e.g. "24/9/2026", "24/09/2026", "2026-09-24", "24/9/2569")
    /// into standard "dd/MM/yyyy" format for Prosoft EditMask.
    /// </summary>
    public static string NormalizeProsoftDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var trimmed = input.Trim();

        int spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx > 0)
        {
            trimmed = trimmed.Substring(0, spaceIdx);
        }

        string[] formats = [
            "d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy",
            "d-M-yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "yyyy/MM/dd",
            "d.M.yyyy", "dd.MM.yyyy"
        ];

        if (System.DateTime.TryParseExact(trimmed, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
        {
            int year = dt.Year;
            if (year > 2400)
            {
                year -= 543;
            }
            return $"{dt.Day:D2}/{dt.Month:D2}/{year:D4}";
        }

        var m = Regex.Match(trimmed, @"^(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{2,4})$");
        if (m.Success)
        {
            int day = int.Parse(m.Groups[1].Value);
            int month = int.Parse(m.Groups[2].Value);
            int year = int.Parse(m.Groups[3].Value);
            if (year < 100) year += 2000;
            if (year > 2400) year -= 543;
            return $"{day:D2}/{month:D2}/{year:D4}";
        }

        return trimmed;
    }
}

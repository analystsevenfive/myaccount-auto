using System;
using System.Drawing;

namespace ProsoftAutoLogin.Automation;

internal static class MoreTabTaxDetector
{
    public sealed record TaxFieldLocation(int EditX, int EditY, int ArrowX, int ArrowY);

    /// <summary>
    /// Finds the "รหัสกลุ่มภาษี" dropdown edit box and arrow in a bitmap of the More tab area.
    /// Searches for the standard Windows/PowerBuilder combobox outline (width ~90-125 px, height ~18-26 px)
    /// with white background and dropdown button.
    /// </summary>
    public static TaxFieldLocation? FindTaxFieldInBitmap(Bitmap screenBmp, int screenOffsetX = 0, int screenOffsetY = 0)
    {
        if (screenBmp == null) return null;

        int width = screenBmp.Width;
        int height = screenBmp.Height;

        for (int y = 2; y < height - 20; y++)
        {
            for (int x = 20; x < width - 100; x++)
            {
                var p = screenBmp.GetPixel(x, y);
                // Dark border pixel
                if (p.R < 50 && p.G < 50 && p.B < 50)
                {
                    // Check horizontal line length
                    int hLineLen = 0;
                    for (int hx = x; hx < Math.Min(x + 130, width); hx++)
                    {
                        var hp = screenBmp.GetPixel(hx, y);
                        if (hp.R < 60 && hp.G < 60 && hp.B < 60) hLineLen++;
                        else break;
                    }

                    if (hLineLen >= 85 && hLineLen <= 130)
                    {
                        // Check vertical line length
                        int vLineLen = 0;
                        for (int vy = y; vy < Math.Min(y + 30, height); vy++)
                        {
                            var vp = screenBmp.GetPixel(x, vy);
                            if (vp.R < 60 && vp.G < 60 && vp.B < 60) vLineLen++;
                            else break;
                        }

                        if (vLineLen >= 16 && vLineLen <= 28)
                        {
                            // Verify interior is white/light
                            int testX = x + Math.Min(20, hLineLen / 2);
                            int testY = y + vLineLen / 2;
                            if (testX < width && testY < height)
                            {
                                var inside = screenBmp.GetPixel(testX, testY);
                                if (inside.R > 200 && inside.G > 200 && inside.B > 200)
                                {
                                    int boxW = hLineLen;
                                    int boxH = vLineLen;
                                    int editX = screenOffsetX + x + (boxW - 20) / 2;
                                    int editY = screenOffsetY + y + boxH / 2;
                                    int arrowX = screenOffsetX + x + boxW - 8;
                                    int arrowY = screenOffsetY + y + boxH / 2;
                                    return new TaxFieldLocation(editX, editY, arrowX, arrowY);
                                }
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates default proportional coordinates for 'รหัสกลุ่มภาษี' on the Credit Purchase window.
    /// Based on standard Prosoft myAccount 789x479 layout.
    /// </summary>
    public static TaxFieldLocation GetDefaultTaxFieldLocation(Win32Native.RECT childRect)
    {
        int editX = childRect.Left + 195;
        int editY = childRect.Top + 205;
        int arrowX = childRect.Left + 242;
        int arrowY = childRect.Top + 205;
        return new TaxFieldLocation(editX, editY, arrowX, arrowY);
    }
}

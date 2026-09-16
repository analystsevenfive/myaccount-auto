using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ProsoftAutoLogin.Automation;

public static class FlowchartDetector
{
    public sealed record CardBar(int Left, int Right, int Top, int Bottom)
    {
        public int CenterX => (Left + Right) / 2;
        public int CenterY => (Top + Bottom) / 2;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Scans a bitmap of the Prosoft workflow pane for the orange header bars of action cards.
    /// </summary>
    public static List<CardBar> FindOrangeBars(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;

        bool[,] mask = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = bmp.GetPixel(x, y);
                // Orange top border of card: R >= 200, G in [85, 175], B <= 85, R - G >= 40
                if (p.R >= 200 && p.G >= 85 && p.G <= 175 && p.B <= 85 && (p.R - p.G) >= 40)
                {
                    mask[x, y] = true;
                }
            }
        }

        var bars = new List<CardBar>();
        bool[,] visited = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mask[x, y] && !visited[x, y])
                {
                    int minX = x, maxX = x, minY = y, maxY = y;
                    var queue = new Queue<(int X, int Y)>();
                    queue.Enqueue((x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        if (cx < minX) minX = cx;
                        if (cx > maxX) maxX = cx;
                        if (cy < minY) minY = cy;
                        if (cy > maxY) maxY = cy;

                        int[] dx = [0, 1, 0, -1];
                        int[] dy = [-1, 0, 1, 0];
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cx + dx[d];
                            int ny = cy + dy[d];
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height && mask[nx, ny] && !visited[nx, ny])
                            {
                                visited[nx, ny] = true;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }

                    int bWidth = maxX - minX;
                    int bHeight = maxY - minY;
                    // Prosoft card header bar is typically 50-200 px wide and 2-25 px high
                    if (bWidth >= 50 && bWidth <= 200 && bHeight >= 2 && bHeight <= 25)
                    {
                        bars.Add(new CardBar(minX, maxX, minY, maxY));
                    }
                }
            }
        }

        return bars;
    }

    /// <summary>
    /// Finds the center point of the "ซื้อเชื่อ" (Credit Purchase) card in the workflow diagram.
    /// In the standard Prosoft Purchase Order workflow, the middle column consists of:
    /// 1. จ่ายเงินมัดจำ (Advance Payment)
    /// 2. ซื้อสด (Cash Purchase)
    /// 3. ซื้อเชื่อ (Credit Purchase) -> TARGET
    /// 4. ปันส่วนต้นทุนสินค้า (Cost Allocation)
    /// </summary>
    public static Point? FindCreditPurchaseCard(Bitmap bmp)
    {
        var bars = FindOrangeBars(bmp);
        if (bars.Count == 0) return null;

        // Group bars into columns by X center (bucket size ~50px)
        var columns = bars
            .GroupBy(b => (int)(Math.Round((double)b.CenterX / 50.0) * 50))
            .OrderByDescending(g => g.Count())
            .ToList();

        if (columns.Count == 0) return null;

        // The middle column has the most cards (4 cards in Purchase Order workflow)
        var middleCol = columns[0].OrderBy(b => b.CenterY).ToList();

        if (middleCol.Count >= 3)
        {
            // Bar index 2 is the 3rd card: "ซื้อเชื่อ"
            var targetBar = middleCol[2];
            return new Point(targetBar.CenterX, targetBar.CenterY + 22);
        }
        else if (middleCol.Count == 2)
        {
            // If only 2 bars found in middle column, take the 2nd
            var targetBar = middleCol[1];
            return new Point(targetBar.CenterX, targetBar.CenterY + 22);
        }
        else if (middleCol.Count == 1)
        {
            var targetBar = middleCol[0];
            return new Point(targetBar.CenterX, targetBar.CenterY + 22);
        }

        return null;
    }

    /// <summary>
    /// Fallback calculation based on known layout geometry proportions when visual detection cannot run.
    /// In the workflow pane:
    /// - Horizontal center is at ~47-50%
    /// - Vertical center of card 3 is at ~68-70%
    /// </summary>
    public static Point GetProportionalCreditPurchasePoint(int paneWidth, int paneHeight)
    {
        int x = (int)(paneWidth * 0.47);
        int y = (int)(paneHeight * 0.69);
        return new Point(x, y);
    }
}

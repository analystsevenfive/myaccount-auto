using System.IO;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.UIA3;
using ProsoftAutoLogin.Automation;
using ProsoftAutoLogin.Configuration;
using ProsoftAutoLogin.Data;
using ProsoftAutoLogin.Models;
using Xunit;

namespace ProsoftAutoLogin.Tests;

public class SettingsAndModelsTests
{
    [Fact]
    public void AppSettings_DeserializesCorrectly()
    {
        var appSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ProsoftAutoLogin", "appsettings.json");

        var fullPath = Path.GetFullPath(appSettingsPath);
        Assert.True(File.Exists(fullPath), $"File not found: {fullPath}");

        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        Assert.NotNull(settings);
        Assert.NotNull(settings.Prosoft);
        Assert.Contains("myAccount", settings.Prosoft.ProcessNames);
        Assert.NotEmpty(settings.Prosoft.UserNameSelectors);
        Assert.NotEmpty(settings.Prosoft.ProfileSelectors);
        Assert.NotEmpty(settings.Prosoft.PasswordSelectors);
        Assert.Contains(settings.Prosoft.PasswordSelectors, s => s.ExcludeComboBoxChildren == true);
        Assert.NotEmpty(settings.Prosoft.LoginButtonSelectors);
        Assert.Contains(settings.Prosoft.LoginButtonSelectors, s => s.NameContains == "OK");
        Assert.Contains("OK", settings.Prosoft.DialogButtonNames);
        Assert.True(settings.Prosoft.AttachTimeoutSeconds > 0);
        Assert.True(settings.Prosoft.ResultTimeoutSeconds > 0);
        Assert.True(settings.Prosoft.PollIntervalMilliseconds > 0);
    }

    [Fact]
    public void LoginResult_Success_SetsCorrectProperties()
    {
        var result = LoginResult.Success("เข้าสู่ระบบเรียบร้อย");

        Assert.True(result.IsSuccess);
        Assert.Equal(LoginResultKind.Success, result.Kind);
        Assert.Equal("เข้าสู่ระบบเรียบร้อย", result.Message);
    }

    [Fact]
    public void LoginResult_Error_SetsCorrectProperties()
    {
        var result = LoginResult.Error("รหัสผ่านไม่ถูกต้อง");

        Assert.False(result.IsSuccess);
        Assert.Equal(LoginResultKind.Error, result.Kind);
        Assert.Equal("รหัสผ่านไม่ถูกต้อง", result.Message);
    }

    [Fact]
    public void LoginResult_Timeout_SetsCorrectProperties()
    {
        var result = LoginResult.Timeout("หมดเวลาเชื่อมต่อ");

        Assert.False(result.IsSuccess);
        Assert.Equal(LoginResultKind.Timeout, result.Kind);
        Assert.Equal("หมดเวลาเชื่อมต่อ", result.Message);
    }

    [Fact]
    public void ProsoftOptions_DefaultInitializers_HaveReasonableValues()
    {
        var options = new ProsoftOptions();

        Assert.NotNull(options.ProcessNames);
        Assert.NotNull(options.LoginWindowTitleContains);
        Assert.NotNull(options.SuccessWindowTitleContains);
        Assert.NotNull(options.UserNameSelectors);
        Assert.NotNull(options.PasswordSelectors);
        Assert.NotNull(options.LoginButtonSelectors);
        Assert.NotNull(options.DialogButtonNames);
        Assert.True(options.AttachTimeoutSeconds > 0);
        Assert.True(options.ResultTimeoutSeconds > 0);
        Assert.True(options.PollIntervalMilliseconds > 0);
        Assert.NotNull(options.Navigation);
        Assert.Equal("Purchase Order", options.Navigation.ModuleName);
        Assert.Equal("PO Data Entry", options.Navigation.SubModuleName);
        Assert.Equal("ซื้อเชื่อ", options.Navigation.TargetAction);
        Assert.True(options.Navigation.NavigationTimeoutSeconds > 0);
        Assert.NotEmpty(options.Navigation.ModuleAliases);
        Assert.NotEmpty(options.Navigation.SubModuleAliases);
        Assert.NotEmpty(options.Navigation.TargetActionAliases);
    }

    [Fact]
    public void NavigationResult_Success_SetsCorrectProperties()
    {
        var result = NavigationResult.Success("เปิดหน้าต่างซื้อเชื่อสำเร็จ");

        Assert.True(result.IsSuccess);
        Assert.Equal(NavigationResultKind.Success, result.Kind);
        Assert.Equal("เปิดหน้าต่างซื้อเชื่อสำเร็จ", result.Message);
    }

    [Fact]
    public void NavigationResult_Error_SetsCorrectProperties()
    {
        var result = NavigationResult.Error("ไม่พบเมนูซื้อเชื่อ");

        Assert.False(result.IsSuccess);
        Assert.Equal(NavigationResultKind.Error, result.Kind);
        Assert.Equal("ไม่พบเมนูซื้อเชื่อ", result.Message);
    }

    [Fact]
    public void NavigationResult_Timeout_SetsCorrectProperties()
    {
        var result = NavigationResult.Timeout("หมดเวลารอหน้าต่างซื้อเชื่อ");

        Assert.False(result.IsSuccess);
        Assert.Equal(NavigationResultKind.Timeout, result.Kind);
        Assert.Equal("หมดเวลารอหน้าต่างซื้อเชื่อ", result.Message);
    }

    [Fact]
    public void AppSettings_NavigationSection_DeserializesCorrectly()
    {
        var appSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ProsoftAutoLogin", "appsettings.json");

        var fullPath = Path.GetFullPath(appSettingsPath);
        Assert.True(File.Exists(fullPath));

        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        Assert.NotNull(settings);
        Assert.NotNull(settings.Prosoft.Navigation);
        Assert.Equal("Purchase Order", settings.Prosoft.Navigation.ModuleName);
        Assert.Equal("PO Data Entry", settings.Prosoft.Navigation.SubModuleName);
        Assert.Equal("ซื้อเชื่อ", settings.Prosoft.Navigation.TargetAction);
        Assert.Contains("ซื้อเชื่อ", settings.Prosoft.Navigation.TargetActionAliases);
        Assert.Contains("จัดซื้อ", settings.Prosoft.Navigation.ModuleAliases);
        Assert.Contains("บันทึกข้อมูลประจำวัน", settings.Prosoft.Navigation.SubModuleAliases);
    }

    [Fact]
    public void FileLogger_WritesToLogFile()
    {
        var testMessage = $"Test log entry {Guid.NewGuid()}";
        FileLogger.Log(testMessage);

        var logPath = FileLogger.LogFilePath;
        Assert.True(File.Exists(logPath));
        var content = File.ReadAllText(logPath);
        Assert.Contains(testMessage, content);
    }

    [Fact]
    public void AppSettings_LoginButtonSelectors_IncludeOkVariants()
    {
        var appSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ProsoftAutoLogin", "appsettings.json");

        var fullPath = Path.GetFullPath(appSettingsPath);
        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(settings);
        var selectors = settings.Prosoft.LoginButtonSelectors;
        Assert.Contains(selectors, s => s.NameContains == "OK");
        Assert.Contains(selectors, s => s.NameContains == "&OK");
        Assert.Contains(selectors, s => s.ClassNameContains == "FNBUTTON");
    }

    [Fact]
    public void ButtonFiltering_SelectsOkButtonOverProfileAndCancel()
    {
        // Mimic real dump from Prosoft login window
        var pwRect = new Win32Native.RECT { Left = 1033, Top = 462, Right = 1186, Bottom = 481 };
        int profileTop = 552;

        var profileLabel = new Win32Native.ChildButtonInfo(
            (IntPtr)0x130C8C, "Profile", "Button",
            new Win32Native.RECT { Left = 993, Top = 554, Right = 1027, Bottom = 570 }, 1003);

        var okButton = new Win32Native.ChildButtonInfo(
            (IntPtr)0xD0CAC, "", "Button",
            new Win32Native.RECT { Left = 1033, Top = 520, Right = 1108, Bottom = 543 }, 1038);

        var cancelButton = new Win32Native.ChildButtonInfo(
            (IntPtr)0x8055E, "", "Button",
            new Win32Native.RECT { Left = 1106, Top = 520, Right = 1181, Bottom = 543 }, 1010);

        var dbLogin = new Win32Native.ChildButtonInfo(
            (IntPtr)0x110D20, "DB Login", "Button",
            new Win32Native.RECT { Left = 955, Top = 669, Right = 1002, Bottom = 685 }, 1009);

        var allButtons = new List<Win32Native.ChildButtonInfo>
        {
            profileLabel,
            okButton,
            cancelButton,
            dbLogin
        };

        // Apply Win32 layout filtering
        var inBand = allButtons.Where(b =>
            b.Rect.Top >= pwRect.Bottom - 5 &&
            b.Rect.Bottom <= profileTop - 2 &&
            b.Rect.Left >= pwRect.Left - 15 &&
            (b.Rect.Right - b.Rect.Left) >= 45 &&
            (b.Rect.Right - b.Rect.Left) <= 120 &&
            (b.Rect.Bottom - b.Rect.Top) >= 18 &&
            (b.Rect.Bottom - b.Rect.Top) <= 35)
            .ToList();

        // Profile and DB Login must be excluded
        Assert.DoesNotContain(inBand, b => b.Text.Contains("Profile"));
        Assert.DoesNotContain(inBand, b => b.Text.Contains("DB Login"));

        // Only OK and Cancel remain in band
        Assert.Equal(2, inBand.Count);
        Assert.Contains(inBand, b => b.CtrlId == 1038);
        Assert.Contains(inBand, b => b.CtrlId == 1010);

        // Selection by ID 1038 or alignment selects OK button
        var selected = inBand.FirstOrDefault(b => b.CtrlId == 1038) ??
                       inBand.FirstOrDefault(b => Math.Abs(b.Rect.Left - pwRect.Left) <= 15);

        Assert.NotNull(selected);
        Assert.Equal((IntPtr)0xD0CAC, selected.Hwnd);
        Assert.Equal(1038, selected.CtrlId);
    }

    [Fact]
    public void InspectRunningProsoftMainWindow()
    {
        var proc = System.Diagnostics.Process.GetProcessesByName("myaccount").FirstOrDefault();
        if (proc == null) return;

        using var app = FlaUI.Core.Application.Attach(proc);
        using var automation = new FlaUI.UIA3.UIA3Automation();

        var logPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "logs", "prosoft-main-window-dump.txt");
        var fullPath = Path.GetFullPath(logPath);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Prosoft Process: PID={proc.Id}");

        Win32Native.EnsureDefaultDesktop();

        var windows = new List<IntPtr>();
        var seen = new HashSet<IntPtr>();

        proc.Refresh();
        foreach (System.Diagnostics.ProcessThread thread in proc.Threads)
        {
            Win32Native.EnumThreadWindows((uint)thread.Id, (hwnd, _) =>
            {
                if (seen.Add(hwnd)) windows.Add(hwnd);
                return true;
            }, IntPtr.Zero);
        }

        Win32Native.EnumWindows((hwnd, _) =>
        {
            Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == proc.Id && seen.Add(hwnd)) windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        sb.AppendLine($"Total Windows found: {windows.Count}");

        foreach (var hwnd in windows)
        {
            var text = Win32Native.GetText(hwnd);
            var cls = Win32Native.GetClass(hwnd);
            var visible = Win32Native.IsWindowVisible(hwnd) || Win32Native.IsStyleVisible(hwnd);
            Win32Native.GetWindowRect(hwnd, out var rect);
            sb.AppendLine($"Window HWND=0x{hwnd.ToInt64():X}, Title='{text}', Class='{cls}', Visible={visible}, Rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom})");

            if (visible)
            {
                // Enumerate Win32 children
                sb.AppendLine("  Win32 Children:");
                Win32Native.EnumChildWindows(hwnd, (childHwnd, _) =>
                {
                    var cText = Win32Native.GetText(childHwnd);
                    var cCls = Win32Native.GetClass(childHwnd);
                    var cId = Win32Native.GetDlgCtrlID(childHwnd);
                    Win32Native.GetWindowRect(childHwnd, out var cRect);
                    sb.AppendLine($"    [Child] HWND=0x{childHwnd.ToInt64():X}, Id={cId}, Class='{cCls}', Text='{cText}', Rect=({cRect.Left},{cRect.Top},{cRect.Right},{cRect.Bottom})");
                    return true;
                }, IntPtr.Zero);

                // Enumerate UIA elements
                try
                {
                    var element = automation.FromHandle(hwnd);
                    if (element != null)
                    {
                        var descendants = element.FindAllDescendants();
                        sb.AppendLine($"  UIA Descendants ({descendants.Length}):");
                        foreach (var d in descendants)
                        {
                            var r = d.BoundingRectangle;
                            sb.AppendLine($"    [UIA] Type={d.ControlType}, Name='{d.Name}', Class='{d.ClassName}', Rect=({r.Left},{r.Top},{r.Right},{r.Bottom})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  UIA Error: {ex.Message}");
                }
            }
            sb.AppendLine();
        }

        File.WriteAllText(fullPath, sb.ToString(), System.Text.Encoding.UTF8);
    }

    [Fact]
    public async Task ExportCurrentUiTree()
    {
        var proc = System.Diagnostics.Process.GetProcessesByName("myaccount").FirstOrDefault();
        if (proc == null) return;

        var service = new ProsoftAutomationService(new ProsoftOptions());
        var path = await service.ExportUiTreeAsync(CancellationToken.None);
        FileLogger.Log($"[Test Export] Exported to {path}");
    }

    [Fact]
    public void CaptureCreditPurchaseScreenshot()
    {
        var proc = System.Diagnostics.Process.GetProcessesByName("myaccount").FirstOrDefault();
        if (proc == null) return;

        Win32Native.EnsureDefaultDesktop();

        // Find ซื้อเชื่อ window HWND
        IntPtr targetHwnd = IntPtr.Zero;
        Win32Native.EnumWindows((hwnd, _) =>
        {
            Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == proc.Id)
            {
                var title = Win32Native.GetText(hwnd);
                if (title.Contains("ซื้อเชื่อ")) targetHwnd = hwnd;
            }
            return true;
        }, IntPtr.Zero);

        if (targetHwnd == IntPtr.Zero)
        {
            // Search child windows of main window
            foreach (System.Diagnostics.ProcessThread thread in proc.Threads)
            {
                Win32Native.EnumThreadWindows((uint)thread.Id, (hwnd, _) =>
                {
                    var title = Win32Native.GetText(hwnd);
                    if (title.Contains("ซื้อเชื่อ")) targetHwnd = hwnd;
                    return true;
                }, IntPtr.Zero);
            }
        }

        if (targetHwnd == IntPtr.Zero)
        {
            FileLogger.Log("[Screenshot] Could not find ซื้อเชื่อ window");
            return;
        }

        Win32Native.GetWindowRect(targetHwnd, out var rect);
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        FileLogger.Log($"[Screenshot] ซื้อเชื่อ window HWND=0x{targetHwnd.ToInt64():X} Rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom}) Size={w}x{h}");

        if (w <= 0 || h <= 0) return;

        try
        {
            using var bmp = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(w, h));
            }

            var savePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "logs", "credit-purchase-window.png");
            var fullSavePath = Path.GetFullPath(savePath);
            bmp.Save(fullSavePath, System.Drawing.Imaging.ImageFormat.Png);
            FileLogger.Log($"[Screenshot] Saved screenshot to {fullSavePath}");
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[Screenshot] Could not capture screen: {ex.Message}");
        }
    }

    [Fact]
    public void AnalyzeVendorCodeFieldCoordinates()
    {
        var savePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "logs", "credit-purchase-window.png");
        var fullSavePath = Path.GetFullPath(savePath);
        if (!File.Exists(fullSavePath)) return;

        using var bmp = new System.Drawing.Bitmap(fullSavePath);
        int minX = 9999, maxX = 0, minY = 9999, maxY = 0;
        for (int y = 40; y <= 110; y++)
        {
            for (int x = 50; x <= 250; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.R > 250 && c.G > 250 && c.B > 250)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        FileLogger.Log($"[Vendor Box] Bounds: X=({minX}..{maxX}), Y=({minY}..{maxY}), Center=({(minX+maxX)/2}, {(minY+maxY)/2})");

        // Also look for white edit field around X=80..180, Y=68..88
        int whiteCount = 0;
        for (int y = 70; y <= 85; y++)
        {
            for (int x = 90; x <= 180; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.R > 240 && c.G > 240 && c.B > 240) whiteCount++;
            }
        }
        FileLogger.Log($"[Edit Field Area] White pixels in 90..180, 70..85: {whiteCount}");
    }

    [Fact]
    public async Task TestOpenFindVendorDialog()
    {
        var proc = System.Diagnostics.Process.GetProcessesByName("myaccount").FirstOrDefault();
        if (proc == null) return;

        Win32Native.EnsureDefaultDesktop();

        // Find top-level main window
        IntPtr mainHwnd = IntPtr.Zero;
        Win32Native.EnumWindows((hwnd, _) =>
        {
            Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == proc.Id && Win32Native.GetText(hwnd).Contains("myAccount"))
            {
                mainHwnd = hwnd;
            }
            return true;
        }, IntPtr.Zero);

        if (mainHwnd == IntPtr.Zero)
        {
            FileLogger.Log("[Test Find] Could not find myAccount main window");
            return;
        }

        Win32Native.ShowWindow(mainHwnd, Win32Native.SW_RESTORE);
        Win32Native.SetForegroundWindow(mainHwnd);
        await Task.Delay(200);

        // Find MDI child window (parent is MDIClient)
        IntPtr targetHwnd = IntPtr.Zero;
        Win32Native.EnumChildWindows(mainHwnd, (childHwnd, _) =>
        {
            var cls = Win32Native.GetClass(childHwnd);
            var parent = Win32Native.GetParent(childHwnd);
            var parentCls = Win32Native.GetClass(parent);
            if (cls == "FNWND380" && parentCls == "MDIClient")
            {
                targetHwnd = childHwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        FileLogger.Log($"[Test Find] Found MDI child window: 0x{targetHwnd.ToInt64():X}");
        if (targetHwnd == IntPtr.Zero) return;

        // Activate child window by clicking its title bar
        Win32Native.GetWindowRect(targetHwnd, out var rect);
        await Win32Native.ClickScreenPointAsync(rect.Left + 60, rect.Top + 12);
        await Task.Delay(150);

        // Click inside the 'รหัสผู้ขาย' edit field (145, 81)
        int editX = rect.Left + 145;
        int editY = rect.Top + 81;
        FileLogger.Log($"[Test Find] Clicking 'รหัสผู้ขาย' at ({editX}, {editY})...");
        await Win32Native.ClickScreenPointAsync(editX, editY);
        await Task.Delay(200);

        // Send F2 key
        FileLogger.Log("[Test Find] Sending F2 key to focused 'รหัสผู้ขาย'...");
        await Win32Native.SendKeyPressAsync(0x70);
        await Task.Delay(1000);

        // Also test clicking dropdown arrow (rect.Left + 198, rect.Top + 81) if Find doesn't open
        IntPtr findHwnd = IntPtr.Zero;
        string findTitle = "";
        void CheckFindWindow()
        {
            Win32Native.EnumWindows((hwnd, _) =>
            {
                Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == proc.Id)
                {
                    var t = Win32Native.GetText(hwnd);
                    if (t.Contains("Find") || t.Contains("ผู้ขาย"))
                    {
                        findHwnd = hwnd;
                        findTitle = t;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (findHwnd == IntPtr.Zero)
            {
                foreach (System.Diagnostics.ProcessThread thread in proc.Threads)
                {
                    Win32Native.EnumThreadWindows((uint)thread.Id, (hwnd, _) =>
                    {
                        var t = Win32Native.GetText(hwnd);
                        if (t.Contains("Find") || t.Contains("ผู้ขาย"))
                        {
                            findHwnd = hwnd;
                            findTitle = t;
                        }
                        return true;
                    }, IntPtr.Zero);
                }
            }
        }

        CheckFindWindow();

        if (findHwnd == IntPtr.Zero)
        {
            int arrowX = rect.Left + 198;
            int arrowY = rect.Top + 81;
            FileLogger.Log($"[Test Find] F2 did not open Find yet; clicking dropdown arrow at ({arrowX}, {arrowY})...");
            await Win32Native.ClickScreenPointAsync(arrowX, arrowY);
            await Task.Delay(1000);
            CheckFindWindow();
        }

        FileLogger.Log($"[Test Find] Result: HWND=0x{findHwnd.ToInt64():X}, Title='{findTitle}'");

        // Take screenshot of desktop
        try
        {
            using var screenBmp = new System.Drawing.Bitmap(1920, 1080);
            using (var g = System.Drawing.Graphics.FromImage(screenBmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(1920, 1080));
            }
            var afterTestPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "logs", "after-find-test.png");
            screenBmp.Save(Path.GetFullPath(afterTestPath), System.Drawing.Imaging.ImageFormat.Png);
            FileLogger.Log($"[Test Find] Saved screenshot to {Path.GetFullPath(afterTestPath)}");
        }
        catch { }
    }

    [Fact]
    public void FlowchartDetector_FindsCreditPurchaseCard_InScreenshot()
    {
        var screenshotPath = @"C:\Users\0125024\.gemini\antigravity-ide\brain\1725a89e-4c5f-42ba-94e3-a5d7e3514b62\.user_uploaded\media_1789435614414.png";
        if (!File.Exists(screenshotPath)) return;

        using var bmp = new System.Drawing.Bitmap(screenshotPath);
        var pt = FlowchartDetector.FindCreditPurchaseCard(bmp);

        Assert.NotNull(pt);
        // The middle column card 3 (ซื้อเชื่อ) is around X=524, Y=292
        Assert.InRange(pt.Value.X, 510, 540);
        Assert.InRange(pt.Value.Y, 280, 310);
    }

    [Fact]
    public void FlowchartDetector_FindsCreditPurchaseCard_InLatestScreenshot()
    {
        var screenshotPath = @"C:\Users\0125024\.gemini\antigravity-ide\brain\1725a89e-4c5f-42ba-94e3-a5d7e3514b62\.user_uploaded\media_1789444835841.png";
        if (!File.Exists(screenshotPath)) return;

        using var bmp = new System.Drawing.Bitmap(screenshotPath);
        var bars = FlowchartDetector.FindOrangeBars(bmp);
        Assert.NotEmpty(bars);

        foreach (var b in bars)
        {
            FileLogger.Log($"[Test] Bar at ({b.Left},{b.Top}) - ({b.Right},{b.Bottom}), Center=({b.CenterX},{b.CenterY}), Size={b.Width}x{b.Height}");
        }

        var pt = FlowchartDetector.FindCreditPurchaseCard(bmp);
        Assert.NotNull(pt);
        FileLogger.Log($"[Test] Target Credit Purchase point = ({pt.Value.X}, {pt.Value.Y})");
    }

    [Fact]
    public void FlowchartDetector_FindsCreditPurchaseCard_InCroppedFlowchartPane()
    {
        var screenshotPath = @"C:\Users\0125024\.gemini\antigravity-ide\brain\1725a89e-4c5f-42ba-94e3-a5d7e3514b62\.user_uploaded\media_1789444835841.png";
        if (!File.Exists(screenshotPath)) return;

        using var fullBmp = new System.Drawing.Bitmap(screenshotPath);
        // Find orange bars in full image to locate the flowchart area
        var bars = FlowchartDetector.FindOrangeBars(fullBmp);
        Assert.NotEmpty(bars);

        int minX = bars.Min(b => b.Left) - 50;
        int maxX = bars.Max(b => b.Right) + 50;
        int minY = bars.Min(b => b.Top) - 50;
        int maxY = bars.Max(b => b.Bottom) + 50;

        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        int w = Math.Min(fullBmp.Width - minX, maxX - minX);
        int h = Math.Min(fullBmp.Height - minY, maxY - minY);

        // Crop just the flowchart pane
        using var croppedBmp = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(croppedBmp))
        {
            g.DrawImage(fullBmp, new System.Drawing.Rectangle(0, 0, w, h), new System.Drawing.Rectangle(minX, minY, w, h), System.Drawing.GraphicsUnit.Pixel);
        }

        var croppedPt = FlowchartDetector.FindCreditPurchaseCard(croppedBmp);
        Assert.NotNull(croppedPt);
        FileLogger.Log($"[Test] Cropped Flowchart Target point = ({croppedPt.Value.X}, {croppedPt.Value.Y}), mapped back = ({minX + croppedPt.Value.X}, {minY + croppedPt.Value.Y})");
        Assert.Equal(390, minX + croppedPt.Value.X);
        Assert.Equal(389, minY + croppedPt.Value.Y);
    }

    [Fact]
    public void VendorCsvReader_ReadsActualInputCsv()
    {
        var record = VendorCsvReader.ReadFirstVendor("input/input.csv");
        Assert.NotNull(record);
        Assert.Equal("GUANGZHOU NANTIAN", record.VendorName);
    }

    [Fact]
    public void VendorFillResult_FactoryMethods_WorkCorrectly()
    {
        var success = VendorFillResult.Success("กรอกข้อมูลผู้ขายสำเร็จ", "GUANGZHOU NANTIAN", "V001");
        Assert.True(success.IsSuccess);
        Assert.Equal("GUANGZHOU NANTIAN", success.VendorName);
        Assert.Equal("V001", success.VendorCode);

        var error = VendorFillResult.Error("ไม่พบหน้าต่างค้นหา");
        Assert.False(error.IsSuccess);
        Assert.Equal("ไม่พบหน้าต่างค้นหา", error.Message);
    }

    [Fact]
    public void AppSettings_ContainsVendorInputOptions()
    {
        var appSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ProsoftAutoLogin", "appsettings.json");

        var fullPath = Path.GetFullPath(appSettingsPath);
        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        Assert.NotNull(settings);
        Assert.NotNull(settings.Prosoft.VendorInput);
        Assert.Equal("input/input.csv", settings.Prosoft.VendorInput.CsvPath);
        Assert.Equal("ชื่อผู้ขาย", settings.Prosoft.VendorInput.VendorNameColumn);
    }

    [Fact]
    public void Win32Native_NewConstantsAndHelpers_HandleZeroGracefully()
    {
        Assert.Equal(0x08, Win32Native.VK_BACK);
        Assert.Equal(0x56, Win32Native.VK_V);
        Assert.Equal(0x70, Win32Native.VK_F2);
        Assert.Equal(0x000D, Win32Native.WM_GETTEXT);
        Assert.Equal(0x0302u, Win32Native.WM_PASTE);
        Assert.Equal(0x0146u, Win32Native.CB_GETCOUNT);

        Assert.Equal(string.Empty, Win32Native.GetEditText(IntPtr.Zero));
        Assert.Equal(string.Empty, Win32Native.GetComboBoxCurrentText(IntPtr.Zero));
        Assert.Empty(Win32Native.GetComboBoxItems(IntPtr.Zero));

        Assert.Equal(0x20, Win32Native.VK_SPACE);
        Assert.Equal(0x24, Win32Native.VK_HOME);
        Assert.Equal(0x26, Win32Native.VK_UP);
        Assert.Equal(0x28, Win32Native.VK_DOWN);
        Assert.Equal(0x014Fu, Win32Native.CB_SHOWDROPDOWN);
        Assert.Equal(0x018Bu, Win32Native.LB_GETCOUNT);
        Assert.Equal(0x130Cu, Win32Native.TCM_SETCURSEL);
        Assert.Equal(0x1330u, Win32Native.TCM_SETCURFOCUS);

        // Test SetClipboardTextSafe with empty text does not throw
        Win32Native.SetClipboardTextSafe(string.Empty);
    }

    [Theory]
    [InlineData("ชื่อผู้ขาย", true)]
    [InlineData("ชื่อผู้จำหน่าย", true)]
    [InlineData("ชื่อเจ้าหนี้", true)]
    [InlineData("Vendor Name", true)]
    [InlineData("ชื่อทางการค้า", false)]
    [InlineData("ชื่อผู้ติดต่อ", false)]
    [InlineData("[All]", false)]
    [InlineData("รหัสผู้ขาย", false)]
    [InlineData("ที่อยู่", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsVendorNameText_DetectsVendorNameMatchesAccurately(string? input, bool expected)
    {
        Assert.Equal(expected, ProsoftAutomationService.IsVendorNameText(input!));
    }

    [Fact]
    public void MoreTabTaxDetector_CalculatesDefaultLocation()
    {
        var childRect = new Win32Native.RECT { Left = 100, Top = 200, Right = 889, Bottom = 679 };
        var loc = MoreTabTaxDetector.GetDefaultTaxFieldLocation(childRect);

        Assert.Equal(100 + 195, loc.EditX);
        Assert.Equal(200 + 205, loc.EditY);
        Assert.Equal(100 + 242, loc.ArrowX);
        Assert.Equal(200 + 205, loc.ArrowY);
    }

    [Fact]
    public void MoreTabTaxDetector_FindsFieldInReferenceImage()
    {
        var refPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "reference", "tax-group-field-reference.png");
        var fullPath = Path.GetFullPath(refPath);
        if (!File.Exists(fullPath)) return;

        using var bmp = new System.Drawing.Bitmap(fullPath);
        var loc = MoreTabTaxDetector.FindTaxFieldInBitmap(bmp, 100, 200);

        Assert.NotNull(loc);
        Assert.InRange(loc.EditX, 100 + 150, 100 + 210);
        Assert.InRange(loc.ArrowX, 100 + 215, 100 + 245);
    }

    [Fact]
    public void VendorCsvReader_ReadsDocumentColumns_FromInputCsv()
    {
        var record = VendorCsvReader.ReadFirstVendor("input/input.csv");
        Assert.NotNull(record);
        Assert.Equal("GUANGZHOU NANTIAN", record.VendorName);
        Assert.Equal("VC0926-00014-5", record.DocumentNumber);
        Assert.Equal("Shanghai Consolidate 3-2044", record.TaxInvoiceNumber);
        Assert.Equal("26NTSTH-SX040", record.DeliveryOrderNumber);
    }

    [Fact]
    public void CreditPurchaseDocDetector_ReturnsAccurateDefaultLocations()
    {
        var childRect = new Win32Native.RECT { Left = 200, Top = 150, Right = 989, Bottom = 629 };
        var loc = CreditPurchaseDocDetector.GetDefaultDocFieldLocations(childRect);

        Assert.Equal(200 + 510, loc.DocNumber.X);
        Assert.Equal(150 + 72, loc.DocNumber.Y);

        Assert.Equal(200 + 520, loc.TaxInvoice.X);
        Assert.Equal(150 + 91, loc.TaxInvoice.Y);

        Assert.Equal(200 + 520, loc.DeliveryOrder.X);
        Assert.Equal(150 + 110, loc.DeliveryOrder.Y);
    }

    [Fact]
    public void AppSettings_ContainsDocumentNumberColumns()
    {
        var appSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ProsoftAutoLogin", "appsettings.json");

        var fullPath = Path.GetFullPath(appSettingsPath);
        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        Assert.NotNull(settings);
        Assert.NotNull(settings.Prosoft.VendorInput);
        Assert.Equal("เลขที่เอกสาร", settings.Prosoft.VendorInput.DocumentNumberColumn);
        Assert.Equal("เลขที่ใบกำกับ", settings.Prosoft.VendorInput.TaxInvoiceNumberColumn);
        Assert.Equal("เลขที่ใบส่งของ", settings.Prosoft.VendorInput.DeliveryOrderNumberColumn);
    }

    [Fact]
    public void VendorCsvReader_ReadsDetailItems_FromInputCsv()
    {
        var record = VendorCsvReader.ReadFirstVendor("input/input.csv");
        Assert.NotNull(record);
        Assert.Equal("GUANGZHOU NANTIAN", record.VendorName);
        Assert.Equal("VC0926-00014-5", record.DocumentNumber);
        Assert.Equal("Shanghai Consolidate 3-2044", record.TaxInvoiceNumber);
        Assert.Equal("26NTSTH-SX040", record.DeliveryOrderNumber);

        Assert.NotNull(record.Items);
        Assert.NotEmpty(record.Items);
        Assert.Equal("NTS1-CD-111-9L", record.Items[0].ItemCode);
        Assert.Equal("20", record.Items[0].Quantity);
    }

    [Fact]
    public void CreditPurchaseDetailDetector_CalculatesCoordinatesAccurately()
    {
        var childRect = new Win32Native.RECT { Left = 100, Top = 50, Right = 889, Bottom = 529 };

        // Detail Tab button location
        var tabPt = CreditPurchaseDetailDetector.GetDefaultDetailTabLocation(childRect);
        Assert.Equal(100 + 45, tabPt.X);
        Assert.Equal(529 - 20, tabPt.Y);

        // Row 1 cells
        var r1Code = CreditPurchaseDetailDetector.GetCellLocation(childRect, 1, DetailColumn.ItemCode);
        Assert.Equal(100 + 70, r1Code.X);
        Assert.Equal(50 + 187, r1Code.Y);

        var r1Qty = CreditPurchaseDetailDetector.GetCellLocation(childRect, 1, DetailColumn.Quantity);
        Assert.Equal(100 + 484, r1Qty.X);
        Assert.Equal(50 + 187, r1Qty.Y);

        var r1Price = CreditPurchaseDetailDetector.GetCellLocation(childRect, 1, DetailColumn.UnitPrice);
        Assert.Equal(100 + 560, r1Price.X);
        Assert.Equal(50 + 187, r1Price.Y);

        // Row 2 cells (pitch 16px)
        var r2Code = CreditPurchaseDetailDetector.GetCellLocation(childRect, 2, DetailColumn.ItemCode);
        Assert.Equal(100 + 70, r2Code.X);
        Assert.Equal(50 + 187 + 16, r2Code.Y);

        var r2Qty = CreditPurchaseDetailDetector.GetCellLocation(childRect, 2, DetailColumn.Quantity);
        Assert.Equal(100 + 484, r2Qty.X);
        Assert.Equal(50 + 187 + 16, r2Qty.Y);
    }

    [Fact]
    public void AppSettings_ContainsDetailItemColumns()
    {
        var appSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ProsoftAutoLogin", "appsettings.json");

        var fullPath = Path.GetFullPath(appSettingsPath);
        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        Assert.NotNull(settings);
        Assert.NotNull(settings.Prosoft.VendorInput);
        Assert.Equal("รหัสสินค้า", settings.Prosoft.VendorInput.ItemCodeColumn);
        Assert.Equal("จำนวน", settings.Prosoft.VendorInput.QuantityColumn);
        Assert.Equal("ราคาต่อหน่วย", settings.Prosoft.VendorInput.UnitPriceColumn);
        Assert.Equal("คลัง", settings.Prosoft.VendorInput.WarehouseColumn);
    }

    [Fact]
    public void CreditPurchaseGlDetector_CalculatesCoordinatesAccurately()
    {
        var childRect = new Win32Native.RECT { Left = 100, Top = 50, Right = 889, Bottom = 529 };

        // GL Tab button location (5th tab, index 4)
        var glTab = CreditPurchaseGlDetector.GetDefaultGlTabLocation(childRect);
        Assert.Equal(100 + 300, glTab.X);
        Assert.Equal(529 - 52, glTab.Y);

        // Post Search button [>]
        var postSearch = CreditPurchaseGlDetector.GetDefaultPostSearchButtonLocation(childRect);
        Assert.Equal(100 + 600, postSearch.X);
        Assert.Equal(50 + 185, postSearch.Y);

        // Edit GL Checkbox
        var editGl = CreditPurchaseGlDetector.GetDefaultEditGlCheckboxLocation(childRect);
        Assert.Equal(100 + 182, editGl.X);
        Assert.Equal(50 + 185, editGl.Y);

        // Department column cells
        var r1Dept = CreditPurchaseGlDetector.GetCellLocation(childRect, 1, GlColumn.Department);
        Assert.Equal(100 + 374, r1Dept.X);
        Assert.Equal(50 + 227, r1Dept.Y);

        var r2Dept = CreditPurchaseGlDetector.GetCellLocation(childRect, 2, GlColumn.Department);
        Assert.Equal(100 + 374, r2Dept.X);
        Assert.Equal(50 + 227 + 17, r2Dept.Y);

        // Department dropdown arrow [v] location
        var r1Arrow = CreditPurchaseGlDetector.GetDepartmentDropdownLocation(childRect, 1);
        Assert.Equal(100 + 416, r1Arrow.X);
        Assert.Equal(50 + 227, r1Arrow.Y);

        // Green arrow relative anchor tests
        var greenPt = new System.Drawing.Point(605, 55);
        var r1ArrowFromGreen = CreditPurchaseGlDetector.GetDepartmentDropdownLocationFromGreenArrow(greenPt, 1);
        Assert.Equal(605 - 186, r1ArrowFromGreen.X);
        Assert.Equal(55 + 42, r1ArrowFromGreen.Y);

        var r1CellFromGreen = CreditPurchaseGlDetector.GetDepartmentCellLocationFromGreenArrow(greenPt, 1);
        Assert.Equal(605 - 235, r1CellFromGreen.X);
        Assert.Equal(55 + 42, r1CellFromGreen.Y);

        // Save button on bottom toolbar
        var saveBtn = CreditPurchaseGlDetector.GetDefaultSaveButtonLocation(childRect);
        Assert.Equal(100 + 85, saveBtn.X);
        Assert.Equal(529 - 20, saveBtn.Y);
    }

    [Fact]
    public void CreditPurchaseGlDetector_FindGreenArrowButtonInBitmap_DetectsGreenColor()
    {
        using var bmp = new System.Drawing.Bitmap(100, 60);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.LightGray);
        }

        // Draw a small bright green rectangle at (40..46, 20..25)
        for (int y = 20; y < 26; y++)
        {
            for (int x = 40; x < 47; x++)
            {
                bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(85, 240, 0));
            }
        }

        var detected = CreditPurchaseGlDetector.FindGreenArrowButtonInBitmap(bmp, 100, 200);
        Assert.NotNull(detected);
        Assert.True(detected.Value.X >= 100 + 40 && detected.Value.X <= 100 + 47);
        Assert.True(detected.Value.Y >= 200 + 20 && detected.Value.Y <= 200 + 26);
    }

    [Fact]
    public void CreditPurchaseGlDetector_IsCheckboxCheckedInBitmap_IdentifiesCheckedAndUnchecked()
    {
        using var bmpUnchecked = new System.Drawing.Bitmap(30, 30);
        using (var g = System.Drawing.Graphics.FromImage(bmpUnchecked))
        {
            g.Clear(System.Drawing.Color.White);
        }

        // Unchecked: all white inside box -> should return false
        Assert.False(CreditPurchaseGlDetector.IsCheckboxCheckedInBitmap(bmpUnchecked, 15, 15));

        // Checked: add dark checkmark pixels
        using var bmpChecked = new System.Drawing.Bitmap(30, 30);
        using (var g = System.Drawing.Graphics.FromImage(bmpChecked))
        {
            g.Clear(System.Drawing.Color.White);
        }

        for (int d = -3; d <= 3; d++)
        {
            bmpChecked.SetPixel(15 + d, 15 + d, System.Drawing.Color.Black);
        }

        Assert.True(CreditPurchaseGlDetector.IsCheckboxCheckedInBitmap(bmpChecked, 15, 15));
    }

    [Fact]
    public void SettingsLoader_LoadsGlOptions_FromAppSettingsJson()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            appSettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "ProsoftAutoLogin", "appsettings.json");
        }

        var fullPath = Path.GetFullPath(appSettingsPath);
        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        Assert.NotNull(settings);
        Assert.NotNull(settings.Prosoft.Gl);
        Assert.Equal("INTER", settings.Prosoft.Gl.DefaultDepartment);
        Assert.True(settings.Prosoft.Gl.AutoSaveAfterGl);
        Assert.Contains("Find", settings.Prosoft.Gl.FindDepartmentDialogTitleContains);
        Assert.Contains("แผนก", settings.Prosoft.Gl.FindDepartmentDialogTitleContains);
    }
}





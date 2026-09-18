using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using ProsoftAutoLogin.Configuration;
using ProsoftAutoLogin.Data;
using ProsoftAutoLogin.Models;

namespace ProsoftAutoLogin.Automation;

public sealed class ProsoftAutomationService : IProsoftAutomationService
{
    private readonly ProsoftOptions _options;

    public ProsoftAutomationService(ProsoftOptions options)
    {
        _options = options;
    }

    public Task<LoginResult> LoginAsync(
        string? password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return LoginAsync(null, password, null, progress, cancellationToken);
    }

    public Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return LoginAsync(username, password, null, progress, cancellationToken);
    }

    public Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        string? profile,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => LoginCoreAsync(username, password, profile, progress, cancellationToken),
            cancellationToken);
    }

    public Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        string? profile,
        bool openCreditPurchase,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return LoginAsync(username, password, profile, openCreditPurchase, false, progress, cancellationToken);
    }

    public Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        string? profile,
        bool openCreditPurchase,
        bool fillVendorFromCsv,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            async () =>
            {
                var loginResult = await LoginCoreAsync(username, password, profile, progress, cancellationToken);
                if (!loginResult.IsSuccess || !openCreditPurchase)
                {
                    return loginResult;
                }

                progress?.Report("เข้าสู่ระบบเรียบร้อย กำลังเปิดหน้าซื้อเชื่อ (PO Data Entry)...");
                await Task.Delay(800, cancellationToken);

                var navResult = await NavigateToCreditPurchaseCoreAsync(progress, cancellationToken);
                if (!navResult.IsSuccess)
                {
                    return LoginResult.Error($"เข้าสู่ระบบแล้ว แต่เปิดหน้าซื้อเชื่อไม่สำเร็จ: {navResult.Message}");
                }

                if (!fillVendorFromCsv)
                {
                    return LoginResult.Success(navResult.Message);
                }

                progress?.Report("เปิดหน้าซื้อเชื่อเรียบร้อย กำลังกรอกรหัสผู้ขายจาก CSV...");
                await Task.Delay(800, cancellationToken);

                var vendorResult = await FillVendorFromCsvCoreAsync(null, progress, cancellationToken);
                if (vendorResult.IsSuccess)
                {
                    return LoginResult.Success($"Login สำเร็จ, เปิดหน้าซื้อเชื่อ และกรอกข้อมูลผู้ขาย '{vendorResult.VendorName}' เรียบร้อยแล้ว");
                }
                else
                {
                    return LoginResult.Error($"Login สำเร็จและเปิดหน้าซื้อเชื่อแล้ว แต่กรอกข้อมูลผู้ขายไม่สำเร็จ: {vendorResult.Message}");
                }
            },
            cancellationToken);
    }

    public Task<NavigationResult> NavigateToCreditPurchaseAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => NavigateToCreditPurchaseCoreAsync(progress, cancellationToken),
            cancellationToken);
    }

    public Task<VendorFillResult> FillVendorFromCsvAsync(
        string? csvPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => FillVendorFromCsvCoreAsync(csvPath, progress, cancellationToken),
            cancellationToken);
    }

    public Task<VendorFillResult> ProcessGlAndSaveAsync(
        string? department,
        bool save,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => ProcessGlAndSaveStandaloneAsync(department, save, progress, cancellationToken),
            cancellationToken);
    }

    public Task<string> ExportUiTreeAsync(CancellationToken cancellationToken)
    {
        return Task.Run(
            () => ExportUiTreeCore(cancellationToken),
            cancellationToken);
    }

    private static void Report(IProgress<string>? progress, string message)
    {
        FileLogger.Log(message);
        progress?.Report(message);
    }

    private async Task<LoginResult> LoginCoreAsync(
        string? username,
        string? password,
        string? profile,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        Report(progress, "กำลังค้นหา Prosoft process...");
        using var process = await FindProcessAsync(cancellationToken);

        Report(progress, $"พบ process: {process.ProcessName} (PID {process.Id})");
        using var application = Application.Attach(process);
        using var automation = new UIA3Automation();

        Report(progress, "กำลังค้นหาหน้า Login...");
        var loginWindow = await FindLoginWindowAsync(
            process,
            application,
            automation,
            cancellationToken);

        var loginWindowHandle = loginWindow.Properties.NativeWindowHandle.ValueOrDefault;
        if (loginWindowHandle != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(loginWindowHandle);
        }

        DumpLoginWindowControls(loginWindow, loginWindowHandle);

        AutomationElement? profileElement = null;
        if (!string.IsNullOrWhiteSpace(profile))
        {
            Report(progress, "กำลังค้นหาช่อง Profile...");
            profileElement = FindWithStrategies(
                loginWindow,
                _options.ProfileSelectors);

            if (profileElement is not null)
            {
                Report(progress, $"กำลังเลือก Profile: {profile}...");
                SetProfile(profileElement, profile);
            }
            else
            {
                Report(progress, "ไม่พบช่อง Profile จะใช้ค่าเดิมที่เลือกไว้");
            }
        }
        else
        {
            profileElement = FindWithStrategies(loginWindow, _options.ProfileSelectors);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            Report(progress, "กำลังค้นหาช่อง User Name...");
            var userNameElement = FindWithStrategies(
                loginWindow,
                _options.UserNameSelectors);

            if (userNameElement is not null)
            {
                Report(progress, $"กำลังตั้งค่า User Name: {username}...");
                SetUserName(userNameElement, username);
            }
            else
            {
                Report(progress, "ไม่พบช่อง User Name จะใช้ค่าเดิมที่เลือกไว้");
            }
        }

        Report(progress, "กำลังค้นหาช่อง Password...");
        var passwordElement = FindWithStrategies(
            loginWindow,
            _options.PasswordSelectors);

        if (passwordElement is null)
        {
            // Check if Prosoft is already logged in to the main window
            var isAlreadyLoggedIn = ContainsAny(loginWindow.Title, _options.SuccessWindowTitleContains) ||
                                    Contains(loginWindow.Title, "บริษัท") ||
                                    loginWindow.ClassName == "FNWND380";

            if (isAlreadyLoggedIn)
            {
                Report(progress, "Prosoft เข้าสู่ระบบอยู่แล้ว — ข้ามขั้นตอนกรอกรหัสผ่าน...");
                return LoginResult.Success("Prosoft เข้าสู่ระบบอยู่แล้ว");
            }

            return LoginResult.Error(
                "ไม่พบช่อง Password — กรุณากด Export UI Tree แล้วปรับ passwordSelectors ใน appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Error("กรุณากรอกรหัสผ่านเพื่อเข้าสู่ระบบ Prosoft");
        }

        Report(progress, "กำลังกรอกรหัสผ่าน...");
        SetPassword(passwordElement, password);

        Report(progress, "กำลังค้นหาและกดปุ่ม OK เพื่อเข้าสู่ระบบ...");

        var pwRect = passwordElement.BoundingRectangle;
        var profileTop = profileElement != null && !profileElement.BoundingRectangle.IsEmpty
            ? (int)profileElement.BoundingRectangle.Top
            : (int)pwRect.Bottom + 120;

        // Bring login window to foreground before clicking
        if (loginWindowHandle != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(loginWindowHandle);
        }

        // Strategy 1: Layout-based UIA search between password and profile
        var loginButton = FindOkButtonByLayout(loginWindow, passwordElement, profileElement);

        // Strategy 2: Selector-based UIA search (excluding non-OK buttons like "DB Login")
        if (loginButton is null)
        {
            var rawButton = FindWithStrategies(loginWindow, _options.LoginButtonSelectors);
            if (rawButton is not null &&
                !rawButton.Name.Contains("Login", StringComparison.OrdinalIgnoreCase) &&
                !rawButton.Name.Contains("Admin", StringComparison.OrdinalIgnoreCase) &&
                !rawButton.Name.Contains("Profile", StringComparison.OrdinalIgnoreCase))
            {
                loginButton = rawButton;
            }
        }

        // Strategy 3: Win32 Layout-based child search
        var pwNativeRect = new Win32Native.RECT
        {
            Left = (int)pwRect.Left,
            Top = (int)pwRect.Top,
            Right = (int)pwRect.Right,
            Bottom = (int)pwRect.Bottom
        };
        var okButtonHwnd = loginWindowHandle != IntPtr.Zero
            ? Win32Native.FindOkButtonByLayout(loginWindowHandle, pwNativeRect, profileTop)
            : IntPtr.Zero;

        // Strategy 4: Win32 Text/ID child search
        if (okButtonHwnd == IntPtr.Zero && loginWindowHandle != IntPtr.Zero)
        {
            okButtonHwnd = Win32Native.FindOkButtonHwnd(loginWindowHandle);
        }

        // Execute click via all valid strategies
        if (loginButton is not null)
        {
            Report(progress, $"พบปุ่ม OK '{loginButton.Name}' ({loginButton.ClassName}) กำลังกด (UIA)...");
            InvokeOrClick(loginButton);
        }

        if (okButtonHwnd != IntPtr.Zero)
        {
            Report(progress, $"กำลังกดปุ่ม OK (Win32 HWND 0x{okButtonHwnd.ToInt64():X})...");
            Win32Native.ClickButtonHwnd(okButtonHwnd, loginWindowHandle);
        }

        // Calculate physical screen click coordinates as reliable fallback
        int clickX, clickY;
        if (loginButton is not null && !loginButton.BoundingRectangle.IsEmpty)
        {
            clickX = (int)(loginButton.BoundingRectangle.Left + loginButton.BoundingRectangle.Width / 2);
            clickY = (int)(loginButton.BoundingRectangle.Top + loginButton.BoundingRectangle.Height / 2);
        }
        else if (okButtonHwnd != IntPtr.Zero && Win32Native.GetWindowRect(okButtonHwnd, out var okRect))
        {
            clickX = (okRect.Left + okRect.Right) / 2;
            clickY = (okRect.Top + okRect.Bottom) / 2;
        }
        else
        {
            // OK button is aligned with password field left (1033) + half of button width (~37)
            // Vertically it is between password bottom (481) and profile top (552) -> ~531
            clickX = (int)(pwRect.Left + 37);
            clickY = (int)((pwRect.Bottom + profileTop) / 2);
        }

        Report(progress, $"ส่งคำสั่งคลิกปุ่ม OK ที่ตำแหน่ง ({clickX}, {clickY})...");
        try
        {
            await Win32Native.ClickScreenPointAsync(clickX, clickY, cancellationToken);
            Mouse.LeftClick(new System.Drawing.Point(clickX, clickY));
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[MouseClick ERROR] {ex.Message}");
        }

        // Fallback: send Enter key to trigger default OK button
        try
        {
            Keyboard.Type(VirtualKeyShort.ENTER);
        }
        catch { }

        Report(progress, "ส่งคำสั่ง Login แล้ว กำลังรอผลลัพธ์...");
        return await WaitForResultAsync(
            process,
            application,
            automation,
            loginWindowHandle,
            cancellationToken);
    }

    private async Task<Process> FindProcessAsync(CancellationToken cancellationToken)
    {
        var existing = FindCurrentProcess();
        if (existing is not null)
        {
            return existing;
        }

        var defaultPath = @"C:\Program Files (x86)\Prosoft\myAccount\Bin\myaccount.exe";
        var exePath = !string.IsNullOrWhiteSpace(_options.ExecutablePath) && File.Exists(_options.ExecutablePath)
            ? _options.ExecutablePath
            : (File.Exists(defaultPath) ? defaultPath : null);

        if (!string.IsNullOrEmpty(exePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        var deadline = DateTime.UtcNow.AddSeconds(_options.AttachTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var process = FindCurrentProcess();
            if (process is not null)
            {
                return process;
            }

            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        }

        throw new InvalidOperationException(
            "ไม่พบโปรแกรม Prosoft myAccount ที่เปิดอยู่ กรุณาเปิดโปรแกรม Prosoft ให้แสดงหน้า Login แล้วลองใหม่อีกครั้ง");
    }

    private Process? FindCurrentProcess()
    {
        foreach (var processName in _options.ProcessNames.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process is not null)
            {
                return process;
            }
        }
        return null;
    }

    private async Task<Window> FindLoginWindowAsync(
        Process process,
        Application application,
        UIA3Automation automation,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.AttachTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windows = GetProcessWindows(process, application, automation);

            // 1. Prefer a window that contains the password field (true login window)
            var windowWithPassword = windows.FirstOrDefault(window =>
                FindWithStrategies(window, _options.PasswordSelectors) is not null);

            if (windowWithPassword is not null)
            {
                return windowWithPassword;
            }

            // 2. Window titled "Login"
            var loginTitledWindow = windows.FirstOrDefault(window =>
                Contains(window.Title, "Login") || Contains(window.Title, "เข้าสู่ระบบ"));

            if (loginTitledWindow is not null)
            {
                return loginTitledWindow;
            }

            // 3. If main window is open, Prosoft is already logged in
            var mainWindow = windows.FirstOrDefault(window =>
                ContainsAny(window.Title, _options.SuccessWindowTitleContains) ||
                Contains(window.Title, "บริษัท") ||
                window.ClassName == "FNWND380");

            if (mainWindow is not null)
            {
                return mainWindow;
            }

            if (windows.Count == 1)
            {
                return windows[0];
            }

            var candidate = windows.FirstOrDefault(w => !string.IsNullOrWhiteSpace(w.Title) || !string.IsNullOrWhiteSpace(w.ClassName));
            if (candidate is not null && windows.Count > 0)
            {
                return candidate;
            }

            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        }

        throw new InvalidOperationException(
            "ไม่พบหน้า Login ของ Prosoft กรุณาเปิดหน้า Login แล้วลองใหม่ หรือปรับ loginWindowTitleContains");
    }

    private async Task<LoginResult> WaitForResultAsync(
        Process process,
        Application application,
        UIA3Automation automation,
        IntPtr loginWindowHandle,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.ResultTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windows = GetProcessWindows(process, application, automation);

            var dialog = windows.FirstOrDefault(window =>
                window.Properties.NativeWindowHandle.ValueOrDefault != loginWindowHandle &&
                HasNamedDialogButton(window));

            if (dialog is not null)
            {
                var message = ReadDialogText(dialog);
                return LoginResult.Error(
                    string.IsNullOrWhiteSpace(message)
                        ? "Prosoft แสดง popup หลัง Login กรุณาตรวจข้อความบนหน้าจอ"
                        : $"Prosoft: {message}");
            }

            var successWindow = windows.FirstOrDefault(window =>
                window.Properties.NativeWindowHandle.ValueOrDefault != loginWindowHandle &&
                ContainsAny(window.Title, _options.SuccessWindowTitleContains));

            if (successWindow is not null)
            {
                return LoginResult.Success("Login สำเร็จ — พบหน้าหลักของ Prosoft");
            }

            var loginWindowStillExists = windows.Any(window =>
                window.Properties.NativeWindowHandle.ValueOrDefault == loginWindowHandle);

            if (!loginWindowStillExists)
            {
                return LoginResult.Success("Login สำเร็จ — หน้า Login ปิดแล้ว");
            }

            var currentLoginWindow = windows.FirstOrDefault(window =>
                window.Properties.NativeWindowHandle.ValueOrDefault == loginWindowHandle);

            if (currentLoginWindow is not null &&
                FindWithStrategies(currentLoginWindow, _options.PasswordSelectors) is null)
            {
                return LoginResult.Success(
                    "Login สำเร็จ — หน้าต่างเดิมเปลี่ยนออกจากหน้า Login แล้ว");
            }

            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        }

        return LoginResult.Timeout(
            $"กด Login แล้ว แต่ยังยืนยันผลไม่ได้ภายใน {_options.ResultTimeoutSeconds} วินาที " +
            "กรุณาตรวจหน้าจอ Prosoft และตั้งค่า successWindowTitleContains");
    }

    private string ExportUiTreeCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = FindExistingProcess();
        using var application = Application.Attach(process);
        using var automation = new UIA3Automation();

        var projectLogsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        var outputDirectory = Directory.Exists(projectLogsDirectory)
            ? projectLogsDirectory
            : Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(
            outputDirectory,
            $"ui-tree-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var output = new StringBuilder();
        output.AppendLine("Prosoft UI Tree (metadata only — input values are not collected)");
        output.AppendLine($"CapturedAt: {DateTimeOffset.Now:O}");
        output.AppendLine($"Process: {process.ProcessName} PID={process.Id}");
        output.AppendLine();

        var windows = GetProcessWindows(process, application, automation);
        output.AppendLine($"Found {windows.Count} window(s) for process {process.ProcessName} (PID {process.Id})");
        output.AppendLine();

        foreach (var window in windows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowHandle = window.Properties.NativeWindowHandle.ValueOrDefault;
            output.AppendLine($"Window HWND: 0x{windowHandle.ToInt64():X}, Title: \"{window.Title}\", Class: \"{window.ClassName}\"");

            AppendElement(output, window, 0, cancellationToken);

            if (windowHandle != IntPtr.Zero)
            {
                output.AppendLine("  Win32 Child Windows:");
                Win32Native.EnumChildWindows(windowHandle, (childHwnd, _) =>
                {
                    var cClass = Win32Native.GetClass(childHwnd);
                    var cText = Win32Native.GetText(childHwnd);
                    var parent = Win32Native.GetParent(childHwnd);
                    var isPw = Win32Native.IsPasswordEdit(childHwnd);
                    var isComboChild = Win32Native.IsChildOfComboBox(childHwnd);
                    output.AppendLine($"    - HWND: 0x{childHwnd.ToInt64():X} | Parent: 0x{parent.ToInt64():X} | Class: \"{cClass}\" | Text: \"{(isPw ? "***" : cText)}\" | IsPassword: {isPw} | IsComboChild: {isComboChild}");
                    return true;
                }, IntPtr.Zero);
            }
            output.AppendLine();
        }

        File.WriteAllText(outputPath, output.ToString(), Encoding.UTF8);
        return outputPath;
    }

    private Process FindExistingProcess()
    {
        var proc = FindCurrentProcess();
        if (proc is not null) return proc;

        throw new InvalidOperationException(
            "ไม่พบโปรแกรม Prosoft myAccount ที่เปิดอยู่ กรุณาเปิดโปรแกรม Prosoft ให้แสดงหน้า Login แล้วลองใหม่อีกครั้ง");
    }

    private static List<Window> GetProcessWindows(
        Process process,
        Application application,
        UIA3Automation automation)
    {
        var result = new List<Window>();
        var seenHandles = new HashSet<IntPtr>();

        void TryAdd(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || seenHandles.Contains(hWnd)) return;
            if (!Win32Native.IsWindowVisible(hWnd) && !Win32Native.IsStyleVisible(hWnd)) return;

            try
            {
                var element = automation.FromHandle(hWnd);
                if (element != null)
                {
                    seenHandles.Add(hWnd);
                    result.Add(element.AsWindow());
                }
            }
            catch { }
        }

        try
        {
            foreach (var win in application.GetAllTopLevelWindows(automation))
            {
                var h = win.Properties.NativeWindowHandle.ValueOrDefault;
                if (h != IntPtr.Zero && seenHandles.Add(h))
                {
                    result.Add(win);
                }
            }
        }
        catch { }

        try
        {
            process.Refresh();
            foreach (ProcessThread thread in process.Threads)
            {
                Win32Native.EnumThreadWindows((uint)thread.Id, (hWnd, _) =>
                {
                    TryAdd(hWnd);
                    return true;
                }, IntPtr.Zero);
            }
        }
        catch { }

        try
        {
            Win32Native.EnumWindows((hWnd, _) =>
            {
                Win32Native.GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == process.Id)
                {
                    TryAdd(hWnd);
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        if (result.Count == 0)
        {
            try
            {
                Win32Native.EnumWindows((hWnd, _) =>
                {
                    Win32Native.GetWindowThreadProcessId(hWnd, out var pid);
                    if (pid == process.Id && !seenHandles.Contains(hWnd))
                    {
                        try
                        {
                            var element = automation.FromHandle(hWnd);
                            if (element != null)
                            {
                                seenHandles.Add(hWnd);
                                result.Add(element.AsWindow());
                            }
                        }
                        catch { }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        return result;
    }

    private static AutomationElement? FindWithStrategies(
        AutomationElement root,
        IReadOnlyList<ControlSelector> strategies)
    {
        var elements = new List<AutomationElement>();

        try
        {
            elements.AddRange(root.FindAllDescendants());
        }
        catch
        {
        }

        try
        {
            var rootHandle = root.Properties.NativeWindowHandle.ValueOrDefault;
            if (rootHandle != IntPtr.Zero)
            {
                var automation = root.Automation;
                Win32Native.EnumChildWindows(rootHandle, (childHwnd, _) =>
                {
                    try
                    {
                        var childElement = automation.FromHandle(childHwnd);
                        if (childElement != null && !elements.Any(e => e.Properties.NativeWindowHandle.ValueOrDefault == childHwnd))
                        {
                            elements.Add(childElement);
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
        }
        catch
        {
        }

        foreach (var selector in strategies)
        {
            var matches = elements.Where(element => Matches(element, selector))
                .OrderBy(element =>
                {
                    try { return element.BoundingRectangle.Top; } catch { return 0; }
                })
                .ToArray();

            if (selector.Index >= 0 && selector.Index < matches.Length)
            {
                return matches[selector.Index];
            }
        }

        return null;
    }

    private static void DumpLoginWindowControls(Window loginWindow, IntPtr loginWindowHandle)
    {
        try
        {
            FileLogger.Log($"[DUMP] LoginWindow HWND=0x{loginWindowHandle.ToInt64():X}, Title='{loginWindow.Title}', Class='{loginWindow.ClassName}', Bounds={loginWindow.BoundingRectangle}");

            if (loginWindowHandle != IntPtr.Zero)
            {
                Win32Native.EnumChildWindows(loginWindowHandle, (childHwnd, _) =>
                {
                    var cClass = Win32Native.GetClass(childHwnd);
                    var cText = Win32Native.GetText(childHwnd);
                    var ctrlId = Win32Native.GetDlgCtrlID(childHwnd);
                    Win32Native.GetWindowRect(childHwnd, out var rect);
                    FileLogger.Log($"  [CHILD] HWND=0x{childHwnd.ToInt64():X}, Class='{cClass}', Text='{cText}', Id={ctrlId}, Rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom})");
                    return true;
                }, IntPtr.Zero);
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[DUMP ERROR] {ex.Message}");
        }
    }

    private static AutomationElement? FindOkButtonByLayout(
        Window loginWindow,
        AutomationElement passwordElement,
        AutomationElement? profileElement)
    {
        try
        {
            var pwRect = passwordElement.BoundingRectangle;
            if (pwRect.IsEmpty) return null;

            var profileTop = profileElement != null && !profileElement.BoundingRectangle.IsEmpty
                ? profileElement.BoundingRectangle.Top
                : pwRect.Bottom + 120;

            var allElements = loginWindow.FindAllDescendants();

            // Find buttons strictly between password field and profile field
            var candidates = allElements.Where(e =>
            {
                var r = e.BoundingRectangle;
                if (r.IsEmpty) return false;

                // Must be vertically between password.Bottom and profile.Top (strictly above profile)
                bool verticalMatch = r.Top >= pwRect.Bottom - 5 && r.Bottom <= profileTop - 2;

                // Must be on the right side aligned near password field
                bool horizontalMatch = r.Left >= pwRect.Left - 15;

                // Typical button size: width 45-120, height 18-35
                bool sizeMatch = r.Width >= 45 && r.Width <= 120 && r.Height >= 18 && r.Height <= 35;

                // Reject input fields
                bool notInput = e.ControlType != ControlType.Edit &&
                                e.ControlType != ControlType.ComboBox &&
                                e.ControlType != ControlType.CheckBox;

                // Reject known non-OK labels/buttons
                var name = e.Name?.Trim() ?? string.Empty;
                bool notIgnoredName = !name.Contains("Cancel", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("ยกเลิก", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Profile", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Server", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Register", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Delete", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Demo", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("New", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("User", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Database", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("DBMS", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Login", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("Admin", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Contains("picture", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Equals(">>", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Equals("<<", StringComparison.OrdinalIgnoreCase) &&
                                     !name.Equals("...", StringComparison.OrdinalIgnoreCase);

                return verticalMatch && horizontalMatch && sizeMatch && notInput && notIgnoredName;
            })
            .OrderBy(e => e.BoundingRectangle.Left)
            .ToList();

            FileLogger.Log($"[FindOkButtonByLayout] Found {candidates.Count} candidates in button zone: " +
                string.Join(", ", candidates.Select(c => $"'{c.Name}' ({c.ClassName}, {c.ControlType}, X={c.BoundingRectangle.Left}, Y={c.BoundingRectangle.Top})")));

            // If there's an explicit "OK" or "&OK", pick it
            var okNamed = candidates.FirstOrDefault(c =>
                c.Name.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("&OK", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("ตกลง", StringComparison.OrdinalIgnoreCase));
            if (okNamed != null) return okNamed;

            // Preferred: button horizontally aligned with password field (Left difference <= 15)
            // In Prosoft, Password Edit Left = 1033, OK Button Left = 1033!
            var alignedWithPw = candidates.FirstOrDefault(c =>
                Math.Abs(c.BoundingRectangle.Left - pwRect.Left) <= 15);
            if (alignedWithPw != null) return alignedWithPw;

            // Otherwise, leftmost candidate
            return candidates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[FindOkButtonByLayout ERROR] {ex.Message}");
            return null;
        }
    }

    private static bool Matches(AutomationElement element, ControlSelector selector)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(selector.AutomationId) &&
                !string.Equals(
                    element.AutomationId,
                    selector.AutomationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(selector.NameContains))
            {
                var name = element.Name;
                var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                if (string.IsNullOrWhiteSpace(name) && handle != IntPtr.Zero)
                {
                    name = Win32Native.GetText(handle);
                }

                var cleanName = name?.Replace("&", string.Empty).Trim();
                var cleanTarget = selector.NameContains.Replace("&", string.Empty).Trim();

                if (!Contains(cleanName, cleanTarget))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(selector.ClassNameContains))
            {
                var className = element.ClassName;
                var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                if (string.IsNullOrWhiteSpace(className) && handle != IntPtr.Zero)
                {
                    className = Win32Native.GetClass(handle);
                }

                if (!Contains(className, selector.ClassNameContains))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(selector.ControlType))
            {
                if (!Enum.TryParse<ControlType>(
                        selector.ControlType,
                        ignoreCase: true,
                        out var expectedType) ||
                    element.ControlType != expectedType)
                {
                    var className = element.ClassName ?? string.Empty;
                    var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                    if (string.IsNullOrWhiteSpace(className) && handle != IntPtr.Zero)
                    {
                        className = Win32Native.GetClass(handle);
                    }

                    bool classMatches = false;
                    if (expectedType == ControlType.Edit && (className.Contains("Edit", StringComparison.OrdinalIgnoreCase) || className.Contains("FNEDIT", StringComparison.OrdinalIgnoreCase)))
                    {
                        classMatches = true;
                    }
                    else if (expectedType == ControlType.Button && (className.Contains("Button", StringComparison.OrdinalIgnoreCase) || className.StartsWith("FN", StringComparison.OrdinalIgnoreCase) || element.ControlType == ControlType.Custom || element.ControlType == ControlType.Pane))
                    {
                        classMatches = true;
                    }
                    else if (expectedType == ControlType.ComboBox && (className.Contains("Combo", StringComparison.OrdinalIgnoreCase) || className.Contains("FNCOMBO", StringComparison.OrdinalIgnoreCase)))
                    {
                        classMatches = true;
                    }

                    if (!classMatches)
                    {
                        return false;
                    }
                }
            }

            if (selector.ExcludeComboBoxChildren == true)
            {
                var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                if (handle != IntPtr.Zero && Win32Native.IsChildOfComboBox(handle))
                {
                    return false;
                }
            }

            if (selector.IsPassword.HasValue)
            {
                var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                bool isPw = element.Properties.IsPassword.ValueOrDefault || (handle != IntPtr.Zero && Win32Native.IsPasswordEdit(handle));
                if (isPw != selector.IsPassword.Value)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetProfile(AutomationElement element, string profile)
    {
        var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle != IntPtr.Zero)
        {
            var targetCombo = Win32Native.IsChildOfComboBox(handle) ? Win32Native.GetParent(handle) : handle;
            if (Win32Native.SelectComboBoxItem(targetCombo, profile))
            {
                return;
            }
        }

        try
        {
            if (element.ControlType == ControlType.ComboBox)
            {
                var comboBox = element.AsComboBox();
                var matchingItem = comboBox.Items.FirstOrDefault(item =>
                    string.Equals(item.Text?.Trim(), profile.Trim(), StringComparison.OrdinalIgnoreCase));

                if (matchingItem is not null)
                {
                    matchingItem.Select();
                    return;
                }
            }
        }
        catch
        {
        }

        try
        {
            var textBox = element.AsTextBox();
            textBox.Text = profile;
            return;
        }
        catch
        {
        }

        try
        {
            element.Focus();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Type(profile);
            Keyboard.Type(VirtualKeyShort.TAB);
        }
        catch
        {
        }
    }

    private static void SetUserName(AutomationElement element, string username)
    {
        var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle != IntPtr.Zero)
        {
            var targetCombo = Win32Native.IsChildOfComboBox(handle) ? Win32Native.GetParent(handle) : handle;
            if (Win32Native.SelectComboBoxItem(targetCombo, username))
            {
                return;
            }
        }

        try
        {
            if (element.ControlType == ControlType.ComboBox)
            {
                var comboBox = element.AsComboBox();
                var matchingItem = comboBox.Items.FirstOrDefault(item =>
                    string.Equals(item.Text?.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase));

                if (matchingItem is not null)
                {
                    matchingItem.Select();
                    return;
                }
            }
        }
        catch
        {
        }

        try
        {
            var textBox = element.AsTextBox();
            textBox.Text = username;
            return;
        }
        catch
        {
        }

        try
        {
            element.Focus();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Type(username);
            Keyboard.Type(VirtualKeyShort.TAB);
        }
        catch
        {
        }
    }

    private static void SetPassword(AutomationElement element, string password)
    {
        try
        {
            var textBox = element.AsTextBox();
            textBox.Text = password;
            return;
        }
        catch
        {
            // Some legacy controls do not expose ValuePattern.
        }

        element.Focus();
        try
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.BACK);
        }
        catch
        {
        }

        Keyboard.Type(password);
    }

    private static void InvokeOrClick(AutomationElement element)
    {
        var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle != IntPtr.Zero)
        {
            var parent = Win32Native.GetParent(handle);
            Win32Native.ClickButtonHwnd(handle, parent);
        }

        try
        {
            element.Focus();
        }
        catch { }

        try
        {
            element.AsButton().Invoke();
        }
        catch
        {
        }

        try
        {
            element.Click();
        }
        catch
        {
        }

        if (handle != IntPtr.Zero)
        {
            try
            {
                Win32Native.ClickAt(handle, 10, 10);
            }
            catch { }
        }
    }

    private bool HasNamedDialogButton(Window window)
    {
        try
        {
            return window.FindAllDescendants()
                .Where(element => element.ControlType == ControlType.Button)
                .Any(element => _options.DialogButtonNames.Any(name =>
                    string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            return false;
        }
    }

    private static string ReadDialogText(Window dialog)
    {
        try
        {
            return string.Join(
                " ",
                dialog.FindAllDescendants()
                    .Where(element => element.ControlType == ControlType.Text)
                    .Select(element => element.Name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AppendElement(
        StringBuilder output,
        AutomationElement element,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            output.Append(' ', depth * 2);
            output.Append("- Type=").Append(Safe(() => element.ControlType.ToString()));
            output.Append(" | Name=").Append(Quote(Safe(() => element.Name)));
            output.Append(" | AutomationId=").Append(Quote(Safe(() => element.AutomationId)));
            output.Append(" | ClassName=").Append(Quote(Safe(() => element.ClassName)));
            output.Append(" | Handle=").Append(Safe(() => element.Properties.NativeWindowHandle.ValueOrDefault.ToString()));
            output.AppendLine();

            if (depth >= 12)
            {
                return;
            }

            foreach (var child in element.FindAllChildren())
            {
                AppendElement(output, child, depth + 1, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            output.Append(' ', (depth + 1) * 2);
            output.AppendLine($"[unavailable: {ex.GetType().Name}]");
        }
    }

    private static bool ContainsAny(string? value, IEnumerable<string> candidates)
    {
        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Any(candidate => Contains(value, candidate));
    }

    private static bool Contains(string? value, string candidate)
    {
        return value?.Contains(candidate, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string Safe(Func<string?> valueFactory)
    {
        try
        {
            return valueFactory() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private async Task<NavigationResult> NavigateToCreditPurchaseCoreAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        Report(progress, "กำลังค้นหา Prosoft process เพื่อเปิดหน้าซื้อเชื่อ...");
        using var process = FindExistingProcess();

        using var application = Application.Attach(process);
        using var automation = new UIA3Automation();

        Report(progress, "กำลังค้นหาหน้าต่างหลักของ Prosoft...");
        var mainWindow = await FindMainWindowAsync(
            process,
            application,
            automation,
            cancellationToken);

        var mainWindowHandle = mainWindow.Properties.NativeWindowHandle.ValueOrDefault;
        if (mainWindowHandle != IntPtr.Zero)
        {
            Win32Native.ShowWindow(mainWindowHandle, Win32Native.SW_RESTORE);
            Win32Native.SetForegroundWindow(mainWindowHandle);
        }

        var navOptions = _options.Navigation;
        var targetActionCandidates = navOptions.TargetActionAliases.Count > 0
            ? navOptions.TargetActionAliases
            : [navOptions.TargetAction];

        var moduleCandidates = navOptions.ModuleAliases.Count > 0
            ? navOptions.ModuleAliases
            : [navOptions.ModuleName];

        var subModuleCandidates = navOptions.SubModuleAliases.Count > 0
            ? navOptions.SubModuleAliases
            : [navOptions.SubModuleName];

        // Step 1: Check if Credit Purchase window is already open
        if (IsCreditPurchaseWindowOpen(process, application, automation, targetActionCandidates))
        {
            return NavigationResult.Success($"หน้าต่าง '{navOptions.TargetAction}' เปิดอยู่แล้ว");
        }

        // Step 2: Try native menu command (cleanest and fastest if available)
        Report(progress, "กำลังตรวจสอบเมนู Prosoft...");
        if (Win32Native.TriggerMenuItem(mainWindowHandle, targetActionCandidates))
        {
            Report(progress, $"ส่งคำสั่งเปิด '{navOptions.TargetAction}' ผ่านเมนูแล้ว...");
        }
        else
        {
            // Step 3: Direct search for TargetAction in UI tree
            Report(progress, $"กำลังค้นหาเมนูหรือปุ่ม '{navOptions.TargetAction}' บนหน้าจอ...");
            var targetElement = FindElementByCandidates(mainWindow, targetActionCandidates);

            if (targetElement is not null)
            {
                Report(progress, $"พบเมนู/ปุ่ม '{targetElement.Name}' กำลังเปิด...");
                InvokeOrDoubleClick(targetElement);
            }
            else
            {
                // Step 4: Hierarchical navigation: Module -> SubModule -> TargetAction
                Report(progress, $"กำลังค้นหาโมดูล '{navOptions.ModuleName}'...");
                var moduleElement = FindElementByCandidates(mainWindow, moduleCandidates);
                if (moduleElement is not null)
                {
                    Report(progress, $"เลือกโมดูล '{moduleElement.Name}'...");
                    if (moduleElement.ControlType == ControlType.TreeItem)
                    {
                        var ti = moduleElement.AsTreeItem();
                        try { ti.Expand(); } catch { }
                        try { ti.Select(); } catch { }
                        ti.Click();
                    }
                    else
                    {
                        InvokeOrClick(moduleElement);
                    }
                    await Task.Delay(800, cancellationToken);
                }

                Report(progress, $"กำลังค้นหาหมวด '{navOptions.SubModuleName}'...");
                var subModuleElement = FindElementByCandidates(mainWindow, subModuleCandidates);
                if (subModuleElement is not null)
                {
                    Report(progress, $"เลือกหมวด '{subModuleElement.Name}'...");
                    if (subModuleElement.ControlType == ControlType.TreeItem)
                    {
                        var ti = subModuleElement.AsTreeItem();
                        try { ti.Select(); } catch { }
                        ti.Click();
                        await Task.Delay(150, cancellationToken);
                        ti.DoubleClick();
                    }
                    else
                    {
                        InvokeOrDoubleClick(subModuleElement);
                    }
                    await Task.Delay(1200, cancellationToken);
                }

                // Search for target action again after expanding/selecting
                Report(progress, $"กำลังค้นหา '{navOptions.TargetAction}' ในหมวด...");
                targetElement = FindElementByCandidates(mainWindow, targetActionCandidates);
                if (targetElement is not null)
                {
                    Report(progress, $"พบ '{targetElement.Name}' กำลังเปิด...");
                    InvokeOrDoubleClick(targetElement);
                }
                else
                {
                    // Step 5: Visual detection and clicking of flowchart card on the right pane
                    Report(progress, $"กำลังค้นหาการ์ด '{navOptions.TargetAction}' บน Workflow Diagram...");
                    var cardClicked = await ClickFlowchartCardAsync(mainWindow, mainWindowHandle, cancellationToken);
                    if (!cardClicked)
                    {
                        return NavigationResult.Error(
                            $"ไม่พบเมนูหรือการ์ด '{navOptions.TargetAction}' ในหน้าจอ Prosoft กรุณาตรวจสอบตำแหน่งเมนูบนหน้าจอ");
                    }
                }
            }
        }

        // Step 6: Verify result
        Report(progress, $"ส่งคำสั่งเปิด '{navOptions.TargetAction}' แล้ว กำลังรอหน้าต่างเปิด...");
        var opened = await WaitForCreditPurchaseWindowAsync(
            process,
            application,
            automation,
            mainWindow,
            targetActionCandidates,
            navOptions.NavigationTimeoutSeconds,
            cancellationToken);

        if (opened)
        {
            return NavigationResult.Success($"เปิดหน้าต่าง '{navOptions.TargetAction}' สำเร็จ");
        }

        return NavigationResult.Error(
            $"ส่งคำสั่งเปิด '{navOptions.TargetAction}' แล้ว แต่ไม่พบหน้าต่าง '{navOptions.TargetAction}' เปิดขึ้นมาภายใน {navOptions.NavigationTimeoutSeconds} วินาที");
    }

    private async Task<Window> FindMainWindowAsync(
        Process process,
        Application application,
        UIA3Automation automation,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.AttachTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windows = GetProcessWindows(process, application, automation);

            // 1. Main window matching SuccessWindowTitleContains, "บริษัท", or FNWND380 without password field
            var mainWindow = windows.FirstOrDefault(window =>
                (ContainsAny(window.Title, _options.SuccessWindowTitleContains) ||
                 Contains(window.Title, "บริษัท") ||
                 window.ClassName == "FNWND380") &&
                FindWithStrategies(window, _options.PasswordSelectors) is null);

            if (mainWindow is not null)
            {
                return mainWindow;
            }

            // 2. Any window with non-empty title that is not login or dialog
            var candidate = windows.FirstOrDefault(w =>
                !string.IsNullOrWhiteSpace(w.Title) &&
                !Contains(w.Title, "Login") &&
                !HasNamedDialogButton(w));

            if (candidate is not null)
            {
                return candidate;
            }

            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        }

        var anyWindows = GetProcessWindows(process, application, automation);
        if (anyWindows.Count > 0)
        {
            return anyWindows[0];
        }

        throw new InvalidOperationException(
            "ไม่พบหน้าต่างหลักของ Prosoft กรุณาตรวจสอบว่าโปรแกรม Prosoft ได้ Login เข้าสู่ระบบเรียบร้อยแล้ว");
    }

    private static AutomationElement? FindElementByCandidates(
        AutomationElement root,
        IEnumerable<string> candidates)
    {
        var candidateList = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (candidateList.Count == 0) return null;

        var allElements = new List<AutomationElement>();
        try
        {
            allElements.AddRange(root.FindAllDescendants());
        }
        catch { }

        try
        {
            var rootHandle = root.Properties.NativeWindowHandle.ValueOrDefault;
            if (rootHandle != IntPtr.Zero)
            {
                var automation = root.Automation;
                Win32Native.EnumChildWindows(rootHandle, (childHwnd, _) =>
                {
                    try
                    {
                        var childElement = automation.FromHandle(childHwnd);
                        if (childElement != null)
                        {
                            if (!allElements.Any(e => e.Properties.NativeWindowHandle.ValueOrDefault == childHwnd))
                            {
                                allElements.Add(childElement);
                            }

                            try
                            {
                                allElements.AddRange(childElement.FindAllDescendants());
                            }
                            catch { }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
        }
        catch { }

        // First: exact or startsWith matches on TreeItem, MenuItem, Button, ListItem, TabItem, Pane, Text
        foreach (var candidate in candidateList)
        {
            var match = allElements.FirstOrDefault(e =>
                e.Name != null &&
                (string.Equals(e.Name.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 e.Name.Trim().StartsWith(candidate.Trim(), StringComparison.OrdinalIgnoreCase)));

            if (match is not null)
            {
                return match;
            }
        }

        // Second: contains match on interactive controls
        foreach (var candidate in candidateList)
        {
            var match = allElements.FirstOrDefault(e =>
                Contains(e.Name, candidate) &&
                (e.ControlType == ControlType.TreeItem ||
                 e.ControlType == ControlType.MenuItem ||
                 e.ControlType == ControlType.Button ||
                 e.ControlType == ControlType.ListItem ||
                 e.ControlType == ControlType.TabItem ||
                 e.ControlType == ControlType.Hyperlink ||
                 e.ControlType == ControlType.Text ||
                 e.ControlType == ControlType.Pane ||
                 e.ControlType == ControlType.Custom));

            if (match is not null)
            {
                return match;
            }
        }

        // Third: contains match on any element
        foreach (var candidate in candidateList)
        {
            var match = allElements.FirstOrDefault(e => Contains(e.Name, candidate));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static void InvokeOrDoubleClick(AutomationElement element)
    {
        try
        {
            element.Focus();
        }
        catch { }

        try
        {
            if (element.ControlType == ControlType.Button)
            {
                element.AsButton().Invoke();
                return;
            }

            if (element.ControlType == ControlType.MenuItem)
            {
                element.AsMenuItem().Invoke();
                return;
            }

            if (element.ControlType == ControlType.TreeItem)
            {
                var treeItem = element.AsTreeItem();
                treeItem.Select();
                treeItem.DoubleClick();
                return;
            }
        }
        catch { }

        try
        {
            element.Click();
        }
        catch { }

        try
        {
            element.DoubleClick();
        }
        catch { }

        try
        {
            var parent = element.Parent;
            if (parent != null && parent.ControlType != ControlType.Window)
            {
                parent.Click();
                parent.DoubleClick();
            }
        }
        catch { }
    }

    private static async Task<bool> ClickFlowchartCardAsync(
        Window mainWindow,
        IntPtr mainWindowHandle,
        CancellationToken cancellationToken)
    {
        try
        {
            if (mainWindowHandle != IntPtr.Zero)
            {
                Win32Native.ShowWindow(mainWindowHandle, Win32Native.SW_RESTORE);
                Win32Native.SetForegroundWindow(mainWindowHandle);
                await Task.Delay(200, cancellationToken);
            }

            // Find PBListView32_80 and PBTreeView32_80 control bounds
            var listViewHwnd = IntPtr.Zero;
            var listRect = new Win32Native.RECT();
            var treeHwnd = IntPtr.Zero;
            var treeRect = new Win32Native.RECT();

            if (mainWindowHandle != IntPtr.Zero)
            {
                Win32Native.EnumChildWindows(mainWindowHandle, (childHwnd, _) =>
                {
                    var cls = Win32Native.GetClass(childHwnd);
                    if (cls.Contains("PBListView", StringComparison.OrdinalIgnoreCase) ||
                        cls.Contains("ListView", StringComparison.OrdinalIgnoreCase))
                    {
                        listViewHwnd = childHwnd;
                        Win32Native.GetWindowRect(childHwnd, out listRect);
                    }
                    if (cls.Contains("PBTreeView", StringComparison.OrdinalIgnoreCase) ||
                        cls.Contains("TreeView", StringComparison.OrdinalIgnoreCase))
                    {
                        treeHwnd = childHwnd;
                        Win32Native.GetWindowRect(childHwnd, out treeRect);
                    }
                    return true;
                }, IntPtr.Zero);
            }

            int flowLeft, flowTop, flowWidth, flowHeight;
            if (listViewHwnd != IntPtr.Zero && (listRect.Right - listRect.Left) > 100)
            {
                flowLeft = listRect.Left;
                flowTop = listRect.Top;
                flowWidth = listRect.Right - listRect.Left;
                flowHeight = listRect.Bottom - listRect.Top;
                FileLogger.Log($"[ClickFlowchartCard] Found PBListView HWND=0x{listViewHwnd.ToInt64():X} Rect=({listRect.Left},{listRect.Top},{listRect.Right},{listRect.Bottom})");
            }
            else
            {
                Win32Native.GetWindowRect(mainWindowHandle, out var mainRect);
                flowLeft = (treeHwnd != IntPtr.Zero && treeRect.Right > mainRect.Left)
                    ? treeRect.Right + 2
                    : mainRect.Left + 250;
                flowTop = (treeHwnd != IntPtr.Zero && treeRect.Top >= mainRect.Top)
                    ? treeRect.Top
                    : mainRect.Top + 60;
                flowWidth = mainRect.Right - 5 - flowLeft;
                flowHeight = (treeHwnd != IntPtr.Zero && treeRect.Bottom > flowTop)
                    ? treeRect.Bottom - flowTop
                    : mainRect.Bottom - 30 - flowTop;
            }

            if (flowWidth < 100 || flowHeight < 100)
            {
                FileLogger.Log($"[ClickFlowchartCard] Invalid flowchart pane size: {flowWidth}x{flowHeight}");
                return false;
            }

            FileLogger.Log($"[ClickFlowchartCard] Flowchart pane bounds: Left={flowLeft}, Top={flowTop}, Width={flowWidth}, Height={flowHeight}");

            // Capture screenshot of flowchart pane
            using var bmp = new System.Drawing.Bitmap(flowWidth, flowHeight);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(flowLeft, flowTop, 0, 0, new System.Drawing.Size(flowWidth, flowHeight));
            }

            // Try visual detection via orange header bars
            var relativePt = FlowchartDetector.FindCreditPurchaseCard(bmp);
            int targetScreenX, targetScreenY;

            if (relativePt.HasValue)
            {
                targetScreenX = flowLeft + relativePt.Value.X;
                targetScreenY = flowTop + relativePt.Value.Y;
                FileLogger.Log($"[ClickFlowchartCard] Visual detector found 'ซื้อเชื่อ' card at screen ({targetScreenX}, {targetScreenY})");
            }
            else
            {
                // Geometric fallback
                var propPt = FlowchartDetector.GetProportionalCreditPurchasePoint(flowWidth, flowHeight);
                targetScreenX = flowLeft + propPt.X;
                targetScreenY = flowTop + propPt.Y;
                FileLogger.Log($"[ClickFlowchartCard] Visual detector did not find bars; using proportional screen ({targetScreenX}, {targetScreenY})");
            }

            if (listViewHwnd != IntPtr.Zero)
            {
                Win32Native.SetFocus(listViewHwnd);
            }

            // Move cursor to card and perform clean double-click
            FileLogger.Log($"[ClickFlowchartCard] Sending double-click to screen ({targetScreenX}, {targetScreenY})...");
            Win32Native.SetCursorPos(targetScreenX, targetScreenY);
            await Task.Delay(80, cancellationToken);
            await Win32Native.DoubleClickScreenPointAsync(targetScreenX, targetScreenY, cancellationToken);

            // Also post message double-click to PBListView as dual backup
            if (listViewHwnd != IntPtr.Zero)
            {
                int clientX = targetScreenX - listRect.Left;
                int clientY = targetScreenY - listRect.Top;
                var lParam = (IntPtr)((clientY << 16) | (clientX & 0xFFFF));
                Win32Native.PostMessage(listViewHwnd, Win32Native.WM_LBUTTONDOWN, (IntPtr)1, lParam);
                Win32Native.PostMessage(listViewHwnd, Win32Native.WM_LBUTTONUP, IntPtr.Zero, lParam);
                Win32Native.PostMessage(listViewHwnd, Win32Native.WM_LBUTTONDBLCLK, (IntPtr)1, lParam);
                Win32Native.PostMessage(listViewHwnd, Win32Native.WM_LBUTTONUP, IntPtr.Zero, lParam);
            }

            return true;
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[ClickFlowchartCard ERROR] {ex.Message}");
            return false;
        }
    }

    private static bool IsCreditPurchaseWindowOpen(
        Process process,
        Application application,
        UIA3Automation automation,
        IEnumerable<string> targetActionCandidates)
    {
        try
        {
            var windows = GetProcessWindows(process, application, automation);
            return windows.Any(w =>
                ContainsAny(w.Title, targetActionCandidates) ||
                Contains(w.Title, "Credit Purchase") ||
                Contains(w.Title, "ซื้อเชื่อ") ||
                Contains(w.Title, "ใบรับสินค้า"));
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForCreditPurchaseWindowAsync(
        Process process,
        Application application,
        UIA3Automation automation,
        Window mainWindow,
        IEnumerable<string> targetActionCandidates,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var mainHwnd = mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windows = GetProcessWindows(process, application, automation);
            var foundWindow = windows.FirstOrDefault(w =>
                w.Properties.NativeWindowHandle.ValueOrDefault != mainHwnd &&
                (ContainsAny(w.Title, targetActionCandidates) ||
                 Contains(w.Title, "Credit Purchase") ||
                 Contains(w.Title, "ซื้อเชื่อ") ||
                 Contains(w.Title, "ใบรับสินค้า")));

            if (foundWindow is not null)
            {
                return true;
            }

            try
            {
                var childWindow = mainWindow.FindAllDescendants().FirstOrDefault(e =>
                    (e.ControlType == ControlType.Window || e.ControlType == ControlType.Pane || e.ControlType == ControlType.TabItem) &&
                    (ContainsAny(e.Name, targetActionCandidates) ||
                     Contains(e.Name, "Credit Purchase") ||
                     Contains(e.Name, "ซื้อเชื่อ") ||
                     Contains(e.Name, "ใบรับสินค้า")));

                if (childWindow is not null)
                {
                    return true;
                }
            }
            catch { }

            if (mainHwnd != IntPtr.Zero)
            {
                bool foundChild = false;
                Win32Native.EnumChildWindows(mainHwnd, (childHwnd, _) =>
                {
                    var cText = Win32Native.GetText(childHwnd);
                    if (ContainsAny(cText, targetActionCandidates) ||
                        Contains(cText, "ซื้อเชื่อ") ||
                        Contains(cText, "Credit Purchase") ||
                        Contains(cText, "ใบรับสินค้า/ซื้อเชื่อ") ||
                        Contains(cText, "ใบรับสินค้า"))
                    {
                        foundChild = true;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                if (foundChild)
                {
                    return true;
                }
            }

            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        }

        return false;
    }

    private async Task<VendorFillResult> FillVendorFromCsvCoreAsync(
        string? csvPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var vOptions = _options.VendorInput;
        var effectivePath = string.IsNullOrWhiteSpace(csvPath) ? vOptions.CsvPath : csvPath;

        Report(progress, $"กำลังอ่านข้อมูลผู้ขายจาก '{effectivePath}'...");
        VendorCsvRecord? vendorRow;
        try
        {
            vendorRow = VendorCsvReader.ReadFirstVendor(
                effectivePath,
                vOptions.VendorNameColumn,
                vOptions.VendorCodeColumn,
                vOptions.DocumentNumberColumn,
                vOptions.TaxInvoiceNumberColumn,
                vOptions.DeliveryOrderNumberColumn,
                vOptions.ItemCodeColumn,
                vOptions.QuantityColumn,
                vOptions.UnitPriceColumn,
                vOptions.WarehouseColumn,
                vOptions.LocationColumn,
                vOptions.DiscountColumn);
        }
        catch (Exception ex)
        {
            return VendorFillResult.Error($"อ่านไฟล์ CSV ไม่สำเร็จ: {ex.Message}");
        }

        if (vendorRow is null || string.IsNullOrWhiteSpace(vendorRow.VendorName))
        {
            return VendorFillResult.Error($"ไม่พบข้อมูลผู้ขายในไฟล์ CSV '{effectivePath}' (ตรวจสอบว่ามีคอลัมน์ '{vOptions.VendorNameColumn}')");
        }

        var targetVendorName = vendorRow.VendorName.Trim();
        Report(progress, $"ผู้ขายเป้าหมาย: '{targetVendorName}'");

        Report(progress, "กำลังค้นหา Prosoft process...");
        using var process = FindExistingProcess();
        using var application = Application.Attach(process);
        using var automation = new UIA3Automation();

        Report(progress, "กำลังค้นหาหน้าต่างหลักของ Prosoft...");
        var mainWindow = await FindMainWindowAsync(process, application, automation, cancellationToken);
        var mainWindowHandle = mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

        if (mainWindowHandle != IntPtr.Zero)
        {
            Win32Native.ShowWindow(mainWindowHandle, Win32Native.SW_RESTORE);
            Win32Native.SetForegroundWindow(mainWindowHandle);
            await Task.Delay(200, cancellationToken);
        }

        // Step 1: Ensure "ซื้อเชื่อ" is open
        var navOptions = _options.Navigation;
        var targetActionCandidates = navOptions.TargetActionAliases.Count > 0
            ? navOptions.TargetActionAliases
            : [navOptions.TargetAction];

        if (!IsCreditPurchaseWindowOpen(process, application, automation, targetActionCandidates))
        {
            Report(progress, "หน้าซื้อเชื่อยังไม่เปิด กำลังเปิดหน้าซื้อเชื่อ...");
            var navResult = await NavigateToCreditPurchaseCoreAsync(progress, cancellationToken);
            if (!navResult.IsSuccess)
            {
                return VendorFillResult.Error($"ไม่สามารถเปิดหน้าซื้อเชื่อได้: {navResult.Message}");
            }
            await Task.Delay(800, cancellationToken);
        }

        // Step 2: Locate Credit Purchase MDI child window
        Report(progress, "กำลังค้นหาหน้าต่างซื้อเชื่อ (MDI Sheet)...");
        var (childHwnd, childRect, purchaseElem) = FindCreditPurchaseWindowInfo(mainWindow, mainWindowHandle);
        if (childHwnd == IntPtr.Zero && (childRect.Right - childRect.Left) <= 0)
        {
            return VendorFillResult.Error("พบหน้าหลักของ Prosoft แต่ไม่พบหน้าต่างย่อยซื้อเชื่อ");
        }

        Report(progress, $"พบหน้าต่างซื้อเชื่อ HWND=0x{childHwnd.ToInt64():X} Rect=({childRect.Left},{childRect.Top},{childRect.Right},{childRect.Bottom})");

        // Activate child window
        Win32Native.SetForegroundWindow(mainWindowHandle);
        if (childHwnd != IntPtr.Zero && childHwnd != mainWindowHandle)
        {
            Win32Native.SetForegroundWindow(childHwnd);
        }
        await Win32Native.ClickScreenPointAsync(childRect.Left + 80, childRect.Top + 15, cancellationToken);
        await Task.Delay(200, cancellationToken);

        // Step 3: Open Find dialog
        IntPtr findDialogHwnd = FindVendorSearchDialogHwnd(process);
        if (findDialogHwnd == IntPtr.Zero)
        {
            Report(progress, "กำลังเปิดหน้าต่างค้นหารหัสผู้ขาย (F2)...");
            findDialogHwnd = await OpenVendorSearchDialogAsync(childHwnd, childRect, purchaseElem, process, cancellationToken);
        }

        if (findDialogHwnd == IntPtr.Zero)
        {
            return VendorFillResult.Error("ไม่สามารถเปิดหน้าต่าง 'Find รหัสผู้ขาย' ได้ กรุณาตรวจว่าหน้าซื้อเชื่ออยู่ในโหมดเพิ่มข้อมูล (New)");
        }

        Report(progress, $"พบหน้าต่างค้นหาผู้ขาย HWND=0x{findDialogHwnd.ToInt64():X} กำลังค้นหา '{targetVendorName}'...");
        Win32Native.ShowWindow(findDialogHwnd, Win32Native.SW_RESTORE);
        Win32Native.SetForegroundWindow(findDialogHwnd);
        await Task.Delay(300, cancellationToken);

        // Step 4: In Find dialog, set Search by = "ชื่อผู้ขาย", enter Text = targetVendorName, press F2, select result
        var fillResult = await OperateVendorSearchDialogAsync(findDialogHwnd, targetVendorName, process, cancellationToken);
        if (!fillResult.IsSuccess)
        {
            Report(progress, fillResult.Message);
            return fillResult;
        }

        Report(progress, $"กรอกผู้ขาย '{targetVendorName}' สำเร็จ...");
        await Task.Delay(300, cancellationToken);

        // Ensure Credit Purchase window is focused and update bounds
        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.GetWindowRect(childHwnd, out childRect);
            Win32Native.SetForegroundWindow(mainWindowHandle);
            Win32Native.SetForegroundWindow(childHwnd);
        }

        // Step 5: Switch to "More" tab
        Report(progress, "กำลังเลือกแท็บ 'More'...");
        bool tabSwitched = await SwitchToMoreTabAsync(childHwnd, childRect, purchaseElem, cancellationToken);
        if (tabSwitched)
        {
            Report(progress, "สลับไปแท็บ More เรียบร้อย กำลังตั้งค่ารหัสกลุ่มภาษีเป็น NOVAT...");
        }

        // Step 6: Set "รหัสกลุ่มภาษี" to "NOVAT"
        Report(progress, "กำลังเลือก รหัสกลุ่มภาษี = 'NOVAT'...");
        bool taxSet = await SetTaxGroupToNovatAsync(childHwnd, childRect, purchaseElem, process, cancellationToken);
        if (taxSet)
        {
            Report(progress, "กำหนดรหัสกลุ่มภาษีเป็น NOVAT สำเร็จ");
        }
        await Task.Delay(300, cancellationToken);

        // Ensure Credit Purchase window is focused and update bounds before filling doc fields
        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.GetWindowRect(childHwnd, out childRect);
            Win32Native.SetForegroundWindow(mainWindowHandle);
            Win32Native.SetForegroundWindow(childHwnd);
        }

        // Step 7: Fill Document Fields (เลขที่เอกสาร, เลขที่ใบกำกับ, เลขที่ใบส่งของ)
        await FillDocumentFieldsAsync(childHwnd, childRect, vendorRow, process, progress, cancellationToken);
        await Task.Delay(300, cancellationToken);

        // Step 8: Switch back to "Detail" tab and fill items
        Report(progress, "กำลังสลับกลับไปแท็บ Detail...");
        await SwitchToDetailTabAsync(childHwnd, childRect, purchaseElem, cancellationToken);
        await Task.Delay(400, cancellationToken);

        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.GetWindowRect(childHwnd, out childRect);
            Win32Native.SetForegroundWindow(mainWindowHandle);
            Win32Native.SetForegroundWindow(childHwnd);
        }

        if (vendorRow.Items != null && vendorRow.Items.Count > 0)
        {
            await FillDetailItemsAsync(childHwnd, childRect, vendorRow.Items, progress, cancellationToken);
            await Task.Delay(300, cancellationToken);
        }
        else
        {
            FileLogger.Log("[FillDetailItems] Notice: No detail items found in CSV input file.");
        }

        // Step 9: GL Tab, Post search [>], Checkbox "แก้ไข GL", Department "INTER" for all rows, and Save
        Report(progress, "กำลังสลับไปแท็บ GL...");
        var glResult = await ProcessGlAndSaveCoreAsync(
            childHwnd,
            childRect,
            purchaseElem,
            process,
            _options.Gl.DefaultDepartment,
            _options.Gl.AutoSaveAfterGl,
            progress,
            vendorRow,
            cancellationToken);

        var docInfo = string.IsNullOrWhiteSpace(vendorRow.DocumentNumber) ? "" : $", เลขที่เอกสาร: {vendorRow.DocumentNumber}";
        var invInfo = string.IsNullOrWhiteSpace(vendorRow.TaxInvoiceNumber) ? "" : $", เลขที่ใบกำกับ: {vendorRow.TaxInvoiceNumber}";
        var doInfo = string.IsNullOrWhiteSpace(vendorRow.DeliveryOrderNumber) ? "" : $", เลขที่ใบส่งของ: {vendorRow.DeliveryOrderNumber}";
        var itemCount = vendorRow.Items?.Count ?? 0;
        var itemInfo = itemCount > 0 ? $", รายการสินค้า {itemCount} รายการในแท็บ Detail" : "";
        var glInfo = glResult.IsSuccess ? $", ดำเนินการแท็บ GL (แผนก {_options.Gl.DefaultDepartment}) และบันทึกข้อมูลเรียบร้อย" : "";
        var finalMsg = $"กรอกข้อมูลผู้ขาย '{targetVendorName}' กำหนดแท็บ More (รหัสกลุ่มภาษี: NOVAT) กรอกเอกสาร{docInfo}{invInfo}{doInfo}{itemInfo}{glInfo} สำเร็จ";
        Report(progress, finalMsg);
        return VendorFillResult.Success(finalMsg, targetVendorName);
    }

    private async Task<IntPtr> OpenVendorSearchDialogAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        Process process,
        CancellationToken cancellationToken)
    {
        // On the 789x479 'ซื้อเชื่อ' MDI window:
        // Edit box: X = 85..192 (Center X = childRect.Left + 138), Y = 65..79 (Center Y = childRect.Top + 73)
        // Dropdown arrow [ v ]: X = 193..209 (Center X = childRect.Left + 199), Center Y = childRect.Top + 73
        int arrowX = childRect.Left + 199;
        int arrowY = childRect.Top + 73;
        int editX = childRect.Left + 138;
        int editY = childRect.Top + 73;

        var dlg = FindVendorSearchDialogHwnd(process);
        if (dlg != IntPtr.Zero) return dlg;

        // Try 1: Click directly inside edit box to focus it and send F2
        FileLogger.Log($"[OpenVendorSearch] Clicking edit box at ({editX}, {editY}) and sending F2...");
        await Win32Native.ClickScreenPointAsync(editX, editY, cancellationToken);
        await Task.Delay(200, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_F2, cancellationToken);
        dlg = await WaitForVendorSearchDialogAsync(process, TimeSpan.FromSeconds(1.5), cancellationToken);
        if (dlg != IntPtr.Zero) return dlg;

        // Try 2: Click directly on dropdown arrow [ v ]
        FileLogger.Log($"[OpenVendorSearch] Clicking dropdown arrow at ({arrowX}, {arrowY})...");
        await Win32Native.ClickScreenPointAsync(arrowX, arrowY, cancellationToken);
        dlg = await WaitForVendorSearchDialogAsync(process, TimeSpan.FromSeconds(1.5), cancellationToken);
        if (dlg != IntPtr.Zero) return dlg;

        // Try 3: Window may be in browse/view mode, so click 'New' (เพิ่มรายการ) first!
        FileLogger.Log("[OpenVendorSearch] Dialog not opened yet. Attempting to click 'New' (เพิ่มรายการ)...");
        await EnsureNewDocumentModeAsync(childHwnd, childRect, purchaseElem, cancellationToken);
        await Task.Delay(500, cancellationToken);

        // Focus edit box and send F2 again
        FileLogger.Log($"[OpenVendorSearch] After New mode: Clicking edit box at ({editX}, {editY}) and sending F2...");
        await Win32Native.ClickScreenPointAsync(editX, editY, cancellationToken);
        await Task.Delay(200, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_F2, cancellationToken);
        dlg = await WaitForVendorSearchDialogAsync(process, TimeSpan.FromSeconds(2.0), cancellationToken);
        if (dlg != IntPtr.Zero) return dlg;

        // Click dropdown arrow again
        FileLogger.Log($"[OpenVendorSearch] After New mode: Clicking dropdown arrow at ({arrowX}, {arrowY})...");
        await Win32Native.ClickScreenPointAsync(arrowX, arrowY, cancellationToken);
        dlg = await WaitForVendorSearchDialogAsync(process, TimeSpan.FromSeconds(2.0), cancellationToken);
        if (dlg != IntPtr.Zero) return dlg;

        // Try 4: Double click inside edit box + F2
        FileLogger.Log($"[OpenVendorSearch] Fallback: Double clicking edit box at ({editX}, {editY}) and sending F2...");
        await Win32Native.DoubleClickScreenPointAsync(editX, editY, cancellationToken);
        await Task.Delay(200, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_F2, cancellationToken);
        dlg = await WaitForVendorSearchDialogAsync(process, TimeSpan.FromSeconds(2.0), cancellationToken);

        return dlg;
    }

    private async Task<IntPtr> WaitForVendorSearchDialogAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            var dlg = FindVendorSearchDialogHwnd(process);
            if (dlg != IntPtr.Zero) return dlg;
            await Task.Delay(150, cancellationToken);
        }
        return IntPtr.Zero;
    }

    private async Task EnsureNewDocumentModeAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        CancellationToken cancellationToken)
    {
        // 1. Try FlaUI UIA button 'New' or 'เพิ่ม'
        if (purchaseElem is not null)
        {
            try
            {
                var newBtn = purchaseElem.FindFirstDescendant(cf =>
                    cf.ByName("New").Or(cf.ByName("เพิ่ม")));
                if (newBtn is not null)
                {
                    FileLogger.Log($"[EnsureNewMode] Found UIA New button: Name='{newBtn.Name}'");
                    if (newBtn.Patterns.Invoke.IsSupported)
                    {
                        newBtn.Patterns.Invoke.Pattern.Invoke();
                    }
                    else
                    {
                        newBtn.Click();
                    }
                    await Task.Delay(300, cancellationToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[EnsureNewMode] UIA New button click error: {ex.Message}");
            }
        }

        // 2. Check if child window has Id=2003 (standard New button in Prosoft data windows) or text "New"
        IntPtr newBtnHwnd = IntPtr.Zero;
        Win32Native.EnumChildWindows(childHwnd, (h, _) =>
        {
            var id = Win32Native.GetDlgCtrlID(h);
            var txt = Win32Native.GetText(h);
            if (id == 2003 || txt.Equals("New", StringComparison.OrdinalIgnoreCase))
            {
                newBtnHwnd = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (newBtnHwnd != IntPtr.Zero)
        {
            Win32Native.GetWindowRect(newBtnHwnd, out var bRect);
            int cx = (bRect.Left + bRect.Right) / 2;
            int cy = (bRect.Top + bRect.Bottom) / 2;
            FileLogger.Log($"[EnsureNewMode] Found New button HWND=0x{newBtnHwnd.ToInt64():X} at ({cx}, {cy}). Clicking...");
            await Win32Native.ClickScreenPointAsync(cx, cy, cancellationToken);
            await Task.Delay(300, cancellationToken);
            return;
        }

        // 3. Fallback: Click at toolbar position (bottom left: childRect.Left + 45, childRect.Bottom - 20)
        int fallbackX = childRect.Left + 45;
        int fallbackY = childRect.Bottom - 20;
        FileLogger.Log($"[EnsureNewMode] Clicking toolbar New button at ({fallbackX}, {fallbackY})...");
        await Win32Native.ClickScreenPointAsync(fallbackX, fallbackY, cancellationToken);

        // 4. Also send Ctrl+N as backup
        await Task.Delay(200, cancellationToken);
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_N, cancellationToken);
    }

    private async Task<VendorFillResult> OperateVendorSearchDialogAsync(
        IntPtr findDialogHwnd,
        string targetVendorName,
        Process process,
        CancellationToken cancellationToken)
    {
        // 1. Gather all child controls of the Find dialog
        var children = new List<(IntPtr Hwnd, string ClassName, string Text, int Id, Win32Native.RECT Rect)>();
        Win32Native.EnumChildWindows(findDialogHwnd, (h, _) =>
        {
            var cls = Win32Native.GetClass(h);
            var txt = Win32Native.GetText(h);
            var id = Win32Native.GetDlgCtrlID(h);
            Win32Native.GetWindowRect(h, out var r);
            children.Add((h, cls, txt, id, r));
            return true;
        }, IntPtr.Zero);

        FileLogger.Log($"[OperateVendorSearch] Find dialog has {children.Count} children");
        foreach (var ch in children.Where(c => c.ClassName.Contains("Combo", StringComparison.OrdinalIgnoreCase) ||
                                               c.ClassName.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
                                               c.ClassName.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                                               c.ClassName.Contains("pbdw", StringComparison.OrdinalIgnoreCase)))
        {
            FileLogger.Log($"   [FindChild] HWND=0x{ch.Hwnd.ToInt64():X} Class='{ch.ClassName}' Id={ch.Id} Text='{ch.Text}' Rect=({ch.Rect.Left},{ch.Rect.Top},{ch.Rect.Right},{ch.Rect.Bottom})");
        }

        // 2. Identify ComboBox for "Search by" (Id=1015 in Prosoft Find dialog)
        var searchByCandidate = children.FirstOrDefault(c => c.ClassName.Equals("ComboBox", StringComparison.OrdinalIgnoreCase) && c.Id == 1015);
        if (searchByCandidate.Hwnd == IntPtr.Zero)
        {
            var comboBoxes = children
                .Where(c => c.ClassName.Equals("ComboBox", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Rect.Top)
                .ToList();
            if (comboBoxes.Count >= 2) searchByCandidate = comboBoxes[1];
            else if (comboBoxes.Count == 1) searchByCandidate = comboBoxes[0];
        }

        if (searchByCandidate.Hwnd != IntPtr.Zero)
        {
            await SetSearchByToVendorNameAsync(findDialogHwnd, searchByCandidate, cancellationToken);
        }

        // 3. Identify Edit box for "Text" (Id=1018 in Prosoft Find dialog)
        // Must ignore 0x0 dummy internal controls inside the DataWindow
        var textEditCandidate = children.FirstOrDefault(c => c.ClassName.Equals("Edit", StringComparison.OrdinalIgnoreCase) && c.Id == 1018);
        if (textEditCandidate.Hwnd == IntPtr.Zero)
        {
            var validEdits = children
                .Where(c => c.ClassName.Equals("Edit", StringComparison.OrdinalIgnoreCase) &&
                            (c.Rect.Right - c.Rect.Left) > 50 &&
                            (c.Rect.Bottom - c.Rect.Top) > 10)
                .OrderBy(c => c.Rect.Top)
                .ToList();
            if (validEdits.Count > 0) textEditCandidate = validEdits.Last();
        }

        if (textEditCandidate.Hwnd != IntPtr.Zero)
        {
            IntPtr textEditHwnd = textEditCandidate.Hwnd;
            var textEditRect = textEditCandidate.Rect;
            FileLogger.Log($"[OperateVendorSearch] Found Text Edit box: HWND=0x{textEditHwnd.ToInt64():X} Id={textEditCandidate.Id} Rect=({textEditRect.Left},{textEditRect.Top},{textEditRect.Right},{textEditRect.Bottom})");

            Win32Native.SetForegroundWindow(findDialogHwnd);
            Win32Native.SetFocus(textEditHwnd);

            int ecx = (textEditRect.Left + textEditRect.Right) / 2;
            int ecy = (textEditRect.Top + textEditRect.Bottom) / 2;
            await Win32Native.ClickScreenPointAsync(ecx, ecy, cancellationToken);
            await Task.Delay(100, cancellationToken);

            // Select all and clear
            await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_A, cancellationToken);
            await Task.Delay(50, cancellationToken);
            await Win32Native.SendKeyPressAsync(Win32Native.VK_BACK, cancellationToken);
            await Task.Delay(50, cancellationToken);

            // Method 1: Clipboard paste (fast and works reliably with Thai/English IME)
            Win32Native.SetClipboardTextSafe(targetVendorName);
            await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_V, cancellationToken);
            await Task.Delay(100, cancellationToken);

            // Verify if text was entered
            var verifyText = Win32Native.GetEditText(textEditHwnd);

            // Method 2: If paste didn't populate, use WM_SETTEXT + EN_CHANGE
            if (string.IsNullOrWhiteSpace(verifyText))
            {
                Win32Native.SendMessage(textEditHwnd, Win32Native.WM_SETTEXT, IntPtr.Zero, targetVendorName);
                var id = Win32Native.GetDlgCtrlID(textEditHwnd);
                Win32Native.SendMessage(findDialogHwnd, Win32Native.WM_COMMAND, (IntPtr)((0x0300 << 16) | (id & 0xFFFF)), textEditHwnd);
                await Task.Delay(100, cancellationToken);
                verifyText = Win32Native.GetEditText(textEditHwnd);
            }

            // Method 3: If still empty, send WM_CHAR for each character
            if (string.IsNullOrWhiteSpace(verifyText))
            {
                foreach (char ch in targetVendorName)
                {
                    Win32Native.SendMessage(textEditHwnd, Win32Native.WM_CHAR, (IntPtr)ch, IntPtr.Zero);
                }
                await Task.Delay(100, cancellationToken);
                verifyText = Win32Native.GetEditText(textEditHwnd);
            }

            FileLogger.Log($"[OperateVendorSearch] Text edit box content verified: '{verifyText}'");
        }
        else
        {
            FileLogger.Log("[OperateVendorSearch] WARNING: Could not find visible Text Edit box!");
        }

        // 4. Trigger search via green arrow button [>] (Id=1019) and (F2) search button (Id=1027)
        // Id=1019 is the square button with the green arrow icon [>] (Rect width=26, height=22)
        // Id=1027 is the text label button "( F2 )" directly to the right of Id=1019
        var greenArrowBtn = children.FirstOrDefault(c =>
            c.ClassName.Equals("Button", StringComparison.OrdinalIgnoreCase) &&
            c.Id == 1019);

        var f2LabelBtn = children.FirstOrDefault(c =>
            c.Id == 1027 ||
            (c.ClassName.Equals("Button", StringComparison.OrdinalIgnoreCase) && c.Text.Contains("F2")));

        // Fallback: If Id=1019 not found by exact ID, find button next to (F2) on the same horizontal row
        if (greenArrowBtn.Hwnd == IntPtr.Zero && f2LabelBtn.Hwnd != IntPtr.Zero)
        {
            greenArrowBtn = children.FirstOrDefault(c =>
                c.ClassName.Equals("Button", StringComparison.OrdinalIgnoreCase) &&
                c.Hwnd != f2LabelBtn.Hwnd &&
                Math.Abs(c.Rect.Top - f2LabelBtn.Rect.Top) <= 15 &&
                c.Rect.Right <= f2LabelBtn.Rect.Left + 5 &&
                c.Rect.Right >= f2LabelBtn.Rect.Left - 25);
        }

        // Primary: Click the green arrow search button [>] (user: "กรอกเสร็จแล้วต้อง กดตรงนี้แล้วค่อยเลือก")
        if (greenArrowBtn.Hwnd != IntPtr.Zero)
        {
            int gx = (greenArrowBtn.Rect.Left + greenArrowBtn.Rect.Right) / 2;
            int gy = (greenArrowBtn.Rect.Top + greenArrowBtn.Rect.Bottom) / 2;
            FileLogger.Log($"[OperateVendorSearch] Clicking green arrow search button [>] (HWND=0x{greenArrowBtn.Hwnd.ToInt64():X}, Id={greenArrowBtn.Id}) at ({gx}, {gy})...");
            await Win32Native.ClickScreenPointAsync(gx, gy, cancellationToken);
            await Task.Delay(100, cancellationToken);
            Win32Native.ClickButtonHwnd(greenArrowBtn.Hwnd, findDialogHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // Secondary: Also click the (F2) button
        if (f2LabelBtn.Hwnd != IntPtr.Zero)
        {
            int fx = (f2LabelBtn.Rect.Left + f2LabelBtn.Rect.Right) / 2;
            int fy = (f2LabelBtn.Rect.Top + f2LabelBtn.Rect.Bottom) / 2;
            FileLogger.Log($"[OperateVendorSearch] Also clicking (F2) button (HWND=0x{f2LabelBtn.Hwnd.ToInt64():X}, Id={f2LabelBtn.Id}) at ({fx}, {fy})...");
            await Win32Native.ClickScreenPointAsync(fx, fy, cancellationToken);
            Win32Native.ClickButtonHwnd(f2LabelBtn.Hwnd, findDialogHwnd);
            await Task.Delay(100, cancellationToken);
        }

        // Shortcut: Send F2 key
        FileLogger.Log("[OperateVendorSearch] Sending F2 shortcut key...");
        await Win32Native.SendKeyPressAsync(Win32Native.VK_F2, cancellationToken);
        await Task.Delay(100, cancellationToken);

        // Also send Enter inside edit box as standard search trigger
        await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);

        // 5. Wait for search results to load into DataWindow
        FileLogger.Log("[OperateVendorSearch] Waiting for search results to load...");
        await Task.Delay(1500, cancellationToken);

        // 6. Select result row (Row 1):
        // CRITICAL: Ensure Row 1 is selected via VK_HOME
        await Win32Native.SendKeyPressAsync(Win32Native.VK_HOME, cancellationToken);
        await Task.Delay(100, cancellationToken);

        Win32Native.GetWindowRect(findDialogHwnd, out var currentDlgRect);
        var dw = children.FirstOrDefault(c => c.ClassName.Contains("pbdw"));
        int gridX = dw.Hwnd != IntPtr.Zero ? dw.Rect.Left + 80 : currentDlgRect.Left + 120;
        // Data row 1 is located ~24px below the top of the DataWindow (header is ~16px, row 1 is 17..33px)
        int gridY = dw.Hwnd != IntPtr.Zero ? dw.Rect.Top + 24 : currentDlgRect.Top + 65;

        FileLogger.Log($"[OperateVendorSearch] Selecting Row 1: Double-clicking result grid at ({gridX}, {gridY})...");
        await Win32Native.ClickScreenPointAsync(gridX, gridY, cancellationToken);
        await Task.Delay(150, cancellationToken);
        await Win32Native.DoubleClickScreenPointAsync(gridX, gridY, cancellationToken);
        await Task.Delay(300, cancellationToken);

        // Send Enter to confirm selection if dialog is still open
        if (Win32Native.IsWindow(findDialogHwnd) && Win32Native.IsWindowVisible(findDialogHwnd))
        {
            FileLogger.Log("[OperateVendorSearch] Dialog still open; sending Enter on selected row...");
            await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);
            await Task.Delay(400, cancellationToken);
        }

        // Also check if there is an OK/Select button in the dialog (e.g. Id=1 or Text='OK' or 'ตกลง')
        if (Win32Native.IsWindow(findDialogHwnd) && Win32Native.IsWindowVisible(findDialogHwnd))
        {
            var okBtn = children.FirstOrDefault(c =>
                c.ClassName.Equals("Button", StringComparison.OrdinalIgnoreCase) &&
                (c.Text.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                 c.Text.Equals("ตกลง", StringComparison.OrdinalIgnoreCase) ||
                 c.Id == 1));

            if (okBtn.Hwnd != IntPtr.Zero)
            {
                int okX = (okBtn.Rect.Left + okBtn.Rect.Right) / 2;
                int okY = (okBtn.Rect.Top + okBtn.Rect.Bottom) / 2;
                FileLogger.Log($"[OperateVendorSearch] Clicking dialog OK button at ({okX}, {okY})...");
                await Win32Native.ClickScreenPointAsync(okX, okY, cancellationToken);
            }
        }

        // 7. Wait for dialog to close
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (!Win32Native.IsWindow(findDialogHwnd) || !Win32Native.IsWindowVisible(findDialogHwnd))
            {
                FileLogger.Log("[OperateVendorSearch] Find dialog closed successfully!");
                return VendorFillResult.Success($"กรอกข้อมูลผู้ขาย '{targetVendorName}' จาก CSV สำเร็จ", targetVendorName);
            }
            await Task.Delay(250, cancellationToken);
        }

        if (!Win32Native.IsWindowVisible(findDialogHwnd))
        {
            return VendorFillResult.Success($"กรอกข้อมูลผู้ขาย '{targetVendorName}' จาก CSV สำเร็จ", targetVendorName);
        }

        FileLogger.Log("[OperateVendorSearch] Find dialog did not close within timeout.");
        return VendorFillResult.Error($"ค้นหาผู้ขาย '{targetVendorName}' แล้ว แต่หน้าต่างค้นหาไม่ปิดลง (อาจไม่พบชื่อผู้ขายนี้ในระบบ Prosoft หรือเกิดข้อผิดพลาดในการเลือกรายการ)");
    }

    private async Task<bool> FillDocumentFieldsAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        VendorCsvRecord vendorRow,
        Process process,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        bool hasAnyDocField = !string.IsNullOrWhiteSpace(vendorRow.DocumentNumber) ||
                              !string.IsNullOrWhiteSpace(vendorRow.TaxInvoiceNumber) ||
                              !string.IsNullOrWhiteSpace(vendorRow.DeliveryOrderNumber);

        if (!hasAnyDocField)
        {
            FileLogger.Log("[FillDocFields] No document number fields provided in CSV record.");
            return true;
        }

        Report(progress, "กำลังกรอกข้อมูลเอกสาร (เลขที่เอกสาร, เลขที่ใบกำกับ, เลขที่ใบส่งของ)...");
        FileLogger.Log($"[FillDocFields] Filling fields: DocNo='{vendorRow.DocumentNumber}', TaxInvoice='{vendorRow.TaxInvoiceNumber}', DO='{vendorRow.DeliveryOrderNumber}'");

        // Determine coordinates (visual detector or verified proportional layout)
        var loc = CreditPurchaseDocDetector.GetDefaultDocFieldLocations(childRect);
        int docX = loc.DocNumber.X;
        int docY = loc.DocNumber.Y;
        int taxX = loc.TaxInvoice.X;
        int taxY = loc.TaxInvoice.Y;
        int doX = loc.DeliveryOrder.X;
        int doY = loc.DeliveryOrder.Y;

        // Visual detection scan if possible
        try
        {
            int scanX = childRect.Left + 420;
            int scanY = childRect.Top + 50;
            int scanW = Math.Min(220, childRect.Right - scanX);
            int scanH = Math.Min(90, childRect.Bottom - scanY);
            if (scanW > 120 && scanH > 60)
            {
                using var scanBmp = new System.Drawing.Bitmap(scanW, scanH);
                using (var g = System.Drawing.Graphics.FromImage(scanBmp))
                {
                    g.CopyFromScreen(scanX, scanY, 0, 0, new System.Drawing.Size(scanW, scanH));
                }
                var detected = CreditPurchaseDocDetector.FindDocFieldsInBitmap(scanBmp, scanX, scanY);
                if (detected != null)
                {
                    docX = detected.DocNumber.X;
                    docY = detected.DocNumber.Y;
                    taxX = detected.TaxInvoice.X;
                    taxY = detected.TaxInvoice.Y;
                    doX = detected.DeliveryOrder.X;
                    doY = detected.DeliveryOrder.Y;
                    FileLogger.Log($"[FillDocFields] Visually detected doc fields: Doc=({docX},{docY}), TaxInv=({taxX},{taxY}), DO=({doX},{doY})");
                }
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[FillDocFields] Visual scan notice: {ex.Message}");
        }

        // 1. เลขที่เอกสาร (Document No)
        if (!string.IsNullOrWhiteSpace(vendorRow.DocumentNumber))
        {
            FileLogger.Log($"[FillDocFields] Setting 'เลขที่เอกสาร' = '{vendorRow.DocumentNumber}' at ({docX}, {docY})...");
            await SetFieldTextSafeAsync(docX, docY, vendorRow.DocumentNumber, cancellationToken, childHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // 2. เลขที่ใบกำกับ (Tax Invoice No)
        if (!string.IsNullOrWhiteSpace(vendorRow.TaxInvoiceNumber))
        {
            FileLogger.Log($"[FillDocFields] Setting 'เลขที่ใบกำกับ' = '{vendorRow.TaxInvoiceNumber}' at ({taxX}, {taxY})...");
            await SetFieldTextSafeAsync(taxX, taxY, vendorRow.TaxInvoiceNumber, cancellationToken, childHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // 3. เลขที่ใบส่งของ (Delivery Order No) with duplicate detection and auto /1, /2... retry
        if (!string.IsNullOrWhiteSpace(vendorRow.DeliveryOrderNumber))
        {
            FileLogger.Log($"[FillDocFields] Setting 'เลขที่ใบส่งของ' = '{vendorRow.DeliveryOrderNumber}' at ({doX}, {doY})...");
            await SetDeliveryOrderWithDuplicateHandlingAsync(process, childHwnd, doX, doY, vendorRow, progress, cancellationToken);
            await Task.Delay(150, cancellationToken);
        }

        FileLogger.Log("[FillDocFields] Document fields populated successfully.");
        return true;
    }

    private static async Task SetFieldTextSafeAsync(
        int x,
        int y,
        string text,
        CancellationToken cancellationToken,
        IntPtr targetHwnd = default)
    {
        // 1. Ensure English keyboard layout so shortcut and navigation keys behave consistently
        if (targetHwnd != IntPtr.Zero)
        {
            Win32Native.EnsureEnglishKeyboardLayout(targetHwnd);
        }
        else
        {
            Win32Native.EnsureEnglishKeyboardLayout(IntPtr.Zero);
        }

        // 2. Click inside edit box to focus and place caret
        await Win32Native.ClickScreenPointAsync(x, y, cancellationToken);
        await Task.Delay(80, cancellationToken);

        // 3. Double-click inside box to select word/content in PowerBuilder
        await Win32Native.DoubleClickScreenPointAsync(x, y, cancellationToken);
        await Task.Delay(80, cancellationToken);

        // 4. Robust multi-stage clear (PowerBuilder DataWindow doesn't always honor Ctrl+A):
        //    a) Ctrl+A -> Backspace
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_A, cancellationToken);
        await Task.Delay(30, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_BACK, cancellationToken);
        await Task.Delay(30, cancellationToken);

        //    b) End -> Shift+Home -> Backspace
        await Win32Native.SendKeyPressAsync(Win32Native.VK_END, cancellationToken);
        await Task.Delay(20, cancellationToken);
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_SHIFT, Win32Native.VK_HOME, cancellationToken);
        await Task.Delay(20, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_BACK, cancellationToken);
        await Task.Delay(20, cancellationToken);

        //    c) Home -> Shift+End -> Delete
        await Win32Native.SendKeyPressAsync(Win32Native.VK_HOME, cancellationToken);
        await Task.Delay(20, cancellationToken);
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_SHIFT, Win32Native.VK_END, cancellationToken);
        await Task.Delay(20, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_DELETE, cancellationToken);
        await Task.Delay(20, cancellationToken);

        //    d) Backspace and Delete sweep to ensure completely blank
        for (int i = 0; i < 20; i++)
        {
            await Win32Native.SendKeyPressAsync(Win32Native.VK_BACK, cancellationToken);
        }
        for (int i = 0; i < 20; i++)
        {
            await Win32Native.SendKeyPressAsync(Win32Native.VK_DELETE, cancellationToken);
        }
        await Task.Delay(50, cancellationToken);

        // 5. Set text via clipboard paste
        Win32Native.SetClipboardTextSafe(text);
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_V, cancellationToken);
        await Task.Delay(100, cancellationToken);

        // 6. Commit to DataWindow buffer
        await Win32Native.SendKeyPressAsync(Win32Native.VK_TAB, cancellationToken);
    }

    private async Task<bool> SetDeliveryOrderWithDuplicateHandlingAsync(
        Process process,
        IntPtr childHwnd,
        int doX,
        int doY,
        VendorCsvRecord vendorRow,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        string? baseDo = vendorRow.DeliveryOrderNumber;
        if (string.IsNullOrWhiteSpace(baseDo)) return true;

        FileLogger.Log($"[SetDeliveryOrder] Setting initial DO='{baseDo}' at ({doX}, {doY})...");
        await SetFieldTextSafeAsync(doX, doY, baseDo, cancellationToken, childHwnd);

        // Check if duplicate DO popup appears (wait up to 800ms)
        var (dupPopup, title, msg) = await WaitForDuplicateDoPopupAsync(process, 800, cancellationToken);
        if (dupPopup == IntPtr.Zero)
        {
            FileLogger.Log($"[SetDeliveryOrder] DO '{baseDo}' accepted without duplicate warning.");
            return true;
        }

        FileLogger.Log($"[SetDeliveryOrder] Detected duplicate DO popup: Title='{title}' Message='{msg}'. Dismissing and looping with /1, /2, ...");
        Report(progress, $"ตรวจพบ '{msg}': กำลังกด OK และแก้ไขเลขที่ใบส่งของ...");
        await DismissDuplicateDoPopupAsync(dupPopup, cancellationToken);

        int suffix = 1;
        const int maxRetries = 50;
        while (suffix <= maxRetries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string candidateDo = CreditPurchaseDocDetector.GenerateNextDeliveryOrderNumber(baseDo, suffix);
            FileLogger.Log($"[SetDeliveryOrder] Attempt {suffix}: Trying candidate DO='{candidateDo}'...");
            Report(progress, $"ลองเปลี่ยนเลขที่ใบส่งของเป็น '{candidateDo}'...");

            if (childHwnd != IntPtr.Zero)
            {
                Win32Native.SetForegroundWindow(childHwnd);
                await Task.Delay(50, cancellationToken);
            }

            // Click into field, select all, paste candidateDo, and commit with TAB
            await SetFieldTextSafeAsync(doX, doY, candidateDo, cancellationToken, childHwnd);

            var (newDupPopup, _, _) = await WaitForDuplicateDoPopupAsync(process, 700, cancellationToken);
            if (newDupPopup != IntPtr.Zero)
            {
                FileLogger.Log($"[SetDeliveryOrder] DO '{candidateDo}' is also duplicate. Dismissing and trying next suffix...");
                await DismissDuplicateDoPopupAsync(newDupPopup, cancellationToken);
                suffix++;
            }
            else
            {
                FileLogger.Log($"[SetDeliveryOrder] DO '{candidateDo}' accepted successfully (no duplicate warning)!");
                Report(progress, $"แก้ไขเลขที่ใบส่งของเป็น '{candidateDo}' สำเร็จ (ไม่ซ้ำ)");
                vendorRow.DeliveryOrderNumber = candidateDo;
                return true;
            }
        }

        Report(progress, "คำเตือน: วนลูปเปลี่ยนเลขที่ใบส่งของครบ 50 ครั้งแล้ว");
        return false;
    }

    private static async Task DismissDuplicateDoPopupAsync(IntPtr popupHwnd, CancellationToken cancellationToken)
    {
        if (popupHwnd == IntPtr.Zero) return;

        Win32Native.SetForegroundWindow(popupHwnd);
        await Task.Delay(50, cancellationToken);

        IntPtr okBtn = Win32Native.FindOkButtonHwnd(popupHwnd);
        if (okBtn != IntPtr.Zero)
        {
            Win32Native.ClickButtonHwnd(okBtn, popupHwnd);
        }
        await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (!Win32Native.IsWindow(popupHwnd) || !Win32Native.IsWindowVisible(popupHwnd))
            {
                break;
            }
            await Task.Delay(100, cancellationToken);
        }
        await Task.Delay(100, cancellationToken);
    }

    private static async Task<(IntPtr Hwnd, string Title, string Message)> WaitForDuplicateDoPopupAsync(
        Process process,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IntPtr foundHwnd = IntPtr.Zero;
            string foundTitle = "";
            string foundMsg = "";

            Win32Native.EnumWindows((h, _) =>
            {
                uint pid;
                Win32Native.GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)process.Id && Win32Native.IsWindowVisible(h))
                {
                    var cls = Win32Native.GetClass(h);
                    var txt = Win32Native.GetText(h);
                    if (cls == "#32770" || cls.Contains("FNWND", StringComparison.OrdinalIgnoreCase))
                    {
                        var msgList = new List<string>();
                        Win32Native.EnumChildWindows(h, (ch, _) =>
                        {
                            var chCls = Win32Native.GetClass(ch);
                            var chTxt = Win32Native.GetText(ch);
                            if (chCls.Equals("Static", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(chTxt))
                            {
                                msgList.Add(chTxt.Trim());
                            }
                            return true;
                        }, IntPtr.Zero);

                        var fullMsg = string.Join(" ", msgList);
                        if (CreditPurchaseDocDetector.IsDuplicateDeliveryOrderMessage(fullMsg, txt))
                        {
                            foundHwnd = h;
                            foundTitle = txt;
                            foundMsg = fullMsg;
                            return false;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (foundHwnd != IntPtr.Zero)
            {
                return (foundHwnd, foundTitle, foundMsg);
            }

            await Task.Delay(100, cancellationToken);
        }

        return (IntPtr.Zero, "", "");
    }

    private async Task<bool> SwitchToMoreTabAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        CancellationToken cancellationToken)
    {
        FileLogger.Log($"[SwitchToMoreTab] Switching to 'More' tab on Credit Purchase window HWND=0x{childHwnd.ToInt64():X}...");

        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(childHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // 1. Try FlaUI UIA TabItem
        if (purchaseElem != null)
        {
            try
            {
                var tabItem = purchaseElem.FindFirstDescendant(cf =>
                    cf.ByName("More").Or(cf.ByName("more")));
                if (tabItem != null)
                {
                    FileLogger.Log($"[SwitchToMoreTab] Found UIA More tab item: '{tabItem.Name}'");
                    if (tabItem.Patterns.SelectionItem.IsSupported)
                    {
                        tabItem.Patterns.SelectionItem.Pattern.Select();
                        await Task.Delay(400, cancellationToken);
                        return true;
                    }
                    else
                    {
                        tabItem.Click();
                        await Task.Delay(400, cancellationToken);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[SwitchToMoreTab] UIA tab select notice: {ex.Message}");
            }
        }

        // 2. Find PBTabControl32_80
        IntPtr tabControlHwnd = IntPtr.Zero;
        Win32Native.RECT tabRect = default;
        Win32Native.EnumChildWindows(childHwnd, (ch, _) =>
        {
            var cls = Win32Native.GetClass(ch);
            if (cls.Contains("TabControl", StringComparison.OrdinalIgnoreCase) ||
                cls.Contains("pbtab", StringComparison.OrdinalIgnoreCase))
            {
                tabControlHwnd = ch;
                Win32Native.GetWindowRect(ch, out tabRect);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        // 3. Click on screen coordinate for More tab header
        // In the Credit Purchase window (789x479):
        // Detail: childRect.Left + 15..88 (center = 51)
        // More: childRect.Left + 89..145 (center = 117)
        // Tab strip center Y is ~14px above tab control bottom, or ~52px above window bottom
        int moreX = tabControlHwnd != IntPtr.Zero ? tabRect.Left + 110 : childRect.Left + 117;
        int moreY = tabControlHwnd != IntPtr.Zero ? tabRect.Bottom - 14 : childRect.Bottom - 52;

        FileLogger.Log($"[SwitchToMoreTab] Clicking 'More' tab at ({moreX}, {moreY})...");
        await Win32Native.ClickScreenPointAsync(moreX, moreY, cancellationToken);
        await Task.Delay(250, cancellationToken);

        // 4. Also send Win32 TCM_SETCURSEL / TCM_SETCURFOCUS message to tab control
        if (tabControlHwnd != IntPtr.Zero)
        {
            FileLogger.Log("[SwitchToMoreTab] Sending TCM_SETCURSEL (index=1) to TabControl...");
            Win32Native.SendMessage(tabControlHwnd, Win32Native.TCM_SETCURSEL, (IntPtr)1, IntPtr.Zero);
            Win32Native.SendMessage(tabControlHwnd, Win32Native.TCM_SETCURFOCUS, (IntPtr)1, IntPtr.Zero);
        }

        await Task.Delay(400, cancellationToken);
        return true;
    }

    private async Task<bool> SetTaxGroupToNovatAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        Process process,
        CancellationToken cancellationToken)
    {
        FileLogger.Log("[SetTaxGroup] Setting 'รหัสกลุ่มภาษี' to 'NOVAT'...");

        // 1. Check if 'Find รหัสกลุ่มภาษี' dialog is already open
        IntPtr taxDialogHwnd = FindTaxSearchDialogHwnd(process);

        if (taxDialogHwnd == IntPtr.Zero)
        {
            // 2. Open dialog by clicking dropdown arrow [ v ] or pressing F2
            var loc = MoreTabTaxDetector.GetDefaultTaxFieldLocation(childRect);
            int fieldX = loc.EditX;
            int fieldY = loc.EditY;
            int arrowX = loc.ArrowX;
            int arrowY = loc.ArrowY;

            // Try visual detection on the More tab pane if possible
            try
            {
                int scanX = childRect.Left;
                int scanY = childRect.Top + 160;
                int scanW = Math.Min(350, childRect.Right - childRect.Left);
                int scanH = Math.Min(120, childRect.Bottom - scanY);
                if (scanW > 100 && scanH > 50)
                {
                    using var scanBmp = new System.Drawing.Bitmap(scanW, scanH);
                    using (var g = System.Drawing.Graphics.FromImage(scanBmp))
                    {
                        g.CopyFromScreen(scanX, scanY, 0, 0, new System.Drawing.Size(scanW, scanH));
                    }
                    var detected = MoreTabTaxDetector.FindTaxFieldInBitmap(scanBmp, scanX, scanY);
                    if (detected != null)
                    {
                        fieldX = detected.EditX;
                        fieldY = detected.EditY;
                        arrowX = detected.ArrowX;
                        arrowY = detected.ArrowY;
                        FileLogger.Log($"[SetTaxGroup] Visually detected tax field at edit=({fieldX}, {fieldY}), arrow=({arrowX}, {arrowY})");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[SetTaxGroup] Visual scan notice: {ex.Message}");
            }

            FileLogger.Log($"[SetTaxGroup] Clicking dropdown arrow [ v ] at ({arrowX}, {arrowY})...");
            await Win32Native.ClickScreenPointAsync(arrowX, arrowY, cancellationToken);

            taxDialogHwnd = await WaitForTaxSearchDialogAsync(process, TimeSpan.FromSeconds(2.5), cancellationToken);

            if (taxDialogHwnd == IntPtr.Zero)
            {
                // Fallback: Click edit box and send F2
                FileLogger.Log($"[SetTaxGroup] Dialog not open yet; clicking edit box at ({fieldX}, {fieldY}) and sending F2...");
                await Win32Native.ClickScreenPointAsync(fieldX, fieldY, cancellationToken);
                await Task.Delay(150, cancellationToken);
                await Win32Native.SendKeyPressAsync(Win32Native.VK_F2, cancellationToken);
                taxDialogHwnd = await WaitForTaxSearchDialogAsync(process, TimeSpan.FromSeconds(2.0), cancellationToken);
            }
        }

        // 3. If 'Find รหัสกลุ่มภาษี' dialog is open: "เลือกแค่ครั้งเดียวก็พอ"
        if (taxDialogHwnd != IntPtr.Zero)
        {
            FileLogger.Log($"[SetTaxGroup] Found 'Find รหัสกลุ่มภาษี' dialog (HWND=0x{taxDialogHwnd.ToInt64():X}). Selecting Row 1 ('NOVAT') once...");
            Win32Native.ShowWindow(taxDialogHwnd, Win32Native.SW_RESTORE);
            Win32Native.SetForegroundWindow(taxDialogHwnd);
            await Task.Delay(250, cancellationToken);

            // Find child controls (DataWindow)
            var children = new List<(IntPtr Hwnd, string ClassName, string Text, int Id, Win32Native.RECT Rect)>();
            Win32Native.EnumChildWindows(taxDialogHwnd, (h, _) =>
            {
                var cls = Win32Native.GetClass(h);
                var txt = Win32Native.GetText(h);
                var id = Win32Native.GetDlgCtrlID(h);
                Win32Native.GetWindowRect(h, out var r);
                children.Add((h, cls, txt, id, r));
                return true;
            }, IntPtr.Zero);

            Win32Native.GetWindowRect(taxDialogHwnd, out var dlgRect);
            var dw = children.FirstOrDefault(c => c.ClassName.Contains("pbdw"));
            int gridX = dw.Hwnd != IntPtr.Zero ? dw.Rect.Left + 80 : dlgRect.Left + 100;
            // CRITICAL: Row 1 (NOVAT) is at dw.Rect.Top + 24!
            // Header is 16px (0..16px), Row 1 is 17..33px (center=24px).
            // NOTE: dw.Rect.Top + 40 was hitting Row 2 (PO-Expense)! Row 1 (NOVAT) MUST use Top + 24.
            int gridY = dw.Hwnd != IntPtr.Zero ? dw.Rect.Top + 24 : dlgRect.Top + 65;

            FileLogger.Log($"[SetTaxGroup] Selecting Row 1 ('NOVAT') at ({gridX}, {gridY})...");
            // 1. Send VK_HOME first to guarantee cursor is on the first row (NOVAT)
            await Win32Native.SendKeyPressAsync(Win32Native.VK_HOME, cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 2. Click & Double-click on Row 1 (Top + 24)
            await Win32Native.ClickScreenPointAsync(gridX, gridY, cancellationToken);
            await Task.Delay(150, cancellationToken);
            await Win32Native.DoubleClickScreenPointAsync(gridX, gridY, cancellationToken);
            await Task.Delay(200, cancellationToken);

            // 3. Send VK_HOME + Enter to confirm Row 1 selection if dialog is still open
            if (Win32Native.IsWindow(taxDialogHwnd) && Win32Native.IsWindowVisible(taxDialogHwnd))
            {
                FileLogger.Log("[SetTaxGroup] Dialog still open; sending VK_HOME then Enter on Row 1 ('NOVAT')...");
                await Win32Native.SendKeyPressAsync(Win32Native.VK_HOME, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);
                await Task.Delay(300, cancellationToken);
            }

            // Check if there is an OK button
            if (Win32Native.IsWindow(taxDialogHwnd) && Win32Native.IsWindowVisible(taxDialogHwnd))
            {
                var okBtn = children.FirstOrDefault(c =>
                    c.ClassName.Equals("Button", StringComparison.OrdinalIgnoreCase) &&
                    (c.Text.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                     c.Text.Equals("ตกลง", StringComparison.OrdinalIgnoreCase) ||
                     c.Id == 1));

                if (okBtn.Hwnd != IntPtr.Zero)
                {
                    int okX = (okBtn.Rect.Left + okBtn.Rect.Right) / 2;
                    int okY = (okBtn.Rect.Top + okBtn.Rect.Bottom) / 2;
                    FileLogger.Log($"[SetTaxGroup] Clicking dialog OK button at ({okX}, {okY})...");
                    await Win32Native.ClickScreenPointAsync(okX, okY, cancellationToken);
                    await Task.Delay(300, cancellationToken);
                }
            }

            // Wait for dialog to close
            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < deadline)
            {
                if (!Win32Native.IsWindow(taxDialogHwnd) || !Win32Native.IsWindowVisible(taxDialogHwnd))
                {
                    FileLogger.Log("[SetTaxGroup] 'Find รหัสกลุ่มภาษี' dialog closed successfully with 'NOVAT' selected!");
                    return true;
                }
                await Task.Delay(200, cancellationToken);
            }

            if (!Win32Native.IsWindowVisible(taxDialogHwnd))
            {
                return true;
            }
        }
        else
        {
            FileLogger.Log("[SetTaxGroup] 'Find รหัสกลุ่มภาษี' dialog did not open; falling back to keyboard selection...");
            await Win32Native.SendKeyPressAsync(Win32Native.VK_HOME, cancellationToken);
            await Task.Delay(100, cancellationToken);
            await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);
            await Task.Delay(200, cancellationToken);
        }

        FileLogger.Log("[SetTaxGroup] Setting 'รหัสกลุ่มภาษี' to 'NOVAT' completed.");
        return true;
    }

    private async Task<bool> SwitchToDetailTabAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        CancellationToken cancellationToken)
    {
        FileLogger.Log($"[SwitchToDetailTab] Switching to 'Detail' tab on Credit Purchase window HWND=0x{childHwnd.ToInt64():X}...");

        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(childHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // 1. Try FlaUI UIA TabItem
        if (purchaseElem != null)
        {
            try
            {
                var tabItem = purchaseElem.FindFirstDescendant(cf =>
                    cf.ByName("Detail").Or(cf.ByName("detail")));
                if (tabItem != null)
                {
                    FileLogger.Log($"[SwitchToDetailTab] Found UIA Detail tab item: '{tabItem.Name}'");
                    if (tabItem.Patterns.SelectionItem.IsSupported)
                    {
                        tabItem.Patterns.SelectionItem.Pattern.Select();
                        await Task.Delay(400, cancellationToken);
                        return true;
                    }
                    else
                    {
                        tabItem.Click();
                        await Task.Delay(400, cancellationToken);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[SwitchToDetailTab] UIA tab select notice: {ex.Message}");
            }
        }

        // 2. Find PBTabControl32_80
        IntPtr tabControlHwnd = IntPtr.Zero;
        Win32Native.RECT tabRect = default;
        Win32Native.EnumChildWindows(childHwnd, (ch, _) =>
        {
            var cls = Win32Native.GetClass(ch);
            if (cls.Contains("TabControl", StringComparison.OrdinalIgnoreCase) ||
                cls.Contains("pbtab", StringComparison.OrdinalIgnoreCase))
            {
                tabControlHwnd = ch;
                Win32Native.GetWindowRect(ch, out tabRect);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        // 3. Click on screen coordinate for Detail tab header
        // Detail: childRect.Left + 15..88 (center = 45..51)
        int detailX = tabControlHwnd != IntPtr.Zero ? tabRect.Left + 45 : childRect.Left + 45;
        int detailY = tabControlHwnd != IntPtr.Zero ? tabRect.Bottom - 14 : childRect.Bottom - 52;

        FileLogger.Log($"[SwitchToDetailTab] Clicking 'Detail' tab at ({detailX}, {detailY})...");
        await Win32Native.ClickScreenPointAsync(detailX, detailY, cancellationToken);
        await Task.Delay(250, cancellationToken);

        // 4. Also send Win32 TCM_SETCURSEL / TCM_SETCURFOCUS message to tab control (index 0 = Detail)
        if (tabControlHwnd != IntPtr.Zero)
        {
            FileLogger.Log("[SwitchToDetailTab] Sending TCM_SETCURSEL (index=0) to TabControl...");
            Win32Native.SendMessage(tabControlHwnd, Win32Native.TCM_SETCURSEL, (IntPtr)0, IntPtr.Zero);
            Win32Native.SendMessage(tabControlHwnd, Win32Native.TCM_SETCURFOCUS, (IntPtr)0, IntPtr.Zero);
        }

        await Task.Delay(400, cancellationToken);
        return true;
    }

    private async Task<bool> FillDetailItemsAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        List<DetailItemRecord> items,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0)
        {
            FileLogger.Log("[FillDetailItems] No detail items to fill.");
            return true;
        }

        FileLogger.Log($"[FillDetailItems] Filling {items.Count} items in Detail grid...");
        Report(progress, $"กำลังกรอกข้อมูลรายการสินค้าในแท็บ Detail (ทั้งหมด {items.Count} รายการ)...");

        for (int i = 1; i <= items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i - 1];
            var wh = !string.IsNullOrWhiteSpace(item.Warehouse) ? item.Warehouse : (_options.VendorInput.DefaultWarehouse ?? "BK");
            var loc = !string.IsNullOrWhiteSpace(item.Location) ? item.Location : (_options.VendorInput.DefaultLocation ?? "BKW");

            FileLogger.Log($"[FillDetailItems] Row {i}/{items.Count}: ItemCode='{item.ItemCode}', WH='{wh}', Loc='{loc}', Qty='{item.Quantity}', Price='{item.UnitPrice}'");
            Report(progress, $"กำลังกรอกสินค้าแถวที่ {i}/{items.Count}: '{item.ItemCode}' (คลัง: {wh}, ที่เก็บ: {loc})...");

            // 1. Item Code (รหัสสินค้า)
            if (!string.IsNullOrWhiteSpace(item.ItemCode))
            {
                var ptCode = CreditPurchaseDetailDetector.GetCellLocation(childRect, i, DetailColumn.ItemCode);
                FileLogger.Log($"[FillDetailItems] Setting ItemCode '{item.ItemCode}' at ({ptCode.X}, {ptCode.Y})...");
                await SetFieldTextSafeAsync(ptCode.X, ptCode.Y, item.ItemCode, cancellationToken);
                await Task.Delay(200, cancellationToken);
            }

            // 2. Warehouse (คลัง) - default "BK"
            if (!string.IsNullOrWhiteSpace(wh))
            {
                var ptWh = CreditPurchaseDetailDetector.GetCellLocation(childRect, i, DetailColumn.Warehouse);
                FileLogger.Log($"[FillDetailItems] Setting Warehouse '{wh}' at ({ptWh.X}, {ptWh.Y})...");
                await SetFieldTextSafeAsync(ptWh.X, ptWh.Y, wh, cancellationToken);
                await Task.Delay(150, cancellationToken);
            }

            // 3. Location (ที่เก็บ) - default "BKW"
            if (!string.IsNullOrWhiteSpace(loc))
            {
                var ptLoc = CreditPurchaseDetailDetector.GetCellLocation(childRect, i, DetailColumn.Location);
                FileLogger.Log($"[FillDetailItems] Setting Location '{loc}' at ({ptLoc.X}, {ptLoc.Y})...");
                await SetFieldTextSafeAsync(ptLoc.X, ptLoc.Y, loc, cancellationToken);
                await Task.Delay(150, cancellationToken);
            }

            // 3. Quantity (จำนวน) - if specified
            if (!string.IsNullOrWhiteSpace(item.Quantity))
            {
                var ptQty = CreditPurchaseDetailDetector.GetCellLocation(childRect, i, DetailColumn.Quantity);
                FileLogger.Log($"[FillDetailItems] Setting Quantity '{item.Quantity}' at ({ptQty.X}, {ptQty.Y})...");
                await SetFieldTextSafeAsync(ptQty.X, ptQty.Y, item.Quantity, cancellationToken);
                await Task.Delay(150, cancellationToken);
            }

            // 4. Unit Price (ราคาต่อหน่วย) - if specified
            if (!string.IsNullOrWhiteSpace(item.UnitPrice))
            {
                var ptPrice = CreditPurchaseDetailDetector.GetCellLocation(childRect, i, DetailColumn.UnitPrice);
                FileLogger.Log($"[FillDetailItems] Setting UnitPrice '{item.UnitPrice}' at ({ptPrice.X}, {ptPrice.Y})...");
                await SetFieldTextSafeAsync(ptPrice.X, ptPrice.Y, item.UnitPrice, cancellationToken);
                await Task.Delay(150, cancellationToken);
            }

            // 5. Discount (ส่วนลด) - if specified
            if (!string.IsNullOrWhiteSpace(item.Discount))
            {
                var ptDisc = CreditPurchaseDetailDetector.GetCellLocation(childRect, i, DetailColumn.Discount);
                FileLogger.Log($"[FillDetailItems] Setting Discount '{item.Discount}' at ({ptDisc.X}, {ptDisc.Y})...");
                await SetFieldTextSafeAsync(ptDisc.X, ptDisc.Y, item.Discount, cancellationToken);
                await Task.Delay(150, cancellationToken);
            }

            await Task.Delay(150, cancellationToken);
        }

        FileLogger.Log("[FillDetailItems] All detail items filled successfully.");
        return true;
    }

    private (IntPtr Hwnd, Win32Native.RECT Rect, AutomationElement? Element) FindCreditPurchaseWindowInfo(
        Window mainWindow,
        IntPtr mainWindowHandle)
    {
        // 1. Try FlaUI UIA first
        try
        {
            var descendants = mainWindow.FindAllDescendants();
            foreach (var elem in descendants)
            {
                try
                {
                    if (elem.ControlType == ControlType.Window || elem.ControlType == ControlType.Pane)
                    {
                        var name = elem.Name ?? "";
                        var cls = elem.ClassName ?? "";
                        if (ContainsAny(name, _options.Navigation.TargetActionAliases) ||
                            Contains(name, "ซื้อเชื่อ") ||
                            Contains(name, "Credit Purchase") ||
                            Contains(name, "ใบรับสินค้า"))
                        {
                            var r = elem.BoundingRectangle;
                            if (r.Width > 300 && r.Height > 200)
                            {
                                var h = elem.Properties.NativeWindowHandle.ValueOrDefault;
                                var wr = new Win32Native.RECT { Left = (int)r.Left, Top = (int)r.Top, Right = (int)r.Right, Bottom = (int)r.Bottom };
                                FileLogger.Log($"[FindCreditPurchaseWindow] Found via UIA: Name='{name}', Class='{cls}', HWND=0x{h.ToInt64():X}, Rect=({wr.Left},{wr.Top},{wr.Right},{wr.Bottom})");
                                return (h != IntPtr.Zero ? h : mainWindowHandle, wr, elem);
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[FindCreditPurchaseWindow] UIA search error: {ex.Message}");
        }

        // 2. Win32 EnumChildWindows: Search for FNWND380 with matching title or Id != 200
        IntPtr bestHwnd = IntPtr.Zero;
        Win32Native.RECT bestRect = default;

        Win32Native.EnumChildWindows(mainWindowHandle, (childHwnd, _) =>
        {
            if (!Win32Native.IsWindowVisible(childHwnd)) return true;
            var cls = Win32Native.GetClass(childHwnd);
            if (cls != "FNWND380") return true;

            var id = Win32Native.GetDlgCtrlID(childHwnd);
            if (id == 200) return true; // CRITICAL: Skip background flowchart canvas!

            var title = Win32Native.GetText(childHwnd);
            Win32Native.GetWindowRect(childHwnd, out var cr);
            int width = cr.Right - cr.Left;
            int height = cr.Bottom - cr.Top;

            if (width < 300 || height < 200) return true;

            if (ContainsAny(title, _options.Navigation.TargetActionAliases) ||
                Contains(title, "ซื้อเชื่อ") ||
                Contains(title, "Credit Purchase") ||
                Contains(title, "ใบรับสินค้า"))
            {
                bestHwnd = childHwnd;
                bestRect = cr;
                return false; // Exact title match!
            }

            // If title is empty or unreadable via Win32, record first visible FNWND380 (with Id != 200)
            if (bestHwnd == IntPtr.Zero)
            {
                bestHwnd = childHwnd;
                bestRect = cr;
            }
            return true;
        }, IntPtr.Zero);

        if (bestHwnd != IntPtr.Zero)
        {
            FileLogger.Log($"[FindCreditPurchaseWindow] Found via Win32: HWND=0x{bestHwnd.ToInt64():X}, Rect=({bestRect.Left},{bestRect.Top},{bestRect.Right},{bestRect.Bottom})");
            return (bestHwnd, bestRect, null);
        }

        return (IntPtr.Zero, default, null);
    }

    private IntPtr FindVendorSearchDialogHwnd(Process process)
    {
        IntPtr foundHwnd = IntPtr.Zero;

        Win32Native.EnumWindows((hwnd, _) =>
        {
            if (IsFindVendorDialog(hwnd, process.Id))
            {
                foundHwnd = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (foundHwnd != IntPtr.Zero) return foundHwnd;

        foreach (ProcessThread thread in process.Threads)
        {
            Win32Native.EnumThreadWindows((uint)thread.Id, (hwnd, _) =>
            {
                if (IsFindVendorDialog(hwnd, process.Id))
                {
                    foundHwnd = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (foundHwnd != IntPtr.Zero) break;
        }

        return foundHwnd;
    }

    private IntPtr FindTaxSearchDialogHwnd(Process process)
    {
        IntPtr foundHwnd = IntPtr.Zero;

        Win32Native.EnumWindows((hwnd, _) =>
        {
            if (IsFindTaxDialog(hwnd, process.Id))
            {
                foundHwnd = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (foundHwnd != IntPtr.Zero) return foundHwnd;

        foreach (ProcessThread thread in process.Threads)
        {
            Win32Native.EnumThreadWindows((uint)thread.Id, (hwnd, _) =>
            {
                if (IsFindTaxDialog(hwnd, process.Id))
                {
                    foundHwnd = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (foundHwnd != IntPtr.Zero) break;
        }

        return foundHwnd;
    }

    private bool IsFindTaxDialog(IntPtr hwnd, int processId)
    {
        if (!Win32Native.IsWindow(hwnd) || !Win32Native.IsWindowVisible(hwnd)) return false;
        Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != processId) return false;

        var title = Win32Native.GetText(hwnd);
        if (Contains(title, "myAccount") || Contains(title, "ซื้อเชื่อ") || Contains(title, "Credit Purchase"))
        {
            return false;
        }

        // Must not be vendor search dialog
        if (Contains(title, "ผู้ขาย") || Contains(title, "Vendor"))
        {
            return false;
        }

        // Title match: "กลุ่มภาษี" or ("Find" and not vendor)
        if (Contains(title, "กลุ่มภาษี") || Contains(title, "ภาษี") || Contains(title, "Tax"))
        {
            return true;
        }

        if (Contains(title, "Find") && !Contains(title, "ผู้ขาย"))
        {
            return true;
        }

        return false;
    }

    private async Task<IntPtr> WaitForTaxSearchDialogAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            var dlg = FindTaxSearchDialogHwnd(process);
            if (dlg != IntPtr.Zero) return dlg;
            await Task.Delay(150, cancellationToken);
        }
        return IntPtr.Zero;
    }

    private bool IsFindVendorDialog(IntPtr hwnd, int processId)
    {
        if (!Win32Native.IsWindow(hwnd) || !Win32Native.IsWindowVisible(hwnd)) return false;
        Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != processId) return false;

        var title = Win32Native.GetText(hwnd);
        var cls = Win32Native.GetClass(hwnd);

        if (Contains(title, "myAccount") || Contains(title, "ซื้อเชื่อ") || Contains(title, "Credit Purchase"))
        {
            return false;
        }

        // Exclude Tax Group search dialog
        if (Contains(title, "กลุ่มภาษี") || Contains(title, "ภาษี") || Contains(title, "Tax"))
        {
            return false;
        }

        // Title match: "Find" or "ผู้ขาย" or "ค้นหา"
        if (Contains(title, "Find") || Contains(title, "ผู้ขาย") || Contains(title, "ค้นหา"))
        {
            return true;
        }

        // Fallback for empty/unreadable Thai title:
        // MUST verify that this dialog contains BOTH ComboBox and Edit controls!
        if (cls == "FNWNS380" || cls == "#32770")
        {
            Win32Native.GetWindowRect(hwnd, out var r);
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w > 250 && w < 850 && h > 120 && h < 650)
            {
                bool hasCombo = false;
                bool hasEdit = false;
                Win32Native.EnumChildWindows(hwnd, (child, _) =>
                {
                    var cCls = Win32Native.GetClass(child);
                    if (cCls.Equals("ComboBox", StringComparison.OrdinalIgnoreCase)) hasCombo = true;
                    if (cCls.Equals("Edit", StringComparison.OrdinalIgnoreCase)) hasEdit = true;
                    return !(hasCombo && hasEdit);
                }, IntPtr.Zero);

                if (hasCombo && hasEdit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> SetSearchByToVendorNameAsync(
        IntPtr findDialogHwnd,
        (IntPtr Hwnd, string ClassName, string Text, int Id, Win32Native.RECT Rect) searchByCandidate,
        CancellationToken cancellationToken)
    {
        if (searchByCandidate.Hwnd == IntPtr.Zero) return false;

        IntPtr comboHwnd = searchByCandidate.Hwnd;
        var comboRect = searchByCandidate.Rect;
        int comboId = Win32Native.GetDlgCtrlID(comboHwnd);

        FileLogger.Log($"[SelectVendorSearchBy] Setting Search by ComboBox to 'ชื่อผู้ขาย' (HWND=0x{comboHwnd.ToInt64():X}, Id={comboId}, Rect=({comboRect.Left},{comboRect.Top},{comboRect.Right},{comboRect.Bottom}))");

        // 0. Check initial text
        var initialText = Win32Native.GetComboBoxCurrentText(comboHwnd);
        FileLogger.Log($"[SelectVendorSearchBy] Initial text: '{initialText}'");
        if (IsVendorNameText(initialText))
        {
            FileLogger.Log("[SelectVendorSearchBy] ComboBox already has 'ชื่อผู้ขาย' selected.");
            return true;
        }

        // Method 1: Dropdown + GetComboBoxItems
        var comboItems = Win32Native.GetComboBoxItems(comboHwnd);
        if (comboItems.Count == 0)
        {
            // Drop down list to force population in PowerBuilder
            Win32Native.SendMessage(comboHwnd, Win32Native.CB_SHOWDROPDOWN, (IntPtr)1, IntPtr.Zero);
            await Task.Delay(100, cancellationToken);
            comboItems = Win32Native.GetComboBoxItems(comboHwnd);
            Win32Native.SendMessage(comboHwnd, Win32Native.CB_SHOWDROPDOWN, (IntPtr)0, IntPtr.Zero);
        }
        FileLogger.Log($"[SelectVendorSearchBy] ComboBox items count={comboItems.Count}: {string.Join(" | ", comboItems.Select((t, i) => $"[{i}]={t}"))}");

        int targetIndex = comboItems.FindIndex(item => item.Trim().Equals("ชื่อผู้ขาย", StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0)
        {
            targetIndex = comboItems.FindIndex(item =>
                item.Trim().Equals("ชื่อผู้จำหน่าย", StringComparison.OrdinalIgnoreCase) ||
                item.Trim().Equals("ชื่อเจ้าหนี้", StringComparison.OrdinalIgnoreCase));
        }
        if (targetIndex < 0)
        {
            targetIndex = comboItems.FindIndex(item =>
                item.Contains("ชื่อผู้ขาย", StringComparison.OrdinalIgnoreCase) &&
                !item.Contains("ชื่อทางการค้า") &&
                !item.Contains("ชื่อผู้ติดต่อ"));
        }
        if (targetIndex < 0)
        {
            targetIndex = comboItems.FindIndex(IsVendorNameText);
        }

        if (targetIndex >= 0)
        {
            FileLogger.Log($"[SelectVendorSearchBy] Found match in items at index [{targetIndex}] '{comboItems[targetIndex]}'. Selecting via CB_SETCURSEL...");
            Win32Native.SendMessage(comboHwnd, Win32Native.CB_SETCURSEL, (IntPtr)targetIndex, IntPtr.Zero);
            Win32Native.SendMessage(findDialogHwnd, Win32Native.WM_COMMAND, (IntPtr)((Win32Native.CBN_SELCHANGE << 16) | (comboId & 0xFFFF)), comboHwnd);
            await Task.Delay(150, cancellationToken);

            var verified = Win32Native.GetComboBoxCurrentText(comboHwnd);
            if (IsVendorNameText(verified))
            {
                FileLogger.Log($"[SelectVendorSearchBy] Verified after CB_SETCURSEL: '{verified}'");
                return true;
            }
        }

        // Method 2: CB_SELECTSTRING (ANSI Windows-874 and Unicode)
        FileLogger.Log("[SelectVendorSearchBy] Trying CB_SELECTSTRING with 'ชื่อผู้ขาย'...");
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var thaiEnc = System.Text.Encoding.GetEncoding(874);
            var thaiBytes = thaiEnc.GetBytes("ชื่อผู้ขาย\0");
            Win32Native.SendMessageA(comboHwnd, Win32Native.CB_SELECTSTRING, (IntPtr)(-1), thaiBytes);
            Win32Native.SendMessage(findDialogHwnd, Win32Native.WM_COMMAND, (IntPtr)((Win32Native.CBN_SELCHANGE << 16) | (comboId & 0xFFFF)), comboHwnd);
            await Task.Delay(100, cancellationToken);

            var selectText = Win32Native.GetComboBoxCurrentText(comboHwnd);
            if (IsVendorNameText(selectText))
            {
                FileLogger.Log($"[SelectVendorSearchBy] CB_SELECTSTRING succeeded: '{selectText}'");
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[SelectVendorSearchBy] CB_SELECTSTRING error: {ex.Message}");
        }

        // Method 3: CB_SETCURSEL with index 3 (Prosoft Find standard item [3] is "ชื่อผู้ขาย")
        FileLogger.Log("[SelectVendorSearchBy] Trying CB_SETCURSEL index 3 ('ชื่อผู้ขาย')...");
        Win32Native.SendMessage(comboHwnd, Win32Native.CB_SETCURSEL, (IntPtr)3, IntPtr.Zero);
        Win32Native.SendMessage(findDialogHwnd, Win32Native.WM_COMMAND, (IntPtr)((Win32Native.CBN_SELCHANGE << 16) | (comboId & 0xFFFF)), comboHwnd);
        await Task.Delay(100, cancellationToken);

        var curSelText = Win32Native.GetComboBoxCurrentText(comboHwnd);
        if (IsVendorNameText(curSelText))
        {
            FileLogger.Log($"[SelectVendorSearchBy] CB_SETCURSEL index 3 succeeded: '{curSelText}'");
            return true;
        }

        // Method 4: UI / Keyboard simulation (Click ComboBox -> Home -> Down -> Down -> Enter)
        // This simulates exact user action
        FileLogger.Log("[SelectVendorSearchBy] Executing UI keyboard selection (Click -> Home -> Down -> Down -> Enter)...");
        Win32Native.SetForegroundWindow(findDialogHwnd);
        await Task.Delay(50, cancellationToken);

        int clickX = comboRect.Left + 25;
        int clickY = (comboRect.Top + comboRect.Bottom) / 2;
        await Win32Native.ClickScreenPointAsync(clickX, clickY, cancellationToken);
        await Task.Delay(120, cancellationToken);

        // Go to top item ([All])
        await Win32Native.SendKeyPressAsync(Win32Native.VK_HOME, cancellationToken);
        await Task.Delay(80, cancellationToken);

        // Move down 1
        await Win32Native.SendKeyPressAsync(Win32Native.VK_DOWN, cancellationToken);
        await Task.Delay(80, cancellationToken);
        var txtAfterDown1 = Win32Native.GetComboBoxCurrentText(comboHwnd);
        FileLogger.Log($"[SelectVendorSearchBy] Text after 1st Down: '{txtAfterDown1}'");

        if (!IsVendorNameText(txtAfterDown1))
        {
            // Move down 2
            await Win32Native.SendKeyPressAsync(Win32Native.VK_DOWN, cancellationToken);
            await Task.Delay(80, cancellationToken);
            var txtAfterDown2 = Win32Native.GetComboBoxCurrentText(comboHwnd);
            FileLogger.Log($"[SelectVendorSearchBy] Text after 2nd Down: '{txtAfterDown2}'");

            if (!IsVendorNameText(txtAfterDown2))
            {
                // Move down 3 (in case Fax -> เครดิต -> ชื่อทางการค้า -> ชื่อผู้ขาย)
                await Win32Native.SendKeyPressAsync(Win32Native.VK_DOWN, cancellationToken);
                await Task.Delay(80, cancellationToken);
                var txtAfterDown3 = Win32Native.GetComboBoxCurrentText(comboHwnd);
                FileLogger.Log($"[SelectVendorSearchBy] Text after 3rd Down: '{txtAfterDown3}'");
            }
        }

        // Send 'ช' as reinforcement (jumps to item starting with 'ช')
        Win32Native.SendMessage(comboHwnd, Win32Native.WM_CHAR, (IntPtr)'ช', IntPtr.Zero);
        await Task.Delay(60, cancellationToken);

        // Confirm with Enter
        await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);
        await Task.Delay(100, cancellationToken);

        // Trigger notification
        Win32Native.SendMessage(findDialogHwnd, Win32Native.WM_COMMAND, (IntPtr)((Win32Native.CBN_SELCHANGE << 16) | (comboId & 0xFFFF)), comboHwnd);

        var finalText = Win32Native.GetComboBoxCurrentText(comboHwnd);
        FileLogger.Log($"[SelectVendorSearchBy] Final ComboBox text: '{finalText}'");

        return true;
    }

    internal static bool IsVendorNameText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.Trim();
        if (clean.Contains("ชื่อทางการค้า") || clean.Contains("ชื่อผู้ติดต่อ")) return false;

        return clean.Equals("ชื่อผู้ขาย", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("ชื่อผู้จำหน่าย", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("ชื่อเจ้าหนี้", StringComparison.OrdinalIgnoreCase) ||
               clean.Contains("ชื่อผู้ขาย", StringComparison.OrdinalIgnoreCase) ||
               clean.Contains("ชื่อผู้จำหน่าย", StringComparison.OrdinalIgnoreCase) ||
               clean.Contains("Vendor Name", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("Vendor", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<VendorFillResult> ProcessGlAndSaveStandaloneAsync(
        string? department,
        bool save,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var targetDept = string.IsNullOrWhiteSpace(department) ? _options.Gl.DefaultDepartment : department;
        Report(progress, "กำลังค้นหา Prosoft process...");
        using var process = FindExistingProcess();
        using var application = Application.Attach(process);
        using var automation = new UIA3Automation();

        Report(progress, "กำลังค้นหาหน้าต่างหลักของ Prosoft...");
        var mainWindow = await FindMainWindowAsync(process, application, automation, cancellationToken);
        var mainWindowHandle = mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

        if (mainWindowHandle != IntPtr.Zero)
        {
            Win32Native.ShowWindow(mainWindowHandle, Win32Native.SW_RESTORE);
            Win32Native.SetForegroundWindow(mainWindowHandle);
            await Task.Delay(200, cancellationToken);
        }

        Report(progress, "กำลังค้นหาหน้าต่างซื้อเชื่อ (MDI Sheet)...");
        var (childHwnd, childRect, purchaseElem) = FindCreditPurchaseWindowInfo(mainWindow, mainWindowHandle);
        if (childHwnd == IntPtr.Zero && (childRect.Right - childRect.Left) <= 0)
        {
            return VendorFillResult.Error("พบหน้าหลักของ Prosoft แต่ไม่พบหน้าต่างย่อยซื้อเชื่อ");
        }

        return await ProcessGlAndSaveCoreAsync(childHwnd, childRect, purchaseElem, process, targetDept, save, progress, null, cancellationToken);
    }

    private async Task<VendorFillResult> ProcessGlAndSaveCoreAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        Process process,
        string targetDept,
        bool save,
        IProgress<string>? progress,
        VendorCsvRecord? vendorRow,
        CancellationToken cancellationToken)
    {
        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.GetWindowRect(childHwnd, out childRect);
            Win32Native.SetForegroundWindow(childHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // 1. Switch to GL tab
        Report(progress, "กำลังสลับไปแท็บ GL...");
        bool glSwitched = await SwitchToGlTabAsync(childHwnd, childRect, purchaseElem, cancellationToken);
        if (glSwitched)
        {
            Report(progress, "สลับไปแท็บ GL เรียบร้อย");
        }
        await Task.Delay(400, cancellationToken);

        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.GetWindowRect(childHwnd, out childRect);
        }

        // 2. Click "serch" (Green arrow [ > ])
        Report(progress, "กำลังกดปุ่มค้นหารูปแบบการ Post (serch ปุ่มเขียว)...");
        var greenPt = await ExecuteGlPostSearchAsync(childRect, cancellationToken);
        Report(progress, "กดปุ่ม serch เรียบร้อย รายการบัญชีถูกคำนวณแล้ว");
        await Task.Delay(500, cancellationToken);

        // 3. Check "แก้ไข GL" checkbox (crucial: must be checked before department can be selected)
        Report(progress, "กำลังติ๊กเลือก 'แก้ไข GL'...");
        bool editChecked = await EnsureEditGlCheckedAsync(childRect, greenPt, cancellationToken);
        if (editChecked)
        {
            Report(progress, "ติ๊กเลือก 'แก้ไข GL' เรียบร้อย");
        }
        else
        {
            Report(progress, "คำเตือน: ยืนยันการติ๊กเลือก 'แก้ไข GL' ไม่สำเร็จ กำลังดำเนินการต่อ...");
        }
        await Task.Delay(300, cancellationToken);

        // 4. Fill Department "INTER" for all rows
        Report(progress, $"กำลังกำหนดคอลัมน์ แผนก เป็น '{targetDept}' ทุกรายการ...");
        int rowsUpdated = await FillGlDepartmentRowsAsync(childHwnd, childRect, greenPt, targetDept, process, progress, cancellationToken);
        Report(progress, $"กำหนดแผนก '{targetDept}' ในตาราง GL สำเร็จ ({rowsUpdated} รายการ)");
        await Task.Delay(400, cancellationToken);

        // 5. Save if enabled
        if (save)
        {
            int doSuffix = 1;
            const int maxSaveRetries = 50;
            string baseDo = vendorRow?.DeliveryOrderNumber ?? "";

            // If baseDo is empty (e.g. standalone mode), read it directly from the Delivery Order field on screen
            if (string.IsNullOrWhiteSpace(baseDo))
            {
                try
                {
                    var loc = CreditPurchaseDocDetector.GetDefaultDocFieldLocations(childRect);
                    await Win32Native.ClickScreenPointAsync(loc.DeliveryOrder.X, loc.DeliveryOrder.Y, cancellationToken);
                    await Task.Delay(100, cancellationToken);
                    await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_A, cancellationToken);
                    await Task.Delay(50, cancellationToken);
                    await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_C, cancellationToken);
                    await Task.Delay(100, cancellationToken);
                    var cbText = Win32Native.GetClipboardTextSafe();
                    if (!string.IsNullOrWhiteSpace(cbText))
                    {
                        baseDo = cbText.Trim();
                        FileLogger.Log($"[SaveLoop] Read base DO from screen: '{baseDo}'");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"[SaveLoop] Failed to read DO from screen: {ex.Message}");
                }
            }

            // Always strip any existing /suffix so we increment cleanly: /1, /2, /3...
            baseDo = System.Text.RegularExpressions.Regex.Replace(baseDo, @"/\d+$", "").Trim();
            FileLogger.Log($"[SaveLoop] Clean base DO for duplicate loop: '{baseDo}'");

            while (true)
            {
                Report(progress, "กำลังกดปุ่ม Save เพื่อบันทึกเอกสาร...");
                var saveResult = await ClickSaveDocumentAsync(childHwnd, childRect, purchaseElem, process, progress, cancellationToken);
                if (saveResult.Success)
                {
                    Report(progress, "บันทึกเอกสารเรียบร้อย");
                    break;
                }

                if (saveResult.IsDuplicateDo && doSuffix <= maxSaveRetries)
                {
                    string candidateDo = string.IsNullOrWhiteSpace(baseDo)
                        ? $"DO/{doSuffix}"
                        : CreditPurchaseDocDetector.GenerateNextDeliveryOrderNumber(baseDo, doSuffix);

                    Report(progress, $"ตรวจพบ '{saveResult.Message}': กำลังแก้ไขเลขที่ใบส่งของเป็น '{candidateDo}' และกด Save อีกรอบ (ครั้งที่ {doSuffix})...");
                    FileLogger.Log($"[SaveLoop] Duplicate DO detected. Trying suffix {doSuffix}: candidate DO='{candidateDo}'...");

                    // Locate Delivery Order field in the header area
                    var loc = CreditPurchaseDocDetector.GetDefaultDocFieldLocations(childRect);
                    int doX = loc.DeliveryOrder.X;
                    int doY = loc.DeliveryOrder.Y;
                    try
                    {
                        int scanX = childRect.Left + 420;
                        int scanY = childRect.Top + 50;
                        int scanW = Math.Min(220, childRect.Right - scanX);
                        int scanH = Math.Min(90, childRect.Bottom - scanY);
                        if (scanW > 120 && scanH > 60)
                        {
                            using var scanBmp = new System.Drawing.Bitmap(scanW, scanH);
                            using (var g = System.Drawing.Graphics.FromImage(scanBmp))
                            {
                                g.CopyFromScreen(scanX, scanY, 0, 0, new System.Drawing.Size(scanW, scanH));
                            }
                            var detected = CreditPurchaseDocDetector.FindDocFieldsInBitmap(scanBmp, scanX, scanY);
                            if (detected != null && detected.DeliveryOrder.X > 0 && detected.DeliveryOrder.Y > 0)
                            {
                                doX = detected.DeliveryOrder.X;
                                doY = detected.DeliveryOrder.Y;
                            }
                        }
                    }
                    catch { }

                    if (childHwnd != IntPtr.Zero)
                    {
                        Win32Native.SetForegroundWindow(childHwnd);
                        Win32Native.EnsureEnglishKeyboardLayout(childHwnd);
                        await Task.Delay(100, cancellationToken);
                    }

                    // Click into DO field, paste candidateDo, and TAB
                    await SetFieldTextSafeAsync(doX, doY, candidateDo, cancellationToken, childHwnd);
                    if (vendorRow != null)
                    {
                        vendorRow.DeliveryOrderNumber = candidateDo;
                    }
                    doSuffix++;
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                return VendorFillResult.Error($"กำหนดแท็บ GL (แผนก: {targetDept}, {rowsUpdated} รายการ) สำเร็จ แต่บันทึกเอกสารไม่สำเร็จ: {saveResult.Message}");
            }
        }

        return VendorFillResult.Success($"กำหนดแท็บ GL (แผนก: {targetDept}, {rowsUpdated} รายการ) {(save ? "และบันทึกเอกสารเรียบร้อย" : "เรียบร้อย")}");
    }

    private async Task<bool> SwitchToGlTabAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        CancellationToken cancellationToken)
    {
        FileLogger.Log($"[SwitchToGlTab] Switching to 'GL' tab on Credit Purchase window HWND=0x{childHwnd.ToInt64():X}...");

        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(childHwnd);
            await Task.Delay(150, cancellationToken);
        }

        // 1. Try FlaUI UIA TabItem
        if (purchaseElem != null)
        {
            try
            {
                var tabItem = purchaseElem.FindFirstDescendant(cf =>
                    cf.ByName("GL").Or(cf.ByName("gl")));
                if (tabItem != null)
                {
                    FileLogger.Log($"[SwitchToGlTab] Found UIA GL tab item: '{tabItem.Name}'");
                    if (tabItem.Patterns.SelectionItem.IsSupported)
                    {
                        tabItem.Patterns.SelectionItem.Pattern.Select();
                        await Task.Delay(400, cancellationToken);
                        return true;
                    }
                    else
                    {
                        tabItem.Click();
                        await Task.Delay(400, cancellationToken);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[SwitchToGlTab] UIA tab select notice: {ex.Message}");
            }
        }

        // 2. Find PBTabControl32_80
        IntPtr tabControlHwnd = IntPtr.Zero;
        Win32Native.RECT tabRect = default;
        Win32Native.EnumChildWindows(childHwnd, (ch, _) =>
        {
            var cls = Win32Native.GetClass(ch);
            if (cls.Contains("TabControl", StringComparison.OrdinalIgnoreCase) ||
                cls.Contains("pbtab", StringComparison.OrdinalIgnoreCase))
            {
                tabControlHwnd = ch;
                Win32Native.GetWindowRect(ch, out tabRect);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        // 3. Click on screen coordinate for GL tab header
        int glX = tabControlHwnd != IntPtr.Zero ? tabRect.Left + 300 : childRect.Left + 300;
        int glY = tabControlHwnd != IntPtr.Zero ? tabRect.Bottom - 14 : childRect.Bottom - 52;

        FileLogger.Log($"[SwitchToGlTab] Clicking 'GL' tab at ({glX}, {glY})...");
        await Win32Native.ClickScreenPointAsync(glX, glY, cancellationToken);
        await Task.Delay(250, cancellationToken);

        // 4. Also send Win32 TCM_SETCURSEL / TCM_SETCURFOCUS message to tab control (index 4 = GL)
        if (tabControlHwnd != IntPtr.Zero)
        {
            FileLogger.Log("[SwitchToGlTab] Sending TCM_SETCURSEL (index=4) to TabControl...");
            Win32Native.SendMessage(tabControlHwnd, Win32Native.TCM_SETCURSEL, (IntPtr)CreditPurchaseGlDetector.GlTabIndex, IntPtr.Zero);
            Win32Native.SendMessage(tabControlHwnd, Win32Native.TCM_SETCURFOCUS, (IntPtr)CreditPurchaseGlDetector.GlTabIndex, IntPtr.Zero);
        }

        await Task.Delay(400, cancellationToken);
        return true;
    }

    private async Task<Point?> ExecuteGlPostSearchAsync(
        Win32Native.RECT childRect,
        CancellationToken cancellationToken)
    {
        FileLogger.Log("[ExecuteGlPostSearch] Clicking post search green arrow button [ > ]...");
        var defaultPoint = CreditPurchaseGlDetector.GetDefaultPostSearchButtonLocation(childRect);
        int clickX = defaultPoint.X;
        int clickY = defaultPoint.Y;
        Point? detectedPoint = null;

        try
        {
            int scanX = childRect.Left + 550;
            int scanY = childRect.Top + 140;
            int scanW = Math.Min(150, childRect.Right - scanX);
            int scanH = 80;
            if (scanW > 30 && scanH > 20)
            {
                using var bmp = new Bitmap(scanW, scanH);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(scanX, scanY, 0, 0, new Size(scanW, scanH));
                }
                var pt = CreditPurchaseGlDetector.FindGreenArrowButtonInBitmap(bmp, scanX, scanY);
                if (pt.HasValue)
                {
                    clickX = pt.Value.X;
                    clickY = pt.Value.Y;
                    detectedPoint = pt.Value;
                    FileLogger.Log($"[ExecuteGlPostSearch] Visually detected green arrow button at ({clickX}, {clickY})");
                }
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[ExecuteGlPostSearch] Visual scan notice: {ex.Message}");
        }

        FileLogger.Log($"[ExecuteGlPostSearch] Clicking green arrow button at ({clickX}, {clickY})...");
        await Win32Native.ClickScreenPointAsync(clickX, clickY, cancellationToken);
        await Task.Delay(600, cancellationToken);
        return detectedPoint ?? new Point(clickX, clickY);
    }

    private async Task<bool> EnsureEditGlCheckedAsync(
        Win32Native.RECT childRect,
        Point? greenArrowPoint,
        CancellationToken cancellationToken)
    {
        FileLogger.Log("[EnsureEditGlChecked] Ensuring 'แก้ไข GL' checkbox is checked...");

        // Anchor from green arrow: DX = -418, DY = 0
        // Or default relative to childRect: Left + 182, Top + 185
        int cbX = greenArrowPoint.HasValue
            ? greenArrowPoint.Value.X - 418
            : childRect.Left + CreditPurchaseGlDetector.EditGlCheckboxXOffset;
        int cbY = greenArrowPoint.HasValue
            ? greenArrowPoint.Value.Y
            : childRect.Top + CreditPurchaseGlDetector.TopControlsYOffset;

        FileLogger.Log($"[EnsureEditGlChecked] Target checkbox location: ({cbX}, {cbY})");

        // Check current visual state
        bool isChecked = CheckIfEditGlCheckboxIsChecked(cbX, cbY);
        FileLogger.Log($"[EnsureEditGlChecked] Initial state: isChecked={isChecked}");

        if (isChecked)
        {
            FileLogger.Log("[EnsureEditGlChecked] 'แก้ไข GL' is already checked.");
            return true;
        }

        // Retry loop up to 3 attempts to ensure it is clicked and verified checked
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            FileLogger.Log($"[EnsureEditGlChecked] Attempt {attempt}: Clicking 'แก้ไข GL' checkbox at ({cbX}, {cbY})...");
            await Win32Native.ClickScreenPointAsync(cbX, cbY, cancellationToken);
            await Task.Delay(350, cancellationToken);

            isChecked = CheckIfEditGlCheckboxIsChecked(cbX, cbY);
            FileLogger.Log($"[EnsureEditGlChecked] After click {attempt}: isChecked={isChecked}");
            if (isChecked)
            {
                FileLogger.Log("[EnsureEditGlChecked] Successfully verified 'แก้ไข GL' is checked!");
                return true;
            }

            // Fallback: Also try clicking the text label "แก้ไข GL" directly to the right (+35px)
            int labelX = cbX + 35;
            FileLogger.Log($"[EnsureEditGlChecked] Attempt {attempt} fallback: Clicking 'แก้ไข GL' label at ({labelX}, {cbY})...");
            await Win32Native.ClickScreenPointAsync(labelX, cbY, cancellationToken);
            await Task.Delay(350, cancellationToken);

            isChecked = CheckIfEditGlCheckboxIsChecked(cbX, cbY);
            FileLogger.Log($"[EnsureEditGlChecked] After label click {attempt}: isChecked={isChecked}");
            if (isChecked)
            {
                FileLogger.Log("[EnsureEditGlChecked] Successfully verified 'แก้ไข GL' is checked!");
                return true;
            }
        }

        return isChecked;
    }

    private static bool CheckIfEditGlCheckboxIsChecked(int cbX, int cbY)
    {
        try
        {
            int scanW = 16;
            int scanH = 16;
            using var bmp = new Bitmap(scanW, scanH);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(cbX - 8, cbY - 8, 0, 0, new Size(scanW, scanH));
            }

            // Inner area of the 13x13 box (excluding outer border)
            int darkPixels = 0;
            for (int dy = 3; dy <= 12; dy++)
            {
                for (int dx = 3; dx <= 12; dx++)
                {
                    var p = bmp.GetPixel(dx, dy);
                    if (p.R < 80 && p.G < 80 && p.B < 80)
                    {
                        darkPixels++;
                    }
                }
            }

            FileLogger.Log($"[CheckIfEditGlCheckboxIsChecked] Scanned at ({cbX}, {cbY}): darkPixels={darkPixels}");
            return darkPixels >= 4;
        }
        catch (Exception ex)
        {
            FileLogger.Log($"[CheckIfEditGlCheckboxIsChecked] Notice: {ex.Message}");
            return false;
        }
    }

    private async Task<int> FillGlDepartmentRowsAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        Point? greenArrowPoint,
        string departmentCode,
        Process process,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int rowCount = 0;
        int maxRows = 10;

        // Ensure Credit Purchase window and GL DataWindow have focus before interacting with rows
        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(childHwnd);
            Win32Native.EnsureEnglishKeyboardLayout(childHwnd);
        }

        // Activate GL DataWindow table area once to ensure it has keyboard focus
        int initX = greenArrowPoint.HasValue
            ? greenArrowPoint.Value.X + CreditPurchaseGlDetector.GreenArrowToDeptCellDx
            : CreditPurchaseGlDetector.GetCellLocation(childRect, 1, GlColumn.Department).X;
        int initY = greenArrowPoint.HasValue
            ? greenArrowPoint.Value.Y + CreditPurchaseGlDetector.GreenArrowToDeptDropdownDy
            : CreditPurchaseGlDetector.GetCellLocation(childRect, 1, GlColumn.Department).Y;

        FileLogger.Log($"[FillGlDepartment] Activating GL DataWindow table at ({initX}, {initY})...");
        await Win32Native.ClickScreenPointAsync(initX, initY, cancellationToken);
        await Task.Delay(250, cancellationToken);

        for (int r = 1; r <= maxRows; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (r > 2)
            {
                bool hasAccount = CheckIfGlRowHasData(childRect, greenArrowPoint, r);
                if (!hasAccount)
                {
                    FileLogger.Log($"[FillGlDepartment] Row {r} has no account data; stopping.");
                    break;
                }
            }

            Report(progress, $"กำลังกำหนดแผนก '{departmentCode}' แถวที่ {r}...");
            await SetDepartmentOnRowAsync(childHwnd, childRect, greenArrowPoint, r, departmentCode, process, cancellationToken);
            rowCount++;
            await Task.Delay(300, cancellationToken);
        }

        return rowCount;
    }

    private async Task SetDepartmentOnRowAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        Point? greenArrowPoint,
        int rowIndex,
        string departmentCode,
        Process process,
        CancellationToken cancellationToken)
    {
        // Calculate exact coordinates for the department dropdown button [ v ] and the cell
        int arrowX = greenArrowPoint.HasValue
            ? CreditPurchaseGlDetector.GetDepartmentDropdownLocationFromGreenArrow(greenArrowPoint.Value, rowIndex).X
            : CreditPurchaseGlDetector.GetDepartmentDropdownLocation(childRect, rowIndex).X;

        int cellY = greenArrowPoint.HasValue
            ? CreditPurchaseGlDetector.GetDepartmentDropdownLocationFromGreenArrow(greenArrowPoint.Value, rowIndex).Y
            : CreditPurchaseGlDetector.GetDepartmentDropdownLocation(childRect, rowIndex).Y;

        int cellX = greenArrowPoint.HasValue
            ? CreditPurchaseGlDetector.GetDepartmentCellLocationFromGreenArrow(greenArrowPoint.Value, rowIndex).X
            : CreditPurchaseGlDetector.GetCellLocation(childRect, rowIndex, GlColumn.Department).X;

        if (childHwnd != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(childHwnd);
            Win32Native.EnsureEnglishKeyboardLayout(childHwnd);
            await Task.Delay(50, cancellationToken);
        }

        FileLogger.Log($"[SetDepartmentOnRow] Row {rowIndex}: Setting Department to '{departmentCode}' at cell=({cellX}, {cellY}), arrow=({arrowX}, {cellY})...");

        // 1. Double-click inside cell text box: activates row and selects all existing text
        await Win32Native.DoubleClickScreenPointAsync(cellX, cellY, cancellationToken);
        await Task.Delay(200, cancellationToken);

        // 2. Clear existing text with Ctrl+A + Backspace so cell is completely clean
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, 0x41, cancellationToken); // Ctrl+A
        await Task.Delay(50, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_BACK, cancellationToken);
        await Task.Delay(100, cancellationToken);

        // 3. Click directly on the dropdown button [ v ] to open dropdown list
        await Win32Native.ClickScreenPointAsync(arrowX, cellY, cancellationToken);
        await Task.Delay(250, cancellationToken);

        // 4. Send key 'I' (0x49) to select INTER from the dropdown list, then Enter (0x0D) to confirm
        FileLogger.Log($"[SetDepartmentOnRow] Row {rowIndex}: Selecting '{departmentCode}' from dropdown by key 'I' + Enter...");
        await Win32Native.SendKeyPressAsync(0x49, cancellationToken); // VK_I
        await Task.Delay(150, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_RETURN, cancellationToken);
        await Task.Delay(200, cancellationToken);

        // 5. In addition, double-click cell and paste via clipboard (Ctrl+V) and commit with Tab
        FileLogger.Log($"[SetDepartmentOnRow] Row {rowIndex}: Pasting '{departmentCode}' into cell at ({cellX}, {cellY})...");
        await Win32Native.DoubleClickScreenPointAsync(cellX, cellY, cancellationToken);
        await Task.Delay(100, cancellationToken);
        Win32Native.SetClipboardTextSafe(departmentCode);
        await Win32Native.SendKeyCombinationAsync(Win32Native.VK_CONTROL, Win32Native.VK_V, cancellationToken);
        await Task.Delay(100, cancellationToken);
        await Win32Native.SendKeyPressAsync(Win32Native.VK_TAB, cancellationToken);
        await Task.Delay(200, cancellationToken);

        FileLogger.Log($"[SetDepartmentOnRow] Row {rowIndex}: Department '{departmentCode}' set successfully.");
    }

    private static bool CheckIfGlRowHasData(Win32Native.RECT childRect, Point? greenArrowPoint, int rowIndex)
    {
        try
        {
            int rowY = greenArrowPoint.HasValue
                ? greenArrowPoint.Value.Y + CreditPurchaseGlDetector.GreenArrowToDeptDropdownDy + (rowIndex - 1) * CreditPurchaseGlDetector.RowPitch
                : childRect.Top + CreditPurchaseGlDetector.FirstRowCenterOffset + (rowIndex - 1) * CreditPurchaseGlDetector.RowPitch;

            // Anchor scan area for account code (รหัสบัญชี) to green arrow or childRect
            int scanX = greenArrowPoint.HasValue
                ? greenArrowPoint.Value.X - 545
                : childRect.Left + 56;
            int scanW = 90;
            int scanH = 12;
            using var bmp = new Bitmap(scanW, scanH);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(scanX, rowY - 6, 0, 0, new Size(scanW, scanH));
            }

            int darkPixels = 0;
            for (int y = 0; y < scanH; y++)
            {
                for (int x = 0; x < scanW; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.R < 100 && p.G < 100 && p.B < 100)
                    {
                        darkPixels++;
                    }
                }
            }

            FileLogger.Log($"[CheckIfGlRowHasData] Row {rowIndex} at Y={rowY}: darkPixels={darkPixels}");
            return darkPixels >= 8;
        }
        catch
        {
            return rowIndex <= 2;
        }
    }

    private async Task<(bool Success, bool IsDuplicateDo, string Message)> ClickSaveDocumentAsync(
        IntPtr childHwnd,
        Win32Native.RECT childRect,
        AutomationElement? purchaseElem,
        Process process,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var savePt = CreditPurchaseGlDetector.GetDefaultSaveButtonLocation(childRect);
        int clickX = savePt.X;
        int clickY = savePt.Y;

        bool uiaClicked = false;
        if (purchaseElem != null)
        {
            try
            {
                var saveBtn = purchaseElem.FindFirstDescendant(cf =>
                    cf.ByName("Save").Or(cf.ByName("บันทึก")));
                if (saveBtn != null)
                {
                    FileLogger.Log($"[ClickSaveDocument] Found UIA Save button: '{saveBtn.Name}'");
                    if (saveBtn.Patterns.Invoke.IsSupported)
                    {
                        saveBtn.Patterns.Invoke.Pattern.Invoke();
                        uiaClicked = true;
                    }
                    else
                    {
                        saveBtn.Click();
                        uiaClicked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[ClickSaveDocument] UIA Save notice: {ex.Message}");
            }
        }

        if (!uiaClicked)
        {
            FileLogger.Log($"[ClickSaveDocument] Clicking toolbar Save button at ({clickX}, {clickY})...");
            await Win32Native.ClickScreenPointAsync(clickX, clickY, cancellationToken);
        }

        await Task.Delay(400, cancellationToken);

        return await HandleSaveConfirmationPopupAsync(childHwnd, process, progress, cancellationToken);
    }

    private static async Task ClickDialogButtonCleanlyAsync(
        IntPtr buttonHwnd,
        IntPtr dialogHwnd,
        bool isYesButton,
        CancellationToken cancellationToken)
    {
        if (buttonHwnd == IntPtr.Zero) return;

        if (dialogHwnd != IntPtr.Zero)
        {
            Win32Native.SetForegroundWindow(dialogHwnd);
            await Task.Delay(50, cancellationToken);
        }

        Win32Native.GetWindowRect(buttonHwnd, out var bRect);
        int bx = (bRect.Left + bRect.Right) / 2;
        int by = (bRect.Top + bRect.Bottom) / 2;

        if (bx > 0 && by > 0)
        {
            FileLogger.Log($"[ClickDialogButtonCleanly] Physical click on button HWND=0x{buttonHwnd.ToInt64():X} at ({bx}, {by})...");
            await Win32Native.ClickScreenPointAsync(bx, by, cancellationToken);
        }
        else
        {
            FileLogger.Log($"[ClickDialogButtonCleanly] SendMessage BM_CLICK on button HWND=0x{buttonHwnd.ToInt64():X}...");
            Win32Native.SendMessage(buttonHwnd, (uint)Win32Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }

        // Wait up to 500ms for dialog to close
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(100, cancellationToken);
            if (dialogHwnd == IntPtr.Zero || !Win32Native.IsWindow(dialogHwnd) || !Win32Native.IsWindowVisible(dialogHwnd))
            {
                FileLogger.Log($"[ClickDialogButtonCleanly] Dialog HWND=0x{dialogHwnd.ToInt64():X} closed successfully.");
                return;
            }
        }

        // Fallback only if dialog is still visible
        if (Win32Native.IsWindow(dialogHwnd) && Win32Native.IsWindowVisible(dialogHwnd))
        {
            FileLogger.Log($"[ClickDialogButtonCleanly] Dialog still open; sending BM_CLICK to button...");
            Win32Native.SendMessage(buttonHwnd, (uint)Win32Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(150, cancellationToken);

            if (isYesButton && Win32Native.IsWindow(dialogHwnd) && Win32Native.IsWindowVisible(dialogHwnd))
            {
                FileLogger.Log($"[ClickDialogButtonCleanly] Dialog still open; sending WM_COMMAND IDYES (6)...");
                Win32Native.SendMessage(dialogHwnd, (uint)Win32Native.WM_COMMAND, (IntPtr)6, buttonHwnd);
                await Task.Delay(150, cancellationToken);
            }
        }
    }

    private async Task<(bool Success, bool IsDuplicateDo, string Message)> HandleSaveConfirmationPopupAsync(
        IntPtr childHwnd,
        Process process,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        FileLogger.Log("[HandleSavePopup] Checking for save confirmation popup...");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        bool handledConfirmationPrompt = false;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IntPtr popupHwnd = IntPtr.Zero;
            string popupTitle = "";
            Win32Native.EnumWindows((h, _) =>
            {
                uint pid;
                Win32Native.GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)process.Id && Win32Native.IsWindowVisible(h))
                {
                    if (h == childHwnd) return true; // NEVER treat Credit Purchase window as a popup dialog!

                    var cls = Win32Native.GetClass(h);
                    var txt = Win32Native.GetText(h);

                    // Must be a standard modal dialog (#32770) and not the main sheet
                    if (cls != "#32770") return true;
                    if (txt.Equals("ซื้อเชื่อ", StringComparison.OrdinalIgnoreCase)) return true;

                    popupHwnd = h;
                    popupTitle = txt;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (popupHwnd != IntPtr.Zero)
            {
                var msgList = new List<string>();
                Win32Native.EnumChildWindows(popupHwnd, (ch, _) =>
                {
                    var cls = Win32Native.GetClass(ch);
                    var txt = Win32Native.GetText(ch);
                    if (cls.Equals("Static", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(txt))
                    {
                        msgList.Add(txt.Trim());
                    }
                    return true;
                }, IntPtr.Zero);

                var fullPopupMsg = string.Join(" ", msgList);
                FileLogger.Log($"[HandleSavePopup] Found popup HWND=0x{popupHwnd.ToInt64():X} Title='{popupTitle}' Message='{fullPopupMsg}'");

                bool isDuplicateDo = CreditPurchaseDocDetector.IsDuplicateDeliveryOrderMessage(fullPopupMsg, popupTitle);
                bool isConfirmPrompt = CreditPurchaseGlDetector.IsSaveConfirmationPrompt(fullPopupMsg, popupTitle);
                bool isWarningOrError = CreditPurchaseGlDetector.IsSaveWarningOrError(popupTitle, fullPopupMsg);

                IntPtr targetBtnHwnd = CreditPurchaseGlDetector.FindDialogButtonHwnd(popupHwnd, isConfirmPrompt);

                if (isDuplicateDo)
                {
                    var warnText = string.IsNullOrWhiteSpace(fullPopupMsg) ? popupTitle : $"{popupTitle}: {fullPopupMsg}";
                    Report(progress, $"ตรวจพบข้อความเตือน: '{warnText}'");
                    FileLogger.Log($"[HandleSavePopup] Primary popup is Duplicate DO: {warnText}");
                    await Task.Delay(2000, cancellationToken);
                    if (targetBtnHwnd != IntPtr.Zero)
                    {
                        await ClickDialogButtonCleanlyAsync(targetBtnHwnd, popupHwnd, isYesButton: false, cancellationToken);
                    }
                    return (false, true, warnText);
                }

                if (isConfirmPrompt)
                {
                    FileLogger.Log($"[HandleSavePopup] Save confirmation prompt detected ('{fullPopupMsg}'). Clicking 'Yes' button cleanly...");
                    Report(progress, "ตรวจพบข้อความเตือนเลขที่เอกสาร: กำลังกด 'Yes' เพื่อยืนยันการบันทึก...");

                    await ClickDialogButtonCleanlyAsync(targetBtnHwnd, popupHwnd, isYesButton: true, cancellationToken);

                    handledConfirmationPrompt = true;
                    Report(progress, "กดยืนยัน 'Yes' เรียบร้อย กำลังรอผลการตรวจสอบและบันทึก...");
                    FileLogger.Log("[HandleSavePopup] Yes confirmed. Waiting up to 8s for follow-up validation popup (e.g. 'เลขที่ใบส่งของ เป็นค่าซ้ำ !!')...");

                    // Wait up to 8s for potential follow-up popup (like "คำเตือน: เลขที่ใบส่งของ เป็นค่าซ้ำ !!")
                    var followUpDeadline = DateTime.UtcNow.AddSeconds(8);
                    while (DateTime.UtcNow < followUpDeadline)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IntPtr followHwnd = IntPtr.Zero;
                        string followTitle = "";
                        Win32Native.EnumWindows((fh, _) =>
                        {
                            uint fpid;
                            Win32Native.GetWindowThreadProcessId(fh, out fpid);
                            if (fpid == (uint)process.Id && Win32Native.IsWindowVisible(fh))
                            {
                                if (fh == childHwnd) return true;
                                if (fh == popupHwnd) return true;
                                var fcls = Win32Native.GetClass(fh);
                                var ftxt = Win32Native.GetText(fh);
                                if (fcls == "#32770" && !ftxt.Equals("ซื้อเชื่อ", StringComparison.OrdinalIgnoreCase))
                                {
                                    followHwnd = fh;
                                    followTitle = ftxt;
                                    return false;
                                }
                            }
                            return true;
                        }, IntPtr.Zero);

                        if (followHwnd != IntPtr.Zero)
                        {
                            // Read message text from follow-up popup
                            var followMsgs = new List<string>();
                            Win32Native.EnumChildWindows(followHwnd, (fch, _) =>
                            {
                                var fcls = Win32Native.GetClass(fch);
                                var ftxt = Win32Native.GetText(fch);
                                if (fcls.Equals("Static", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ftxt))
                                {
                                    followMsgs.Add(ftxt.Trim());
                                }
                                return true;
                            }, IntPtr.Zero);
                            var fullFollowMsg = string.Join(" ", followMsgs);

                            FileLogger.Log($"[HandleSavePopup] Follow-up popup HWND=0x{followHwnd.ToInt64():X} Title='{followTitle}' Message='{fullFollowMsg}'.");
                            Report(progress, $"ตรวจพบข้อความแจ้งเตือน: '{followTitle}: {fullFollowMsg}'");

                            // Crucial: Pause 2.0 seconds so user clearly sees the warning popup on screen!
                            await Task.Delay(2000, cancellationToken);

                            IntPtr okBtn = Win32Native.FindOkButtonHwnd(followHwnd);
                            if (okBtn != IntPtr.Zero)
                            {
                                await ClickDialogButtonCleanlyAsync(okBtn, followHwnd, isYesButton: false, cancellationToken);
                            }
                            await Task.Delay(300, cancellationToken);

                            bool followIsDup = CreditPurchaseDocDetector.IsDuplicateDeliveryOrderMessage(fullFollowMsg, followTitle);
                            if (followIsDup)
                            {
                                FileLogger.Log($"[HandleSavePopup] Follow-up popup was Duplicate DO: '{followTitle}: {fullFollowMsg}'.");
                                return (false, true, $"{followTitle}: {fullFollowMsg}");
                            }

                            bool followIsError = CreditPurchaseGlDetector.IsSaveWarningOrError(followTitle, fullFollowMsg);
                            if (followIsError)
                            {
                                FileLogger.Log($"[HandleSavePopup] Follow-up popup was warning/error: '{followTitle}: {fullFollowMsg}'.");
                                return (false, false, $"{followTitle}: {fullFollowMsg}");
                            }

                            // Follow-up was an informational popup (e.g. "บันทึกเรียบร้อย")
                            FileLogger.Log("[HandleSavePopup] Follow-up popup dismissed. Save completed.");
                            return (true, false, "บันทึกเอกสารเรียบร้อย");
                        }

                        await Task.Delay(200, cancellationToken);
                    }

                    FileLogger.Log("[HandleSavePopup] Save completed successfully (no subsequent error dialog after 8s).");
                    return (true, false, "บันทึกเอกสารเรียบร้อย");
                }

                if (isWarningOrError)
                {
                    var warnText = string.IsNullOrWhiteSpace(fullPopupMsg) ? popupTitle : $"{popupTitle}: {fullPopupMsg}";
                    Report(progress, $"ตรวจพบข้อความเตือนจาก Prosoft: '{warnText}'");
                    FileLogger.Log($"[HandleSavePopup] Save resulted in warning/error: {warnText}");
                    await Task.Delay(2000, cancellationToken);
                    if (targetBtnHwnd != IntPtr.Zero)
                    {
                        await ClickDialogButtonCleanlyAsync(targetBtnHwnd, popupHwnd, isYesButton: false, cancellationToken);
                    }
                    return (false, false, warnText);
                }

                // General popup
                if (targetBtnHwnd != IntPtr.Zero)
                {
                    await ClickDialogButtonCleanlyAsync(targetBtnHwnd, popupHwnd, isYesButton: false, cancellationToken);
                }
                Report(progress, $"ตรวจพบ Popup บันทึก: '{popupTitle}' — ดำเนินการเรียบร้อย");
                return (true, false, "บันทึกเอกสารเรียบร้อย");
            }

            if (handledConfirmationPrompt)
            {
                FileLogger.Log("[HandleSavePopup] Save confirmation prompt handled and no subsequent modal dialog detected.");
                return (true, false, "บันทึกเอกสารเรียบร้อย");
            }

            await Task.Delay(250, cancellationToken);
        }

        FileLogger.Log("[HandleSavePopup] No modal confirmation popup appeared (save completed directly).");
        return (true, false, "บันทึกเอกสารเรียบร้อย");
    }
}

using System.Windows;
using System.Windows.Input;
using ProsoftAutoLogin.Automation;
using ProsoftAutoLogin.Configuration;

namespace ProsoftAutoLogin;

public partial class MainWindow : Window
{
    private readonly ProsoftAutomationService _automation;
    private CancellationTokenSource? _loginCancellation;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            var settings = SettingsLoader.Load();
            _automation = new ProsoftAutomationService(settings.Prosoft);
        }
        catch (Exception ex)
        {
            _automation = new ProsoftAutomationService(new ProsoftOptions());
            Loaded += (_, _) =>
                StatusText.Text = $"อ่าน appsettings.json ไม่สำเร็จ: {ex.Message}";
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameInput.Text?.Trim();
        var password = PasswordInput.Password;
        var profile = ProfileInput.Text?.Trim();

        SetBusy(true);
        _loginCancellation = new CancellationTokenSource();

        FileLogger.Log($"[UI] กด Login Prosoft: Profile={profile}, User={username}, PasswordProvided={!string.IsNullOrWhiteSpace(password)}, OpenCreditPurchase={OpenCreditPurchaseCheckBox.IsChecked}");

        var progress = new Progress<string>(message =>
        {
            StatusText.Text = message;
            FileLogger.Log($"[Progress] {message}");
        });
        var openCreditPurchase = OpenCreditPurchaseCheckBox.IsChecked ?? false;
        var fillVendorFromCsv = FillVendorFromCsvCheckBox.IsChecked ?? false;

        try
        {
            var result = await _automation.LoginAsync(
                username,
                password,
                profile,
                openCreditPurchase,
                fillVendorFromCsv,
                progress,
                _loginCancellation.Token);

            StatusText.Text = result.Message;
            FileLogger.Log($"[Result] Success={result.IsSuccess}: {result.Message}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ยกเลิกการทำงานแล้ว";
            FileLogger.Log("[Cancel] ยกเลิกการทำงานแล้ว");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"เกิดข้อผิดพลาด: {ex.Message}";
            FileLogger.Log($"[Error] {ex.Message}");
        }
        finally
        {
            PasswordInput.Clear();
            _loginCancellation?.Dispose();
            _loginCancellation = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _loginCancellation?.Cancel();
    }

    private async void NavigateCreditPurchaseButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        _loginCancellation = new CancellationTokenSource();

        var progress = new Progress<string>(message => StatusText.Text = message);

        try
        {
            var result = await _automation.NavigateToCreditPurchaseAsync(
                progress,
                _loginCancellation.Token);

            StatusText.Text = result.Message;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ยกเลิกการเปิดหน้าซื้อเชื่อแล้ว";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"เกิดข้อผิดพลาด: {ex.Message}";
        }
        finally
        {
            _loginCancellation?.Dispose();
            _loginCancellation = null;
            SetBusy(false);
        }
    }

    private async void FillVendorButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        _loginCancellation = new CancellationTokenSource();

        var progress = new Progress<string>(message =>
        {
            StatusText.Text = message;
            FileLogger.Log($"[Progress] {message}");
        });

        try
        {
            var result = await _automation.FillVendorFromCsvAsync(
                null,
                progress,
                _loginCancellation.Token);

            StatusText.Text = result.Message;
            FileLogger.Log($"[Vendor Result] Success={result.IsSuccess}: {result.Message}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ยกเลิกการกรอกข้อมูลผู้ขายแล้ว";
            FileLogger.Log("[Cancel] ยกเลิกการกรอกข้อมูลผู้ขาย");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"เกิดข้อผิดพลาด: {ex.Message}";
            FileLogger.Log($"[Vendor Error] {ex.Message}");
        }
        finally
        {
            _loginCancellation?.Dispose();
            _loginCancellation = null;
            SetBusy(false);
        }
    }

    private async void ProcessGlButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        _loginCancellation = new CancellationTokenSource();

        var progress = new Progress<string>(message =>
        {
            StatusText.Text = message;
            FileLogger.Log($"[Progress] {message}");
        });

        try
        {
            var result = await _automation.ProcessGlAndSaveAsync(
                null,
                true,
                progress,
                _loginCancellation.Token);

            StatusText.Text = result.Message;
            FileLogger.Log($"[GL Result] Success={result.IsSuccess}: {result.Message}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ยกเลิกการทำงานแท็บ GL แล้ว";
            FileLogger.Log("[Cancel] ยกเลิกการทำงานแท็บ GL");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"เกิดข้อผิดพลาด: {ex.Message}";
            FileLogger.Log($"[GL Error] {ex.Message}");
        }
        finally
        {
            _loginCancellation?.Dispose();
            _loginCancellation = null;
            SetBusy(false);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        StatusText.Text = "กำลังอ่าน UI controls ของ Prosoft...";

        try
        {
            var outputPath = await _automation.ExportUiTreeAsync(CancellationToken.None);
            StatusText.Text = $"Export สำเร็จ: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export ไม่สำเร็จ: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LoginButton.IsEnabled)
        {
            LoginButton_Click(LoginButton, new RoutedEventArgs());
        }
    }

    private void LaunchProsoftButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var defaultPath = @"C:\Program Files (x86)\Prosoft\myAccount\Bin\myaccount.exe";
            if (System.IO.File.Exists(defaultPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = defaultPath,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(defaultPath) ?? string.Empty,
                    UseShellExecute = true
                });
                StatusText.Text = "เปิดโปรแกรม Prosoft แล้ว กรุณารอหน้าต่าง Login ปรากฏ";
            }
            else
            {
                StatusText.Text = $"ไม่พบไฟล์ติดตั้ง Prosoft ที่: {defaultPath}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"เปิด Prosoft ไม่สำเร็จ: {ex.Message}";
        }
    }

    private void SetBusy(bool isBusy)
    {
        LoginButton.IsEnabled = !isBusy;
        LaunchProsoftButton.IsEnabled = !isBusy;
        NavigateCreditPurchaseButton.IsEnabled = !isBusy;
        FillVendorButton.IsEnabled = !isBusy;
        ProcessGlButton.IsEnabled = !isBusy;
        ExportButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = isBusy && _loginCancellation is not null;
        PasswordInput.IsEnabled = !isBusy;
        UsernameInput.IsEnabled = !isBusy;
        ProfileInput.IsEnabled = !isBusy;
        OpenCreditPurchaseCheckBox.IsEnabled = !isBusy;
        FillVendorFromCsvCheckBox.IsEnabled = !isBusy;
    }
}

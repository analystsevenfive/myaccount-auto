using System.Windows;
using ProsoftAutoLogin.Automation;
using ProsoftAutoLogin.Configuration;

namespace ProsoftAutoLogin;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => string.Equals(a, "--export-ui-tree", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var settings = SettingsLoader.Load();
                var automation = new ProsoftAutomationService(settings.Prosoft);
                var path = await automation.ExportUiTreeAsync(CancellationToken.None);
                Console.WriteLine($"[UI_TREE_EXPORT_SUCCESS] {path}");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UI_TREE_EXPORT_ERROR] {ex.Message}");
                Shutdown(1);
            }
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

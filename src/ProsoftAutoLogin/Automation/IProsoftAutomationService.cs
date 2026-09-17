using ProsoftAutoLogin.Models;

namespace ProsoftAutoLogin.Automation;

public interface IProsoftAutomationService
{
    Task<LoginResult> LoginAsync(
        string? password,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        string? profile,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        string? profile,
        bool openCreditPurchase,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<LoginResult> LoginAsync(
        string? username,
        string? password,
        string? profile,
        bool openCreditPurchase,
        bool fillVendorFromCsv,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<NavigationResult> NavigateToCreditPurchaseAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<VendorFillResult> FillVendorFromCsvAsync(
        string? csvPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<VendorFillResult> ProcessGlAndSaveAsync(
        string? department,
        bool save,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<string> ExportUiTreeAsync(CancellationToken cancellationToken);
}

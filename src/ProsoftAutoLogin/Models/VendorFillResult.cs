namespace ProsoftAutoLogin.Models;

public sealed record VendorFillResult(
    bool IsSuccess,
    string Message,
    string? VendorName = null,
    string? VendorCode = null)
{
    public static VendorFillResult Success(string message, string? vendorName = null, string? vendorCode = null) =>
        new(true, message, vendorName, vendorCode);

    public static VendorFillResult Error(string message) =>
        new(false, message);

    public static VendorFillResult Timeout(string message) =>
        new(false, message);
}

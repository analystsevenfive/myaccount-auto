namespace ProsoftAutoLogin.Models;

public enum NavigationResultKind
{
    Success,
    Error,
    Timeout
}

public sealed record NavigationResult(NavigationResultKind Kind, string Message)
{
    public bool IsSuccess => Kind == NavigationResultKind.Success;

    public static NavigationResult Success(string message) =>
        new(NavigationResultKind.Success, message);

    public static NavigationResult Error(string message) =>
        new(NavigationResultKind.Error, message);

    public static NavigationResult Timeout(string message) =>
        new(NavigationResultKind.Timeout, message);
}

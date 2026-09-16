namespace ProsoftAutoLogin.Models;

public enum LoginResultKind
{
    Success,
    Error,
    Timeout
}

public sealed record LoginResult(LoginResultKind Kind, string Message)
{
    public bool IsSuccess => Kind == LoginResultKind.Success;

    public static LoginResult Success(string message) =>
        new(LoginResultKind.Success, message);

    public static LoginResult Error(string message) =>
        new(LoginResultKind.Error, message);

    public static LoginResult Timeout(string message) =>
        new(LoginResultKind.Timeout, message);
}

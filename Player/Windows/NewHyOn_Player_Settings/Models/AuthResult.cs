namespace NewHyOn.Player.Settings.Models;

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public bool IsLicensed { get; init; }
    public bool DisablePasswordInput { get; init; }
    public string Message { get; init; } = string.Empty;
}

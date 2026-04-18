namespace NewHyOn.Settings.Wpf.Models;

public sealed class SettingsLoadResult
{
    public SettingsFormData Data { get; init; } = new();

    public bool LoadedFromRemote { get; init; }

    public bool UsedLocalFallback { get; init; }

    public string? RemoteErrorMessage { get; init; }
}

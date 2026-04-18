namespace NewHyOn.Settings.Wpf.Models;

public sealed class SettingsSaveResult
{
    public bool SavedToLocal { get; init; }

    public bool SavedToRemote { get; init; }

    public string? Message { get; init; }

    public string? RemoteErrorMessage { get; init; }

    public bool SavedLocallyOnly => SavedToLocal && SavedToRemote == false;
}

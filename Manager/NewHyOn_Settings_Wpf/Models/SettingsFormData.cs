namespace NewHyOn.Settings.Wpf.Models;

public sealed class SettingsFormData
{
    public string DataServerIp { get; set; } = "127.0.0.1";

    public string MessageServerIp { get; set; } = "127.0.0.1";

    public int FtpPort { get; set; } = 10021;

    public int PasvMinPort { get; set; } = 24000;

    public int PasvMaxPort { get; set; } = 24240;

    public string FtpRootPath { get; set; } = "/NewHyOn";

    public bool PreserveAspectRatio { get; set; }
}

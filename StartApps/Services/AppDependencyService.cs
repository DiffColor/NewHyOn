using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using StartApps.Models;

namespace StartApps.Services;

public class AppDependencyService
{
    public const int DefaultFtpPort = 10021;

    private const string RethinkDbArchiveName = "rethinkdb.zip";
    private const string FtpSrvArchiveName = "ftpsrv.zip";
    private const string FtpSrvFolderName = "ftpsrv";
    private const string FileZillaConfigFileName = "FileZilla Server.xml";
    private const string SignalrNet472ArchiveName = "signalr_net472.zip";
    private const string SignalrNet10ArchiveName = "signalr_net10.zip";
    private const string SignalrNet472FolderName = "signalr";
    private const string SignalrNet10FolderName = "signalr10";
    private const string SignalrNet472ExeName = "SignalR_Net472.exe";
    private const string SignalrNet10ExeName = "signalr_net10linux.exe";
    private const string NtpServerArchiveName = "ntpserver.zip";
    private const string NtpServerExeName = "NtpServer.exe";
    private const string NtpServerFolderName = "NtpServer";
    private const string DefaultWorkspaceFolderName = "Turtle Lab";
    private const string DefaultNewHyOnFolderName = "NewHyOn Manager";
    private const string DefaultDataFolderName = "Data";
    private const string DefaultPlayerFolderName = "NewHyOn Player";
    private const string MyDocumentsToken = "{MyDocuments}";
    private static readonly JsonSerializerOptions ProfileDefaultsSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly AppProfile _profile;
    public string StorageRoot { get; }

    public AppDependencyService(AppProfile profile)
    {
        _profile = profile;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        StorageRoot = Path.Combine(appData, profile.StorageFolderName, "Apps");
        Directory.CreateDirectory(StorageRoot);
    }

    public string GetExecutablePath(AppType type) =>
        type switch
        {
            AppType.Rdb => Path.Combine(StorageRoot, "rethinkdb", "rethinkdb.exe"),
            AppType.Ftp => Path.Combine(StorageRoot, FtpSrvFolderName, "FileZilla Server.exe"),
            AppType.Msg => Path.Combine(StorageRoot, SignalrNet472FolderName, SignalrNet472ExeName),
            AppType.Msg472 => Path.Combine(StorageRoot, SignalrNet472FolderName, SignalrNet472ExeName),
            AppType.Msg10 => Path.Combine(StorageRoot, SignalrNet10FolderName, SignalrNet10ExeName),
            AppType.Ntp => Path.Combine(StorageRoot, NtpServerFolderName, NtpServerExeName),
            _ => string.Empty
        };

    public string GetFtpInterfacePath() =>
        Path.Combine(StorageRoot, FtpSrvFolderName, "FileZilla Server Interface.exe");

    public string GetDefaultFtpHomeDirectory()
    {
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var defaultPath = _profile.Id switch
        {
            AppProfile.ManagerId => Path.Combine(
                myDocuments,
                DefaultWorkspaceFolderName,
                DefaultNewHyOnFolderName,
                "Manager",
                DefaultDataFolderName),
            AppProfile.PlayerId => Path.Combine(
                myDocuments,
                DefaultWorkspaceFolderName,
                DefaultNewHyOnFolderName,
                "Player",
                DefaultDataFolderName),
            _ => Path.Combine(
                myDocuments,
                DefaultWorkspaceFolderName,
                DefaultNewHyOnFolderName,
                DefaultDataFolderName)
        };

        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    public IReadOnlyList<AppDefinition> CreateDefaultAppDefinitions()
    {
        var seededDefinitions = LoadProfileSeedDefinitions(_profile.Id)
            ?? (!_profile.IsDefault ? LoadProfileSeedDefinitions(AppProfile.DefaultId) : null)
            ?? CreateFallbackDefaultAppDefinitions();

        return seededDefinitions
            .Select((definition, index) => FinalizeSeedDefinition(definition, index))
            .ToList();
    }

    public async Task EnsureDependenciesAsync(AppType type, CancellationToken cancellationToken = default)
    {
        if (type == AppType.Rdb)
        {
            var executablePath = GetExecutablePath(AppType.Rdb);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(RethinkDbArchiveName, Path.Combine(StorageRoot, "rethinkdb"), cancellationToken);
            }
        }
        else if (type == AppType.Ftp)
        {
            var executablePath = GetExecutablePath(AppType.Ftp);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(FtpSrvArchiveName, Path.Combine(StorageRoot, FtpSrvFolderName), cancellationToken);
            }
        }
        else if (type == AppType.Msg || type == AppType.Msg472)
        {
            var executablePath = GetExecutablePath(type);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(SignalrNet472ArchiveName, Path.Combine(StorageRoot, SignalrNet472FolderName), cancellationToken);
            }
        }
        else if (type == AppType.Msg10)
        {
            var executablePath = GetExecutablePath(AppType.Msg10);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(SignalrNet10ArchiveName, Path.Combine(StorageRoot, SignalrNet10FolderName), cancellationToken);
            }
        }
        else if (type == AppType.Ntp)
        {
            var executablePath = GetExecutablePath(AppType.Ntp);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(NtpServerArchiveName, Path.Combine(StorageRoot, NtpServerFolderName), cancellationToken);
            }
        }
    }

    public void ApplyFtpConfiguration(AppDefinition definition)
    {
        if (definition.Type != AppType.Ftp)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.FtpHomeDirectory))
        {
            definition.FtpHomeDirectory = GetDefaultFtpHomeDirectory();
        }

        var configPath = Path.Combine(StorageRoot, FtpSrvFolderName, FileZillaConfigFileName);
        if (!File.Exists(configPath))
        {
            return;
        }

        var document = XDocument.Load(configPath);
        var root = document.Root;
        if (root == null)
        {
            return;
        }

        var settingsElement = root.Element("Settings");
        if (settingsElement != null)
        {
            var ftpPort = definition.Port ?? DefaultFtpPort;
            var (passiveMin, passiveMax) = ParsePassiveRange(definition.PassivePortRange);
            definition.PassivePortRange = $"{passiveMin}-{passiveMax}";

            SetItem(settingsElement, "Serverports", ftpPort.ToString(CultureInfo.InvariantCulture));
            SetItem(settingsElement, "Use custom PASV ports", "1");
            SetItem(settingsElement, "Custom PASV min port", passiveMin.ToString(CultureInfo.InvariantCulture));
            SetItem(settingsElement, "Custom PASV max port", passiveMax.ToString(CultureInfo.InvariantCulture));
        }

        var usersElement = root.Element("Users");
        if (usersElement == null)
        {
            usersElement = new XElement("Users");
            root.Add(usersElement);
        }

        usersElement.RemoveNodes();
        var username = string.IsNullOrWhiteSpace(definition.FtpUsername) ? "asdf" : definition.FtpUsername;
        var userElement = new XElement("User", new XAttribute("Name", username));
        usersElement.Add(userElement);

        AddUserOptions(userElement, definition);
        document.Save(configPath);
    }

    private static void SetItem(XElement settingsElement, string name, string value)
    {
        var itemElement = settingsElement.Elements("Item")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), name, StringComparison.OrdinalIgnoreCase));

        if (itemElement != null)
        {
            itemElement.Value = value;
        }
    }

    private static void AddUserOptions(XElement userElement, AppDefinition definition)
    {
        userElement.Add(
            new XElement("Option", new XAttribute("Name", "Pass"), HashPassword(definition.FtpPassword ?? "")),
            new XElement("Option", new XAttribute("Name", "Group"), string.Empty),
            new XElement("Option", new XAttribute("Name", "Bypass server userlimit"), "0"),
            new XElement("Option", new XAttribute("Name", "User Limit"), "0"),
            new XElement("Option", new XAttribute("Name", "IP Limit"), "0"),
            new XElement("Option", new XAttribute("Name", "Enabled"), "1"),
            new XElement("Option", new XAttribute("Name", "Comments"), string.Empty),
            new XElement("Option", new XAttribute("Name", "ForceSsl"), "0")
        );

        var ipFilter = new XElement("IpFilter",
            new XElement("Disallowed"),
            new XElement("Allowed"));
        userElement.Add(ipFilter);

        var permissions = new XElement("Permissions");
        var home = definition.FtpHomeDirectory;
        if (!string.IsNullOrWhiteSpace(home) && !Directory.Exists(home))
        {
            Directory.CreateDirectory(home);
        }
        var permission = new XElement("Permission", new XAttribute("Dir", home));
        permissions.Add(permission);
        userElement.Add(permissions);

        void SetPermission(string name, bool allow) =>
            permission.Add(new XElement("Option", new XAttribute("Name", name), allow ? "1" : "0"));

        permission.Add(new XElement("Option", new XAttribute("Name", "FileRead"), definition.FtpAllowRead ? "1" : "0"));
        SetPermission("FileWrite", definition.FtpAllowWrite);
        SetPermission("FileDelete", definition.FtpAllowWrite);
        SetPermission("FileAppend", definition.FtpAllowWrite);
        permission.Add(new XElement("Option", new XAttribute("Name", "DirList"), definition.FtpAllowRead ? "1" : "0"));
        SetPermission("DirCreate", definition.FtpAllowWrite);
        SetPermission("DirDelete", definition.FtpAllowWrite);
        permission.Add(new XElement("Option", new XAttribute("Name", "DirSubdirs"), definition.FtpAllowRead ? "1" : "0"));
        permission.Add(new XElement("Option", new XAttribute("Name", "IsHome"), "1"));
        permission.Add(new XElement("Option", new XAttribute("Name", "AutoCreate"), definition.FtpAllowWrite ? "1" : "0"));

        var speedLimits = new XElement("SpeedLimits",
            new XAttribute("DlType", "0"),
            new XAttribute("DlLimit", "10"),
            new XAttribute("ServerDlLimitBypass", "0"),
            new XAttribute("UlType", "0"),
            new XAttribute("UlLimit", "10"),
            new XAttribute("ServerUlLimitBypass", "0"),
            new XElement("Download"),
            new XElement("Upload"));
        userElement.Add(speedLimits);
    }

    private static (int Min, int Max) ParsePassiveRange(string? range)
    {
        const int defaultMin = 24000;
        const int defaultMax = 24240;

        if (string.IsNullOrWhiteSpace(range))
        {
            return (defaultMin, defaultMax);
        }

        var parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var min = parts.Length > 0 && int.TryParse(parts[0], out var parsedMin) ? parsedMin : defaultMin;
        var max = parts.Length > 1 && int.TryParse(parts[1], out var parsedMax) ? parsedMax : min;

        if (max < min)
        {
            (min, max) = (max, min);
        }

        return (min, max);
    }

    private static string HashPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = MD5.HashData(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    private List<AppDefinition>? LoadProfileSeedDefinitions(string profileId)
    {
        using var stream = TryOpenProfileSeedStream(profileId);
        if (stream == null)
        {
            return null;
        }

        var definitions = JsonSerializer.Deserialize<List<AppDefinition>>(stream, ProfileDefaultsSerializerOptions);
        return definitions ?? [];
    }

    private Stream? TryOpenProfileSeedStream(string profileId)
    {
        var assembly = typeof(AppDependencyService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($"Profiles.{profileId}.apps.json", StringComparison.OrdinalIgnoreCase));

        return resourceName == null ? null : assembly.GetManifestResourceStream(resourceName);
    }

    private IReadOnlyList<AppDefinition> CreateFallbackDefaultAppDefinitions()
    {
        return
        [
            new AppDefinition
            {
                Name = "RDB",
                Type = AppType.Rdb,
                Zone = AppExecutionZone.Parallel,
                IsEnabled = true,
                RunAsAdministrator = true,
                Port = 28015
            },
            new AppDefinition
            {
                Name = "SignalR10",
                Type = AppType.Msg10,
                Zone = AppExecutionZone.Parallel,
                IsEnabled = true,
                Port = 5000,
                MsgHubPath = "/Data",
                ShowWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RequireNetworkAvailable = true
            },
            new AppDefinition
            {
                Name = "FTP",
                Type = AppType.Ftp,
                Zone = AppExecutionZone.Parallel,
                IsEnabled = true,
                RunAsAdministrator = true,
                Port = DefaultFtpPort,
                PassivePortRange = "24000-24240",
                FtpHomeDirectory = GetDefaultFtpHomeDirectory(),
                FtpAllowRead = true,
                FtpAllowWrite = true
            }
        ];
    }

    private AppDefinition FinalizeSeedDefinition(AppDefinition definition, int index)
    {
        if (definition.Id == Guid.Empty)
        {
            definition.Id = Guid.NewGuid();
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            definition.Name = definition.Type.ToString();
        }

        if (definition.Type == AppType.Ftp && string.IsNullOrWhiteSpace(definition.FtpHomeDirectory))
        {
            definition.FtpHomeDirectory = GetDefaultFtpHomeDirectory();
        }

        definition.ExecutablePath = ResolveSeedPath(definition.ExecutablePath) ?? string.Empty;
        definition.WorkingDirectory = ResolveSeedPath(definition.WorkingDirectory);
        definition.FtpHomeDirectory = ResolveSeedPath(definition.FtpHomeDirectory) ?? string.Empty;

        definition.DisplayOrder = index;
        return definition;
    }

    private string? ResolveSeedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path.Contains(MyDocumentsToken, StringComparison.OrdinalIgnoreCase))
        {
            path = ResolveDocumentsSeedPath(path);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private static string ResolveDocumentsSeedPath(string path)
    {
        var relativePath = path.Replace(MyDocumentsToken, string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var documentsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Documents");

        return string.IsNullOrWhiteSpace(relativePath)
            ? documentsRoot
            : Path.Combine(documentsRoot, relativePath);
    }

    private Task ExtractEmbeddedZipAsync(string resourceFileName, string destinationFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CleanDirectory(destinationFolder);

        using var resourceStream = OpenEmbeddedResourceStream(resourceFileName);
        using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read, leaveOpen: false);
        archive.ExtractToDirectory(destinationFolder, overwriteFiles: true);

        return Task.CompletedTask;
    }

    private static Stream OpenEmbeddedResourceStream(string resourceFileName)
    {
        var assembly = typeof(AppDependencyService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"{resourceFileName} 리소스를 찾을 수 없습니다.");
        }

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"{resourceFileName} 리소스를 열 수 없습니다.");
        }

        return stream;
    }

    private static void CleanDirectory(string destinationFolder)
    {
        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(destinationFolder))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.EnumerateDirectories(destinationFolder))
        {
            Directory.Delete(dir, true);
        }
    }
}

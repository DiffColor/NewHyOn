using AndoW.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;

namespace NewHyOn.Player.Settings.Services;

public sealed class TransferServerSettingsClient : IDisposable
{
    private const string DatabaseName = "NewHyOn";
    private const string ServerSettingsTableName = "ServerSettings";
    private const string PlayerInfoTableName = "PlayerInfoManager";
    private const string WeeklyScheduleTableName = "WeeklyInfoManagerClass";
    private const string DefaultUser = "admin";
    private const string DefaultPassword = "turtle04!9";
    private static readonly RethinkDB R = RethinkDB.R;

    private readonly DataServerAddressEndpoint? endpoint;
    private Connection? connection;

    public TransferServerSettingsClient(string rethinkAddress)
    {
        if (DataServerAddressParser.TryParse(rethinkAddress, out DataServerAddressEndpoint parsedEndpoint))
        {
            endpoint = parsedEndpoint;
        }
    }

    public TransferServerSettingsQueryResult QuerySettings()
    {
        if (endpoint == null)
        {
            return TransferServerSettingsQueryResult.InvalidAddress();
        }

        try
        {
            Connection conn = GetConnection();
            List<string> databases = R.DbList().RunAtom<List<string>>(conn) ?? [];
            if (!databases.Contains(DatabaseName))
            {
                return TransferServerSettingsQueryResult.DatabaseMissing();
            }

            List<string> tables = R.Db(DatabaseName).TableList().RunAtom<List<string>>(conn) ?? [];
            if (!tables.Contains(ServerSettingsTableName))
            {
                return TransferServerSettingsQueryResult.TableMissing();
            }

            var table = R.Db(DatabaseName).Table(ServerSettingsTableName);
            TransferServerSettingsRecord? settings = table.Get(0).RunAtom<TransferServerSettingsRecord>(conn);
            if (settings != null)
            {
                return TransferServerSettingsQueryResult.Success(settings);
            }

            settings = table.RunCursor<TransferServerSettingsRecord>(conn).FirstOrDefault();
            return settings == null
                ? TransferServerSettingsQueryResult.NotFound()
                : TransferServerSettingsQueryResult.Success(settings);
        }
        catch (Exception ex)
        {
            return TransferServerSettingsQueryResult.ConnectionFailed(BuildErrorMessage(ex));
        }
    }

    public RemotePlayerQueryResult QueryPlayerByName(string playerName)
    {
        if (endpoint == null)
        {
            return RemotePlayerQueryResult.InvalidAddress();
        }

        string normalizedName = Normalize(playerName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return RemotePlayerQueryResult.NotFound();
        }

        try
        {
            Connection conn = GetConnection();
            if (!HasDatabase(conn))
            {
                return RemotePlayerQueryResult.DatabaseMissing();
            }

            if (!HasTable(conn, PlayerInfoTableName))
            {
                return RemotePlayerQueryResult.TableMissing();
            }

            PlayerInfoClass? player = R.Db(DatabaseName)
                .Table(PlayerInfoTableName)
                .Filter(row => row["PIF_PlayerName"].Downcase().Eq(normalizedName.ToLowerInvariant()))
                .Limit(1)
                .RunCursor<PlayerInfoClass>(conn)
                .FirstOrDefault();

            return player == null
                ? RemotePlayerQueryResult.NotFound()
                : RemotePlayerQueryResult.Success(player);
        }
        catch (Exception ex)
        {
            return RemotePlayerQueryResult.ConnectionFailed(BuildErrorMessage(ex));
        }
    }

    public RemoteWeeklyScheduleQueryResult QueryWeeklySchedule(string playerId, string playerName)
    {
        if (endpoint == null)
        {
            return RemoteWeeklyScheduleQueryResult.InvalidAddress();
        }

        string normalizedPlayerId = Normalize(playerId);
        string normalizedPlayerName = Normalize(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerId) && string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return RemoteWeeklyScheduleQueryResult.NotFound();
        }

        try
        {
            Connection conn = GetConnection();
            if (!HasDatabase(conn))
            {
                return RemoteWeeklyScheduleQueryResult.DatabaseMissing();
            }

            if (!HasTable(conn, WeeklyScheduleTableName))
            {
                return RemoteWeeklyScheduleQueryResult.TableMissing();
            }

            AndoW.Shared.WeeklyPlayScheduleInfo? schedule = null;
            if (!string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                schedule = R.Db(DatabaseName)
                    .Table(WeeklyScheduleTableName)
                    .Get(normalizedPlayerId)
                    .RunAtom<AndoW.Shared.WeeklyPlayScheduleInfo>(conn);
            }

            if (schedule == null && !string.IsNullOrWhiteSpace(normalizedPlayerName))
            {
                schedule = R.Db(DatabaseName)
                    .Table(WeeklyScheduleTableName)
                    .Filter(row => row["PlayerName"].Downcase().Eq(normalizedPlayerName.ToLowerInvariant()))
                    .Limit(1)
                    .RunCursor<AndoW.Shared.WeeklyPlayScheduleInfo>(conn)
                    .FirstOrDefault();
            }

            return schedule == null
                ? RemoteWeeklyScheduleQueryResult.NotFound()
                : RemoteWeeklyScheduleQueryResult.Success(schedule);
        }
        catch (Exception ex)
        {
            return RemoteWeeklyScheduleQueryResult.ConnectionFailed(BuildErrorMessage(ex));
        }
    }

    private Connection GetConnection()
    {
        if (connection is { Open: true })
        {
            return connection;
        }

        connection = R.Connection()
            .Hostname(endpoint!.Host)
            .Port(endpoint.Port)
            .User(DefaultUser, DefaultPassword)
            .Timeout(3000)
            .Connect();

        return connection;
    }

    private static bool HasDatabase(Connection conn)
    {
        List<string> databases = R.DbList().RunAtom<List<string>>(conn) ?? [];
        return databases.Contains(DatabaseName);
    }

    private static bool HasTable(Connection conn, string tableName)
    {
        List<string> tables = R.Db(DatabaseName).TableList().RunAtom<List<string>>(conn) ?? [];
        return tables.Contains(tableName);
    }

    public void Dispose()
    {
        if (connection == null)
        {
            return;
        }

        try
        {
            connection.Close(false);
            connection.Dispose();
        }
        catch
        {
        }
        finally
        {
            connection = null;
        }
    }

    private static string BuildErrorMessage(Exception ex)
    {
        Exception root = ex;
        while (root.InnerException != null)
        {
            root = root.InnerException;
        }

        string message = string.IsNullOrWhiteSpace(root.Message)
            ? ex.GetType().Name
            : root.Message.Trim();

        if (root is FileNotFoundException fileNotFound)
        {
            string missingFile = string.IsNullOrWhiteSpace(fileNotFound.FileName)
                ? ExtractQuotedAssemblyName(message)
                : Path.GetFileName(fileNotFound.FileName);

            if (!string.IsNullOrWhiteSpace(missingFile))
            {
                return $"필수 DLL을 찾을 수 없습니다: {missingFile}";
            }
        }

        if (root is DllNotFoundException dllNotFound)
        {
            string missingDll = ExtractQuotedAssemblyName(dllNotFound.Message);
            if (!string.IsNullOrWhiteSpace(missingDll))
            {
                return $"필수 DLL을 찾을 수 없습니다: {missingDll}";
            }

            return "필수 DLL을 찾을 수 없습니다.";
        }

        if (root is BadImageFormatException badImageFormat)
        {
            string targetFile = string.IsNullOrWhiteSpace(badImageFormat.FileName)
                ? ExtractQuotedAssemblyName(badImageFormat.Message)
                : Path.GetFileName(badImageFormat.FileName);

            if (!string.IsNullOrWhiteSpace(targetFile))
            {
                return $"DLL 로드에 실패했습니다: {targetFile}";
            }

            return "DLL 로드에 실패했습니다.";
        }

        return message;
    }

    private static string ExtractQuotedAssemblyName(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        int start = message.IndexOf('\'', StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        int end = message.IndexOf('\'', start + 1);
        if (end <= start)
        {
            return string.Empty;
        }

        string quoted = message[(start + 1)..end].Trim();
        if (string.IsNullOrWhiteSpace(quoted))
        {
            return string.Empty;
        }

        int commaIndex = quoted.IndexOf(',');
        return commaIndex > 0 ? quoted[..commaIndex].Trim() : quoted;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class TransferServerSettingsQueryResult
{
    private TransferServerSettingsQueryResult()
    {
    }

    public bool IsSuccess { get; init; }
    public bool IsNotFound { get; init; }
    public bool IsInvalidAddress { get; init; }
    public bool IsConnectionFailed { get; init; }
    public bool IsDatabaseMissing { get; init; }
    public bool IsTableMissing { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public TransferServerSettingsRecord? Settings { get; init; }

    public static TransferServerSettingsQueryResult Success(TransferServerSettingsRecord settings)
    {
        return new TransferServerSettingsQueryResult
        {
            IsSuccess = true,
            Settings = settings
        };
    }

    public static TransferServerSettingsQueryResult NotFound()
    {
        return new TransferServerSettingsQueryResult
        {
            IsNotFound = true
        };
    }

    public static TransferServerSettingsQueryResult InvalidAddress()
    {
        return new TransferServerSettingsQueryResult
        {
            IsInvalidAddress = true
        };
    }

    public static TransferServerSettingsQueryResult ConnectionFailed(string? errorMessage = null)
    {
        return new TransferServerSettingsQueryResult
        {
            IsConnectionFailed = true,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? string.Empty : errorMessage.Trim()
        };
    }

    public static TransferServerSettingsQueryResult DatabaseMissing()
    {
        return new TransferServerSettingsQueryResult
        {
            IsDatabaseMissing = true
        };
    }

    public static TransferServerSettingsQueryResult TableMissing()
    {
        return new TransferServerSettingsQueryResult
        {
            IsTableMissing = true
        };
    }
}

public sealed class TransferServerSettingsRecord
{
    [JsonProperty("id")]
    public int Id { get; set; }

    public int FTP_Port { get; set; }
    public int FTP_PasvMinPort { get; set; }
    public int FTP_PasvMaxPort { get; set; }
    public string FTP_RootPath { get; set; } = string.Empty;
    public string DataServerIp { get; set; } = string.Empty;
    public string MessageServerIp { get; set; } = string.Empty;
}

public sealed class RemotePlayerQueryResult
{
    private RemotePlayerQueryResult()
    {
    }

    public bool IsSuccess { get; init; }
    public bool IsNotFound { get; init; }
    public bool IsInvalidAddress { get; init; }
    public bool IsConnectionFailed { get; init; }
    public bool IsDatabaseMissing { get; init; }
    public bool IsTableMissing { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public PlayerInfoClass? Player { get; init; }

    public static RemotePlayerQueryResult Success(PlayerInfoClass player)
    {
        return new RemotePlayerQueryResult
        {
            IsSuccess = true,
            Player = player
        };
    }

    public static RemotePlayerQueryResult NotFound()
    {
        return new RemotePlayerQueryResult
        {
            IsNotFound = true
        };
    }

    public static RemotePlayerQueryResult InvalidAddress()
    {
        return new RemotePlayerQueryResult
        {
            IsInvalidAddress = true
        };
    }

    public static RemotePlayerQueryResult ConnectionFailed(string? errorMessage = null)
    {
        return new RemotePlayerQueryResult
        {
            IsConnectionFailed = true,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? string.Empty : errorMessage.Trim()
        };
    }

    public static RemotePlayerQueryResult DatabaseMissing()
    {
        return new RemotePlayerQueryResult
        {
            IsDatabaseMissing = true
        };
    }

    public static RemotePlayerQueryResult TableMissing()
    {
        return new RemotePlayerQueryResult
        {
            IsTableMissing = true
        };
    }
}

public sealed class RemoteWeeklyScheduleQueryResult
{
    private RemoteWeeklyScheduleQueryResult()
    {
    }

    public bool IsSuccess { get; init; }
    public bool IsNotFound { get; init; }
    public bool IsInvalidAddress { get; init; }
    public bool IsConnectionFailed { get; init; }
    public bool IsDatabaseMissing { get; init; }
    public bool IsTableMissing { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public AndoW.Shared.WeeklyPlayScheduleInfo? Schedule { get; init; }

    public static RemoteWeeklyScheduleQueryResult Success(AndoW.Shared.WeeklyPlayScheduleInfo schedule)
    {
        return new RemoteWeeklyScheduleQueryResult
        {
            IsSuccess = true,
            Schedule = schedule
        };
    }

    public static RemoteWeeklyScheduleQueryResult NotFound()
    {
        return new RemoteWeeklyScheduleQueryResult
        {
            IsNotFound = true
        };
    }

    public static RemoteWeeklyScheduleQueryResult InvalidAddress()
    {
        return new RemoteWeeklyScheduleQueryResult
        {
            IsInvalidAddress = true
        };
    }

    public static RemoteWeeklyScheduleQueryResult ConnectionFailed(string? errorMessage = null)
    {
        return new RemoteWeeklyScheduleQueryResult
        {
            IsConnectionFailed = true,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? string.Empty : errorMessage.Trim()
        };
    }

    public static RemoteWeeklyScheduleQueryResult DatabaseMissing()
    {
        return new RemoteWeeklyScheduleQueryResult
        {
            IsDatabaseMissing = true
        };
    }

    public static RemoteWeeklyScheduleQueryResult TableMissing()
    {
        return new RemoteWeeklyScheduleQueryResult
        {
            IsTableMissing = true
        };
    }
}

using AndoW.Shared;
using LicenseHub.DeviceAuth.Core;
using LicenseHub.DeviceAuth.Wpf;
using NewHyOn.Player.Settings.DataManager;
using NewHyOn.Player.Settings.Models;
using NewHyOn.Shared.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NewHyOn.Player.Settings.Services;

public sealed class PlayerConfigurationService
{
    private readonly LocalSettingsManager localSettingsManager = new();
    private readonly PlayerInfoManager playerInfoManager = new();
    private readonly TTPlayerInfoManager ttPlayerInfoManager = new();
    private readonly PortInfoManager portInfoManager = new();
    private readonly WeeklyInfoManagerClass weeklyInfoManager = new();

    private readonly string sourceKey;

    public PlayerConfigurationService()
    {
        sourceKey = LicenseHubDeviceFingerprint.Generate().Fingerprint;
        SystemPolicyService.DisableUac();
    }

    public ConfigPlayerSnapshot Load()
    {
        portInfoManager.LoadData();
        playerInfoManager.LoadData();
        localSettingsManager.LoadData();
        ttPlayerInfoManager.Load();
        weeklyInfoManager.LoadWeeklySchedule(playerInfoManager.PlayerInfo.PIF_GUID, playerInfoManager.PlayerInfo.PIF_PlayerName);

        (string authStatusText, bool isLicensed, bool authInputEnabled) = EvaluateAuthState();
        PortInfoClass portInfo = GetOrCreatePortInfo();

        ConfigPlayerSnapshot snapshot = new()
        {
            ManagerIp = localSettingsManager.Settings.ManagerIP,
            PlayerIp = string.IsNullOrWhiteSpace(playerInfoManager.PlayerInfo.PIF_IPAddress)
                ? LegacyNetworkService.GetAutoIp().ToString()
                : playerInfoManager.PlayerInfo.PIF_IPAddress,
            PlayerName = playerInfoManager.PlayerInfo.PIF_PlayerName,
            SourceKey = sourceKey,
            AuthStatusText = authStatusText,
            IsLicensed = isLicensed,
            IsAuthInputEnabled = authInputEnabled,
            SignalRPort = LegacyNetworkService.SIGNALR_PORT.ToString(),
            FtpPort = FormatPort(portInfo.AIF_FTP),
            SyncPort = FormatPort(portInfo.AIF_SYNC),
            PreserveAspectRatio = ttPlayerInfoManager.PlayerInfo.TTInfo_Data1.Equals("YES", StringComparison.OrdinalIgnoreCase),
            EnableHardwareAcceleration = ttPlayerInfoManager.PlayerInfo.TTInfo_DAta2.Equals("YES", StringComparison.OrdinalIgnoreCase),
            EnableSubMonitorOutput = ttPlayerInfoManager.PlayerInfo.TTInfo_DAta4.Equals("YES", StringComparison.OrdinalIgnoreCase),
            IsTestMode = localSettingsManager.Settings.IsTestMode,
            HideCursor = localSettingsManager.Settings.HideCursor,
            BlockMonitorOnEndTime = localSettingsManager.Settings.BlockMonitorOnEndTime,
            EndTimeAction = localSettingsManager.Settings.EndTimeAction,
            SwitchTiming = localSettingsManager.Settings.SwitchTiming ?? "Immediately",
            IsSyncEnabled = localSettingsManager.Settings.IsSyncEnabled,
            IsLeading = localSettingsManager.Settings.IsLeading,
            SyncClientIps = new List<string>(localSettingsManager.Settings.SyncClientIps ?? new List<string>()),
            LedLeft = ttPlayerInfoManager.PlayerInfo.TTInfo_DAta6,
            LedWidth = ttPlayerInfoManager.PlayerInfo.TTInfo_DAta8,
            LedTop = ttPlayerInfoManager.PlayerInfo.TTInfo_Data7,
            LedHeight = ttPlayerInfoManager.PlayerInfo.TTInfo_Data9,
            LedTransferPort = FormatPort(portInfo.AIF_FTP),
            WeeklySchedules = weeklyInfoManager.ScheduleList.Select(ToRowModel).ToList()
        };

        ApplyTransferServerSettings(snapshot);
        EnsureWeeklySchedules(snapshot);
        snapshot.TransferServerStatusText = string.IsNullOrWhiteSpace(snapshot.ManagerIp)
            ? "데이터 서버 주소가 없어 저장된 로컬 전송 서버 설정만 표시합니다."
            : "저장된 로컬 전송 서버 설정을 표시하고 있습니다.";

        return snapshot;
    }

    public async Task<ConfigPlayerSnapshot> SyncTransferServerSettingsAsync(string dataServerAddress, CancellationToken cancellationToken = default)
    {
        ConfigPlayerSnapshot snapshot = CreateTransferServerSnapshot();
        string rethinkAddress = Normalize(dataServerAddress);
        if (string.IsNullOrWhiteSpace(rethinkAddress))
        {
            snapshot.TransferServerStatusText = "데이터 서버 주소가 없어 저장된 로컬 전송 서버 설정만 표시합니다.";
            snapshot.IsTransferServerStatusError = false;
            return snapshot;
        }

        if (!DataServerAddressParser.TryParse(rethinkAddress, out _))
        {
            snapshot.TransferServerStatusText = "데이터 서버 주소 형식이 올바르지 않아 로컬값만 표시합니다.";
            snapshot.IsTransferServerStatusError = true;
            return snapshot;
        }

        try
        {
            return await Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return MarkTransferServerSyncCanceled(CreateTransferServerSnapshot());
                }

                localSettingsManager.LoadData();
                portInfoManager.LoadData();

                ConfigPlayerSnapshot localSnapshot = CreateTransferServerSnapshot();
                if (cancellationToken.IsCancellationRequested)
                {
                    return MarkTransferServerSyncCanceled(localSnapshot);
                }

                using TransferServerSettingsClient client = new(rethinkAddress);
                TransferServerSettingsQueryResult queryResult = client.QuerySettings();
                if (cancellationToken.IsCancellationRequested)
                {
                    return MarkTransferServerSyncCanceled(localSnapshot);
                }

                if (queryResult.IsInvalidAddress)
                {
                    localSnapshot.TransferServerStatusText = "데이터 서버 주소 형식이 올바르지 않아 로컬값만 표시합니다.";
                    localSnapshot.IsTransferServerStatusError = true;
                    return localSnapshot;
                }

                if (queryResult.IsConnectionFailed)
                {
                    localSnapshot.TransferServerStatusText = string.IsNullOrWhiteSpace(queryResult.ErrorMessage)
                        ? "데이터 서버 접속 또는 조회에 실패해 로컬값만 표시합니다."
                        : $"데이터 서버 접속 또는 조회에 실패했습니다. ({queryResult.ErrorMessage})";
                    localSnapshot.IsTransferServerStatusError = true;
                    return localSnapshot;
                }

                if (queryResult.IsDatabaseMissing)
                {
                    localSnapshot.TransferServerStatusText = "데이터 서버에 NewHyOn 데이터베이스가 없어 로컬값만 표시합니다.";
                    localSnapshot.IsTransferServerStatusError = true;
                    return localSnapshot;
                }

                if (queryResult.IsTableMissing)
                {
                    localSnapshot.TransferServerStatusText = "데이터 서버에 ServerSettings 테이블이 없어 로컬값만 표시합니다.";
                    localSnapshot.IsTransferServerStatusError = true;
                    return localSnapshot;
                }

                if (queryResult.IsNotFound || queryResult.Settings == null)
                {
                    localSnapshot.TransferServerStatusText = "원격 전송 서버 설정을 찾지 못해 로컬값만 표시합니다.";
                    localSnapshot.IsTransferServerStatusError = true;
                    return localSnapshot;
                }

                TransferServerSettingsRecord remoteSettings = queryResult.Settings;
                ApplyRemoteTransferServerSettings(remoteSettings);

                ConfigPlayerSnapshot syncedSnapshot = CreateTransferServerSnapshot(remoteSettings);
                syncedSnapshot.TransferServerStatusText = "원격 전송 서버 설정을 확인해 로컬값과 동기화했습니다.";
                syncedSnapshot.IsTransferServerSyncSuccessful = true;
                syncedSnapshot.IsTransferServerStatusError = false;
                return syncedSnapshot;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            snapshot.TransferServerStatusText = "데이터 서버 접속 또는 조회에 실패해 로컬값만 표시합니다.";
            snapshot.IsTransferServerStatusError = true;
            return snapshot;
        }
    }

    public async Task<ConfigPlayerSnapshot> SyncStoredPlayerConfigurationFromServerAsync(
        string dataServerAddress,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        ConfigPlayerSnapshot transferSnapshot = await SyncTransferServerSettingsAsync(dataServerAddress, cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            return MergeTransferServerState(Load(), transferSnapshot);
        }

        string rethinkAddress = Normalize(dataServerAddress);
        string normalizedPlayerName = Normalize(playerName);
        if (string.IsNullOrWhiteSpace(rethinkAddress) ||
            !DataServerAddressParser.TryParse(rethinkAddress, out _) ||
            string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return MergeTransferServerState(Load(), transferSnapshot);
        }

        try
        {
            await Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                localSettingsManager.LoadData();
                playerInfoManager.LoadData();
                weeklyInfoManager.LoadWeeklySchedule(playerInfoManager.PlayerInfo.PIF_GUID, playerInfoManager.PlayerInfo.PIF_PlayerName);

                using TransferServerSettingsClient client = new(rethinkAddress);
                RemotePlayerQueryResult playerResult = client.QueryPlayerByName(normalizedPlayerName);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (playerResult.IsSuccess && playerResult.Player != null)
                {
                    ApplyRemotePlayerInfo(playerResult.Player);
                }

                playerInfoManager.LoadData();
                string resolvedPlayerId = Normalize(playerInfoManager.PlayerInfo.PIF_GUID);
                string resolvedPlayerName = string.IsNullOrWhiteSpace(playerInfoManager.PlayerInfo.PIF_PlayerName)
                    ? normalizedPlayerName
                    : Normalize(playerInfoManager.PlayerInfo.PIF_PlayerName);

                RemoteWeeklyScheduleQueryResult weeklyResult = client.QueryWeeklySchedule(resolvedPlayerId, resolvedPlayerName);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (weeklyResult.IsSuccess && weeklyResult.Schedule != null)
                {
                    weeklyInfoManager.ApplyRemoteWeeklySchedule(weeklyResult.Schedule, resolvedPlayerId, resolvedPlayerName);
                }
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }

        ConfigPlayerSnapshot mergedSnapshot = MergeTransferServerState(Load(), transferSnapshot);
        EnsureWeeklySchedules(mergedSnapshot);
        return mergedSnapshot;
    }

    public async Task<AuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        AuthCompletionResult authResult = await LicenseHub.DeviceAuth.Wpf.DeviceAuthClient.AuthenticateAsync(
            new AuthenticateOptions
            {
                ApiBaseUrl = LicenseHubAuthPolicy.ApiBaseUrl,
                ProductId = LicenseHubAuthPolicy.ProductId,
                WindowTitle = LicenseHubAuthPolicy.AuthWindowTitle,
                WindowWidth = LicenseHubAuthPolicy.AuthWindowWidth,
                WindowHeight = LicenseHubAuthPolicy.AuthWindowHeight,
                LocalLicensePath = LicenseHubAuthPolicy.LicenseFilePath
            },
            cancellationToken).ConfigureAwait(true);

        (string statusText, bool isLicensed, bool authInputEnabled) = EvaluateAuthState();
        if (isLicensed)
        {
            return new AuthResult
            {
                Success = true,
                StatusText = statusText,
                IsLicensed = true,
                DisablePasswordInput = !authInputEnabled,
                Message = "인증이 완료되었습니다."
            };
        }

        bool isPendingOfflineProof = authResult.Status == AuthCompletionStatus.OfflinePendingProof;
        bool isOfflineVerified = authResult.Status == AuthCompletionStatus.OfflineVerified;
        if (isOfflineVerified && PersistOfflineProofAuthToPlayerInfo(authResult))
        {
            return new AuthResult
            {
                Success = true,
                StatusText = "인증 상태 : 정품 인증 완료",
                IsLicensed = true,
                DisablePasswordInput = true,
                Message = "오프라인 인증이 완료되었습니다."
            };
        }

        string message;
        if (isPendingOfflineProof)
        {
            message = "오프라인 2단계 QR이 생성되었습니다. 완료를 눌러 인증을 마무리해 주세요.";
        }
        else if (isOfflineVerified)
        {
            message = "오프라인 인증이 완료되었지만 인증 정보를 저장하지 못했습니다.";
        }
        else if (authResult.Status == AuthCompletionStatus.Cancelled)
        {
            message = "인증 창이 닫혔습니다.";
        }
        else if (!string.IsNullOrWhiteSpace(authResult.Message))
        {
            message = authResult.Message;
        }
        else
        {
            ValidationResult validation = LicenseHubLocalValidator.Validate();
            message = string.IsNullOrWhiteSpace(validation.Reason)
                ? "인증 결과를 확인하지 못했습니다."
                : validation.Reason;
        }

        return new AuthResult
        {
            Success = false,
            StatusText = statusText,
            IsLicensed = false,
            DisablePasswordInput = !authInputEnabled,
            WasCancelled = authResult.Status == AuthCompletionStatus.Cancelled,
            IsPendingOfflineProof = isPendingOfflineProof,
            Message = message
        };
    }

    public void SavePorts(string ftpPortText, string syncPortText)
    {
        if (!TryParsePort(ftpPortText, out int ftpPort) ||
            !TryParsePort(syncPortText, out int syncPort))
        {
            throw new InvalidOperationException("포트번호를 입력해 주세요.");
        }

        PortInfoClass info = GetOrCreatePortInfo();
        info.AIF_FTP = ftpPort;
        info.AIF_SYNC = syncPort;
        portInfoManager.SaveData();

        localSettingsManager.Settings.CachedFtpPort = ftpPort;
        localSettingsManager.SaveData();
    }

    public void PersistSyncClientIps(IEnumerable<string> clientIps)
    {
        localSettingsManager.Settings.SyncClientIps = clientIps
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        localSettingsManager.SaveData();
    }

    public void SaveAll(ConfigPlayerSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.PlayerName))
        {
            throw new InvalidOperationException("플레이어 이름을 입력해주세요.");
        }

        bool isLocalPlay = localSettingsManager.Settings.IsLocalPlay;
        if (!isLocalPlay)
        {
            if (!IsValidHostOrIp(snapshot.ManagerIp))
            {
                throw new InvalidOperationException("데이터 서버 주소가 올바르지 않습니다.");
            }

            if (!IPAddress.TryParse(snapshot.PlayerIp, out _))
            {
                throw new InvalidOperationException("플레이어 주소가 올바르지 않습니다.");
            }
        }

        SavePorts(snapshot.FtpPort, snapshot.SyncPort);
        KillProcesses();
        SaveAppInfo(snapshot);
        SaveWeeklySchedule(snapshot);
        SavePlayerInfo(snapshot);
        SaveTtPlayerInfo(snapshot);
    }

    public async Task<string?> TryApplyFirewallRulesAsync(string syncPortText, CancellationToken cancellationToken = default)
    {
        if (!TryParsePort(syncPortText, out int syncPort))
        {
            throw new InvalidOperationException("포트번호를 입력해 주세요.");
        }

        return await FirewallRuleService.TryApplyPlayerRulesAsync(syncPort, cancellationToken);
    }

    public static bool IsValidHostOrIp(string value)
    {
        return DataServerAddressParser.TryParse(value, out _);
    }

    private void SaveAppInfo(ConfigPlayerSnapshot snapshot)
    {
        localSettingsManager.Settings.ManagerIP = DataServerAddressParser.NormalizeForStorage(snapshot.ManagerIp);
        localSettingsManager.Settings.EndTimeAction = snapshot.EndTimeAction;
        localSettingsManager.Settings.HideCursor = snapshot.HideCursor;
        localSettingsManager.Settings.IsTestMode = snapshot.IsTestMode;
        localSettingsManager.Settings.BlockMonitorOnEndTime = snapshot.BlockMonitorOnEndTime;
        localSettingsManager.Settings.SwitchTiming = string.IsNullOrWhiteSpace(snapshot.SwitchTiming)
            ? "Immediately"
            : snapshot.SwitchTiming;
        localSettingsManager.Settings.IsSyncEnabled = snapshot.IsSyncEnabled;
        localSettingsManager.Settings.IsLeading = snapshot.IsLeading;
        localSettingsManager.Settings.SyncClientIps = snapshot.SyncClientIps
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        localSettingsManager.Settings.CachedDataServerAddress = DataServerAddressParser.NormalizeForStorage(snapshot.ManagerIp);
        localSettingsManager.Settings.CachedMessageServerAddress = Normalize(snapshot.LocalMessageServerAddress);
        localSettingsManager.Settings.CachedFtpRootPath = Normalize(snapshot.LocalFtpRootPath);
        if (TryParsePort(snapshot.LocalFtpPort, out int ftpPort))
        {
            localSettingsManager.Settings.CachedFtpPort = ftpPort;
        }
        localSettingsManager.SaveData();
    }

    private void SaveWeeklySchedule(ConfigPlayerSnapshot snapshot)
    {
        weeklyInfoManager.ScheduleList.Clear();
        foreach (ScheduleRowModel row in snapshot.WeeklySchedules)
        {
            weeklyInfoManager.ScheduleList.Add(new WeeklyPlayScheduleInfo
            {
                DayCode = row.DayCode,
                DayLabel = row.DayLabel,
                WPS_DayOfWeek = row.DayCode,
                WPS_Hour1 = row.StartHour,
                WPS_Min1 = row.StartMinute,
                WPS_Hour2 = row.EndHour,
                WPS_Min2 = row.EndMinute,
                WPS_IsOnAir = row.IsOnAir
            });
        }

        weeklyInfoManager.SaveWeeklySchedule(playerInfoManager.PlayerInfo.PIF_GUID, snapshot.PlayerName);
    }

    private void SavePlayerInfo(ConfigPlayerSnapshot snapshot)
    {
        playerInfoManager.PlayerInfo.PIF_PlayerName = snapshot.PlayerName.Trim();
        playerInfoManager.PlayerInfo.PIF_IPAddress = snapshot.PlayerIp.Trim();
        playerInfoManager.PlayerInfo.PIF_MacAddress = sourceKey;
        playerInfoManager.SaveData();
    }

    private void SaveTtPlayerInfo(ConfigPlayerSnapshot snapshot)
    {
        ttPlayerInfoManager.PlayerInfo.TTInfo_Data1 = snapshot.PreserveAspectRatio ? "YES" : "NO";
        ttPlayerInfoManager.PlayerInfo.TTInfo_DAta2 = snapshot.EnableHardwareAcceleration ? "YES" : "NO";
        ttPlayerInfoManager.PlayerInfo.TTInfo_DAta4 = snapshot.EnableSubMonitorOutput ? "YES" : "NO";
        ttPlayerInfoManager.PlayerInfo.TTInfo_DAta6 = string.IsNullOrWhiteSpace(snapshot.LedLeft) ? "0" : snapshot.LedLeft.Trim();
        ttPlayerInfoManager.PlayerInfo.TTInfo_Data7 = string.IsNullOrWhiteSpace(snapshot.LedTop) ? "0" : snapshot.LedTop.Trim();
        ttPlayerInfoManager.PlayerInfo.TTInfo_DAta8 = string.IsNullOrWhiteSpace(snapshot.LedWidth) ? "160" : snapshot.LedWidth.Trim();
        ttPlayerInfoManager.PlayerInfo.TTInfo_Data9 = string.IsNullOrWhiteSpace(snapshot.LedHeight) ? "90" : snapshot.LedHeight.Trim();
        ttPlayerInfoManager.SaveData();
    }

    private void ApplyTransferServerSettings(ConfigPlayerSnapshot snapshot, TransferServerSettingsRecord? remoteSettings = null)
    {
        localSettingsManager.LoadData();
        portInfoManager.LoadData();

        PortInfoClass portInfo = GetOrCreatePortInfo();
        string localFtpPort = ResolveLocalFtpPort(portInfo);

        snapshot.LocalDataServerAddress = Normalize(localSettingsManager.Settings.ManagerIP);
        snapshot.LocalMessageServerAddress = Normalize(localSettingsManager.Settings.CachedMessageServerAddress);
        snapshot.LocalFtpRootPath = Normalize(localSettingsManager.Settings.CachedFtpRootPath);
        snapshot.LocalFtpPort = localFtpPort;
        snapshot.FtpPort = localFtpPort;

        snapshot.RemoteDataServerAddress = Normalize(remoteSettings?.DataServerIp);
        snapshot.RemoteMessageServerAddress = Normalize(remoteSettings?.MessageServerIp);
        snapshot.RemoteFtpRootPath = Normalize(remoteSettings?.FTP_RootPath);
        snapshot.RemoteFtpPort = remoteSettings == null ? string.Empty : FormatPort(remoteSettings.FTP_Port);
    }

    private ConfigPlayerSnapshot CreateTransferServerSnapshot(TransferServerSettingsRecord? remoteSettings = null)
    {
        ConfigPlayerSnapshot snapshot = new();
        ApplyTransferServerSettings(snapshot, remoteSettings);
        ApplyAuthStateSnapshot(snapshot);
        return snapshot;
    }

    private void ApplyAuthStateSnapshot(ConfigPlayerSnapshot snapshot)
    {
        (string authStatusText, bool isLicensed, bool authInputEnabled) = EvaluateAuthState();
        snapshot.AuthStatusText = authStatusText;
        snapshot.IsLicensed = isLicensed;
        snapshot.IsAuthInputEnabled = authInputEnabled;
    }

    private void ApplyRemoteTransferServerSettings(TransferServerSettingsRecord remoteSettings)
    {
        string messageServerAddress = Normalize(remoteSettings.MessageServerIp);
        string ftpRootPath = Normalize(remoteSettings.FTP_RootPath);

        localSettingsManager.Settings.CachedMessageServerAddress = messageServerAddress;
        localSettingsManager.Settings.CachedFtpRootPath = ftpRootPath;
        if (remoteSettings.FTP_Port > 0 && remoteSettings.FTP_Port <= 65535)
        {
            localSettingsManager.Settings.CachedFtpPort = remoteSettings.FTP_Port;
        }
        localSettingsManager.SaveData();

        if (remoteSettings.FTP_Port > 0 && remoteSettings.FTP_Port <= 65535)
        {
            PortInfoClass portInfo = GetOrCreatePortInfo();
            portInfo.AIF_FTP = remoteSettings.FTP_Port;
            portInfoManager.SaveData();
        }
    }

    private void ApplyRemotePlayerInfo(PlayerInfoClass remotePlayer)
    {
        playerInfoManager.LoadData();
        bool changed = false;

        if (!string.IsNullOrWhiteSpace(remotePlayer.PIF_GUID) &&
            !string.Equals(playerInfoManager.PlayerInfo.PIF_GUID, remotePlayer.PIF_GUID, StringComparison.OrdinalIgnoreCase))
        {
            playerInfoManager.PlayerInfo.PIF_GUID = remotePlayer.PIF_GUID.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(playerInfoManager.PlayerInfo.PIF_DefaultPlayList) &&
            !string.IsNullOrWhiteSpace(remotePlayer.PIF_DefaultPlayList))
        {
            playerInfoManager.PlayerInfo.PIF_DefaultPlayList = remotePlayer.PIF_DefaultPlayList.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(playerInfoManager.PlayerInfo.PIF_CurrentPlayList) &&
            !string.IsNullOrWhiteSpace(remotePlayer.PIF_CurrentPlayList))
        {
            playerInfoManager.PlayerInfo.PIF_CurrentPlayList = remotePlayer.PIF_CurrentPlayList.Trim();
            changed = true;
        }

        if (changed)
        {
            playerInfoManager.SaveData();
        }
    }

    private static ConfigPlayerSnapshot MarkTransferServerSyncCanceled(ConfigPlayerSnapshot snapshot)
    {
        snapshot.TransferServerStatusText = "전송 서버 확인이 취소되어 저장된 로컬값만 유지합니다.";
        snapshot.IsTransferServerSyncSuccessful = false;
        snapshot.IsTransferServerStatusError = false;
        return snapshot;
    }

    private static ConfigPlayerSnapshot MergeTransferServerState(ConfigPlayerSnapshot snapshot, ConfigPlayerSnapshot transferSnapshot)
    {
        snapshot.LocalDataServerAddress = transferSnapshot.LocalDataServerAddress;
        snapshot.LocalMessageServerAddress = transferSnapshot.LocalMessageServerAddress;
        snapshot.LocalFtpRootPath = transferSnapshot.LocalFtpRootPath;
        snapshot.LocalFtpPort = transferSnapshot.LocalFtpPort;
        snapshot.RemoteDataServerAddress = transferSnapshot.RemoteDataServerAddress;
        snapshot.RemoteMessageServerAddress = transferSnapshot.RemoteMessageServerAddress;
        snapshot.RemoteFtpRootPath = transferSnapshot.RemoteFtpRootPath;
        snapshot.RemoteFtpPort = transferSnapshot.RemoteFtpPort;
        snapshot.TransferServerStatusText = transferSnapshot.TransferServerStatusText;
        snapshot.IsTransferServerSyncSuccessful = transferSnapshot.IsTransferServerSyncSuccessful;
        snapshot.IsTransferServerStatusError = transferSnapshot.IsTransferServerStatusError;
        snapshot.FtpPort = transferSnapshot.FtpPort;
        return snapshot;
    }

    private static void EnsureWeeklySchedules(ConfigPlayerSnapshot snapshot)
    {
        if (snapshot.WeeklySchedules == null)
        {
            snapshot.WeeklySchedules = new List<ScheduleRowModel>();
        }

        if (snapshot.WeeklySchedules.Count >= 7)
        {
            return;
        }

        Dictionary<string, ScheduleRowModel> existing = snapshot.WeeklySchedules
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.DayCode))
            .ToDictionary(x => x.DayCode, x => x, StringComparer.OrdinalIgnoreCase);

        snapshot.WeeklySchedules = GetDefaultScheduleRows()
            .Select(row => existing.TryGetValue(row.DayCode, out ScheduleRowModel? current)
                ? current
                : row)
            .ToList();
    }

    private static List<ScheduleRowModel> GetDefaultScheduleRows()
    {
        return new List<ScheduleRowModel>
        {
            new() { DayCode = "SUN", DayLabel = "일요일" },
            new() { DayCode = "MON", DayLabel = "월요일" },
            new() { DayCode = "TUE", DayLabel = "화요일" },
            new() { DayCode = "WED", DayLabel = "수요일" },
            new() { DayCode = "THU", DayLabel = "목요일" },
            new() { DayCode = "FRI", DayLabel = "금요일" },
            new() { DayCode = "SAT", DayLabel = "토요일" }
        };
    }

    private PortInfoClass GetOrCreatePortInfo()
    {
        if (portInfoManager.DataList.Count == 0)
        {
            portInfoManager.DataList.Add(new PortInfoClass());
        }

        return portInfoManager.DataList[0];
    }

    private string ResolveLocalFtpPort(PortInfoClass portInfo)
    {
        if (localSettingsManager.Settings.CachedFtpPort > 0 && localSettingsManager.Settings.CachedFtpPort <= 65535)
        {
            return localSettingsManager.Settings.CachedFtpPort.ToString();
        }

        if (portInfo.AIF_FTP > 0 && portInfo.AIF_FTP <= 65535)
        {
            return portInfo.AIF_FTP.ToString();
        }

        return LegacyNetworkService.FTP_PORT.ToString();
    }

    private void KillProcesses()
    {
        ProcessService.KillProcessByName(FndTools.GetAgentProcName());
        ProcessService.KillProcessByName(FndTools.GetEmergScrollProcName());
        ProcessService.KillProcessByName(FndTools.GetPptViewerProcName());
        ProcessService.KillProcessByName(FndTools.GetPlayerProcName());
        ProcessService.KillProcessByName(FndTools.GetPcsProcName());
    }

    private (string statusText, bool isLicensed, bool authInputEnabled) EvaluateAuthState()
    {
        LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.Read();
        ValidationResult validation = LicenseHubLocalValidator.ValidateForCurrentDevice(license);
        if (validation.IsValid)
        {
            PersistLicenseHubAuthToPlayerInfo(license, validation);
            return ("인증 상태 : 정품 인증 완료", true, false);
        }

        if (IsStoredAuthMarkerForCurrentDevice())
        {
            return ("인증 상태 : 정품 인증 완료", true, false);
        }

        ClearStoredAuthKey();
        return ("인증 상태 : 미인증", false, true);
    }

    private bool PersistOfflineProofAuthToPlayerInfo(AuthCompletionResult authResult)
    {
        string deviceId = Normalize(authResult.DeviceId);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Normalize(authResult.DeviceFingerprint);
        }

        string authPayload = LicenseHubAuthMarker.Build(LicenseHubAuthPolicy.ProductId, deviceId);
        if (string.IsNullOrWhiteSpace(authPayload))
        {
            return false;
        }

        bool changed = false;
        if (!string.Equals(playerInfoManager.PlayerInfo.PIF_AuthKey ?? string.Empty, authPayload, StringComparison.Ordinal))
        {
            playerInfoManager.PlayerInfo.PIF_AuthKey = authPayload;
            changed = true;
        }

        if (!string.Equals(playerInfoManager.PlayerInfo.PIF_MacAddress ?? string.Empty, sourceKey, StringComparison.OrdinalIgnoreCase))
        {
            playerInfoManager.PlayerInfo.PIF_MacAddress = sourceKey;
            changed = true;
        }

        if (changed)
        {
            playerInfoManager.SaveData();
        }

        return true;
    }

    private bool IsStoredAuthMarkerForCurrentDevice()
    {
        if (!LicenseHubAuthMarker.TryParse(playerInfoManager.PlayerInfo.PIF_AuthKey ?? string.Empty, out LicenseHubValidationMarker marker))
        {
            return false;
        }

        return marker.ProductId == LicenseHubAuthPolicy.ProductId &&
            string.Equals(playerInfoManager.PlayerInfo.PIF_MacAddress ?? string.Empty, sourceKey, StringComparison.OrdinalIgnoreCase);
    }

    private void PersistLicenseHubAuthToPlayerInfo(LicenseHubLocalLicenseFile license, ValidationResult validation)
    {
        if (validation == null || !validation.IsValid)
        {
            return;
        }

        string authPayload = LicenseHubAuthMarker.Build(license);
        if (string.IsNullOrWhiteSpace(authPayload))
        {
            ClearStoredAuthKey();
            return;
        }

        bool changed = false;
        if (!string.Equals(playerInfoManager.PlayerInfo.PIF_AuthKey ?? string.Empty, authPayload, StringComparison.Ordinal))
        {
            playerInfoManager.PlayerInfo.PIF_AuthKey = authPayload;
            changed = true;
        }

        if (!string.Equals(playerInfoManager.PlayerInfo.PIF_MacAddress ?? string.Empty, sourceKey, StringComparison.OrdinalIgnoreCase))
        {
            playerInfoManager.PlayerInfo.PIF_MacAddress = sourceKey;
            changed = true;
        }

        if (changed)
        {
            playerInfoManager.SaveData();
        }
    }

    private void ClearStoredAuthKey()
    {
        if (string.IsNullOrWhiteSpace(playerInfoManager.PlayerInfo.PIF_AuthKey))
        {
            return;
        }

        playerInfoManager.PlayerInfo.PIF_AuthKey = string.Empty;
        playerInfoManager.SaveData();
    }

    private static ScheduleRowModel ToRowModel(WeeklyPlayScheduleInfo row)
    {
        return new ScheduleRowModel
        {
            DayCode = row.DayCode,
            DayLabel = row.DayLabel,
            IsOnAir = row.WPS_IsOnAir,
            StartHour = row.WPS_Hour1,
            StartMinute = row.WPS_Min1,
            EndHour = row.WPS_Hour2,
            EndMinute = row.WPS_Min2
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string FormatPort(int value)
    {
        return value > 0 && value <= 65535 ? value.ToString() : string.Empty;
    }

    private static bool TryParsePort(string raw, out int value)
    {
        value = 0;
        return int.TryParse(raw, out value) && value > 0 && value <= 65535;
    }
}

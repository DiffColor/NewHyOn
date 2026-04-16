using AndoW.Shared;
using NewHyOn.Player.Settings.DataManager;
using NewHyOn.Player.Settings.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

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
        sourceKey = LegacyNetworkService.GetFirstMacAddress();
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            sourceKey = AuthRegistryService.GetUuid12FromWmi();
        }

        SystemPolicyService.DisableUac();
        AuthRegistryService.WriteDemoReg();
    }

    public ConfigPlayerSnapshot Load()
    {
        portInfoManager.LoadData();
        playerInfoManager.LoadData();
        localSettingsManager.LoadData();
        ttPlayerInfoManager.Load();
        weeklyInfoManager.LoadWeeklySchedule(playerInfoManager.PlayerInfo.PIF_GUID, playerInfoManager.PlayerInfo.PIF_PlayerName);

        (string authStatusText, bool isLicensed, bool authInputEnabled) = EvaluateAuthState();
        PortInfoClass portInfo = portInfoManager.DataList.FirstOrDefault() ?? new PortInfoClass();

        return new ConfigPlayerSnapshot
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
            FtpPort = portInfo.AIF_FTP.ToString(),
            SyncPort = portInfo.AIF_SYNC.ToString(),
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
            LedTransferPort = portInfo.AIF_FTP.ToString(),
            WeeklySchedules = weeklyInfoManager.ScheduleList.Select(ToRowModel).ToList()
        };
    }

    public AuthResult Authenticate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult
            {
                Success = false,
                StatusText = EvaluateAuthState().statusText,
                Message = "인증 비밀번호를 입력해주세요."
            };
        }

        string checkValue = GetPasswd2(sourceKey);
        if (password == checkValue || password == "turtle0419")
        {
            ExecuteAuthLogic();
            return new AuthResult
            {
                Success = true,
                StatusText = "인증 상태 : 정품 인증 완료",
                IsLicensed = true,
                DisablePasswordInput = true,
                Message = "인증키 생성에 성공했습니다."
            };
        }

        if (CheckInvalidAuthKey(playerInfoManager.PlayerInfo.PIF_AuthKey))
        {
            AuthRegistryService.WriteTryAuthReg();
            bool prohibitTrying = AuthRegistryService.ProhibitTrying();
            return new AuthResult
            {
                Success = false,
                StatusText = "인증 상태 : 시험판",
                IsLicensed = false,
                DisablePasswordInput = prohibitTrying,
                Message = "인증키 생성에 실패했습니다. \r\n3회 인증 실패 후에는 비밀번호 인증이 제한됩니다."
            };
        }

        return new AuthResult
        {
            Success = false,
            StatusText = "인증 상태 : 정품 인증 완료",
            IsLicensed = true,
            DisablePasswordInput = true,
            Message = "이미 인증된 장치입니다."
        };
    }

    public void SavePorts(string ftpPortText, string syncPortText)
    {
        if (!TryParsePort(ftpPortText, out int ftpPort) ||
            !TryParsePort(syncPortText, out int syncPort))
        {
            throw new InvalidOperationException("포트번호를 입력해 주세요.");
        }

        PortInfoClass info = portInfoManager.DataList.FirstOrDefault() ?? new PortInfoClass();
        info.AIF_FTP = ftpPort;
        info.AIF_SYNC = syncPort;

        if (portInfoManager.DataList.Count == 0)
        {
            portInfoManager.DataList.Add(info);
        }

        portInfoManager.SaveData();
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
                throw new InvalidOperationException("서버 아이피주소가 올바르지 않습니다.");
            }

            if (!IPAddress.TryParse(snapshot.PlayerIp, out _))
            {
                throw new InvalidOperationException("플레이어 아이피주소가 올바르지 않습니다.");
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IPAddress.TryParse(value, out _) || Uri.CheckHostName(value) != UriHostNameType.Unknown;
    }

    private void SaveAppInfo(ConfigPlayerSnapshot snapshot)
    {
        localSettingsManager.Settings.ManagerIP = snapshot.ManagerIp.Trim();
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
        playerInfoManager.PlayerInfo.PIF_MacAddress = LegacyNetworkService.GetMacAddressFromIp(snapshot.PlayerIp.Trim());
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
        bool isLicensed = !CheckInvalidAuthKey(playerInfoManager.PlayerInfo.PIF_AuthKey);
        if (isLicensed)
        {
            return ("인증 상태 : 정품 인증 완료", true, false);
        }

        return ("인증 상태 : 시험판", false, !AuthRegistryService.ProhibitTrying());
    }

    private void ExecuteAuthLogic()
    {
        List<string> networkCards = LegacyNetworkService.GetAllMacAddresses();
        string encodedKey = playerInfoManager.PlayerInfo.PIF_AuthKey;

        bool hasValid = networkCards.Any(nic =>
            string.Equals(encodedKey, AuthRegistryService.EncodeAuthKey(nic), StringComparison.CurrentCultureIgnoreCase));

        if (!hasValid && networkCards.Count < 1)
        {
            string uuidKey = AuthRegistryService.EncodeAuthKey(AuthRegistryService.GetUuid12FromWmi());
            hasValid = string.Equals(encodedKey, uuidKey, StringComparison.CurrentCultureIgnoreCase);
            if (!hasValid)
            {
                encodedKey = uuidKey;
            }
        }

        if (!hasValid)
        {
            if (networkCards.Count > 0)
            {
                encodedKey = AuthRegistryService.EncodeAuthKey(networkCards[0]);
            }

            playerInfoManager.PlayerInfo.PIF_AuthKey = encodedKey;
            playerInfoManager.SaveData();
        }
    }

    private bool CheckInvalidAuthKey(string encodedKey)
    {
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            return true;
        }

        List<string> networkCards = LegacyNetworkService.GetAllMacAddresses();
        foreach (string nic in networkCards)
        {
            if (encodedKey.Equals(AuthRegistryService.EncodeAuthKey(nic), StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
        }

        if (networkCards.Count < 1 &&
            encodedKey.Equals(AuthRegistryService.EncodeAuthKey(AuthRegistryService.GetUuid12FromWmi()), StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string GetPasswd2(string macString)
    {
        if (string.IsNullOrWhiteSpace(macString) || macString.Length < 4)
        {
            return string.Empty;
        }

        char[] chars = macString[^4..].ToCharArray();
        string numberString = string.Empty;
        foreach (char character in chars)
        {
            numberString += Convert.ToInt32(character.ToString(), 16);
        }

        if (numberString.Length < 4)
        {
            return string.Empty;
        }

        numberString = numberString[^4..];
        char[] reverseChars = numberString.ToCharArray();
        Array.Reverse(reverseChars);
        string reversed = new(reverseChars);
        reversed = reversed.TrimStart('0');
        if (string.IsNullOrWhiteSpace(reversed))
        {
            reversed = "0";
        }

        return (((int.Parse(reversed) * 2) - 1) * 2).ToString();
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

    private static bool TryParsePort(string raw, out int value)
    {
        value = 0;
        return int.TryParse(raw, out value) && value > 0 && value <= 65535;
    }
}

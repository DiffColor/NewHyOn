using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AndoW_Manager;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;

namespace TurtleTools
{
    public static class RethinkDbBootstrapper
    {
        private const string ExecutableName = "rethinkdb.exe";
        public const string ProcessName = "rethinkdb";
        private const string ProgramRuleName = "rethinkdb_program";
        private const string PortRule28015 = "rethinkdb_port_28015";
        private const string PortRule9911 = "rethinkdb_port_9911";
        private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan DefaultTablesReadyTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan TableReadyCheckInterval = TimeSpan.FromSeconds(2);
        private static readonly string[] DefaultTableNames =
        {
            nameof(PageInfoManager),
            nameof(PageListInfoManager),
            nameof(PlayerInfoManager),
            nameof(PlayerGroupManager),
            nameof(SpecialScheduleInfoManager),
            nameof(TextInfoManager),
            nameof(WeeklyInfoManagerClass),
            nameof(ServerSettingsManager),
            "CommandQueue",
            "UpdateThrottleSettings",
            "UpdateLease",
        };

        public static void EnsureRethinkDbReady()
        {
            EnsureRethinkDbReadyInternal(false, null);
        }

        public static bool EnsureRethinkDbReadyWithWait(TimeSpan? startupTimeout = null)
        {
            return EnsureRethinkDbReadyInternal(true, startupTimeout);
        }

        public static bool IsRethinkDbRunning()
        {
            return ProcessTools.CheckExeProcessAlive(ProcessName);
        }

        public static async Task<bool> EnsureAndWaitTablesReadyAsync(string databaseName, TimeSpan? totalTimeout = null, CancellationToken cancellationToken = default)
        {
            EnsureRethinkDbReady();

            var timeout = totalTimeout ?? DefaultTablesReadyTimeout;
            var perWaitTimeoutSeconds = (int)Math.Ceiling(TableReadyCheckInterval.TotalSeconds);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryWaitForTables(databaseName, perWaitTimeoutSeconds))
                {
                    return true;
                }

                await Task.Delay(TableReadyCheckInterval, cancellationToken);
            }

            return false;
        }

        private static bool EnsureRethinkDbReadyInternal(bool waitForStartup, TimeSpan? startupTimeout)
        {
            string exePath = GetExecutablePath();
            EnsureFirewallRules(exePath);
            bool wasRunning = IsRethinkDbRunning();

            if (!EnsureProcessRunning(exePath))
            {
                return false;
            }

            if (waitForStartup && !wasRunning)
            {
                var timeout = startupTimeout ?? DefaultStartupTimeout;
                if (!WaitForRethinkDbReady(timeout))
                {
                    Logger.WriteErrorLog($"RethinkDB가 {timeout.TotalSeconds}초 내에 시작되지 않았습니다.", Logger.GetLogFileName());
                    return false;
                }
            }

            return true;
        }

        private static void EnsureFirewallRules(string exePath)
        {
            try
            {
                EnsurePortRule(PortRule28015, 28015);
                EnsurePortRule(PortRule9911, 9911);
                EnsureProgramRule(exePath);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private static void EnsurePortRule(string ruleName, int port)
        {
            if (!SecurityTools.NeedToAddRule(ruleName))
            {
                return;
            }

            var ports = new Dictionary<string, int> { { ruleName, port } };
            SecurityTools.ReleaseFirewallRules(SecurityTools.CreateOpenPortNetshCmdList(ports));
        }

        private static void EnsureProgramRule(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return;
            }

            if (!SecurityTools.NeedToAddRule(ProgramRuleName))
            {
                return;
            }

            var progs = new Dictionary<string, string> { { ProgramRuleName, exePath } };
            SecurityTools.ReleaseFirewallRules(SecurityTools.CreateAuthorAppNetshCmdList(progs));
        }

        private static bool EnsureProcessRunning(string exePath)
        {
            try
            {
                if (ProcessTools.CheckExeProcessAlive(ProcessName))
                    return true;

                if (!File.Exists(exePath))
                {
                    Logger.WriteErrorLog($"RethinkDB executable not found: {exePath}", Logger.GetLogFileName());
                    return false;
                }

                ProcessTools.LaunchProcess(exePath, false, "--bind all --initial-password turtle04!9 --http-port 9911");
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
        }

        private static bool TryWaitForTables(string databaseName, int perWaitTimeoutSeconds)
        {
            try
            {
                EnsureDatabaseAndTables(databaseName);
                Connection conn = RethinkDbContext.GetRawConnection();
                var tableNames = GetTableNames(conn, databaseName);

                RethinkDB.R.Db(databaseName)
                    .Wait_()
                    .OptArg("wait_for", "ready_for_writes")
                    .OptArg("timeout", perWaitTimeoutSeconds)
                    .Run(conn);

                foreach (var table in tableNames)
                {
                    RethinkDB.R.Db(databaseName)
                        .Table(table)
                        .Wait_()
                        .OptArg("wait_for", "ready_for_writes")
                        .OptArg("timeout", perWaitTimeoutSeconds)
                        .Run(conn);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
        }

        private static List<string> GetTableNames(Connection conn, string databaseName)
        {
            try
            {
                var names = RethinkDB.R.Db(databaseName).TableList().RunAtom<List<string>>(conn);
                if (names == null)
                {
                    return new List<string>();
                }

                return names.Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return new List<string>();
            }
        }

        private static void EnsureDatabaseAndTables(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return;
            }

            try
            {
                RethinkDbConfigurator.EnsureConfigured();

                foreach (string tableName in DefaultTableNames)
                {
                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        continue;
                    }

                    RethinkDbContext.EnsureTable(databaseName, tableName, "id");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private static bool WaitForRethinkDbReady(TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (IsRethinkDbRunning() && CanConnectToServer(28015, TimeSpan.FromSeconds(1)))
                {
                    return true;
                }

                Thread.Sleep(300);
            }

            return false;
        }

        private static bool CanConnectToServer(int port, TimeSpan connectTimeout)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync("127.0.0.1", port);
                    if (connectTask.Wait(connectTimeout) && client.Connected)
                    {
                        return true;
                    }
                }
            }
            catch (SocketException)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }

            return false;
        }

        private static string GetExecutablePath()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                return Path.Combine(baseDir, ExecutableName);
            }
            catch
            {
                return ExecutableName;
            }
        }
    }
}

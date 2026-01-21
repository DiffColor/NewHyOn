using System;
using System.Collections.Generic;
using System.IO;

namespace TurtleTools
{
    public static class RethinkDbBootstrapper
    {
        private const string ExecutableName = "rethinkdb.exe";
        private const string ProcessName = "rethinkdb";
        private const string ProgramRuleName = "rethinkdb_program";
        private const string PortRule28015 = "rethinkdb_port_28015";
        private const string PortRule9911 = "rethinkdb_port_9911";

        public static void EnsureRethinkDbReady()
        {
            string exePath = GetExecutablePath();
            EnsureFirewallRules(exePath);
            EnsureProcessRunning(exePath);
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

        private static void EnsureProcessRunning(string exePath)
        {
            try
            {
                if (ProcessTools.CheckExeProcessAlive(ProcessName))
                    return;

                if (!File.Exists(exePath))
                {
                    Logger.WriteErrorLog($"RethinkDB executable not found: {exePath}", Logger.GetLogFileName());
                    return;
                }

                ProcessTools.LaunchProcess(exePath, false, "--bind all --initial-password turtle04!9 --http-port 9911");
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
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

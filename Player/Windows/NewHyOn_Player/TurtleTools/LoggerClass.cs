using System;
using System.Diagnostics;
using System.IO;


namespace TurtleTools
{
    public class Logger
    {
        private static void WriteDebugLine(string message)
        {
            try
            {
                Debug.WriteLine(message);
                Trace.WriteLine(message);
            }
            catch
            {
            }
        }

        public static string GetLogFileName()
        {
            DateTime dt = DateTime.Now;
            string logFileName = string.Format("{0}{1:D2}{2:D2}_Log.txt", dt.Year, dt.Month, dt.Day);
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogData");

            string folderPath = string.Format("{0}\\{1}{2:D2}", dataPath, dt.Year, dt.Month);
            Directory.CreateDirectory(folderPath);
            string logfilePath = string.Format("{0}\\{1}", folderPath, logFileName);

            return logfilePath;
        }

        public static string GetLogFileNameForAgent()
        {
            DateTime dt = DateTime.Now;
            string logFileName = string.Format("{0}{1:D2}{2:D2}_Log.txt", dt.Year, dt.Month, dt.Day);
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogDataForAgent");

            string folderPath = string.Format("{0}\\{1}{2:D2}", dataPath, dt.Year, dt.Month);
            Directory.CreateDirectory(folderPath);
            string logfilePath = string.Format("{0}\\{1}", folderPath, logFileName);

            return logfilePath;
        }
        public static string GetDebugLogFileName()
        {
            DateTime dt = DateTime.Now;
            string logFileName = string.Format("{0}{1:D2}{2:D2}_Log.txt", dt.Year, dt.Month, dt.Day);
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogDataForDebug");

            string folderPath = string.Format("{0}\\{1}{2:D2}", dataPath, dt.Year, dt.Month);
            Directory.CreateDirectory(folderPath);
            string logfilePath = string.Format("{0}\\{1}", folderPath, logFileName);

            return logfilePath;
        }

        public static void WriteErrorLog(string msg, string logpath)
        {
            string logMsg = string.Format("[{0}] {1} ------------------- <Error Reported> ------------------.", DateTime.Now, msg);
            WriteDebugLine(logMsg);

            StreamWriter logStream = null;

            try
            {
                if (File.Exists(logpath))
                    logStream = File.AppendText(logpath);
                else
                    logStream = new StreamWriter(logpath);

                logStream.WriteLine(logMsg);
                logStream.Flush();

                logStream.Close();
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (logStream != null)
                {
                    logStream.Close();
                }
            }
        }

        public static void WriteLog(string msg, string logpath)
        {
            string logMsg = string.Format("[{0}] {1}", DateTime.Now, msg);
            WriteDebugLine(logMsg);

            StreamWriter logStream = null;

            if (!File.Exists(logpath))
            {
                try
                {
                    logStream = new StreamWriter(logpath);
                    logStream.WriteLine(logMsg);
                    logStream.Flush();

                    logStream.Close();
                }
                catch (IOException iexo)
                {
                }
                finally
                {
                    if (logStream != null)
                    {
                        logStream.Close();
                    }

                }
            }
            else
            {
                try
                {
                    logStream = File.AppendText(logpath);
                    logStream.WriteLine(logMsg);
                    logStream.Flush();

                    logStream.Close();
                }
                catch (IOException ioExo)
                {

                }
                finally
                {
                    if (logStream != null)
                    {
                        logStream.Close();
                    }
                }


            }

        }


        public static void WriteLogWithAddNewLine(string msg, string logpath)
        {
            StreamWriter logStream;

            if (!File.Exists(logpath))
            {
                logStream = new StreamWriter(logpath);
            }
            else
            {
                logStream = File.AppendText(logpath);
            }

            string logMsg = string.Format("[{0}] {1}", DateTime.Now, msg);
            WriteDebugLine(logMsg);
            logStream.WriteLine(logMsg);
            logStream.WriteLine();
            logStream.Flush();

            logStream.Close();
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Tizen.Applications;

namespace NewHyOnFtpDownloader
{
    internal sealed class App : ServiceApplication
    {
        private const string Operation = "http://turtlelab.co.kr/appcontrol/newhyon/ftp-download";
        private const int BufferSize = 2 * 1024 * 1024;
        private static readonly Encoding ControlEncoding = new UTF8Encoding(false);

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            base.OnAppControlReceived(e);
            var control = e.ReceivedAppControl;
            var reply = new AppControl();

            try
            {
                if (control.Operation != Operation)
                {
                    throw new InvalidOperationException("UNSUPPORTED_OPERATION");
                }

                var host = ReadExtra(control, "host");
                var portText = ReadExtra(control, "port");
                var userName = ReadExtra(control, "userName");
                var password = ReadExtra(control, "password");
                var remotePath = NormalizeRemotePath(ReadExtra(control, "remotePath"));
                var fileName = SanitizeFileName(ReadExtra(control, "fileName"));
                if (string.IsNullOrWhiteSpace(host)
                    || string.IsNullOrWhiteSpace(portText)
                    || string.IsNullOrWhiteSpace(userName)
                    || string.IsNullOrWhiteSpace(remotePath)
                    || string.IsNullOrWhiteSpace(fileName))
                {
                    throw new InvalidOperationException("REQUEST_EMPTY");
                }
                int port;
                if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                    || port <= 0
                    || port > 65535)
                {
                    throw new InvalidOperationException("FTP_PORT_INVALID");
                }

                var sharedDataPath = Current.ApplicationInfo.SharedDataPath;
                if (string.IsNullOrWhiteSpace(sharedDataPath))
                {
                    throw new IOException("SHARED_DATA_PATH_EMPTY");
                }

                var downloadDirectory = Path.Combine(sharedDataPath, "downloads");
                Directory.CreateDirectory(downloadDirectory);
                var finalPath = Path.Combine(downloadDirectory, fileName);
                var tempPath = finalPath + ".tmp";
                DownloadFtpFile(host, port, userName, password, remotePath, tempPath);
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }

                File.Move(tempPath, finalPath);
                reply.ExtraData.Add("status", "ok");
                reply.ExtraData.Add("path", new Uri(finalPath).AbsoluteUri);
                control.ReplyToLaunchRequest(reply, AppControlReplyResult.Succeeded);
            }
            catch (Exception ex)
            {
                reply.ExtraData.Add("status", "error");
                reply.ExtraData.Add("error", ex.Message);
                if (control.IsReplyRequest)
                {
                    control.ReplyToLaunchRequest(reply, AppControlReplyResult.Succeeded);
                }
            }
        }

        private static string ReadExtra(ReceivedAppControl control, string key)
        {
            string value;
            return control.ExtraData.TryGet(key, out value) ? value : string.Empty;
        }

        private static string SanitizeFileName(string fileName)
        {
            var sanitized = fileName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '-');
            }

            return sanitized;
        }

        private static string NormalizeRemotePath(string remotePath)
        {
            var normalized = (remotePath ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return normalized.StartsWith("/", StringComparison.Ordinal)
                ? normalized
                : "/" + normalized;
        }

        private static void DownloadFtpFile(
            string host,
            int port,
            string userName,
            string password,
            string remotePath,
            string targetPath)
        {
            using (var control = new TcpClient())
            {
                control.ReceiveTimeout = 30000;
                control.SendTimeout = 30000;
                control.Connect(host, port);
                using (var stream = control.GetStream())
                using (var reader = new StreamReader(stream, ControlEncoding, false, 4096, true))
                using (var writer = new StreamWriter(stream, ControlEncoding, 4096, true) { NewLine = "\r\n", AutoFlush = true })
                {
                    ExpectPositive(reader);
                    SendCommand(writer, reader, "USER " + userName);
                    SendCommand(writer, reader, "PASS " + password);
                    SendCommand(writer, reader, "TYPE I");
                    var pasv = SendCommand(writer, reader, "PASV");
                    var endpoint = ParsePassiveEndpoint(pasv);
                    using (var data = new TcpClient())
                    {
                        data.ReceiveTimeout = 30000;
                        data.SendTimeout = 30000;
                        data.Connect(endpoint.Host, endpoint.Port);
                        writer.WriteLine("RETR " + remotePath);
                        ExpectPositive(reader);
                        using (var dataStream = data.GetStream())
                        using (var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                        {
                            dataStream.CopyTo(output, BufferSize);
                        }
                    }

                    ExpectPositive(reader);
                    writer.WriteLine("QUIT");
                }
            }
        }

        private static string SendCommand(StreamWriter writer, StreamReader reader, string command)
        {
            writer.WriteLine(command);
            return ExpectPositive(reader);
        }

        private static string ExpectPositive(StreamReader reader)
        {
            var response = ReadResponse(reader);
            if (response.Length < 3)
            {
                throw new IOException("FTP_EMPTY_RESPONSE");
            }

            var code = int.Parse(response.Substring(0, 3), CultureInfo.InvariantCulture);
            if (code >= 400)
            {
                throw new IOException("FTP_ERROR:" + response);
            }

            return response;
        }

        private static string ReadResponse(StreamReader reader)
        {
            var firstLine = reader.ReadLine();
            if (string.IsNullOrEmpty(firstLine))
            {
                return string.Empty;
            }

            if (firstLine.Length >= 4 && firstLine[3] == '-')
            {
                var prefix = firstLine.Substring(0, 3) + " ";
                string line;
                do
                {
                    line = reader.ReadLine();
                } while (line != null && !line.StartsWith(prefix, StringComparison.Ordinal));

                return line ?? firstLine;
            }

            return firstLine;
        }

        private static PassiveEndpoint ParsePassiveEndpoint(string response)
        {
            var start = response.IndexOf('(');
            var end = response.IndexOf(')');
            if (start < 0 || end <= start)
            {
                throw new IOException("FTP_PASV_PARSE_FAIL");
            }

            var parts = response.Substring(start + 1, end - start - 1).Split(',');
            if (parts.Length != 6)
            {
                throw new IOException("FTP_PASV_PARTS_FAIL");
            }

            var host = string.Join(".", parts[0], parts[1], parts[2], parts[3]);
            var port = (int.Parse(parts[4], CultureInfo.InvariantCulture) * 256)
                + int.Parse(parts[5], CultureInfo.InvariantCulture);
            return new PassiveEndpoint(host, port);
        }

        protected override void OnTerminate()
        {
            base.OnTerminate();
        }

        private static void Main(string[] args)
        {
            var app = new App();
            app.Run(args);
        }
    }

    internal sealed class PassiveEndpoint
    {
        public PassiveEndpoint(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }
    }
}

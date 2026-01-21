using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TurtleTools;

namespace HyOnPlayer.Services
{
    internal enum UdpSyncMessageType
    {
        Prepare,
        Commit
    }

    internal sealed class UdpSyncMessage
    {
        public UdpSyncMessage(UdpSyncMessageType type, int index, IPEndPoint remoteEndPoint, string raw)
        {
            Type = type;
            Index = index;
            RemoteEndPoint = remoteEndPoint;
            Raw = raw;
        }

        public UdpSyncMessageType Type { get; }
        public int Index { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public string Raw { get; }
    }

    internal sealed class UdpSyncService : IDisposable
    {
        private readonly object syncRoot = new object();
        private UdpClient receiver;
        private Thread receiveThread;
        private bool isRunning;
        private int port;

        public event Action<UdpSyncMessage> MessageReceived;

        public void Start(int port)
        {
            lock (syncRoot)
            {
                if (isRunning)
                {
                    return;
                }

                this.port = port;
                receiver = new UdpClient(port);
                receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                isRunning = true;

                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "UdpSyncReceiver"
                };
                receiveThread.Start();
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!isRunning)
                {
                    return;
                }

                isRunning = false;
                try
                {
                    receiver?.Close();
                }
                catch (Exception)
                {
                }
                receiver = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(500);
            }
            receiveThread = null;
        }

        public void SendPrepare(IEnumerable<IPEndPoint> targets, int index, int burstCount = 3)
        {
            SendMessage("P", targets, index, burstCount);
        }

        public void SendCommit(IEnumerable<IPEndPoint> targets, int index, int burstCount = 3)
        {
            SendMessage("C", targets, index, burstCount);
        }

        public void Dispose()
        {
            Stop();
        }

        private void SendMessage(string prefix, IEnumerable<IPEndPoint> targets, int index, int burstCount)
        {
            if (targets == null)
            {
                return;
            }

            string payload = string.Concat(prefix, ",", index.ToString());
            byte[] data = Encoding.UTF8.GetBytes(payload);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (UdpClient sender = new UdpClient())
                    {
                        for (int i = 0; i < Math.Max(1, burstCount); i++)
                        {
                            foreach (var target in targets)
                            {
                                sender.Send(data, data.Length, target);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
                }
            });
        }

        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                try
                {
                    if (receiver == null)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    byte[] data = receiver.Receive(ref remote);
                    if (data == null || data.Length == 0)
                    {
                        continue;
                    }

                    string message = Encoding.UTF8.GetString(data).Trim();
                    if (TryParseMessage(message, remote, out UdpSyncMessage parsed))
                    {
                        MessageReceived?.Invoke(parsed);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (!isRunning)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
                }
            }
        }

        private static bool TryParseMessage(string message, IPEndPoint remote, out UdpSyncMessage parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] parts = message.Split(',');
            if (parts.Length < 2)
            {
                return false;
            }

            string typeToken = parts[0].Trim();
            string indexToken = parts[1].Trim();

            if (!int.TryParse(indexToken, out int index))
            {
                return false;
            }

            UdpSyncMessageType type;
            if (string.Equals(typeToken, "P", StringComparison.OrdinalIgnoreCase))
            {
                type = UdpSyncMessageType.Prepare;
            }
            else if (string.Equals(typeToken, "C", StringComparison.OrdinalIgnoreCase))
            {
                type = UdpSyncMessageType.Commit;
            }
            else
            {
                return false;
            }

            parsed = new UdpSyncMessage(type, index, remote, message);
            return true;
        }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AndoW_Manager
{
    public sealed class TizenRemoteGatewayClient : IDisposable
    {
        private readonly Uri gatewayUri;
        private readonly string deviceId;
        private readonly string sessionId;
        private readonly Action<JObject> onMessage;
        private readonly Action<TizenRemoteStreamFrame, byte[]> onFrame;
        private readonly Action<string> onError;
        private readonly ClientWebSocket socket = new ClientWebSocket();
        private readonly HttpClient http = new HttpClient();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private TizenRemoteStreamFrame pendingFrame;

        public TizenRemoteGatewayClient(
            Uri gatewayUri,
            string deviceId,
            Action<JObject> onMessage,
            Action<TizenRemoteStreamFrame, byte[]> onFrame,
            Action<string> onError)
        {
            this.gatewayUri = gatewayUri;
            this.deviceId = (deviceId ?? string.Empty).Trim();
            this.sessionId = "wpf-" + Guid.NewGuid().ToString("N");
            this.onMessage = onMessage;
            this.onFrame = onFrame;
            this.onError = onError;
        }

        public async Task ConnectAsync()
        {
            await socket.ConnectAsync(gatewayUri, cancellation.Token);
            await SendEnvelopeAsync("hello", new
            {
                role = "wpf",
                displayName = "NewHyOn WPF Manager",
            });
            _ = Task.Run(ReadLoopAsync);
        }

        public Task RequestStreamAsync()
        {
            return SendEnvelopeAsync("stream.request", new
            {
                action = "start",
                backend = "player-sssp-file-capture",
                targetDeviceId = deviceId,
                requestedProfile = new
                {
                    width = 1280,
                    height = 720,
                    maxFps = 15,
                    maxBitrateKbps = 1000,
                },
            });
        }

        public Task StopStreamAsync()
        {
            if (socket.State != WebSocketState.Open)
            {
                return Task.FromResult(0);
            }

            return SendEnvelopeAsync("stream.stop", new
            {
                targetDeviceId = deviceId,
                reason = "window-closed",
            });
        }

        public Task SendPlayerCommandAsync(string command)
        {
            string safeCommand = (command ?? string.Empty).Trim();
            if (safeCommand.Length == 0)
            {
                return Task.FromResult(0);
            }

            return SendEnvelopeAsync("remote.command", new
            {
                command = safeCommand,
                action = safeCommand,
                targetDeviceId = deviceId,
            });
        }

        public async Task<bool> SendTvKeyAsync(string key)
        {
            string safeKey = (key ?? string.Empty).Trim();
            if (safeKey.Length == 0)
            {
                return false;
            }

            Uri uri = BuildHttpUri("/api/remote/devices/" + Uri.EscapeDataString(deviceId) + "/keys");
            using (StringContent content = new StringContent(safeKey, Encoding.UTF8, "text/plain"))
            using (HttpResponseMessage response = await http.PostAsync(uri, content, cancellation.Token))
            {
                string responseText = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode == false)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseText) ? response.ReasonPhrase : responseText);
                }

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return true;
                }

                JObject payload = JObject.Parse(responseText);
                JToken okToken = payload["ok"];
                return okToken == null || okToken.Type != JTokenType.Boolean || okToken.Value<bool>();
            }
        }

        public async Task<TizenRemoteDeviceReadiness> ReadDeviceReadinessAsync()
        {
            Uri uri = BuildHttpUri("/api/remote/readiness?deviceId=" + Uri.EscapeDataString(deviceId));
            using (HttpResponseMessage response = await http.GetAsync(uri, cancellation.Token))
            {
                string responseText = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode == false)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseText) ? response.ReasonPhrase : responseText);
                }

                TizenRemoteReadinessSnapshot snapshot = JsonConvert.DeserializeObject<TizenRemoteReadinessSnapshot>(responseText);
                if (snapshot == null || snapshot.Devices == null)
                {
                    return null;
                }

                return snapshot.Devices.Find(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private async Task SendEnvelopeAsync(string type, object payload)
        {
            if (socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("Tizen 원격 스트리밍 서버에 연결되어 있지 않습니다.");
            }

            JObject envelope = JObject.FromObject(new
            {
                version = 1,
                type = type,
                id = "wpf-" + Guid.NewGuid().ToString("N"),
                timestamp = DateTimeOffset.UtcNow.ToString("O"),
                deviceId = deviceId,
                sessionId = sessionId,
                payload = payload,
            });
            byte[] bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.None));
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation.Token);
        }

        private async Task ReadLoopAsync()
        {
            byte[] buffer = new byte[64 * 1024];
            try
            {
                while (cancellation.IsCancellationRequested == false && socket.State == WebSocketState.Open)
                {
                    using (MemoryStream message = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                return;
                            }

                            message.Write(buffer, 0, result.Count);
                        }
                        while (result.EndOfMessage == false);

                        byte[] bytes = message.ToArray();
                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            HandleBinaryFrame(bytes);
                            continue;
                        }

                        string json = Encoding.UTF8.GetString(bytes);
                        JObject envelope = JObject.Parse(json);
                        if (string.Equals((string)envelope["type"], "stream.frame", StringComparison.OrdinalIgnoreCase))
                        {
                            pendingFrame = envelope["payload"] == null
                                ? null
                                : envelope["payload"].ToObject<TizenRemoteStreamFrame>();
                        }

                        onMessage(envelope);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                onError(error.Message);
            }
        }

        private void HandleBinaryFrame(byte[] bytes)
        {
            TizenRemoteStreamFrame frame = pendingFrame;
            pendingFrame = null;
            if (frame == null)
            {
                frame = new TizenRemoteStreamFrame
                {
                    ByteLength = bytes == null ? 0 : bytes.Length,
                };
            }

            onFrame(frame, bytes);
        }

        private Uri BuildHttpUri(string pathAndQuery)
        {
            UriBuilder builder = new UriBuilder(gatewayUri)
            {
                Scheme = string.Equals(gatewayUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http",
                Path = string.Empty,
                Query = string.Empty,
            };
            string baseUrl = builder.Uri.GetLeftPart(UriPartial.Authority);
            return new Uri(baseUrl + pathAndQuery);
        }

        public void Dispose()
        {
            cancellation.Cancel();
            socket.Dispose();
            http.Dispose();
            cancellation.Dispose();
        }
    }
}

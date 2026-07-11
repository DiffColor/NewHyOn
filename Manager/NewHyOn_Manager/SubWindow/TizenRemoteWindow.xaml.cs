using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace AndoW_Manager
{
    public partial class TizenRemoteWindow : Window
    {
        private static readonly Uri DefaultGatewayUri = new Uri("wss://newhyon-remote.turtlelab.app/ws");
        private readonly string deviceId;
        private readonly string playerName;
        private readonly bool isLandscape;
        private readonly DispatcherTimer readinessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        private TizenRemoteGatewayClient client;
        private bool closingConfirmed;
        private int frameCount;

        public TizenRemoteWindow(string deviceId, string playerName, bool isLandscape)
        {
            InitializeComponent();
            this.deviceId = (deviceId ?? string.Empty).Trim();
            this.playerName = (playerName ?? string.Empty).Trim();
            this.isLandscape = isLandscape;
            readinessTimer.Tick += async delegate { await RefreshReadinessAsync(); };
            TitleTextBlock.Text = string.IsNullOrWhiteSpace(this.playerName) ? "Tizen 원격 화면" : "Tizen 원격 화면 - " + this.playerName;
            SubtitleTextBlock.Text = this.isLandscape ? "가로형 원격 스트림 준비 중" : "세로형 원격 스트림 준비 중";
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                SetPreviewStatus("Tizen 원격화면 대상 식별자가 없습니다.", true);
                return;
            }

            try
            {
                SetPreviewStatus("Tizen 원격 스트림에 연결 중입니다.", true);
                client = new TizenRemoteGatewayClient(DefaultGatewayUri, deviceId, HandleGatewayMessage, HandleStreamFrame, HandleGatewayError);
                await client.ConnectAsync();
                SubtitleTextBlock.Text = "스트림 프레임을 기다리는 중입니다.";
                readinessTimer.Start();
                await RefreshReadinessAsync();
                await client.RequestStreamAsync();
            }
            catch (Exception error)
            {
                SetPreviewStatus("Tizen 원격 접속 실패: " + error.Message, true);
                ReadinessTextBlock.Text = "연결 실패";
            }
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (closingConfirmed)
            {
                return;
            }

            closingConfirmed = true;
            readinessTimer.Stop();
            if (client != null)
            {
                e.Cancel = true;
                try
                {
                    await client.StopStreamAsync();
                }
                catch
                {
                }
                client.Dispose();
                client = null;
                Close();
            }
        }

        private async void TvKey_Click(object sender, RoutedEventArgs e)
        {
            string key = ReadButtonTag(sender);
            if (string.IsNullOrWhiteSpace(key) || client == null)
            {
                return;
            }

            await RunRemoteActionAsync(key, async delegate
            {
                bool ok = await client.SendTvKeyAsync(key);
                return ok;
            });
        }

        private async void PlayerCommand_Click(object sender, RoutedEventArgs e)
        {
            string command = ReadButtonTag(sender);
            if (string.IsNullOrWhiteSpace(command) || client == null)
            {
                return;
            }

            await RunRemoteActionAsync(command, async delegate
            {
                await client.SendPlayerCommandAsync(command);
                return true;
            });
        }

        private async Task RunRemoteActionAsync(string label, Func<Task<bool>> action)
        {
            RemoteResultTextBlock.Text = label + " 전송 중";
            try
            {
                bool ok = await action();
                RemoteResultTextBlock.Text = ok ? label + " 전송 완료" : label + " 전송 실패";
            }
            catch (Exception error)
            {
                RemoteResultTextBlock.Text = label + " 전송 실패: " + error.Message;
            }
        }

        private string ReadButtonTag(object sender)
        {
            Button button = sender as Button;
            return button == null ? string.Empty : Convert.ToString(button.Tag);
        }

        private void HandleGatewayMessage(JObject envelope)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                string type = Convert.ToString(envelope["type"]);
                JObject payload = envelope["payload"] as JObject;
                if (type == "stream.status")
                {
                    string detail = Convert.ToString(payload == null ? null : payload["detail"]);
                    if (string.IsNullOrWhiteSpace(detail) == false)
                    {
                        SubtitleTextBlock.Text = detail;
                    }
                }
                else if (type == "remote.command.result")
                {
                    string command = Convert.ToString(payload == null ? null : (payload["command"] ?? payload["action"]));
                    bool ok = payload != null && payload["ok"] != null && payload["ok"].Type == JTokenType.Boolean && payload["ok"].Value<bool>();
                    RemoteResultTextBlock.Text = string.Format("{0} {1}", command, ok ? "전송 완료" : "전송 실패");
                }
                else if (type == "error")
                {
                    string message = Convert.ToString(payload == null ? null : payload["message"]);
                    string code = Convert.ToString(payload == null ? null : payload["code"]);
                    SetPreviewStatus(string.IsNullOrWhiteSpace(message) ? "Tizen 원격 오류: " + code : message, true);
                }
            }));
        }

        private void HandleStreamFrame(TizenRemoteStreamFrame frame, byte[] bytes)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        BitmapImage image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();

                        PreviewImage.Source = image;
                        frameCount++;
                        SubtitleTextBlock.Text = string.Format("스트림 확인 완료 {0} · {1}x{2} · {3:N0} bytes", frameCount, image.PixelWidth, image.PixelHeight, bytes.Length);

                        if (HasVisiblePixels(image))
                        {
                            PreviewStatusOverlay.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            SetPreviewStatus("검은 캡처 프레임입니다. 스트리밍 프레임을 다시 확인하는 중입니다.", true);
                        }
                    }
                }
                catch (Exception error)
                {
                    SetPreviewStatus("스트림 프레임 표시 실패: " + error.Message, true);
                }
            }));
        }

        private void HandleGatewayError(string message)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                SetPreviewStatus("Tizen 원격 연결 오류: " + message, true);
            }));
        }

        private async Task RefreshReadinessAsync()
        {
            if (client == null)
            {
                ReadinessTextBlock.Text = "-";
                return;
            }

            try
            {
                TizenRemoteDeviceReadiness readiness = await client.ReadDeviceReadinessAsync();
                if (readiness == null)
                {
                    ReadinessTextBlock.Text = "not registered";
                    return;
                }

                ReadinessTextBlock.Text = readiness.Summary;
                if (readiness.LiveReady == false && frameCount == 0)
                {
                    SetPreviewStatus("Tizen 플레이어가 원격 스트리밍 서버에 연결되어 있지 않습니다.", true);
                }
            }
            catch (Exception error)
            {
                ReadinessTextBlock.Text = "readiness error: " + error.Message;
            }
        }

        private void SetPreviewStatus(string message, bool visible)
        {
            PreviewStatusTextBlock.Text = message;
            PreviewStatusOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool HasVisiblePixels(BitmapSource source)
        {
            if (source == null || source.PixelWidth < 1 || source.PixelHeight < 1)
            {
                return false;
            }

            BitmapSource readable = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = readable.PixelWidth;
            int height = readable.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            readable.CopyPixels(pixels, stride, 0);
            int stepX = Math.Max(1, width / 80);
            int stepY = Math.Max(1, height / 45);
            int visible = 0;
            int total = 0;

            for (int y = 0; y < height; y += stepY)
            {
                int row = y * stride;
                for (int x = 0; x < width; x += stepX)
                {
                    int offset = row + (x * 4);
                    double luma = (pixels[offset + 2] * 0.2126) + (pixels[offset + 1] * 0.7152) + (pixels[offset] * 0.0722);
                    if (luma >= 32)
                    {
                        visible++;
                    }
                    total++;
                }
            }

            return total > 0 && ((double)visible / total) >= 0.01;
        }
    }
}

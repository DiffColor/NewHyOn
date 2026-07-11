using System.Collections.Generic;
using Newtonsoft.Json;

namespace AndoW_Manager
{
    public sealed class TizenRemoteStreamFrame
    {
        [JsonProperty("frameId")]
        public int FrameId { get; set; }

        [JsonProperty("byteLength")]
        public int ByteLength { get; set; }

        [JsonProperty("captureElapsedMs")]
        public int CaptureElapsedMs { get; set; }

        [JsonProperty("readElapsedMs")]
        public int ReadElapsedMs { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    public sealed class TizenRemoteReadinessSnapshot
    {
        [JsonProperty("deviceCount")]
        public int DeviceCount { get; set; }

        [JsonProperty("liveReadyDeviceCount")]
        public int LiveReadyDeviceCount { get; set; }

        [JsonProperty("devices")]
        public List<TizenRemoteDeviceReadiness> Devices { get; set; } = new List<TizenRemoteDeviceReadiness>();
    }

    public sealed class TizenRemoteDeviceReadiness
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("playerConnected")]
        public bool PlayerConnected { get; set; }

        [JsonProperty("agentConnected")]
        public bool AgentConnected { get; set; }

        [JsonProperty("nativePublisherReady")]
        public bool NativePublisherReady { get; set; }

        [JsonProperty("publisherBackend")]
        public string PublisherBackend { get; set; } = string.Empty;

        [JsonProperty("liveReady")]
        public bool LiveReady { get; set; }

        [JsonProperty("blockers")]
        public List<string> Blockers { get; set; } = new List<string>();

        [JsonProperty("activeViewerCount")]
        public int ActiveViewerCount { get; set; }

        public string Summary
        {
            get
            {
                if (LiveReady)
                {
                    return string.Format("{0} ready / viewers={1}", string.IsNullOrWhiteSpace(PublisherBackend) ? "native-agent" : PublisherBackend, ActiveViewerCount);
                }

                string blockers = Blockers == null || Blockers.Count == 0 ? "LIVE_NOT_READY" : string.Join(" | ", Blockers);
                return "not ready / " + blockers;
            }
        }
    }
}

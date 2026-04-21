using AndoW_Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TurtleTools
{
    public sealed class PlayerHeartbeatMonitor : IDisposable
    {
        private readonly TimeSpan _pollInterval;
        private readonly Timer _timer;
        private readonly object _syncRoot = new object();
        private bool _isPolling;
        private readonly object _stateLock = new object();
        private Dictionary<string, PlayerHeartbeatState> _cachedStates = new Dictionary<string, PlayerHeartbeatState>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan OfflineThreshold = TimeSpan.FromSeconds(60);

        public event EventHandler<IReadOnlyList<PlayerHeartbeatState>> HeartbeatsChanged;

        public PlayerHeartbeatMonitor(TimeSpan? pollInterval = null)
        {
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(3);
            _timer = new Timer(OnTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        public IReadOnlyList<PlayerHeartbeatState> GetCurrentStatesSnapshot()
        {
            lock (_stateLock)
            {
                return _cachedStates?.Values.ToList() ?? new List<PlayerHeartbeatState>();
            }
        }

        public void UpdateFromSignalR(SignalRHeartbeatPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ClientId))
            {
                return;
            }

            bool isDisconnected = string.Equals(payload.Status, "disconnected", StringComparison.OrdinalIgnoreCase);
            DateTime? heartbeat = isDisconnected ? (DateTime?)null : DateTime.Now;
            double progress = NormalizeHeartbeatProcess(payload.Process);
            var state = new PlayerHeartbeatState(
                payload.ClientId.Trim(),
                ParseStatusValue(payload.Status),
                progress,
                payload.Version,
                payload.CurrentPage,
                payload.HdmiState.ToString(),
                heartbeat);

            bool changed = false;
            lock (_stateLock)
            {
                if (!_cachedStates.TryGetValue(state.ClientId, out var oldState) || !state.HasSamePayload(oldState))
                {
                    _cachedStates[state.ClientId] = state;
                    changed = true;
                }
                else
                {
                    // 동일한 페이로드라도 하트비트 시각은 갱신하여 오프라인 오탐지 방지
                    _cachedStates[state.ClientId] = new PlayerHeartbeatState(
                        oldState.ClientId,
                        oldState.Status,
                        oldState.Process,
                        oldState.Version,
                        oldState.CurrentPageName,
                        oldState.HdmiState,
                        heartbeat);
                }
            }

            if (changed)
            {
                HeartbeatsChanged?.Invoke(this, new List<PlayerHeartbeatState> { state });
            }
        }

        private List<PlayerHeartbeatState> CollectOfflineChanges()
        {
            var changes = new List<PlayerHeartbeatState>();
            Dictionary<string, PlayerHeartbeatState> snapshot;
            lock (_stateLock)
            {
                snapshot = new Dictionary<string, PlayerHeartbeatState>(_cachedStates, StringComparer.OrdinalIgnoreCase);
            }

            if (snapshot.Count == 0)
            {
                return changes;
            }

            var normalized = NormalizeStates(snapshot);
            foreach (var kv in normalized)
            {
                if (!snapshot.TryGetValue(kv.Key, out var oldState) ||
                    !kv.Value.HasSamePayload(oldState))
                {
                    changes.Add(kv.Value);
                }
            }

            lock (_stateLock)
            {
                _cachedStates = normalized;
            }

            return changes;
        }

        private void OnTimerTick(object state)
        {
            lock (_syncRoot)
            {
                if (_isPolling)
                {
                    return;
                }

                _isPolling = true;
            }

            try
            {
                var updatedStates = CollectOfflineChanges();
                if (updatedStates.Count > 0)
                {
                    HeartbeatsChanged?.Invoke(this, updatedStates);
                }
            }
            finally
            {
                lock (_syncRoot)
                {
                    _isPolling = false;
                }

                _timer.Change(_pollInterval, Timeout.InfiniteTimeSpan);
            }
        }


        private static PlayerStatus ParseStatusValue(string rawStatus)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return PlayerStatus.Stopped;
            }

            if (Enum.TryParse(rawStatus, true, out PlayerStatus parsed))
            {
                return parsed;
            }

            string normalized = rawStatus.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "playing":
                    return PlayerStatus.Playing;
                case "idle":
                case "stopped":
                case "stop":
                    return PlayerStatus.Stopped;
                case "queued":
                case "downloading":
                case "downloaded":
                case "validating":
                case "ready":
                case "applying":
                case "updating":
                    return PlayerStatus.Updating;
                default:
                    return PlayerStatus.Stopped;
            }
        }

        private static Dictionary<string, PlayerHeartbeatState> NormalizeStates(Dictionary<string, PlayerHeartbeatState> latestStates)
        {
            DateTime now = DateTime.Now;
            var normalized = new Dictionary<string, PlayerHeartbeatState>(latestStates.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in latestStates)
            {
                normalized[kv.Key] = NormalizeState(kv.Value, now);
            }
            return normalized;
        }

        private static PlayerHeartbeatState NormalizeState(PlayerHeartbeatState state, DateTime now)
        {
            if (state == null)
            {
                return null;
            }

            if (!state.LastHeartbeat.HasValue || now - state.LastHeartbeat.Value >= OfflineThreshold)
            {
                return PlayerHeartbeatState.CreateOffline(state.ClientId, state.Version);
            }
            return state;
        }

        private static double NormalizeHeartbeatProcess(double process)
        {
            double value = process;
            if (value > 1.0)
            {
                value /= 100.0;
            }

            return Math.Min(1.0, Math.Max(0.0, value));
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    public sealed class PlayerHeartbeatState
    {
        public PlayerHeartbeatState(string clientId, PlayerStatus status, double process, string version, string currentPageName, string hdmiState, DateTime? lastHeartbeat)
        {
            ClientId = clientId;
            Status = status;
            Process = process;
            Version = version ?? string.Empty;
            CurrentPageName = currentPageName ?? string.Empty;
            HdmiState = hdmiState ?? string.Empty;
            LastHeartbeat = lastHeartbeat;
        }

        public string ClientId { get; }
        public PlayerStatus Status { get; }
        public double Process { get; }
        public string Version { get; }
        public string CurrentPageName { get; }
        public string HdmiState { get; }
        public DateTime? LastHeartbeat { get; }

        public static PlayerHeartbeatState CreateOffline(string clientId, string version = "")
        {
            return new PlayerHeartbeatState(clientId, PlayerStatus.Stopped, 0, version ?? string.Empty, string.Empty, string.Empty, null);
        }

        public bool HasSamePayload(PlayerHeartbeatState other)
        {
            if (other == null)
            {
                return false;
            }

            return Status == other.Status &&
                   Math.Abs(Process - other.Process) < 0.0001 &&
                   string.Equals(Version, other.Version, StringComparison.Ordinal) &&
                   string.Equals(CurrentPageName, other.CurrentPageName, StringComparison.Ordinal) &&
                   string.Equals(HdmiState, other.HdmiState, StringComparison.Ordinal);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessLayoutRuntime
    {
        private const int LayoutTimerPeriodMilliseconds = 16;

        private readonly List<SeamlessContentSlot> slots = new List<SeamlessContentSlot>();
        private readonly MultimediaTimer.Timer playbackTimer;
        private readonly Stopwatch playbackStopwatch = new Stopwatch();
        private readonly object playbackLock = new object();

        private long playbackBaseOffsetMilliseconds;
        private long currentElapsedMilliseconds;
        private int timerBusy;
        private int completionRaised;
        private int lastPulseSecond = -1;
        private int lastPulsePrimaryIndex = -1;
        private int hostPresentationVisible;

        public SeamlessLayoutRuntime(string layoutName)
        {
            LayoutName = layoutName;
            Host = new SeamlessLayoutHost();

            for (int i = 0; i < 6; i++)
            {
                slots.Add(new SeamlessContentSlot(layoutName, i));
            }

            Host.AttachSurfaces(slots);
            playbackTimer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.Periodic,
                Period = LayoutTimerPeriodMilliseconds,
                Resolution = 1
            };
            playbackTimer.Tick += PlaybackTimer_Tick;
            State = SeamlessLayoutState.Idle;
        }

        public event Action<SeamlessLayoutRuntime> PlaybackCompleted;
        public event Action<SeamlessLayoutRuntime, SeamlessPlaybackPulse> PlaybackPulse;

        public string LayoutName { get; private set; }
        public SeamlessLayoutHost Host { get; private set; }
        public SeamlessLayoutState State { get; private set; }
        public SeamlessPagePlan CurrentPlan { get; private set; }

        public bool IsPresentationActive
        {
            get
            {
                return State == SeamlessLayoutState.Active
                    && Volatile.Read(ref hostPresentationVisible) == 1;
            }
        }

        public long CurrentElapsedMilliseconds
        {
            get
            {
                lock (playbackLock)
                {
                    return currentElapsedMilliseconds;
                }
            }
        }

        public async Task PrepareAsync(SeamlessPagePlan plan)
        {
            StopPlaybackTimer();
            CurrentPlan = plan;
            State = SeamlessLayoutState.Preparing;

            if (plan == null)
            {
                Clear();
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Host.SetCanvasSize(plan.CanvasWidth, plan.CanvasHeight);
                Host.Visibility = Visibility.Hidden;
                Host.Opacity = 0.0;
            });
            Volatile.Write(ref hostPresentationVisible, 0);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < slots.Count; i++)
            {
                SeamlessSlotPlan slotPlan = i < plan.Slots.Count ? plan.Slots[i] : new SeamlessSlotPlan();
                tasks.Add(slots[i].PrepareAsync(slotPlan));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            State = SeamlessLayoutState.Ready;
        }

        public void Activate()
        {
            if (CurrentPlan == null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Host.Visibility = Visibility.Visible;
                Host.Opacity = 1.0;
            });
            Volatile.Write(ref hostPresentationVisible, 1);

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Activate();
            }

            State = SeamlessLayoutState.Active;
            StartPlaybackFromOffset(0);
        }

        public void RestartLoop()
        {
            if (CurrentPlan == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].RestartFromBeginning();
            }

            State = SeamlessLayoutState.Active;
            Volatile.Write(ref hostPresentationVisible, 1);
            StartPlaybackFromOffset(0);
        }

        public void Deactivate()
        {
            StopPlaybackTimer();

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Deactivate();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Host.Opacity = 0.0;
                Host.Visibility = Visibility.Hidden;
            });
            Volatile.Write(ref hostPresentationVisible, 0);

            if (CurrentPlan != null)
            {
                State = SeamlessLayoutState.Ready;
            }
            else
            {
                State = SeamlessLayoutState.Idle;
            }
        }

        public void Clear()
        {
            StopPlaybackTimer();
            CurrentPlan = null;

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Stop();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Host.Opacity = 0.0;
                Host.Visibility = Visibility.Hidden;
                Host.SetCanvasSize(0, 0);
            });
            Volatile.Write(ref hostPresentationVisible, 0);

            State = SeamlessLayoutState.Idle;
        }

        public bool TryApplySyncIndex(int index)
        {
            bool applied = false;
            long resolvedOffsetMilliseconds = -1;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].TryApplySyncIndex(index))
                {
                    applied = true;
                    if (resolvedOffsetMilliseconds < 0)
                    {
                        long slotOffset;
                        if (slots[i].TryResolveStartOffsetMilliseconds(index, out slotOffset))
                        {
                            resolvedOffsetMilliseconds = slotOffset;
                        }
                    }
                }
            }

            if (applied && resolvedOffsetMilliseconds >= 0)
            {
                StartPlaybackFromOffset(resolvedOffsetMilliseconds);
            }

            return applied;
        }

        public SeamlessSyncStatus GetPrimarySyncStatus()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].HasPlayableItems && slots[i].IsCurrentContentVideo())
                {
                    return slots[i].GetSyncStatus();
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].HasPlayableItems)
                {
                    return slots[i].GetSyncStatus();
                }
            }

            return null;
        }

        public List<PlaybackDebugItem> GetDebugItems()
        {
            List<PlaybackDebugItem> items = new List<PlaybackDebugItem>();
            for (int i = 0; i < slots.Count; i++)
            {
                items.Add(slots[i].CreateDebugItem(State));
            }
            return items;
        }

        public bool Matches(SeamlessPagePlan plan)
        {
            if (CurrentPlan == null || plan == null)
            {
                return false;
            }

            return string.Equals(CurrentPlan.PlanKey, plan.PlanKey, StringComparison.OrdinalIgnoreCase);
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (State != SeamlessLayoutState.Active || CurrentPlan == null)
            {
                return;
            }

            if (Interlocked.Exchange(ref timerBusy, 1) == 1)
            {
                return;
            }

            try
            {
                long durationMilliseconds = Math.Max(1, CurrentPlan.DurationSeconds) * 1000L;
                long elapsedMilliseconds;
                lock (playbackLock)
                {
                    elapsedMilliseconds = playbackBaseOffsetMilliseconds + playbackStopwatch.ElapsedMilliseconds;
                    currentElapsedMilliseconds = Math.Min(elapsedMilliseconds, durationMilliseconds);
                }

                long displayElapsedMilliseconds = durationMilliseconds > 1
                    ? Math.Min(elapsedMilliseconds, durationMilliseconds - 1)
                    : 0;

                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i].ApplyPlaybackPosition(displayElapsedMilliseconds);
                }

                SeamlessPlaybackPulse pulse = BuildPlaybackPulse();
                if (pulse != null)
                {
                    PlaybackPulse?.Invoke(this, pulse);
                }

                if (elapsedMilliseconds < durationMilliseconds)
                {
                    return;
                }

                StopPlaybackTimer();
                if (Interlocked.Exchange(ref completionRaised, 1) == 0)
                {
                    PlaybackCompleted?.Invoke(this);
                }
            }
            finally
            {
                Interlocked.Exchange(ref timerBusy, 0);
            }
        }

        private void StartPlaybackFromOffset(long offsetMilliseconds)
        {
            lock (playbackLock)
            {
                playbackBaseOffsetMilliseconds = Math.Max(0, offsetMilliseconds);
                currentElapsedMilliseconds = playbackBaseOffsetMilliseconds;
                playbackStopwatch.Reset();
                playbackStopwatch.Start();
                completionRaised = 0;
            }

            lastPulseSecond = -1;
            lastPulsePrimaryIndex = -1;

            playbackTimer.Stop();
            playbackTimer.Start();
        }

        private void StopPlaybackTimer()
        {
            playbackTimer.Stop();
            lock (playbackLock)
            {
                playbackStopwatch.Reset();
                playbackBaseOffsetMilliseconds = 0;
                currentElapsedMilliseconds = 0;
            }

            lastPulseSecond = -1;
            lastPulsePrimaryIndex = -1;
        }

        private SeamlessPlaybackPulse BuildPlaybackPulse()
        {
            SeamlessSyncStatus status = GetPrimarySyncStatus();
            int elapsedSeconds = (int)(CurrentElapsedMilliseconds / 1000);

            bool isSecondTick = lastPulseSecond >= 0 && elapsedSeconds != lastPulseSecond;
            bool isContentBoundary = false;

            if (status != null)
            {
                if (lastPulsePrimaryIndex >= 0 && status.CurrentIndex != lastPulsePrimaryIndex)
                {
                    isContentBoundary = true;
                }

                lastPulsePrimaryIndex = status.CurrentIndex;
            }

            lastPulseSecond = elapsedSeconds;

            if (!isSecondTick && !isContentBoundary)
            {
                return null;
            }

            return new SeamlessPlaybackPulse
            {
                Status = status,
                IsSecondTick = isSecondTick,
                IsContentBoundary = isContentBoundary
            };
        }
    }
}

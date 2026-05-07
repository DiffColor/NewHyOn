using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TurtleTools;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessContentSlot
    {
        private const int PrepareTimeoutMilliseconds = 5000;

        private readonly string layoutName;
        private readonly int slotIndex;
        private readonly SeamlessMpvSurface surface;
        private readonly Func<SeamlessContentItem, bool> isContentPlayable;
        private SeamlessSlotPlan currentPlan;
        private int currentItemIndex;
        private long currentItemElapsedMilliseconds;
        private long currentItemDurationMilliseconds;
        private long lastLayoutElapsedMilliseconds;
        private bool isActive;
        private bool playlistPrepared;
        private bool? appliedLoopState;
        private int suspendedItemIndex;
        private TaskCompletionSource<bool> prepareSource;

        public SeamlessContentSlot(string layoutName, int slotIndex, Func<SeamlessContentItem, bool> isContentPlayable)
        {
            this.layoutName = layoutName;
            this.slotIndex = slotIndex;
            this.isContentPlayable = isContentPlayable;
            surface = new SeamlessMpvSurface();
            surface.MediaLoaded += Surface_MediaLoaded;
            State = SeamlessSlotState.Idle;
        }

        public FrameworkElement View
        {
            get { return surface; }
        }

        public SeamlessSlotState State { get; private set; }

        public string ElementName
        {
            get { return currentPlan != null ? currentPlan.ElementName : string.Empty; }
        }

        public bool HasPlayableItems
        {
            get { return HasAnyPlayableItems(); }
        }

        public bool NeedsActivationRecovery
        {
            get { return !isActive && currentItemIndex < 0 && HasPlayableItems; }
        }

        public async Task PrepareAsync(SeamlessSlotPlan plan, bool preserveAspectRatio)
        {
            currentPlan = plan ?? new SeamlessSlotPlan();
            currentItemIndex = 0;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = 0;
            lastLayoutElapsedMilliseconds = -1;
            isActive = false;
            playlistPrepared = false;
            appliedLoopState = null;
            suspendedItemIndex = -1;
            ApplyLayoutPlan(preserveAspectRatio);

            if (!HasPlayableItems)
            {
                SuspendForNoPlayableItems();
                State = SeamlessSlotState.Ready;
                return;
            }

            currentItemIndex = FindNextPlayableIndex(0);
            if (currentItemIndex < 0)
            {
                SuspendForNoPlayableItems();
                State = SeamlessSlotState.Ready;
                return;
            }

            State = SeamlessSlotState.Preparing;
            TaskCompletionSource<bool> activePrepareSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            prepareSource = activePrepareSource;

            RunOnUiThread(() =>
            {
                LoadPlaylist(false, false);
            });

            Task completed = await Task.WhenAny(activePrepareSource.Task, Task.Delay(PrepareTimeoutMilliseconds)).ConfigureAwait(false);
            if (completed != activePrepareSource.Task)
            {
                Logger.WriteErrorLog($"슬롯 준비 타임아웃: Layout={layoutName}, Slot={slotIndex}, Element={ElementName}", Logger.GetLogFileName());
                State = SeamlessSlotState.Error;
                RunOnUiThread(() =>
                {
                    surface.Stop();
                });

                if (ReferenceEquals(prepareSource, activePrepareSource))
                {
                    prepareSource = null;
                }

                return;
            }

            await activePrepareSource.Task.ConfigureAwait(false);
        }

        public void Activate()
        {
            if (!HasPlayableItems)
            {
                RunOnUiThread(() =>
                {
                    surface.HideSurface();
                });
                State = SeamlessSlotState.Active;
                return;
            }

            if (currentItemIndex < 0 || !IsContentPlayable(GetCurrentItem()))
            {
                int startIndex = currentItemIndex >= 0
                    ? currentItemIndex + 1
                    : 0;
                int nextPlayableIndex = FindNextPlayableIndex(startIndex);
                if (nextPlayableIndex < 0)
                {
                    RunOnUiThread(() =>
                    {
                        surface.HideSurface();
                    });
                    State = SeamlessSlotState.Active;
                    return;
                }

                currentItemIndex = nextPlayableIndex;
                currentItemElapsedMilliseconds = 0;
                currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
                lastLayoutElapsedMilliseconds = -1;
                appliedLoopState = null;
            }

            isActive = true;
            State = SeamlessSlotState.Active;

            RunOnUiThread(() =>
            {
                surface.HideSurface();
                if (!playlistPrepared)
                {
                    SwitchToCurrentItem(true, false);
                    return;
                }

                if (!surface.SeekToStart())
                {
                    SwitchToCurrentItem(true, false);
                    return;
                }

                UpdateCurrentItemLoopState(force: true);
                surface.ShowSurface();
                surface.Play();
            });
        }

        public void Deactivate()
        {
            isActive = false;
            RunOnUiThread(() =>
            {
                surface.HideSurface();
                surface.PauseIfActive();
            });

            if (State != SeamlessSlotState.Error)
            {
                State = SeamlessSlotState.Ready;
            }
        }

        public void Stop()
        {
            isActive = false;
            currentItemIndex = -1;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = 0;
            lastLayoutElapsedMilliseconds = -1;
            playlistPrepared = false;
            appliedLoopState = null;
            suspendedItemIndex = -1;

            RunOnUiThread(() =>
            {
                surface.Stop();
            });

            State = SeamlessSlotState.Idle;
        }

        private void SuspendForNoPlayableItems()
        {
            isActive = false;
            if (currentItemIndex >= 0)
            {
                suspendedItemIndex = currentItemIndex;
            }
            currentItemIndex = -1;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = 0;
            lastLayoutElapsedMilliseconds = -1;
            appliedLoopState = null;

            RunOnUiThread(() =>
            {
                surface.HideSurface();
                surface.PauseIfActive();
            });
        }

        public void RefreshForContentPeriodUpdate(long layoutElapsedMilliseconds)
        {
            if (!HasPlayableItems)
            {
                SuspendForNoPlayableItems();
                State = SeamlessSlotState.Ready;
                return;
            }

            if (isActive && currentItemIndex >= 0)
            {
                ApplyPlaybackPosition(layoutElapsedMilliseconds);
                return;
            }

            int startIndex = suspendedItemIndex >= 0
                ? suspendedItemIndex + 1
                : 0;
            int nextPlayableIndex = FindNextPlayableIndex(startIndex);
            if (nextPlayableIndex < 0)
            {
                SuspendForNoPlayableItems();
                State = SeamlessSlotState.Ready;
                return;
            }

            currentItemIndex = nextPlayableIndex;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
            lastLayoutElapsedMilliseconds = layoutElapsedMilliseconds;
            appliedLoopState = null;
            isActive = true;
            suspendedItemIndex = -1;

            bool applied = true;
                RunOnUiThread(() =>
                {
                    if (!SwitchToCurrentItem(true, true))
                    {
                        applied = false;
                        return;
                    }
                });

            if (applied)
            {
                State = SeamlessSlotState.Active;
                return;
            }

            isActive = false;
            State = SeamlessSlotState.Error;
        }

        public void ApplyPlaybackPosition(long layoutElapsedMilliseconds)
        {
            if (!isActive || currentItemIndex < 0)
            {
                return;
            }

            if (!HasPlayableItems)
            {
                SuspendForNoPlayableItems();
                State = SeamlessSlotState.Ready;
                return;
            }

            SeamlessContentItem currentItem = GetCurrentItem();
            if (!IsContentPlayable(currentItem))
            {
                int nextPlayableIndex = FindNextPlayableIndex(currentItemIndex + 1);
                if (nextPlayableIndex < 0)
                {
                    SuspendForNoPlayableItems();
                    State = SeamlessSlotState.Ready;
                    return;
                }

                currentItemIndex = nextPlayableIndex;
                currentItemElapsedMilliseconds = 0;
                currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
                lastLayoutElapsedMilliseconds = layoutElapsedMilliseconds;
                appliedLoopState = null;

                bool switchedApplied = true;
                RunOnUiThread(() =>
                {
                    if (!SwitchToCurrentItem(true, false))
                    {
                        switchedApplied = false;
                        return;
                    }
                });

                if (switchedApplied)
                {
                    State = SeamlessSlotState.Active;
                }

                return;
            }

            if (lastLayoutElapsedMilliseconds < 0 || layoutElapsedMilliseconds < lastLayoutElapsedMilliseconds)
            {
                lastLayoutElapsedMilliseconds = layoutElapsedMilliseconds;
            }

            long elapsedDeltaMilliseconds = Math.Max(0, layoutElapsedMilliseconds - lastLayoutElapsedMilliseconds);
            lastLayoutElapsedMilliseconds = layoutElapsedMilliseconds;

            if (currentItemDurationMilliseconds <= 0)
            {
                currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
            }

            currentItemElapsedMilliseconds += elapsedDeltaMilliseconds;
            bool switched = false;
            while (currentItemElapsedMilliseconds >= currentItemDurationMilliseconds)
            {
                currentItemElapsedMilliseconds -= currentItemDurationMilliseconds;

                int nextPlayableIndex = FindNextPlayableIndex(currentItemIndex + 1);
                if (nextPlayableIndex < 0)
                {
                    SuspendForNoPlayableItems();
                    State = SeamlessSlotState.Ready;
                    return;
                }

                currentItemIndex = nextPlayableIndex;
                currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
                appliedLoopState = null;
                switched = true;
            }

            if (!switched)
            {
                UpdateCurrentItemLoopState();
                return;
            }

            bool applied = true;
            long seekMilliseconds = currentItemElapsedMilliseconds;
            RunOnUiThread(() =>
            {
                if (!SwitchToCurrentItem(true, false))
                {
                    applied = false;
                    return;
                }

                if (seekMilliseconds > 0)
                {
                    surface.TrySeekToPosition(TimeSpan.FromMilliseconds(seekMilliseconds));
                }
            });

            if (applied)
            {
                State = SeamlessSlotState.Active;
            }
        }

        public void RestartFromBeginning()
        {
            if (!HasPlayableItems)
            {
                return;
            }

            currentItemIndex = FindNextPlayableIndex(0);
            if (currentItemIndex < 0)
            {
                SuspendForNoPlayableItems();
                State = SeamlessSlotState.Ready;
                return;
            }

            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
            lastLayoutElapsedMilliseconds = -1;
            appliedLoopState = null;

            if (!isActive)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                SwitchToCurrentItem(true, false);
            });
        }


        public SeamlessPlaybackStatus GetPlaybackStatus()
        {
            SeamlessContentItem current = GetCurrentItem();
            SeamlessContentItem next = GetNextItem();
            bool isVisible = IsSurfaceVisible();

            return new SeamlessPlaybackStatus
            {
                ElementName = ElementName,
                CurrentContentName = current != null && current.Source != null ? current.Source.CIF_FileName : string.Empty,
                NextContentName = next != null && next.Source != null ? next.Source.CIF_FileName : string.Empty,
                CurrentIndex = currentItemIndex,
                NextIndex = GetNextIndex(),
                ElapsedSeconds = currentItemElapsedMilliseconds / 1000,
                DurationSeconds = Math.Max(1, currentItemDurationMilliseconds / 1000),
                IsVisible = isVisible
            };
        }

        public PlaybackDebugItem CreateDebugItem(SeamlessLayoutState layoutState)
        {
            SeamlessContentItem current = GetCurrentItem();
            SeamlessContentItem next = GetNextItem();
            bool isVisible = IsSurfaceVisible();

            return new PlaybackDebugItem
            {
                ElementName = ElementName,
                CurrentContentName = current != null && current.Source != null ? current.Source.CIF_FileName : string.Empty,
                NextContentName = next != null && next.Source != null ? next.Source.CIF_FileName : string.Empty,
                ElapsedSeconds = currentItemElapsedMilliseconds / 1000,
                DurationSeconds = Math.Max(1, currentItemDurationMilliseconds / 1000),
                IsVisible = isVisible,
                LayoutName = layoutName,
                SlotIndex = slotIndex,
                LayoutState = layoutState,
                SlotState = State
            };
        }

        public bool IsCurrentContentVideo()
        {
            SeamlessContentItem current = GetCurrentItem();
            return current != null && current.IsVideo;
        }

        private void ApplyLayoutPlan(bool preserveAspectRatio)
        {
            RunOnUiThread(() =>
            {
                Canvas.SetLeft(surface, currentPlan.Left);
                Canvas.SetTop(surface, currentPlan.Top);
                Canvas.SetZIndex(surface, currentPlan.ZIndex);
                surface.Width = currentPlan.Width;
                surface.Height = currentPlan.Height;
                surface.Configure(currentPlan.IsMuted, preserveAspectRatio);
                surface.HideSurface();
            });
        }

        private void ResolvePlaybackPosition(long layoutElapsedMilliseconds, out int targetIndex, out long itemElapsedMilliseconds)
        {
            targetIndex = 0;
            itemElapsedMilliseconds = 0;

            if (!HasPlayableItems)
            {
                return;
            }

            long cycleElapsedMilliseconds = layoutElapsedMilliseconds;
            long cycleDurationMilliseconds = GetCycleDurationMilliseconds();
            if (cycleDurationMilliseconds > 0)
            {
                cycleElapsedMilliseconds %= cycleDurationMilliseconds;
            }

            long cumulative = 0;
            for (int i = 0; i < currentPlan.Items.Count; i++)
            {
                long itemDurationMilliseconds = GetItemDurationMilliseconds(i);
                if (cycleElapsedMilliseconds < cumulative + itemDurationMilliseconds)
                {
                    targetIndex = i;
                    itemElapsedMilliseconds = cycleElapsedMilliseconds - cumulative;
                    return;
                }

                cumulative += itemDurationMilliseconds;
            }

            targetIndex = Math.Max(0, currentPlan.Items.Count - 1);
            itemElapsedMilliseconds = Math.Max(0, GetItemDurationMilliseconds(targetIndex) - 1);
        }

        private int GetNextIndex()
        {
            if (!HasPlayableItems)
            {
                return -1;
            }

            return FindNextPlayableIndex(currentItemIndex + 1);
        }

        private SeamlessContentItem GetCurrentItem()
        {
            if (!HasPlayableItems || currentItemIndex < 0 || currentItemIndex >= currentPlan.Items.Count)
            {
                return null;
            }

            return currentPlan.Items[currentItemIndex];
        }

        private SeamlessContentItem GetNextItem()
        {
            int nextIndex = GetNextIndex();
            if (nextIndex < 0 || !HasPlayableItems || nextIndex >= currentPlan.Items.Count)
            {
                return null;
            }

            return currentPlan.Items[nextIndex];
        }

        private long GetCycleDurationMilliseconds()
        {
            if (!HasPlayableItems)
            {
                return 0;
            }

            long duration = 0;
            for (int i = 0; i < currentPlan.Items.Count; i++)
            {
                duration += GetItemDurationMilliseconds(i);
            }

            return duration;
        }

        private long GetItemDurationMilliseconds(int index)
        {
            if (!HasPlayableItems || index < 0 || index >= currentPlan.Items.Count)
            {
                return 1000;
            }

            return GetItemDurationMilliseconds(currentPlan.Items[index]);
        }

        private static long GetItemDurationMilliseconds(SeamlessContentItem item)
        {
            if (item == null)
            {
                return 1000;
            }

            return Math.Max(1, item.DurationSeconds) * 1000L;
        }

        private static long GetActualDurationMilliseconds(SeamlessContentItem item)
        {
            if (item == null)
            {
                return 1000;
            }

            return Math.Max(1, item.ActualDurationSeconds) * 1000L;
        }

        private int FindNextPlayableIndex(int startIndex)
        {
            if (currentPlan?.Items == null)
            {
                return -1;
            }

            int count = currentPlan.Items.Count;
            if (count == 0)
            {
                return -1;
            }

            int safeStartIndex = startIndex;
            if (safeStartIndex < 0)
            {
                safeStartIndex = 0;
            }

            safeStartIndex %= count;

            for (int offset = 0; offset < count; offset++)
            {
                int candidateIndex = (safeStartIndex + offset) % count;
                SeamlessContentItem candidate = currentPlan.Items[candidateIndex];
                if (IsContentPlayable(candidate))
                {
                    return candidateIndex;
                }
            }

            return -1;
        }

        private bool HasAnyPlayableItems()
        {
            if (currentPlan?.Items == null || currentPlan.Items.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < currentPlan.Items.Count; i++)
            {
                if (IsContentPlayable(currentPlan.Items[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsContentPlayable(SeamlessContentItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (isContentPlayable == null)
            {
                return true;
            }

            try
            {
                return isContentPlayable(item);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateCurrentItemLoopState(bool force = false)
        {
            SeamlessContentItem item = GetCurrentItem();
            bool shouldLoop = ShouldLoopCurrentItem(item, currentItemElapsedMilliseconds);
            if (!force && appliedLoopState.HasValue && appliedLoopState.Value == shouldLoop)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                surface.SetLooping(shouldLoop);
            });
            appliedLoopState = shouldLoop;
        }

        private static bool ShouldLoopCurrentItem(SeamlessContentItem item, long elapsedMilliseconds)
        {
            if (item == null || !item.IsVideo || !item.ShouldLoop)
            {
                return false;
            }

            if (item.TransitionByTimer)
            {
                return true;
            }

            long remainingMilliseconds = Math.Max(0, GetItemDurationMilliseconds(item) - Math.Max(0, elapsedMilliseconds));
            return remainingMilliseconds > GetActualDurationMilliseconds(item);
        }

        private void LoadPlaylist(bool autoPlay, bool hideUntilLoaded)
        {
            SeamlessContentItem item = GetCurrentItem();
            if (item == null)
            {
                surface.Stop();
                State = SeamlessSlotState.Error;
                if (prepareSource != null)
                {
                    prepareSource.TrySetResult(false);
                    prepareSource = null;
                }
                return;
            }

            currentItemDurationMilliseconds = GetItemDurationMilliseconds(item);
            playlistPrepared = surface.LoadPlaylist(currentPlan.Items.ToArray(), currentItemIndex, autoPlay);
            if (!playlistPrepared)
            {
                State = SeamlessSlotState.Error;
                if (prepareSource != null)
                {
                    prepareSource.TrySetResult(false);
                    prepareSource = null;
                }
                return;
            }

            if (!autoPlay)
            {
                surface.HideSurface();
            }
            else if (!hideUntilLoaded)
            {
                surface.ShowSurface();
            }

            UpdateCurrentItemLoopState(force: true);
        }

        private bool SwitchToCurrentItem(bool autoPlay, bool hideUntilLoaded)
        {
            SeamlessContentItem item = GetCurrentItem();
            if (item == null)
            {
                surface.Stop();
                State = SeamlessSlotState.Error;
                return false;
            }

            currentItemDurationMilliseconds = GetItemDurationMilliseconds(item);

            if (!playlistPrepared)
            {
                LoadPlaylist(autoPlay, hideUntilLoaded);
                return State != SeamlessSlotState.Error;
            }

            if (surface.PlaylistIndex == currentItemIndex)
            {
                UpdateCurrentItemLoopState(force: true);

                if (!surface.SeekToStart())
                {
                    Logger.WriteErrorLog($"플레이리스트 시작 위치 재설정 실패: Layout={layoutName}, Slot={slotIndex}, Element={ElementName}, Index={currentItemIndex}", Logger.GetLogFileName());
                    State = SeamlessSlotState.Error;
                    return false;
                }

                if (autoPlay)
                {
                    surface.ShowSurface();
                    surface.Play();
                }
                else
                {
                    surface.Pause();
                    surface.HideSurface();
                }

                return true;
            }

            bool switched = surface.SwitchToIndex(currentPlan.Items.ToArray(), currentItemIndex, autoPlay);
            if (!switched)
            {
                Logger.WriteErrorLog($"플레이리스트 index 전환 실패: Layout={layoutName}, Slot={slotIndex}, Element={ElementName}, Index={currentItemIndex}", Logger.GetLogFileName());
                State = SeamlessSlotState.Error;
                return false;
            }

            if (!autoPlay)
            {
                surface.HideSurface();
            }
            else if (hideUntilLoaded)
            {
                surface.HideSurface();
            }
            else
            {
                surface.ShowSurface();
            }

            UpdateCurrentItemLoopState(force: true);
            return true;
        }

        private void Surface_MediaLoaded()
        {
            if (State == SeamlessSlotState.Preparing)
            {
                State = SeamlessSlotState.Ready;
                if (prepareSource != null)
                {
                    prepareSource.TrySetResult(true);
                    prepareSource = null;
                }
                return;
            }

            if (State == SeamlessSlotState.Active)
            {
                RunOnUiThread(() =>
                {
                    surface.ShowSurface();
                });
            }
        }

        private bool IsSurfaceVisible()
        {
            bool isVisible = false;
            RunOnUiThread(() =>
            {
                isVisible = surface.Visibility == Visibility.Visible;
            });
            return isVisible;
        }

        private static void RunOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }
    }
}

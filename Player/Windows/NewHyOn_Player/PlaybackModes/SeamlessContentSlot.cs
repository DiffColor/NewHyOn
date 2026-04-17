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
        private SeamlessSlotPlan currentPlan;
        private int currentItemIndex;
        private long currentItemElapsedMilliseconds;
        private long currentItemDurationMilliseconds;
        private bool isActive;
        private bool playlistPrepared;
        private bool? appliedLoopState;
        private TaskCompletionSource<bool> prepareSource;

        public SeamlessContentSlot(string layoutName, int slotIndex)
        {
            this.layoutName = layoutName;
            this.slotIndex = slotIndex;
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
            get { return currentPlan != null && currentPlan.HasPlayableItems; }
        }

        public async Task PrepareAsync(SeamlessSlotPlan plan)
        {
            currentPlan = plan ?? new SeamlessSlotPlan();
            currentItemIndex = 0;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = 0;
            isActive = false;
            playlistPrepared = false;
            appliedLoopState = null;
            ApplyLayoutPlan();

            if (!currentPlan.HasPlayableItems)
            {
                Stop();
                State = SeamlessSlotState.Ready;
                return;
            }

            State = SeamlessSlotState.Preparing;
            TaskCompletionSource<bool> activePrepareSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            prepareSource = activePrepareSource;

            RunOnUiThread(() =>
            {
                LoadPlaylist(false);
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

            isActive = true;
            State = SeamlessSlotState.Active;

            RunOnUiThread(() =>
            {
                surface.ShowSurface();
                if (!playlistPrepared)
                {
                    SwitchToCurrentItem(true);
                    return;
                }

                if (!surface.SeekToStart())
                {
                    SwitchToCurrentItem(true);
                    return;
                }

                UpdateCurrentItemLoopState(force: true);
                surface.Play();
            });
        }

        public void Deactivate()
        {
            isActive = false;
            RunOnUiThread(() =>
            {
                surface.HideSurface();
                if (surface.IsPlaying)
                {
                    surface.Pause();
                }
            });

            if (State != SeamlessSlotState.Error)
            {
                State = SeamlessSlotState.Ready;
            }
        }

        public void Stop()
        {
            isActive = false;
            currentItemIndex = 0;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = 0;
            playlistPrepared = false;
            appliedLoopState = null;

            RunOnUiThread(() =>
            {
                surface.Stop();
            });

            State = SeamlessSlotState.Idle;
        }

        public void ApplyPlaybackPosition(long layoutElapsedMilliseconds)
        {
            if (!HasPlayableItems || !isActive)
            {
                return;
            }

            int targetIndex;
            long itemElapsedMilliseconds;
            ResolvePlaybackPosition(layoutElapsedMilliseconds, out targetIndex, out itemElapsedMilliseconds);

            currentItemElapsedMilliseconds = itemElapsedMilliseconds;
            currentItemDurationMilliseconds = GetItemDurationMilliseconds(targetIndex);

            if (targetIndex == currentItemIndex)
            {
                UpdateCurrentItemLoopState();
                return;
            }

            currentItemIndex = targetIndex;
            appliedLoopState = null;
            bool applied = true;
            RunOnUiThread(() =>
            {
                if (!SwitchToCurrentItem(true))
                {
                    applied = false;
                    return;
                }

                surface.ShowSurface();
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

            currentItemIndex = 0;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = GetItemDurationMilliseconds(currentItemIndex);
            appliedLoopState = null;

            if (!isActive)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                if (SwitchToCurrentItem(true))
                {
                    surface.ShowSurface();
                }
            });
        }

        public bool TryResolveStartOffsetMilliseconds(int index, out long offsetMilliseconds)
        {
            offsetMilliseconds = 0;
            if (!HasPlayableItems)
            {
                return false;
            }

            List<SeamlessContentItem> items = currentPlan.Items;
            if (index < 0 || index >= items.Count)
            {
                return false;
            }

            long offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += GetItemDurationMilliseconds(i);
            }

            offsetMilliseconds = offset;
            return true;
        }

        public bool TryApplySyncIndex(int index)
        {
            if (!HasPlayableItems)
            {
                return false;
            }

            List<SeamlessContentItem> items = currentPlan.Items;
            if (index < 0 || items == null || index >= items.Count)
            {
                return false;
            }

            currentItemIndex = index;
            currentItemElapsedMilliseconds = 0;
            currentItemDurationMilliseconds = GetItemDurationMilliseconds(index);
            appliedLoopState = null;

            bool applied = true;
            RunOnUiThread(() =>
            {
                if (!SwitchToCurrentItem(isActive))
                {
                    applied = false;
                    return;
                }

                if (isActive)
                {
                    surface.ShowSurface();
                }
            });

            if (applied)
            {
                State = isActive ? SeamlessSlotState.Active : SeamlessSlotState.Ready;
            }

            return applied;
        }

        public SeamlessSyncStatus GetSyncStatus()
        {
            SeamlessContentItem current = GetCurrentItem();
            SeamlessContentItem next = GetNextItem();
            bool isVisible = IsSurfaceVisible();

            return new SeamlessSyncStatus
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

        private void ApplyLayoutPlan()
        {
            RunOnUiThread(() =>
            {
                Canvas.SetLeft(surface, currentPlan.Left);
                Canvas.SetTop(surface, currentPlan.Top);
                Canvas.SetZIndex(surface, currentPlan.ZIndex);
                surface.Width = currentPlan.Width;
                surface.Height = currentPlan.Height;
                surface.Configure(currentPlan.IsMuted);
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

            if (currentPlan.Items.Count == 1)
            {
                return 0;
            }

            int next = currentItemIndex + 1;
            if (next >= currentPlan.Items.Count)
            {
                next = 0;
            }

            return next;
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

        private void LoadPlaylist(bool autoPlay)
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

            if (autoPlay)
            {
                surface.ShowSurface();
            }
            else
            {
                surface.HideSurface();
            }

            UpdateCurrentItemLoopState(force: true);
        }

        private bool SwitchToCurrentItem(bool autoPlay)
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
                LoadPlaylist(autoPlay);
                return State != SeamlessSlotState.Error;
            }

            bool switched = surface.SwitchToIndex(currentPlan.Items.ToArray(), currentItemIndex, autoPlay);
            if (!switched)
            {
                Logger.WriteErrorLog($"플레이리스트 index 전환 실패: Layout={layoutName}, Slot={slotIndex}, Element={ElementName}, Index={currentItemIndex}", Logger.GetLogFileName());
                State = SeamlessSlotState.Error;
                return false;
            }

            if (autoPlay)
            {
                surface.ShowSurface();
            }
            else
            {
                surface.HideSurface();
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

package kr.co.turtlelab.andowsignage.views;

import android.app.Activity;
import android.content.Context;
import android.widget.RelativeLayout;

import java.util.List;

import kr.co.turtlelab.andowsignage.datamodels.ElementDataModel;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;
import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;

public class PlaybackSlotView extends RelativeLayout {

    public interface SlotPreparedCallback {
        void onPrepared(PlaybackSlotView view);
    }

    public interface SlotPlaybackReadyCallback {
        void onPlaybackReady(PlaybackSlotView view);
    }

    private final MediaView mediaView;
    private boolean mediaActive = false;

    public PlaybackSlotView(Activity act, Context context) {
        super(context);

        LayoutParams params = new LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);

        mediaView = new MediaView(act, context, 1, 1, null);
        mediaView.setVisibility(GONE);
        addView(mediaView, params);

        releaseSlot();
    }

    public void applyElementBounds(ElementDataModel element) {
        LayoutParams params = (LayoutParams) getLayoutParams();
        if (params == null) {
            params = new LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
        }
        if (element == null) {
            params.width = 1;
            params.height = 1;
            params.leftMargin = 0;
            params.topMargin = 0;
        } else {
            params.width = Math.max(1, element.getWidth());
            params.height = Math.max(1, element.getHeight());
            params.leftMargin = element.getX();
            params.topMargin = element.getY();
        }
        setLayoutParams(params);
    }

    public void configureMediaSlot(ElementDataModel element, List<MediaDataModel> contents) {
        if (element == null) {
            deactivateSlot();
            return;
        }
        mediaActive = true;
        applyElementBounds(element);
        mediaView.configureMediaContents(Math.max(1, element.getWidth()), Math.max(1, element.getHeight()), contents);
        mediaView.setVisibility(VISIBLE);
        setVisibility(VISIBLE);
    }

    public void configureScrollSlot(ElementDataModel element, List<ScrolltextDataModel> contents) {
        // 현재 안드로이드 seamless 레이아웃은 MediaView만 사용한다.
        deactivateSlot();
    }

    public void configureWelcomeSlot(ElementDataModel element, WelcomeDataModel data) {
        // 현재 안드로이드 seamless 레이아웃은 MediaView만 사용한다.
        deactivateSlot();
    }

    public void configureTemplateSlot(ElementDataModel element, List<MediaDataModel> dataList) {
        // 현재 안드로이드 seamless 레이아웃은 MediaView만 사용한다.
        deactivateSlot();
    }

    public void deactivateSlot() {
        mediaActive = false;
        applyElementBounds(null);
        mediaView.deactivateMediaContents();
        setVisibility(INVISIBLE);
    }

    public void releaseSlot() {
        mediaActive = false;
        applyElementBounds(null);
        mediaView.releaseMediaContents();
        setVisibility(INVISIBLE);
    }

    public void prepareInitialContent(final SlotPreparedCallback callback) {
        if (isMediaSlot()) {
            mediaView.prepareInitialContent(new MediaView.PreparationCallback() {
                @Override
                public void onPrepared(MediaView view) {
                    if (callback != null) {
                        callback.onPrepared(PlaybackSlotView.this);
                    }
                }
            });
            return;
        }
        if (callback != null) {
            callback.onPrepared(this);
        }
    }

    public void showPreparedContent() {
        if (isMediaSlot()) {
            mediaView.showPreparedContent();
            setVisibility(VISIBLE);
        } else {
            setVisibility(INVISIBLE);
        }
    }

    public void startPreparedPlayback() {
        startPreparedPlayback(0L, null);
    }

    public void startPreparedPlayback(long layoutClockBaseAtElapsedRealtimeMs) {
        startPreparedPlayback(layoutClockBaseAtElapsedRealtimeMs, null);
    }

    public void startPreparedPlayback(final SlotPlaybackReadyCallback callback) {
        startPreparedPlayback(0L, callback);
    }

    public void startPreparedPlayback(long layoutClockBaseAtElapsedRealtimeMs, final SlotPlaybackReadyCallback callback) {
        if (isMediaSlot()) {
            mediaView.setLayoutContentClockBaseAtElapsedRealtimeMs(layoutClockBaseAtElapsedRealtimeMs);
            mediaView.startPreparedPlayback(new MediaView.PlaybackReadyCallback() {
                @Override
                public void onPlaybackReady(MediaView view) {
                    if (callback != null) {
                        callback.onPlaybackReady(PlaybackSlotView.this);
                    }
                }
            });
            return;
        }
        if (callback != null) {
            callback.onPlaybackReady(this);
        }
    }

    public void stopPlayback() {
        if (isMediaSlot()) {
            mediaView.stopPlaylist();
        }
    }

    public void pausePlayback() {
        if (isMediaSlot()) {
            mediaView.pausePlaylist();
        }
    }

    public void tick() {
        if (isMediaSlot()) {
            mediaView.count();
        }
    }

    public void nextContent() {
        if (isMediaSlot()) {
            mediaView.nextContent();
        }
    }

    public void prevContent() {
        if (isMediaSlot()) {
            mediaView.prevContent();
        }
    }

    public boolean isMediaSlot() {
        return mediaActive && mediaView.hasConfiguredContents();
    }

    public boolean shouldDelayLayoutTransition() {
        return isMediaSlot() && mediaView.shouldDelayLayoutTransition();
    }

    public boolean isContentTransitionDue(long nowElapsedRealtimeMs, long frameWindowMs) {
        return isMediaSlot() && mediaView.isContentTransitionDue(nowElapsedRealtimeMs, frameWindowMs);
    }

    public long getContentTransitionDeadlineAtElapsedRealtimeMs() {
        return isMediaSlot() ? mediaView.getContentTransitionDeadlineAtElapsedRealtimeMs() : 0L;
    }

    public void updateVideoLoopForLayoutTimer(long nowElapsedRealtimeMs, long frameWindowMs) {
        if (isMediaSlot()) {
            mediaView.updateVideoLoopForLayoutTimer(nowElapsedRealtimeMs, frameWindowMs);
        }
    }

    public void advanceContentFromLayoutTimer(long boundaryAtElapsedRealtimeMs) {
        if (isMediaSlot()) {
            mediaView.advanceContentFromLayoutTimer(boundaryAtElapsedRealtimeMs);
        }
    }

    public void beginSynchronizedContentSwap(long groupId, int participantCount) {
        if (isMediaSlot()) {
            mediaView.beginSynchronizedContentSwap(groupId, participantCount);
        }
    }

    public boolean isReadyForSynchronizedContentAdvance() {
        return !isMediaSlot() || mediaView.isReadyForSynchronizedContentAdvance();
    }

    public boolean willAdvanceToVideoContent() {
        return isMediaSlot() && mediaView.willAdvanceToVideoContent();
    }

    public boolean willAdvanceToImageContent() {
        return isMediaSlot() && mediaView.willAdvanceToImageContent();
    }

    public String getDebugContentState() {
        if (!isMediaSlot()) {
            return "slot=" + System.identityHashCode(this) + " inactive";
        }
        return "slot=" + System.identityHashCode(this) + " " + mediaView.getDebugContentState();
    }

    public boolean isPlaybackActiveForHeartbeat() {
        return isMediaSlot() && mediaView.isPlaybackActiveForHeartbeat();
    }
}

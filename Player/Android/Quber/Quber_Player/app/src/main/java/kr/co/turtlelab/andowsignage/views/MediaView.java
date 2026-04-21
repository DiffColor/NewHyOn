package kr.co.turtlelab.andowsignage.views;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Bitmap.Config;
import android.graphics.Color;
import android.media.MediaPlayer;
import android.media.MediaPlayer.OnCompletionListener;
import android.media.MediaPlayer.OnErrorListener;
import android.os.AsyncTask;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.ImageView;
import android.widget.ImageView.ScaleType;
import android.widget.RelativeLayout;

import com.nostra13.universalimageloader.core.DisplayImageOptions;
import com.nostra13.universalimageloader.core.ImageLoader;
import com.nostra13.universalimageloader.core.assist.FailReason;
import com.nostra13.universalimageloader.core.assist.ImageScaleType;
import com.nostra13.universalimageloader.core.listener.SimpleImageLoadingListener;

import java.io.File;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.AndoWSignageApp.CONTENT_TYPE;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class MediaView extends RelativeLayout {
    private static final String TAG = "MediaView";
    private static final String CONTENT_TRACE_TAG = "ContentTimerTrace";
    private static final String VIDEO_PREPARING_TAG_PREFIX = "preparing:";
    private static final long SYNCHRONIZED_SWAP_TIMEOUT_MS = 900L;
    private static final Handler SYNCHRONIZED_SWAP_HANDLER = new Handler(Looper.getMainLooper());
    private static final Map<Long, SynchronizedSwapGroup> SYNCHRONIZED_SWAP_GROUPS = new HashMap<>();

    public interface PreparationCallback {
        void onPrepared(MediaView view);
    }

    public interface PlaybackReadyCallback {
        void onPlaybackReady(MediaView view);
    }

    private static class SynchronizedSwapGroup {
        final long groupId;
        final int expectedCount;
        final List<Runnable> readyActions = new ArrayList<>();
        boolean committed = false;
        Runnable timeoutRunnable;

        SynchronizedSwapGroup(long groupId, int expectedCount) {
            this.groupId = groupId;
            this.expectedCount = Math.max(1, expectedCount);
        }
    }

    List<MediaDataModel> cdmList;

    long tick = 0;
    long playTime = 1000;
    CONTENT_TYPE s_usedType = CONTENT_TYPE.Image;

    ImageView imgView1;
    ImageView imgView2;
    TurtleVideoView videoView;
    TurtleVideoView videoView2;
    // 현재 seamless 재생 구조에서는 WebView 기반 콘텐츠를 사용하지 않는다.

    Activity act;
    Context ctx;

    Runnable mPopContentRunnable;
    Runnable mStopTaskRunnable;
    LoopPlay mLoopPlay = null;

    boolean s_isFirst = true;
    private static final long IMAGE_CROSSFADE_DURATION_MS = 240;
    private boolean initialPrepared = false;
    private boolean preparedContentShown = false;
    private boolean preparedPlaybackStarted = false;
    private boolean waitingForPreparedAdvance = false;
    private CONTENT_TYPE preparedInitialType = CONTENT_TYPE.Image;
    private boolean preparedInitialMuted = true;
    private String preparedInitialPath = "";
    private CONTENT_TYPE preparedNextType = CONTENT_TYPE.Image;
    private String preparedNextPath = "";
    private boolean preparedNextMuted = true;
    private PreparationCallback pendingPreparationCallback;
    private PlaybackReadyCallback pendingPlaybackReadyCallback;
    private boolean playbackReadyNotified = false;
    private static final long PLAYBACK_READY_FALLBACK_MS = 300L;
    private static final int PREPARED_VIDEO_SWAP_POSITION_MS = 80;
    private static final long PREPARED_VIDEO_SWAP_TIMEOUT_MS = 600L;
    private final Runnable playbackReadyFallbackRunnable = new Runnable() {
        @Override
        public void run() {
            markVideoPresentationStarted();
            notifyPlaybackReady();
        }
    };
    private static final long VIDEO_PRESENTATION_FALLBACK_MS = 300L;
    private final Handler playbackTimingHandler = new Handler(Looper.getMainLooper());
    private final Runnable videoPresentationFallbackRunnable = new Runnable() {
        @Override
        public void run() {
            markVideoPresentationStarted();
        }
    };
    private Runnable videoSwitchFallbackRunnable;
    private TurtleVideoView completionSuppressedVideoView;
    private TurtleVideoView pendingVideoSwitchTargetView;
    private final Runnable contentAdvanceRunnable = new Runnable() {
        @Override
        public void run() {
            requestContentAdvance();
        }
    };
    private boolean advancePending = false;
    private int advanceSignalCount = 0;
    private int consumedAdvanceSignalCount = 0;
    private boolean videoPresentationStarted = false;
    private long currentContentStartedAtElapsedRealtimeMs = 0L;
    private long currentContentDeadlineAtElapsedRealtimeMs = 0L;
    private long lastContentBoundaryAtElapsedRealtimeMs = 0L;
    private long layoutContentClockBaseAtElapsedRealtimeMs = 0L;
    private boolean currentContentBlocksLayoutSwitch = false;
    private int contentRenderWidth;
    private int contentRenderHeight;
    private int nextAdvanceContentIndex = 0;
    private long synchronizedSwapGroupId = 0L;

    DisplayImageOptions imgOpt;
    private static final ExecutorService loopExecutor = Executors.newCachedThreadPool();
    private String mediaConfigurationSignature = "";
    private boolean mediaConfigured = false;
    private int mediaConfigurationVersion = 0;

    public MediaView(Activity act, Context context, int width, int height, List<MediaDataModel> cdmList) {
        super(context);

        this.cdmList = cdmList != null ? cdmList : new ArrayList<MediaDataModel>();
        ctx = context;
        this.act = act;
        contentRenderWidth = Math.max(1, width);
        contentRenderHeight = Math.max(1, height);

        setMinimumWidth(width);
        setMinimumHeight(height);

        initChildViews();
        setViewEvents();
        setOtherSettings();

        mLoopPlay = new LoopPlay();
    }

    public void configureMediaContents(int width, int height, List<MediaDataModel> contents) {
        contentRenderWidth = Math.max(1, width);
        contentRenderHeight = Math.max(1, height);
        setMinimumWidth(width);
        setMinimumHeight(height);
        List<MediaDataModel> nextContents = copyMediaContents(contents);
        String nextSignature = buildMediaConfigurationSignature(nextContents);
        boolean changed = !nextSignature.equals(mediaConfigurationSignature);
        this.cdmList = nextContents;
        mediaConfigured = !this.cdmList.isEmpty();
        if (!changed) {
            return;
        }
        mediaConfigurationSignature = nextSignature;
        mediaConfigurationVersion++;
        resetPlaybackState();
    }

    public void deactivateMediaContents() {
        stopPlaylist();
        hideAllImageOverlays();
        if (videoView != null) {
            videoView.setVisibility(View.GONE);
            resetViewPosition(videoView);
        }
        if (videoView2 != null) {
            videoView2.setVisibility(View.GONE);
            resetViewPosition(videoView2);
        }
        setVisibility(View.GONE);
    }

    public void releaseMediaContents() {
        mediaConfigurationSignature = "";
        mediaConfigured = false;
        mediaConfigurationVersion++;
        this.cdmList = new ArrayList<MediaDataModel>();
        resetPlaybackState();
        setVisibility(View.GONE);
    }

    public boolean hasConfiguredContents() {
        return mediaConfigured && cdmList != null && !cdmList.isEmpty();
    }

    private void initChildViews() {

        LayoutParams params = new LayoutParams(
                LayoutParams.MATCH_PARENT,
                LayoutParams.MATCH_PARENT);

        params.addRule(RelativeLayout.CENTER_IN_PARENT, RelativeLayout.TRUE);

        imgView1 = new ImageView(ctx);
        imgView1.setVisibility(View.GONE);

        imgView2 = new ImageView(ctx);
        imgView2.setVisibility(View.GONE);

        videoView = new TurtleVideoView(ctx);
        videoView.setVisibility(View.GONE);

        videoView2 = new TurtleVideoView(ctx);
        videoView2.setVisibility(View.GONE);

        if (AndoWSignageApp.KEEP_ASPECT_RATIO) {
            imgView1.setScaleType(ScaleType.FIT_CENTER);
            imgView2.setScaleType(ScaleType.FIT_CENTER);
            videoView.setKeepAspectRatio(true);
            videoView2.setKeepAspectRatio(true);
        } else {
            imgView1.setScaleType(ScaleType.FIT_XY);
            imgView2.setScaleType(ScaleType.FIT_XY);
            videoView.setKeepAspectRatio(false);
            videoView2.setKeepAspectRatio(false);
        }

        addView(videoView, params);
        addView(videoView2, params);
        addView(imgView1, params);
        addView(imgView2, params);
        restoreVisibleOutputs();
    }

    private void setViewEvents() {
        setVideoViewEvents(videoView);
        setVideoViewEvents(videoView2);
    }

    private void setVideoViewEvents(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        targetVideoView.setOnCompletionListener(new OnCompletionListener() {

            @Override
            public void onCompletion(MediaPlayer mp) {
                if (targetVideoView == completionSuppressedVideoView) {
                    return;
                }
                if (targetVideoView != videoView || !isVideoViewOnScreen(targetVideoView)) {
                    return;
                }
                handleVideoCompletion();
            }
        });

        targetVideoView.setOnErrorListener(new OnErrorListener() {

            @Override
            public boolean onError(MediaPlayer arg0, int arg1, int arg2) {
                if (targetVideoView == pendingVideoSwitchTargetView) {
                    pendingVideoSwitchTargetView = null;
                    completionSuppressedVideoView = null;
                    stopVideoPlayback(targetVideoView);
                    popContent();
                    return true;
                }
                if (targetVideoView != videoView || !isVideoViewOnScreen(targetVideoView)) {
                    stopVideoPlayback(targetVideoView);
                    return true;
                }
                popContent();
                return true;
            }
        });
    }

    private void setOtherSettings() {

        mPopContentRunnable = new Runnable() {

            @Override
            public void run() {
                signalAdvanceReady();
            }
        };

        mStopTaskRunnable = new Runnable() {

            @Override
            public void run() {
                synchronized (mLoopPlay) {
                    mLoopPlay.cancel(true);
                }
            }
        };

        imgOpt = new DisplayImageOptions.Builder()
                .bitmapConfig(Config.RGB_565)
                .cacheInMemory(false)
                .cacheOnDisk(true)
                .resetViewBeforeLoading(false)
                .imageScaleType(ImageScaleType.EXACTLY)
                .build();
    }

    public void runPlaylist() {
        resetContentTimingState();
        preparedContentShown = false;
        preparedPlaybackStarted = false;
        waitingForPreparedAdvance = false;
        initialPrepared = false;
        pendingPlaybackReadyCallback = null;
        playbackReadyNotified = false;
        cancelPlaybackReadyFallback();
        s_isFirst = true;
        mLoopPlay = new LoopPlay();
        mLoopPlay.executeOnExecutor(loopExecutor);
    }

    public void prepareInitialContent(PreparationCallback callback) {
        resetContentTimingState();
        pendingPreparationCallback = callback;
        initialPrepared = false;
        preparedContentShown = false;
        preparedPlaybackStarted = false;
        waitingForPreparedAdvance = false;
        pendingPlaybackReadyCallback = null;
        playbackReadyNotified = false;
        cancelPlaybackReadyFallback();
        tick = 0;
        contentIdx = 0;
        nextAdvanceContentIndex = 0;
        s_isFirst = true;

        if (!hasConfiguredContents()) {
            notifyPrepared();
            return;
        }
        final int configVersion = mediaConfigurationVersion;

        int currentIndex = 0;
        int nextIndex = cdmList.size() > 1 ? 1 : 0;
        MediaDataModel current = cdmList.get(currentIndex);
        MediaDataModel next = cdmList.get(nextIndex);
        preparedInitialType = safeContentType(current);
        preparedInitialMuted = current != null && current.isMuted();
        preparedInitialPath = current == null ? "" : current.getFilePath();
        preparedNextType = safeContentType(next);
        preparedNextPath = next == null ? "" : next.getFilePath();
        preparedNextMuted = next != null && next.isMuted();
        playTime = current == null ? 1 : current.getPlayTimeSec();

        switch (preparedInitialType) {
            case Image:
                prepareInitialImage(preparedInitialPath, preparedNextType, preparedNextPath, configVersion);
                break;

            case Video:
                prepareInitialVideo(preparedInitialPath, preparedInitialMuted, preparedNextType, preparedNextPath, configVersion);
                break;

            default:
                initialPrepared = true;
                notifyPrepared();
                break;
        }
    }

    public void startPreparedPlayback() {
        startPreparedPlayback(null);
    }

    public void startPreparedPlayback(PlaybackReadyCallback callback) {
        if (preparedPlaybackStarted) {
            if (callback != null) {
                callback.onPlaybackReady(this);
            }
            return;
        }
        preparedPlaybackStarted = true;
        pendingPlaybackReadyCallback = callback;
        playbackReadyNotified = false;
        cancelPlaybackReadyFallback();
        tick = 0;
        manual = false;
        s_isFirst = false;
        s_usedType = preparedInitialType;

        if (cdmList == null || cdmList.isEmpty()) {
            notifyPlaybackReady();
            return;
        }

        if (preparedInitialType == CONTENT_TYPE.Video) {
            startPreparedVideoPlayback();
        } else {
            notifyContentActuallyPresented(getVisibleImageView(), new Runnable() {
                @Override
                public void run() {
                    notifyPlaybackReady();
                }
            });
        }

        if (cdmList.size() == 1) {
            contentIdx = 0;
            nextAdvanceContentIndex = 0;
            if (preparedInitialType == CONTENT_TYPE.Video) {
                videoView.setLoop(true);
            }
            return;
        }

        contentIdx = 1;
        nextAdvanceContentIndex = 1;
        waitingForPreparedAdvance = true;
        mLoopPlay = new LoopPlay();
        mLoopPlay.executeOnExecutor(loopExecutor);
    }

    public void showPreparedContent() {
        if (preparedContentShown) {
            return;
        }
        restoreVisibleOutputs();
        preparedContentShown = true;
        tick = 0;
        manual = false;
        s_isFirst = false;
        s_usedType = preparedInitialType;

        switch (preparedInitialType) {
            case Image:
                showPreparedImage();
                break;

            case Video:
                showPreparedVideoFrame();
                break;

            default:
                break;
        }
    }

    public void stopPlaylist() {
        try {
            stopVideoPlayback();
        } catch (Exception e) {

        } finally {
            cancelLoopPlayback(true);
            resetContentTimingState();
            initialPrepared = false;
            preparedContentShown = false;
            preparedPlaybackStarted = false;
            waitingForPreparedAdvance = false;
            pendingPreparationCallback = null;
            pendingPlaybackReadyCallback = null;
            playbackReadyNotified = false;
        }
    }

    public void pausePlaylist() {
        try {
            pauseVideoPlayback();
        } catch (Exception e) {

        } finally {
            cancelLoopPlayback(true);
            resetContentTimingState();
            preparedPlaybackStarted = false;
            waitingForPreparedAdvance = false;
            pendingPlaybackReadyCallback = null;
            playbackReadyNotified = false;
        }
    }

    private void resetPlaybackState() {
        stopPlaylist();
        hideAllImageOverlays();
        if (videoView != null) {
            videoView.setMediaInfoListener(null);
            videoView.setVisibility(View.GONE);
            resetViewPosition(videoView);
        }
        if (videoView2 != null) {
            videoView2.setMediaInfoListener(null);
            videoView2.setVisibility(View.GONE);
            resetViewPosition(videoView2);
        }
        setVisibility(View.GONE);
    }

    @Override
    protected void onDetachedFromWindow() {
        stopPlaylist();
        super.onDetachedFromWindow();
    }

    public void count() {
        if (currentContentStartedAtElapsedRealtimeMs <= 0L) {
            tick = 0L;
            return;
        }
        tick = Math.max(0L, (SystemClock.elapsedRealtime() - currentContentStartedAtElapsedRealtimeMs) / 1000L);
    }

    public void nextContent() {
        popContent();
    }

    public void setLayoutContentClockBaseAtElapsedRealtimeMs(long baseAtElapsedRealtimeMs) {
        layoutContentClockBaseAtElapsedRealtimeMs = Math.max(0L, baseAtElapsedRealtimeMs);
    }

    public void beginSynchronizedContentSwap(long groupId, int participantCount) {
        if (groupId <= 0L || participantCount <= 1) {
            synchronizedSwapGroupId = 0L;
            return;
        }
        synchronizedSwapGroupId = groupId;
        ensureSynchronizedSwapGroup(groupId, participantCount);
    }

    boolean manual = false;

    public void prevContent() {
        tick = 0;
        manual = true;
        int tmpIdx = contentIdx - 2;

        if (tmpIdx < -1)
            contentIdx = this.cdmList.size() - 2;
        else
            contentIdx = tmpIdx;

        popContent();
    }

    public void popContent() {
        requestContentAdvance();
    }

    int contentIdx = 0;

    class LoopPlay extends AsyncTask<Void, String, Void> {

        CONTENT_TYPE usedType = s_usedType;

        Bitmap image1 = null;
        Bitmap image2 = null;

        @Override
        protected Void doInBackground(Void... params) {
            int j = 0;
            if (waitingForPreparedAdvance) {
                waitingForPreparedAdvance = false;
                if (!waitForAdvanceSignal()) {
                    return null;
                }
            }
            while (!isCancelled()) {
                String[] contentData = null;
                synchronized (cdmList) {
                    if (cdmList.size() >= 1) {
                        AndoWSignage.act.stopTick();

                        if (contentIdx >= cdmList.size())
                            contentIdx = 0;

                        if (contentIdx >= cdmList.size() - 1)
                            j = 0;
                        else
                            j = contentIdx + 1;

                        if (isCancelled()) {
                            break;
                        }

                        playTime = cdmList.get(contentIdx).getPlayTimeSec();
                        Log.i(CONTENT_TRACE_TAG, "loop publish request media=" + debugId()
                                + " contentIdx=" + contentIdx
                                + " type=" + cdmList.get(contentIdx).getType()
                                + " playTimeSec=" + playTime
                                + " nextIdx=" + j
                                + " nextType=" + cdmList.get(j).getType());

                        contentData = new String[]{
                                cdmList.get(contentIdx).getType(),
                                cdmList.get(contentIdx).getFilePath(),
                                String.valueOf(cdmList.get(contentIdx).isMuted()),
                                cdmList.get(j).getType(),
                                cdmList.get(j).getFilePath(),
                                String.valueOf(cdmList.get(j).isMuted())
                        };
                        nextAdvanceContentIndex = j;
                    }
                }

                if (contentData == null) {
                    try {
                        Thread.sleep(250);
                    } catch (InterruptedException e) {
                        e.printStackTrace();
                    }
                    continue;
                }

                publishProgress(contentData);

                if (!waitForAdvanceSignal()) {
                    break;
                }

                if (s_isFirst)
                    s_isFirst = false;

                if (manual) {
                    s_isFirst = true;
                    manual = false;
                }

                synchronized (cdmList) {
                    contentIdx++;
                }
            }

            return null;
        }

        private boolean waitForAdvanceSignal() {
            synchronized (this) {
                while (!isCancelled() && consumedAdvanceSignalCount >= advanceSignalCount) {
                    try {
                        wait();
                    } catch (InterruptedException ignored) {
                    }
                }
                if (isCancelled()) {
                    return false;
                }
                consumedAdvanceSignalCount++;
                Log.i(CONTENT_TRACE_TAG, "loop advance signal consumed media=" + debugId()
                        + " consumed=" + consumedAdvanceSignalCount
                        + " total=" + advanceSignalCount
                        + " contentIdx=" + contentIdx);
                return true;
            }
        }

        @Override
        protected void onProgressUpdate(final String... contentData) {
            super.onProgressUpdate(contentData);

            SystemUtils.runOnUiThread(new Runnable() {

                public void run() {

                    try {
                        CONTENT_TYPE type1 = CONTENT_TYPE.valueOf(contentData[0]);
                        boolean muted1 = Boolean.parseBoolean(contentData[2]);
                        CONTENT_TYPE type2 = CONTENT_TYPE.valueOf(contentData[3]);
                        boolean muted2 = contentData.length > 5 && Boolean.parseBoolean(contentData[5]);
                        Log.i(CONTENT_TRACE_TAG, "loop publish apply media=" + debugId()
                                + " type=" + type1
                                + " nextType=" + type2
                                + " path=" + summarizePath(contentData[1]));

                        switch (type1) {
                            case Image:
                                boolean deferVideoStop = usedType == CONTENT_TYPE.Video;
                                releaseUsedResources(usedType, type1, false, deferVideoStop);
                                prepareForNewContentWindow(false);
                                showImageWithCrossfade(
                                        contentData[1],
                                        s_isFirst,
                                        type2,
                                        contentData[4],
                                        muted2,
                                        deferVideoStop,
                                        deferVideoStop ? new Runnable() {
                                            @Override
                                            public void run() {
                                                stopVideoPlayback();
                                            }
                                        } : null
                                );
                                break;

                            case Video:
                                boolean deferImageHide = usedType == CONTENT_TYPE.Image;
                                boolean keepPreviousVideoOpen = usedType == CONTENT_TYPE.Video;
                                releaseUsedResources(usedType, type1, deferImageHide, keepPreviousVideoOpen);
                                prepareForNewContentWindow(true);
                                showVideoWithImageFade(contentData[1], muted1, type2, contentData[4], muted2);
                                break;

                            case Flash:
                                // 현재 seamless 재생 구조에서는 WebView 기반 콘텐츠를 사용하지 않는다.
                                popContent();
                                break;

                            case WebSiteURL:
                                // 현재 seamless 재생 구조에서는 WebView 기반 콘텐츠를 사용하지 않는다.
                                popContent();
                                break;

                            case PPT:
                            default:
                                break;
                        }
                        s_usedType = usedType = type1;
                    } catch (Exception e) {
                    } finally {
                        AndoWSignage.act.startTick();
                        SystemUtils.systemBarVisibility(act, false);
                    }
                }

                private void releaseUsedResources(CONTENT_TYPE usedType, CONTENT_TYPE type, boolean deferImageHide, boolean deferVideoStop) {

                    if (usedType == null) return;

                    switch (usedType) {
                        case Image:
                            if (type != CONTENT_TYPE.Image && !deferImageHide) {
                                if (imgView1.isShown()) {
                                    imgView1.setVisibility(View.GONE);
                                } else if (imgView2.isShown()) {
                                    imgView2.setVisibility(View.GONE);
                                }
                            }
                            break;

                        case Video:
                            if (!deferVideoStop) {
                                stopVideoPlayback();
                            }
                            break;

                        case WebSiteURL:
                            break;
                    }
                }
            });
        }
    }

    private ImageView getVisibleImageView() {
        if (imgView1.getVisibility() == View.VISIBLE) {
            return imgView1;
        }
        if (imgView2.getVisibility() == View.VISIBLE) {
            return imgView2;
        }
        return null;
    }

    private ImageView getHiddenImageView(ImageView visibleView) {
        if (visibleView == imgView1) {
            return imgView2;
        }
        if (visibleView == imgView2) {
            return imgView1;
        }
        return imgView1;
    }

    private boolean isPreloadedImageView(ImageView view, String filePath) {
        return view != null
                && filePath != null
                && filePath.equals(view.getTag())
                && view.getDrawable() != null;
    }

    private ImageView findPreloadedImageView(String filePath, ImageView excludeView) {
        if (isPreloadedImageView(imgView1, filePath) && imgView1 != excludeView) {
            return imgView1;
        }
        if (isPreloadedImageView(imgView2, filePath) && imgView2 != excludeView) {
            return imgView2;
        }
        return null;
    }

    private void showImageWithCrossfade(final String filePath, boolean immediate, final CONTENT_TYPE nextType, final String nextPath, final boolean nextMuted, boolean fadeOverVideo, final Runnable endAction) {
        restoreVisibleOutputs();
        final ImageView currentView = getVisibleImageView();
        final ImageView preloadedView = findPreloadedImageView(filePath, currentView);
        final ImageView nextView = preloadedView != null ? preloadedView : getHiddenImageView(currentView);
        if (nextView == null) {
            return;
        }

        nextView.animate().cancel();
        if (currentView != null) {
            currentView.animate().cancel();
        }

        boolean alreadyLoaded = isPreloadedImageView(nextView, filePath);

        if (alreadyLoaded) {
            showImageImmediately(nextView, currentView, nextType, nextPath, nextMuted, endAction);
            return;
        }

        nextView.setTag(filePath);
        displayImageAtRenderSize(nextView, filePath, new SimpleImageLoadingListener() {
            @Override
            public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                showImageImmediately(nextView, currentView, nextType, nextPath, nextMuted, endAction);
            }

            @Override
            public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                showImageImmediately(nextView, currentView, nextType, nextPath, nextMuted, endAction);
            }
        });
    }

    private void showImageImmediately(final ImageView nextView,
                                      final ImageView previousView,
                                      final CONTENT_TYPE nextType,
                                      final String nextPath,
                                      final boolean nextMuted,
                                      final Runnable endAction) {
        if (nextView == null) {
            if (endAction != null) {
                endAction.run();
            }
            return;
        }
        runOrRegisterSynchronizedSwap(new Runnable() {
            @Override
            public void run() {
                nextView.animate().cancel();
                nextView.setAlpha(1f);
                nextView.setVisibility(View.VISIBLE);
                nextView.bringToFront();
                if (previousView != null && previousView != nextView) {
                    previousView.animate().cancel();
                    previousView.setVisibility(View.GONE);
                    previousView.setAlpha(1f);
                }
                notifyContentActuallyPresented(nextView, new Runnable() {
                    @Override
                    public void run() {
                        if (endAction != null) {
                            endAction.run();
                        }
                        preloadNextMediaIfNeeded(nextType, nextPath, nextMuted, previousView);
                    }
                });
            }
        });
    }

    private void preloadNextImageIfNeeded(CONTENT_TYPE nextType, String nextPath, ImageView targetView) {
        if (nextType != CONTENT_TYPE.Image || nextPath == null || targetView == null) {
            return;
        }
        targetView.setAlpha(1f);
        targetView.setVisibility(View.GONE);
        targetView.setTag(nextPath);
        displayImageAtRenderSize(targetView, nextPath, null);
    }

    private void preloadNextMediaIfNeeded(CONTENT_TYPE nextType, String nextPath, boolean nextMuted, ImageView imageTargetView) {
        if (nextType == CONTENT_TYPE.Image) {
            preloadNextImageIfNeeded(nextType, nextPath, imageTargetView);
            return;
        }
        if (nextType == CONTENT_TYPE.Video) {
            preloadNextVideoIfNeeded(nextType, nextPath, nextMuted, mediaConfigurationVersion);
        }
    }

    private void preloadNextVideoIfNeeded(CONTENT_TYPE nextType, String nextPath, boolean nextMuted, final int configVersion) {
        if (nextType != CONTENT_TYPE.Video) {
            return;
        }
        final String normalizedPath = normalizeLocalVideoPath(nextPath);
        if (!isPlayableLocalVideo(normalizedPath)) {
            return;
        }
        final TurtleVideoView targetVideoView = getStandbyVideoView();
        if (targetVideoView == null) {
            return;
        }
        if (isPreparedVideoView(targetVideoView, normalizedPath) || isPreparingVideoView(targetVideoView, normalizedPath)) {
            return;
        }
        prepareStandbyVideoView(targetVideoView, normalizedPath, nextMuted, configVersion);
    }

    private void prepareStandbyVideoView(final TurtleVideoView targetVideoView, final String normalizedPath, boolean muted, final int configVersion) {
        if (targetVideoView == null || TextUtils.isEmpty(normalizedPath)) {
            return;
        }
        final String preparingTag = getPreparingVideoTag(normalizedPath);
        final boolean targetMuted = muted;
        try {
            targetVideoView.setTag(preparingTag);
            targetVideoView.setMediaInfoListener(null);
            targetVideoView.setMuted(true);
            targetVideoView.setLoop(true);
            showStandbyVideoView(targetVideoView);
            targetVideoView.setMediaInfoListener(new MediaPlayer.OnInfoListener() {
                @Override
                public boolean onInfo(MediaPlayer mp, int what, int extra) {
                    if (what == MediaPlayer.MEDIA_INFO_VIDEO_RENDERING_START) {
                        markStandbyVideoPrepared(targetVideoView, normalizedPath, preparingTag, configVersion, targetMuted);
                    }
                    return false;
                }
            });
            targetVideoView.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
                @Override
                public void onPrepared(MediaPlayer mp) {
                    if (!isCurrentMediaConfiguration(configVersion)) {
                        return;
                    }
                    markStandbyVideoPrepared(targetVideoView, normalizedPath, preparingTag, configVersion, targetMuted);
                }
            });
            targetVideoView.setVideoPath(normalizedPath);
        } catch (Exception ignored) {
            targetVideoView.setTag(null);
        }
    }

    private void markStandbyVideoPrepared(TurtleVideoView targetVideoView, String normalizedPath, String preparingTag, int configVersion, boolean muted) {
        if (targetVideoView == null || !isCurrentMediaConfiguration(configVersion)) {
            return;
        }
        Object tag = targetVideoView.getTag();
        if (!preparingTag.equals(tag)) {
            return;
        }
        targetVideoView.setMediaInfoListener(null);
        targetVideoView.setMuted(true);
        targetVideoView.setLoop(true);
        showStandbyVideoView(targetVideoView);
        pausePreparedStandbyVideo(targetVideoView);
        targetVideoView.setTag(normalizedPath);
        Log.i(CONTENT_TRACE_TAG, "standby prepared media=" + debugId()
                + " view=" + System.identityHashCode(targetVideoView)
                + " path=" + summarizePath(normalizedPath));
    }

    private void pausePreparedStandbyVideo(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        try {
            targetVideoView.pause();
            targetVideoView.seekTo(1);
        } catch (Exception ignored) {
        }
    }

    private void runAfterOffscreenFrame(final TurtleVideoView targetVideoView, final Runnable action) {
        if (targetVideoView == null) {
            if (action != null) {
                action.run();
            }
            return;
        }
        targetVideoView.postOnAnimation(new Runnable() {
            @Override
            public void run() {
                targetVideoView.postOnAnimation(new Runnable() {
                    @Override
                    public void run() {
                        if (action != null) {
                            action.run();
                        }
                    }
                });
            }
        });
    }

    private void runWhenPreparedVideoFrameReady(final TurtleVideoView targetVideoView,
                                                final long startedAtUptimeMs,
                                                final Runnable action) {
        if (targetVideoView == null) {
            if (action != null) {
                action.run();
            }
            return;
        }
        targetVideoView.postOnAnimation(new Runnable() {
            @Override
            public void run() {
                if (targetVideoView != pendingVideoSwitchTargetView) {
                    return;
                }
                int currentPositionMs = 0;
                try {
                    currentPositionMs = targetVideoView.getCurrentPosition();
                } catch (Exception ignored) {
                }
                long elapsedMs = SystemClock.uptimeMillis() - startedAtUptimeMs;
                if (currentPositionMs >= PREPARED_VIDEO_SWAP_POSITION_MS
                        || elapsedMs >= PREPARED_VIDEO_SWAP_TIMEOUT_MS) {
                    if (action != null) {
                        action.run();
                    }
                    return;
                }
                runWhenPreparedVideoFrameReady(targetVideoView, startedAtUptimeMs, action);
            }
        });
    }

    private int getSafeVideoPositionMs(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return 0;
        }
        try {
            return Math.max(0, targetVideoView.getCurrentPosition());
        } catch (Exception ignored) {
            return 0;
        }
    }

    private static void ensureSynchronizedSwapGroup(final long groupId, int participantCount) {
        if (groupId <= 0L || participantCount <= 1) {
            return;
        }
        SynchronizedSwapGroup existing = SYNCHRONIZED_SWAP_GROUPS.get(groupId);
        if (existing != null) {
            return;
        }
        final SynchronizedSwapGroup group = new SynchronizedSwapGroup(groupId, participantCount);
        group.timeoutRunnable = new Runnable() {
            @Override
            public void run() {
                commitSynchronizedSwapGroup(groupId, "timeout");
            }
        };
        SYNCHRONIZED_SWAP_GROUPS.put(groupId, group);
        SYNCHRONIZED_SWAP_HANDLER.postDelayed(group.timeoutRunnable, SYNCHRONIZED_SWAP_TIMEOUT_MS);
    }

    private static void registerSynchronizedSwapAction(long groupId, Runnable action) {
        if (groupId <= 0L) {
            if (action != null) {
                action.run();
            }
            return;
        }
        SynchronizedSwapGroup group = SYNCHRONIZED_SWAP_GROUPS.get(groupId);
        if (group == null || group.committed) {
            if (action != null) {
                action.run();
            }
            return;
        }
        if (action != null) {
            group.readyActions.add(action);
        }
        if (group.readyActions.size() >= group.expectedCount) {
            commitSynchronizedSwapGroup(groupId, "ready");
        }
    }

    private static void commitSynchronizedSwapGroup(long groupId, String reason) {
        SynchronizedSwapGroup group = SYNCHRONIZED_SWAP_GROUPS.remove(groupId);
        if (group == null || group.committed) {
            return;
        }
        group.committed = true;
        if (group.timeoutRunnable != null) {
            SYNCHRONIZED_SWAP_HANDLER.removeCallbacks(group.timeoutRunnable);
        }
        Log.i(CONTENT_TRACE_TAG, "sync swap commit group=" + groupId
                + " reason=" + reason
                + " ready=" + group.readyActions.size()
                + " expected=" + group.expectedCount);
        List<Runnable> actions = new ArrayList<>(group.readyActions);
        for (Runnable action : actions) {
            if (action != null) {
                action.run();
            }
        }
    }

    private void runOrRegisterSynchronizedSwap(Runnable action) {
        long groupId = synchronizedSwapGroupId;
        synchronizedSwapGroupId = 0L;
        if (groupId > 0L) {
            Log.i(CONTENT_TRACE_TAG, "sync swap ready media=" + debugId()
                    + " group=" + groupId);
            registerSynchronizedSwapAction(groupId, action);
            return;
        }
        if (action != null) {
            action.run();
        }
    }

    private String getPreparingVideoTag(String normalizedPath) {
        return VIDEO_PREPARING_TAG_PREFIX + normalizedPath;
    }

    private boolean isPreparingVideoView(TurtleVideoView targetVideoView, String normalizedPath) {
        return targetVideoView != null
                && !TextUtils.isEmpty(normalizedPath)
                && getPreparingVideoTag(normalizedPath).equals(targetVideoView.getTag());
    }

    private boolean isPreparedVideoView(TurtleVideoView targetVideoView, String normalizedPath) {
        return targetVideoView != null
                && !TextUtils.isEmpty(normalizedPath)
                && normalizedPath.equals(targetVideoView.getTag());
    }

    private TurtleVideoView findPreparedVideoView(String normalizedPath, TurtleVideoView excludeView) {
        if (isPreparedVideoView(videoView2, normalizedPath) && videoView2 != excludeView) {
            return videoView2;
        }
        if (isPreparedVideoView(videoView, normalizedPath) && videoView != excludeView) {
            return videoView;
        }
        return null;
    }

    private TurtleVideoView getStandbyVideoView() {
        if (videoView2 != null && videoView2 != videoView) {
            return videoView2;
        }
        return null;
    }

    private TurtleVideoView getCurrentOnScreenVideoView() {
        if (isVideoViewOnScreen(videoView)) {
            return videoView;
        }
        if (isVideoViewOnScreen(videoView2)) {
            return videoView2;
        }
        return null;
    }

    private boolean isVideoViewOnScreen(TurtleVideoView targetVideoView) {
        return targetVideoView != null
                && targetVideoView.getVisibility() == View.VISIBLE
                && Math.abs(targetVideoView.getTranslationX()) < 1f;
    }

    private void showVideoOnScreen(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        targetVideoView.setVisibility(View.VISIBLE);
        targetVideoView.setAlpha(1f);
        targetVideoView.setTranslationX(0f);
        targetVideoView.setTranslationY(0f);
        targetVideoView.bringToFront();
    }

    private void showStandbyVideoView(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        targetVideoView.setVisibility(View.VISIBLE);
        targetVideoView.setAlpha(1f);
        targetVideoView.setTranslationX(getStandbyTranslationX());
        targetVideoView.setTranslationY(0f);
    }

    private void stageVideoBehindPreviousFrame(TurtleVideoView targetVideoView, TurtleVideoView previousVideoView) {
        if (targetVideoView == null) {
            return;
        }
        targetVideoView.setVisibility(View.VISIBLE);
        targetVideoView.setAlpha(1f);
        targetVideoView.setTranslationX(0f);
        targetVideoView.setTranslationY(0f);
        if (previousVideoView != null && previousVideoView != targetVideoView) {
            previousVideoView.setVisibility(View.VISIBLE);
            previousVideoView.setAlpha(1f);
            previousVideoView.setTranslationX(0f);
            previousVideoView.setTranslationY(0f);
            previousVideoView.bringToFront();
        } else {
            targetVideoView.bringToFront();
        }
    }

    private float getStandbyTranslationX() {
        int width = contentRenderWidth;
        if (width <= 0) {
            width = getWidth();
        }
        if (width <= 0) {
            width = getMeasuredWidth();
        }
        if (width <= 0) {
            width = AndoWSignageApp.getDeviceWidth();
        }
        if (width <= 0) {
            width = 1920;
        }
        return width + 32f;
    }

    private void promoteVideoView(TurtleVideoView targetVideoView) {
        if (targetVideoView == null || targetVideoView == videoView) {
            return;
        }
        TurtleVideoView previousActiveVideoView = videoView;
        videoView = targetVideoView;
        videoView2 = previousActiveVideoView;
    }

    private void hideAllImageOverlays() {
        hideImageOverlay(imgView1);
        hideImageOverlay(imgView2);
    }

    private void hideImageOverlay(ImageView view) {
        if (view == null) {
            return;
        }
        view.animate().cancel();
        view.setVisibility(View.GONE);
        view.setAlpha(1f);
        view.setBackgroundColor(Color.TRANSPARENT);
    }

    private void stopVideoPlayback() {
        cancelVideoPresentationFallback();
        stopVideoPlayback(videoView);
        if (videoView2 != null && videoView2 != videoView) {
            stopVideoPlayback(videoView2);
        }
    }

    private void stopVideoPlayback(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        try {
            if (targetVideoView == completionSuppressedVideoView) {
                completionSuppressedVideoView = null;
            }
            if (targetVideoView == pendingVideoSwitchTargetView) {
                pendingVideoSwitchTargetView = null;
            }
            targetVideoView.setTag(null);
            targetVideoView.setMediaInfoListener(null);
            targetVideoView.stopPlayback();
        } catch (Exception e) {
        } finally {
            targetVideoView.setVisibility(View.GONE);
            resetViewPosition(targetVideoView);
        }
    }

    private void pauseVideoPlayback() {
        cancelVideoPresentationFallback();
        pauseVideoPlayback(videoView);
        if (videoView2 != null && videoView2 != videoView) {
            pauseVideoPlayback(videoView2);
        }
    }

    private void pauseVideoPlayback(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        try {
            targetVideoView.setMediaInfoListener(null);
            if (targetVideoView.isPlaying()) {
                targetVideoView.pause();
            }
        } catch (Exception ignored) {
        }
    }

    private void cancelLoopPlayback(boolean wakeLoop) {
        LoopPlay currentLoop = mLoopPlay;
        if (currentLoop == null) {
            return;
        }
        synchronized (currentLoop) {
            currentLoop.cancel(true);
            if (wakeLoop) {
                currentLoop.notifyAll();
            }
        }
    }

    private void showVideoWithImageFade(final String videoPath, final boolean muted, final CONTENT_TYPE nextType, final String nextPath, final boolean nextMuted) {
        restoreVisibleOutputs();
        final String normalizedPath = normalizeLocalVideoPath(videoPath);
        Log.i(CONTENT_TRACE_TAG, "show video media=" + debugId()
                + " path=" + summarizePath(normalizedPath)
                + " nextType=" + nextType
                + " currentView=" + System.identityHashCode(videoView)
                + " standbyView=" + System.identityHashCode(videoView2));
        if (!isPlayableLocalVideo(normalizedPath)) {
            Log.w(TAG, "showVideoWithImageFade: invalid video file. path=" + normalizedPath);
            hideAllImageOverlays();
            stopVideoPlayback();
            popContent();
            return;
        }

        final TurtleVideoView previousVideoView = isVideoViewOnScreen(videoView) ? videoView : null;
        final ImageView overlay = getVisibleImageView();
        final boolean useFakeOverlay = overlay == null && previousVideoView == null;
        final ImageView fakeOverlay;
        final ImageView preloadTarget;

        if (useFakeOverlay) {
            fakeOverlay = imgView1 != null ? imgView1 : imgView2;
            preloadTarget = (fakeOverlay == imgView1) ? imgView2 : imgView1;
            if (fakeOverlay != null) {
                fakeOverlay.animate().cancel();
                fakeOverlay.setImageDrawable(null);
                fakeOverlay.setTag(null);
                fakeOverlay.setBackgroundColor(Color.argb(1, 0, 0, 0));
                fakeOverlay.setAlpha(1f);
                fakeOverlay.setVisibility(View.VISIBLE);
                fakeOverlay.bringToFront();
            }
        } else {
            fakeOverlay = null;
            preloadTarget = null;
        }

        final ImageView overlayToFade = overlay != null ? overlay : fakeOverlay;
        if (overlayToFade != null) {
            overlayToFade.animate().cancel();
            overlayToFade.setAlpha(1f);
            overlayToFade.setVisibility(View.VISIBLE);
            overlayToFade.bringToFront();
        }

        TurtleVideoView targetVideoView = findPreparedVideoView(normalizedPath, previousVideoView);
        if (targetVideoView == null) {
            targetVideoView = previousVideoView != null ? getStandbyVideoView() : videoView;
        }
        if (targetVideoView == null) {
            targetVideoView = videoView;
        }
        final TurtleVideoView transitionTargetVideoView = targetVideoView;

        cancelVideoPresentationFallback();
        if (previousVideoView != null && transitionTargetVideoView != previousVideoView) {
            completionSuppressedVideoView = previousVideoView;
            showStandbyVideoView(transitionTargetVideoView);
        } else {
            completionSuppressedVideoView = null;
            showVideoOnScreen(transitionTargetVideoView);
        }
        transitionTargetVideoView.setMuted(muted);
        transitionTargetVideoView.setLoop(true);
        pendingVideoSwitchTargetView = transitionTargetVideoView;

        if (isPreparedVideoView(transitionTargetVideoView, normalizedPath)) {
            startPreparedTransitionVideo(transitionTargetVideoView, previousVideoView, overlayToFade, nextType, nextPath, nextMuted, preloadTarget, true);
            return;
        }

        transitionTargetVideoView.setTag(null);
        transitionTargetVideoView.setMediaInfoListener(null);
        transitionTargetVideoView.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
            @Override
            public void onPrepared(MediaPlayer mp) {
                transitionTargetVideoView.setTag(normalizedPath);
                startPreparedTransitionVideo(transitionTargetVideoView, previousVideoView, overlayToFade, nextType, nextPath, nextMuted, preloadTarget, false);
            }
        });
        transitionTargetVideoView.setVideoPath(normalizedPath);
    }

    private void startPreparedTransitionVideo(final TurtleVideoView targetVideoView,
                                              final TurtleVideoView previousVideoView,
                                              final ImageView overlayToFade,
                                              final CONTENT_TYPE nextType,
                                              final String nextPath,
                                              final boolean nextMuted,
                                              final ImageView preloadTarget,
                                              final boolean readyForImmediateSwap) {
        if (targetVideoView == null) {
            return;
        }
        cancelVideoPresentationFallback();
        final boolean[] completed = new boolean[]{false};
        final long[] preparedVideoStartedAtUptimeMs = new long[]{0L};
        final Runnable completeTransition = new Runnable() {
            @Override
            public void run() {
                if (completed[0]) {
                    return;
                }
                if (targetVideoView != pendingVideoSwitchTargetView) {
                    return;
                }
                completed[0] = true;
                runOrRegisterSynchronizedSwap(new Runnable() {
                    @Override
                    public void run() {
                        cancelVideoPresentationFallback();
                        if (previousVideoView != null && previousVideoView == completionSuppressedVideoView) {
                            completionSuppressedVideoView = null;
                        }
                        if (targetVideoView == pendingVideoSwitchTargetView) {
                            pendingVideoSwitchTargetView = null;
                        }
                        promoteVideoView(targetVideoView);
                        showVideoOnScreen(targetVideoView);
                        boolean preloadHandledByRecycledVideo = previousVideoView != null && nextType == CONTENT_TYPE.Video;
                        recyclePreviousVideoViewAfterSwap(previousVideoView, targetVideoView, nextType, nextPath, nextMuted);
                        finishVideoOverlayAndPreloadNext(overlayToFade,
                                nextType,
                                nextPath,
                                nextMuted,
                                preloadTarget,
                                preloadHandledByRecycledVideo);
                        startVideoPlayback(targetVideoView, readyForImmediateSwap, new Runnable() {
                            @Override
                            public void run() {
                                Log.i(CONTENT_TRACE_TAG, "video transition complete media=" + debugId()
                                        + " targetView=" + System.identityHashCode(targetVideoView)
                                        + " previousView=" + System.identityHashCode(previousVideoView)
                                        + " waitedVisibleFrame=" + readyForImmediateSwap
                                        + " positionMs=" + getSafeVideoPositionMs(targetVideoView));
                            }
                        });
                    }
                });
            }
        };

        targetVideoView.setMediaInfoListener(new MediaPlayer.OnInfoListener() {
            @Override
            public boolean onInfo(MediaPlayer mp, int what, int extra) {
                if (what == MediaPlayer.MEDIA_INFO_VIDEO_RENDERING_START) {
                    if (readyForImmediateSwap) {
                        long startedAt = preparedVideoStartedAtUptimeMs[0] > 0L
                                ? preparedVideoStartedAtUptimeMs[0]
                                : SystemClock.uptimeMillis();
                        runWhenPreparedVideoFrameReady(targetVideoView, startedAt, completeTransition);
                    } else {
                        completeTransition.run();
                    }
                }
                return false;
            }
        });
        if (readyForImmediateSwap) {
            stageVideoBehindPreviousFrame(targetVideoView, previousVideoView);
            markVideoPresentationStarted();
            preparedVideoStartedAtUptimeMs[0] = SystemClock.uptimeMillis();
            startVideoPlayback(targetVideoView, true, null);
            runWhenPreparedVideoFrameReady(targetVideoView, preparedVideoStartedAtUptimeMs[0], completeTransition);
            videoSwitchFallbackRunnable = completeTransition;
            playbackTimingHandler.postAtTime(
                    videoSwitchFallbackRunnable,
                    SystemClock.uptimeMillis() + PREPARED_VIDEO_SWAP_TIMEOUT_MS);
            return;
        }
        markVideoPresentationStarted();
        targetVideoView.start();
        videoSwitchFallbackRunnable = completeTransition;
        playbackTimingHandler.postAtTime(
                videoSwitchFallbackRunnable,
                SystemClock.uptimeMillis() + VIDEO_PRESENTATION_FALLBACK_MS);
    }

    private void startVideoPlayback(final TurtleVideoView targetVideoView,
                                    boolean waitForVisibleFrame,
                                    final Runnable afterStart) {
        if (targetVideoView == null) {
            if (afterStart != null) {
                afterStart.run();
            }
            return;
        }
        Runnable startRunnable = new Runnable() {
            @Override
            public void run() {
                try {
                    if (!targetVideoView.isPlaying()) {
                        targetVideoView.start();
                    }
                } catch (Exception ignored) {
                }
                if (afterStart != null) {
                    afterStart.run();
                }
            }
        };
        if (waitForVisibleFrame) {
            targetVideoView.postOnAnimation(startRunnable);
        } else {
            startRunnable.run();
        }
    }

    private void recyclePreviousVideoViewAfterSwap(TurtleVideoView previousVideoView,
                                                   TurtleVideoView activeVideoView,
                                                   CONTENT_TYPE nextType,
                                                   String nextPath,
                                                   boolean nextMuted) {
        if (previousVideoView == null || previousVideoView == activeVideoView) {
            return;
        }
        String normalizedNextPath = nextType == CONTENT_TYPE.Video ? normalizeLocalVideoPath(nextPath) : null;
        showStandbyVideoView(previousVideoView);
        final TurtleVideoView parkedVideoView = previousVideoView;
        final boolean shouldPrepareNextVideo = nextType == CONTENT_TYPE.Video
                && isPlayableLocalVideo(normalizedNextPath);
        final String parkedPath = normalizedNextPath;
        final boolean parkedMuted = nextMuted;
        final int configVersion = mediaConfigurationVersion;
        runAfterOffscreenFrame(parkedVideoView, new Runnable() {
            @Override
            public void run() {
                stopVideoPlayback(parkedVideoView);
                Log.i(CONTENT_TRACE_TAG, "previous video stopped after offscreen media=" + debugId()
                        + " view=" + System.identityHashCode(parkedVideoView));
                if (!shouldPrepareNextVideo || !isCurrentMediaConfiguration(configVersion)) {
                    return;
                }
                playbackTimingHandler.postAtTime(new Runnable() {
                    @Override
                    public void run() {
                        if (!isCurrentMediaConfiguration(configVersion)) {
                            return;
                        }
                        prepareStandbyVideoView(parkedVideoView, parkedPath, parkedMuted, configVersion);
                        Log.i(CONTENT_TRACE_TAG, "previous video loading after close media=" + debugId()
                                + " view=" + System.identityHashCode(parkedVideoView)
                                + " path=" + summarizePath(parkedPath));
                    }
                }, SystemClock.uptimeMillis() + 32L);
            }
        });
    }

    private void finishVideoOverlayAndPreloadNext(final ImageView overlayToFade,
                                                  final CONTENT_TYPE nextType,
                                                  final String nextPath,
                                                  final boolean nextMuted,
                                                  ImageView preloadTarget,
                                                  boolean skipVideoPreload) {
        if (overlayToFade != null && overlayToFade.getVisibility() == View.VISIBLE) {
            overlayToFade.bringToFront();
            overlayToFade.animate()
                    .alpha(0f)
                    .setDuration(IMAGE_CROSSFADE_DURATION_MS)
                    .withEndAction(new Runnable() {
                        @Override
                        public void run() {
                            hideAllImageOverlays();
                        }
                    })
                    .start();
        } else {
            hideAllImageOverlays();
        }

        if (nextType == CONTENT_TYPE.Image) {
            ImageView target = preloadTarget != null ? preloadTarget : getHiddenImageView(getVisibleImageView());
            if (target == null) {
                target = getHiddenImageView(null);
            }
            preloadNextImageIfNeeded(nextType, nextPath, target);
            return;
        }
        if (nextType == CONTENT_TYPE.Video) {
            if (skipVideoPreload) {
                return;
            }
            preloadNextVideoIfNeeded(nextType, nextPath, nextMuted, mediaConfigurationVersion);
        }
    }

    private String normalizeLocalVideoPath(String videoPath) {
        if (TextUtils.isEmpty(videoPath)) {
            return videoPath;
        }
        try {
            android.net.Uri parsed = android.net.Uri.parse(videoPath);
            if ("file".equalsIgnoreCase(parsed.getScheme()) && !TextUtils.isEmpty(parsed.getPath())) {
                return parsed.getPath();
            }
        } catch (Exception ignored) {
        }
        return videoPath;
    }

    private boolean isPlayableLocalVideo(String videoPath) {
        if (TextUtils.isEmpty(videoPath)) {
            return false;
        }
        if (!videoPath.startsWith("/")) {
            return true;
        }
        File file = new File(videoPath);
        if (!file.exists()) {
            Log.w(TAG, "isPlayableLocalVideo: file missing. path=" + videoPath);
            return false;
        }
        if (file.length() <= 0) {
            Log.w(TAG, "isPlayableLocalVideo: file empty. path=" + videoPath);
            return false;
        }
        return true;
    }

    private CONTENT_TYPE safeContentType(MediaDataModel model) {
        if (model == null || TextUtils.isEmpty(model.getType())) {
            return CONTENT_TYPE.Image;
        }
        try {
            return CONTENT_TYPE.valueOf(model.getType());
        } catch (Exception ignored) {
            return CONTENT_TYPE.Image;
        }
    }

    private void prepareInitialImage(final String filePath, final CONTENT_TYPE nextType, final String nextPath, final int configVersion) {
        restoreVisibleOutputs();
        final ImageView target = imgView1;
        final ImageView preloadTarget = imgView2;
        if (target == null) {
            initialPrepared = true;
            notifyPrepared();
            return;
        }
        target.animate().cancel();
        target.setAlpha(0f);
        target.setVisibility(View.VISIBLE);
        target.setTag(filePath);
        displayImageAtRenderSize(
                target,
                filePath,
                new SimpleImageLoadingListener() {
                    @Override
                    public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                        if (!isCurrentMediaConfiguration(configVersion)) {
                            return;
                        }
                        if (nextType == CONTENT_TYPE.Image && preloadTarget != null) {
                            preloadTarget.setAlpha(0f);
                            preloadTarget.setVisibility(View.VISIBLE);
                            preloadNextImageIfNeeded(nextType, nextPath, preloadTarget);
                        } else if (nextType == CONTENT_TYPE.Video) {
                            preloadNextVideoIfNeeded(nextType, nextPath, preparedNextMuted, configVersion);
                        }
                        initialPrepared = true;
                        notifyPrepared();
                    }

                    @Override
                    public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                        if (!isCurrentMediaConfiguration(configVersion)) {
                            return;
                        }
                        initialPrepared = true;
                        notifyPrepared();
                    }
                });
    }

    private void displayImageAtRenderSize(ImageView targetView, String filePath, SimpleImageLoadingListener listener) {
        if (targetView == null || TextUtils.isEmpty(filePath)) {
            return;
        }
        ImageLoader.getInstance().displayImage(
                LocalPathUtils.getUriStringFromAbsPath(filePath),
                new SafeImageViewAware(targetView, getTargetDecodeWidth(), getTargetDecodeHeight()),
                imgOpt,
                listener);
    }

    private int getTargetDecodeWidth() {
        if (contentRenderWidth > 0) {
            return contentRenderWidth;
        }
        int width = getWidth();
        if (width > 0) {
            return width;
        }
        width = getMeasuredWidth();
        if (width > 0) {
            return width;
        }
        return AndoWSignageApp.getDeviceWidth();
    }

    private int getTargetDecodeHeight() {
        if (contentRenderHeight > 0) {
            return contentRenderHeight;
        }
        int height = getHeight();
        if (height > 0) {
            return height;
        }
        height = getMeasuredHeight();
        if (height > 0) {
            return height;
        }
        return AndoWSignageApp.getDeviceHeight();
    }

    private void prepareInitialVideo(final String videoPath,
                                     final boolean muted,
                                     final CONTENT_TYPE nextType,
                                     final String nextPath,
                                     final int configVersion) {
        restoreVisibleOutputs();
        final String normalizedPath = normalizeLocalVideoPath(videoPath);
        if (!isPlayableLocalVideo(normalizedPath)) {
            initialPrepared = true;
            notifyPrepared();
            return;
        }

        videoView.setMediaInfoListener(null);
        videoView.setAlpha(0f);
        videoView.setVisibility(View.VISIBLE);
        videoView.setMuted(muted);
        videoView.setLoop(true);
        videoView.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
            @Override
            public void onPrepared(MediaPlayer mp) {
                if (!isCurrentMediaConfiguration(configVersion)) {
                    return;
                }
                try {
                    videoView.pause();
                    videoView.seekTo(1);
                } catch (Exception ignored) {
                }
                videoView.setTag(normalizedPath);
                if (nextType == CONTENT_TYPE.Image) {
                    preloadNextImageIfNeeded(nextType, nextPath, imgView1 == null ? null : imgView1 == getVisibleImageView() ? imgView2 : imgView1);
                } else if (nextType == CONTENT_TYPE.Video) {
                    preloadNextVideoIfNeeded(nextType, nextPath, preparedNextMuted, configVersion);
                }
                initialPrepared = true;
                notifyPrepared();
            }
        });
        videoView.setVideoPath(normalizedPath);
    }

    private void showPreparedImage() {
        restoreVisibleOutputs();
        hideAllImageOverlays();
        videoView.setMediaInfoListener(null);
        stopVideoPlayback(videoView);
        if (imgView1 != null) {
            imgView1.setAlpha(1f);
            imgView1.setVisibility(View.VISIBLE);
            imgView1.bringToFront();
        }
    }

    private void showPreparedVideoFrame() {
        restoreVisibleOutputs();
        hideAllImageOverlays();
        videoView.setMediaInfoListener(null);
        videoView.setVisibility(View.VISIBLE);
        videoView.setAlpha(1f);
        try {
            videoView.pause();
            videoView.seekTo(1);
        } catch (Exception ignored) {
        }

    }

    private void startPreparedVideoPlayback() {
        restoreVisibleOutputs();
        cancelVideoPresentationFallback();
        videoView.setMediaInfoListener(new MediaPlayer.OnInfoListener() {
            @Override
            public boolean onInfo(MediaPlayer mp, int what, int extra) {
                if (what == MediaPlayer.MEDIA_INFO_VIDEO_RENDERING_START) {
                    markVideoPresentationStarted();
                    notifyPlaybackReady();
                }
                return false;
            }
        });
        videoView.setLoop(true);
        markVideoPresentationStarted();
        startVideoPlayback(videoView, true, new Runnable() {
            @Override
            public void run() {
                schedulePlaybackReadyFallback();
                scheduleVideoPresentationFallback();
            }
        });
        if (preparedNextType == CONTENT_TYPE.Video) {
            preloadNextVideoIfNeeded(preparedNextType, preparedNextPath, preparedNextMuted, mediaConfigurationVersion);
        }
    }

    private void notifyPrepared() {
        PreparationCallback callback = pendingPreparationCallback;
        pendingPreparationCallback = null;
        if (callback != null) {
            callback.onPrepared(this);
        }
    }

    private void schedulePlaybackReadyFallback() {
        cancelPlaybackReadyFallback();
        playbackTimingHandler.postAtTime(
                playbackReadyFallbackRunnable,
                SystemClock.uptimeMillis() + PLAYBACK_READY_FALLBACK_MS);
    }

    private void notifyPlaybackReady() {
        if (playbackReadyNotified) {
            return;
        }
        playbackReadyNotified = true;
        cancelPlaybackReadyFallback();
        PlaybackReadyCallback callback = pendingPlaybackReadyCallback;
        pendingPlaybackReadyCallback = null;
        if (callback != null) {
            callback.onPlaybackReady(this);
        }
    }

    private void restoreVisibleOutputs() {
        setVisibility(View.VISIBLE);
        resetViewPosition(this);
        resetViewPosition(videoView);
        if (videoView2 != null && videoView2 != videoView && videoView2.getVisibility() == View.VISIBLE) {
            showStandbyVideoView(videoView2);
        }
        resetViewPosition(imgView1);
        resetViewPosition(imgView2);
    }

    private void resetViewPosition(View view) {
        if (view == null) {
            return;
        }
        view.setTranslationX(0f);
        view.setTranslationY(0f);
    }

    private List<MediaDataModel> copyMediaContents(List<MediaDataModel> contents) {
        return contents != null ? new ArrayList<>(contents) : new ArrayList<MediaDataModel>();
    }

    private String buildMediaConfigurationSignature(List<MediaDataModel> contents) {
        if (contents == null || contents.isEmpty()) {
            return "";
        }
        StringBuilder builder = new StringBuilder();
        for (MediaDataModel model : contents) {
            if (model == null) {
                builder.append("null;");
                continue;
            }
            builder.append(model.getType()).append('|')
                    .append(model.getFilePath()).append('|')
                    .append(model.getPlayTimeSec()).append('|')
                    .append(model.isMuted()).append(';');
        }
        return builder.toString();
    }

    private boolean isCurrentMediaConfiguration(int configVersion) {
        return configVersion == mediaConfigurationVersion;
    }

    public boolean shouldDelayLayoutTransition() {
        if (!hasConfiguredContents() || cdmList == null || cdmList.size() <= 1) {
            return false;
        }
        if (!currentContentBlocksLayoutSwitch) {
            return false;
        }
        if (currentContentStartedAtElapsedRealtimeMs <= 0L) {
            return false;
        }
        return getRemainingContentIntervalMs() > 0L;
    }

    public long getContentTransitionDeadlineAtElapsedRealtimeMs() {
        if (!hasConfiguredContents() || cdmList == null || cdmList.size() <= 1) {
            return 0L;
        }
        if (!currentContentBlocksLayoutSwitch || advancePending) {
            return 0L;
        }
        return currentContentDeadlineAtElapsedRealtimeMs;
    }

    public boolean isContentTransitionDue(long nowElapsedRealtimeMs, long frameWindowMs) {
        long deadline = getContentTransitionDeadlineAtElapsedRealtimeMs();
        return deadline > 0L && deadline <= nowElapsedRealtimeMs + Math.max(1L, frameWindowMs);
    }

    public void updateVideoLoopForLayoutTimer(long nowElapsedRealtimeMs, long frameWindowMs) {
        if (!hasConfiguredContents()
                || s_usedType != CONTENT_TYPE.Video
                || currentContentStartedAtElapsedRealtimeMs <= 0L
                || currentContentDeadlineAtElapsedRealtimeMs <= 0L
                || advancePending) {
            return;
        }
        TurtleVideoView currentVideoView = getCurrentOnScreenVideoView();
        if (currentVideoView == null) {
            return;
        }
        int videoDurationMs = currentVideoView.getDuration();
        if (videoDurationMs <= 0) {
            return;
        }
        long configuredPlayTimeMs = getConfiguredPlayTimeMs();
        if (configuredPlayTimeMs <= videoDurationMs) {
            return;
        }
        long remainingMs = Math.max(0L, currentContentDeadlineAtElapsedRealtimeMs - nowElapsedRealtimeMs);
        int currentPositionMs = Math.max(0, currentVideoView.getCurrentPosition());
        long videoRemainingMs = Math.max(1L, videoDurationMs - currentPositionMs);
        boolean shouldLoop = remainingMs > videoRemainingMs + Math.max(1L, frameWindowMs);
        if (currentVideoView.isLoop() != shouldLoop) {
            Log.i(CONTENT_TRACE_TAG, "video loop change media=" + debugId()
                    + " shouldLoop=" + shouldLoop
                    + " remainingMs=" + remainingMs
                    + " videoRemainingMs=" + videoRemainingMs
                    + " positionMs=" + currentPositionMs
                    + " durationMs=" + videoDurationMs);
            currentVideoView.setLoop(shouldLoop);
        }
    }

    public void advanceContentFromLayoutTimer(long boundaryAtElapsedRealtimeMs) {
        Log.i(CONTENT_TRACE_TAG, "timer advance request media=" + debugId()
                + " boundary=" + boundaryAtElapsedRealtimeMs
                + " state=" + getDebugContentState());
        requestContentAdvance(boundaryAtElapsedRealtimeMs);
    }

    public boolean isReadyForSynchronizedContentAdvance() {
        MediaDataModel nextContent = getNextAdvanceContentModel();
        if (nextContent == null) {
            return true;
        }
        CONTENT_TYPE nextType = safeContentType(nextContent);
        if (nextType == CONTENT_TYPE.Image) {
            String nextPath = nextContent.getFilePath();
            ImageView currentView = getVisibleImageView();
            if (findPreloadedImageView(nextPath, currentView) != null) {
                Log.i(CONTENT_TRACE_TAG, "sync ready image media=" + debugId()
                        + " next=" + summarizePath(nextPath));
                return true;
            }
            ImageView target = getHiddenImageView(currentView);
            preloadNextImageIfNeeded(CONTENT_TYPE.Image, nextPath, target);
            boolean readyAfterPreload = findPreloadedImageView(nextPath, currentView) != null;
            Log.i(CONTENT_TRACE_TAG, "sync ready image after preload media=" + debugId()
                    + " ready=" + readyAfterPreload
                    + " next=" + summarizePath(nextPath));
            return readyAfterPreload;
        }
        if (nextType != CONTENT_TYPE.Video) {
            return true;
        }
        String normalizedPath = normalizeLocalVideoPath(nextContent.getFilePath());
        if (!isPlayableLocalVideo(normalizedPath)) {
            return true;
        }
        TurtleVideoView currentVideoView = getCurrentOnScreenVideoView();
        if (findPreparedVideoView(normalizedPath, currentVideoView) != null) {
            Log.i(CONTENT_TRACE_TAG, "sync ready prepared media=" + debugId()
                    + " next=" + summarizePath(normalizedPath));
            return true;
        }
        if (isPreparingVideoView(videoView, normalizedPath)
                || isPreparingVideoView(videoView2, normalizedPath)) {
            Log.i(CONTENT_TRACE_TAG, "sync ready preparing media=" + debugId()
                    + " next=" + summarizePath(normalizedPath));
            return true;
        }
        preloadNextVideoIfNeeded(CONTENT_TYPE.Video,
                nextContent.getFilePath(),
                nextContent.isMuted(),
                mediaConfigurationVersion);
        boolean readyAfterPreload = findPreparedVideoView(normalizedPath, currentVideoView) != null;
        Log.i(CONTENT_TRACE_TAG, "sync ready after preload media=" + debugId()
                + " ready=" + readyAfterPreload
                + " next=" + summarizePath(normalizedPath));
        return readyAfterPreload;
    }

    public boolean willAdvanceToVideoContent() {
        MediaDataModel nextContent = getNextAdvanceContentModel();
        return nextContent != null && safeContentType(nextContent) == CONTENT_TYPE.Video;
    }

    public boolean willAdvanceToImageContent() {
        MediaDataModel nextContent = getNextAdvanceContentModel();
        return nextContent != null && safeContentType(nextContent) == CONTENT_TYPE.Image;
    }

    public boolean isPlaybackActiveForHeartbeat() {
        if (!hasConfiguredContents() || getVisibility() != View.VISIBLE) {
            return false;
        }
        if (currentContentStartedAtElapsedRealtimeMs > 0L) {
            return true;
        }
        if (preparedPlaybackStarted) {
            return true;
        }
        if (videoPresentationStarted) {
            return true;
        }
        if (videoView != null && videoView.getVisibility() == View.VISIBLE) {
            try {
                if (videoView.isPlaying()) {
                    return true;
                }
            } catch (Exception ignored) {
            }
        }
        if (videoView2 != null && videoView2.getVisibility() == View.VISIBLE) {
            try {
                if (videoView2.isPlaying()) {
                    return true;
                }
            } catch (Exception ignored) {
            }
        }
        return false;
    }

    private void requestContentAdvance() {
        requestContentAdvance(SystemClock.elapsedRealtime());
    }

    private void requestContentAdvance(long boundaryAtElapsedRealtimeMs) {
        if (advancePending) {
            Log.i(CONTENT_TRACE_TAG, "advance ignored pending media=" + debugId()
                    + " boundary=" + boundaryAtElapsedRealtimeMs
                    + " state=" + getDebugContentState());
            return;
        }
        advancePending = true;
        cancelContentAdvanceTimer();
        Log.i(CONTENT_TRACE_TAG, "advance accepted media=" + debugId()
                + " boundary=" + boundaryAtElapsedRealtimeMs
                + " stateBeforeBoundary=" + getDebugContentState());
        markContentBoundaryReached(boundaryAtElapsedRealtimeMs);
        if (AndoWSignage.act != null) {
            AndoWSignage.act.onMediaContentComplete();
        }
        mPopContentRunnable.run();
    }

    private void prepareForNewContentWindow(boolean videoContent) {
        cancelContentAdvanceTimer();
        cancelVideoPresentationFallback();
        advancePending = false;
        videoPresentationStarted = !videoContent;
        currentContentBlocksLayoutSwitch = false;
    }

    private void markCurrentContentPresented() {
        long durationMs = getConfiguredPlayTimeMs();
        currentContentStartedAtElapsedRealtimeMs = resolveContentTimingStartAtElapsedRealtimeMs();
        lastContentBoundaryAtElapsedRealtimeMs = 0L;
        currentContentDeadlineAtElapsedRealtimeMs = currentContentStartedAtElapsedRealtimeMs + durationMs;
        currentContentBlocksLayoutSwitch = shouldBlockLayoutSwitchForCurrentContent();
        Log.i(CONTENT_TRACE_TAG, "content presented media=" + debugId()
                + " type=" + s_usedType
                + " playTimeMs=" + durationMs
                + " start=" + currentContentStartedAtElapsedRealtimeMs
                + " deadline=" + currentContentDeadlineAtElapsedRealtimeMs
                + " contentIdx=" + contentIdx
                + " nextIdx=" + nextAdvanceContentIndex
                + " blocks=" + currentContentBlocksLayoutSwitch);
        if (!currentContentBlocksLayoutSwitch) {
            cancelContentAdvanceTimer();
            return;
        }
        scheduleContentAdvanceTimer(durationMs);
    }

    private void notifyContentActuallyPresented(View anchorView, final Runnable afterPresentation) {
        final View targetView = anchorView != null ? anchorView : this;
        Runnable presentationRunnable = new Runnable() {
            @Override
            public void run() {
                markCurrentContentPresented();
                if (afterPresentation != null) {
                    afterPresentation.run();
                }
            }
        };
        if (targetView != null) {
            targetView.postOnAnimation(presentationRunnable);
        } else {
            presentationRunnable.run();
        }
    }

    private void markVideoPresentationStarted() {
        if (videoPresentationStarted) {
            return;
        }
        videoPresentationStarted = true;
        cancelVideoPresentationFallback();
        markCurrentContentPresented();
    }

    private void handleVideoCompletion() {
        if (!advancePending && currentContentBlocksLayoutSwitch && !videoPresentationStarted) {
            markVideoPresentationStarted();
        }
        if (currentContentBlocksLayoutSwitch && !advancePending) {
            long nowElapsedRealtimeMs = SystemClock.elapsedRealtime();
            if (currentContentDeadlineAtElapsedRealtimeMs > nowElapsedRealtimeMs + 16L) {
                return;
            }
            requestContentAdvance();
            return;
        }
        signalAdvanceReady();
    }

    private void signalAdvanceReady() {
        LoopPlay currentLoop = mLoopPlay;
        if (currentLoop == null) {
            Log.i(CONTENT_TRACE_TAG, "signal advance skipped no loop media=" + debugId());
            return;
        }
        synchronized (currentLoop) {
            advanceSignalCount++;
            Log.i(CONTENT_TRACE_TAG, "signal advance media=" + debugId()
                    + " total=" + advanceSignalCount
                    + " consumed=" + consumedAdvanceSignalCount);
            currentLoop.notifyAll();
        }
    }

    private void scheduleContentAdvanceTimer(long delayMs) {
        cancelContentAdvanceTimer();
    }

    private void cancelContentAdvanceTimer() {
        playbackTimingHandler.removeCallbacks(contentAdvanceRunnable);
    }

    private void scheduleVideoPresentationFallback() {
        cancelVideoPresentationFallback();
        playbackTimingHandler.postAtTime(
                videoPresentationFallbackRunnable,
                SystemClock.uptimeMillis() + VIDEO_PRESENTATION_FALLBACK_MS);
    }

    private void cancelVideoPresentationFallback() {
        playbackTimingHandler.removeCallbacks(videoPresentationFallbackRunnable);
        if (videoSwitchFallbackRunnable != null) {
            playbackTimingHandler.removeCallbacks(videoSwitchFallbackRunnable);
            videoSwitchFallbackRunnable = null;
        }
    }

    private void cancelPlaybackReadyFallback() {
        playbackTimingHandler.removeCallbacks(playbackReadyFallbackRunnable);
    }

    private void resetContentTimingState() {
        cancelContentAdvanceTimer();
        cancelVideoPresentationFallback();
        cancelPlaybackReadyFallback();
        advancePending = false;
        completionSuppressedVideoView = null;
        pendingVideoSwitchTargetView = null;
        advanceSignalCount = 0;
        consumedAdvanceSignalCount = 0;
        videoPresentationStarted = false;
        currentContentStartedAtElapsedRealtimeMs = 0L;
        currentContentDeadlineAtElapsedRealtimeMs = 0L;
        currentContentBlocksLayoutSwitch = false;
        lastContentBoundaryAtElapsedRealtimeMs = 0L;
        layoutContentClockBaseAtElapsedRealtimeMs = 0L;
    }

    private void markContentBoundaryReached(long boundaryAtElapsedRealtimeMs) {
        lastContentBoundaryAtElapsedRealtimeMs = Math.max(1L, boundaryAtElapsedRealtimeMs);
        currentContentStartedAtElapsedRealtimeMs = 0L;
        currentContentDeadlineAtElapsedRealtimeMs = 0L;
        currentContentBlocksLayoutSwitch = false;
        videoPresentationStarted = false;
    }

    private long resolveContentTimingStartAtElapsedRealtimeMs() {
        if (lastContentBoundaryAtElapsedRealtimeMs > 0L) {
            return lastContentBoundaryAtElapsedRealtimeMs;
        }
        if (layoutContentClockBaseAtElapsedRealtimeMs > 0L) {
            return layoutContentClockBaseAtElapsedRealtimeMs;
        }
        return SystemClock.elapsedRealtime();
    }

    private boolean shouldBlockLayoutSwitchForCurrentContent() {
        return cdmList != null && cdmList.size() > 1;
    }

    private MediaDataModel getNextAdvanceContentModel() {
        if (cdmList == null || cdmList.isEmpty()) {
            return null;
        }
        synchronized (cdmList) {
            if (cdmList.isEmpty()) {
                return null;
            }
            int index = nextAdvanceContentIndex;
            if (index < 0 || index >= cdmList.size()) {
                index = 0;
            }
            return cdmList.get(index);
        }
    }

    private long getRemainingContentIntervalMs() {
        if (currentContentStartedAtElapsedRealtimeMs <= 0L) {
            return 0L;
        }
        long contentElapsedMs = Math.max(0L, SystemClock.elapsedRealtime() - currentContentStartedAtElapsedRealtimeMs);
        return Math.max(0L, getConfiguredPlayTimeMs() - contentElapsedMs);
    }

    public String getDebugContentState() {
        MediaDataModel nextContent = getNextAdvanceContentModel();
        return "media=" + debugId()
                + " used=" + s_usedType
                + " contentIdx=" + contentIdx
                + " nextIdx=" + nextAdvanceContentIndex
                + " nextType=" + (nextContent == null ? "null" : safeContentType(nextContent))
                + " playTimeSec=" + playTime
                + " started=" + currentContentStartedAtElapsedRealtimeMs
                + " deadline=" + currentContentDeadlineAtElapsedRealtimeMs
                + " advancePending=" + advancePending
                + " blocks=" + currentContentBlocksLayoutSwitch
                + " videoStarted=" + videoPresentationStarted;
    }

    private String debugId() {
        return Integer.toHexString(System.identityHashCode(this));
    }

    private String summarizePath(String path) {
        if (TextUtils.isEmpty(path)) {
            return "";
        }
        int slashIndex = path.lastIndexOf('/');
        return slashIndex >= 0 && slashIndex + 1 < path.length()
                ? path.substring(slashIndex + 1)
                : path;
    }

    private long getConfiguredPlayTimeMs() {
        return Math.max(1L, playTime) * 1000L;
    }

}

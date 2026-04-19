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
import java.util.List;
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

    public interface PreparationCallback {
        void onPrepared(MediaView view);
    }

    public interface PlaybackReadyCallback {
        void onPlaybackReady(MediaView view);
    }

    List<MediaDataModel> cdmList;

    long tick = 0;
    long playTime = 1000;
    CONTENT_TYPE s_usedType = CONTENT_TYPE.Image;

    ImageView imgView1;
    ImageView imgView2;
    TurtleVideoView videoView;
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
    private PreparationCallback pendingPreparationCallback;
    private PlaybackReadyCallback pendingPlaybackReadyCallback;
    private boolean playbackReadyNotified = false;
    private static final long PLAYBACK_READY_FALLBACK_MS = 300L;
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
    private boolean currentContentBlocksLayoutSwitch = false;
    private int contentRenderWidth;
    private int contentRenderHeight;

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

        if (AndoWSignageApp.KEEP_ASPECT_RATIO) {
            imgView1.setScaleType(ScaleType.FIT_CENTER);
            imgView2.setScaleType(ScaleType.FIT_CENTER);
            videoView.setKeepAspectRatio(true);
        } else {
            imgView1.setScaleType(ScaleType.FIT_XY);
            imgView2.setScaleType(ScaleType.FIT_XY);
            videoView.setKeepAspectRatio(false);
        }

        addView(videoView, params);
        addView(imgView1, params);
        addView(imgView2, params);
        restoreVisibleOutputs();
    }

    private void setViewEvents() {

        videoView.setOnCompletionListener(new OnCompletionListener() {

            @Override
            public void onCompletion(MediaPlayer mp) {
                handleVideoCompletion();
            }
        });

        videoView.setOnErrorListener(new OnErrorListener() {

            @Override
            public boolean onError(MediaPlayer arg0, int arg1, int arg2) {
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
            if (preparedInitialType == CONTENT_TYPE.Video) {
                videoView.setLoop(true);
            }
            return;
        }

        contentIdx = 1;
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

        CONTENT_TYPE usedType;

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
                synchronized (cdmList) {

                    if (cdmList.size() < 1) {
                        try {
                            Thread.sleep(250);
                        } catch (InterruptedException e) {
                            e.printStackTrace();
                        }
                        continue;
                    }

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

                    publishProgress(new String[]{
                            cdmList.get(contentIdx).getType(),
                            cdmList.get(contentIdx).getFilePath(),
                            String.valueOf(cdmList.get(contentIdx).isMuted()),
                            cdmList.get(j).getType(),
                            cdmList.get(j).getFilePath()
                    });

                    if (!waitForAdvanceSignal()) {
                        break;
                    }

                    if (s_isFirst)
                        s_isFirst = false;

                    if (manual) {
                        s_isFirst = true;
                        manual = false;
                    }

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
                                releaseUsedResources(usedType, type1, deferImageHide, false);
                                prepareForNewContentWindow(true);
                                showVideoWithImageFade(contentData[1], muted1, type2, contentData[4]);
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

    private void showImageWithCrossfade(final String filePath, boolean immediate, final CONTENT_TYPE nextType, final String nextPath, boolean fadeOverVideo, final Runnable endAction) {
        restoreVisibleOutputs();
        final ImageView currentView = getVisibleImageView();
        final ImageView preloadedView = findPreloadedImageView(filePath, currentView);
        final ImageView nextView = preloadedView != null ? preloadedView : getHiddenImageView(currentView);
        final String uri = LocalPathUtils.getUriStringFromAbsPath(filePath);

        if (nextView == null) {
            return;
        }

        nextView.animate().cancel();
        nextView.setVisibility(View.GONE);

        boolean shouldImmediate = immediate || (currentView == null && !fadeOverVideo);

        boolean alreadyLoaded = isPreloadedImageView(nextView, filePath);

        if (alreadyLoaded) {
            if (shouldImmediate) {
                nextView.setAlpha(1f);
                nextView.setVisibility(View.VISIBLE);
                if (currentView != null) {
                    currentView.setVisibility(View.GONE);
                }
                notifyContentActuallyPresented(nextView, new Runnable() {
                    @Override
                    public void run() {
                        preloadNextImageIfNeeded(nextType, nextPath, currentView);
                        if (endAction != null) {
                            endAction.run();
                        }
                    }
                });
                return;
            }

            if (currentView == null && fadeOverVideo) {
                nextView.setAlpha(0f);
                nextView.setVisibility(View.VISIBLE);
                crossfadeImages(null, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
                    @Override
                    public void run() {
                        notifyContentActuallyPresented(nextView, new Runnable() {
                            @Override
                            public void run() {
                                preloadNextImageIfNeeded(nextType, nextPath, currentView);
                                if (endAction != null) {
                                    endAction.run();
                                }
                            }
                        });
                    }
                });
                return;
            }

            nextView.post(new Runnable() {
                @Override
                public void run() {
                    nextView.setAlpha(0f);
                    nextView.setVisibility(View.VISIBLE);
                    crossfadeImages(currentView, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
                        @Override
                        public void run() {
                            notifyContentActuallyPresented(nextView, new Runnable() {
                                @Override
                                public void run() {
                                    preloadNextImageIfNeeded(nextType, nextPath, currentView);
                                    if (endAction != null) {
                                        endAction.run();
                                    }
                                }
                            });
                        }
                    });
                }
            });
            return;
        }

        if (shouldImmediate) {
            nextView.setTag(filePath);
            displayImageAtRenderSize(nextView, filePath, new SimpleImageLoadingListener() {
                @Override
            public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                nextView.setAlpha(1f);
                nextView.setVisibility(View.VISIBLE);
                if (currentView != null) {
                    currentView.setVisibility(View.GONE);
                }
                notifyContentActuallyPresented(nextView, new Runnable() {
                    @Override
                    public void run() {
                        preloadNextImageIfNeeded(nextType, nextPath, currentView);
                        if (endAction != null) {
                            endAction.run();
                        }
                    }
                });
            }

            @Override
            public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                nextView.setAlpha(1f);
                    nextView.setVisibility(View.VISIBLE);
                if (currentView != null) {
                    currentView.setVisibility(View.GONE);
                    currentView.setAlpha(1f);
                }
                notifyContentActuallyPresented(nextView, new Runnable() {
                    @Override
                    public void run() {
                        preloadNextImageIfNeeded(nextType, nextPath, currentView);
                        if (endAction != null) {
                            endAction.run();
                        }
                    }
                });
            }
        });
        return;
        }

        nextView.setTag(filePath);
        displayImageAtRenderSize(nextView, filePath, new SimpleImageLoadingListener() {
            @Override
            public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                nextView.post(new Runnable() {
                    @Override
                    public void run() {
                        nextView.setAlpha(0f);
                        nextView.setVisibility(View.VISIBLE);
                        crossfadeImages(currentView, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
                            @Override
                            public void run() {
                                notifyContentActuallyPresented(nextView, new Runnable() {
                                    @Override
                                    public void run() {
                                        preloadNextImageIfNeeded(nextType, nextPath, currentView);
                                        if (endAction != null) {
                                            endAction.run();
                                        }
                                    }
                                });
                            }
                        });
                    }
                });
            }

            @Override
            public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                nextView.setAlpha(1f);
                nextView.setVisibility(View.VISIBLE);
                if (currentView != null) {
                    currentView.setVisibility(View.GONE);
                    currentView.setAlpha(1f);
                }
                notifyContentActuallyPresented(nextView, new Runnable() {
                    @Override
                    public void run() {
                        preloadNextImageIfNeeded(nextType, nextPath, currentView);
                        if (endAction != null) {
                            endAction.run();
                        }
                    }
                });
            }
        });
    }

    private void crossfadeImages(final ImageView fromView, final ImageView toView, long durationMs, final Runnable endAction) {
        if (toView == null) {
            if (endAction != null) {
                endAction.run();
            }
            return;
        }

        toView.bringToFront();
        toView.setVisibility(View.VISIBLE);
        toView.setAlpha(0f);

        if (fromView == null || fromView.getVisibility() != View.VISIBLE) {
            toView.animate()
                    .alpha(1f)
                    .setDuration(durationMs)
                    .withEndAction(endAction)
                    .start();
            return;
        }

        fromView.setAlpha(1f);
        fromView.animate().cancel();
        toView.animate().cancel();

        fromView.animate()
                .alpha(0f)
                .setDuration(durationMs)
                .withEndAction(new Runnable() {
                    @Override
                    public void run() {
                        fromView.setVisibility(View.GONE);
                        fromView.setAlpha(1f);
                    }
                })
                .start();

        toView.animate()
                .alpha(1f)
                .setDuration(durationMs)
                .withEndAction(endAction)
                .start();
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
        try {
            cancelVideoPresentationFallback();
            videoView.setMediaInfoListener(null);
            videoView.stopPlayback();
        } catch (Exception e) {
        } finally {
            videoView.setVisibility(View.GONE);
            resetViewPosition(videoView);
        }
    }

    private void pauseVideoPlayback() {
        if (videoView == null) {
            return;
        }
        try {
            cancelVideoPresentationFallback();
            videoView.setMediaInfoListener(null);
            if (videoView.isPlaying()) {
                videoView.pause();
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

    private void showVideoWithImageFade(final String videoPath, final boolean muted, final CONTENT_TYPE nextType, final String nextPath) {
        restoreVisibleOutputs();
        String normalizedPath = normalizeLocalVideoPath(videoPath);
        if (!isPlayableLocalVideo(normalizedPath)) {
            Log.w(TAG, "showVideoWithImageFade: invalid video file. path=" + normalizedPath);
            hideAllImageOverlays();
            stopVideoPlayback();
            popContent();
            return;
        }

        final ImageView overlay = getVisibleImageView();
        final boolean useFakeOverlay = overlay == null;
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

        cancelVideoPresentationFallback();
        videoView.setVisibility(View.VISIBLE);
        videoView.setMuted(muted);
        videoView.setMediaInfoListener(new MediaPlayer.OnInfoListener() {
            @Override
            public boolean onInfo(MediaPlayer mp, int what, int extra) {
                if (what == MediaPlayer.MEDIA_INFO_VIDEO_RENDERING_START) {
                    markVideoPresentationStarted();
                }
                return false;
            }
        });
        videoView.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
            @Override
            public void onPrepared(MediaPlayer mp) {
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
                videoView.setLoop(true);
            }
        });

        videoView.setVideoPath(normalizedPath);
        videoView.start();
        scheduleVideoPresentationFallback();

        if (nextType == CONTENT_TYPE.Image) {
            ImageView target = overlay != null ? getHiddenImageView(overlay) : preloadTarget;
            if (target == null) {
                target = getHiddenImageView(null);
            }
            preloadNextImageIfNeeded(nextType, nextPath, target);
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
        String normalizedPath = normalizeLocalVideoPath(videoPath);
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
                if (nextType == CONTENT_TYPE.Image) {
                    preloadNextImageIfNeeded(nextType, nextPath, imgView1 == null ? null : imgView1 == getVisibleImageView() ? imgView2 : imgView1);
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
        stopVideoPlayback();
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
        videoView.start();
        schedulePlaybackReadyFallback();
        scheduleVideoPresentationFallback();
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
        return false;
    }

    private void requestContentAdvance() {
        if (advancePending) {
            return;
        }
        advancePending = true;
        cancelContentAdvanceTimer();
        markContentBoundaryReached();
        boolean consumedByLayoutTransition = false;
        if (AndoWSignage.act != null) {
            consumedByLayoutTransition = AndoWSignage.act.onMediaContentComplete();
        }
        if (!consumedByLayoutTransition) {
            mPopContentRunnable.run();
        }
    }

    private void prepareForNewContentWindow(boolean videoContent) {
        cancelContentAdvanceTimer();
        cancelVideoPresentationFallback();
        advancePending = false;
        videoPresentationStarted = !videoContent;
        currentContentBlocksLayoutSwitch = false;
        lastContentBoundaryAtElapsedRealtimeMs = 0L;
    }

    private void markCurrentContentPresented() {
        long durationMs = getConfiguredPlayTimeMs();
        currentContentStartedAtElapsedRealtimeMs = SystemClock.elapsedRealtime();
        currentContentDeadlineAtElapsedRealtimeMs = currentContentStartedAtElapsedRealtimeMs + durationMs;
        currentContentBlocksLayoutSwitch = shouldBlockLayoutSwitchForCurrentContent();
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
            requestContentAdvance();
            return;
        }
        signalAdvanceReady();
    }

    private void signalAdvanceReady() {
        LoopPlay currentLoop = mLoopPlay;
        if (currentLoop == null) {
            return;
        }
        synchronized (currentLoop) {
            advanceSignalCount++;
            currentLoop.notifyAll();
        }
    }

    private void scheduleContentAdvanceTimer(long delayMs) {
        cancelContentAdvanceTimer();
        playbackTimingHandler.postAtTime(
                contentAdvanceRunnable,
                SystemClock.uptimeMillis() + Math.max(1L, delayMs));
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
    }

    private void cancelPlaybackReadyFallback() {
        playbackTimingHandler.removeCallbacks(playbackReadyFallbackRunnable);
    }

    private void resetContentTimingState() {
        cancelContentAdvanceTimer();
        cancelVideoPresentationFallback();
        cancelPlaybackReadyFallback();
        advancePending = false;
        advanceSignalCount = 0;
        consumedAdvanceSignalCount = 0;
        videoPresentationStarted = false;
        currentContentStartedAtElapsedRealtimeMs = 0L;
        currentContentDeadlineAtElapsedRealtimeMs = 0L;
        currentContentBlocksLayoutSwitch = false;
        lastContentBoundaryAtElapsedRealtimeMs = 0L;
    }

    private void markContentBoundaryReached() {
        lastContentBoundaryAtElapsedRealtimeMs = SystemClock.elapsedRealtime();
        currentContentStartedAtElapsedRealtimeMs = 0L;
        currentContentDeadlineAtElapsedRealtimeMs = 0L;
        currentContentBlocksLayoutSwitch = false;
        videoPresentationStarted = false;
    }

    private boolean shouldBlockLayoutSwitchForCurrentContent() {
        return cdmList != null && cdmList.size() > 1;
    }

    private long getRemainingContentIntervalMs() {
        if (currentContentStartedAtElapsedRealtimeMs <= 0L) {
            return 0L;
        }
        long contentElapsedMs = Math.max(0L, SystemClock.elapsedRealtime() - currentContentStartedAtElapsedRealtimeMs);
        return Math.max(0L, getConfiguredPlayTimeMs() - contentElapsedMs);
    }

    private long getConfiguredPlayTimeMs() {
        return Math.max(1L, playTime) * 1000L;
    }

}

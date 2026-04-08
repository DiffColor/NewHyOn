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
    TurtleWebView webView;

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
            notifyPlaybackReady();
        }
    };

    DisplayImageOptions imgOpt;
    private static final ExecutorService loopExecutor = Executors.newCachedThreadPool();

    public MediaView(Activity act, Context context, int width, int height, List<MediaDataModel> cdmList) {
        super(context);

        this.cdmList = cdmList;
        ctx = context;
        this.act = act;

        setMinimumWidth(width);
        setMinimumHeight(height);

        initChildViews();
        setViewEvents();
        setOtherSettings();

        mLoopPlay = new LoopPlay();
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

        webView = new TurtleWebView(ctx, getMinimumWidth(), getMinimumHeight());
        webView.setVisibility(View.GONE);

        addView(videoView, params);
        addView(imgView1, params);
        addView(imgView2, params);
        addView(webView, params);
        restoreVisibleOutputs();
    }

    private void setViewEvents() {

        videoView.setOnCompletionListener(new OnCompletionListener() {

            @Override
            public void onCompletion(MediaPlayer mp) {
                synchronized (mLoopPlay) {
                    mLoopPlay.notify();
                }
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
                synchronized (mLoopPlay) {
                    mLoopPlay.notify();
                }
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
        preparedContentShown = false;
        preparedPlaybackStarted = false;
        waitingForPreparedAdvance = false;
        initialPrepared = false;
        pendingPlaybackReadyCallback = null;
        playbackReadyNotified = false;
        removeCallbacks(playbackReadyFallbackRunnable);
        s_isFirst = true;
        mLoopPlay = new LoopPlay();
        mLoopPlay.executeOnExecutor(loopExecutor);
    }

    public void prepareInitialContent(PreparationCallback callback) {
        pendingPreparationCallback = callback;
        initialPrepared = false;
        preparedContentShown = false;
        preparedPlaybackStarted = false;
        waitingForPreparedAdvance = false;
        pendingPlaybackReadyCallback = null;
        playbackReadyNotified = false;
        removeCallbacks(playbackReadyFallbackRunnable);
        tick = 0;
        contentIdx = 0;
        s_isFirst = true;

        if (cdmList == null || cdmList.isEmpty()) {
            notifyPrepared();
            return;
        }

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
                prepareInitialImage(preparedInitialPath, preparedNextType, preparedNextPath);
                break;

            case Video:
                prepareInitialVideo(preparedInitialPath, preparedInitialMuted, preparedNextType, preparedNextPath);
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
        removeCallbacks(playbackReadyFallbackRunnable);
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
            notifyPlaybackReady();
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
        setVisibility(View.VISIBLE);
        resetViewPosition(this);
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
            if (videoView.isPlaying()) {
                videoView.stopPlayback();
            }
        } catch (Exception e) {

        } finally {
            mStopTaskRunnable.run();
            mPopContentRunnable.run();
            initialPrepared = false;
            preparedContentShown = false;
            preparedPlaybackStarted = false;
            waitingForPreparedAdvance = false;
            pendingPreparationCallback = null;
            pendingPlaybackReadyCallback = null;
            playbackReadyNotified = false;
            removeCallbacks(playbackReadyFallbackRunnable);
        }
    }

    public void count() {
        ++tick;

        if (this.cdmList == null || this.cdmList.isEmpty()) {
            tick = 0;
            return;
        }

        if (this.cdmList.size() == 1) {
            if (s_usedType == CONTENT_TYPE.Video) {
                videoView.setLoop(true);
            }
            tick = 0;
            return;
        }

        if (s_usedType == CONTENT_TYPE.Video) {
            int duration = videoView.getDuration();
            if (duration > 0 && playTime * 1000 >= duration && duration - (tick * 1000) <= 1000) {
                videoView.setLoop(false);
                tick = 0;
                return;
            }
        }

        if (tick >= playTime) {
            tick = 0;
            popContent();
        }
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
        mPopContentRunnable.run();
        if (AndoWSignage.act != null) {
            AndoWSignage.act.onMediaContentComplete();
        }
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
                try {
                    synchronized (mLoopPlay) {
                        mLoopPlay.wait();
                    }
                } catch (Exception ignored) {
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

                    try {
                        synchronized (mLoopPlay) {
                            mLoopPlay.wait();
                        }
                    } catch (Exception e) {
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
                                showVideoWithImageFade(contentData[1], muted1, type2, contentData[4]);
                                break;

                            case Flash:

                                releaseUsedResources(usedType, type1, false, false);

                                String html = "<object width=\"550\" height=\"400\"> <param name=\"movie\" value=\"file://" + contentData[1] + "\"> <embed src=\"file://" + contentData[1] + "\" width=\"550\" height=\"400\"> </embed> </object>";
                                String mimeType = "text/html";
                                String encoding = "utf-8";

                                webView.loadDataWithBaseURL("null", html, mimeType, encoding, "");

                                webView.setVisibility(View.VISIBLE);
                                webView.setBackgroundColor(Color.BLACK);

                                if (usedType == CONTENT_TYPE.Image) {
                                    imgView1.setVisibility(View.GONE);
                                    imgView2.setVisibility(View.GONE);
                                }
                                break;

                            case WebSiteURL:
                                releaseUsedResources(usedType, type1, false, false);

                                webView.loadUrl(contentData[1]);

                                webView.setBackgroundColor(Color.WHITE);
                                webView.setVisibility(View.VISIBLE);

                                if (usedType == CONTENT_TYPE.Image) {
                                    imgView1.setVisibility(View.GONE);
                                    imgView2.setVisibility(View.GONE);
                                }
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
                            webView.releaseWebView();
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
                preloadNextImageIfNeeded(nextType, nextPath, currentView);
                if (endAction != null) {
                    endAction.run();
                }
                return;
            }

            if (currentView == null && fadeOverVideo) {
                nextView.setAlpha(0f);
                nextView.setVisibility(View.VISIBLE);
                crossfadeImages(null, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
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

            nextView.post(new Runnable() {
                @Override
                public void run() {
                    nextView.setAlpha(0f);
                    nextView.setVisibility(View.VISIBLE);
                    crossfadeImages(currentView, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
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

        if (shouldImmediate) {
            nextView.setTag(filePath);
            ImageLoader.getInstance().displayImage(uri, new SafeImageViewAware(nextView), imgOpt, new SimpleImageLoadingListener() {
                @Override
                public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                    nextView.setAlpha(1f);
                    nextView.setVisibility(View.VISIBLE);
                    if (currentView != null) {
                        currentView.setVisibility(View.GONE);
                    }
                    preloadNextImageIfNeeded(nextType, nextPath, currentView);
                    if (endAction != null) {
                        endAction.run();
                    }
                }

                @Override
                public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                    nextView.setAlpha(1f);
                    nextView.setVisibility(View.VISIBLE);
                    if (currentView != null) {
                        currentView.setVisibility(View.GONE);
                        currentView.setAlpha(1f);
                    }
                    preloadNextImageIfNeeded(nextType, nextPath, currentView);
                    if (endAction != null) {
                        endAction.run();
                    }
                }
            });
            return;
        }

        nextView.setTag(filePath);
        ImageLoader.getInstance().displayImage(uri, new SafeImageViewAware(nextView), imgOpt, new SimpleImageLoadingListener() {
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
                                preloadNextImageIfNeeded(nextType, nextPath, currentView);
                                if (endAction != null) {
                                    endAction.run();
                                }
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
                preloadNextImageIfNeeded(nextType, nextPath, currentView);
                if (endAction != null) {
                    endAction.run();
                }
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
        ImageLoader.getInstance().displayImage(LocalPathUtils.getUriStringFromAbsPath(nextPath), new SafeImageViewAware(targetView), imgOpt);
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
            videoView.setMediaInfoListener(null);
            videoView.stopPlayback();
        } catch (Exception e) {
        } finally {
            videoView.setVisibility(View.GONE);
            resetViewPosition(videoView);
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

        videoView.setVisibility(View.VISIBLE);
        videoView.setMuted(muted);
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

    private void prepareInitialImage(final String filePath, final CONTENT_TYPE nextType, final String nextPath) {
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
        ImageLoader.getInstance().displayImage(
                LocalPathUtils.getUriStringFromAbsPath(filePath),
                new SafeImageViewAware(target),
                imgOpt,
                new SimpleImageLoadingListener() {
                    @Override
                    public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
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
                        initialPrepared = true;
                        notifyPrepared();
                    }
                });
    }

    private void prepareInitialVideo(final String videoPath,
                                     final boolean muted,
                                     final CONTENT_TYPE nextType,
                                     final String nextPath) {
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
        videoView.setLoop(cdmList == null || cdmList.size() <= 1);
        videoView.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
            @Override
            public void onPrepared(MediaPlayer mp) {
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
        videoView.start();
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
        videoView.setMediaInfoListener(new MediaPlayer.OnInfoListener() {
            @Override
            public boolean onInfo(MediaPlayer mp, int what, int extra) {
                if (what == MediaPlayer.MEDIA_INFO_VIDEO_RENDERING_START) {
                    notifyPlaybackReady();
                }
                return false;
            }
        });
        videoView.start();
        schedulePlaybackReadyFallback();
    }

    private void notifyPrepared() {
        PreparationCallback callback = pendingPreparationCallback;
        pendingPreparationCallback = null;
        if (callback != null) {
            callback.onPrepared(this);
        }
    }

    private void schedulePlaybackReadyFallback() {
        removeCallbacks(playbackReadyFallbackRunnable);
        postDelayed(playbackReadyFallbackRunnable, PLAYBACK_READY_FALLBACK_MS);
    }

    private void notifyPlaybackReady() {
        if (playbackReadyNotified) {
            return;
        }
        playbackReadyNotified = true;
        removeCallbacks(playbackReadyFallbackRunnable);
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
        resetViewPosition(webView);
    }

    private void resetViewPosition(View view) {
        if (view == null) {
            return;
        }
        view.setTranslationX(0f);
        view.setTranslationY(0f);
    }

}

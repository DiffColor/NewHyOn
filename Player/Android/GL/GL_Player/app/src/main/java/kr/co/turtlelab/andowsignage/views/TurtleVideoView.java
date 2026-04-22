package kr.co.turtlelab.andowsignage.views;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.SurfaceTexture;
import android.media.MediaMetadataRetriever;
import android.media.MediaPlayer;
import android.net.Uri;
import android.text.TextUtils;
import android.util.AttributeSet;
import android.util.Log;
import android.view.Surface;
import android.view.TextureView;

import java.io.File;

import kr.co.turtlelab.andowsignage.AndoWSignage;

@SuppressLint("DrawAllocation")
public class TurtleVideoView extends TextureView implements TextureView.SurfaceTextureListener {

    String TAG = "TurtleVideoView";
    Context mContext;

    boolean mResumable = false;
    private onMediaPlayerChangedListener mOnMediaPlayerChangedListener;

    private int mVideoWidth;
    private int mVideoHeight;
    private String fpath;
    private Uri fUri;
    private boolean mKeepRatio = true;
    private boolean mLoop = true;
    private MediaPlayer.OnPreparedListener mUserPreparedListener;
    private MediaPlayer.OnInfoListener mUserInfoListener;
    private MediaPlayer.OnCompletionListener mUserCompletionListener;
    private MediaPlayer.OnErrorListener mUserErrorListener;
    private MediaPlayer mPreparedPlayer;
    private Surface mSurface;
    private int mDuration = 0;
    private boolean mMuted = true;
    private boolean mPrepared = false;
    private boolean mPreparing = false;
    private boolean mStartWhenPrepared = false;
    private int mPendingSeekMs = -1;

    public TurtleVideoView(Context context) {
        super(context);
        init(context);
    }

    public TurtleVideoView(Context context, AttributeSet attrs) {
        super(context, attrs);
        init(context);
    }

    public TurtleVideoView(Context context, AttributeSet attrs, int defStyle) {
        super(context, attrs, defStyle);
        init(context);
    }

    private void init(Context context) {
        mContext = context;
        setSurfaceTextureListener(this);
        if (isAvailable()) {
            createSurface(getSurfaceTexture());
        }
    }

    public void setVideoPath(String path) {
        String normalizedPath = normalizeLocalVideoPath(path);
        fpath = normalizedPath;
        fUri = null;
        Uri notifyUri = null;
        try {
            Uri parsed = TextUtils.isEmpty(normalizedPath) ? null : Uri.parse(normalizedPath);
            String scheme = parsed == null ? null : parsed.getScheme();
            if (TextUtils.isEmpty(scheme) && !TextUtils.isEmpty(normalizedPath) && normalizedPath.startsWith("/")) {
                notifyUri = Uri.fromFile(new File(normalizedPath));
            } else if (parsed != null && !TextUtils.isEmpty(scheme)) {
                fUri = parsed;
                notifyUri = parsed;
            }
        } catch (Exception ignored) {
        }
        onMediaPlayerChanged(notifyUri, normalizedPath);
        openVideo();
    }

    public void setVideoURI(Uri uri) {
        fUri = uri;
        fpath = null;
        onMediaPlayerChanged(uri, null);
        openVideo();
    }

    public void setOnPreparedListener(MediaPlayer.OnPreparedListener l) {
        mUserPreparedListener = l;
    }

    public void setMediaInfoListener(MediaPlayer.OnInfoListener l) {
        mUserInfoListener = l;
        MediaPlayer player = mPreparedPlayer;
        if (player != null) {
            try {
                player.setOnInfoListener(l);
            } catch (Exception ignored) {
            }
        }
    }

    public void setOnCompletionListener(MediaPlayer.OnCompletionListener l) {
        mUserCompletionListener = l;
        MediaPlayer player = mPreparedPlayer;
        if (player != null) {
            try {
                player.setOnCompletionListener(l);
            } catch (Exception ignored) {
            }
        }
    }

    public void setOnErrorListener(MediaPlayer.OnErrorListener l) {
        mUserErrorListener = l;
        MediaPlayer player = mPreparedPlayer;
        if (player != null) {
            try {
                player.setOnErrorListener(l);
            } catch (Exception ignored) {
            }
        }
    }

    public void stopPlayback() {
        releasePlayer();
        mResumable = false;
        mDuration = 0;
        mPendingSeekMs = -1;
        mStartWhenPrepared = false;
    }

    public void pause() {
        mStartWhenPrepared = false;
        MediaPlayer player = mPreparedPlayer;
        if (player == null) {
            return;
        }
        try {
            if (player.isPlaying()) {
                player.pause();
            }
        } catch (Exception ignored) {
        }
    }

    public void resume() {
        if (mResumable) {
            start();
        }
    }

    public void suspend() {
        releasePlayer();
        mResumable = false;
        mDuration = 0;
        mPendingSeekMs = -1;
        mStartWhenPrepared = false;
    }

    public void start() {
        mResumable = true;
        MediaPlayer player = mPreparedPlayer;
        if (player == null || !mPrepared) {
            mStartWhenPrepared = true;
            if (!mPreparing) {
                openVideo();
            }
            return;
        }
        try {
            player.start();
            mStartWhenPrepared = false;
        } catch (Exception e) {
            Log.w(TAG, "start failed", e);
        }
    }

    public boolean isPlaying() {
        MediaPlayer player = mPreparedPlayer;
        if (player == null || !mPrepared) {
            return false;
        }
        try {
            return player.isPlaying();
        } catch (Exception ignored) {
            return false;
        }
    }

    public int getCurrentPosition() {
        MediaPlayer player = mPreparedPlayer;
        if (player == null || !mPrepared) {
            return 0;
        }
        try {
            return player.getCurrentPosition();
        } catch (Exception ignored) {
            return 0;
        }
    }

    public void seekTo(int msec) {
        mPendingSeekMs = Math.max(0, msec);
        MediaPlayer player = mPreparedPlayer;
        if (player == null || !mPrepared) {
            return;
        }
        try {
            player.seekTo(mPendingSeekMs);
            mPendingSeekMs = -1;
        } catch (Exception ignored) {
        }
    }

    public boolean isResumable() {
        return mResumable;
    }

    public void setOnMediaPlayerChanged(onMediaPlayerChangedListener l) {
        mOnMediaPlayerChangedListener = l;
    }

    public interface onMediaPlayerChangedListener {
        void onMediaPlayerChanged(Uri uri, String path);
    }

    protected void onMediaPlayerChanged(Uri uri, String path) {
        if (mOnMediaPlayerChangedListener != null) {
            mOnMediaPlayerChangedListener.onMediaPlayerChanged(uri, path);
        }
    }

    public void setResumable(Boolean resumable) {
        mResumable = resumable;
    }

    public void setVideoSize(int width, int height) {
        mVideoWidth = width;
        mVideoHeight = height;
        requestLayout();
    }

    public void setKeepAspectRatio(boolean keep) {
        mKeepRatio = keep;
        requestLayout();
    }

    public void setLoop(boolean loop) {
        mLoop = loop;
        MediaPlayer player = mPreparedPlayer;
        if (player != null) {
            try {
                player.setLooping(loop);
            } catch (Exception ignored) {
            }
        }
    }

    public boolean isLoop() {
        return mLoop;
    }

    public void setMuted(boolean muted) {
        mMuted = muted;
        applyMutedState(mPreparedPlayer);
    }

    private void applyMutedState(MediaPlayer player) {
        if (player == null) {
            return;
        }
        try {
            float volume = mMuted ? 0f : 1f;
            player.setVolume(volume, volume);
        } catch (Exception ignored) {
        }
    }

    public int getDuration() {
        return mDuration;
    }

    @Override
    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
        int width = 0;
        int height = 0;

        if (mKeepRatio) {
            MediaMetadataRetriever metaRetriever = null;
            try {
                metaRetriever = new MediaMetadataRetriever();

                if (fpath != null) {
                    metaRetriever.setDataSource(fpath);
                } else if (fUri != null) {
                    metaRetriever.setDataSource(AndoWSignage.getCtx(), fUri);
                }

                mVideoWidth = Integer.parseInt(metaRetriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_WIDTH));
                mVideoHeight = Integer.parseInt(metaRetriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_HEIGHT));
                metaRetriever.release();

                width = getDefaultSize(mVideoWidth, widthMeasureSpec);
                height = getDefaultSize(mVideoHeight, heightMeasureSpec);
                if (mVideoWidth > 0 && mVideoHeight > 0) {
                    if (mVideoWidth * height > width * mVideoHeight) {
                        height = width * mVideoHeight / mVideoWidth;
                    } else if (mVideoWidth * height < width * mVideoHeight) {
                        width = height * mVideoWidth / mVideoHeight;
                    }
                }
            } catch (Exception e) {

                if (metaRetriever != null) {
                    metaRetriever.release();
                }

                width = getDefaultSize(getMeasuredWidth(), widthMeasureSpec);
                height = getDefaultSize(getMeasuredHeight(), heightMeasureSpec);
            }
        } else {
            width = getDefaultSize(getMeasuredWidth(), widthMeasureSpec);
            height = getDefaultSize(getMeasuredHeight(), heightMeasureSpec);
        }

        setMeasuredDimension(width, height);
    }

    @Override
    public void onSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) {
        createSurface(surface);
        if (mPreparedPlayer != null) {
            try {
                mPreparedPlayer.setSurface(mSurface);
            } catch (Exception ignored) {
            }
        } else if (hasVideoSource()) {
            openVideo();
        }
    }

    @Override
    public void onSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) {
    }

    @Override
    public boolean onSurfaceTextureDestroyed(SurfaceTexture surface) {
        if (mPreparedPlayer != null) {
            try {
                mPreparedPlayer.setSurface(null);
            } catch (Exception ignored) {
            }
        }
        releaseSurface();
        return true;
    }

    @Override
    public void onSurfaceTextureUpdated(SurfaceTexture surface) {
    }

    private void createSurface(SurfaceTexture surfaceTexture) {
        releaseSurface();
        if (surfaceTexture != null) {
            mSurface = new Surface(surfaceTexture);
        }
    }

    private boolean hasVideoSource() {
        return fUri != null || !TextUtils.isEmpty(fpath);
    }

    private void openVideo() {
        if (!hasVideoSource() || mSurface == null) {
            return;
        }
        releasePlayer();
        try {
            MediaPlayer player = new MediaPlayer();
            mPreparedPlayer = player;
            mPreparing = true;
            mPrepared = false;
            mDuration = 0;
            player.setSurface(mSurface);
            player.setLooping(mLoop);
            applyMutedState(player);
            player.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
                @Override
                public void onPrepared(MediaPlayer mp) {
                    handlePrepared(mp);
                }
            });
            player.setOnInfoListener(mUserInfoListener);
            player.setOnCompletionListener(new MediaPlayer.OnCompletionListener() {
                @Override
                public void onCompletion(MediaPlayer mp) {
                    if (mUserCompletionListener != null) {
                        mUserCompletionListener.onCompletion(mp);
                    }
                }
            });
            player.setOnErrorListener(new MediaPlayer.OnErrorListener() {
                @Override
                public boolean onError(MediaPlayer mp, int what, int extra) {
                    mPreparing = false;
                    mPrepared = false;
                    if (mUserErrorListener != null) {
                        return mUserErrorListener.onError(mp, what, extra);
                    }
                    return true;
                }
            });
            if (fUri != null) {
                player.setDataSource(mContext, fUri);
            } else {
                player.setDataSource(fpath);
            }
            player.prepareAsync();
        } catch (Exception e) {
            mPreparing = false;
            mPrepared = false;
            releasePlayer();
            Log.w(TAG, "openVideo failed. path=" + fpath + " uri=" + fUri, e);
            if (mUserErrorListener != null) {
                mUserErrorListener.onError(null, MediaPlayer.MEDIA_ERROR_UNKNOWN, 0);
            }
        }
    }

    private void handlePrepared(MediaPlayer mp) {
        mPreparing = false;
        mPrepared = true;
        mPreparedPlayer = mp;
        try {
            mDuration = mp.getDuration();
            mp.setLooping(mLoop);
            applyMutedState(mp);
            mp.setOnInfoListener(mUserInfoListener);
            if (mPendingSeekMs >= 0) {
                mp.seekTo(mPendingSeekMs);
                mPendingSeekMs = -1;
            }
        } catch (Exception ignored) {
        }
        requestLayout();
        if (mUserPreparedListener != null) {
            mUserPreparedListener.onPrepared(mp);
        }
        if (mStartWhenPrepared) {
            start();
        }
    }

    private void releasePlayer() {
        MediaPlayer player = mPreparedPlayer;
        mPreparedPlayer = null;
        mPreparing = false;
        mPrepared = false;
        if (player == null) {
            return;
        }
        try {
            player.reset();
        } catch (Exception ignored) {
        }
        try {
            player.release();
        } catch (Exception ignored) {
        }
    }

    private void releaseSurface() {
        Surface surface = mSurface;
        mSurface = null;
        if (surface != null) {
            try {
                surface.release();
            } catch (Exception ignored) {
            }
        }
    }

    private String normalizeLocalVideoPath(String path) {
        if (TextUtils.isEmpty(path)) {
            return path;
        }
        try {
            Uri parsed = Uri.parse(path);
            if ("file".equalsIgnoreCase(parsed.getScheme()) && !TextUtils.isEmpty(parsed.getPath())) {
                return parsed.getPath();
            }
        } catch (Exception ignored) {
        }
        return path;
    }
}

package kr.co.turtlelab.andowsignage.views;

import android.annotation.SuppressLint;
import android.content.Context;
import android.media.MediaPlayer;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.text.TextUtils;
import android.util.Log;
import android.util.AttributeSet;
import android.widget.VideoView;

import java.io.File;

import kr.co.turtlelab.andowsignage.AndoWSignage;

@SuppressLint("DrawAllocation")
public class TurtleVideoView extends VideoView {

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
    private MediaPlayer mPreparedPlayer;
    private int mDuration = 0;
    private boolean mMuted = true;

    private final MediaPlayer.OnPreparedListener mInternalPreparedListener = new MediaPlayer.OnPreparedListener() {
        @Override
        public void onPrepared(MediaPlayer mp) {
            mPreparedPlayer = mp;
            try {
                mDuration = mp.getDuration();
                mp.setLooping(mLoop);
                applyMutedState(mp);
            } catch (Exception ignored) {
            }
            if (mUserPreparedListener != null) {
                mUserPreparedListener.onPrepared(mp);
            }
        }
    };

    public TurtleVideoView(Context context) {
        super(context);
        mContext = context;
        super.setOnPreparedListener(mInternalPreparedListener);
    }

    public TurtleVideoView(Context context, AttributeSet attrs) {
        super(context, attrs);
        mContext = context;
        super.setOnPreparedListener(mInternalPreparedListener);
    }

    public TurtleVideoView(Context context, AttributeSet attrs, int defStyle) {
        super(context, attrs, defStyle);
        mContext = context;
        super.setOnPreparedListener(mInternalPreparedListener);
    }

    @Override
    public void setVideoPath(String path) {
        String normalizedPath = normalizeLocalVideoPath(path);
        fpath = normalizedPath;
        Uri uri = null;
        mDuration = 0;
        try {
            Uri parsed = TextUtils.isEmpty(normalizedPath) ? null : Uri.parse(normalizedPath);
            String scheme = parsed == null ? null : parsed.getScheme();
            if (TextUtils.isEmpty(scheme) && !TextUtils.isEmpty(normalizedPath) && normalizedPath.startsWith("/")) {
                uri = Uri.fromFile(new File(normalizedPath));
                super.setVideoURI(uri);
            } else {
                super.setVideoPath(normalizedPath);
            }
        } catch (Exception ex) {
            Log.w(TAG, "setVideoPath: fallback to raw path. path=" + normalizedPath, ex);
            super.setVideoPath(normalizedPath);
        }
        fUri = uri;
        onMediaPlayerChanged(uri, normalizedPath);
    }

    @Override
    public void setVideoURI(Uri uri) {
        super.setVideoURI(uri);
        onMediaPlayerChanged(uri, null);
        fUri = uri;
        mDuration = 0;
    }

    @Override
    public void setOnPreparedListener(MediaPlayer.OnPreparedListener l) {
        mUserPreparedListener = l;
        super.setOnPreparedListener(mInternalPreparedListener);
    }

    @Override
    public void stopPlayback() {
        super.stopPlayback();
        mResumable = false;
        mPreparedPlayer = null;
        mDuration = 0;
    }

    @Override
    public void pause() {
        super.pause();
    }

    @Override
    public void resume() {
        super.resume();
    }

    @Override
    public void suspend() {
        super.suspend();
        mResumable = false;
        mPreparedPlayer = null;
        mDuration = 0;
    }

    @Override
    public void start() {
        super.start();
        mResumable = true;
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
    }

    public void setKeepAspectRatio(boolean keep) {
        mKeepRatio = keep;
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

                if (fpath != null)
                    metaRetriever.setDataSource(fpath);
                else if (fUri != null)
                    metaRetriever.setDataSource(AndoWSignage.getCtx(), fUri);

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

                if (metaRetriever != null)
                    metaRetriever.release();

                width = getDefaultSize(getMeasuredWidth(), widthMeasureSpec);
                height = getDefaultSize(getMeasuredHeight(), heightMeasureSpec);
            }
        } else {
            width = getDefaultSize(getMeasuredWidth(), widthMeasureSpec);
            height = getDefaultSize(getMeasuredHeight(), heightMeasureSpec);
        }

        setMeasuredDimension(width, height);
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

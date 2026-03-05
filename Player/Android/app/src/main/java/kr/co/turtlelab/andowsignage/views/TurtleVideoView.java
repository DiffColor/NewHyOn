package kr.co.turtlelab.andowsignage.views;

import android.annotation.SuppressLint;
import android.content.Context;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.text.TextUtils;
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
    
	public TurtleVideoView(Context context) {
		super(context);
		mContext = context;
	}
	
	public TurtleVideoView(Context context, AttributeSet attrs) {
		super(context, attrs);
		mContext = context;
	}
	
	public TurtleVideoView(Context context, AttributeSet attrs, int defStyle) {
		super(context, attrs, defStyle);
		mContext = context;
	}
	
	@Override
	public void setVideoPath(String path) {
		fpath = path;
		Uri uri = null;
		try {
			Uri parsed = TextUtils.isEmpty(path) ? null : Uri.parse(path);
			String scheme = parsed == null ? null : parsed.getScheme();
			if (TextUtils.isEmpty(scheme) && !TextUtils.isEmpty(path) && path.startsWith("/")) {
				uri = Uri.fromFile(new File(path));
				super.setVideoURI(uri);
			} else {
				super.setVideoPath(path);
			}
		} catch (Exception ex) {
			super.setVideoPath(path);
		}
		fUri = uri;
		onMediaPlayerChanged(uri, path);
	}
	
	@Override
	public void setVideoURI(Uri uri) {
		super.setVideoURI(uri);
		onMediaPlayerChanged(uri, null);
		fUri = uri;
	}
	
	@Override
	public void stopPlayback() {
		super.stopPlayback();
		mResumable = false;
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
	
	@Override
	protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
		int width = 0;
		int height = 0;
		
		if(mKeepRatio) {
			MediaMetadataRetriever metaRetriever = null;
			try {
				metaRetriever = new MediaMetadataRetriever();

				if(fpath != null)
					metaRetriever.setDataSource(fpath);
				else if(fUri != null)
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
			} catch(Exception e) {
				
				if(metaRetriever != null)
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
}

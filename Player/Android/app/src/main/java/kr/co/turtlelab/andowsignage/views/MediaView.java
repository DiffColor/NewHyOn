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
import android.util.Log;
import android.view.View;
import android.widget.ImageView;
import android.widget.ImageView.ScaleType;
import android.widget.RelativeLayout;

import com.nostra13.universalimageloader.core.DisplayImageOptions;
import com.nostra13.universalimageloader.core.ImageLoader;
import com.nostra13.universalimageloader.core.assist.ImageScaleType;

import java.text.SimpleDateFormat;
import java.util.Date;
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
	
	List<MediaDataModel> cdmList;
	
	long tick = 0;
	long playTime = 1000;
	CONTENT_TYPE s_usedType = CONTENT_TYPE.None;
	
	ImageView imgView1;
	ImageView imgView2;
	TurtleVideoView videoView;
	TurtleWebView webView;
	
	Activity act;
	Context ctx;
	
	long sRemainRepeat = 1;	
	Runnable mPopContentRunnable;
	Runnable mStopTaskRunnable;
	LoopPlay mLoopPlay = null; 

	boolean s_isFirst = true;
	
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
		setOtherSettings(); // bitmap options, schedule executors
		
		mLoopPlay = new LoopPlay();
	}
	
	private void initChildViews() {
		
		RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
												RelativeLayout.LayoutParams.MATCH_PARENT,
												RelativeLayout.LayoutParams.MATCH_PARENT);

		params.addRule(RelativeLayout.CENTER_IN_PARENT, RelativeLayout.TRUE);
		
		imgView1 = new ImageView(ctx);		
		imgView1.setLayerType(View.LAYER_TYPE_SOFTWARE, null);
		imgView1.setVisibility(View.GONE);
		
		imgView2 = new ImageView(ctx);
		imgView2.setLayerType(View.LAYER_TYPE_SOFTWARE, null);
		imgView2.setVisibility(View.GONE);
		
		videoView = new TurtleVideoView(ctx);
		videoView.setVisibility(View.GONE);

		if(AndoWSignageApp.KEEP_ASPECT_RATIO) {
			imgView1.setScaleType(ScaleType.FIT_CENTER);
			imgView2.setScaleType(ScaleType.FIT_CENTER);
			videoView.setKeepAspectRatio(true);
		}
		else {
			imgView1.setScaleType(ScaleType.FIT_XY);
			imgView2.setScaleType(ScaleType.FIT_XY);
			videoView.setKeepAspectRatio(false);
		}

		webView = new TurtleWebView(ctx, getMinimumWidth(), getMinimumHeight());
		webView.setVisibility(View.GONE);

		addView(imgView1, params);
		addView(imgView2, params);
		addView(videoView, params);
		addView(webView, params);
	}
	
	private void setViewEvents() {
		
		videoView.setOnCompletionListener(new OnCompletionListener() {
			
			@Override
			public void onCompletion(MediaPlayer mp) {
				if(sRemainRepeat > 1) {
					mp.start();
					sRemainRepeat--;
				} else {
					synchronized (mLoopPlay) {
						mLoopPlay.notify();
					}
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
		.resetViewBeforeLoading(true)
		.imageScaleType(ImageScaleType.EXACTLY)
		.build();
	}
	
	public void runPlaylist() {
		s_isFirst = true;
		mLoopPlay = new LoopPlay();
		mLoopPlay.executeOnExecutor(loopExecutor);
	}
	
	public void stopPlaylist() {
		try{
			if(videoView.isPlaying()) {
				videoView.stopPlayback();
			}
		} catch (Exception e) {
			
		} finally {
			mStopTaskRunnable.run();
			mPopContentRunnable.run();
		}
	}

	public void count() {
		++tick;
		
		if(tick >= playTime) {
			if(this.cdmList.size() > 1 && s_usedType !=  CONTENT_TYPE.Video)
				popContent();
			tick = 0;
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

		if(tmpIdx < -1)
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
	class LoopPlay extends AsyncTask<Void, String , Void> {
		
		AndoWSignageApp.CONTENT_TYPE usedType;

		Bitmap image1 = null;
		Bitmap image2 = null;
		
		@Override
		protected Void doInBackground(Void... params) {
			int j = 0;
			while(!isCancelled()) {
					synchronized (cdmList) {

						if(cdmList.size() < 1) {
							try {
								Thread.sleep(250);
							} catch (InterruptedException e) {
								e.printStackTrace();
							}
							continue;
						}

                        AndoWSignage.act.stopTick();

						if(contentIdx >= cdmList.size())
							contentIdx = 0;

						if(contentIdx >= cdmList.size()-1)
							j = 0;
						else
							j = contentIdx+1;

						if(isCancelled()) {
							break;
						}

						playTime = cdmList.get(contentIdx).getPlayTimeSec();

						publishProgress(new String[]{cdmList.get(contentIdx).getType(), cdmList.get(contentIdx).getFilePath(), cdmList.get(j).getType(), cdmList.get(j).getFilePath()});

						try {
							synchronized (mLoopPlay) {
								mLoopPlay.wait();
							}
						} catch(Exception e) {
						}

						if(s_isFirst)
							s_isFirst = false;

						if(manual)
						{
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
						AndoWSignageApp.CONTENT_TYPE type1 = AndoWSignageApp.CONTENT_TYPE.valueOf(contentData[0]);
						AndoWSignageApp.CONTENT_TYPE type2 = AndoWSignageApp.CONTENT_TYPE.valueOf(contentData[2]);
						
						switch(type1) {
							case Image:
								releaseUsedResources(usedType, type1);
								
								if(imgView1.isShown()) {
									
									if(s_isFirst) {
										ImageLoader.getInstance().displayImage(LocalPathUtils.getUriStringFromAbsPath(contentData[1]), imgView2, imgOpt);
									}
	
									imgView1.setVisibility(View.GONE);
									imgView2.setVisibility(View.VISIBLE);
									
									if(type2 == CONTENT_TYPE.Image)
										ImageLoader.getInstance().displayImage(LocalPathUtils.getUriStringFromAbsPath(contentData[3]), imgView1, imgOpt);
									
								} else {	
	
									if(s_isFirst)
										ImageLoader.getInstance().displayImage(LocalPathUtils.getUriStringFromAbsPath(contentData[1]), imgView1, imgOpt);
	
									imgView2.setVisibility(View.GONE);
									imgView1.setVisibility(View.VISIBLE);
	
									if(type2 == CONTENT_TYPE.Image)
										ImageLoader.getInstance().displayImage(LocalPathUtils.getUriStringFromAbsPath(contentData[3]), imgView2, imgOpt);
								}
								break;
								
							case Video:
								releaseUsedResources(usedType, type1);
	
								videoView.setVideoPath(contentData[1]);
								videoView.start();
								videoView.setVisibility(View.VISIBLE);		

								sRemainRepeat = playTime / videoView.getDuration();
								
								if(type2 == CONTENT_TYPE.Image)
									ImageLoader.getInstance().displayImage(LocalPathUtils.getUriStringFromAbsPath(contentData[3]), imgView1, imgOpt);
								
								break;
								
							case Flash:

								releaseUsedResources(usedType, type1);
//								String path = "file://" + contentData[1];
								//File ffpath = new File(contentData[1]);
//								webView.loadUrl(LocalPathUtils.getUriStringFromAbsPath(contentData[1]));
//								webView.loadUrl(path);
								
								String html = "<object width=\"550\" height=\"400\"> <param name=\"movie\" value=\"file://"+ contentData[1] +"\"> <embed src=\"file://"+ contentData[1] +"\" width=\"550\" height=\"400\"> </embed> </object>";
						        String mimeType = "text/html";
						        String encoding = "utf-8";

						        webView.loadDataWithBaseURL("null", html, mimeType, encoding, "");

								webView.setVisibility(View.VISIBLE);
								webView.setBackgroundColor(Color.BLACK);

								if(usedType == CONTENT_TYPE.Image) {
									imgView1.setVisibility(View.GONE);
									imgView2.setVisibility(View.GONE);
								}
								break;
								
							case WebSiteURL:
								releaseUsedResources(usedType, type1);

								webView.loadUrl(contentData[1]);

								webView.setBackgroundColor(Color.WHITE);
								webView.setVisibility(View.VISIBLE);

								if(usedType == CONTENT_TYPE.Image) {
									imgView1.setVisibility(View.GONE);
									imgView2.setVisibility(View.GONE);
								}
								break;
								
							case PPT:
							default:
								break;
						}
						s_usedType = usedType = type1;	
					} catch(Exception e) {
					}
					finally {
                        AndoWSignage.act.startTick();
				        SystemUtils.systemBarVisibility(act, false);	
					}
				}

				private void releaseUsedResources(CONTENT_TYPE usedType, CONTENT_TYPE type) {

					if(usedType == null) return;
					
					switch(usedType) {
						case Image:
							if(type != CONTENT_TYPE.Image) {
								if(imgView1.isShown())  {
									imgView1.setVisibility(View.GONE);
								}
								else if(imgView2.isShown())	{ 
									imgView2.setVisibility(View.GONE);
								}
							}
							break;

						case Video:
							videoView.stopPlayback();
							if(type != CONTENT_TYPE.Video)
								videoView.setVisibility(View.GONE);
							break;
							
						case WebSiteURL:
							webView.releaseWebView();
							break;
					}
				}
			});
		}
	}
}

package kr.co.turtlelab.andowsignage.views;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Bitmap.Config;
import android.net.Uri;
import android.os.AsyncTask;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.ImageView;
import android.widget.ImageView.ScaleType;
import android.widget.RelativeLayout;
import android.widget.TextView;

import com.nostra13.universalimageloader.core.DisplayImageOptions;
import com.nostra13.universalimageloader.core.ImageLoader;
import com.nostra13.universalimageloader.core.assist.ImageScaleType;

import java.io.File;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class WelcomeView extends RelativeLayout {
	
	WelcomeDataModel wdm;
	private static final String TAG = "WelcomeView";
		
	ImageView imgView;
	TextView txtView;
	
	Activity act;
	Context ctx;
	
	LoopPlay mLoopPlay = null; 
	
	DisplayImageOptions imgOpt;
	
	public WelcomeView(Activity act, Context context, int width, int height, WelcomeDataModel wdm) {
		super(context);
		
		this.wdm = wdm;
		ctx = context;
		this.act = act;
		
		setMinimumWidth(width);
		setMinimumHeight(height);

		//this.setBackgroundColor(Color.parseColor(wdm.getBgColor()));
		
		initChildViews();

		setOtherSettings(); // bitmap options, schedule executors
		
		mLoopPlay = new LoopPlay();
		mLoopPlay.executeOnExecutor(AsyncTask.THREAD_POOL_EXECUTOR);
	}
	
	private void initChildViews() {
		
		RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
												RelativeLayout.LayoutParams.MATCH_PARENT,
												RelativeLayout.LayoutParams.MATCH_PARENT);
		
		imgView = new ImageView(ctx);
		imgView.setScaleType(ScaleType.FIT_XY);
		imgView.setLayerType(View.LAYER_TYPE_SOFTWARE, null);
		imgView.setVisibility(View.GONE);

		addView(imgView, params);
		
//		txtView = new TextView(ctx);
//		txtView.setText(wdm.getText());
//		setFontStyle();
//		addView(txtView, params);
	}
	
//	private void setFontStyle() {
//		
//		Typeface font = null;
//		
//		try {
//			String fontpath = LocalPathUtils.getFontFilePath(wdm.getFontColor());
//			File fontfile = new File(fontpath);
//			font = Typeface.createFromFile(fontfile);
//		} catch(Exception e) {
//			e.printStackTrace();
//		} finally {
//			if(font == null) {
//				font = Typeface.createFromAsset(act.getAssets(),"fonts/NanumGothic.otf");
//			}
//		}
//		
//		boolean isBold = wdm.getIsBold();
//		boolean isItalic = wdm.getIsItalic();
//		int style = Typeface.NORMAL;
//		
//		if(isBold && isItalic) {
//			style = Typeface.BOLD_ITALIC;
//		} else if(isBold) {
//			style = Typeface.BOLD;
//		} else if(isItalic) {
//			style = Typeface.ITALIC;
//		}
//		
//		txtView.setTypeface(font, style);
//		
//		txtView.setTextColor(Color.parseColor(wdm.getFontColor()));
//		txtView.setTextSize(TypedValue.COMPLEX_UNIT_PX, wdm.getFontSize());
//		txtView.setGravity(Gravity.CENTER);
//	}
		
	private void setOtherSettings() {		
		imgOpt = new DisplayImageOptions.Builder()
		.bitmapConfig(Config.RGB_565)
		.cacheInMemory(false)
		.cacheOnDisk(true)
		.resetViewBeforeLoading(true)
		.imageScaleType(ImageScaleType.EXACTLY)
		.build();
	}
	
	class LoopPlay extends AsyncTask<Void, String , Void> {
		
		AndoWSignageApp.CONTENT_TYPE usedType;

		Bitmap image = null;
		
		@Override
		protected Void doInBackground(Void... params) {
			if (wdm == null) {
				return null;
			}
			try {
				String localPath;
				synchronized (wdm) {
					localPath = wdm.getLocalImgPath();
				}
				if (!TextUtils.isEmpty(localPath)) {
					publishProgress(localPath);
				}
			} catch (Exception e) {
				Log.e(TAG, "Failed to prepare welcome content", e);
			}

			return null;
		}

		@Override
		protected void onProgressUpdate(final String... contentData) {
			super.onProgressUpdate(contentData);

			SystemUtils.runOnUiThread(new Runnable() {
				
				public void run() {					
						if (contentData == null || contentData.length == 0 || TextUtils.isEmpty(contentData[0])) {
							return;
						}
						try {
							File file = new File(contentData[0]);
							if (!file.exists()) {
								return;
							}
							
							//setBackgroundColor(Color.TRANSPARENT);
							
						    String uri = Uri.fromFile(file).toString();

							final String fpath = Uri.decode(uri);
							
							imgView.setVisibility(View.VISIBLE);
							ImageLoader.getInstance().displayImage(fpath, new SafeImageViewAware(imgView), imgOpt);
						} catch (Exception e) {
							Log.e(TAG, "Failed to display welcome content", e);
						}
				}
			});
		}
	}
}

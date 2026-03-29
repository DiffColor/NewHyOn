package kr.co.turtlelab.andowsignage.tools;

import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.graphics.drawable.Drawable;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.widget.RelativeLayout;
import android.widget.RelativeLayout.LayoutParams;

import java.io.File;

import kr.co.turtlelab.andowsignage.R;
import kr.co.turtlelab.andowsignage.views.GifMovieView;

public class Utils {

	public static String parseNumber(String str) {
		return str.replaceAll("[^0-9]", "");
	}
	
	static boolean alertRet = false;
	public static boolean showAlertDialog(Context ctx, Drawable icon, String title, String msg, String btn1, String btn2) {
		// 1. Instantiate an AlertDialog.Builder with its constructor
		AlertDialog.Builder builder = new AlertDialog.Builder(ctx);

		// 2. Chain together various setter methods to set the dialog characteristics
		builder.setTitle(title)
		       .setMessage(msg);
		
		if(icon != null) {
			builder.setIcon(icon);
		} else {
			builder.setIcon(android.R.drawable.ic_dialog_alert);
		}
		
		if(btn1 != null) {
			// Add the buttons
			builder.setPositiveButton(btn1, new DialogInterface.OnClickListener() {
			           public void onClick(DialogInterface dialog, int id) {
			        	   alertRet = true;
			           }
			       });
		}
		
		if(btn2 != null) {
			builder.setNegativeButton(btn2, new DialogInterface.OnClickListener() {
		           public void onClick(DialogInterface dialog, int id) {
		        	   alertRet = false;
		           }
		       });
		}
		
		// 3. Get the AlertDialog from create()
		AlertDialog dialog = builder.create();
		dialog.show();
		
		return alertRet;
	}
	
	public static <T extends Enum<T>> String[] enumNameToStringArray(T[] values) {  
	    int i = 0;  
	    String[] result = new String[values.length];  
	    for (T value: values) {  
	        result[i++] = value.name();  
	    }  
	    return result;  
	}
	
	public static boolean isNullOrEmpty(String str) {
		if(str == null) return true;
		else if(str.trim().length() < 1) return true;
		else return false;
	}
	
	public static int findViewByName(Context context, String name) {
		return context.getResources().getIdentifier(name, "id", context.getPackageName());
	}
	
	public static GifMovieView showLoadingAnim(Context ctx, RelativeLayout layout_root) {
		GifMovieView gifView = new GifMovieView(ctx);
		RelativeLayout.LayoutParams layout_params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
		layout_params.addRule(RelativeLayout.CENTER_IN_PARENT);
		layout_root.addView(gifView ,layout_params);
		
		gifView.setMovieResource(R.drawable.loading);
		
		return gifView;
	}
	
	public static void stopLoadingAnim(GifMovieView gifView, RelativeLayout layout_root) {
		layout_root.removeView(gifView);
		gifView = null;
	}
	
	public static long getSecondsADay(int hour, int min) {
		return ((hour*60)+min)*60;
	}

	public static long getVideoDuration(Context ctx, String fpath) {
		MediaMetadataRetriever retriever = new MediaMetadataRetriever();
		//use one of overloaded setDataSource() functions to set your data source
		retriever.setDataSource(ctx, Uri.fromFile(new File(fpath)));
		String time = retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION);
		retriever.release();
		return Long.parseLong(time);
	}
}

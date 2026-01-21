package kr.co.turtlelab.andowsignage.tools;

import android.app.Activity;
import android.app.ActivityManager;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.os.StatFs;
import android.view.View;
import android.view.Window;
import android.view.WindowManager.LayoutParams;

import java.lang.reflect.Field;
import java.util.List;

public class SystemUtils {

	public static void setDimButtons(Activity act, boolean dimButtons) {     
		Window window = act.getWindow();
		LayoutParams layoutParams = window.getAttributes();     
		float val = dimButtons ? 0 : -1;     
		
		try {         
			Field buttonBrightness = layoutParams.getClass().getField("buttonBrightness");         
			buttonBrightness.set(layoutParams, val);     
			} catch (Exception e) {         
				e.printStackTrace();     
			}     
		
		window.setAttributes(layoutParams); 
	} 
	
	public static void systemBarVisibility(Activity act, boolean visible) {
	
		int hideState = act.getWindow().getDecorView().getSystemUiVisibility();
		if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.ICE_CREAM_SANDWICH )
			hideState |= View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
		if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.JELLY_BEAN )
			hideState |= View.SYSTEM_UI_FLAG_FULLSCREEN;
		
		if( android.os.Build.VERSION.SDK_INT >= 19 )
			hideState |= 4096;
		
		if(android.os.Build.VERSION.SDK_INT < 19)
			hideState = View.SYSTEM_UI_FLAG_LOW_PROFILE;
		
//		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.KITKAT) {
//			hideState = View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
//	                            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
//	                            | View.SYSTEM_UI_FLAG_FULLSCREEN
//	                            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY;
//	    } else {
//	    	hideState = View.SYSTEM_UI_FLAG_LAYOUT_STABLE
//	    	        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
//	    	        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
//	    	        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION // hide nav bar
//	    	        | View.SYSTEM_UI_FLAG_FULLSCREEN; // hide status bar
//	    }
		
		int showState = View.SYSTEM_UI_FLAG_VISIBLE;
		
		if(visible) {
//		if(VERSION.SDK_INT > 10) {
			act.getWindow().getDecorView().setSystemUiVisibility(showState);
//		}
		} else {
			act.getWindow().getDecorView().setSystemUiVisibility(hideState);
		}
	}
	

	
	@SuppressWarnings("deprecation")
	public static int getRemainingExtSDPercent()
	{
	    StatFs stat = new StatFs(Environment.getExternalStorageDirectory().getPath());
//	    long remainBytes = (long)stat.getBlockSize() * (long)stat.getAvailableBlocks();
//	    long totalBytes = (long)stat.getBlockSize() * (long)stat.getBlockCount();
	    int remainBlocks = stat.getAvailableBlocks();
	    int totalBlocks = stat.getBlockCount();
	    return (remainBlocks*100)/totalBlocks;
	}
	
	@SuppressWarnings("deprecation")
	public static int getUsingExtSDPercent()
	{
	    return (100 - getRemainingExtSDPercent());
	}
	
	public static boolean checkIsLand(Activity act) {
		return act.getResources().getConfiguration().orientation == Configuration.ORIENTATION_LANDSCAPE;
	}
	

	public static void runOnUiThread(Runnable runnable){
	    final Handler UIHandler = new Handler(Looper.getMainLooper());
	    UIHandler.post(runnable);
	}
	
	public static boolean isForeground(Context context, String PackageName){
	  // Get the Activity Manager
	  ActivityManager manager = (ActivityManager) context.getSystemService(context.ACTIVITY_SERVICE);
	 
	  // Get a list of running tasks, we are only interested in the last one, 
	  // the top most so we give a 1 as parameter so we only get the topmost.
	  List< ActivityManager.RunningTaskInfo > task = manager.getRunningTasks(1); 
	 
	  // Get the info we need for comparison.
	  ComponentName componentInfo = task.get(0).topActivity;
	 
	  // Check if it matches our package name.
	  if(componentInfo.getPackageName().equals(PackageName)) return true;
	     
	  // If not then our app is not on the foreground.
	  return false;
	}
	
	public static void launchAnotherActWithData(Activity act, String pkgname, String clsname, String data_apkpath, String data_pkgname, String data_clsname, boolean cleartask) {
		Intent intent = new Intent();
		intent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
		
		Bundle extras = new Bundle();
		       extras.putString("apkpath", data_apkpath);
		       extras.putString("pkgname", data_pkgname);
		       extras.putString("clsname", data_clsname);
		       
        intent.putExtras(extras);
        
        if(cleartask) {
			intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
			intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
        }
        
        act.startActivity(intent);
        act.finish();
	}
	
	public static void restartAct(Context ctx, boolean cleartask) {
		Intent intent = ctx.getPackageManager().getLaunchIntentForPackage(ctx.getPackageName());
		
		if(cleartask) {
			intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
			intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
        }
		
		ctx.startActivity(intent);
	}
	
	public static void launchAnotherAct(Activity act, String pkgname, String clsname) {
		Intent intent = new Intent();
		intent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
		intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
		intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
		act.startActivity(intent);
        act.finish();
	}

	public static boolean launchAppNewTask(Context context, String pkgname, String clsname, boolean cleartask){
		try {
			Intent launchIntent = new Intent();
			launchIntent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));

			if(cleartask) {
				launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
			}

			launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			context.startActivity(launchIntent);
			return true;
		} catch(Exception e)
		{
			return false;
		}
	}
}

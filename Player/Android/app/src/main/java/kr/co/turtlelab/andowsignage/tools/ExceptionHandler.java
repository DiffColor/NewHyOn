package kr.co.turtlelab.andowsignage.tools;

import android.app.Activity;
import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;

public class ExceptionHandler implements Thread.UncaughtExceptionHandler {
	  private Activity activity;
	  
	  public ExceptionHandler(Activity a) {
	    activity = a;
	  }
	  
	  @Override
	  public void uncaughtException(Thread thread, Throwable ex) {
	    Intent intent = new Intent(activity, AndoWSignageApp.class);
	    intent.putExtra("crash", true);
	    intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_NEW_TASK);
	    PendingIntent pendingIntent = PendingIntent.getActivity(AndoWSignageApp.getApplication().getBaseContext(), 0, intent, PendingIntent.FLAG_ONE_SHOT);
	    AlarmManager mgr = (AlarmManager) AndoWSignageApp.getApplication().getBaseContext().getSystemService(Context.ALARM_SERVICE);
	    mgr.set(AlarmManager.RTC, System.currentTimeMillis() + 100, pendingIntent);
	    activity.finish();
	    System.exit(2);
	  }
	}

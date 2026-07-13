package kr.co.turtlelab.andowsignage.services;

import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.os.IBinder;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.WeeklyScheduleProvider;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.PowerApi;

public class PowerService extends Service {
	private static final int CHECK_INTERVAL_MS = 15000;

	List<WeeklyScheduleDataModel> weeklySchDataList = new ArrayList<WeeklyScheduleDataModel>();
	
	LightestTimer checkTimer = new LightestTimer(CHECK_INTERVAL_MS, new Runnable() {
		
		@Override
		public void run() {
//			if(skip_count > 1) {
//				skip_count--;
//				return;
//			}
			checkIsOnTime();
		}
	});
	
	Context ctx;
//	final int SKIP_COUNT = 1;
//	int skip_count;

	public void checkIsOnTime() {
	    
		if(AndoWSignageApp.isUpdating) return;
	    
		weeklySchDataList.clear();
		weeklySchDataList = WeeklyScheduleProvider.getWeeklyScheduleList();

		Date date = new Date();
		SimpleDateFormat df = new SimpleDateFormat("ccc HH mm", Locale.ENGLISH);
		String[] curTime = df.format(date).split(" ");
		boolean handled = false;
		
		for (WeeklyScheduleDataModel sch : weeklySchDataList) {
			if(sch.getDayStr().toLowerCase(Locale.US).equalsIgnoreCase(curTime[0].toLowerCase(Locale.US))) {
				handled = true;
				if(sch.getOnAir()) {					
					int[] from = sch.getFrom();
					int[] to = sch.getTo();
					int currentMinutes = (Integer.parseInt(curTime[1]) * 60) + Integer.parseInt(curTime[2]);
					int startMinutes = (from[0] * 60) + from[1];
					int endMinutes = (to[0] * 60) + to[1];
					if (startMinutes == endMinutes) {
						WakeUp();
						break;
					}
					if(isWithinOnAirWindow(currentMinutes, startMinutes, endMinutes)) {
						WakeUp();
					} else {
						Sleep();
					}
					break;
				}
				else {
					Sleep();
				}
			}
		}

		if (!handled) {
			WakeUp();
		}
	}

	private boolean isWithinOnAirWindow(int currentMinutes, int startMinutes, int endMinutes) {
		if (startMinutes == endMinutes) {
			return true;
		}
		if (endMinutes > startMinutes) {
			return currentMinutes >= startMinutes && currentMinutes < endMinutes;
		}
		return currentMinutes >= startMinutes || currentMinutes < endMinutes;
	}
	
	public void Sleep() {
		if(AndoWSignageApp.isSlept == false) {
			AndoWSignageApp.isSlept = true;
            AndoWSignageApp.markStoppedState();
			if (PowerApi.setSleepMode(ctx, true)) {
				Intent sleepIntent = new Intent();
				sleepIntent.setAction("andowsignage.intent.action.SLEEP");
				ctx.sendBroadcast(sleepIntent);
			} else {
				stopPlaybackForSleepCommandFailure();
			}
		}
	}

	private void stopPlaybackForSleepCommandFailure() {
		final AndoWSignage player = AndoWSignage.act;
		if (player == null) {
			return;
		}
		player.runOnUiThread(new Runnable() {
			@Override
			public void run() {
				player.stopAndRemoveAllViews();
			}
		});
	}
	
	public void WakeUp() {
		if(AndoWSignageApp.isSlept) {
//			skip_count = SKIP_COUNT;
			AndoWSignageApp.isSlept = false;
			PowerApi.setSleepMode(ctx, false);
			resumePlaybackAfterWakeUp();

//			Intent wakeupIntent = new Intent();
//			wakeupIntent.setAction("andowsignage.intent.action.WAKEUP");
//			ctx.sendBroadcast(wakeupIntent);

//			Intent _intent = new Intent();
//			_intent.setAction("rk.android.wakeupmode.action");
//			ctx.sendBroadcast(_intent);

//			try {
//				Thread.sleep(1000);
//			} catch (InterruptedException e) {
//				e.printStackTrace();
//			}
//
//			Intent playerIntent = new Intent(ctx, AndoWSignage.class);
//			playerIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
//			playerIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
//			playerIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
//			ctx.startActivity(playerIntent);

			// Android 플레이어는 방송 시작 시 재부팅하지 않는다.
			// PowerApi.requestReboot(ctx);
		}
	}

	private void resumePlaybackAfterWakeUp() {
		final AndoWSignage player = AndoWSignage.act;
		if (player == null) {
			return;
		}
		player.runOnUiThread(new Runnable() {
			@Override
			public void run() {
				player.updateAndRestart(true);
			}
		});
	}

	@Override
	public IBinder onBind(Intent intent) {		
		return null;
	}

	@Override
	public void onCreate() {
		super.onCreate();
		//AlarmUtils.setWeeklyAlarm(ctx));
		
		ctx = getApplicationContext();

		checkIsOnTime();
		checkTimer.start();

//		skip_count = SKIP_COUNT;
	}

	@Override
	public void onDestroy() {
		//AlarmUtils.cancelAlarm(ctx));
		checkTimer.stop();
		super.onDestroy();
	}

	@Override
	public boolean onUnbind(Intent intent) {
		return super.onUnbind(intent);
	}

}

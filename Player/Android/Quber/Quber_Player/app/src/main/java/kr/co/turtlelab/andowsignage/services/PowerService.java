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

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.WeeklyScheduleProvider;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.PowerApi;
import kr.co.turtlelab.andowsignage.tools.Utils;

public class PowerService extends Service {

	List<WeeklyScheduleDataModel> weeklySchDataList = new ArrayList<WeeklyScheduleDataModel>();
	
	LightestTimer checkTimer = new LightestTimer(15000, new Runnable() {
		
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
		
		for (WeeklyScheduleDataModel sch : weeklySchDataList) {
			if(sch.getDayStr().toLowerCase(Locale.US).equalsIgnoreCase(curTime[0].toLowerCase(Locale.US))) {
				if(sch.getOnAir()) {					
					int[] from = sch.getFrom();
					int[] to = sch.getTo();
					long currSec = Utils.getSecondsADay(Integer.parseInt(curTime[1]), Integer.parseInt(curTime[2]));
					long fromSec = Utils.getSecondsADay(from[0], from[1]);
					long toSec = Utils.getSecondsADay(to[0], to[1]);
					if (from[0] == 0 && from[1] == 0 && to[0] == 0 && to[1] == 0) {
						WakeUp();
						break;
					}
					if(currSec >= fromSec && currSec <= toSec) {
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
	}
	
	public void Sleep() {
		if(AndoWSignageApp.isSlept == false) {
			PowerApi.setSleepMode(ctx, true);
			Intent sleepIntent = new Intent();
			sleepIntent.setAction("andowsignage.intent.action.SLEEP");
	        ctx.sendBroadcast(sleepIntent);
		}
		AndoWSignageApp.isSlept = true;
        AndoWSignageApp.state = AndoWSignageApp.RP_STATUS.stopped.toString();
	}
	
	public void WakeUp() {
		if(AndoWSignageApp.isSlept) {
//			skip_count = SKIP_COUNT;
			AndoWSignageApp.isSlept = false;
			PowerApi.setSleepMode(ctx, false);

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

			PowerApi.requestReboot(ctx);
		}
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

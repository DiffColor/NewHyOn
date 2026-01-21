package kr.co.turtlelab.andowsignage.tools;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;

import java.util.Calendar;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp.DAY_OF_WEEK;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.WeeklyScheduleProvider;
import kr.co.turtlelab.andowsignage.receivers.WakeOrSleepReceiver;

public class AlarmUtils {
	
	static final long INTERVAL_WEEK = 7 * 24 * 60 * 60 * 1000;
	
	public static void setWeeklyAlarm(Context context)
    {	
		cancelAlarm(context);
	
		List<WeeklyScheduleDataModel> weeklySchedlues = WeeklyScheduleProvider.getWeeklyScheduleList();
		
		AlarmManager alarmMgr;
		alarmMgr = (AlarmManager)context.getSystemService(Context.ALARM_SERVICE);

		PendingIntent wakeupPending;
		PendingIntent sleepPending;
		
		Intent wakeup_intent = new Intent(context, WakeOrSleepReceiver.class);
		Intent sleep_intent = new Intent(context, WakeOrSleepReceiver.class);
		
		wakeup_intent.setAction("andowsignage.intent.action.WAKEUP");
		sleep_intent.setAction("andowsignage.intent.action.SLEEP");		
		
		wakeupPending = PendingIntent.getBroadcast(context, 0, wakeup_intent, 0);
		sleepPending = PendingIntent.getBroadcast(context, 0, sleep_intent, 0);

		Calendar calendar = Calendar.getInstance();
		
		for (WeeklyScheduleDataModel sch : weeklySchedlues) {
			calendar.set(Calendar.DAY_OF_WEEK, getDayInt(sch.getDay()));
			int[] from = sch.getFrom();
			calendar.set(Calendar.HOUR_OF_DAY, from[0]);
			calendar.set(Calendar.MINUTE, from[1]);	
//			calendar.set(Calendar.SECOND, 0);
//			calendar.set(Calendar.MILLISECOND, 0);
			alarmMgr.setRepeating(AlarmManager.RTC_WAKEUP, calendar.getTimeInMillis(), INTERVAL_WEEK, wakeupPending);
			calendar.clear();
			
			int[] to = sch.getTo();
			calendar.set(Calendar.HOUR_OF_DAY, to[0]);
			calendar.set(Calendar.MINUTE, to[1]);	
//			calendar.set(Calendar.SECOND, 0);
//			calendar.set(Calendar.MILLISECOND, 0);
			alarmMgr.setRepeating(AlarmManager.RTC_WAKEUP, calendar.getTimeInMillis(), INTERVAL_WEEK, sleepPending);
		}
    }
	
	public static int getDayInt(DAY_OF_WEEK dow) {
		int ret = 1;
		switch (dow) {
			case MON:
				ret = Calendar.MONDAY;
				break;
			case TUE:
				ret = Calendar.TUESDAY;
				break;
			case WED:
				ret = Calendar.WEDNESDAY;
				break;
			case THU:
				ret = Calendar.THURSDAY;
				break;
			case FRI:
				ret = Calendar.FRIDAY;
				break;
			case SAT:
				ret = Calendar.SATURDAY;
				break;
			case SUN:
				ret = Calendar.SUNDAY;
				break;
			default:
				break;
		}
		return ret;
	}

    public static void cancelAlarm(Context context)
    {
        Intent intent = new Intent(context, WakeOrSleepReceiver.class);
        PendingIntent sender = PendingIntent.getBroadcast(context, 0, intent, 0);
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        alarmManager.cancel(sender);
    }
}

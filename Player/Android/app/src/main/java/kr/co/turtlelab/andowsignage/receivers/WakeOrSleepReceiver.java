package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public class WakeOrSleepReceiver extends BroadcastReceiver {

	@Override
	public void onReceive(Context context, Intent intent) {
		if (intent.getAction().equals("andowsignage.intent.action.WAKEUP")){
//
//			Intent _intent = new Intent();
//			_intent.setAction("rk.android.wakeupmode.action");
//			context.sendBroadcast(_intent);
//
//			try {
//				Thread.sleep(1000);
//			} catch (InterruptedException e) {
//				e.printStackTrace();
//			}
//
//			Intent playerIntent = new Intent(context, AndoWSignage.class);
//			playerIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
//			context.startActivity(playerIntent);
		} else if(intent.getAction().equals("andowsignage.intent.action.SLEEP")) {
			Intent _intent = new Intent();
			_intent.setAction("andowsignage.intent.action.STOP_AND_SLEEP");
            context.sendBroadcast(_intent);
		}
	}
}

package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.AndoWSignageApp.RP_STATUS;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class SystemMsgReceiver extends BroadcastReceiver {

	private static final String LOG_TAG = "SystemMsgReceiver";
    private static final String SYSTEM_DIALOG_REASON_KEY = "reason";
    private static final String SYSTEM_DIALOG_REASON_RECENT_APPS = "recentapps";
    private static final String SYSTEM_DIALOG_REASON_HOME_KEY = "homekey";
    private static final String SYSTEM_DIALOG_REASON_LOCK = "lock";
    private static final String SYSTEM_DIALOG_REASON_ASSIST = "assist";

    @Override
    public void onReceive(Context context, Intent intent) {
        String action = intent.getAction();
        //Log.i(LOG_TAG, "onReceive: action: " + action);
        if (action.equals(Intent.ACTION_CLOSE_SYSTEM_DIALOGS)) {
            // android.intent.action.CLOSE_SYSTEM_DIALOGS
            String reason = intent.getStringExtra(SYSTEM_DIALOG_REASON_KEY);

            if (SYSTEM_DIALOG_REASON_HOME_KEY.equals(reason)) {
            }
            else if (SYSTEM_DIALOG_REASON_RECENT_APPS.equals(reason)) {
            }
            else if (SYSTEM_DIALOG_REASON_ASSIST.equals(reason)) {
            }
            else if (SYSTEM_DIALOG_REASON_LOCK.equals(reason)) {
            }
        }
        else if(action.equals(Intent.ACTION_SCREEN_ON)) {
        	SystemUtils.runOnUiThread(new Runnable() {
				@Override
				public void run() {
					if(AndoWSignageApp.state.equalsIgnoreCase(RP_STATUS.playing.toString()))
						AndoWSignage.act.updateAndRestart(true);
				}
			});
        }
        else if(action.equals(Intent.ACTION_SCREEN_OFF)) {
        }
    }


}

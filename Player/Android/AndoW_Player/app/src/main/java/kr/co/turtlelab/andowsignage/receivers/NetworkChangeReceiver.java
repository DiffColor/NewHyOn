package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.AndoWSignageApp.RP_STATUS;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class NetworkChangeReceiver extends BroadcastReceiver {

	@Override
	public void onReceive(Context context, Intent intent) {

		int newState = NetworkUtils.getConnectivityStatus(context);
		int oldState = AndoWSignageApp.networkState;

		if(oldState == newState)
			return;
		
		AndoWSignageApp.networkState = newState;
		
		if(oldState<=0 && newState>0) {
//			if(LohbsPlayerApp.isRunning) {
//				Intent stoppingIntent = new Intent();
//				stoppingIntent.setAction("lohbs.intent.action.REFRESH_CS");
//	            context.sendBroadcast(stoppingIntent);
//			}
			
			SystemUtils.runOnUiThread(new Runnable() {
				@Override
				public void run() {
					if(AndoWSignageApp.state.equalsIgnoreCase(RP_STATUS.playing.toString()))
						if(AndoWSignageApp.isRunning)
							AndoWSignage.act.restartNetworkSrvs();
				}
			});
		}
	}
}

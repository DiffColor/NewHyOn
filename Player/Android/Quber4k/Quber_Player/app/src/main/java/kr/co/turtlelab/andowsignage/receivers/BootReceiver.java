package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import kr.co.turtlelab.andowsignage.AndoWSignage;

public class BootReceiver extends BroadcastReceiver {

	@Override	
	public void onReceive(Context context, Intent intent) {
			Intent playerIntent = new Intent(context, AndoWSignage.class);
			playerIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
		    context.startActivity(playerIntent);
	}
}

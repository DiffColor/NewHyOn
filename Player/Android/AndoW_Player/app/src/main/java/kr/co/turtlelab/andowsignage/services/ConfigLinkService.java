package kr.co.turtlelab.andowsignage.services;

import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.os.IBinder;

import kr.co.turtlelab.andowsignage.R;

public class ConfigLinkService extends Service {

	private NotificationManager mNotificationManager;
	private final int NOTIFICATION_ID = 1;
	//private final Messenger mMessenger = new Messenger(new IncomingMessageHandler()); // Target we publish for clients to send messages to IncomingHandler.

	private static boolean isRunning = false;
	
	@Override
	public void onCreate() {
		super.onCreate();

		showNotification();
		isRunning = true;
	}

	@Override
	public int onStartCommand(Intent intent, int flags, int startId) {		
		return START_STICKY;	// Run until explicitly stopped.
	}

	@Override
	public IBinder onBind(Intent intent) {
		return null;
		//return mMessenger.getBinder();
	}
	
	@Override
	public void onDestroy() {
		mNotificationManager.cancel(NOTIFICATION_ID); // Cancel the persistent notification.
		isRunning = false;
	}

	private void showNotification() {
		mNotificationManager = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);

		//PendingIntent contentIntent = PendingIntent.getActivity(this, 0, new Intent(this, PlayerConfig.class), 0);
		//PendingIntent contentIntent = PendingIntent.getActivity(this, 0, new Intent(this, PlayerPreferences.class), 0);
		Intent customIntent = new Intent("andowsignage.intent.action.CALL_SETTINGS");
		PendingIntent contentIntent = PendingIntent.getBroadcast(this, 0, customIntent, 0);
		
		//		Notification noti = new Notification(R.drawable.ic_launcher, text, System.currentTimeMillis());
//		noti.flags |= Notification.FLAG_ONGOING_EVENT;
//		noti.setLatestEventInfo(this, "Service!!", text, contentIntent);
		
		// Build notification
        // Actions are just fake
        Notification noti = new Notification.Builder(getApplicationContext())
            .setContentTitle(getText(R.string.config_noti_name))
            .setContentText(getText(R.string.config_noti_desc)).setSmallIcon(R.drawable.ic_launcher)
            .setWhen(0)
            .setContentIntent(contentIntent)
            .build();
        // hide the notification after its selected
        noti.flags |= Notification.FLAG_NO_CLEAR;
                
		mNotificationManager.notify(NOTIFICATION_ID, noti);
	}
}

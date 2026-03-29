package kr.co.turtlelab.andowsignage.tools;

import android.content.Context;
import android.os.PowerManager;

public class WakeLocker {

	private static PowerManager.WakeLock sCpuWakeLock;
	private final static String TAG = WakeLocker.class.getSimpleName();
	
	public static void stanbyMode(Context context) {
		if(sCpuWakeLock != null) {
			releaseCpuLock();
		}
		
		PowerManager pm = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
		sCpuWakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, TAG);
		sCpuWakeLock.acquire();
	}
	
	public static void releaseCpuLock() {
		if(sCpuWakeLock != null) {
			sCpuWakeLock.release();
			sCpuWakeLock = null;
		}
	}
	
	
}

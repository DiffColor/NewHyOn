package kr.co.turtlelab.andowsignage.tools;

import android.content.Context;
import android.content.Intent;
import android.util.Log;

public final class PowerApi {

    private static final String TAG = "PowerApi";
    private static final String ACTION_REBOOT = "ads.android.setreboot.action";
    private static final String ACTION_POWEROFF = "ads.android.setpoweroff.action";

    private PowerApi() {
    }

    public static void requestReboot(Context context) {
        sendAction(context, ACTION_REBOOT);
    }

    public static void requestPowerOff(Context context) {
        sendAction(context, ACTION_POWEROFF);
    }

    public static void setSleepMode(Context context, boolean enabled) {
        Log.d(TAG, "Sleep mode is controlled by broadcast/service flow. enabled=" + enabled);
    }

    public static Boolean queryHdmiCableState() {
        return null;
    }

    public static void pushScheduleToDevice() {
        Log.d(TAG, "pushScheduleToDevice is not supported on RK3229 power API.");
    }

    private static void sendAction(Context context, String action) {
        if (context == null) {
            Log.w(TAG, "Context is null, cannot send action: " + action);
            return;
        }
        try {
            Intent intent = new Intent(action);
            context.sendBroadcast(intent);
            Log.d(TAG, "Sent power action: " + action);
        } catch (Exception e) {
            Log.e(TAG, "Failed to send power action: " + action, e);
        }
    }
}

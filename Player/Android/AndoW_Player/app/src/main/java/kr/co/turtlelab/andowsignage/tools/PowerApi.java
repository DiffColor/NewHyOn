package kr.co.turtlelab.andowsignage.tools;

import android.content.Context;
import android.content.Intent;
import android.util.Log;

import java.util.List;

import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.WeeklyScheduleProvider;
import kr.co.turtlelab.andowsignage.tools.QuberAgentClient;

public final class PowerApi {

    private static final String TAG = "PowerApi";
    private static final String ACTION_REBOOT = "ads.android.setreboot.action";
    private static final String ACTION_POWEROFF = "ads.android.setpoweroff.action";
    private static final long HDMI_CACHE_MS = 60000L;

    private static Boolean cachedHdmiState = null;
    private static long cachedHdmiStateAt = 0L;

    private PowerApi() {
    }

    public static void requestReboot(Context context) {
        boolean handled = QuberAgentClient.get().requestReboot();
        if (handled) return;
        sendAction(context, ACTION_REBOOT);
    }

    public static void requestPowerOff(Context context) {
        boolean handled = QuberAgentClient.get().requestPowerOff();
        if (handled) return;
        sendAction(context, ACTION_POWEROFF);
    }

    public static void setSleepMode(Context context, boolean enabled) {
        boolean handled = QuberAgentClient.get().setSleepMode(enabled);
        if (handled) return;
        // Fallback: no-op for non-Quber 장비.
        Log.d(TAG, "Fallback sleepMode(not supported), enabled=" + enabled);
    }

    public static Boolean queryHdmiCableState() {
        long now = System.currentTimeMillis();
        synchronized (PowerApi.class) {
            if (cachedHdmiState != null && now - cachedHdmiStateAt < HDMI_CACHE_MS) {
                return cachedHdmiState;
            }
        }
        Boolean latest = QuberAgentClient.get().readHdmiCableConnected();
        if (latest != null) {
            synchronized (PowerApi.class) {
                cachedHdmiState = latest;
                cachedHdmiStateAt = System.currentTimeMillis();
            }
            return latest;
        }
        synchronized (PowerApi.class) {
            return cachedHdmiState;
        }
    }

    public static void pushScheduleToDevice() {
        List<WeeklyScheduleDataModel> schedules = WeeklyScheduleProvider.getWeeklyScheduleList();
        boolean ok = QuberAgentClient.get().pushWeeklySchedule(schedules);
        Log.d(TAG, "pushScheduleToDevice result=" + ok);
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

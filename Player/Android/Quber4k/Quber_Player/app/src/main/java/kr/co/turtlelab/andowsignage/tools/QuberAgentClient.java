package kr.co.turtlelab.andowsignage.tools;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.IBinder;
import android.os.RemoteException;
import android.util.Log;

import net.quber.qubersignageagent.IQuberCallback;
import net.quber.qubersignageagent.IQuberManager;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;

/**
 * Quber Signage Agent 연동용 헬퍼.
 * AIDL 기반 JSON 명령을 보내고 응답을 비동기/동기 모두 지원한다.
 */
public final class QuberAgentClient {

    private static final String TAG = "QuberAgentClient";
    private static final String ACTION_QUBER_AGENT = "net.quber.qubersignageagent.QUBER_AGENT_SERVICE";
    private static final String PACKAGE_QUBER_AGENT = "net.quber.qubersignageagent";
    private static final long CONNECT_TIMEOUT_MS = 1200L;
    private static final long RESPONSE_TIMEOUT_MS = 2500L;

    private static final String CMD_REBOOT = "215001";
    private static final String CMD_SLEEP_MODE_SET = "213017";
    private static final String CMD_SLEEP_MODE_READ = "211028";
    private static final String CMD_HDMI_CABLE_STATE_READ = "211024";
    private static final String CMD_SCHEDULE_SET = "213004";
    private static final String CMD_HDMI_ON_OFF = "213020";

    private static final QuberAgentClient INSTANCE = new QuberAgentClient();

    public static QuberAgentClient get() {
        return INSTANCE;
    }

    private final Map<String, ResponseWaiter> pendingResponses = new ConcurrentHashMap<>();
    private final SimpleDateFormat requestIdFormat =
            new SimpleDateFormat("yyyyMMddHHmmssSSS", Locale.US);

    private Context appContext;
    private volatile IQuberManager manager;
    private volatile boolean isBinding = false;
    private volatile CountDownLatch connectionLatch;

    private QuberAgentClient() {
    }

    public synchronized void initialize(Context context) {
        if (appContext != null) return;
        appContext = context.getApplicationContext();
        bindService();
    }

    public boolean requestReboot() {
        return sendCommand(CMD_REBOOT, null, false).success;
    }

    /**
     * HDMI 출력을 끄는 방식으로 전원 종료와 동일한 효과를 낸다.
     */
    public boolean requestPowerOff() {
        JSONObject params = new JSONObject();
        try {
            params.put("status", false);
        } catch (JSONException ignore) { }
        return sendCommand(CMD_HDMI_ON_OFF, params, false).success;
    }

    public boolean setSleepMode(boolean enabled) {
        JSONObject params = new JSONObject();
        try {
            // Spec 상 boolean/int 모두 허용된다.
            params.put("systemSleepMode", enabled);
        } catch (JSONException ignore) { }
        return sendCommand(CMD_SLEEP_MODE_SET, params, false).success;
    }

    public Boolean readSleepMode() {
        QuberResponse resp = sendCommand(CMD_SLEEP_MODE_READ, null, true);
        if (!resp.success || resp.body == null) return null;
        JSONObject params = resp.body.optJSONObject("params");
        if (params == null) return null;
        if (params.has("systemSleepMode")) {
            return params.optBoolean("systemSleepMode", false);
        }
        return null;
    }

    public Boolean readHdmiCableConnected() {
        QuberResponse resp = sendCommand(CMD_HDMI_CABLE_STATE_READ, null, true);
        if (!resp.success || resp.body == null) return null;
        JSONObject params = resp.body.optJSONObject("params");
        if (params == null) return null;
        if (params.has("connectStatus")) {
            return params.optBoolean("connectStatus", false);
        }
        return null;
    }

    public boolean pushWeeklySchedule(List<WeeklyScheduleDataModel> schedule) {
        if (schedule == null) {
            return false;
        }
        JSONArray params = new JSONArray();
        for (WeeklyScheduleDataModel item : schedule) {
            if (item == null) continue;
            JSONObject obj = new JSONObject();
            try {
                obj.put("dayOfWeek", mapDayOfWeek(item.getDay()));
                boolean onAir = item.getOnAir();
                if (onAir) {
                    int[] from = item.getFrom();
                    int[] to = item.getTo();
                    obj.put("wakeupTime", formatTime(from[0], from[1]));
                    obj.put("sleepTime", formatTime(to[0], to[1]));
                    obj.put("rebootTime", "-1");
                } else {
                    obj.put("wakeupTime", "-1");
                    obj.put("sleepTime", "-1");
                    obj.put("rebootTime", "-1");
                }
                params.put(obj);
            } catch (JSONException ignore) {
            }
        }
        QuberResponse resp = sendCommand(CMD_SCHEDULE_SET, params, false);
        return resp.success;
    }

    public boolean setHdmiOn(boolean on) {
        JSONObject params = new JSONObject();
        try {
            params.put("status", on);
        } catch (JSONException ignore) { }
        return sendCommand(CMD_HDMI_ON_OFF, params, false).success;
    }

    private QuberResponse sendCommand(String cmdCode, Object params, boolean expectResponse) {
        if (!ensureServiceConnected()) {
            return QuberResponse.failed();
        }
        String requestId = createRequestId();
        JSONObject payload = new JSONObject();
        try {
            payload.put("requestId", requestId);
            payload.put("cmdCode", cmdCode);
            if (params != null) {
                payload.put("params", params);
            }
        } catch (JSONException e) {
            Log.w(TAG, "Failed to build payload", e);
            return QuberResponse.failed();
        }

        ResponseWaiter waiter = null;
        if (expectResponse) {
            waiter = new ResponseWaiter();
            pendingResponses.put(requestId, waiter);
        }

        boolean sent = false;
        try {
            sent = manager.sendRequestCmd(payload.toString());
        } catch (RemoteException e) {
            Log.w(TAG, "sendRequestCmd remote exception", e);
        } catch (Exception e) {
            Log.w(TAG, "sendRequestCmd failed", e);
        }

        if (!sent) {
            if (waiter != null) pendingResponses.remove(requestId);
            return QuberResponse.failed();
        }

        if (!expectResponse) {
            return QuberResponse.success(null);
        }

        try {
            JSONObject response = waiter.await(RESPONSE_TIMEOUT_MS);
            return QuberResponse.success(response);
        } catch (Exception e) {
            pendingResponses.remove(requestId);
            Log.w(TAG, "Timeout waiting for response " + cmdCode, e);
            return QuberResponse.failed();
        }
    }

    private boolean ensureServiceConnected() {
        if (manager != null) return true;
        bindService();
        CountDownLatch latch = connectionLatch;
        if (latch != null) {
            try {
                latch.await(CONNECT_TIMEOUT_MS, TimeUnit.MILLISECONDS);
            } catch (InterruptedException ignored) {
            }
        }
        return manager != null;
    }

    private void bindService() {
        if (appContext == null || isBinding) return;
        isBinding = true;
        connectionLatch = new CountDownLatch(1);
        Intent intent = new Intent(ACTION_QUBER_AGENT);
        intent.setPackage(PACKAGE_QUBER_AGENT);
        boolean bound = appContext.bindService(intent, connection, Context.BIND_AUTO_CREATE);
        if (!bound) {
            connectionLatch.countDown();
            isBinding = false;
            Log.w(TAG, "bindService failed");
        }
    }

    private final ServiceConnection connection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName name, IBinder service) {
            manager = IQuberManager.Stub.asInterface(service);
            try {
                if (manager != null) {
                    manager.agentResponse(callback);
                }
            } catch (RemoteException e) {
                Log.w(TAG, "agentResponse registration failed", e);
            }
            isBinding = false;
            CountDownLatch latch = connectionLatch;
            if (latch != null) latch.countDown();
            Log.d(TAG, "Quber agent connected");
        }

        @Override
        public void onServiceDisconnected(ComponentName name) {
            manager = null;
            isBinding = false;
            bindService();
            Log.w(TAG, "Quber agent disconnected, rebinding");
        }
    };

    private final IQuberCallback.Stub callback = new IQuberCallback.Stub() {
        @Override
        public void responseListener(String jsonMsg) {
            try {
                JSONObject json = new JSONObject(jsonMsg);
                String responseId = json.optString("responseId", json.optString("requestId", null));
                if (responseId == null) return;
                ResponseWaiter waiter = pendingResponses.remove(responseId);
                if (waiter != null) {
                    waiter.complete(json);
                }
            } catch (JSONException e) {
                Log.w(TAG, "Invalid response json: " + jsonMsg, e);
            }
        }
    };

    private String createRequestId() {
        try {
            synchronized (requestIdFormat) {
                return requestIdFormat.format(System.currentTimeMillis());
            }
        } catch (Exception e) {
            return Long.toString(System.currentTimeMillis());
        }
    }

    private static int mapDayOfWeek(AndoWSignageApp.DAY_OF_WEEK day) {
        switch (day) {
            case MON:
                return 1;
            case TUE:
                return 2;
            case WED:
                return 3;
            case THU:
                return 4;
            case FRI:
                return 5;
            case SAT:
                return 6;
            case SUN:
            default:
                return 7;
        }
    }

    private static String formatTime(int hour, int minute) {
        return String.format(Locale.US, "%02d:%02d", hour, minute);
    }

    private static final class QuberResponse {
        final boolean success;
        final JSONObject body;

        private QuberResponse(boolean success, JSONObject body) {
            this.success = success;
            this.body = body;
        }

        static QuberResponse success(JSONObject body) {
            return new QuberResponse(true, body);
        }

        static QuberResponse failed() {
            return new QuberResponse(false, null);
        }
    }

    private static final class ResponseWaiter {
        private final CountDownLatch latch = new CountDownLatch(1);
        private volatile JSONObject response;

        void complete(JSONObject json) {
            response = json;
            latch.countDown();
        }

        JSONObject await(long timeoutMs) throws InterruptedException {
            latch.await(timeoutMs, TimeUnit.MILLISECONDS);
            return response;
        }
    }
}

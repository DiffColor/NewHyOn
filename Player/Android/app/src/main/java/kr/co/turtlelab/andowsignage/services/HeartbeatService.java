package kr.co.turtlelab.andowsignage.services;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;
import android.text.TextUtils;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.PowerApi;

public class HeartbeatService extends Service {

    public static final String EXTRA_INTERVAL_MS = "kr.co.turtlelab.andowsignage.services.EXTRA_HEARTBEAT_INTERVAL";
    public static final String ACTION_SEND_STOPPED = "kr.co.turtlelab.andowsignage.services.action.SEND_HEARTBEAT_STOPPED";
    private static final long DEFAULT_INTERVAL_MS = 5000L;
    private static final long MIN_INTERVAL_MS = 1000L;

    private final IBinder binder = new LocalBinder();
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private LightestTimer heartbeatTimer;
    private long intervalMs = DEFAULT_INTERVAL_MS;

    @Override
    public void onCreate() {
        super.onCreate();
        heartbeatTimer = new LightestTimer((int) intervalMs, this::scheduleHeartbeat);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        updateEndpointFromSettings();
        if (intent != null && ACTION_SEND_STOPPED.equals(intent.getAction())) {
            triggerHeartbeatStopNow();
            return START_STICKY;
        }
        if (intent != null && intent.hasExtra(EXTRA_INTERVAL_MS)) {
            long requested = intent.getLongExtra(EXTRA_INTERVAL_MS, intervalMs);
            updateIntervalInternal(requested);
        }
        startTimerIfNeeded();
        triggerHeartbeatNow();
        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        if (heartbeatTimer != null) {
            heartbeatTimer.stop();
        }
        executor.shutdownNow();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return binder;
    }

    private void scheduleHeartbeat() {
        executor.execute(this::publishHeartbeat);
    }

    private void startTimerIfNeeded() {
        if (heartbeatTimer == null) {
            heartbeatTimer = new LightestTimer((int) intervalMs, this::scheduleHeartbeat);
        }
        if (!heartbeatTimer.getIsTicking()) {
            heartbeatTimer.start();
        }
    }

    private void updateIntervalInternal(long requestedIntervalMs) {
        long clamped = Math.max(MIN_INTERVAL_MS, requestedIntervalMs);
        if (clamped == intervalMs) {
            return;
        }
        intervalMs = clamped;
        if (heartbeatTimer == null) {
            heartbeatTimer = new LightestTimer((int) intervalMs, this::scheduleHeartbeat);
            return;
        }
        if (heartbeatTimer.getIsTicking()) {
            heartbeatTimer.changeInterval(intervalMs);
        } else {
            heartbeatTimer = new LightestTimer((int) intervalMs, this::scheduleHeartbeat);
        }
    }

    private void triggerHeartbeatNow() {
        executor.execute(this::publishHeartbeat);
    }

    private void updateEndpointFromSettings() {
        String host = AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)
                ? AndoWSignageApp.MANUAL_IP
                : AndoWSignageApp.MANAGER_IP;
        if (!TextUtils.isEmpty(host)) {
            RethinkDbClient.getInstance().updateHost(host);
        }
    }

    public void publishHeartbeat() {
        String clientId = resolveClientGuid();
        if (TextUtils.isEmpty(clientId)) {
            return;
        }
        String status = AndoWSignageApp.state;
        int process = parseProcess(AndoWSignageApp.process);
        // 업데이트 큐 진행 중이면 Heartbeat에도 반영
        kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue queue = kr.co.turtlelab.andowsignage.dataproviders.UpdateQueueProvider.getLatestQueueSnapshot();
        if (queue != null && !TextUtils.equals(queue.getStatus(), kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract.Status.DONE)
                && !TextUtils.equals(queue.getStatus(), kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract.Status.FAILED)
                && !TextUtils.equals(queue.getStatus(), kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract.Status.CANCELLED)) {
            status = "updating";
            float dl = queue.getDownloadProgress();
            float vl = queue.getValidateProgress();
            float avg = (dl + vl) / 2f;
            process = Math.round(avg);
        }
        String version = AndoWSignageApp.version;
        String currentPage = AndoWSignage.currentPageName;
        Boolean quberHdmi = PowerApi.queryHdmiCableState();
        String hdmiState = quberHdmi != null
                ? Boolean.toString(quberHdmi)
                : Boolean.toString(!AndoWSignageApp.isSlept);

        RethinkDbClient.getInstance().sendHeartbeat(
                clientId,
                status,
                process,
                version,
                currentPage,
                hdmiState);
    }

    private void triggerHeartbeatStopNow() {
        executor.execute(this::publishHeartbeatStopped);
    }

    public void publishHeartbeatStopped() {
        String clientId = resolveClientGuid();
        if (TextUtils.isEmpty(clientId)) {
            return;
        }

        RethinkDbClient.getInstance().sendHeartbeatStopped(clientId, AndoWSignageApp.version);
    }

    private String resolveClientGuid() {
        RethinkDbClient client = RethinkDbClient.getInstance();
        String clientId = client.getCachedPlayerGuid();
        if (TextUtils.isEmpty(clientId)) {
            String storedPlayerName = client.getStoredPlayerName();
            if (!TextUtils.isEmpty(storedPlayerName)) {
                clientId = client.ensurePlayerGuid(storedPlayerName);
            }
        }
        if (TextUtils.isEmpty(clientId)) {
            clientId = client.ensurePlayerGuid();
        }
        return clientId;
    }


    private int parseProcess(String processValue) {
        if (TextUtils.isEmpty(processValue)) {
            return 0;
        }
        try {
            return Integer.parseInt(processValue);
        } catch (NumberFormatException ignore) {
            return 0;
        }
    }

    public class LocalBinder extends Binder {
        public HeartbeatService getService() {
            return HeartbeatService.this;
        }

        public void updateInterval(long intervalMillis) {
            updateIntervalInternal(intervalMillis);
            startTimerIfNeeded();
        }

        public void sendHeartbeatNow() {
            triggerHeartbeatNow();
        }
    }
}

package kr.co.turtlelab.andowsignage.services;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;
import android.text.TextUtils;
import android.util.Log;

import java.net.InetSocketAddress;
import java.net.Socket;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.update.UpdateHeartbeatState;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.PowerApi;

public class HeartbeatService extends Service {
    private static final String TAG = "HeartbeatService";
    private static final int RETHINK_PORT = 28015;
    private static final int CONNECT_TIMEOUT_MS = 1000;
    private static final long RECONNECT_SKIP_MS = 5000L;

    public static final String EXTRA_INTERVAL_MS = "kr.co.turtlelab.andowsignage.services.EXTRA_HEARTBEAT_INTERVAL";
    public static final String ACTION_SEND_STOPPED = "kr.co.turtlelab.andowsignage.services.action.SEND_HEARTBEAT_STOPPED";
    public static final String ACTION_SEND_NOW = "kr.co.turtlelab.andowsignage.services.action.SEND_HEARTBEAT_NOW";
    private static final String EXTRA_UPDATE_REVISION = "kr.co.turtlelab.andowsignage.services.extra.UPDATE_REVISION";
    private static final long DEFAULT_INTERVAL_MS = 5000L;
    private static final long MIN_INTERVAL_MS = 1000L;
    private static final long CLIENT_ID_REFRESH_MS = 5000L;
    private static final Object UPDATE_REPORT_LOCK = new Object();
    private static boolean terminalStopRequested = false;

    private final IBinder binder = new LocalBinder();
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private LightestTimer heartbeatTimer;
    private long intervalMs = DEFAULT_INTERVAL_MS;
    private SignalRClientService signalRClient;
    private volatile String cachedClientId = "";
    private volatile long lastClientIdResolveAt = 0L;
    private volatile String activeRethinkHost = "";
    private volatile long reconnectBlockedUntilMs = 0L;

    @Override
    public void onCreate() {
        super.onCreate();
        heartbeatTimer = new LightestTimer((int) intervalMs, this::scheduleHeartbeat);
        signalRClient = SignalRClientService.getShared(null);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_SEND_STOPPED.equals(intent.getAction())) {
            AndoWSignageApp.beginShutdown();
            executor.execute(() -> {
                ensureSignalRReady();
                publishHeartbeatStoppedAndStop();
                stopManagedServicesForShutdown();
                stopSelfResult(startId);
            });
            return START_NOT_STICKY;
        }
        if (intent != null && ACTION_SEND_NOW.equals(intent.getAction())) {
            long expectedRevision = intent.getLongExtra(EXTRA_UPDATE_REVISION, 0L);
            executor.execute(() -> {
                if (isTerminalStopRequested()) {
                    return;
                }
                ensureSignalRReady();
                publishHeartbeat(true, true, expectedRevision);
            });
            return START_NOT_STICKY;
        }
        AndoWSignageApp.clearShutdownInProgress();
        clearTerminalStopRequested();
        executor.execute(this::ensureSignalRReady);
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
        executor.execute(() -> publishHeartbeat(false, false, 0L));
    }

    private void ensureSignalRReady() {
        updateEndpointFromSettings();
        if (signalRClient != null) {
            signalRClient.start();
        }
    }

    private void updateEndpointFromSettings() {
        String host;
        String dataServerIp = LocalSettingsProvider.getDataServerIp();
        if (!TextUtils.isEmpty(dataServerIp)) {
            host = dataServerIp;
        } else if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            host = AndoWSignageApp.MANUAL_IP;
        } else {
            host = AndoWSignageApp.MANAGER_IP;
        }
        activeRethinkHost = host == null ? "" : host;
        if (!TextUtils.isEmpty(host)) {
            RethinkDbClient.getInstance().updateHost(host);
        }
    }

    public void publishHeartbeat() {
        publishHeartbeat(false, false, 0L);
    }

    private void publishHeartbeat(boolean forceGuidRefresh) {
        publishHeartbeat(forceGuidRefresh, false, 0L);
    }

    private void publishHeartbeat(boolean forceGuidRefresh, boolean forceUpdateReport, long expectedRevision) {
        if (isTerminalStopRequested()) {
            return;
        }
        String clientId = resolveClientId(forceGuidRefresh);
        if (TextUtils.isEmpty(clientId)) {
            return;
        }
        String playerName = resolvePlayerName();
        String status = AndoWSignageApp.state;
        int process = parseProcess(AndoWSignageApp.process);
        SignalRClientService.HeartbeatGuard heartbeatGuard = null;
        UpdateHeartbeatState.Snapshot updateSnapshot = UpdateHeartbeatState.captureForPublish(forceUpdateReport, expectedRevision);
        if (updateSnapshot.hasUpdatePayload) {
            heartbeatGuard = () -> !isTerminalStopRequested() && UpdateHeartbeatState.canSend(updateSnapshot.revision);
            status = updateSnapshot.status;
            process = updateSnapshot.progress;
        } else if (updateSnapshot.suppressNormalHeartbeat) {
            return;
        } else if (expectedRevision > 0L) {
            return;
        }
        String version = AndoWSignageApp.version;
        String currentPage = AndoWSignage.currentPageName;
        Boolean hdmiConnected = PowerApi.queryHdmiCableState();
        String hdmiState = hdmiConnected != null
                ? Boolean.toString(hdmiConnected)
                : Boolean.toString(!AndoWSignageApp.isSlept);
        if (signalRClient != null) {
            SignalRClientService.HeartbeatPayload payload = SignalRClientService.HeartbeatPayload.create(
                    clientId,
                    status,
                    process,
                    version,
                    currentPage,
                    Boolean.parseBoolean(hdmiState));
            payload.PlayerName = playerName;
            signalRClient.sendHeartbeat(payload, heartbeatGuard);
        }
    }

    public void publishHeartbeatStoppedAndStop() {
        synchronized (UPDATE_REPORT_LOCK) {
            terminalStopRequested = true;
        }
        UpdateHeartbeatState.reset();
        String clientId = resolveClientId(true);
        if (TextUtils.isEmpty(clientId)) {
            if (signalRClient != null) {
                signalRClient.stop();
            }
            return;
        }
        String playerName = resolvePlayerName();
        if (signalRClient != null) {
            SignalRClientService.HeartbeatPayload payload = SignalRClientService.HeartbeatPayload.create(
                    clientId,
                    "stopped",
                    0,
                    AndoWSignageApp.version,
                    "",
                    false);
            payload.PlayerName = playerName;
            signalRClient.sendStoppedAndStop(payload);
        }
    }

    private void stopManagedServicesForShutdown() {
        AndoWSignageApp app = AndoWSignageApp.getApplication();
        if (app == null) {
            return;
        }

        try {
            app.stopService(new Intent(app, UpdateManagerService.class));
        } catch (Exception ex) {
            Log.w(TAG, "stopManagedServicesForShutdown: failed to stop UpdateManagerService", ex);
        }

        try {
            app.stopService(new Intent(app, PowerService.class));
        } catch (Exception ex) {
            Log.w(TAG, "stopManagedServicesForShutdown: failed to stop PowerService", ex);
        }

        try {
            app.stopService(new Intent(app, ConfigLinkService.class));
        } catch (Exception ex) {
            Log.w(TAG, "stopManagedServicesForShutdown: failed to stop ConfigLinkService", ex);
        }
    }

    public static void reportUpdateProgress(String status, float progress, boolean force) {
        synchronized (UPDATE_REPORT_LOCK) {
            if (terminalStopRequested) {
                return;
            }
        }
        UpdateHeartbeatState.DispatchRequest request = UpdateHeartbeatState.reportProgress(status, progress, force);
        if (request.shouldSendUpdateNow) {
            requestServiceAction(ACTION_SEND_NOW, request.revision);
        }
    }

    public static void reportQueueStatus(String status, float progress, boolean isScheduleQueue) {
        synchronized (UPDATE_REPORT_LOCK) {
            if (terminalStopRequested) {
                return;
            }
        }
        UpdateHeartbeatState.DispatchRequest request = UpdateHeartbeatState.reportQueueStatus(status, progress, isScheduleQueue);
        if (request.shouldSendUpdateNow) {
            requestServiceAction(ACTION_SEND_NOW, request.revision);
        } else if (request.shouldSendNormalNow) {
            requestServiceAction(ACTION_SEND_NOW);
        }
    }

    private static void requestServiceAction(String action) {
        requestServiceAction(action, 0L);
    }

    private static void requestServiceAction(String action, long expectedRevision) {
        AndoWSignageApp app = AndoWSignageApp.getApplication();
        if (app == null || TextUtils.isEmpty(action)) {
            return;
        }
        Intent intent = new Intent(app, HeartbeatService.class);
        intent.setAction(action);
        if (expectedRevision > 0L) {
            intent.putExtra(EXTRA_UPDATE_REVISION, expectedRevision);
        }
        app.startService(intent);
    }

    private static void clearTerminalStopRequested() {
        synchronized (UPDATE_REPORT_LOCK) {
            terminalStopRequested = false;
        }
    }

    private static boolean isTerminalStopRequested() {
        synchronized (UPDATE_REPORT_LOCK) {
            return terminalStopRequested;
        }
    }


    private int parseProcess(String processValue) {
        if (TextUtils.isEmpty(processValue)) {
            return 0;
        }
        try {
            return Integer.parseInt(processValue);
        } catch (NumberFormatException ex) {
            Log.w(TAG, "parseProcess: invalid process value=" + processValue, ex);
            return 0;
        }
    }

    private String resolveClientId(boolean forceRefresh) {
        long now = System.currentTimeMillis();
        if (!forceRefresh && !TextUtils.isEmpty(cachedClientId) && (now - lastClientIdResolveAt) < CLIENT_ID_REFRESH_MS) {
            return cachedClientId;
        }
        String previousClientId = cachedClientId;
        String resolved = RethinkDbClient.getInstance().getStoredPlayerGuid();
        if (TextUtils.isEmpty(resolved) && canAttemptCommunicationNow() && canReachRethink(activeRethinkHost)) {
            resolved = RethinkDbClient.getInstance().ensurePlayerGuid();
        } else if (TextUtils.isEmpty(resolved)) {
            blockReconnectTemporarily();
        }
        if (TextUtils.isEmpty(resolved)) {
            resolved = previousClientId;
        }
        if (!TextUtils.isEmpty(resolved)) {
            boolean guidChanged = !TextUtils.isEmpty(previousClientId)
                    && !previousClientId.equalsIgnoreCase(resolved);
            cachedClientId = resolved;
            lastClientIdResolveAt = now;
            if (guidChanged && signalRClient != null) {
                signalRClient.reconnect();
            }
        }
        return resolved;
    }

    private boolean canAttemptCommunicationNow() {
        return System.currentTimeMillis() >= reconnectBlockedUntilMs;
    }

    private void blockReconnectTemporarily() {
        reconnectBlockedUntilMs = System.currentTimeMillis() + RECONNECT_SKIP_MS;
    }

    private boolean canReachRethink(String host) {
        if (TextUtils.isEmpty(host)) {
            return false;
        }
        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress(host, RETHINK_PORT), CONNECT_TIMEOUT_MS);
            return true;
        } catch (Exception ex) {
            return false;
        }
    }

    private String resolvePlayerName() {
        String stored = RethinkDbClient.getInstance().getStoredPlayerName();
        if (!TextUtils.isEmpty(stored)) {
            return stored;
        }
        return TextUtils.isEmpty(AndoWSignageApp.PLAYER_ID) ? "" : AndoWSignageApp.PLAYER_ID;
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

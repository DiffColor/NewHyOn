package kr.co.turtlelab.andowsignage.services;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;
import android.text.TextUtils;
import android.util.Log;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileReader;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.update.UpdateHeartbeatState;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;

public class HeartbeatService extends Service {
    private static final String TAG = "HeartbeatService";
    private static final int RETHINK_PORT = 28015;
    private static final int CONNECT_TIMEOUT_MS = 1000;
    private static final long RECONNECT_SKIP_MS = 5000L;

    public static final String EXTRA_INTERVAL_MS = "kr.co.turtlelab.andowsignage.services.EXTRA_HEARTBEAT_INTERVAL";
    public static final String ACTION_SEND_STOPPED = "kr.co.turtlelab.andowsignage.services.action.SEND_HEARTBEAT_STOPPED";
    public static final String ACTION_SEND_NOW = "kr.co.turtlelab.andowsignage.services.action.SEND_HEARTBEAT_NOW";
    private static final String EXTRA_UPDATE_REVISION = "kr.co.turtlelab.andowsignage.services.extra.UPDATE_REVISION";
    public static final String EXTRA_SHUTDOWN_TOKEN = "kr.co.turtlelab.andowsignage.services.extra.SHUTDOWN_TOKEN";
    private static final long DEFAULT_INTERVAL_MS = 5000L;
    private static final long MIN_INTERVAL_MS = 1000L;
    private static final long CLIENT_ID_REFRESH_MS = 5000L;
    private static final Object UPDATE_REPORT_LOCK = new Object();
    private static boolean terminalStopRequested = false;

    private final IBinder binder = new LocalBinder();
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final ExecutorService hdmiStateExecutor = Executors.newSingleThreadExecutor();
    private final AtomicBoolean hdmiStateRefreshInFlight = new AtomicBoolean(false);
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
        requestHdmiStateRefresh();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_SEND_STOPPED.equals(intent.getAction())) {
            final long shutdownToken = intent.getLongExtra(EXTRA_SHUTDOWN_TOKEN, 0L);
            executor.execute(() -> {
                if (!AndoWSignageApp.matchesShutdownToken(shutdownToken)) {
                    stopSelfResult(startId);
                    return;
                }
                ensureSignalRReady();
                if (!AndoWSignageApp.matchesShutdownToken(shutdownToken)) {
                    stopSelfResult(startId);
                    return;
                }
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
        hdmiStateExecutor.shutdownNow();
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
        RethinkDbClient.getInstance().syncCurrentDeviceInfo();
        String playerName = resolvePlayerName();
        String status = resolvePlaybackAwareStatus(AndoWSignageApp.state);
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
        requestHdmiStateRefresh();
        String hdmiState = Boolean.toString(AndoWSignageApp.isHdmiConnected());
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

    /**
     * Refreshes the cached monitor connection state without blocking heartbeat publication.
     */
    private void requestHdmiStateRefresh() {
        if (!hdmiStateRefreshInFlight.compareAndSet(false, true)) {
            return;
        }
        try {
            hdmiStateExecutor.execute(() -> {
                try {
                    Boolean refreshedState = readHdmiSwitchState();
                    if (refreshedState != null) {
                        AndoWSignageApp.updateHdmiConnected(refreshedState);
                    }
                } catch (Exception e) {
                    Log.w(TAG, "HDMI state refresh failed", e);
                } finally {
                    hdmiStateRefreshInFlight.set(false);
                }
            });
        } catch (Exception e) {
            hdmiStateRefreshInFlight.set(false);
            Log.w(TAG, "Failed to schedule HDMI state refresh", e);
        }
    }

    private Boolean readHdmiSwitchState() {
        Boolean disp0State = readDisplayOutputState("/proc/msp/disp0");
        Boolean disp1State = readDisplayOutputState("/proc/msp/disp1");
        Boolean panelState = readPanelOutputState("/proc/msp/panel");
        if (disp0State == null && disp1State == null && panelState == null) {
            return null;
        }
        return Boolean.TRUE.equals(disp0State)
                || Boolean.TRUE.equals(disp1State)
                || Boolean.TRUE.equals(panelState);
    }

    private Boolean readDisplayOutputState(String path) {
        String displayState = readProcValue(path);
        if (TextUtils.isEmpty(displayState)) {
            return null;
        }

        String interfaceType = extractProcField(displayState, "chg_type/chg_step/intftype");
        if (TextUtils.isEmpty(interfaceType)) {
            return null;
        }

        String normalizedInterfaceType = interfaceType.toUpperCase(Locale.US);
        if ("BUTT".equals(normalizedInterfaceType)) {
            return false;
        }

        String openEnableState = extractProcField(displayState, "open/enable/lost_int/cur_int/disp_state");
        String dispEnableState = extractProcField(displayState, "disp_en/update/master/slave/attach");
        boolean isDisplayEnabled = containsYesToken(openEnableState, 0)
                && containsYesToken(openEnableState, 1)
                && openEnableState.toUpperCase(Locale.US).contains("/ENABLE")
                && containsYesToken(dispEnableState, 0);
        return isDisplayEnabled;
    }

    private Boolean readPanelOutputState(String path) {
        String panelState = readProcValue(path);
        if (TextUtils.isEmpty(panelState)) {
            return null;
        }
        String normalizedState = panelState.toUpperCase(Locale.US);
        return normalizedState.contains("INTF_TYPE   :LVDS")
                || normalizedState.contains("INTF_TYPE   :HDMI")
                || normalizedState.contains("INTF_TYPE   :EDP")
                || normalizedState.contains("INTF_TYPE   :MIPI")
                || normalizedState.contains("POWER_ON    :TRUE")
                || normalizedState.contains("INTF_ENABLE :TRUE")
                || normalizedState.contains("BL_ENABLE   :TRUE");
    }

    private String readProcValue(String path) {
        File procFile = new File(path);
        if (!procFile.exists()) {
            return "";
        }
        try (BufferedReader reader = new BufferedReader(new FileReader(procFile))) {
            StringBuilder builder = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) {
                builder.append(line).append('\n');
            }
            return builder.toString();
        } catch (Exception e) {
            Log.w(TAG, "Failed to read display proc file: " + path, e);
            return "";
        }
    }

    private String extractProcField(String raw, String fieldName) {
        if (TextUtils.isEmpty(raw) || TextUtils.isEmpty(fieldName)) {
            return "";
        }
        String[] lines = raw.split("\n");
        for (String line : lines) {
            int separatorIndex = line.indexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }
            String key = line.substring(0, separatorIndex).trim();
            if (!fieldName.equalsIgnoreCase(key)) {
                continue;
            }
            return line.substring(separatorIndex + 1).trim();
        }
        return "";
    }

    private boolean containsYesToken(String value, int tokenIndex) {
        if (TextUtils.isEmpty(value)) {
            return false;
        }
        String[] tokens = value.split("/");
        if (tokenIndex < 0 || tokenIndex >= tokens.length) {
            return false;
        }
        return "YES".equalsIgnoreCase(tokens[tokenIndex].trim());
    }

    private String resolvePlaybackAwareStatus(String rawStatus) {
        String normalized = TextUtils.isEmpty(rawStatus)
                ? AndoWSignageApp.RP_STATUS.stopped.toString()
                : rawStatus.trim().toLowerCase(Locale.US);

        if (AndoWSignageApp.RP_STATUS.updating.toString().equals(normalized)) {
            return normalized;
        }

        if (AndoWSignage.hasActiveContentPlaybackForHeartbeat()) {
            return AndoWSignageApp.RP_STATUS.playing.toString();
        }

        return normalized;
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
        String playerName = resolvePlayerName();
        String resolved;
        if (canAttemptCommunicationNow() && canReachRethink(activeRethinkHost)) {
            resolved = RethinkDbClient.getInstance().ensurePlayerGuid(playerName);
            if (TextUtils.isEmpty(resolved)) {
                cachedClientId = "";
                lastClientIdResolveAt = now;
                return "";
            }
        } else {
            resolved = RethinkDbClient.getInstance().getStoredPlayerGuid();
            if (TextUtils.isEmpty(resolved)) {
                blockReconnectTemporarily();
                resolved = previousClientId;
            }
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

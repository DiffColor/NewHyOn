package kr.co.turtlelab.andowsignage.services;

import android.text.TextUtils;
import android.util.Log;

import com.google.gson.Gson;
import com.google.gson.annotations.SerializedName;
import com.microsoft.signalr.HubConnection;
import com.microsoft.signalr.HubConnectionBuilder;
import com.microsoft.signalr.HubConnectionState;
import com.microsoft.signalr.TransportEnum;

import java.net.URLEncoder;
import java.util.Locale;
import java.util.concurrent.LinkedBlockingDeque;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkModels;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class SignalRClientService {
    private static final String TAG = "SignalRClientService";

    public interface HeartbeatGuard {
        boolean shouldSend();
    }

    public interface Listener {
        void onCommand(String command);
        void onCommandEnvelope(SignalRCommandEnvelope envelope);
        void onWeeklySchedule(RethinkModels.WeeklyScheduleRecord weekly);
    }

    private static final int DEFAULT_PORT = 5000;
    private static final String DEFAULT_HUB_PATH = "/Data";
    private static final long RECONNECT_DELAY_MS = 5000L;
    private static final long CONNECT_TIMEOUT_MS = 15000L;
    private static final long HEARTBEAT_TIMEOUT_MS = 5000L;
    private static final int MAX_SIGNALR_ACTION_QUEUE = 10;

    private Listener listener;
    private final Gson gson;
    private final ThreadPoolExecutor actionExecutor = new ThreadPoolExecutor(
            1,
            1,
            0L,
            TimeUnit.MILLISECONDS,
            new LinkedBlockingDeque<>(MAX_SIGNALR_ACTION_QUEUE),
            runnable -> {
                Thread thread = new Thread(runnable, "SignalRActionQueue");
                thread.setDaemon(true);
                return thread;
            });
    private final ScheduledExecutorService reconnectScheduler = java.util.concurrent.Executors.newSingleThreadScheduledExecutor(runnable -> {
        Thread thread = new Thread(runnable, "SignalRReconnectScheduler");
        thread.setDaemon(true);
        return thread;
    });
    private final Object syncRoot = new Object();
    private final AtomicBoolean stopping = new AtomicBoolean(false);
    private final AtomicBoolean reconnectScheduled = new AtomicBoolean(false);
    private final AtomicBoolean terminalHeartbeatMode = new AtomicBoolean(false);
    private final AtomicLong heartbeatGeneration = new AtomicLong(0L);

    private HubConnection connection;
    private ScheduledFuture<?> reconnectFuture;

    private static SignalRClientService shared;

    public SignalRClientService(Listener listener, Gson gson) {
        this.listener = listener;
        this.gson = gson == null ? new Gson() : gson;
        actionExecutor.setRejectedExecutionHandler((runnable, executor) -> {
            if (executor.isShutdown()) {
                throw new RejectedExecutionException("SignalR action queue is shut down");
            }
            Runnable dropped = executor.getQueue().poll();
            Log.w(TAG, "SignalR action queue full. dropped oldest action=" + describeAction(dropped));
            executor.execute(runnable);
        });
    }

    public static synchronized SignalRClientService getShared(Gson gson) {
        if (shared == null) {
            shared = new SignalRClientService(null, gson);
        }
        return shared;
    }

    public void setListener(Listener listener) {
        this.listener = listener;
    }

    public void start() {
        stopping.set(false);
        terminalHeartbeatMode.set(false);
        enqueueAction("start", this::ensureConnectedInternal);
    }

    public void stop() {
        stopping.set(true);
        invalidateHeartbeatGeneration();
        cancelReconnectSchedule();
        clearPendingActions();
        enqueueAction("stop", this::stopInternal);
    }

    public void reconnect() {
        stopping.set(false);
        terminalHeartbeatMode.set(false);
        cancelReconnectSchedule();
        clearPendingActions();
        enqueueAction("reconnect", () -> {
            stopInternal();
            ensureConnectedInternal();
        });
    }

    public void sendHeartbeat(HeartbeatPayload payload) {
        sendHeartbeat(payload, null);
    }

    public void sendHeartbeat(HeartbeatPayload payload, HeartbeatGuard guard) {
        if (payload == null) {
            return;
        }
        if (terminalHeartbeatMode.get()) {
            return;
        }
        long generation = heartbeatGeneration.get();
        enqueueAction("heartbeat", () -> sendHeartbeatInternal(payload, guard, generation));
    }

    public void sendStoppedAndStop(HeartbeatPayload payload) {
        terminalHeartbeatMode.set(true);
        invalidateHeartbeatGeneration();
        cancelReconnectSchedule();
        clearPendingActions();
        try {
            sendTerminalHeartbeat(payload);
        } finally {
            stopping.set(true);
            stopInternal();
        }
    }

    private void ensureConnectedInternal() {
        if (stopping.get()) {
            return;
        }

        String baseUrl = buildUrl();
        if (TextUtils.isEmpty(baseUrl)) {
            Log.w(TAG, "ensureConnectedInternal: empty SignalR url");
            return;
        }

        String url = appendQueryString(baseUrl, buildQueryString());
        HubConnection existingConnection;
        synchronized (syncRoot) {
            existingConnection = connection;
            if (shouldKeepCurrentConnection(existingConnection)) {
                return;
            }
        }

        HubConnection nextConnection = buildConnection(url);
        HubConnection previous;
        synchronized (syncRoot) {
            previous = connection;
            connection = nextConnection;
        }
        safeStopConnection(previous);

        try {
            startConnection(nextConnection);
            reconnectScheduled.set(false);
        } catch (Exception ex) {
            if (isOperationCancelled(ex)) {
                Log.w(TAG, "ensureConnectedInternal: start cancelled. url=" + url, ex);
            } else {
                Log.e(TAG, "ensureConnectedInternal: start failed, scheduling reconnect. url=" + url, ex);
            }
            synchronized (syncRoot) {
                if (connection == nextConnection) {
                    connection = null;
                }
            }
            safeStopConnection(nextConnection);
            if (!stopping.get()) {
                scheduleReconnect();
            }
        }
    }

    private HubConnection buildConnection(String url) {
        HubConnection hubConnection = HubConnectionBuilder.create(url)
                // 기존 안드로이드 플레이어는 장비/망 제약을 피하기 위해 롱폴링으로 동작했다.
                .withTransport(TransportEnum.LONG_POLLING)
                .build();

        hubConnection.on("ReceiveMessage", this::handleMessage, SignalRMessage.class);
        hubConnection.onClosed(exception -> {
            if (!stopping.get()) {
                synchronized (syncRoot) {
                    if (connection == hubConnection) {
                        connection = null;
                    }
                }
                if (exception != null) {
                    Log.e(TAG, "SignalR connection closed with error", exception);
                }
                scheduleReconnect();
            }
        });

        return hubConnection;
    }

    private void scheduleReconnect() {
        if (stopping.get()) {
            return;
        }
        if (reconnectScheduled.getAndSet(true)) {
            return;
        }
        synchronized (syncRoot) {
            if (reconnectFuture != null) {
                reconnectFuture.cancel(false);
            }
            reconnectFuture = reconnectScheduler.schedule(() -> {
                reconnectScheduled.set(false);
                if (!stopping.get()) {
                    enqueueAction("scheduled-reconnect", this::ensureConnectedInternal);
                }
            }, RECONNECT_DELAY_MS, TimeUnit.MILLISECONDS);
        }
    }

    private void safeStopConnection(HubConnection hubConnection) {
        if (hubConnection == null) {
            return;
        }
        try {
            hubConnection.stop().blockingAwait();
        } catch (Exception ex) {
            Log.w(TAG, "safeStopConnection: failed to stop hub connection", ex);
        }
        try {
            hubConnection.close();
        } catch (Exception ex) {
            Log.w(TAG, "safeStopConnection: failed to close hub connection", ex);
        }
    }

    private void stopInternal() {
        HubConnection local;
        synchronized (syncRoot) {
            local = connection;
            connection = null;
        }
        safeStopConnection(local);
    }

    private void startConnection(HubConnection local) {
        if (Thread.interrupted()) {
            Log.w(TAG, "startConnection: clearing stale interrupted flag before connect.");
        }
        local.start()
                .timeout(CONNECT_TIMEOUT_MS, TimeUnit.MILLISECONDS)
                .blockingAwait();
    }

    private boolean shouldKeepCurrentConnection(HubConnection local) {
        if (local == null) {
            return false;
        }
        HubConnectionState state = local.getConnectionState();
        return state == HubConnectionState.CONNECTED || state == HubConnectionState.CONNECTING;
    }

    private void sendHeartbeatInternal(HeartbeatPayload payload, HeartbeatGuard guard, long generation) {
        if (stopping.get()) {
            return;
        }
        if (!isCurrentHeartbeatGeneration(generation)) {
            return;
        }
        if (guard != null && !guard.shouldSend()) {
            return;
        }

        HubConnection localConn;
        synchronized (syncRoot) {
            localConn = connection;
        }

        if (localConn == null || localConn.getConnectionState() != HubConnectionState.CONNECTED) {
            scheduleReconnect();
            return;
        }

        try {
            if (!isCurrentHeartbeatGeneration(generation)) {
                return;
            }
            if (guard != null && !guard.shouldSend()) {
                return;
            }
            localConn.invoke("ReportHeartbeat", payload)
                    .timeout(HEARTBEAT_TIMEOUT_MS, TimeUnit.MILLISECONDS)
                    .blockingAwait();
        } catch (Exception ex) {
            Log.e(TAG, "sendHeartbeat: ReportHeartbeat invoke failed", ex);
            synchronized (syncRoot) {
                if (connection == localConn) {
                    connection = null;
                }
            }
            safeStopConnection(localConn);
            scheduleReconnect();
        }
    }

    private void sendTerminalHeartbeat(HeartbeatPayload payload) {
        if (payload == null) {
            return;
        }

        HubConnection localConn;
        synchronized (syncRoot) {
            localConn = connection;
        }

        if (localConn == null || localConn.getConnectionState() != HubConnectionState.CONNECTED) {
            Log.w(TAG, "sendTerminalHeartbeat: connection unavailable");
            return;
        }

        try {
            localConn.invoke("ReportHeartbeat", payload)
                    .timeout(HEARTBEAT_TIMEOUT_MS, TimeUnit.MILLISECONDS)
                    .blockingAwait();
        } catch (Exception ex) {
            Log.e(TAG, "sendTerminalHeartbeat: ReportHeartbeat invoke failed", ex);
        }
    }

    private void cancelReconnectSchedule() {
        reconnectScheduled.set(false);
        synchronized (syncRoot) {
            if (reconnectFuture != null) {
                reconnectFuture.cancel(false);
                reconnectFuture = null;
            }
        }
    }

    private void clearPendingActions() {
        actionExecutor.getQueue().clear();
    }

    private void invalidateHeartbeatGeneration() {
        heartbeatGeneration.incrementAndGet();
    }

    private boolean isCurrentHeartbeatGeneration(long generation) {
        return generation == heartbeatGeneration.get() && !terminalHeartbeatMode.get();
    }

    private void enqueueAction(String label, Runnable action) {
        try {
            actionExecutor.execute(new NamedAction(label, action));
        } catch (RejectedExecutionException ex) {
            Log.e(TAG, "enqueueAction failed. label=" + label, ex);
        }
    }

    private static String describeAction(Runnable runnable) {
        if (runnable instanceof NamedAction) {
            return ((NamedAction) runnable).label;
        }
        return runnable == null ? "none" : runnable.getClass().getSimpleName();
    }

    private static final class NamedAction implements Runnable {
        private final String label;
        private final Runnable delegate;

        private NamedAction(String label, Runnable delegate) {
            this.label = label;
            this.delegate = delegate;
        }

        @Override
        public void run() {
            delegate.run();
        }
    }

    private boolean isOperationCancelled(Throwable throwable) {
        Throwable current = throwable;
        while (current != null) {
            if (current instanceof InterruptedException) {
                return true;
            }
            String message = current.getMessage();
            if (!TextUtils.isEmpty(message) && message.toLowerCase(Locale.US).contains("operation was cancelled")) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private void handleMessage(SignalRMessage message) {
        if (message == null) {
            return;
        }

        if ("CommandQueue".equalsIgnoreCase(message.DataType)) {
            SignalRCommandEnvelope envelope = extractCommandEnvelope(message);
            if (envelope != null && listener != null) {
                listener.onCommandEnvelope(envelope);
            }
            return;
        }

        if ("StateMessage".equalsIgnoreCase(message.DataType)) {
            return;
        }

        if ("WeeklySchedule".equalsIgnoreCase(message.DataType)
                || "weekly-schedule-updated".equalsIgnoreCase(message.Command)) {
            RethinkModels.WeeklyScheduleRecord weekly = extractWeeklySchedule(message);
            if (weekly != null && listener != null) {
                listener.onWeeklySchedule(weekly);
            }
            return;
        }

        String command = extractCommand(message);
        if (!TextUtils.isEmpty(command) && listener != null) {
            listener.onCommand(command);
        }
    }

    private SignalRCommandEnvelope extractCommandEnvelope(SignalRMessage message) {
        if (message == null || message.Data == null) {
            return null;
        }
        try {
            if (message.Data instanceof SignalRCommandEnvelope) {
                return (SignalRCommandEnvelope) message.Data;
            }
            if (message.Data instanceof String) {
                String raw = (String) message.Data;
                if (TextUtils.isEmpty(raw)) {
                    return null;
                }
                return gson.fromJson(raw, SignalRCommandEnvelope.class);
            }
            String json = gson.toJson(message.Data);
            if (TextUtils.isEmpty(json)) {
                return null;
            }
            return gson.fromJson(json, SignalRCommandEnvelope.class);
        } catch (Exception ex) {
            Log.w(TAG, "extractCommandEnvelope: failed to parse message data", ex);
            return null;
        }
    }

    private String extractCommand(SignalRMessage message) {
        if (message == null) {
            return null;
        }
        String command = message.Command == null ? "" : message.Command;
        String dataCommand = null;
        if (message.Data instanceof String) {
            dataCommand = (String) message.Data;
        }
        if ("Update".equalsIgnoreCase(command)) {
            if (!TextUtils.isEmpty(dataCommand)) {
                command = dataCommand;
            } else {
                command = "updatelist";
            }
        }
        if (TextUtils.isEmpty(command)) {
            return null;
        }
        return command.trim().toLowerCase(Locale.US);
    }

    private RethinkModels.WeeklyScheduleRecord extractWeeklySchedule(SignalRMessage message) {
        if (message == null || message.Data == null) {
            return null;
        }
        try {
            if (message.Data instanceof RethinkModels.WeeklyScheduleRecord) {
                return (RethinkModels.WeeklyScheduleRecord) message.Data;
            }
            if (message.Data instanceof String) {
                String raw = (String) message.Data;
                if (TextUtils.isEmpty(raw)) {
                    return null;
                }
                return gson.fromJson(raw, RethinkModels.WeeklyScheduleRecord.class);
            }
            String json = gson.toJson(message.Data);
            if (TextUtils.isEmpty(json)) {
                return null;
            }
            return gson.fromJson(json, RethinkModels.WeeklyScheduleRecord.class);
        } catch (Exception ex) {
            Log.w(TAG, "extractWeeklySchedule: failed to parse weekly payload", ex);
            return null;
        }
    }

    private String buildUrl() {
        String address = resolveServerAddress();
        if (TextUtils.isEmpty(address)) {
            return null;
        }
        int port = resolvePort();
        String hubPath = resolveHubPath();
        if (!hubPath.startsWith("/")) {
            hubPath = "/" + hubPath;
        }
        return NetworkUtils.buildHttpUrl(address, "http", port, hubPath);
    }

    private String buildQueryString() {
        try {
            String playerName = RethinkDbClient.getInstance().getStoredPlayerName();
            if (TextUtils.isEmpty(playerName)) {
                playerName = AndoWSignageApp.PLAYER_ID;
            }
            String playerGuid = RethinkDbClient.getInstance().getStoredPlayerGuid();
            StringBuilder sb = new StringBuilder();
            if (!TextUtils.isEmpty(playerName)) {
                sb.append("playerName=").append(URLEncoder.encode(playerName, "UTF-8"));
            }
            if (!TextUtils.isEmpty(playerGuid)) {
                if (sb.length() > 0) sb.append("&");
                sb.append("playerGuid=").append(URLEncoder.encode(playerGuid, "UTF-8"));
            }
            return sb.toString();
        } catch (Exception ex) {
            Log.e(TAG, "buildQueryString: failed to build query string", ex);
            return "";
        }
    }

    private static String appendQueryString(String url, String queryString) {
        if (TextUtils.isEmpty(url) || TextUtils.isEmpty(queryString)) {
            return url;
        }
        return url.contains("?") ? url + "&" + queryString : url + "?" + queryString;
    }

    private String resolveServerAddress() {
        String messageServerIp = LocalSettingsProvider.getMessageServerIp();
        if (!TextUtils.isEmpty(messageServerIp)) {
            return messageServerIp;
        }
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return AndoWSignageApp.MANUAL_IP;
        }
        String managerIp = LocalSettingsProvider.getManagerIp();
        if (!TextUtils.isEmpty(managerIp)) {
            return managerIp;
        }
        return AndoWSignageApp.MANAGER_IP;
    }

    private int resolvePort() {
        int configured = LocalSettingsProvider.getSignalrPort();
        if (configured > 0 && configured <= 65535) {
            return configured;
        }
        return DEFAULT_PORT;
    }

    private String resolveHubPath() {
        String configured = LocalSettingsProvider.getSignalrHubPath();
        if (TextUtils.isEmpty(configured)) {
            return DEFAULT_HUB_PATH;
        }
        return configured;
    }

    public static class SignalRMessage {
        @SerializedName("from")
        public String From = "Server";
        @SerializedName("to")
        public String To = "All";
        @SerializedName("command")
        public String Command = "Update";
        @SerializedName("dataType")
        public String DataType = "String";
        @SerializedName("data")
        public Object Data = null;
    }

    public static class SignalRCommandEnvelope {
        @SerializedName("commandId")
        public String CommandId;
        @SerializedName("command")
        public String Command;
        @SerializedName("playerId")
        public String PlayerId;
        @SerializedName("payloadJson")
        public String PayloadJson;
        @SerializedName("createdAt")
        public String CreatedAt;
        @SerializedName("isUrgent")
        public boolean IsUrgent;
    }

    public static class HeartbeatPayload {
        @SerializedName("clientId")
        public String ClientId = "";
        @SerializedName("playerName")
        public String PlayerName = "";
        @SerializedName("status")
        public String Status = "";
        @SerializedName("process")
        public int Process;
        @SerializedName("version")
        public String Version = "";
        @SerializedName("currentPage")
        public String CurrentPage = "";
        @SerializedName("hdmiState")
        public boolean HdmiState;

        public static HeartbeatPayload create(String clientId,
                                              String status,
                                              int process,
                                              String version,
                                              String currentPage,
                                              boolean hdmiState) {
            HeartbeatPayload payload = new HeartbeatPayload();
            payload.ClientId = clientId == null ? "" : clientId;
            payload.PlayerName = AndoWSignageApp.PLAYER_ID == null ? "" : AndoWSignageApp.PLAYER_ID;
            payload.Status = status == null ? "" : status;
            payload.Process = process;
            payload.Version = version == null ? "" : version;
            payload.CurrentPage = currentPage == null ? "" : currentPage;
            payload.HdmiState = hdmiState;
            return payload;
        }
    }
}

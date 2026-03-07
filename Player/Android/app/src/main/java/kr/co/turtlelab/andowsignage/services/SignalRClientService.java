package kr.co.turtlelab.andowsignage.services;

import android.text.TextUtils;
import android.util.Log;

import com.google.gson.Gson;

import java.net.URLEncoder;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.LinkedBlockingDeque;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkModels;
import microsoft.aspnet.signalr.client.ConnectionState;
import microsoft.aspnet.signalr.client.NullLogger;
import microsoft.aspnet.signalr.client.Platform;
import microsoft.aspnet.signalr.client.SignalRFuture;
import microsoft.aspnet.signalr.client.hubs.HubConnection;
import microsoft.aspnet.signalr.client.hubs.HubProxy;
import microsoft.aspnet.signalr.client.hubs.SubscriptionHandler1;
import microsoft.aspnet.signalr.client.http.android.AndroidPlatformComponent;
import microsoft.aspnet.signalr.client.transport.LongPollingTransport;

public class SignalRClientService {
    private static final String TAG = "SignalRClientService";

    public interface Listener {
        void onCommand(String command);
        void onCommandEnvelope(SignalRCommandEnvelope envelope);
        void onWeeklySchedule(RethinkModels.WeeklyScheduleRecord weekly);
    }

    private static final int DEFAULT_PORT = 5000;
    private static final String DEFAULT_HUB_PATH = "/Data";
    private static final String HUB_NAME = "MsgHub";
    private static final long RECONNECT_DELAY_MS = 5000L;
    private static final long CONNECT_TIMEOUT_MS = 15000L;
    private static final long HEARTBEAT_TIMEOUT_MS = 5000L;
    private static final int MAX_SIGNALR_ACTION_QUEUE = 10;
    private static final NullLogger SIGNALR_NULL_LOGGER = new NullLogger();

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
    private final ScheduledExecutorService reconnectScheduler = Executors.newSingleThreadScheduledExecutor(runnable -> {
        Thread thread = new Thread(runnable, "SignalRReconnectScheduler");
        thread.setDaemon(true);
        return thread;
    });
    private final Object syncRoot = new Object();
    private final AtomicBoolean stopping = new AtomicBoolean(false);
    private final AtomicBoolean reconnectScheduled = new AtomicBoolean(false);

    private HubConnection connection;
    private HubProxy hubProxy;
    private ScheduledFuture<?> reconnectFuture;

    private static SignalRClientService shared;

    public SignalRClientService(Listener listener, Gson gson) {
        this.listener = listener;
        this.gson = gson == null ? new Gson() : gson;
        Platform.loadPlatformComponent(new AndroidPlatformComponent());
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
        enqueueAction("start", this::ensureConnectedInternal);
    }

    public void stop() {
        stopping.set(true);
        cancelReconnectSchedule();
        enqueueAction("stop", this::stopInternal);
    }

    public void reconnect() {
        stopping.set(false);
        cancelReconnectSchedule();
        enqueueAction("reconnect", () -> {
            stopInternal();
            ensureConnectedInternal();
        });
    }

    public void sendHeartbeat(HeartbeatPayload payload) {
        if (payload == null) {
            return;
        }
        enqueueAction("heartbeat", () -> sendHeartbeatInternal(payload));
    }

    private void ensureConnectedInternal() {
        if (stopping.get()) {
            return;
        }
        String url = buildUrl();
        if (TextUtils.isEmpty(url)) {
            Log.w(TAG, "ensureConnectedInternal: empty SignalR url");
            return;
        }

        HubConnection existingConnection;
        synchronized (syncRoot) {
            existingConnection = connection;
            if (shouldKeepCurrentConnection(existingConnection)) {
                return;
            }
        }

        ConnectionBundle bundle = buildConnection(url);
        HubConnection previous;
        synchronized (syncRoot) {
            previous = connection;
            connection = bundle.connection;
            hubProxy = bundle.proxy;
        }
        safeStopConnection(previous);

        try {
            startConnection(bundle.connection);
            reconnectScheduled.set(false);
        } catch (Exception ex) {
            if (isOperationCancelled(ex)) {
                Log.w(TAG, "startInternal: startConnection cancelled. url=" + url, ex);
            } else {
                Log.e(TAG, "startInternal: startConnection failed, scheduling reconnect. url=" + url, ex);
            }
            synchronized (syncRoot) {
                if (connection == bundle.connection) {
                    connection = null;
                    hubProxy = null;
                }
            }
            safeStopConnection(bundle.connection);
            if (!stopping.get()) {
                scheduleReconnect();
            }
        }
    }

    private ConnectionBundle buildConnection(String url) {
        HubConnection hubConnection = new HubConnection(url, buildQueryString(), true, SIGNALR_NULL_LOGGER);
        HubProxy proxy = hubConnection.createHubProxy(HUB_NAME);
        proxy.on("ReceiveMessage", new SubscriptionHandler1<SignalRMessage>() {
            @Override
            public void run(SignalRMessage message) {
                handleMessage(message);
            }
        }, SignalRMessage.class);
        hubConnection.closed(() -> {
            if (!stopping.get()) {
                synchronized (syncRoot) {
                    if (connection == hubConnection) {
                        connection = null;
                        hubProxy = null;
                    }
                }
                scheduleReconnect();
            }
        });
        return new ConnectionBundle(hubConnection, proxy);
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
            hubConnection.stop();
        } catch (Exception ex) {
            Log.w(TAG, "safeStopConnection: failed to stop hub connection", ex);
        }
    }

    private void stopInternal() {
        HubConnection local;
        synchronized (syncRoot) {
            local = connection;
            connection = null;
            hubProxy = null;
        }
        safeStopConnection(local);
    }

    private static final class ConnectionBundle {
        final HubConnection connection;
        final HubProxy proxy;

        ConnectionBundle(HubConnection connection, HubProxy proxy) {
            this.connection = connection;
            this.proxy = proxy;
        }
    }

    private void startConnection(HubConnection local) throws Exception {
        // 이전 작업의 interrupt 플래그가 남아 있으면 future.get()가 즉시 취소될 수 있어 초기화한다.
        if (Thread.interrupted()) {
            Log.w(TAG, "startConnection: clearing stale interrupted flag before connect.");
        }
        SignalRFuture<Void> future = local.start(new LongPollingTransport(SIGNALR_NULL_LOGGER));
        future.get(CONNECT_TIMEOUT_MS, TimeUnit.MILLISECONDS);
    }

    private boolean shouldKeepCurrentConnection(HubConnection local) {
        if (local == null) {
            return false;
        }
        ConnectionState state = local.getState();
        return state == ConnectionState.Connected || state == ConnectionState.Connecting;
    }

    private void sendHeartbeatInternal(HeartbeatPayload payload) {
        if (stopping.get()) {
            return;
        }
        HubConnection localConn;
        HubProxy localProxy;
        synchronized (syncRoot) {
            localConn = connection;
            localProxy = hubProxy;
        }
        if (localConn == null || localProxy == null) {
            scheduleReconnect();
            return;
        }
        if (localConn.getState() != ConnectionState.Connected) {
            scheduleReconnect();
            return;
        }
        try {
            SignalRFuture<Void> future = localProxy.invoke("ReportHeartbeat", payload);
            future.get(HEARTBEAT_TIMEOUT_MS, TimeUnit.MILLISECONDS);
        } catch (Exception ex) {
            Log.e(TAG, "sendHeartbeat: ReportHeartbeat invoke failed", ex);
            synchronized (syncRoot) {
                if (connection == localConn) {
                    connection = null;
                    hubProxy = null;
                }
            }
            safeStopConnection(localConn);
            scheduleReconnect();
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
        String host = resolveHost();
        if (TextUtils.isEmpty(host)) {
            return null;
        }
        int port = resolvePort();
        String hubPath = resolveHubPath();
        if (!hubPath.startsWith("/")) {
            hubPath = "/" + hubPath;
        }
        return "http://" + host + ":" + port + hubPath;
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

    private String resolveHost() {
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
        public String From = "Server";
        public String To = "All";
        public String Command = "Update";
        public String DataType = "String";
        public Object Data = null;
    }

    public static class SignalRCommandEnvelope {
        public String CommandId;
        public String Command;
        public String PlayerId;
        public String PayloadJson;
        public String CreatedAt;
        public boolean IsUrgent;
    }

    public static class HeartbeatPayload {
        public String ClientId = "";
        public String PlayerName = "";
        public String Status = "";
        public int Process;
        public String Version = "";
        public String CurrentPage = "";
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

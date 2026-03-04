package kr.co.turtlelab.andowsignage.services;

import android.text.TextUtils;

import com.google.gson.Gson;

import java.net.URLEncoder;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
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

    public interface Listener {
        void onCommand(String command);
        void onCommandEnvelope(SignalRCommandEnvelope envelope);
        void onWeeklySchedule(RethinkModels.WeeklyScheduleRecord weekly);
    }

    private static final int DEFAULT_PORT = 5000;
    private static final String DEFAULT_HUB_PATH = "/Data";
    private static final String HUB_NAME = "MsgHub";
    private static final long RECONNECT_DELAY_MS = 5000L;

    private Listener listener;
    private final Gson gson;
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final Object syncRoot = new Object();
    private final AtomicBoolean stopping = new AtomicBoolean(false);
    private final AtomicBoolean reconnecting = new AtomicBoolean(false);

    private HubConnection connection;
    private HubProxy hubProxy;

    private static SignalRClientService shared;

    public SignalRClientService(Listener listener, Gson gson) {
        this.listener = listener;
        this.gson = gson == null ? new Gson() : gson;
        Platform.loadPlatformComponent(new AndroidPlatformComponent());
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
        executor.execute(this::startInternal);
    }

    public void stop() {
        stopping.set(true);
        HubConnection local;
        synchronized (syncRoot) {
            local = connection;
            hubProxy = null;
            connection = null;
        }
        if (local != null) {
            try {
                local.stop();
            } catch (Exception ignore) {
            }
        }
    }

    public void reconnect() {
        stop();
        reconnecting.set(false);
        start();
    }

    public void sendHeartbeat(HeartbeatPayload payload) {
        if (payload == null) {
            return;
        }
        HubConnection localConn;
        HubProxy localProxy;
        synchronized (syncRoot) {
            localConn = connection;
            localProxy = hubProxy;
        }
        if (localConn == null || localProxy == null || localConn.getState() != ConnectionState.Connected) {
            scheduleReconnect();
            return;
        }
        executor.execute(() -> {
            try {
                SignalRFuture<Void> future = localProxy.invoke("ReportHeartbeat", payload);
                future.get();
            } catch (Exception ignore) {
                scheduleReconnect();
            }
        });
    }

    private void startInternal() {
        String url = buildUrl();
        if (TextUtils.isEmpty(url)) {
            return;
        }

        HubConnection local;
        synchronized (syncRoot) {
            if (connection != null) {
                return;
            }
            local = buildConnection(url);
            connection = local;
        }

        try {
            SignalRFuture<Void> future = local.start(new LongPollingTransport(new NullLogger()));
            future.get();
        } catch (Exception ex) {
            scheduleReconnect();
        }
    }

    private HubConnection buildConnection(String url) {
        HubConnection hubConnection = new HubConnection(url, buildQueryString(), true, new NullLogger());
        HubProxy proxy = hubConnection.createHubProxy(HUB_NAME);
        proxy.on("ReceiveMessage", new SubscriptionHandler1<SignalRMessage>() {
            @Override
            public void run(SignalRMessage message) {
                handleMessage(message);
            }
        }, SignalRMessage.class);
        hubConnection.closed(() -> {
            if (!stopping.get()) {
                scheduleReconnect();
            }
        });
        hubProxy = proxy;
        return hubConnection;
    }

    private void scheduleReconnect() {
        if (reconnecting.getAndSet(true)) {
            return;
        }
        executor.execute(() -> {
            try {
                while (!stopping.get()) {
                    try {
                        Thread.sleep(RECONNECT_DELAY_MS);
                    } catch (InterruptedException ignore) {
                    }
                    HubConnection local;
                    synchronized (syncRoot) {
                        local = connection;
                    }
                    if (local == null) {
                        return;
                    }
                    try {
                        SignalRFuture<Void> future = local.start(new LongPollingTransport(new NullLogger()));
                        future.get();
                        return;
                    } catch (Exception ignore) {
                    }
                }
            } finally {
                reconnecting.set(false);
            }
        });
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
        } catch (Exception ignore) {
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
        } catch (Exception ignore) {
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
            String playerGuid = RethinkDbClient.getInstance().ensurePlayerGuid();
            if (TextUtils.isEmpty(playerGuid) && !TextUtils.isEmpty(playerName)) {
                playerGuid = RethinkDbClient.getInstance().ensurePlayerGuid(playerName);
            }
            StringBuilder sb = new StringBuilder();
            if (!TextUtils.isEmpty(playerName)) {
                sb.append("playerName=").append(URLEncoder.encode(playerName, "UTF-8"));
            }
            if (!TextUtils.isEmpty(playerGuid)) {
                if (sb.length() > 0) sb.append("&");
                sb.append("playerGuid=").append(URLEncoder.encode(playerGuid, "UTF-8"));
            }
            return sb.toString();
        } catch (Exception ignore) {
            return "";
        }
    }

    private String resolveHost() {
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return AndoWSignageApp.MANUAL_IP;
        }
        String messageHost = LocalSettingsProvider.getMessageServerIp();
        if (!TextUtils.isEmpty(messageHost)) {
            return messageHost;
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
        public String Status = "";
        public int Process;
        public String Version = "";
        public String CurrentPage = "";
        public boolean HdmiState;
        public String Timestamp = "";
        public String TimestampString = "";

        public static HeartbeatPayload create(String clientId,
                                              String status,
                                              int process,
                                              String version,
                                              String currentPage,
                                              boolean hdmiState) {
            HeartbeatPayload payload = new HeartbeatPayload();
            payload.ClientId = clientId == null ? "" : clientId;
            payload.Status = status == null ? "" : status;
            payload.Process = process;
            payload.Version = version == null ? "" : version;
            payload.CurrentPage = currentPage == null ? "" : currentPage;
            payload.HdmiState = hdmiState;
            payload.TimestampString = buildTimestampString();
            payload.Timestamp = buildIsoTimestamp();
            return payload;
        }

        private static String buildTimestampString() {
            SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
            format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
            return format.format(new Date());
        }

        private static String buildIsoTimestamp() {
            SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSSXXX", Locale.KOREA);
            format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
            return format.format(new Date());
        }
    }
}

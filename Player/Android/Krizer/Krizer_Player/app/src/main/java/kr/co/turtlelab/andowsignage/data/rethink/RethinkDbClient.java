package kr.co.turtlelab.andowsignage.data.rethink;

import android.os.Build;
import android.text.TextUtils;
import android.util.Log;

import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import com.google.gson.JsonPrimitive;
import com.google.gson.reflect.TypeToken;
import com.rethinkdb.RethinkDB;
import com.rethinkdb.gen.ast.ReqlExpr;
import com.rethinkdb.net.Connection;
import com.rethinkdb.net.Result;
import com.rethinkdb.model.MapObject;

import java.lang.reflect.Array;
import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.lang.reflect.Type;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.Enumeration;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.UUID;
import java.util.TimeZone;
import java.util.concurrent.atomic.AtomicBoolean;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.data.update.UpdateFailurePolicy;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

/**
 * RethinkDB 에 직접 연결하여 필요한 데이터를 조회한다.
 * 모든 호출은 UpdateManagerService 의 워커 스레드에서 수행되므로 별도 스레드 처리는 하지 않는다.
 */
public class RethinkDbClient {
    private static final String TAG = "RethinkDbClient";

    private static final String DATABASE = "NewHyOn";
    private static final String TABLE_PLAYER = "PlayerInfoManager";
    private static final String TABLE_PAGE_LIST = "PageListInfoManager";
    private static final String TABLE_PAGE = "PageInfoManager";
    private static final String TABLE_TEXT_INFO = "TextInfoManager";
    private static final String TABLE_WEEKLY = "WeeklyInfoManagerClass";
    private static final String TABLE_PERIOD = "ContentPeriodManager";
    private static final String TABLE_HEARTBEAT = "ClientHeartbeat";
    private static final String TABLE_UPDATE_QUEUE = "UpdateQueue";
    private static final String TABLE_COMMAND_HISTORY = "CommandHistory";
    private static final String TABLE_SERVER_SETTINGS = "ServerSettings";
    private static final int QUERY_RETRY_COUNT_ON_RESTART = 1;
    private static final long QUERY_RETRY_DELAY_MS = 100L;
    private static final long CONNECTION_RETRY_DELAY_MS = 5000L;

    private static final RethinkDB R = RethinkDB.r;

    private static RethinkDbClient sInstance;

    private final Gson gson = new Gson();
    private final Object connectionLock = new Object();
    private final Type mapType = new TypeToken<Map<String, Object>>(){}.getType();
    private Connection connection;
    private String host = "127.0.0.1";
    private int port = 28015;
    private String username = "admin";
    private String password = "turtle04!9";
    private final Object deviceInfoLock = new Object();
    private boolean deviceInfoSynced = false;
    private boolean deviceInfoSyncInProgress = false;
    private long deviceInfoRevision = 0L;
    private boolean forceClearAuthKeyOnce = false;
    private String lastSyncedPlayerGuid = null;
    private boolean guidVerified = false;
    private volatile long lastGuidVerificationEpochMs = 0L;
    private static final long GUID_VERIFICATION_INTERVAL_MS = 5000L;
    private final AtomicBoolean postConnectInitInProgress = new AtomicBoolean(false);
    private final Object heartbeatTableLock = new Object();
    private volatile boolean heartbeatTableReady = false;
    private final Object updateQueueTableLock = new Object();
    private volatile boolean updateQueueTableReady = false;
    private final Object commandHistoryTableLock = new Object();
    private volatile boolean commandHistoryTableReady = false;

    public static RethinkDbClient getInstance() {
        if (sInstance == null) {
            synchronized (RethinkDbClient.class) {
                if (sInstance == null) {
                    sInstance = new RethinkDbClient();
                }
            }
        }
        return sInstance;
    }

    private Connection getConnection() {
        while (true) {
            boolean shouldInitialize = false;
            Connection current;
            String targetHost;
            int targetPort;
            String targetUser;
            String targetPassword;
            synchronized (connectionLock) {
                if (connection != null && connection.isOpen()) {
                    return connection;
                }
                targetHost = host;
                targetPort = port;
                targetUser = username;
                targetPassword = password;
            }

            Connection connected = null;
            try {
                connected = R.connection()
                        .hostname(targetHost)
                        .port(targetPort)
                        .user(targetUser, targetPassword)
                        .timeout(3000L)
                        .connect();
            } catch (Exception ex) {
                Log.w(TAG, "RethinkDbClient: connection failed. retrying in " + CONNECTION_RETRY_DELAY_MS + "ms", ex);
                sleepQuietly(CONNECTION_RETRY_DELAY_MS);
                continue;
            }

            synchronized (connectionLock) {
                if (connection != null && connection.isOpen()) {
                    closeConnectionQuietly(connected);
                    return connection;
                }

                String latestHost = host == null ? "" : host.trim();
                String connectedHost = targetHost == null ? "" : targetHost.trim();
                if (!latestHost.equalsIgnoreCase(connectedHost)
                        || port != targetPort
                        || !safeEquals(username, targetUser)
                        || !safeEquals(password, targetPassword)) {
                    closeConnectionQuietly(connected);
                    continue;
                }

                connection = connected;
                synchronized (deviceInfoLock) {
                    deviceInfoSynced = false;
                    deviceInfoSyncInProgress = false;
                    lastSyncedPlayerGuid = null;
                }
                guidVerified = false;
                lastGuidVerificationEpochMs = 0L;
                shouldInitialize = true;
                current = connection;
            }

            if (shouldInitialize) {
                runPostConnectInitializationAsync();
            }
            return current;
        }
    }

    private void runPostConnectInitializationAsync() {
        if (postConnectInitInProgress.getAndSet(true)) {
            return;
        }
        new Thread(() -> {
            try {
                try {
                    refreshPlayerGuidFromRemote();
                } catch (Exception ex) {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
                try {
                    updateDeviceInfoIfNeeded();
                } catch (Exception ex) {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
                try {
                    fetchInitialWeeklySchedule();
                } catch (Exception ex) {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
            } finally {
                postConnectInitInProgress.set(false);
            }
        }, "RethinkDbInit").start();
    }

    private boolean connectOnce() {
        String targetHost;
        int targetPort;
        String targetUser;
        String targetPassword;
        synchronized (connectionLock) {
            if (connection != null && connection.isOpen()) {
                return true;
            }
            targetHost = host;
            targetPort = port;
            targetUser = username;
            targetPassword = password;
        }

        Connection connected = null;
        try {
            connected = R.connection()
                    .hostname(targetHost)
                    .port(targetPort)
                    .user(targetUser, targetPassword)
                    .timeout(3000L)
                    .connect();
        } catch (Exception ex) {
            Log.w(TAG, "RethinkDbClient: one-shot connection failed.", ex);
            return false;
        }

        synchronized (connectionLock) {
            if (connection != null && connection.isOpen()) {
                closeConnectionQuietly(connected);
                return true;
            }

            String latestHost = host == null ? "" : host.trim();
            String connectedHost = targetHost == null ? "" : targetHost.trim();
            if (!latestHost.equalsIgnoreCase(connectedHost)
                    || port != targetPort
                    || !safeEquals(username, targetUser)
                    || !safeEquals(password, targetPassword)) {
                closeConnectionQuietly(connected);
                return false;
            }

            connection = connected;
            synchronized (deviceInfoLock) {
                deviceInfoSynced = false;
                deviceInfoSyncInProgress = false;
                lastSyncedPlayerGuid = null;
            }
            guidVerified = false;
            lastGuidVerificationEpochMs = 0L;
        }

        return true;
    }

    public void updateHost(String newHost) {
        String normalizedNewHost = NetworkUtils.extractHost(newHost);
        if (normalizedNewHost == null || normalizedNewHost.isEmpty()) {
            return;
        }
        synchronized (connectionLock) {
            String normalizedCurrentHost = host == null ? "" : host.trim();
            if (!normalizedCurrentHost.isEmpty() &&
                    normalizedCurrentHost.equalsIgnoreCase(normalizedNewHost)) {
                return;
            }
            host = normalizedNewHost;
            if (connection != null) {
                try {
                    connection.close();
                } catch (Exception ex) {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
                connection = null;
            }
            guidVerified = false;
            lastGuidVerificationEpochMs = 0L;
        }
    }

    public void resetConnectionForReconnect(String newHost) {
        String normalizedNewHost = NetworkUtils.extractHost(newHost);
        if (normalizedNewHost == null || normalizedNewHost.isEmpty()) {
            return;
        }
        synchronized (connectionLock) {
            host = normalizedNewHost;
            if (connection != null) {
                try {
                    connection.close();
                } catch (Exception ex) {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
                connection = null;
            }
            synchronized (deviceInfoLock) {
                deviceInfoSynced = false;
                deviceInfoSyncInProgress = false;
                lastSyncedPlayerGuid = null;
            }
            guidVerified = false;
            lastGuidVerificationEpochMs = 0L;
            heartbeatTableReady = false;
            updateQueueTableReady = false;
            commandHistoryTableReady = false;
        }
    }

    public RethinkModels.PlayerInfoRecord fetchPlayer(String playerName) {
        return fetchPlayerInternal(playerName, true);
    }

    public boolean hasRegisteredPlayerName(String playerName) {
        return fetchPlayerInternal(playerName, false) != null;
    }

    private RethinkModels.PlayerInfoRecord fetchPlayerInternal(String playerName, boolean persistSkeleton) {
        return fetchPlayerInternal(playerName, persistSkeleton, getStoredPlayerGuid());
    }

    private RethinkModels.PlayerInfoRecord fetchPlayerInternal(String playerName, boolean persistSkeleton, String preferredGuid) {
        String normalizedPlayerName = normalizePlayerName(playerName);
        if (TextUtils.isEmpty(normalizedPlayerName)) {
            return null;
        }
        ReqlExpr query = R.db(DATABASE).table(TABLE_PLAYER);
        List<Map> result = runList(query);
        List<RethinkModels.PlayerInfoRecord> candidates = new ArrayList<>();
        for (Map row : result) {
            RethinkModels.PlayerInfoRecord candidate = convert(row, RethinkModels.PlayerInfoRecord.class);
            if (candidate == null) {
                continue;
            }
            if (isSamePlayerName(candidate.getPlayerName(), normalizedPlayerName)) {
                candidates.add(candidate);
            }
        }
        RethinkModels.PlayerInfoRecord record = selectCanonicalPlayer(candidates, preferredGuid);
        if (record == null) {
            return null;
        }
        if (persistSkeleton) {
            saveRealmPlayerSkeleton(record);
        }
        return record;
    }

    public RethinkModels.PlayerInfoRecord fetchPlayerByGuid(String playerGuid) {
        return fetchPlayerByGuidInternal(playerGuid, true);
    }

    private RethinkModels.PlayerInfoRecord fetchPlayerByGuidInternal(String playerGuid, boolean persistSkeleton) {
        if (TextUtils.isEmpty(playerGuid)) {
            return null;
        }
        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_PLAYER)
                .get(playerGuid);
        Map map = runSingle(query);
        if (map == null) {
            return null;
        }
        RethinkModels.PlayerInfoRecord record = convert(map, RethinkModels.PlayerInfoRecord.class);
        if (persistSkeleton) {
            saveRealmPlayerSkeleton(record);
        }
        return record;
    }

    public String fetchPlayerCommand(String playerGuid) {
        if (TextUtils.isEmpty(playerGuid)) {
            return null;
        }
        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_PLAYER)
                .get(playerGuid)
                .pluck("command");
        Map map = runSingle(query);
        if (map == null) {
            return null;
        }
        Object cmd = map.get("command");
        return cmd == null ? null : String.valueOf(cmd);
    }

    public void clearCommand(String playerGuid) {
        if (playerGuid == null || playerGuid.isEmpty()) {
            return;
        }
        try {
            R.db(DATABASE)
                    .table(TABLE_PLAYER)
                    .get(playerGuid)
                    .update(R.hashMap("command", ""))
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    public RethinkModels.PageListRecord fetchPageList(String playlistName) {
        if (playlistName == null || playlistName.isEmpty()) {
            return null;
        }
        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_PAGE_LIST)
                .filter(R.hashMap("PLI_PageListName", playlistName))
                .limit(1);
        List<Map> result = runList(query);
        if (result.isEmpty()) {
            return null;
        }
        return convert(result.get(0), RethinkModels.PageListRecord.class);
    }

    public List<RethinkModels.PageInfoRecord> fetchPagesByIds(List<String> pageIds) {
        List<RethinkModels.PageInfoRecord> pages = new ArrayList<>();
        if (pageIds == null || pageIds.isEmpty()) {
            return pages;
        }

        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_PAGE)
                .getAll(R.args(pageIds));
        List<Map> rows = runList(query);
        Map<String, Map> temp = new HashMap<>();
        for (Map row : rows) {
            Object id = row.get("id");
            if (id != null) {
                temp.put(String.valueOf(id), row);
            }
        }
        for (String id : pageIds) {
            Map row = temp.get(id);
            if (row != null) {
                pages.add(convert(row, RethinkModels.PageInfoRecord.class));
            }
        }
        return pages;
    }

    public RethinkModels.TextInfoRecord fetchTextInfo(String pageName, String elementName) {
        if (pageName == null || elementName == null) {
            return null;
        }
        Map<String, Object> filter = new HashMap<>();
        filter.put("CIF_PageName", pageName);
        filter.put("CIF_DataFileName", elementName);
        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_TEXT_INFO)
                .filter(filter)
                .limit(1);
        List<Map> result = runList(query);
        if (result.isEmpty()) {
            return null;
        }
        return convert(result.get(0), RethinkModels.TextInfoRecord.class);
    }

    public RethinkModels.WeeklyScheduleRecord fetchWeeklySchedule(String playerId) {
        if (playerId == null || playerId.isEmpty()) {
            return null;
        }
        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_WEEKLY)
                .get(playerId);
        Map map = runSingle(query);
        if (map == null) {
            return null;
        }
        return convert(map, RethinkModels.WeeklyScheduleRecord.class);
    }

    public List<RethinkModels.ContentPeriodRecord> fetchContentPeriods(List<String> contentGuids) {
        List<RethinkModels.ContentPeriodRecord> result = new ArrayList<>();
        if (contentGuids == null || contentGuids.isEmpty()) {
            return result;
        }

        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_PERIOD)
                .filter(row -> R.expr(contentGuids).contains(row.g("ContentGuid")));

        List<Map> rows = runList(query);
        for (Map row : rows) {
            RethinkModels.ContentPeriodRecord record = convert(row, RethinkModels.ContentPeriodRecord.class);
            if (record != null && !TextUtils.isEmpty(record.getContentGuid())) {
                result.add(record);
            }
        }
        return result;
    }

    public RethinkModels.ServerSettingsRecord fetchServerSettings() {
        Map map = runSingle(R.db(DATABASE)
                .table(TABLE_SERVER_SETTINGS)
                .get(0));
        if (map == null || map.isEmpty()) {
            map = runSingle(R.db(DATABASE)
                    .table(TABLE_SERVER_SETTINGS)
                    .get("0"));
        }
        if (map == null || map.isEmpty()) {
            List<Map> fallbackRows = runList(R.db(DATABASE)
                    .table(TABLE_SERVER_SETTINGS)
                    .limit(1));
            if (!fallbackRows.isEmpty()) {
                map = fallbackRows.get(0);
            }
        }
        if (map == null || map.isEmpty()) {
            return null;
        }
        return convert(map, RethinkModels.ServerSettingsRecord.class);
    }

    public boolean updatePlayerDeviceInfo(String playerId, String ip, String mac, String osName) {
        if (playerId == null || playerId.isEmpty()) {
            return false;
        }
        Map existing = runSingle(R.db(DATABASE).table(TABLE_PLAYER).get(playerId));
        if (existing == null) {
            return false;
        }
        Map<String, Object> values = new HashMap<>();
        AuthSyncSelection authSelection = resolveAuthSync(existing, mac);
        String expectedAuthKey = authSelection.shouldWriteAuthKey
                ? normalizeAuthKey(authSelection.authKey)
                : normalizeAuthKey(getString(existing, "PIF_AuthKey"));
        if (authSelection.shouldWriteAuthKey) {
            values.put("PIF_AuthKey", expectedAuthKey);
        }
        if (!TextUtils.isEmpty(ip)) {
            values.put("PIF_IPAddress", ip);
        }
        String expectedFingerprint = normalizeAuthKey(authSelection.fingerprint);
        if (authSelection.shouldWriteFingerprint && !TextUtils.isEmpty(expectedFingerprint)) {
            values.put("PIF_MacAddress", expectedFingerprint);
        }
        if (!TextUtils.isEmpty(osName)) {
            values.put("PIF_OSName", osName);
        }
        if (values.isEmpty()) {
            return false;
        }
        Object updateResult = null;
        try {
            updateResult = R.db(DATABASE)
                    .table(TABLE_PLAYER)
                    .get(playerId)
                    .update(values)
                    .run(getConnection());
            boolean verified = verifyPlayerDeviceAuthSynced(playerId, expectedAuthKey, expectedFingerprint);
            Log.i(TAG, "RethinkDbClient: player auth sync verified=" + verified
                    + ", authKeyLength=" + expectedAuthKey.length()
                    + ", fingerprintLength=" + expectedFingerprint.length());
            return verified;
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
            return false;
        } finally {
            closeIfNeeded(updateResult);
        }
    }

    public String getCachedPlayerGuid() {
        return getStoredPlayerGuid();
    }

    public void sendHeartbeat(String clientId,
                              String status,
                              int process,
                              String version,
                              String currentPage,
                              String hdmiState) {
        if (TextUtils.isEmpty(clientId)) {
            return;
        }
        ensureHeartbeatTable();
        MapObject<Object, Object> payload = R.hashMap("id", clientId)
                .with("status", TextUtils.isEmpty(status) ? "" : status)
                .with("process", process)
                .with("version", TextUtils.isEmpty(version) ? "" : version)
                .with("currentPage", TextUtils.isEmpty(currentPage) ? "" : currentPage)
                .with("hdmiState", TextUtils.isEmpty(hdmiState) ? "" : hdmiState);
        try {
            R.db(DATABASE)
                    .table(TABLE_HEARTBEAT)
                    .insert(payload)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    public void sendHeartbeatStopped(String clientId, String version) {
        if (TextUtils.isEmpty(clientId)) {
            return;
        }
        ensureHeartbeatTable();
        MapObject<Object, Object> payload = R.hashMap("id", clientId)
                .with("status", "stopped")
                .with("process", 0)
                .with("version", TextUtils.isEmpty(version) ? "" : version)
                .with("currentPage", "")
                .with("hdmiState", false);
        try {
            R.db(DATABASE)
                    .table(TABLE_HEARTBEAT)
                    .insert(payload)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    /**
     * UpdateQueue 진행률 보고.
     */
    public void sendProgress(String queueId,
                             float percent,
                             String statusText) {
        sendProgress(queueId, percent, statusText, 0, 0, null, null, null, null, null, null, null, null, null, null, null);
    }

    public void sendProgress(String queueId,
                             float percent,
                             String statusText,
                             int retryCount,
                             long nextRetryAt,
                             String errorCode,
                             String errorMessage) {
        sendProgress(queueId, percent, statusText, retryCount, nextRetryAt, errorCode, errorMessage, null, null, null, null, null, null, null, null, null);
    }

    public void sendProgress(String queueId,
                             float percent,
                             String statusText,
                             int retryCount,
                             long nextRetryAt,
                             String errorCode,
                             String errorMessage,
                             String downloadJson,
                             String playerId,
                             Float downloadPercentOverride,
                             Float validatePercentOverride,
                             String playerName,
                             String playlistId,
                             String playlistName,
                             String payloadJson,
                             Long createdTicks) {
        String documentId = buildUpdateQueueDocumentId(playerId, queueId);
        if (TextUtils.isEmpty(documentId)) {
            return;
        }
        String normalizedStatus = statusText == null ? "" : statusText.trim().toUpperCase(Locale.US);
        boolean failed = UpdateQueueContract.Status.FAILED.equalsIgnoreCase(normalizedStatus);
        boolean completedOrCancelled = UpdateQueueContract.Status.DONE.equalsIgnoreCase(normalizedStatus)
                || UpdateQueueContract.Status.CANCELLED.equalsIgnoreCase(normalizedStatus);
        if (completedOrCancelled || failed) {
            try {
                updateCommandHistoryByQueue(queueId,
                        normalizedStatus.toLowerCase(Locale.US),
                        errorCode,
                        errorMessage,
                        playerId,
                        createdTicks,
                        UpdateQueueContract.Status.FAILED.equalsIgnoreCase(normalizedStatus) ? Math.max(1, retryCount) : 0,
                        UpdateQueueContract.Status.FAILED.equalsIgnoreCase(normalizedStatus)
                                ? (UpdateFailurePolicy.isPermanent(errorCode) ? "permanent" : "transient_exhausted")
                                : "");
            } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex); }
        }
        if (completedOrCancelled) {
            deleteQueueRecord(queueId, playerId);
            if (UpdateQueueContract.Status.DONE.equalsIgnoreCase(normalizedStatus)) {
                clearPlayerUpdateFields(playerId);
            } else {
                updatePlayerUpdateFields(playerId, statusText, Math.max(0f, Math.min(1f, percent / 100f)), errorMessage, retryCount, nextRetryAt);
            }
            return;
        }
        ensureUpdateQueueTable();

        float progress01 = Math.max(0f, Math.min(1f, percent / 100f));
        float download01 = downloadPercentOverride == null
                ? progress01
                : Math.max(0f, Math.min(1f, downloadPercentOverride / 100f));
        float validate01 = validatePercentOverride == null
                ? progress01
                : Math.max(0f, Math.min(1f, validatePercentOverride / 100f));
        String downloadJsonString = TextUtils.isEmpty(downloadJson) ? "" : downloadJson;
        String payloadJsonString = TextUtils.isEmpty(payloadJson) ? "" : payloadJson;
        String playerNameSafe = TextUtils.isEmpty(playerName) ? "" : playerName;
        String playlistIdSafe = TextUtils.isEmpty(playlistId) ? "" : playlistId;
        String playlistNameSafe = TextUtils.isEmpty(playlistName) ? "" : playlistName;
        Map<String, Object> payload = new HashMap<>();
        payload.put("queueId", queueId);
        payload.put("playerId", TextUtils.isEmpty(playerId) ? "" : playerId);
        payload.put("playerName", playerNameSafe);
        payload.put("playlistId", playlistIdSafe);
        payload.put("playlistName", playlistNameSafe);
        payload.put("status", statusText == null ? "" : statusText);
        payload.put("progress", progress01);
        payload.put("downloadProgress", download01);
        payload.put("validateProgress", validate01);
        payload.put("retryCount", retryCount);
        long nextAttemptTicks = toDotNetLocalTicks(nextRetryAt);
        String nextRetryLocal = formatLocalTimestamp(nextRetryAt);
        payload.put("nextAttemptTicks", nextAttemptTicks);
        payload.put("nextRetryAt", nextRetryLocal);
        payload.put("nextRetryEpochMillis", nextRetryAt);
        payload.put("createdTicks", createdTicks == null ? 0L : createdTicks);
        payload.put("errorCode", TextUtils.isEmpty(errorCode) ? "" : errorCode);
        payload.put("errorMessage", TextUtils.isEmpty(errorMessage) ? "" : errorMessage);
        payload.put("lastError", TextUtils.isEmpty(errorMessage) ? "" : errorMessage);
        payload.put("updatedAt", getCurrentTimestamp());
        payload.put("id", documentId);
        payload.put("downloadJson", downloadJsonString);
        payload.put("payloadJson", payloadJsonString);
        try {
            R.db(DATABASE)
                    .table(TABLE_UPDATE_QUEUE)
                    .insert(payload)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }

        // Update player progress/status for parity with Windows player
        if (!TextUtils.isEmpty(playerId)) {
            updatePlayerUpdateFields(playerId, statusText, progress01, errorMessage, retryCount, nextRetryAt);
        }
    }

    private void ensureHeartbeatTable() {
        if (heartbeatTableReady) {
            return;
        }
        synchronized (heartbeatTableLock) {
            if (heartbeatTableReady) {
                return;
            }
            Object tableListObj = null;
            try {
                tableListObj = R.db(DATABASE).tableList().run(getConnection());
                boolean exists = false;
                if (tableListObj instanceof Iterable) {
                    for (Object item : (Iterable) tableListObj) {
                        if (TABLE_HEARTBEAT.equals(String.valueOf(item))) {
                            exists = true;
                            break;
                        }
                    }
                } else if (tableListObj != null) {
                    exists = TABLE_HEARTBEAT.equals(String.valueOf(tableListObj));
                }
                if (!exists) {
                    R.db(DATABASE).tableCreate(TABLE_HEARTBEAT).run(getConnection());
                }
                heartbeatTableReady = true;
            } catch (Exception ex) {
                if (isTableAlreadyExists(ex)) {
                    heartbeatTableReady = true;
                } else {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
            } finally {
                closeIfNeeded(tableListObj);
            }
        }
    }

    private void ensureUpdateQueueTable() {
        if (updateQueueTableReady) {
            return;
        }
        synchronized (updateQueueTableLock) {
            if (updateQueueTableReady) {
                return;
            }
            Object tableListObj = null;
            try {
                tableListObj = R.db(DATABASE).tableList().run(getConnection());
                boolean exists = false;
                if (tableListObj instanceof Iterable) {
                    for (Object item : (Iterable) tableListObj) {
                        if (TABLE_UPDATE_QUEUE.equals(String.valueOf(item))) {
                            exists = true;
                            break;
                        }
                    }
                } else if (tableListObj != null) {
                    exists = TABLE_UPDATE_QUEUE.equals(String.valueOf(tableListObj));
                }
                if (!exists) {
                    R.db(DATABASE).tableCreate(TABLE_UPDATE_QUEUE).run(getConnection());
                }
                updateQueueTableReady = true;
            } catch (Exception ex) {
                if (isTableAlreadyExists(ex)) {
                    updateQueueTableReady = true;
                } else {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
            } finally {
                closeIfNeeded(tableListObj);
            }
        }
    }

    private void ensureCommandHistoryTable() {
        if (commandHistoryTableReady) {
            return;
        }
        synchronized (commandHistoryTableLock) {
            if (commandHistoryTableReady) {
                return;
            }
            Object tableListObj = null;
            try {
                tableListObj = R.db(DATABASE).tableList().run(getConnection());
                boolean exists = false;
                if (tableListObj instanceof Iterable) {
                    for (Object item : (Iterable) tableListObj) {
                        if (TABLE_COMMAND_HISTORY.equals(String.valueOf(item))) {
                            exists = true;
                            break;
                        }
                    }
                } else if (tableListObj != null) {
                    exists = TABLE_COMMAND_HISTORY.equals(String.valueOf(tableListObj));
                }
                if (!exists) {
                    R.db(DATABASE).tableCreate(TABLE_COMMAND_HISTORY).run(getConnection());
                }
                commandHistoryTableReady = true;
            } catch (Exception ex) {
                if (isTableAlreadyExists(ex)) {
                    commandHistoryTableReady = true;
                } else {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
            } finally {
                closeIfNeeded(tableListObj);
            }
        }
    }

    private Object parseDownloadEntries(String downloadJson) {
        if (TextUtils.isEmpty(downloadJson)) {
            return new ArrayList<>();
        }
        try {
            return gson.fromJson(downloadJson, Object.class);
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
            return new ArrayList<>();
        }
    }

    public void deleteQueueRecord(String queueId, String playerId) {
        String documentId = buildUpdateQueueDocumentId(playerId, queueId);
        if (TextUtils.isEmpty(documentId)) {
            return;
        }
        ensureUpdateQueueTable();
        try {
            R.db(DATABASE)
                    .table(TABLE_UPDATE_QUEUE)
                    .get(documentId)
                    .delete()
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }
    public void deleteQueueRecord(String queueId) {
        deleteQueueRecord(queueId, null);
    }

    private String buildUpdateQueueDocumentId(String playerId, String queueId) {
        if (TextUtils.isEmpty(queueId)) {
            return null;
        }
        String owner = playerId;
        if (TextUtils.isEmpty(owner)) {
            owner = getStoredPlayerGuid();
            if (TextUtils.isEmpty(owner)) {
                owner = ensurePlayerGuid();
            }
        }
        if (TextUtils.isEmpty(owner)) {
            return null;
        }
        if (queueId.contains(":")) {
            return queueId.trim();
        }
        return owner.trim() + ":" + queueId;
    }

    private void updateDeviceInfoIfNeeded() {
        long syncRevision;
        synchronized (deviceInfoLock) {
            if (deviceInfoSynced || deviceInfoSyncInProgress) {
                return;
            }
            deviceInfoSyncInProgress = true;
            syncRevision = deviceInfoRevision;
        }
        try {
            String playerId = ensurePlayerGuid();
            if (TextUtils.isEmpty(playerId) || !guidVerified) {
                return;
            }
            String ip = resolveLocalIpAddress();
            String mac = NetworkUtils.getMACAddress();
            String os = "Android " + Build.VERSION.RELEASE;
            if (!updatePlayerDeviceInfo(playerId, ip, mac, os)) {
                return;
            }
            synchronized (deviceInfoLock) {
                if (syncRevision == deviceInfoRevision) {
                    deviceInfoSynced = true;
                    lastSyncedPlayerGuid = playerId;
                }
            }
        } finally {
            synchronized (deviceInfoLock) {
                deviceInfoSyncInProgress = false;
            }
        }
    }

    public void syncCurrentDeviceInfo() {
        updateDeviceInfoIfNeeded();
    }

    public boolean syncCurrentDeviceInfoOnce(String playerName) {
        if (!TextUtils.isEmpty(playerName)) {
            preparePlayerNameChange(playerName);
        }
        if (!connectOnce()) {
            return false;
        }
        updateDeviceInfoIfNeeded();
        return isDeviceInfoSynced();
    }

    public boolean syncCurrentDeviceInfoClearingAuthOnce(String playerName) {
        synchronized (deviceInfoLock) {
            forceClearAuthKeyOnce = true;
            deviceInfoSynced = false;
        }
        boolean result = syncCurrentDeviceInfoOnce(playerName);
        synchronized (deviceInfoLock) {
            forceClearAuthKeyOnce = false;
        }
        return result;
    }

    public boolean isDeviceInfoSynced() {
        synchronized (deviceInfoLock) {
            return deviceInfoSynced;
        }
    }

    public void invalidateDeviceInfoSync() {
        synchronized (deviceInfoLock) {
            deviceInfoSynced = false;
            lastSyncedPlayerGuid = null;
            deviceInfoRevision++;
        }
    }

    public String ensurePlayerGuid() {
        String storedPlayerName = getStoredPlayerName();
        if (!TextUtils.isEmpty(storedPlayerName)) {
            return ensurePlayerGuid(storedPlayerName);
        }
        String configuredPlayerName = LocalSettingsProvider.getPlayerId();
        if (!TextUtils.isEmpty(configuredPlayerName)) {
            return ensurePlayerGuid(configuredPlayerName);
        }
        return ensurePlayerGuid(AndoWSignageApp.PLAYER_ID);
    }

    public String ensurePlayerGuid(String playerName) {
        String normalizedPlayerName = normalizePlayerName(playerName);
        String storedGuid = getStoredPlayerGuid();
        String guid = storedGuid;
        String storedPlayerName = getStoredPlayerName();
        if (TextUtils.isEmpty(normalizedPlayerName)) {
            guidVerified = !TextUtils.isEmpty(storedGuid);
            resetDeviceInfoSyncWhenGuidChanged(storedGuid);
            return storedGuid;
        }
        boolean isStoredNameMismatched = !TextUtils.isEmpty(normalizedPlayerName)
                && !TextUtils.isEmpty(storedPlayerName)
                && !normalizedPlayerName.equalsIgnoreCase(normalizePlayerName(storedPlayerName));
        if (isStoredNameMismatched) {
            guid = null;
            guidVerified = false;
        }

        RethinkModels.PlayerInfoRecord playerRecord = null;
        long now = System.currentTimeMillis();
        boolean shouldVerifyRemotely = isStoredNameMismatched
                || TextUtils.isEmpty(guid)
                || !guidVerified
                || (now - lastGuidVerificationEpochMs) >= GUID_VERIFICATION_INTERVAL_MS;

        if (shouldVerifyRemotely) {
            if (!TextUtils.isEmpty(guid)) {
                RethinkModels.PlayerInfoRecord playerByGuid = fetchPlayerByGuidInternal(guid, false);
                if (playerByGuid != null && isSamePlayerName(playerByGuid.getPlayerName(), normalizedPlayerName)) {
                    playerRecord = playerByGuid;
                }
            }

            if (playerRecord == null) {
                playerRecord = fetchPlayerInternal(normalizedPlayerName, false, storedGuid);
            }

            if (playerRecord != null && !TextUtils.isEmpty(playerRecord.getGuid())) {
                guid = playerRecord.getGuid();
                guidVerified = true;
            } else {
                guid = null;
                guidVerified = false;
            }

            lastGuidVerificationEpochMs = now;
        }

        if (TextUtils.isEmpty(guid)) {
            resetDeviceInfoSyncWhenGuidChanged(null);
            return null;
        }

        boolean shouldPersistSkeleton = isStoredNameMismatched
                || TextUtils.isEmpty(storedGuid)
                || !TextUtils.equals(storedGuid, guid)
                || !TextUtils.equals(storedPlayerName, normalizedPlayerName);
        if (!TextUtils.isEmpty(guid) && playerRecord == null && shouldPersistSkeleton) {
            String playerNameToSave = !TextUtils.isEmpty(normalizedPlayerName)
                    ? normalizedPlayerName
                    : storedPlayerName;
            saveRealmPlayerSkeleton(guid, playerNameToSave);
        }

        guidVerified = !TextUtils.isEmpty(guid);
        resetDeviceInfoSyncWhenGuidChanged(guid);
        return guid;
    }

    public String getStoredPlayerGuid() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer player = realm.where(RealmPlayer.class).findFirst();
            if (player != null) {
                return player.getPlayerId();
            }
        } finally {
            realm.close();
        }
        return null;
    }

    public String getStoredPlayerName() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer player = realm.where(RealmPlayer.class).findFirst();
            if (player != null) {
                return player.getPlayerName();
            }
        } finally {
            realm.close();
        }
        return null;
    }

    public String getStoredDefaultPlaylist() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer player = realm.where(RealmPlayer.class).findFirst();
            if (player != null) {
                return player.getPlaylistName();
            }
        } finally {
            realm.close();
        }
        return "";
    }

    private AuthSyncSelection resolveAuthSync(Map existing, String mac) {
        synchronized (deviceInfoLock) {
            if (forceClearAuthKeyOnce) {
                return new AuthSyncSelection("", "", true, false);
            }
        }

        String localAuthKey = normalizeAuthKey(getStoredPlayerAuthKey());
        if (!TextUtils.isEmpty(localAuthKey)) {
            String fingerprint = normalizeAuthKey(getStoredPlayerAuthFingerprint());
            return new AuthSyncSelection(localAuthKey, fingerprint, true);
        }

        return new AuthSyncSelection("", "", false);
    }

    private String getStoredPlayerAuthKey() {
        return PlayerDataProvider.getPlayerAuthKey();
    }

    private String getStoredPlayerAuthFingerprint() {
        return PlayerDataProvider.getPlayerAuthFingerprint();
    }

    private boolean verifyPlayerDeviceAuthSynced(String playerId, String expectedAuthKey, String expectedFingerprint) {
        Map updated = runSingle(R.db(DATABASE).table(TABLE_PLAYER).get(playerId));
        if (updated == null) {
            return false;
        }
        String remoteAuthKey = normalizeAuthKey(getString(updated, "PIF_AuthKey"));
        String remoteFingerprint = normalizeAuthKey(getString(updated, "PIF_MacAddress"));
        return TextUtils.equals(remoteAuthKey, expectedAuthKey)
                && (TextUtils.isEmpty(expectedFingerprint)
                        || TextUtils.equals(remoteFingerprint, expectedFingerprint));
    }

    private String normalizeAuthKey(String value) {
        return value == null ? "" : value.trim();
    }

    private String extractLicenseHubDeviceFingerprint(String authKey) {
        if (TextUtils.isEmpty(authKey)) {
            return "";
        }
        try {
            JsonObject json = gson.fromJson(authKey.trim(), JsonObject.class);
            if (json == null || (!json.has("LicenseToken") && !json.has("licenseToken"))) {
                return "";
            }
            JsonElement value = json.has("DeviceFingerprint")
                    ? json.get("DeviceFingerprint")
                    : json.get("deviceFingerprint");
            return value == null || value.isJsonNull() ? "" : value.getAsString();
        } catch (Exception ignored) {
            return "";
        }
    }

    private String formatMacAddress(String value) {
        String normalized = normalizeMacAddress(value);
        if (normalized.length() != 12) {
            return "";
        }
        StringBuilder builder = new StringBuilder(17);
        for (int i = 0; i < normalized.length(); i += 2) {
            if (builder.length() > 0) {
                builder.append(':');
            }
            builder.append(normalized, i, i + 2);
        }
        return builder.toString();
    }

    private String normalizeMacAddress(String value) {
        if (TextUtils.isEmpty(value)) {
            return "";
        }
        return value.replaceAll("[^0-9A-Fa-f]", "").toUpperCase(Locale.US);
    }

    private String getString(Map map, String key) {
        if (map == null || TextUtils.isEmpty(key)) {
            return "";
        }
        Object value = map.get(key);
        return value == null ? "" : String.valueOf(value).trim();
    }

    private static class AuthSyncSelection {
        final String authKey;
        final String fingerprint;
        final boolean shouldWriteAuthKey;
        final boolean shouldWriteFingerprint;

        AuthSyncSelection(String authKey, String fingerprint) {
            this(authKey, fingerprint, false);
        }

        AuthSyncSelection(String authKey, String fingerprint, boolean shouldWriteAuthKey) {
            this(authKey, fingerprint, shouldWriteAuthKey, shouldWriteAuthKey);
        }

        AuthSyncSelection(String authKey, String fingerprint, boolean shouldWriteAuthKey, boolean shouldWriteFingerprint) {
            this.authKey = authKey == null ? "" : authKey;
            this.fingerprint = fingerprint == null ? "" : fingerprint;
            this.shouldWriteAuthKey = shouldWriteAuthKey;
            this.shouldWriteFingerprint = shouldWriteFingerprint;
        }
    }

    public void preparePlayerNameChange(String playerName) {
        String normalizedPlayerName = playerName == null ? "" : playerName.trim();
        if (TextUtils.isEmpty(normalizedPlayerName)) {
            return;
        }
        updateStoredPlayerName(normalizedPlayerName);
        synchronized (deviceInfoLock) {
            deviceInfoSynced = false;
            deviceInfoSyncInProgress = false;
            lastSyncedPlayerGuid = null;
        }
        guidVerified = false;
        lastGuidVerificationEpochMs = 0L;
    }

    public String refreshPlayerGuidForPlayerName(String playerName) {
        preparePlayerNameChange(playerName);
        return ensurePlayerGuid(playerName);
    }

    private void updateStoredPlayerName(String playerName) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmPlayer player = r.where(RealmPlayer.class).findFirst();
            if (player != null) {
                player.setPlayerName(playerName);
            }
        });
        realm.close();
    }

    private void saveRealmPlayerSkeleton(RethinkModels.PlayerInfoRecord record) {
        if (record == null || TextUtils.isEmpty(record.getGuid())) {
            return;
        }
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmPlayer existing = r.where(RealmPlayer.class).findFirst();
            String existingAuthKey = PlayerDataProvider.getPlayerAuthKey();
            String existingFingerprint = PlayerDataProvider.getPlayerAuthFingerprint();
            if (existing != null && !TextUtils.isEmpty(existing.getPlayerId())
                    && !existing.getPlayerId().equals(record.getGuid())) {
                existing.deleteFromRealm();
                existing = null;
            }
            RealmPlayer target = existing;
            if (target == null) {
                target = r.createObject(RealmPlayer.class, record.getGuid());
            }
            if (!TextUtils.isEmpty(record.getPlayerName())) {
                target.setPlayerName(record.getPlayerName());
            }
            if (!TextUtils.isEmpty(record.getPlaylist())) {
                target.setPlaylistName(record.getPlaylist());
            }
            target.setPifAuthKey(existingAuthKey == null ? "" : existingAuthKey);
            target.setPifFingerprint(existingFingerprint == null ? "" : existingFingerprint);
            target.setLandscape(record.isLandscape());
        });
        realm.close();
    }

    private void saveRealmPlayerSkeleton(String guid, String playerName) {
        if (TextUtils.isEmpty(guid)) {
            return;
        }
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmPlayer existing = r.where(RealmPlayer.class).findFirst();
            String existingAuthKey = PlayerDataProvider.getPlayerAuthKey();
            String existingFingerprint = PlayerDataProvider.getPlayerAuthFingerprint();
            if (existing != null && !TextUtils.isEmpty(existing.getPlayerId())
                    && !existing.getPlayerId().equals(guid)) {
                existing.deleteFromRealm();
                existing = null;
            }
            RealmPlayer target = existing;
            if (target == null) {
                target = r.createObject(RealmPlayer.class, guid);
            }
            if (!TextUtils.isEmpty(playerName)) {
                target.setPlayerName(playerName);
            }
            target.setPifAuthKey(existingAuthKey == null ? "" : existingAuthKey);
            target.setPifFingerprint(existingFingerprint == null ? "" : existingFingerprint);
        });
        realm.close();
    }

    private void resetDeviceInfoSyncWhenGuidChanged(String guid) {
        synchronized (deviceInfoLock) {
            if (lastSyncedPlayerGuid != null && !lastSyncedPlayerGuid.equals(guid)) {
                deviceInfoSynced = false;
                lastSyncedPlayerGuid = null;
            }
        }
    }

    private void refreshPlayerGuidFromRemote() {
        String playerName = getStoredPlayerName();
        if (TextUtils.isEmpty(playerName)) {
            playerName = AndoWSignageApp.PLAYER_ID;
        }
        if (TextUtils.isEmpty(playerName)) {
            return;
        }
        ensurePlayerGuid(playerName);
    }

    private RethinkModels.PlayerInfoRecord selectCanonicalPlayer(List<RethinkModels.PlayerInfoRecord> candidates,
                                                                 String preferredGuid) {
        if (candidates == null || candidates.isEmpty()) {
            return null;
        }
        RethinkModels.PlayerInfoRecord best = null;
        int bestScore = Integer.MIN_VALUE;
        for (RethinkModels.PlayerInfoRecord candidate : candidates) {
            if (candidate == null || TextUtils.isEmpty(candidate.getGuid())) {
                continue;
            }
            int score = 0;
            if (!TextUtils.isEmpty(candidate.getPlaylist())) {
                score += 4;
            }
            if (!TextUtils.isEmpty(preferredGuid) && preferredGuid.equalsIgnoreCase(candidate.getGuid())) {
                score += 2;
            }
            if (!TextUtils.isEmpty(candidate.getPlayerName())) {
                score += 1;
            }
            if (best == null || score > bestScore
                    || (score == bestScore && compareGuid(candidate.getGuid(), best.getGuid()) < 0)) {
                best = candidate;
                bestScore = score;
            }
        }
        return best;
    }

    private int compareGuid(String left, String right) {
        String safeLeft = left == null ? "" : left;
        String safeRight = right == null ? "" : right;
        return safeLeft.compareToIgnoreCase(safeRight);
    }

    private boolean isSamePlayerName(String left, String right) {
        return normalizePlayerName(left).equalsIgnoreCase(normalizePlayerName(right));
    }

    private String normalizePlayerName(String playerName) {
        return playerName == null ? "" : playerName.trim();
    }

    private String resolveLocalIpAddress() {
        try {
            Enumeration<NetworkInterface> interfaces = NetworkInterface.getNetworkInterfaces();
            while (interfaces.hasMoreElements()) {
                NetworkInterface intf = interfaces.nextElement();
                if (intf == null || !intf.isUp() || intf.isLoopback()) {
                    continue;
                }
                Enumeration<InetAddress> addrs = intf.getInetAddresses();
                while (addrs.hasMoreElements()) {
                    InetAddress addr = addrs.nextElement();
                    if (addr instanceof Inet4Address && !addr.isLoopbackAddress()) {
                        return addr.getHostAddress();
                    }
                }
            }
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
        return "127.0.0.1";
    }

    private Map runSingle(ReqlExpr expr) {
        for (int attempt = 0; attempt <= QUERY_RETRY_COUNT_ON_RESTART; attempt++) {
            Object obj = null;
            try {
                obj = expr.run(getConnection());
                if (obj == null) {
                    return null;
                }
                if (obj instanceof Result) {
                    Result result = (Result) obj;
                    if (result.hasNext()) {
                        return asMap(result.next());
                    }
                    return null;
                }
                if (obj instanceof Iterable) {
                    Iterable iterable = (Iterable) obj;
                    java.util.Iterator iterator = iterable.iterator();
                    if (iterator.hasNext()) {
                        return asMap(iterator.next());
                    }
                    return null;
                }
                return asMap(obj);
            } catch (Exception ex) {
                if (isConnectionShutdownRequested(ex) && attempt < QUERY_RETRY_COUNT_ON_RESTART) {
                    Log.w(TAG, "RethinkDbClient: query retried while connection is restarting.");
                    sleepQuietly(QUERY_RETRY_DELAY_MS);
                    continue;
                }
                if (isConnectionShutdownRequested(ex)) {
                    Log.w(TAG, "RethinkDbClient: query skipped while connection is restarting.");
                } else {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
                return null;
            } finally {
                closeIfNeeded(obj);
            }
        }
        return null;
    }

    private List<Map> runList(ReqlExpr expr) {
        for (int attempt = 0; attempt <= QUERY_RETRY_COUNT_ON_RESTART; attempt++) {
            List<Map> list = new ArrayList<>();
            Object obj = null;
            try {
                obj = expr.run(getConnection());
                if (obj instanceof Iterable) {
                    for (Object item : (Iterable) obj) {
                        list.add(asMap(item));
                    }
                } else if (obj instanceof List) {
                    for (Object item : (List) obj) {
                        list.add(asMap(item));
                    }
                } else if (obj != null) {
                    list.add(asMap(obj));
                }
                return list;
            } catch (Exception ex) {
                if (isConnectionShutdownRequested(ex) && attempt < QUERY_RETRY_COUNT_ON_RESTART) {
                    Log.w(TAG, "RethinkDbClient: query retried while connection is restarting.");
                    sleepQuietly(QUERY_RETRY_DELAY_MS);
                    continue;
                }
                if (isConnectionShutdownRequested(ex)) {
                    Log.w(TAG, "RethinkDbClient: query skipped while connection is restarting.");
                } else {
                    Log.e(TAG, "RethinkDbClient: operation failed", ex);
                }
                return list;
            } finally {
                closeIfNeeded(obj);
            }
        }
        return new ArrayList<>();
    }

    private boolean isConnectionShutdownRequested(Throwable throwable) {
        Throwable current = throwable;
        while (current != null) {
            String message = current.getMessage();
            if (!TextUtils.isEmpty(message) && message.contains("Shutdown was requested")) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private boolean isTableAlreadyExists(Throwable throwable) {
        Throwable current = throwable;
        while (current != null) {
            String message = current.getMessage();
            if (!TextUtils.isEmpty(message) && message.contains("already exists")) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private void sleepQuietly(long delayMs) {
        try {
            Thread.sleep(delayMs);
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
        }
    }

    private void closeConnectionQuietly(Connection conn) {
        if (conn == null) {
            return;
        }
        try {
            conn.close();
        } catch (Exception ex) {
            Log.w(TAG, "RethinkDbClient: failed to close stale connection", ex);
        }
    }

    private boolean safeEquals(String left, String right) {
        if (left == null) {
            return right == null;
        }
        return left.equals(right);
    }

    private <T> T convert(Object obj, Class<T> clazz) {
        if (obj == null) {
            return null;
        }
        JsonElement json = toJsonElement(obj);
        return gson.fromJson(json, clazz);
    }

    private JsonElement toJsonElement(Object value) {
        if (value == null) {
            return JsonNull.INSTANCE;
        }
        if (value instanceof JsonElement) {
            return (JsonElement) value;
        }
        if (value instanceof MapObject) {
            Map map = new HashMap();
            map.putAll((MapObject) value);
            return toJsonElement(map);
        }
        if (value instanceof Map) {
            JsonObject jsonObject = new JsonObject();
            Map map = (Map) value;
            for (Object entryObj : map.entrySet()) {
                Map.Entry entry = (Map.Entry) entryObj;
                String key = entry.getKey() == null ? "" : String.valueOf(entry.getKey());
                jsonObject.add(key, toJsonElement(entry.getValue()));
            }
            return jsonObject;
        }
        if (value instanceof Iterable) {
            JsonArray array = new JsonArray();
            for (Object item : (Iterable) value) {
                array.add(toJsonElement(item));
            }
            return array;
        }
        if (value.getClass().isArray()) {
            JsonArray array = new JsonArray();
            int length = Array.getLength(value);
            for (int i = 0; i < length; i++) {
                array.add(toJsonElement(Array.get(value, i)));
            }
            return array;
        }
        if (value instanceof Number) {
            return new JsonPrimitive((Number) value);
        }
        if (value instanceof Boolean) {
            return new JsonPrimitive((Boolean) value);
        }
        if (value instanceof Character) {
            return new JsonPrimitive((Character) value);
        }
        if (value instanceof String) {
            return new JsonPrimitive((String) value);
        }
        return gson.toJsonTree(value);
    }

    private Map asMap(Object item) {
        if (item == null) {
            return new HashMap();
        }
        if (item instanceof Map) {
            return new HashMap<>((Map) item);
        }
        if (item instanceof MapObject) {
            Map map = new HashMap<>();
            map.putAll((MapObject) item);
            return map;
        }
        JsonElement json = toJsonElement(item);
        Map map = gson.fromJson(json, mapType);
        return map == null ? new HashMap() : map;
    }

    private void closeIfNeeded(Object obj) {
        if (obj instanceof AutoCloseable) {
            try {
                ((AutoCloseable) obj).close();
            } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex); }
        }
    }

    public void fetchInitialWeeklySchedule() {
        String ensuredGuid = ensurePlayerGuid();
        if (TextUtils.isEmpty(ensuredGuid)) {
            return;
        }

        RethinkModels.PlayerInfoRecord player = fetchPlayerByGuid(ensuredGuid);
        if (player == null) {
            player = fetchPlayer(AndoWSignageApp.PLAYER_ID);
        }
        if (player == null) {
            return;
        }

        DataSyncManager manager = new DataSyncManager();
        manager.syncWeeklySchedule(player);
    }

    private String getCurrentTimestamp() {
        SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
        format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
        return format.format(new Date());
    }

    public void updateQueueStatus(String playerGUID, long queueId, String status) {
        String documentId = buildUpdateQueueDocumentId(playerGUID, String.valueOf(queueId));
        if (TextUtils.isEmpty(documentId)) {
            return;
        }
        Map<String, Object> payload = new HashMap<>();
        payload.put("status", status == null ? "" : status);
        try {
            R.db(DATABASE)
                    .table(TABLE_UPDATE_QUEUE)
                    .get(documentId)
                    .update(payload)
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    private void updatePlayerUpdateFields(String playerId,
                                          String status,
                                          float progress01,
                                          String errorMessage,
                                          int retryCount,
                                          long nextAttemptTicks) {
        if (TextUtils.isEmpty(playerId)) {
            return;
        }
        Map<String, Object> values = new HashMap<>();
        values.put("UpdateStatus", status == null ? "" : status);
        values.put("UpdateProgress", progress01);
        values.put("UpdateError", TextUtils.isEmpty(errorMessage) ? "" : errorMessage);
        values.put("UpdateRetry", retryCount);
        long nextTicks = toDotNetLocalTicks(nextAttemptTicks);
        values.put("UpdateNext", nextTicks);
        values.put("UpdateNextEpochMillis", nextAttemptTicks);
        try {
            R.db(DATABASE)
                    .table(TABLE_PLAYER)
                    .get(playerId)
                    .update(values)
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    private void clearPlayerUpdateFields(String playerId) {
        if (TextUtils.isEmpty(playerId)) {
            return;
        }
        Map<String, Object> values = new HashMap<>();
        values.put("UpdateStatus", "");
        values.put("UpdateProgress", 0f);
        values.put("UpdateError", "");
        values.put("UpdateRetry", 0);
        values.put("UpdateNext", 0L);
        values.put("UpdateNextEpochMillis", 0L);
        try {
            R.db(DATABASE)
                    .table(TABLE_PLAYER)
                    .get(playerId)
                    .update(values)
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    public String createCommandHistory(String playerId, String playerName, String command) {
        return createCommandHistory(playerId, playerName, command, null);
    }

    public String createCommandHistory(String playerId, String playerName, String command, String metadata) {
        ensureCommandHistoryTable();
        String historyId = buildCommandHistoryId(playerId, command);
        Map<String, Object> doc = new HashMap<>();
        String now = getCurrentTimestamp();
        doc.put("id", historyId);
        doc.put("playerId", TextUtils.isEmpty(playerId) ? "" : playerId);
        doc.put("playerName", TextUtils.isEmpty(playerName) ? "" : playerName);
        doc.put("command", TextUtils.isEmpty(command) ? "" : command);
        doc.put("refQueueId", "");
        doc.put("status", "queued");
        doc.put("errorCode", "");
        doc.put("errorMessage", "");
        doc.put("createdAt", now);
        doc.put("startedAt", "");
        doc.put("endedAt", "");
        doc.put("metadata", TextUtils.isEmpty(metadata) ? "" : metadata);
        try {
            R.db(DATABASE)
                    .table(TABLE_COMMAND_HISTORY)
                    .insert(doc)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
            return historyId;
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
            return "";
        }
    }

    public String upsertCommandHistoryForQueue(String playerId,
                                               String playerName,
                                               String command,
                                               long queueId,
                                               String status,
                                               String errorCode,
                                               String errorMessage,
                                               String metadata) {
        return upsertCommandHistoryForQueue(playerId, playerName, command, String.valueOf(queueId), status, errorCode, errorMessage, metadata, null);
    }

    public String upsertCommandHistoryForQueue(String playerId,
                                               String playerName,
                                               String command,
                                               long queueId,
                                               String status,
                                               String errorCode,
                                               String errorMessage,
                                               String metadata,
                                               Long createdTicks) {
        return upsertCommandHistoryForQueue(playerId, playerName, command, String.valueOf(queueId), status, errorCode, errorMessage, metadata, createdTicks);
    }

    public String upsertCommandHistoryForQueue(String playerId,
                                               String playerName,
                                               String command,
                                               String queueId,
                                               String status,
                                               String errorCode,
                                               String errorMessage,
                                               String metadata) {
        return upsertCommandHistoryForQueue(playerId, playerName, command, queueId, status, errorCode, errorMessage, metadata, null);
    }

    public String upsertCommandHistoryForQueue(String playerId,
                                               String playerName,
                                               String command,
                                               String queueId,
                                               String status,
                                               String errorCode,
                                               String errorMessage,
                                               String metadata,
                                               Long createdTicks) {
        ensureCommandHistoryTable();
        String owner = resolveOwnerPlayerId(playerId);
        long ticks = createdTicks != null && createdTicks > 0
                ? createdTicks
                : toDotNetLocalTicks(System.currentTimeMillis());
        String queueIdSafe = TextUtils.isEmpty(queueId) ? "" : queueId;
        String historyId = buildCommandHistoryIdForQueue(owner, queueIdSafe, ticks);
        String now = getCurrentTimestamp();
        String normalized = normalizeHistoryStatus(status);
        Map<String, Object> doc = new HashMap<>();
        doc.put("id", historyId);
        doc.put("playerId", TextUtils.isEmpty(owner) ? "" : owner);
        doc.put("playerName", TextUtils.isEmpty(playerName) ? "" : playerName);
        doc.put("command", TextUtils.isEmpty(command) ? "" : command);
        doc.put("refQueueId", queueIdSafe);
        doc.put("status", normalized);
        doc.put("errorCode", TextUtils.isEmpty(errorCode) ? "" : errorCode);
        doc.put("errorMessage", TextUtils.isEmpty(errorMessage) ? "" : errorMessage);
        doc.put("createdAt", now);
        doc.put("startedAt", "in_progress".equals(normalized) ? now : "");
        doc.put("endedAt", "in_progress".equals(normalized) ? "" : now);
        doc.put("metadata", TextUtils.isEmpty(metadata) ? "" : metadata);
        try {
            R.db(DATABASE)
                    .table(TABLE_COMMAND_HISTORY)
                    .insert(doc)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
            return historyId;
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
            return "";
        }
    }

    private String buildCommandHistoryId(String playerId, String command) {
        String owner = TextUtils.isEmpty(playerId) ? ensurePlayerGuid() : playerId;
        String cmd = TextUtils.isEmpty(command) ? "" : command.trim().toLowerCase(Locale.US);
        if (TextUtils.isEmpty(owner) || TextUtils.isEmpty(cmd)) {
            return java.util.UUID.randomUUID().toString();
        }
        return owner + ":" + cmd;
    }

    public void updateCommandHistory(String historyId,
                                     String status,
                                     String errorCode,
                                     String errorMessage,
                                     String refQueueId) {
        updateCommandHistory(historyId, status, errorCode, errorMessage, refQueueId, null);
    }

    public void updateCommandHistory(String historyId,
                                     String status,
                                     String errorCode,
                                     String errorMessage,
                                     String refQueueId,
                                     String metadata) {
        if (TextUtils.isEmpty(historyId)) {
            return;
        }
        ensureCommandHistoryTable();
        Map<String, Object> update = new HashMap<>();
        String now = getCurrentTimestamp();
        String normalized = normalizeHistoryStatus(status);
        if ("in_progress".equals(normalized)) {
            update.put("startedAt", now);
        } else {
            update.put("endedAt", now);
        }
        update.put("status", normalized);
        update.put("errorCode", TextUtils.isEmpty(errorCode) ? "" : errorCode);
        update.put("errorMessage", TextUtils.isEmpty(errorMessage) ? "" : errorMessage);
        if (refQueueId != null) {
            update.put("refQueueId", refQueueId);
        }
        if (metadata != null) {
            update.put("metadata", metadata);
        }
        try {
            R.db(DATABASE)
                    .table(TABLE_COMMAND_HISTORY)
                    .get(historyId)
                    .update(update)
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    public void updateCommandHistoryByQueue(String queueId,
                                            String status,
                                            String errorCode,
                                            String errorMessage) {
        updateCommandHistoryByQueue(queueId, status, errorCode, errorMessage, null, null);
    }

    public void updateCommandHistoryByQueue(String queueId,
                                            String status,
                                            String errorCode,
                                            String errorMessage,
                                            String playerId) {
        updateCommandHistoryByQueue(queueId, status, errorCode, errorMessage, playerId, null);
    }

    public void updateCommandHistoryByQueue(String queueId,
                                            String status,
                                            String errorCode,
                                            String errorMessage,
                                            String playerId,
                                            Long createdTicks) {
        updateCommandHistoryByQueue(queueId, status, errorCode, errorMessage, playerId, createdTicks, 0, "");
    }

    public void updateCommandHistoryByQueue(String queueId,
                                            String status,
                                            String errorCode,
                                            String errorMessage,
                                            String playerId,
                                            Long createdTicks,
                                            int attemptCount,
                                            String failureClass) {
        if (TextUtils.isEmpty(queueId)) {
            return;
        }
        ensureCommandHistoryTable();
        String normalized = normalizeHistoryStatus(status);
        Map<String, Object> update = new HashMap<>();
        String now = getCurrentTimestamp();
        if ("in_progress".equals(normalized)) {
            update.put("startedAt", now);
            update.put("endedAt", "");
        } else {
            update.put("endedAt", now);
        }
        update.put("status", normalized);
        update.put("errorCode", TextUtils.isEmpty(errorCode) ? "" : errorCode);
        update.put("errorMessage", TextUtils.isEmpty(errorMessage) ? "" : errorMessage);
        if (attemptCount > 0) {
            update.put("attemptCount", attemptCount);
        }
        if (!TextUtils.isEmpty(failureClass)) {
            update.put("failureClass", failureClass);
        }
        String owner = resolveOwnerPlayerId(playerId);
        String docId = buildCommandHistoryIdForQueue(owner, queueId, createdTicks);
        Map<String, Object> upsert = new HashMap<>(update);
        upsert.put("id", docId);
        upsert.put("playerId", TextUtils.isEmpty(owner) ? "" : owner);
        upsert.put("command", "updatelist");
        upsert.put("refQueueId", queueId);
        upsert.put("createdAt", now);
        try {
            R.db(DATABASE)
                    .table(TABLE_COMMAND_HISTORY)
                    .insert(upsert)
                    .optArg("conflict", "update")
                    .runNoReply(getConnection());
            R.db(DATABASE)
                    .table(TABLE_COMMAND_HISTORY)
                    .filter(row -> row.g("refQueueId").eq(queueId))
                    .update(update)
                    .runNoReply(getConnection());
        } catch (Exception ex) {
            Log.e(TAG, "RethinkDbClient: operation failed", ex);
        }
    }

    private String normalizeHistoryStatus(String status) {
        if (status == null) {
            return "queued";
        }
        String lower = status.toLowerCase(Locale.US);
        switch (lower) {
            case "queued":
            case "in_progress":
            case "done":
            case "failed":
            case "cancelled":
                return lower;
            default:
                return "failed";
        }
    }

    private String buildCommandHistoryIdForQueue(String ownerPlayerId, long queueId) {
        return buildCommandHistoryIdForQueue(ownerPlayerId, queueId, null);
    }

    private String buildCommandHistoryIdForQueue(String ownerPlayerId, long queueId, Long createdTicks) {
        if (queueId <= 0) {
            return java.util.UUID.randomUUID().toString();
        }
        return buildCommandHistoryIdForQueue(ownerPlayerId, String.valueOf(queueId), createdTicks);
    }

    private String buildCommandHistoryIdForQueue(String ownerPlayerId, String queueId) {
        return buildCommandHistoryIdForQueue(ownerPlayerId, queueId, null);
    }

    private String buildCommandHistoryIdForQueue(String ownerPlayerId, String queueId, Long createdTicks) {
        if (TextUtils.isEmpty(queueId)) {
            return java.util.UUID.randomUUID().toString();
        }
        String owner = ownerPlayerId;
        if (TextUtils.isEmpty(owner)) {
            owner = resolveOwnerPlayerId(null);
        }
        if (queueId.contains(":")) {
            return queueId;
        }
        if (createdTicks != null && createdTicks > 0 && !TextUtils.isEmpty(owner)) {
            return owner + ":" + createdTicks;
        }
        if (!TextUtils.isEmpty(owner)) {
            return owner + ":" + queueId;
        }
        return queueId;
    }

    private String formatLocalTimestamp(long millis) {
        if (millis <= 0) {
            return "";
        }
        try {
            SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.getDefault());
            return sdf.format(new Date(millis));
        } catch (Exception e) {
            Log.e(TAG, "formatLocalTimestamp: failed to format millis=" + millis, e);
            return "";
        }
    }

    private long toDotNetLocalTicks(long epochMillis) {
        if (epochMillis <= 0) {
            return 0L;
        }
        long offset = java.util.TimeZone.getDefault().getOffset(epochMillis);
        long localMillis = epochMillis + offset;
        return (localMillis * 10_000L) + 621355968000000000L;
    }

    private String resolveOwnerPlayerId(String playerId) {
        String owner = TextUtils.isEmpty(playerId) ? getStoredPlayerGuid() : playerId;
        if (TextUtils.isEmpty(owner)) {
            owner = ensurePlayerGuid();
        }
        return owner;
    }
}

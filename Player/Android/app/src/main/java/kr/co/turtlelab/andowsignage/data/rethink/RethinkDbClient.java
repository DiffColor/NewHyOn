package kr.co.turtlelab.andowsignage.data.rethink;

import android.os.Build;
import android.text.TextUtils;

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
import java.util.TimeZone;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

/**
 * RethinkDB 에 직접 연결하여 필요한 데이터를 조회한다.
 * 모든 호출은 UpdateManagerService 의 워커 스레드에서 수행되므로 별도 스레드 처리는 하지 않는다.
 */
public class RethinkDbClient {

    private static final String DATABASE = "NewHyOn";
    private static final String TABLE_PLAYER = "PlayerInfoManager";
    private static final String TABLE_PAGE_LIST = "PageListInfoManager";
    private static final String TABLE_PAGE = "PageInfoManager";
    private static final String TABLE_TEXT_INFO = "TextInfoManager";
    private static final String TABLE_WEEKLY = "WeeklyInfoManagerClass";
    private static final String TABLE_SERVER_SETTINGS = "ServerSettings";
    private static final String TABLE_HEARTBEAT = "ClientHeartbeat";
    private static final String TABLE_UPDATE_QUEUE = "UpdateQueue";
    private static final String TABLE_COMMAND_HISTORY = "CommandHistory";

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
    private String lastSyncedPlayerGuid = null;
    private boolean guidVerified = false;
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
        synchronized (connectionLock) {
            if (connection == null || !connection.isOpen()) {
                connection = R.connection()
                        .hostname(host)
                        .port(port)
                        .user(username, password)
                        .connect();
                synchronized (deviceInfoLock) {
                    deviceInfoSynced = false;
                    deviceInfoSyncInProgress = false;
                    lastSyncedPlayerGuid = null;
                }
                guidVerified = false;
                refreshServerSettings();
                updateDeviceInfoIfNeeded();
                fetchInitialWeeklySchedule();
            }
            return connection;
        }
    }

    public void updateHost(String newHost) {
        if (newHost == null || newHost.isEmpty()) {
            return;
        }
        synchronized (connectionLock) {
            host = newHost;
            if (connection != null) {
                try {
                    connection.close();
                } catch (Exception ignore) {
                }
                connection = null;
            }
        }
    }

    private void refreshServerSettings() {
        try {
            RethinkModels.ServerSettingsRecord settings = fetchServerSettings();
            if (settings == null) {
                return;
            }
            LocalSettingsProvider.applyServerSettings(
                    settings.getDataServerIp(),
                    settings.getMessageServerIp(),
                    settings.getFtpPort(),
                    settings.getFtpPasvMinPort(),
                    settings.getFtpPasvMaxPort());
        } catch (Exception ignored) {
        }
    }

    public RethinkModels.ServerSettingsRecord fetchServerSettings() {
        Map settingsMap = runSingle(R.db(DATABASE)
                .table(TABLE_SERVER_SETTINGS)
                .get(0));
        if (settingsMap == null || settingsMap.isEmpty()) {
            List<Map> rows = runList(R.db(DATABASE)
                    .table(TABLE_SERVER_SETTINGS)
                    .limit(1));
            if (rows.isEmpty()) {
                return null;
            }
            settingsMap = rows.get(0);
        }
        return convert(settingsMap, RethinkModels.ServerSettingsRecord.class);
    }

    private RethinkModels.PlayerInfoRecord fetchPlayerByNameOrGuid(String playerKey) {
        if (TextUtils.isEmpty(playerKey)) {
            return null;
        }
        RethinkModels.PlayerInfoRecord record = fetchPlayer(playerKey);
        if (record != null && !TextUtils.isEmpty(record.getGuid())) {
            return record;
        }
        return fetchPlayerByGuid(playerKey);
    }

    public RethinkModels.PlayerInfoRecord fetchPlayer(String playerName) {
        if (playerName == null || playerName.isEmpty()) {
            return null;
        }
        String lowered = playerName.toLowerCase(Locale.US);
        ReqlExpr query = R.db(DATABASE)
                .table(TABLE_PLAYER)
                .filter(row -> row.g("PIF_PlayerName").downcase().eq(lowered))
                .limit(1);
        List<Map> result = runList(query);
        if (result.isEmpty()) {
            return null;
        }
        RethinkModels.PlayerInfoRecord record = convert(result.get(0), RethinkModels.PlayerInfoRecord.class);
        saveRealmPlayerSkeleton(record);
        return record;
    }

    public RethinkModels.PlayerInfoRecord fetchPlayerByGuid(String playerGuid) {
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
        saveRealmPlayerSkeleton(record);
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
        } catch (Exception ignore) {
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

    public void updatePlayerDeviceInfo(String playerId, String ip, String mac, String osName) {
        if (playerId == null || playerId.isEmpty()) {
            return;
        }
        Map<String, Object> values = new HashMap<>();
        if (!TextUtils.isEmpty(ip)) {
            values.put("PIF_IPAddress", ip);
        }
        if (!TextUtils.isEmpty(mac)) {
            values.put("PIF_MacAddress", mac);
        }
        if (!TextUtils.isEmpty(osName)) {
            values.put("PIF_OSName", osName);
        }
        if (values.isEmpty()) {
            return;
        }
        try {
            R.db(DATABASE)
                    .table(TABLE_PLAYER)
                    .get(playerId)
                    .update(values)
                    .runNoReply(getConnection());
        } catch (Exception ignored) {
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
        Map<String, Object> payload = new HashMap<>();
        payload.put("id", clientId);
        payload.put("status", TextUtils.isEmpty(status) ? "" : status);
        payload.put("process", process);
        payload.put("version", TextUtils.isEmpty(version) ? "" : version);
        payload.put("currentPage", TextUtils.isEmpty(currentPage) ? "" : currentPage);
        payload.put("hdmiState", TextUtils.isEmpty(hdmiState) ? "" : hdmiState);
        payload.put("heartbeatTs", getCurrentTimestamp());
        try {
            R.db(DATABASE)
                    .table(TABLE_HEARTBEAT)
                    .insert(payload)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
        } catch (Exception ignored) { }
    }

    public void sendHeartbeatStopped(String clientId, String version) {
        if (TextUtils.isEmpty(clientId)) {
            return;
        }
        ensureHeartbeatTable();
        Map<String, Object> payload = new HashMap<>();
        payload.put("id", clientId);
        payload.put("status", "stopped");
        payload.put("process", 0);
        payload.put("version", TextUtils.isEmpty(version) ? "" : version);
        payload.put("currentPage", "");
        payload.put("hdmiState", false);
        payload.put("heartbeatTs", getCurrentTimestamp());
        try {
            R.db(DATABASE)
                    .table(TABLE_HEARTBEAT)
                    .insert(payload)
                    .optArg("conflict", "replace")
                    .runNoReply(getConnection());
        } catch (Exception ignored) {
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
        if (UpdateQueueContract.Status.DONE.equalsIgnoreCase(normalizedStatus)
                || UpdateQueueContract.Status.FAILED.equalsIgnoreCase(normalizedStatus)
                || UpdateQueueContract.Status.CANCELLED.equalsIgnoreCase(normalizedStatus)) {
            try {
                updateCommandHistoryByQueue(queueId,
                        normalizedStatus.toLowerCase(Locale.US),
                        errorCode,
                        errorMessage,
                        playerId,
                        createdTicks);
            } catch (Exception ignored) { }
            deleteQueueRecord(queueId, playerId);
            updatePlayerUpdateFields(playerId, statusText, Math.max(0f, Math.min(1f, percent / 100f)), errorMessage, retryCount, nextRetryAt);
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
        String nextRetryLocal = formatLocalTimestamp(nextRetryAt);
        payload.put("nextAttemptTicks", nextRetryLocal);
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
        } catch (Exception ignored) {
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
            } catch (Exception ignored) {
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
            } catch (Exception ignored) {
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
            } catch (Exception ignored) {
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
        } catch (Exception ignored) {
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
        } catch (Exception ignored) {
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
        synchronized (deviceInfoLock) {
            if (deviceInfoSynced || deviceInfoSyncInProgress) {
                return;
            }
            deviceInfoSyncInProgress = true;
        }
        String syncedPlayerId = null;
        String playerId = getStoredPlayerGuid();
        if (TextUtils.isEmpty(playerId)) {
            playerId = ensurePlayerGuid();
        }
        if (!TextUtils.isEmpty(playerId)) {
            String ip = resolveLocalIpAddress();
            String mac = NetworkUtils.getMACAddress();
            String os = "Android " + Build.VERSION.RELEASE;
            updatePlayerDeviceInfo(playerId, ip, mac, os);
            syncedPlayerId = playerId;
        }
        synchronized (deviceInfoLock) {
            deviceInfoSynced = !TextUtils.isEmpty(syncedPlayerId);
            deviceInfoSyncInProgress = false;
            lastSyncedPlayerGuid = syncedPlayerId;
        }
    }
    public boolean isDeviceInfoSynced() {
        synchronized (deviceInfoLock) {
            return deviceInfoSynced;
        }
    }

    public String ensurePlayerGuid() {
        return ensurePlayerGuid(resolvePlayerLookupKey());
    }

    private String resolvePlayerLookupKey() {
        String storedPlayerName = getStoredPlayerName();
        if (!TextUtils.isEmpty(storedPlayerName)) {
            return storedPlayerName;
        }
        return AndoWSignageApp.PLAYER_ID;
    }

    public String ensurePlayerGuid(String playerKey) {
        String guid = getStoredPlayerGuid();
        if (!guidVerified) {
            RethinkModels.PlayerInfoRecord playerRecord = fetchPlayerByNameOrGuid(playerKey);
            if (playerRecord != null) {
                guid = playerRecord.getGuid();
                guidVerified = true;
            }
        }
        if (TextUtils.isEmpty(guid)) {
            RethinkModels.PlayerInfoRecord playerRecord = fetchPlayerByNameOrGuid(playerKey);
            if (playerRecord == null) {
                String storedName = getStoredPlayerName();
                if (!TextUtils.isEmpty(storedName)) {
                    playerRecord = fetchPlayer(storedName);
                }
            }
            if (playerRecord != null) {
                guid = playerRecord.getGuid();
                guidVerified = true;
            }
        }
        synchronized (deviceInfoLock) {
            if (lastSyncedPlayerGuid != null && !lastSyncedPlayerGuid.equals(guid)) {
                deviceInfoSynced = false;
                lastSyncedPlayerGuid = null;
            }
        }
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

    private void saveRealmPlayerSkeleton(RethinkModels.PlayerInfoRecord record) {
        if (record == null || TextUtils.isEmpty(record.getGuid())) {
            return;
        }
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmPlayer existing = r.where(RealmPlayer.class).findFirst();
            if (existing != null && !TextUtils.isEmpty(existing.getPlayerId())
                    && !existing.getPlayerId().equals(record.getGuid())) {
                existing.deleteFromRealm();
                existing = null;
            }
            RealmPlayer target = existing;
            if (target == null) {
                target = r.createObject(RealmPlayer.class, record.getGuid());
            }
            target.setPlayerName(record.getPlayerName());
            if (record.getPlaylist() != null) {
                target.setPlaylistName(record.getPlaylist());
            }
            target.setLandscape(record.isLandscape());
        });
        realm.close();
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
        } catch (Exception ignored) {
        }
        return "127.0.0.1";
    }

    private Map runSingle(ReqlExpr expr) {
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
        } catch (Exception ignored) {
            return null;
        } finally {
            closeIfNeeded(obj);
        }
    }

    private List<Map> runList(ReqlExpr expr) {
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
        } catch (Exception ignored) {
        } finally {
            closeIfNeeded(obj);
        }
        return list;
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
            } catch (Exception ignore) { }
        }
    }

    public void fetchInitialWeeklySchedule() {
        String guid = ensurePlayerGuid();
        RethinkModels.PlayerInfoRecord player = fetchPlayerByGuid(guid);
        if (player == null) {
            player = fetchPlayerByNameOrGuid(AndoWSignageApp.PLAYER_ID);
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
        } catch (Exception ignored) {
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
        values.put("UpdateNext", formatLocalTimestamp(nextAttemptTicks));
        values.put("UpdateNextEpochMillis", nextAttemptTicks);
        try {
            R.db(DATABASE)
                    .table(TABLE_PLAYER)
                    .get(playerId)
                    .update(values)
                    .runNoReply(getConnection());
        } catch (Exception ignored) {
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
        } catch (Exception ignored) {
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
        return upsertCommandHistoryForQueue(playerId, playerName, command, queueId, status, errorCode, errorMessage, metadata, null);
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
        ensureCommandHistoryTable();
        String owner = resolveOwnerPlayerId(playerId);
        long ticks = createdTicks != null && createdTicks > 0
                ? createdTicks
                : toDotNetLocalTicks(System.currentTimeMillis());
        String historyId = buildCommandHistoryIdForQueue(owner, queueId, ticks);
        String now = getCurrentTimestamp();
        String normalized = normalizeHistoryStatus(status);
        Map<String, Object> doc = new HashMap<>();
        doc.put("id", historyId);
        doc.put("playerId", TextUtils.isEmpty(owner) ? "" : owner);
        doc.put("playerName", TextUtils.isEmpty(playerName) ? "" : playerName);
        doc.put("command", TextUtils.isEmpty(command) ? "" : command);
        doc.put("refQueueId", String.valueOf(queueId));
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
        } catch (Exception ignored) {
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
        } catch (Exception ignored) {
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
        String owner = resolveOwnerPlayerId(playerId);
        String docId = buildCommandHistoryIdForQueue(owner, queueId, createdTicks);
        Map<String, Object> upsert = new HashMap<>(update);
        upsert.put("id", docId);
        upsert.put("playerId", TextUtils.isEmpty(owner) ? "" : owner);
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
        } catch (Exception ignored) {
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

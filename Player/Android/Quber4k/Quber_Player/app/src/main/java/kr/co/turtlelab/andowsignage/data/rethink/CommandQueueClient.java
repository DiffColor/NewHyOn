package kr.co.turtlelab.andowsignage.data.rethink;

import android.text.TextUtils;
import android.util.Log;

import com.google.gson.Gson;
import com.rethinkdb.RethinkDB;
import com.rethinkdb.net.Connection;
import com.rethinkdb.net.Result;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.TimeZone;
import java.util.Date;
import java.text.SimpleDateFormat;

import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class CommandQueueClient {
    private static final String TAG = "CommandQueueClient";
    private static final long SENT_FALLBACK_DELAY_MS = 15000L;

    private static final String DATABASE = "NewHyOn";
    private static final String TABLE = "CommandQueue";

    private static final RethinkDB R = RethinkDB.r;

    private final Gson gson = new Gson();
    private final Object syncRoot = new Object();
    private String host = "127.0.0.1";
    private int port = 28015;
    private String username = "admin";
    private String password = "turtle04!9";
    private Connection connection;

    public CommandQueueClient(String managerHost) {
        String normalized = NetworkUtils.extractHost(managerHost);
        if (!TextUtils.isEmpty(normalized)) {
            host = normalized;
        }
    }

    public void updateHost(String managerHost) {
        if (TextUtils.isEmpty(managerHost)) {
            return;
        }
        String normalized = NetworkUtils.extractHost(managerHost);
        if (TextUtils.isEmpty(normalized)) {
            return;
        }
        synchronized (syncRoot) {
            if (!TextUtils.isEmpty(host) && host.equalsIgnoreCase(normalized)) {
                return;
            }
            host = normalized;
            resetConnection();
        }
    }

    public List<CommandQueueEntry> fetchPendingCommands(String playerId) {
        String normalizedPlayerId = normalizePlayerId(playerId);
        if (TextUtils.isEmpty(normalizedPlayerId)) {
            return new ArrayList<>();
        }
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return new ArrayList<>();
            }
            Object result = R.db(DATABASE)
                    .table(TABLE)
                    .orderBy("CreatedAt")
                    .run(conn);
            List<Map> rows = runList(result);
            List<CommandQueueEntry> entries = new ArrayList<>();
            for (Map row : rows) {
                CommandQueueEntry entry = convert(row);
                if (entry != null && hasPlayer(entry, normalizedPlayerId)
                        && isActionableStatus(entry, normalizedPlayerId)) {
                    entries.add(entry);
                }
            }
            return entries;
        } catch (Exception ex) {
            Log.e(TAG, "fetchPendingCommands: failed", ex);
            resetConnection();
            return new ArrayList<>();
        }
    }

    public CommandQueueEntry fetchNextPending(String playerId) {
        List<CommandQueueEntry> pending = fetchPendingCommands(playerId);
        return pending.isEmpty() ? null : pending.get(0);
    }

    public void markAck(String commandId, String playerId) {
        updateStatus(commandId, playerId, "ack");
    }

    public void markFailed(String commandId, String playerId) {
        updateStatus(commandId, playerId, "failed");
    }

    public void markSent(String commandId, String playerId) {
        updateStatus(commandId, playerId, "sent");
    }

    public void markAttempt(String commandId) {
        if (TextUtils.isEmpty(commandId)) {
            return;
        }
        String now = getCurrentTimestamp();
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return;
            }
            R.db(DATABASE)
                    .table(TABLE)
                    .get(commandId)
                    .update(R.hashMap("AttemptCount", R.row("AttemptCount").default_(0).add(1))
                            .with("LastAttemptAt", now)
                            .with("UpdatedAt", now))
                    .runNoReply(conn);
        } catch (Exception ex) {
            Log.e(TAG, "markAttempt: failed commandId=" + commandId, ex);
            resetConnection();
        }
    }

    private void updateStatus(String commandId, String playerId, String status) {
        if (TextUtils.isEmpty(commandId) || TextUtils.isEmpty(playerId) || TextUtils.isEmpty(status)) {
            return;
        }
        String normalizedPlayerId = normalizePlayerId(playerId);
        if (TextUtils.isEmpty(normalizedPlayerId)) {
            return;
        }
        String now = getCurrentTimestamp();
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return;
            }
            Map row = runSingle(R.db(DATABASE)
                    .table(TABLE)
                    .get(commandId)
                    .run(conn));
            if (row == null) {
                return;
            }
            CommandQueueEntry entry = convert(row);
            if (entry == null) {
                return;
            }
            if (entry.Status == null) {
                entry.Status = new HashMap<>();
            }
            String statusKey = findStatusKeyIgnoreCase(entry.Status, normalizedPlayerId);
            if (TextUtils.isEmpty(statusKey)) {
                statusKey = normalizedPlayerId;
            }
            entry.Status.put(statusKey, status.trim());

            Map<String, Object> update = new HashMap<>();
            update.put("Status", entry.Status);
            update.put("UpdatedAt", now);
            R.db(DATABASE)
                    .table(TABLE)
                    .get(commandId)
                    .update(update)
                    .runNoReply(conn);
        } catch (Exception ex) {
            Log.e(TAG, "updateStatus: failed commandId=" + commandId + ", playerId=" + normalizedPlayerId + ", status=" + status, ex);
            resetConnection();
        }
    }

    private Connection getConnection() {
        synchronized (syncRoot) {
            if (connection != null && connection.isOpen()) {
                return connection;
            }
            connection = R.connection()
                    .hostname(host)
                    .port(port)
                    .user(username, password)
                    .timeout(3000L)
                    .connect();
            return connection;
        }
    }

    private void resetConnection() {
        synchronized (syncRoot) {
            if (connection != null) {
                try {
                    connection.close();
                } catch (Exception ex) {
                    Log.w(TAG, "resetConnection: failed to close connection", ex);
                }
                connection = null;
            }
        }
    }

    private CommandQueueEntry convert(Map map) {
        if (map == null) {
            return null;
        }
        try {
            String json = gson.toJson(map);
            return gson.fromJson(json, CommandQueueEntry.class);
        } catch (Exception ex) {
            Log.e(TAG, "convert: failed to parse command queue row", ex);
            return null;
        }
    }

    private List<Map> runList(Object result) {
        List<Map> list = new ArrayList<>();
        if (result instanceof Result) {
            Result cursor = (Result) result;
            while (cursor.hasNext()) {
                Object item = cursor.next();
                if (item instanceof Map) {
                    list.add((Map) item);
                }
            }
        } else if (result instanceof Iterable) {
            for (Object item : (Iterable) result) {
                if (item instanceof Map) {
                    list.add((Map) item);
                }
            }
        } else if (result instanceof Map) {
            list.add((Map) result);
        }
        return list;
    }

    private Map runSingle(Object result) {
        if (result instanceof Result) {
            Result cursor = (Result) result;
            if (cursor.hasNext()) {
                Object item = cursor.next();
                if (item instanceof Map) {
                    return (Map) item;
                }
            }
            return null;
        }
        if (result instanceof Iterable) {
            for (Object item : (Iterable) result) {
                if (item instanceof Map) {
                    return (Map) item;
                }
                break;
            }
            return null;
        }
        if (result instanceof Map) {
            return (Map) result;
        }
        return null;
    }

    private String getCurrentTimestamp() {
        SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
        format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
        return format.format(new Date());
    }

    private String normalizePlayerId(String playerId) {
        return TextUtils.isEmpty(playerId) ? "" : playerId.trim().toLowerCase(Locale.US);
    }

    private boolean hasPlayer(CommandQueueEntry entry, String normalizedPlayerId) {
        if (entry == null || entry.PlayerIds == null || entry.PlayerIds.isEmpty()) {
            return false;
        }
        for (String id : entry.PlayerIds) {
            if (id != null && id.equalsIgnoreCase(normalizedPlayerId)) {
                return true;
            }
        }
        return false;
    }

    private boolean isStatus(CommandQueueEntry entry, String normalizedPlayerId, String expected) {
        if (entry == null || entry.Status == null || TextUtils.isEmpty(expected)) {
            return false;
        }
        String statusKey = findStatusKeyIgnoreCase(entry.Status, normalizedPlayerId);
        String current = TextUtils.isEmpty(statusKey) ? null : entry.Status.get(statusKey);
        return expected.equalsIgnoreCase(current == null ? "" : current);
    }

    private boolean isActionableStatus(CommandQueueEntry entry, String normalizedPlayerId) {
        if (isStatus(entry, normalizedPlayerId, "pending")) {
            return true;
        }
        if (!isStatus(entry, normalizedPlayerId, "sent")) {
            return false;
        }
        return isSentDeliveryStale(entry);
    }

    private boolean isSentDeliveryStale(CommandQueueEntry entry) {
        if (entry == null) {
            return false;
        }
        long sentAt = parseTimestampMillis(entry.UpdatedAt);
        if (sentAt <= 0L) {
            sentAt = parseTimestampMillis(entry.CreatedAt);
        }
        if (sentAt <= 0L) {
            return true;
        }
        return System.currentTimeMillis() - sentAt >= SENT_FALLBACK_DELAY_MS;
    }

    private long parseTimestampMillis(String value) {
        if (TextUtils.isEmpty(value)) {
            return -1L;
        }
        try {
            SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
            format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
            Date parsed = format.parse(value);
            return parsed != null ? parsed.getTime() : -1L;
        } catch (Exception ex) {
            Log.w(TAG, "parseTimestampMillis: failed value=" + value, ex);
            return -1L;
        }
    }

    private String findStatusKeyIgnoreCase(Map<String, String> statusMap, String normalizedPlayerId) {
        if (statusMap == null || statusMap.isEmpty() || TextUtils.isEmpty(normalizedPlayerId)) {
            return null;
        }
        for (String key : statusMap.keySet()) {
            if (!TextUtils.isEmpty(key) && key.equalsIgnoreCase(normalizedPlayerId)) {
                return key;
            }
        }
        return null;
    }

    public static class CommandQueueEntry {
        public String id;
        public List<String> PlayerIds = new ArrayList<>();
        public String Command;
        public String payloadJson;
        public Map<String, String> Status = new HashMap<>();
        public String CreatedAt;
        public String UpdatedAt;
        public String ExpiresAt;
        public int AttemptCount;
        public String LastAttemptAt;
        public String Source;
        public String ReplacedBy;
    }
}

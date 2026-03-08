package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import com.rethinkdb.RethinkDB;
import com.rethinkdb.net.Connection;
import com.rethinkdb.net.Result;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.UUID;

public class UpdateLeaseClient {

    private static final String DATABASE = "NewHyOn";
    private static final String TABLE = "UpdateLease";
    private static final RethinkDB R = RethinkDB.r;

    private final Object syncRoot = new Object();
    private String host = "127.0.0.1";
    private int port = 28015;
    private String username = "admin";
    private String password = "turtle04!9";
    private Connection connection;

    public UpdateLeaseClient(String managerHost) {
        if (!TextUtils.isEmpty(managerHost)) {
            host = managerHost;
        }
    }

    public void updateHost(String managerHost) {
        if (TextUtils.isEmpty(managerHost)) {
            return;
        }
        synchronized (syncRoot) {
            host = managerHost;
            resetConnection();
        }
    }

    public UpdateThrottleModels.UpdateLeaseEntry tryAcquire(String playerId, String queueId, int maxConcurrent, int ttlSeconds) {
        if (TextUtils.isEmpty(playerId)) {
            return null;
        }
        int limit = Math.max(1, maxConcurrent);
        String now = getCurrentTimestamp();
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return null;
            }
            Object activeResult = R.db(DATABASE).table(TABLE).count().run(conn);
            int activeCount = readCount(activeResult);
            if (activeCount >= limit) {
                return null;
            }
            UpdateThrottleModels.UpdateLeaseEntry lease = new UpdateThrottleModels.UpdateLeaseEntry();
            lease.Id = UUID.randomUUID().toString();
            lease.PlayerId = playerId;
            lease.QueueId = queueId == null ? "" : queueId;
            lease.LastRenewAt = now;
            R.db(DATABASE).table(TABLE).insert(lease).run(conn);
            return lease;
        } catch (Exception ignore) {
            resetConnection();
            return null;
        }
    }

    public boolean renew(String leaseId, int ttlSeconds) {
        if (TextUtils.isEmpty(leaseId)) {
            return false;
        }
        String now = getCurrentTimestamp();
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return false;
            }
            Object result = R.db(DATABASE)
                    .table(TABLE)
                    .get(leaseId)
                    .update(R.hashMap("LastRenewAt", now))
                    .run(conn);
            return result != null;
        } catch (Exception ignore) {
            resetConnection();
            return false;
        }
    }

    public void release(String leaseId) {
        if (TextUtils.isEmpty(leaseId)) {
            return;
        }
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return;
            }
            R.db(DATABASE).table(TABLE).get(leaseId).delete().run(conn);
        } catch (Exception ignore) {
            resetConnection();
        }
    }

    public void releaseByPlayer(String playerId) {
        if (TextUtils.isEmpty(playerId)) {
            return;
        }
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return;
            }
            String lowered = playerId.toLowerCase(Locale.US);
            R.db(DATABASE)
                    .table(TABLE)
                    .filter(row -> row.g("PlayerId").downcase().eq(lowered))
                    .delete()
                    .run(conn);
        } catch (Exception ignore) {
            resetConnection();
        }
    }

    public void releaseStaleByLastRenew(int seconds) {
        int maxAge = seconds <= 0 ? 60 : seconds;
        String threshold = getTimestampWithOffset(-maxAge);
        try {
            Connection conn = getConnection();
            if (conn == null) {
                return;
            }
            R.db(DATABASE)
                    .table(TABLE)
                    .filter(row -> row.g("LastRenewAt").lt(threshold))
                    .delete()
                    .run(conn);
        } catch (Exception ignore) {
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
                    .connect();
            return connection;
        }
    }

    private void resetConnection() {
        synchronized (syncRoot) {
            if (connection != null) {
                try {
                    connection.close();
                } catch (Exception ignore) {
                }
                connection = null;
            }
        }
    }

    private int readCount(Object result) {
        if (result == null) {
            return 0;
        }
        if (result instanceof Number) {
            return ((Number) result).intValue();
        }
        if (result instanceof Result) {
            Result res = (Result) result;
            try {
                if (res.hasNext()) {
                    Object value = res.next();
                    if (value instanceof Number) {
                        return ((Number) value).intValue();
                    }
                }
                return 0;
            } finally {
                try {
                    res.close();
                } catch (Exception ignore) {
                }
            }
        }
        if (result instanceof Iterable) {
            for (Object value : (Iterable) result) {
                if (value instanceof Number) {
                    return ((Number) value).intValue();
                }
                break;
            }
        }
        return 0;
    }

    private String getCurrentTimestamp() {
        SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
        format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
        return format.format(new Date());
    }

    private String getTimestampWithOffset(int seconds) {
        SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
        format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
        return format.format(new Date(System.currentTimeMillis() + (seconds * 1000L)));
    }
}

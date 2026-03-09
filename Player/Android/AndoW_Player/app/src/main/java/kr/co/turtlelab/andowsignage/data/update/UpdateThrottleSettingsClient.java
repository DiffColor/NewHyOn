package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import com.google.gson.Gson;
import com.rethinkdb.RethinkDB;
import com.rethinkdb.net.Connection;
import com.rethinkdb.net.Result;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class UpdateThrottleSettingsClient {

    private static final String DATABASE = "NewHyOn";
    private static final String TABLE = "UpdateThrottleSettings";
    private static final String DEFAULT_ID = "global";
    private static final RethinkDB R = RethinkDB.r;

    private final Gson gson = new Gson();
    private final Object syncRoot = new Object();
    private String host = "127.0.0.1";
    private int port = 28015;
    private String username = "admin";
    private String password = "turtle04!9";
    private Connection connection;
    private UpdateThrottleModels.UpdateThrottleSettings cached;
    private long nextRefreshAt;

    public UpdateThrottleSettingsClient(String managerHost) {
        String normalized = NetworkUtils.extractHost(managerHost);
        if (!TextUtils.isEmpty(normalized)) {
            host = normalized;
        }
    }

    public void updateHost(String managerHost) {
        String normalized = NetworkUtils.extractHost(managerHost);
        if (TextUtils.isEmpty(normalized)) {
            return;
        }
        synchronized (syncRoot) {
            host = normalized;
            resetConnection();
        }
    }

    public UpdateThrottleModels.UpdateThrottleSettings getSettings() {
        if (cached == null || System.currentTimeMillis() >= nextRefreshAt) {
            refreshSettings();
        }
        return cached == null ? buildDefault() : cached;
    }

    public void refreshSettings() {
        try {
            Connection conn = getConnection();
            if (conn == null) {
                cached = buildDefault();
            } else {
                Object result = R.db(DATABASE)
                        .table(TABLE)
                        .get(DEFAULT_ID)
                        .run(conn);
                cached = convert(result);
                if (cached == null) {
                    cached = buildDefault();
                }
            }
        } catch (Exception ignore) {
            resetConnection();
            cached = buildDefault();
        }
        int refreshSeconds = cached == null ? 1800 : cached.SettingsRefreshSeconds;
        if (refreshSeconds <= 0) {
            refreshSeconds = 1800;
        }
        nextRefreshAt = System.currentTimeMillis() + (refreshSeconds * 1000L);
    }

    private UpdateThrottleModels.UpdateThrottleSettings convert(Object result) {
        if (result == null) {
            return null;
        }
        if (result instanceof Result) {
            Result cursor = (Result) result;
            if (cursor.hasNext()) {
                result = cursor.next();
            } else {
                return null;
            }
        }
        try {
            String json = gson.toJson(result);
            return gson.fromJson(json, UpdateThrottleModels.UpdateThrottleSettings.class);
        } catch (Exception ignore) {
            return null;
        }
    }

    private UpdateThrottleModels.UpdateThrottleSettings buildDefault() {
        UpdateThrottleModels.UpdateThrottleSettings settings = new UpdateThrottleModels.UpdateThrottleSettings();
        settings.Id = DEFAULT_ID;
        settings.MaxConcurrentDownloads = 8;
        settings.RetryIntervalSeconds = 60;
        settings.LeaseTtlSeconds = 3600;
        settings.LeaseRenewIntervalSeconds = 30;
        settings.SettingsRefreshSeconds = 1800;
        settings.UpdatedAt = getCurrentTimestamp();
        return settings;
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

    private String getCurrentTimestamp() {
        SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
        format.setTimeZone(TimeZone.getTimeZone("Asia/Seoul"));
        return format.format(new Date());
    }
}

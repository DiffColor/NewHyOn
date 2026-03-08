package kr.co.turtlelab.andowsignage.dataproviders;

import android.os.Environment;
import android.text.TextUtils;
import android.util.Log;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.util.ArrayList;
import java.util.List;
import java.util.Properties;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmLocalSettings;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class LocalSettingsProvider {

    private static final String TAG = "LocalSettingsProvider";
    private static final String LOCAL_SETTINGS_ID = "local_settings";
    private static final String BACKUP_FILE_NAME = "local_settings.properties";
    private static final String KEY_DATA_SERVER_IP = "data_server_ip";
    private static final String KEY_MESSAGE_SERVER_IP = "message_server_ip";
    private static final String KEY_FTP_PORT = "ftp_port";
    private static final String KEY_FTP_PASV_MIN_PORT = "ftp_pasv_min_port";
    private static final String KEY_FTP_PASV_MAX_PORT = "ftp_pasv_max_port";
    private static final String KEY_FTP_ROOT_PATH = "ftp_root_path";
    public static final String KEY_ENABLE_MANUAL_IP = "is_manual";
    public static final String KEY_KEEP_RATIO = "keepratio";
    public static final String KEY_SWITCH_ON_CONTENT_END = "switch_on_content_end";
    public static final String KEY_PLAYER_ID = "player_ip";
    public static final String KEY_MANAGER_IP = "manager_ip";
    public static final String KEY_MANUAL_IP = "manual_ip";
    public static final String KEY_SIGNALR_PORT = "signalr_port";
    public static final String KEY_SIGNALR_HUB_PATH = "signalr_hub_path";

    private LocalSettingsProvider() {
    }

    public static List<LocalSettingsModel> getLocalSettings() {
        List<LocalSettingsModel> list = new ArrayList<>();
        LocalSettingsModel model = loadOrCreateSettings();
        list.add(model);
        return list;
    }

    private static LocalSettingsModel loadOrCreateSettings() {
        LocalSettingsModel model = new LocalSettingsModel();
        Realm realm = null;
        try {
            realm = Realm.getDefaultInstance();
            RealmLocalSettings settings = realm.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                realm.close();
                createNewLocalSettings();
                realm = Realm.getDefaultInstance();
                settings = realm.where(RealmLocalSettings.class)
                        .equalTo("id", LOCAL_SETTINGS_ID)
                        .findFirst();
            }
            if (settings != null) {
                model.setManualIPState(settings.isManualIpEnabled());
                model.setKeepRatioState(settings.isKeepRatioEnabled());
                model.setSwitchOnContentEnd(settings.isSwitchOnContentEndEnabled());
                model.setUsbAuthKey(settings.getUsbAuthKey());
                model.setPlayerId(settings.getPlayerId());
                model.setManagerIp(settings.getManagerIp());
                model.setManualIp(settings.getManualIp());
                model.setSignalrPort(settings.getSignalrPort());
                model.setSignalrHubPath(settings.getSignalrHubPath());
                model.setDataServerIp(settings.getDataServerIp());
                model.setMessageServerIp(settings.getMessageServerIp());
                model.setFtpPort(settings.getFtpPort());
                model.setFtpPasvMinPort(settings.getFtpPasvMinPort());
                model.setFtpPasvMaxPort(settings.getFtpPasvMaxPort());
                model.setFtpRootPath(settings.getFtpRootPath());
                ensureBackupFromRealmSettings(settings);
            }
        } finally {
            if (realm != null && !realm.isClosed()) {
                realm.close();
            }
        }
        return model;
    }

    public static void createNewLocalSettings() {
        final boolean enableManualIp = AndoWSignageApp.IS_MANUAL;
        final boolean keepRatio = AndoWSignageApp.KEEP_ASPECT_RATIO;
        final boolean switchOnContentEnd = AndoWSignageApp.SWITCH_ON_CONTENT_END;
        final Properties backupProperties = readBackupProperties();
        String resolvedPlayerId = AndoWSignageApp.PLAYER_ID;
        String resolvedManagerIp = AndoWSignageApp.MANAGER_IP;
        String resolvedManualIp = AndoWSignageApp.MANUAL_IP;
        Realm realmPlayer = null;
        try {
            realmPlayer = Realm.getDefaultInstance();
            kr.co.turtlelab.andowsignage.data.realm.RealmPlayer rp =
                    realmPlayer.where(kr.co.turtlelab.andowsignage.data.realm.RealmPlayer.class).findFirst();
            if (rp != null && !TextUtils.isEmpty(rp.getPlayerName())) {
                resolvedPlayerId = rp.getPlayerName();
            }
        } catch (Exception ignored) {
        } finally {
            if (realmPlayer != null) realmPlayer.close();
        }
        final String playerId = resolvedPlayerId;
        final String managerIp = resolvedManagerIp;
        final String manualIp = resolvedManualIp;

        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManualIpEnabled(enableManualIp);
            settings.setKeepRatioEnabled(keepRatio);
            settings.setSwitchOnContentEnd(switchOnContentEnd);
            settings.setUsbAuthKey("");
            settings.setPlayerId(playerId == null ? "" : playerId);
            settings.setManagerIp(managerIp == null ? "" : managerIp);
            settings.setManualIp(manualIp == null ? "" : manualIp);
            settings.setSignalrPort(5000);
            settings.setSignalrHubPath("/Data");
            settings.setDataServerIp("");
            settings.setMessageServerIp("");
            settings.setFtpPort(AndoWSignageApp.FTP_PORT);
            settings.setFtpPasvMinPort(0);
            settings.setFtpPasvMaxPort(0);
            settings.setFtpRootPath("/NewHyOnEnt");
            applyBackupToSettings(settings, backupProperties);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateManualIPState(boolean state) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManualIpEnabled(state);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateKeepRatioState(boolean state) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setKeepRatioEnabled(state);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateSwitchOnContentEndState(boolean state) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setSwitchOnContentEnd(state);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateUsbAuthKey(String encodedKey) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setUsbAuthKey(encodedKey == null ? "" : encodedKey);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updatePlayerId(String playerId) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setPlayerId(playerId == null ? "" : playerId);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateManagerIp(String managerIp) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManagerIp(managerIp == null ? "" : managerIp);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateManualIp(String manualIp) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManualIp(manualIp == null ? "" : manualIp);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateSignalrPort(int port) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setSignalrPort(port);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static void updateSignalrHubPath(String hubPath) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setSignalrHubPath(hubPath == null ? "" : hubPath);
        });
        realm.close();
        persistCurrentSettings();
    }

    public static int getSignalrPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getSignalrPort() : 0;
        } finally {
            realm.close();
        }
    }

    public static String getSignalrHubPath() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getSignalrHubPath() : "";
        } finally {
            realm.close();
        }
    }

    public static String getUsbAuthKey() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getUsbAuthKey() : "";
        } finally {
            realm.close();
        }
    }

    public static String getManagerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings != null && !TextUtils.isEmpty(settings.getManagerIp())) {
                return settings.getManagerIp();
            }
            return AndoWSignageApp.MANAGER_IP == null ? "" : AndoWSignageApp.MANAGER_IP;
        } finally {
            realm.close();
        }
    }

    public static String getManualIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings != null && !TextUtils.isEmpty(settings.getManualIp())) {
                return settings.getManualIp();
            }
            return AndoWSignageApp.MANUAL_IP == null ? "" : AndoWSignageApp.MANUAL_IP;
        } finally {
            realm.close();
        }
    }

    public static void updateCommunicationSettings(String dataServerIp,
                                                   String messageServerIp,
                                                   int ftpPort,
                                                   int ftpPasvMinPort,
                                                   int ftpPasvMaxPort,
                                                   String ftpRootPath) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = rWhere(r);
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            if (!TextUtils.isEmpty(dataServerIp)) {
                settings.setDataServerIp(dataServerIp.trim());
            }
            if (!TextUtils.isEmpty(messageServerIp)) {
                settings.setMessageServerIp(messageServerIp.trim());
            } else {
                // Windows 플레이어와 동일하게 MessageServerIp가 없으면 비워둔다.
                // SignalR은 DataServerIp를 대체로 사용하지 않는다.
                settings.setMessageServerIp("");
            }
            if (ftpPort > 0 && ftpPort <= 65535) {
                settings.setFtpPort(ftpPort);
            }
            if (ftpPasvMinPort > 0 && ftpPasvMinPort <= 65535) {
                settings.setFtpPasvMinPort(ftpPasvMinPort);
            }
            if (ftpPasvMaxPort > 0 && ftpPasvMaxPort <= 65535) {
                settings.setFtpPasvMaxPort(ftpPasvMaxPort);
            }
            if (!TextUtils.isEmpty(ftpRootPath)) {
                settings.setFtpRootPath(normalizeFtpRootPath(ftpRootPath));
            }
        });
        realm.close();
        persistCurrentSettings();
    }

    public static String getDataServerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null || TextUtils.isEmpty(settings.getDataServerIp())) {
                return "";
            }
            return settings.getDataServerIp();
        } finally {
            realm.close();
        }
    }

    public static String getMessageServerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null) {
                return "";
            }
            if (!TextUtils.isEmpty(settings.getMessageServerIp())) {
                return settings.getMessageServerIp();
            }
            return "";
        } finally {
            realm.close();
        }
    }

    public static int getFtpPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null) {
                return AndoWSignageApp.FTP_PORT;
            }
            int value = settings.getFtpPort();
            if (value <= 0 || value > 65535) {
                return AndoWSignageApp.FTP_PORT;
            }
            return value;
        } finally {
            realm.close();
        }
    }

    public static String getFtpRootPath() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null || TextUtils.isEmpty(settings.getFtpRootPath())) {
                return "/NewHyOnEnt";
            }
            return normalizeFtpRootPath(settings.getFtpRootPath());
        } finally {
            realm.close();
        }
    }

    public static void applyStoredCommunicationSettings() {
        int ftpPort = getFtpPort();
        if (ftpPort > 0 && ftpPort <= 65535) {
            AndoWSignageApp.FTP_PORT = ftpPort;
        }
    }

    private static void ensureBackupFromRealmSettings(RealmLocalSettings settings) {
        File backupFile = getBackupFile();
        if (backupFile.exists()) {
            return;
        }
        writeBackupProperties(settings);
    }

    private static void persistCurrentSettings() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings != null) {
                writeBackupProperties(settings);
            }
        } finally {
            realm.close();
        }
    }

    private static File getBackupFile() {
        File rootDir = new File(Environment.getExternalStorageDirectory().getPath(), "AndoWSignage");
        if (!rootDir.exists() && !rootDir.mkdirs()) {
            Log.w(TAG, "getBackupFile: failed to create backup dir=" + rootDir.getAbsolutePath());
        }
        return new File(rootDir, BACKUP_FILE_NAME);
    }

    private static Properties readBackupProperties() {
        Properties properties = new Properties();
        File backupFile = getBackupFile();
        if (!backupFile.exists()) {
            return properties;
        }
        try (FileInputStream input = new FileInputStream(backupFile)) {
            properties.load(input);
        } catch (Exception ex) {
            Log.w(TAG, "readBackupProperties: failed to load backup file", ex);
        }
        return properties;
    }

    private static void writeBackupProperties(RealmLocalSettings settings) {
        if (settings == null) {
            return;
        }
        Properties properties = new Properties();
        properties.setProperty(KEY_ENABLE_MANUAL_IP, Boolean.toString(settings.isManualIpEnabled()));
        properties.setProperty(KEY_KEEP_RATIO, Boolean.toString(settings.isKeepRatioEnabled()));
        properties.setProperty(KEY_SWITCH_ON_CONTENT_END, Boolean.toString(settings.isSwitchOnContentEndEnabled()));
        properties.setProperty(KEY_PLAYER_ID, settings.getPlayerId() == null ? "" : settings.getPlayerId());
        properties.setProperty(KEY_MANAGER_IP, settings.getManagerIp() == null ? "" : settings.getManagerIp());
        properties.setProperty(KEY_MANUAL_IP, settings.getManualIp() == null ? "" : settings.getManualIp());
        properties.setProperty(KEY_SIGNALR_PORT, Integer.toString(settings.getSignalrPort()));
        properties.setProperty(KEY_SIGNALR_HUB_PATH, settings.getSignalrHubPath() == null ? "" : settings.getSignalrHubPath());
        properties.setProperty(KEY_DATA_SERVER_IP, settings.getDataServerIp() == null ? "" : settings.getDataServerIp());
        properties.setProperty(KEY_MESSAGE_SERVER_IP, settings.getMessageServerIp() == null ? "" : settings.getMessageServerIp());
        properties.setProperty(KEY_FTP_PORT, Integer.toString(settings.getFtpPort()));
        properties.setProperty(KEY_FTP_PASV_MIN_PORT, Integer.toString(settings.getFtpPasvMinPort()));
        properties.setProperty(KEY_FTP_PASV_MAX_PORT, Integer.toString(settings.getFtpPasvMaxPort()));
        properties.setProperty(KEY_FTP_ROOT_PATH, settings.getFtpRootPath() == null ? "" : settings.getFtpRootPath());
        properties.setProperty("usb_auth_key", settings.getUsbAuthKey() == null ? "" : settings.getUsbAuthKey());

        File backupFile = getBackupFile();
        try (FileOutputStream output = new FileOutputStream(backupFile)) {
            properties.store(output, "AndoW local settings backup");
        } catch (Exception ex) {
            Log.w(TAG, "writeBackupProperties: failed to write backup file", ex);
        }
    }

    private static void applyBackupToSettings(RealmLocalSettings settings, Properties properties) {
        if (settings == null || properties == null || properties.isEmpty()) {
            return;
        }
        settings.setManualIpEnabled(parseBoolean(properties, KEY_ENABLE_MANUAL_IP, settings.isManualIpEnabled()));
        settings.setKeepRatioEnabled(parseBoolean(properties, KEY_KEEP_RATIO, settings.isKeepRatioEnabled()));
        settings.setSwitchOnContentEnd(parseBoolean(properties, KEY_SWITCH_ON_CONTENT_END, settings.isSwitchOnContentEndEnabled()));
        settings.setPlayerId(getString(properties, KEY_PLAYER_ID, settings.getPlayerId()));
        settings.setManagerIp(getString(properties, KEY_MANAGER_IP, settings.getManagerIp()));
        settings.setManualIp(getString(properties, KEY_MANUAL_IP, settings.getManualIp()));
        settings.setUsbAuthKey(getString(properties, "usb_auth_key", settings.getUsbAuthKey()));
        int signalrPort = parseInt(properties, KEY_SIGNALR_PORT, settings.getSignalrPort());
        if (signalrPort > 0 && signalrPort <= 65535) {
            settings.setSignalrPort(signalrPort);
        }
        settings.setSignalrHubPath(getString(properties, KEY_SIGNALR_HUB_PATH, settings.getSignalrHubPath()));
        settings.setDataServerIp(getString(properties, KEY_DATA_SERVER_IP, settings.getDataServerIp()));
        settings.setMessageServerIp(getString(properties, KEY_MESSAGE_SERVER_IP, settings.getMessageServerIp()));
        int ftpPort = parseInt(properties, KEY_FTP_PORT, settings.getFtpPort());
        if (ftpPort > 0 && ftpPort <= 65535) {
            settings.setFtpPort(ftpPort);
        }
        int ftpPasvMin = parseInt(properties, KEY_FTP_PASV_MIN_PORT, settings.getFtpPasvMinPort());
        if (ftpPasvMin >= 0 && ftpPasvMin <= 65535) {
            settings.setFtpPasvMinPort(ftpPasvMin);
        }
        int ftpPasvMax = parseInt(properties, KEY_FTP_PASV_MAX_PORT, settings.getFtpPasvMaxPort());
        if (ftpPasvMax >= 0 && ftpPasvMax <= 65535) {
            settings.setFtpPasvMaxPort(ftpPasvMax);
        }
        String ftpRootPath = getString(properties, KEY_FTP_ROOT_PATH, settings.getFtpRootPath());
        if (!TextUtils.isEmpty(ftpRootPath)) {
            settings.setFtpRootPath(normalizeFtpRootPath(ftpRootPath));
        }
    }

    private static boolean parseBoolean(Properties properties, String key, boolean fallback) {
        String value = properties.getProperty(key);
        if (value == null) {
            return fallback;
        }
        return Boolean.parseBoolean(value.trim());
    }

    private static int parseInt(Properties properties, String key, int fallback) {
        String value = properties.getProperty(key);
        if (TextUtils.isEmpty(value)) {
            return fallback;
        }
        try {
            return Integer.parseInt(value.trim());
        } catch (NumberFormatException ex) {
            return fallback;
        }
    }

    private static String getString(Properties properties, String key, String fallback) {
        String value = properties.getProperty(key);
        if (value == null) {
            return fallback == null ? "" : fallback;
        }
        return value.trim();
    }

    private static String normalizeFtpRootPath(String rootPath) {
        if (TextUtils.isEmpty(rootPath)) {
            return "/NewHyOnEnt";
        }
        String normalized = rootPath.replace("\\", "/").trim();
        if (!normalized.startsWith("/")) {
            normalized = "/" + normalized;
        }
        while (normalized.length() > 1 && normalized.endsWith("/")) {
            normalized = normalized.substring(0, normalized.length() - 1);
        }
        return TextUtils.isEmpty(normalized) ? "/" : normalized;
    }

    private static RealmLocalSettings rWhere(Realm realm) {
        return realm.where(RealmLocalSettings.class)
                .equalTo("id", LOCAL_SETTINGS_ID)
                .findFirst();
    }

    public static boolean hasStoredUsbKeyForDevice() {
        return matchesStoredUsbKey(NetworkUtils.getMACAddress());
    }

    public static boolean matchesStoredUsbKey(String mac) {
        if (TextUtils.isEmpty(mac)) {
            return false;
        }
        String stored = getUsbAuthKey();
        if (TextUtils.isEmpty(stored)) {
            return false;
        }
        try {
            return AuthUtils.DecodeAuthKey(stored)
                    .equalsIgnoreCase(mac.replace(":", ""));
        } catch (Exception e) {
            return false;
        }
    }

}

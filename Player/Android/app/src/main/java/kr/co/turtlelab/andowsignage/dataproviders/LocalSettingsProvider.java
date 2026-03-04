package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmLocalSettings;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class LocalSettingsProvider {

    private static final String LOCAL_SETTINGS_ID = "local_settings";
    public static final String KEY_ENABLE_MANUAL_IP = "is_manual";
    public static final String KEY_KEEP_RATIO = "keepratio";
    public static final String KEY_SWITCH_ON_CONTENT_END = "switch_on_content_end";
    public static final String KEY_PLAYER_ID = "player_ip";
    public static final String KEY_MANAGER_IP = "manager_ip";
    public static final String KEY_DATA_SERVER_IP = "data_server_ip";
    public static final String KEY_MESSAGE_SERVER_IP = "message_server_ip";
    public static final String KEY_MANUAL_IP = "manual_ip";
    public static final String KEY_FTP_PORT = "ftp_port";
    public static final String KEY_FTP_PASV_MIN_PORT = "ftp_pasv_min_port";
    public static final String KEY_FTP_PASV_MAX_PORT = "ftp_pasv_max_port";
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
                model.setDataServerIp(settings.getDataServerIp());
                model.setMessageServerIp(settings.getMessageServerIp());
                model.setManualIp(settings.getManualIp());
                model.setFtpPort(settings.getFtpPort());
                model.setFtpPasvMinPort(settings.getFtpPasvMinPort());
                model.setFtpPasvMaxPort(settings.getFtpPasvMaxPort());
                model.setSignalrPort(settings.getSignalrPort());
                model.setSignalrHubPath(settings.getSignalrHubPath());
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
        final String dataServerIp = resolvedManagerIp;
        final String messageServerIp = resolvedManagerIp;
        final String manualIp = resolvedManualIp;
        final int ftpPort = AndoWSignageApp.FTP_PORT;

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
            settings.setDataServerIp(dataServerIp == null ? "" : dataServerIp);
            settings.setMessageServerIp(messageServerIp == null ? "" : messageServerIp);
            settings.setManualIp(manualIp == null ? "" : manualIp);
            settings.setFtpPort(ftpPort > 0 ? ftpPort : 21);
            settings.setFtpPasvMinPort(55536);
            settings.setFtpPasvMaxPort(55636);
            settings.setSignalrPort(5000);
            settings.setSignalrHubPath("/Data");
        });
        realm.close();
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
            String normalized = managerIp == null ? "" : managerIp;
            settings.setManagerIp(normalized);
            settings.setDataServerIp(normalized);
        });
        realm.close();
    }

    public static void updateDataServerIp(String dataServerIp) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setDataServerIp(dataServerIp == null ? "" : dataServerIp);
        });
        realm.close();
    }

    public static void updateMessageServerIp(String messageServerIp) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setMessageServerIp(messageServerIp == null ? "" : messageServerIp);
        });
        realm.close();
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
    }

    public static void updateFtpPort(int ftpPort) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setFtpPort(ftpPort);
        });
        realm.close();
    }

    public static void updateFtpPasvMinPort(int ftpPasvMinPort) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setFtpPasvMinPort(ftpPasvMinPort);
        });
        realm.close();
    }

    public static void updateFtpPasvMaxPort(int ftpPasvMaxPort) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setFtpPasvMaxPort(ftpPasvMaxPort);
        });
        realm.close();
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
    }

    public static void applyServerSettings(String dataServerIp,
                                           String messageServerIp,
                                           int ftpPort,
                                           int ftpPasvMinPort,
                                           int ftpPasvMaxPort) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            if (!TextUtils.isEmpty(dataServerIp)) {
                settings.setDataServerIp(dataServerIp.trim());
                settings.setManagerIp(dataServerIp.trim());
            }
            if (!TextUtils.isEmpty(messageServerIp)) {
                settings.setMessageServerIp(messageServerIp.trim());
            }
            if (ftpPort > 0) {
                settings.setFtpPort(ftpPort);
            }
            if (ftpPasvMinPort > 0) {
                settings.setFtpPasvMinPort(ftpPasvMinPort);
            }
            if (ftpPasvMaxPort > 0) {
                settings.setFtpPasvMaxPort(ftpPasvMaxPort);
            }
        });
        realm.close();
    }

    public static String getDataServerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null) {
                return "";
            }
            String host = settings.getDataServerIp();
            if (TextUtils.isEmpty(host)) {
                host = settings.getManagerIp();
            }
            return host == null ? "" : host;
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
            String host = settings.getMessageServerIp();
            if (TextUtils.isEmpty(host)) {
                host = settings.getManagerIp();
            }
            return host == null ? "" : host;
        } finally {
            realm.close();
        }
    }

    public static int getFtpPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getFtpPort() : 0;
        } finally {
            realm.close();
        }
    }

    public static int getFtpPasvMinPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getFtpPasvMinPort() : 0;
        } finally {
            realm.close();
        }
    }

    public static int getFtpPasvMaxPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getFtpPasvMaxPort() : 0;
        } finally {
            realm.close();
        }
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

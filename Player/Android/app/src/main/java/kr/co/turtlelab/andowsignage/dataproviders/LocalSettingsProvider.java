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
    public static final String KEY_MANUAL_IP = "manual_ip";

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
            settings.setManagerIp(managerIp == null ? "" : managerIp);
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

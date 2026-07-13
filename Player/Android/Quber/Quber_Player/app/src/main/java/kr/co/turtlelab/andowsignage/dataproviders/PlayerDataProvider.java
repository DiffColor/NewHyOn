package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.datamodels.PlayerDataModel;

public class PlayerDataProvider {

    public static final String KEY_MANAGER_IP = "manager_ip";
    public static final String KEY_PLAYER_ID = "player_id";
    public static final String KEY_ENABLE_MANUAL_IP = "is_manual";
    public static final String KEY_MANUAL_IP = "manual_ip";
    public static final String KEY_KEEP_RATIO = "keepratio";
    private static final String LEGACY_LOCAL_PLAYER_ID = "local_player";

    private PlayerDataProvider() {
    }

    public static PlayerDataModel getPlayerData() {
        PlayerDataModel playerData = new PlayerDataModel();
        LocalSettingsModel local = LocalSettingsProvider.getLocalSettings().get(0);
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer realmPlayer = realm.where(RealmPlayer.class).findFirst();
            if (isLegacyLocalPlayer(realmPlayer)) {
                // GUID가 확정되는 시점에 인증값을 실제 플레이어 레코드로 옮긴다.
                realmPlayer = null;
            }
            if (realmPlayer != null) {
                playerData.setPlayerId(realmPlayer.getPlayerId());
                String name = realmPlayer.getPlayerName();
                if (TextUtils.isEmpty(name)) {
                    name = local.getPlayerId();
                    if (TextUtils.isEmpty(name)) {
                        name = AndoWSignageApp.PLAYER_ID;
                    }
                }
                playerData.setPlayerName(name);
                playerData.setPlaylist(TextUtils.isEmpty(realmPlayer.getPlaylistName()) ? "" : realmPlayer.getPlaylistName());
                playerData.setIsLandscape(String.valueOf(realmPlayer.isLandscape()));
            } else {
                String playerId = local.getPlayerId();
                if (TextUtils.isEmpty(playerId)) {
                    playerId = AndoWSignageApp.PLAYER_ID;
                }
                playerData.setPlayerId(playerId);
                playerData.setPlayerName(playerId);
                playerData.setPlaylist("");
                playerData.setIsLandscape(String.valueOf(true));
            }
        } finally {
            realm.close();
        }

        boolean manual = local.getManualIPState();
        String manualIp = local.getManualIp();
        playerData.setPlayerIP(manual ? manualIp : "");
        String managerIp = local.getManagerIp();
        if (TextUtils.isEmpty(managerIp)) {
            managerIp = AndoWSignageApp.MANAGER_IP;
        }
        playerData.setManagerIP(managerIp);
        return playerData;
    }

    public static void updatePlayerName() {
        LocalSettingsProvider.updatePlayerId(AndoWSignageApp.PLAYER_ID);
        RethinkDbClient.getInstance().preparePlayerNameChange(AndoWSignageApp.PLAYER_ID);
    }

    public static void updateCurrentPListName(String playlistName) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmPlayer player = r.where(RealmPlayer.class).findFirst();
            if (player != null) {
                player.setPlaylistName(playlistName);
            }
        });
        realm.close();
    }

    public static boolean updatePlayerAuthInfo(String authKey, String fingerprint) {
        Realm realm = Realm.getDefaultInstance();
        try {
            LocalSettingsProvider.updatePlayerAuthKey(authKey);
            realm.executeTransaction(r -> {
                RealmPlayer player = r.where(RealmPlayer.class).findFirst();
                if (player != null && !isLegacyLocalPlayer(player)) {
                    player.setPifAuthKey(authKey == null ? "" : authKey);
                    player.setPifFingerprint(fingerprint == null ? "" : fingerprint);
                }
            });
            RethinkDbClient.getInstance().invalidateDeviceInfoSync();
            return true;
        } catch (Throwable ignored) {
            return false;
        } finally {
            realm.close();
        }
    }

    public static String getPlayerAuthKey() {
        String stored = LocalSettingsProvider.getPlayerAuthKey();
        if (!TextUtils.isEmpty(stored)) {
            return stored;
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer player = realm.where(RealmPlayer.class).findFirst();
            if (player == null || player.getPifAuthKey() == null) {
                return "";
            }
            return player.getPifAuthKey();
        } finally {
            realm.close();
        }
    }

    public static String getPlayerAuthFingerprint() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer player = realm.where(RealmPlayer.class).findFirst();
            if (player == null || player.getPifFingerprint() == null) {
                return "";
            }
            return player.getPifFingerprint();
        } finally {
            realm.close();
        }
    }

    public static void updateManagerIP() {
        LocalSettingsProvider.updateManagerIp(AndoWSignageApp.MANAGER_IP);
    }

    public static void updateManualIP() {
        LocalSettingsProvider.updateManualIp(AndoWSignageApp.MANUAL_IP);
    }

    public static void updateOrientation(boolean isLandscape) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmPlayer player = r.where(RealmPlayer.class).findFirst();
            if (player != null) {
                player.setLandscape(isLandscape);
            }
        });
        realm.close();
    }

    public static boolean isLegacyLocalPlayerId(String playerId) {
        return LEGACY_LOCAL_PLAYER_ID.equalsIgnoreCase(playerId == null ? "" : playerId.trim());
    }

    private static boolean isLegacyLocalPlayer(RealmPlayer player) {
        return player != null && isLegacyLocalPlayerId(player.getPlayerId());
    }

}

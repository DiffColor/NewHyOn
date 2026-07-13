package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.store.StoredPlayer;
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
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPlayer storedPlayer = storeDb.where(StoredPlayer.class).findFirst();
            if (isLegacyLocalPlayer(storedPlayer)) {
                if (migrateLegacyLocalPlayerAuth(storedPlayer)) {
                    StoredPlayer legacyPlayer = storedPlayer;
                    storeDb.executeTransaction(r -> r.delete(legacyPlayer));
                    RethinkDbClient.getInstance().invalidateDeviceInfoSync();
                }
                storedPlayer = null;
            }
            if (storedPlayer != null) {
                playerData.setPlayerId(storedPlayer.getPlayerId());
                String name = storedPlayer.getPlayerName();
                if (TextUtils.isEmpty(name)) {
                    name = local.getPlayerId();
                    if (TextUtils.isEmpty(name)) {
                        name = AndoWSignageApp.PLAYER_ID;
                    }
                }
                playerData.setPlayerName(name);
                playerData.setPlaylist(TextUtils.isEmpty(storedPlayer.getPlaylistName()) ? "" : storedPlayer.getPlaylistName());
                playerData.setIsLandscape(String.valueOf(storedPlayer.isLandscape()));
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
            storeDb.close();
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
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        storeDb.executeTransaction(r -> {
            StoredPlayer player = r.where(StoredPlayer.class).findFirst();
            if (player != null) {
                player.setPlaylistName(playlistName);
            }
        });
        storeDb.close();
    }

    public static boolean updatePlayerAuthInfo(String authKey, String fingerprint) {
        if (!saveAuthInfo(authKey, fingerprint)) {
            return false;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            storeDb.executeTransaction(r -> {
                StoredPlayer player = r.where(StoredPlayer.class).findFirst();
                if (isLegacyLocalPlayer(player)) {
                    r.delete(player);
                }
            });
            RethinkDbClient.getInstance().invalidateDeviceInfoSync();
            return true;
        } catch (Throwable ignored) {
            return false;
        } finally {
            storeDb.close();
        }
    }

    public static String getPlayerAuthKey() {
        String stored = LocalSettingsProvider.getPifAuthKey();
        if (!TextUtils.isEmpty(stored)) {
            return stored;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPlayer player = storeDb.where(StoredPlayer.class).findFirst();
            if (player == null || player.getPifAuthKey() == null) {
                return "";
            }
            String authKey = player.getPifAuthKey();
            String fingerprint = player.getPifFingerprint();
            if (saveAuthInfo(authKey, fingerprint) && isLegacyLocalPlayer(player)) {
                storeDb.executeTransaction(r -> r.delete(player));
                RethinkDbClient.getInstance().invalidateDeviceInfoSync();
            }
            return authKey;
        } finally {
            storeDb.close();
        }
    }

    public static String getPlayerAuthFingerprint() {
        String stored = LocalSettingsProvider.getPifFingerprint();
        if (!TextUtils.isEmpty(stored)) {
            return stored;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPlayer player = storeDb.where(StoredPlayer.class).findFirst();
            if (player == null || player.getPifFingerprint() == null) {
                return "";
            }
            String authKey = player.getPifAuthKey();
            String fingerprint = player.getPifFingerprint();
            if (saveAuthInfo(authKey, fingerprint) && isLegacyLocalPlayer(player)) {
                storeDb.executeTransaction(r -> r.delete(player));
                RethinkDbClient.getInstance().invalidateDeviceInfoSync();
            }
            return fingerprint;
        } finally {
            storeDb.close();
        }
    }

    public static void updateManagerIP() {
        LocalSettingsProvider.updateManagerIp(AndoWSignageApp.MANAGER_IP);
    }

    public static void updateManualIP() {
        LocalSettingsProvider.updateManualIp(AndoWSignageApp.MANUAL_IP);
    }

    public static void updateOrientation(boolean isLandscape) {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        storeDb.executeTransaction(r -> {
            StoredPlayer player = r.where(StoredPlayer.class).findFirst();
            if (player != null) {
                player.setLandscape(isLandscape);
            }
        });
        storeDb.close();
    }

    public static boolean isLegacyLocalPlayerId(String playerId) {
        return LEGACY_LOCAL_PLAYER_ID.equalsIgnoreCase(playerId == null ? "" : playerId.trim());
    }

    private static boolean isLegacyLocalPlayer(StoredPlayer player) {
        return player != null && isLegacyLocalPlayerId(player.getPlayerId());
    }

    private static boolean migrateLegacyLocalPlayerAuth(StoredPlayer player) {
        if (player == null) {
            return false;
        }
        String authKey = LocalSettingsProvider.getPifAuthKey();
        String fingerprint = LocalSettingsProvider.getPifFingerprint();
        if (TextUtils.isEmpty(authKey)) {
            authKey = player.getPifAuthKey();
        }
        if (TextUtils.isEmpty(fingerprint)) {
            fingerprint = player.getPifFingerprint();
        }
        return saveAuthInfo(authKey, fingerprint);
    }

    private static boolean saveAuthInfo(String authKey, String fingerprint) {
        return LocalSettingsProvider.updatePifAuthInfo(authKey, fingerprint);
    }
}

package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.datamodels.PlayerDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;

public class PlayerDataProvider {

    public static final String KEY_MANAGER_IP = "manager_ip";
    public static final String KEY_PLAYER_ID = "player_id";
    public static final String KEY_ENABLE_MANUAL_IP = "is_manual";
    public static final String KEY_MANUAL_IP = "manual_ip";
    public static final String KEY_KEEP_RATIO = "keepratio";

    private PlayerDataProvider() {
    }

    public static PlayerDataModel getPlayerData() {
        PlayerDataModel playerData = new PlayerDataModel();
        LocalSettingsModel local = LocalSettingsProvider.getLocalSettings().get(0);
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPlayer realmPlayer = realm.where(RealmPlayer.class).findFirst();
            if (realmPlayer != null) {
                String name = realmPlayer.getPlayerName();
                if (TextUtils.isEmpty(name)) {
                    name = local.getPlayerId();
                    if (TextUtils.isEmpty(name)) {
                        name = AndoWSignageApp.PLAYER_ID;
                    }
                }
                playerData.setPlayerName(name);
                playerData.setPlaylist(realmPlayer.getPlaylistName());
                playerData.setIsLandscape(String.valueOf(realmPlayer.isLandscape()));
            } else {
                String playerId = local.getPlayerId();
                if (TextUtils.isEmpty(playerId)) {
                    playerId = AndoWSignageApp.PLAYER_ID;
                }
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
}

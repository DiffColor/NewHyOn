package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

public class RealmPlayer extends RealmObject {

    @PrimaryKey
    private String playerId;
    private String playerName;
    private String playlistName;
    private String pifAuthKey = "";
    private String pifFingerprint = "";
    private boolean landscape;

    public String getPlayerId() {
        return playerId;
    }

    public void setPlayerId(String playerId) {
        this.playerId = playerId;
    }

    public String getPlayerName() {
        return playerName;
    }

    public void setPlayerName(String playerName) {
        this.playerName = playerName;
    }

    public String getPlaylistName() {
        return playlistName;
    }

    public void setPlaylistName(String playlistName) {
        this.playlistName = playlistName;
    }

    public String getPifAuthKey() {
        return pifAuthKey;
    }

    public void setPifAuthKey(String pifAuthKey) {
        this.pifAuthKey = pifAuthKey;
    }

    public String getPifFingerprint() {
        return pifFingerprint;
    }

    public void setPifFingerprint(String pifFingerprint) {
        this.pifFingerprint = pifFingerprint;
    }

    public boolean isLandscape() {
        return landscape;
    }

    public void setLandscape(boolean landscape) {
        this.landscape = landscape;
    }
}

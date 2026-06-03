package kr.co.turtlelab.andowsignage.data.store;

import io.objectbox.annotation.Entity;
import io.objectbox.annotation.Id;
import io.objectbox.annotation.Unique;
import kr.co.turtlelab.andowsignage.data.objectbox.BusinessId;

@Entity
public class StoredPlayer {

    @Id
    private long objectBoxId;

    @BusinessId
    @Unique
    private String playerId;
    private String playerName;
    private String playlistName;
    private String pifAuthKey = "";
    private String pifFingerprint = "";
    private boolean landscape;

    public long getObjectBoxId() {
        return objectBoxId;
    }

    public void setObjectBoxId(long objectBoxId) {
        this.objectBoxId = objectBoxId;
    }

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

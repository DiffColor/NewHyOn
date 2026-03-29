package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

/**
 * Windows SpecialScheduleCache와 동일 목적의 로컬 캐시.
 * 복잡한 중첩 구조는 JSON 문자열로 보관한다.
 */
public class RealmSpecialScheduleCache extends RealmObject {

    @PrimaryKey
    private String id;
    private String playerId;
    private String playerName;
    private String updatedAt;
    private String schedulesJson;
    private String playlistsJson;

    public String getId() {
        return id;
    }

    public void setId(String id) {
        this.id = id;
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

    public String getUpdatedAt() {
        return updatedAt;
    }

    public void setUpdatedAt(String updatedAt) {
        this.updatedAt = updatedAt;
    }

    public String getSchedulesJson() {
        return schedulesJson;
    }

    public void setSchedulesJson(String schedulesJson) {
        this.schedulesJson = schedulesJson;
    }

    public String getPlaylistsJson() {
        return playlistsJson;
    }

    public void setPlaylistsJson(String playlistsJson) {
        this.playlistsJson = playlistsJson;
    }
}

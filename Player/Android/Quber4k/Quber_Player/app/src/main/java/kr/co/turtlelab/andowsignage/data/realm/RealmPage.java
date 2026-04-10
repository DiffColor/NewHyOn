package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmList;
import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

public class RealmPage extends RealmObject {

    @PrimaryKey
    private String pageId;
    private String pageName;
    private String playlistName;
    private int orderIndex;
    private int playHour;
    private int playMinute;
    private int playSecond;
    private int volume;
    private boolean landscape;
    private double canvasWidth = 1920;
    private double canvasHeight = 1080;
    private RealmList<RealmElement> elements;

    public String getPageId() {
        return pageId;
    }

    public void setPageId(String pageId) {
        this.pageId = pageId;
    }

    public String getPageName() {
        return pageName;
    }

    public void setPageName(String pageName) {
        this.pageName = pageName;
    }

    public String getPlaylistName() {
        return playlistName;
    }

    public void setPlaylistName(String playlistName) {
        this.playlistName = playlistName;
    }

    public int getOrderIndex() {
        return orderIndex;
    }

    public void setOrderIndex(int orderIndex) {
        this.orderIndex = orderIndex;
    }

    public int getPlayHour() {
        return playHour;
    }

    public void setPlayHour(int playHour) {
        this.playHour = playHour;
    }

    public int getPlayMinute() {
        return playMinute;
    }

    public void setPlayMinute(int playMinute) {
        this.playMinute = playMinute;
    }

    public int getPlaySecond() {
        return playSecond;
    }

    public void setPlaySecond(int playSecond) {
        this.playSecond = playSecond;
    }

    public int getVolume() {
        return volume;
    }

    public void setVolume(int volume) {
        this.volume = volume;
    }

    public boolean isLandscape() {
        return landscape;
    }

    public void setLandscape(boolean landscape) {
        this.landscape = landscape;
    }

    public double getCanvasWidth() {
        return canvasWidth;
    }

    public void setCanvasWidth(double canvasWidth) {
        this.canvasWidth = canvasWidth;
    }

    public double getCanvasHeight() {
        return canvasHeight;
    }

    public void setCanvasHeight(double canvasHeight) {
        this.canvasHeight = canvasHeight;
    }

    public RealmList<RealmElement> getElements() {
        return elements;
    }

    public void setElements(RealmList<RealmElement> elements) {
        this.elements = elements;
    }
}

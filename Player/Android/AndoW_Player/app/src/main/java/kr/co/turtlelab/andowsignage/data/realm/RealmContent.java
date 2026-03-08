package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

public class RealmContent extends RealmObject {

    @PrimaryKey
    private String uid;
    private String elementId;
    private String fileName;
    private String fileFullPath;
    private String contentType;
    private String playMinute;
    private String playSecond;
    private boolean valid;
    private boolean fileExist;
    private int scrollSpeedSec;

    public String getUid() {
        return uid;
    }

    public void setUid(String uid) {
        this.uid = uid;
    }

    public String getElementId() {
        return elementId;
    }

    public void setElementId(String elementId) {
        this.elementId = elementId;
    }

    public String getFileName() {
        return fileName;
    }

    public void setFileName(String fileName) {
        this.fileName = fileName;
    }

    public String getFileFullPath() {
        return fileFullPath;
    }

    public void setFileFullPath(String fileFullPath) {
        this.fileFullPath = fileFullPath;
    }

    public String getContentType() {
        return contentType;
    }

    public void setContentType(String contentType) {
        this.contentType = contentType;
    }

    public String getPlayMinute() {
        return playMinute;
    }

    public void setPlayMinute(String playMinute) {
        this.playMinute = playMinute;
    }

    public String getPlaySecond() {
        return playSecond;
    }

    public void setPlaySecond(String playSecond) {
        this.playSecond = playSecond;
    }

    public boolean isContentValid() {
        return valid;
    }

    public void setValid(boolean valid) {
        this.valid = valid;
    }

    public boolean isFileExist() {
        return fileExist;
    }

    public void setFileExist(boolean fileExist) {
        this.fileExist = fileExist;
    }

    public int getScrollSpeedSec() {
        return scrollSpeedSec;
    }

    public void setScrollSpeedSec(int scrollSpeedSec) {
        this.scrollSpeedSec = scrollSpeedSec;
    }
}

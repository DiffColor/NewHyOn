package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

public class RealmLocalSettings extends RealmObject {

    @PrimaryKey
    private String id;
    private boolean manualIpEnabled;
    private boolean keepRatioEnabled;
    private String usbAuthKey = "";
    private boolean switchOnContentEnd;
    private String playerId = "";
    private String managerIp = "";
    private String manualIp = "";

    public String getId() {
        return id;
    }

    public void setId(String id) {
        this.id = id;
    }

    public boolean isManualIpEnabled() {
        return manualIpEnabled;
    }

    public void setManualIpEnabled(boolean manualIpEnabled) {
        this.manualIpEnabled = manualIpEnabled;
    }

    public boolean isKeepRatioEnabled() {
        return keepRatioEnabled;
    }

    public void setKeepRatioEnabled(boolean keepRatioEnabled) {
        this.keepRatioEnabled = keepRatioEnabled;
    }

    public String getUsbAuthKey() {
        return usbAuthKey;
    }

    public void setUsbAuthKey(String usbAuthKey) {
        this.usbAuthKey = usbAuthKey;
    }

    public boolean isSwitchOnContentEndEnabled() {
        return switchOnContentEnd;
    }

    public void setSwitchOnContentEnd(boolean switchOnContentEnd) {
        this.switchOnContentEnd = switchOnContentEnd;
    }

    public String getPlayerId() {
        return playerId;
    }

    public void setPlayerId(String playerId) {
        this.playerId = playerId;
    }

    public String getManagerIp() {
        return managerIp;
    }

    public void setManagerIp(String managerIp) {
        this.managerIp = managerIp;
    }

    public String getManualIp() {
        return manualIp;
    }

    public void setManualIp(String manualIp) {
        this.manualIp = manualIp;
    }
}

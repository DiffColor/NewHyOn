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
    private String dataServerIp = "";
    private String messageServerIp = "";
    private String manualIp = "";
    private int ftpPort;
    private int ftpPasvMinPort;
    private int ftpPasvMaxPort;
    private String ftpRootPath = "/NewHyOnEnt";
    private int signalrPort;
    private String signalrHubPath = "";

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

    public String getDataServerIp() {
        return dataServerIp;
    }

    public void setDataServerIp(String dataServerIp) {
        this.dataServerIp = dataServerIp;
    }

    public String getMessageServerIp() {
        return messageServerIp;
    }

    public void setMessageServerIp(String messageServerIp) {
        this.messageServerIp = messageServerIp;
    }

    public String getManualIp() {
        return manualIp;
    }

    public void setManualIp(String manualIp) {
        this.manualIp = manualIp;
    }

    public int getFtpPort() {
        return ftpPort;
    }

    public void setFtpPort(int ftpPort) {
        this.ftpPort = ftpPort;
    }

    public int getFtpPasvMinPort() {
        return ftpPasvMinPort;
    }

    public void setFtpPasvMinPort(int ftpPasvMinPort) {
        this.ftpPasvMinPort = ftpPasvMinPort;
    }

    public int getFtpPasvMaxPort() {
        return ftpPasvMaxPort;
    }

    public void setFtpPasvMaxPort(int ftpPasvMaxPort) {
        this.ftpPasvMaxPort = ftpPasvMaxPort;
    }

    public String getFtpRootPath() {
        return ftpRootPath;
    }

    public void setFtpRootPath(String ftpRootPath) {
        this.ftpRootPath = ftpRootPath;
    }

    public int getSignalrPort() {
        return signalrPort;
    }

    public void setSignalrPort(int signalrPort) {
        this.signalrPort = signalrPort;
    }

    public String getSignalrHubPath() {
        return signalrHubPath;
    }

    public void setSignalrHubPath(String signalrHubPath) {
        this.signalrHubPath = signalrHubPath;
    }
}

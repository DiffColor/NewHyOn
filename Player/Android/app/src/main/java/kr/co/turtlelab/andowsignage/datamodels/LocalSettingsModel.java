package kr.co.turtlelab.andowsignage.datamodels;

public class LocalSettingsModel {

	boolean manualIPState = false;
	boolean keepRatioState = false;
	boolean switchOnContentEnd = false;
	String usbAuthKey = "";
    String playerId = "";
    String managerIp = "";
    String dataServerIp = "";
    String messageServerIp = "";
    String manualIp = "";
    int ftpPort = 0;
    int ftpPasvMinPort = 0;
    int ftpPasvMaxPort = 0;
    int signalrPort = 0;
    String signalrHubPath = "";

	
	/*
	 * Setter Methods
	 */
	public void setManualIPState(boolean state) {
		manualIPState = state;
	}
	
	public void setKeepRatioState(boolean state) {
		keepRatioState = state;
	}

	public void setUsbAuthKey(String key) {
		usbAuthKey = key;
	}

	public void setSwitchOnContentEnd(boolean state) {
		switchOnContentEnd = state;
	}

    public void setPlayerId(String id) {
        playerId = id == null ? "" : id;
    }

    public void setManagerIp(String ip) {
        managerIp = ip == null ? "" : ip;
    }

    public void setDataServerIp(String ip) {
        dataServerIp = ip == null ? "" : ip;
    }

    public void setMessageServerIp(String ip) {
        messageServerIp = ip == null ? "" : ip;
    }

    public void setManualIp(String ip) {
        manualIp = ip == null ? "" : ip;
    }

    public void setFtpPort(int port) {
        ftpPort = port;
    }

    public void setFtpPasvMinPort(int port) {
        ftpPasvMinPort = port;
    }

    public void setFtpPasvMaxPort(int port) {
        ftpPasvMaxPort = port;
    }

    public void setSignalrPort(int port) {
        signalrPort = port;
    }

    public void setSignalrHubPath(String path) {
        signalrHubPath = path == null ? "" : path;
    }
		
	/*
	 * Getter Methods
	 */
	public boolean getManualIPState() {
		return manualIPState;
	}
	
	public boolean getKeepRatioState() {
		return keepRatioState;
	}

	public String getUsbAuthKey() {
		return usbAuthKey;
	}

	public boolean getSwitchOnContentEnd() {
		return switchOnContentEnd;
	}

    public String getPlayerId() {
        return playerId;
    }

    public String getManagerIp() {
        return managerIp;
    }

    public String getDataServerIp() {
        return dataServerIp;
    }

    public String getMessageServerIp() {
        return messageServerIp;
    }

    public String getManualIp() {
        return manualIp;
    }

    public int getFtpPort() {
        return ftpPort;
    }

    public int getFtpPasvMinPort() {
        return ftpPasvMinPort;
    }

    public int getFtpPasvMaxPort() {
        return ftpPasvMaxPort;
    }

    public int getSignalrPort() {
        return signalrPort;
    }

    public String getSignalrHubPath() {
        return signalrHubPath;
    }
	
}

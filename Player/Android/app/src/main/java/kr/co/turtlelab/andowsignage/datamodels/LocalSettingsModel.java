package kr.co.turtlelab.andowsignage.datamodels;

public class LocalSettingsModel {

	boolean manualIPState = false;
	boolean keepRatioState = false;
	boolean switchOnContentEnd = false;
	String usbAuthKey = "";
    String playerId = "";
    String managerIp = "";
    String manualIp = "";

	
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

    public void setManualIp(String ip) {
        manualIp = ip == null ? "" : ip;
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

    public String getManualIp() {
        return manualIp;
    }
	
}

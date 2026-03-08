package kr.co.turtlelab.andowsignage.datamodels;

public class PlayerDataModel {

	String playerName = "";
	String playlist = "";
	String playerIP = "";
	String managerIP = "";
	boolean isLandscape = false;
	boolean isAllDay = false;
	
	/*
	 * Setter Methods
	 */
	public void setPlayerName(String playername) {
		playerName = playername;
	}
	
	public void setPlaylist(String playlist) {
		this.playlist = playlist;
	}
	
	public void setPlayerIP(String ip) {
		playerIP = ip;
	}
	
	public void setManagerIP(String ip) {
		managerIP = ip;
	}
	
	public void setIsLandscape(String islandscape) {
		isLandscape = Boolean.parseBoolean(islandscape);
	}
	
	/*
	 * Getter Methods
	 */
	public String getPlayerName() {
		return playerName;
	}
	
	public String getPlaylist() {
		return playlist;
	}
	
	public String getPlayerIP() {
		return playerIP;
	}
	
	public String getManagerIP() {
		return managerIP;
	}
	
	public boolean getIsLandscape() {
		return isLandscape;
	}

}

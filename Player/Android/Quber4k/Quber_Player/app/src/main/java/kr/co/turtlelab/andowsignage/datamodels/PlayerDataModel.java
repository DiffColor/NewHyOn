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
		playerName = playername == null ? "" : playername;
	}
	
	public void setPlaylist(String playlist) {
		this.playlist = playlist == null ? "" : playlist;
	}
	
	public void setPlayerIP(String ip) {
		playerIP = ip == null ? "" : ip;
	}
	
	public void setManagerIP(String ip) {
		managerIP = ip == null ? "" : ip;
	}
	
	public void setIsLandscape(String islandscape) {
		isLandscape = Boolean.parseBoolean(islandscape);
	}
	
	/*
	 * Getter Methods
	 */
	public String getPlayerName() {
		return playerName == null ? "" : playerName;
	}
	
	public String getPlaylist() {
		return playlist == null ? "" : playlist;
	}
	
	public String getPlayerIP() {
		return playerIP == null ? "" : playerIP;
	}
	
	public String getManagerIP() {
		return managerIP == null ? "" : managerIP;
	}
	
	public boolean getIsLandscape() {
		return isLandscape;
	}

}

package kr.co.turtlelab.andowsignage.datamodels;


public class PageDataModel {

	String PIC_PageName = "";
	String PIC_PlaytimeHour = "0";
	String PIC_PlaytimeMinute = "0";
	String PIC_PlaytimeSecond = "0";
	String PIC_Volume = "0";
	String PIC_GUID = "";
	double PIC_CanvasWidth = 1920;
	double PIC_CanvasHeight = 1080;
	boolean PIC_IsLandscape = true;
	
	long playTimeSec = 0;
	
	public PageDataModel() {
	}
	
	public PageDataModel(String pageName, String timeHour, String timeMin, String timeSec, String volume, String guid) {
		PIC_PageName = pageName;
		PIC_PlaytimeHour = timeHour;
		PIC_PlaytimeMinute = timeMin;
		PIC_PlaytimeSecond = timeSec;
		PIC_Volume = volume;
		PIC_GUID = guid;
	}
	
	/*
	 * Setter Methods
	 */
	public void setPageName(String pagename) {
		PIC_PageName = pagename;
	}
	
	public void setPlayTime(String hourStr, String minStr, String secStr) {
		playTimeSec = 0;
		
		PIC_PlaytimeHour = hourStr;
		PIC_PlaytimeMinute = minStr;
		PIC_PlaytimeSecond = secStr;
		
		playTimeSec = (Integer.parseInt(hourStr)*60 + Integer.parseInt(minStr))*60 + Integer.parseInt(secStr);
	}
	
	public void setVolume(String volStr) {
		PIC_Volume = volStr;
	}
	
	public void setGUID(String guid) {
		PIC_GUID = guid;
	}

	public void setCanvasSize(double width, double height) {
		PIC_CanvasWidth = width > 0 ? width : 1920;
		PIC_CanvasHeight = height > 0 ? height : 1080;
	}

	public void setLandscape(boolean landscape) {
		PIC_IsLandscape = landscape;
	}
	
	/*
	 * Getter Methods
	 */
	public String getPageName() {
		return PIC_PageName;
	}
	
	public long getPlayTimeSec() {
		return playTimeSec;
	}
	
	public String[] getPlayTime() {
		return new String[] { PIC_PlaytimeHour, PIC_PlaytimeMinute, PIC_PlaytimeSecond };
	}
	
	public int getPlayVolume() {
		return Integer.parseInt(PIC_Volume);
	}
	
	public String getGUID() {
		return PIC_GUID;
	}

	public double getCanvasWidth() {
		return PIC_CanvasWidth > 0 ? PIC_CanvasWidth : (PIC_IsLandscape ? 1920 : 1080);
	}

	public double getCanvasHeight() {
		return PIC_CanvasHeight > 0 ? PIC_CanvasHeight : (PIC_IsLandscape ? 1080 : 1920);
	}

	public boolean isLandscape() {
		return PIC_IsLandscape;
	}
}

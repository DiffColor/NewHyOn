package kr.co.turtlelab.andowsignage.datamodels;


public class PageDataModel {

	String PIC_PageName = "";
	String PIC_PlaytimeHour = "0";
	String PIC_PlaytimeMinute = "0";
	String PIC_PlaytimeSecond = "0";
	String PIC_Volume = "0";
	String PIC_GUID = "";
	
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
}

package kr.co.turtlelab.andowsignage.datamodels;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.AndoWSignageApp.DAY_OF_WEEK;

public class WeeklyScheduleDataModel {
	
	String WPS_DayOfWeek = DAY_OF_WEEK.MON.toString();
	String WPS_Hour1 = "09";
	String WPS_Min1 = "00";
	String WPS_Hour2 = "0";
	String WPS_Min2 = "0";
	String WPS_IsOnAir = "true";
			
	public WeeklyScheduleDataModel() {
		
	}
	
	public WeeklyScheduleDataModel(String day, String fromHour, String fromMin, String toHour, String toMin, String isOnAir) {
		WPS_DayOfWeek = day;
		WPS_Hour1 = fromHour;
		WPS_Min1 = fromMin;
		WPS_Hour2 = toHour;
		WPS_Min2 = toMin;
		WPS_IsOnAir = isOnAir;
	}
	
	/*Setter*/
	public void setDay(String day) {
		WPS_DayOfWeek = day;
	}
	
	public void setDay(AndoWSignageApp.DAY_OF_WEEK day) {
		WPS_DayOfWeek = day.toString();
	}
	public void setFrom(String hour, String minute) {
		WPS_Hour1 = String.format("%02d", Integer.parseInt(hour));
		WPS_Min1 = String.format("%02d", Integer.parseInt(minute));
	}
	
	public void setTo(String hour, String minute) {
		WPS_Hour2 = String.format("%02d", Integer.parseInt(hour));
		WPS_Min2 = String.format("%02d", Integer.parseInt(minute));
	}
	
	public void setOnAir(String isOnAir) {
		WPS_IsOnAir = isOnAir;
	}
	
	/*Getter*/
	public String getDayStr() {
		return WPS_DayOfWeek;
	}
	
	public DAY_OF_WEEK getDay() {
		return DAY_OF_WEEK.valueOf(WPS_DayOfWeek);
	}
	
	public int[] getFrom() {
		return new int[] { Integer.parseInt(WPS_Hour1), Integer.parseInt(WPS_Min1) };
	}
	
	public String getFromStr() {
		return String.format("%s:%s", WPS_Hour1, WPS_Min1);
	}
	
	public int[] getTo() {
		return new int[] { Integer.parseInt(WPS_Hour2), Integer.parseInt(WPS_Min2) };
	}
	
	public String getToStr() {
		return String.format("%s:%s", WPS_Hour2, WPS_Min2);
	}
	
	public boolean getOnAir() {
		return Boolean.parseBoolean(WPS_IsOnAir);
	}
}

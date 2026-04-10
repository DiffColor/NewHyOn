package kr.co.turtlelab.andowsignage.datamodels;


public class ScrolltextDataModel {

	String text;
	String backColor = "#12BE2323";
	String foreColor = "#FFFDF7F7";
	String fontName = "";
	String fontFileName = "";
//	boolean isBold = false;
//	boolean isItalic = false;
	int scrolltime = 12;
	
	/*
	 * Setter Methods	
	 */
	public void setText(String text) {
		this.text = text;
	}

	public void setBackColor(String color) {
		if(color.isEmpty()) {
			backColor = "#000000";
			return;
		}
		backColor = color;
	}
	
	public void setForeColor(String color) {
		if(color.isEmpty()) { 
			foreColor = "#FFFFFF";
			return;
		}
		foreColor = color;
	}
	
	public void setFont(String font) {
		fontName = font;
	}
	
	public void setFontFileName(String filename) {
		fontFileName = filename;
	}
	
//	public void setBold(String isbold) {
//		isBold = Boolean.parseBoolean(isbold);
//	}
//	
//	public void setItalic(String isitalic) {
//		isItalic = Boolean.parseBoolean(isitalic);
//	}

	public void setScrolltime(String timeStr) {
		scrolltime = Integer.parseInt(timeStr);
	}
	
	/*
	 * Getter Methods
	 */
	public String getText() {
		return text;
	}

	public String getBackground() {
		return backColor;
	}
	
	public String getForeground() {
		return foreColor;
	}
	
	public String getFontName() {
		return fontName;
	}
	
	public String getFontFileName() {
		return fontFileName;
	}
	
//	public boolean isBold() {
//		return isBold;
//	}
//	
//	public boolean isItalic() {
//		return isItalic;
//	}
	
	public int getScrolltime() {
		return scrolltime;
	}
}
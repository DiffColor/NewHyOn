package kr.co.turtlelab.andowsignage.datamodels;

import android.text.TextUtils;

import java.io.File;

import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

public class WelcomeDataModel {
   
//	String text;
//	String fontname;
//	String fontcolor;
//	int fontsize;
//	boolean isBold;
//	boolean isItalic;
//	String bgcolor;
//	String remotebgfilename;
//	String remotebgfilepath;
//	boolean isFileExist;
//	int fontcolorindex;
//	int bgcolorindex;
	String localfilename;
	String localfilepath;
	
	/*
	 * Setter Methods
	 */
//	public void setText(String str) {
//		this.text = str;
//	}
//
//	public void setFontName(String font) {
//		this.fontname = font;
//	}
//
//	public void setFontColor(String color) {
//		this.fontcolor = color;
//	}
//
//	public void setFontSize(String size) {
//		this.fontsize = Integer.parseInt(size);
//	}
//
//	public void setBoldState(String state) {
//		this.isBold = Boolean.parseBoolean(state);
//	}
//
//	public void setItalicState(String state) {
//		this.isItalic = Boolean.parseBoolean(state);
//	}
//	
//	public void setBgColor(String color) {
//		this.bgcolor = color;
//	}
//	
//	public void setRemoteBgFileName(String name) {
//		this.remotebgfilename = name;
//	}
//	
//	public void setRemoteBgFilePath(String path) {
//		this.remotebgfilepath = path;
//	}
//
//	public void setFileExistState(String state) {
//		this.isFileExist = Boolean.parseBoolean(state);
//	}
//	
//	public void setFontColorIndex(String idx) {
//		this.fontcolorindex = Integer.parseInt(idx);;
//	}
//	
//	public void setBgColorIndex(String idx) {
//		this.bgcolorindex = Integer.parseInt(idx);;
//	}
	
	public void setLocalImage(String filename, String absolutePath) {
		this.localfilename = filename;
		if (!TextUtils.isEmpty(absolutePath)) {
			this.localfilepath = absolutePath;
			return;
		}
		if (!TextUtils.isEmpty(filename)) {
			File path = new File(LocalPathUtils.getContentsDirPath(), filename);
			this.localfilepath = path.getAbsolutePath();
		} else {
			this.localfilepath = null;
		}
	}
	
	/*
	 * Getter Methods
	 */
//	public String getText() {
//		return text;
//	}
//
//	public String getFontName() {
//		return fontname;
//	}
//
//	public String getFontColor() {
//		return fontcolor;
//	}
//	
//	public int getFontSize() {
//		return fontsize;
//	}
//
//	public boolean getIsBold() {
//		return isBold;
//	}
//
//	public boolean getIsItalic() {
//		return isItalic;
//	}
//	
//	public String getBgColor() {
//		return bgcolor;
//	}
//	
//	public String getRemoteBgFileName() {
//		return remotebgfilename;
//	}
//	
//	public String getRemoteBgFilePath() {
//		return remotebgfilepath;
//	}
//
//	public boolean getIsFileExist() {
//		return isFileExist;
//	}
//	
//	public int getFontColorIndex() {
//		return fontcolorindex;
//	}
//	
//	public int getBgColorIndex() {
//		return bgcolorindex;
//	}
	
	public String getLocalImgName() {
		return localfilename;
	}
	
	public String getLocalImgPath() {
		return localfilepath;
	}
}

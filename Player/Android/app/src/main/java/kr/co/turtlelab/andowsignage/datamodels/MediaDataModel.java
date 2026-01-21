package kr.co.turtlelab.andowsignage.datamodels;

import java.io.File;

import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;


public class MediaDataModel {

	String fileName;
	String filePath;
	String remoteSubPath;
	//Uri fileUri;
	String type;
	long playTime;
	boolean isValid;
	
	/*
	 * Setter Methods
	 */
	public void setFileName(String filename) {
		fileName = filename;
		filePath = LocalPathUtils.getContentPath(fileName);
		//fileUri = Utils.getAbsPathToUri(filePath);
	}

	public void setFilePath(String fpath) {
		fileName = new File(fpath).getName();
		filePath = fpath;
	}

	public void setFileNamePath(String fname, String fpath) {
		fileName = fname;
		filePath = fpath;
	}

	public void setFileNamePath(File file) {
		fileName = file.getName();
		filePath = file.getAbsolutePath();
	}
	
	public void setRemoteSubPath(String rsubpath) {
		remoteSubPath = rsubpath;
	}

	public void setType(String type) {
		this.type = type;
	}
	
	public void setPlayTime(String min, String sec) {
		playTime = 0;
		playTime = Long.parseLong(min)*60 + Long.parseLong(sec);
	}

	public void setPlayTime(long secs) {
		playTime = secs;
	}
	
	public void setValidState(String valid) {
		isValid = Boolean.parseBoolean(valid);
	}
	
	/*
	 * Getter Methods
	 */
	public String getFileName() {
		return fileName;
	}
	
	public String getFilePath() {
		if(type.equalsIgnoreCase("WebSiteURL"))
			return fileName;
		else
			return filePath;
	}
	
	public String getRemoteSubPath() {
		return remoteSubPath;
	}
	
//	public Uri getFileUri() {
//		return fileUri;
//	}

	public String getType() {
		return type;
	}
	
	public long getPlayTimeSec() {
		return playTime;
	}
	
	public boolean getValidState() {
		return isValid;
	}

}

package kr.co.turtlelab.andowsignage.datamodels;

import android.net.Uri;
import android.text.TextUtils;

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
	boolean isMuted = true;
	
	/*
	 * Setter Methods
	 */
	public void setFileName(String filename) {
		fileName = filename;
		filePath = LocalPathUtils.getContentPath(fileName);
		//fileUri = Utils.getAbsPathToUri(filePath);
	}

	public void setFilePath(String fpath) {
		String normalizedPath = fpath;
		if (!TextUtils.isEmpty(fpath)) {
			try {
				Uri parsed = Uri.parse(fpath);
				if ("file".equalsIgnoreCase(parsed.getScheme()) && !TextUtils.isEmpty(parsed.getPath())) {
					normalizedPath = parsed.getPath();
				}
			} catch (Exception ignored) {
			}
		}
		fileName = new File(normalizedPath).getName();
		filePath = normalizedPath;
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

	public void setMuted(boolean muted) {
		isMuted = muted;
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

	public boolean isMuted() {
		return isMuted;
	}

}

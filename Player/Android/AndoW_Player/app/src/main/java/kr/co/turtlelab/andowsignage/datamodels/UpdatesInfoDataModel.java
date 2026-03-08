package kr.co.turtlelab.andowsignage.datamodels;

import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

public class UpdatesInfoDataModel {

	/*
	 * "FIT_FileName": "PageBasicInfo.xml",
	 * "FIT_RelativePath": "Contents\Fonts"
	 */
    	
	String FIT_FileName;
	String FIT_RelativePath;
	
	public UpdatesInfoDataModel() {}
	
	public UpdatesInfoDataModel(String fileName, String pageName) {
		this.FIT_FileName = fileName;
		this.FIT_RelativePath = pageName;
	}
	
	public void setFileName(String filename) {
		this.FIT_FileName = filename;
	}
	
	public String getFileName() {
		return this.FIT_FileName;
	}
	
	public void setRelativePath(String path) {
		this.FIT_RelativePath = LocalPathUtils.convertPathWinToLinux(path);
	}
	
	public String getRelativePath() {
		return this.FIT_RelativePath;
	}
	
	@Override
    public String toString() {
        return new StringBuffer(" File Name : ").append(this.FIT_FileName)
        				.append(" Relative Path : ").append(this.FIT_RelativePath).toString();
    }

}

package kr.co.turtlelab.andowsignage.tools;

import android.content.Context;
import android.database.Cursor;
import android.net.Uri;
import android.provider.MediaStore;
import android.view.View;
import android.view.ViewGroup;

import java.io.File;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.UpdatesInfoDataModel;

public class LocalPathUtils {

	/*
	 * Root Directory Path
	 */
	
	public static String getContentsDirPath() {
		return getAbsolutePath("Contents");
	}

	public static String getFontsDirPath() {
		return getAbsolutePath("Fonts");
	}
	
	public static String getTempDirPath() {
		return getAbsolutePath("Temp");
	}

	
	public static String getUpgradeAPKDirPath() {
		return getAbsolutePath("UpgradeAPK");
	}

	/*
	 * File Path
	 */

	public static String getContentPath(String contentName) {
		String path = getContentsDirPath();
		return combinePath(path, contentName);
	}
	
	public static String getFontFilePath(String filename) {
		return combinePath(getFontsDirPath(), filename);
	}

	/*
	 * Path Tool
	 */

	public static String combinePath(String path1, String path2) {
		File path = new File(path1,path2);
		return path.getAbsolutePath();
	}
	
	public static String getTempPath(String relativePath) {
		return combinePath(getTempDirPath(), relativePath);
	}
	
	public static String getTempFilePath(String relativeDirPath, String filename) {
		return combinePath(getTempPath(relativeDirPath), filename);
	}

	public static String getAbsolutePath(String relativePath) {
		return combinePath(AndoWSignageApp.getDirPath(), relativePath);
	}
	
	public static String getAbsoluteFilePath(String relativeDirPath, String filename) {
		return combinePath(getAbsolutePath(relativeDirPath), filename);
	}

	public static void checkTargetFolders(Context ctx, String targetPath) {   
		try {
			File file = new File(targetPath);
			
		    if (file.exists()) {
		    	return;
		    }
		    
		    file.mkdirs();
		    
			AndoWSignage.act.mScanner.notify(file.getAbsolutePath(), false);
			
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
	
	public static void checkTargetFoldersFromFilePath(Context ctx, String filePath) {   
		try {
			
			//String folerpath = filePath.substring(0, filePath.lastIndexOf("/"));
			File file = new File(filePath);
			
			String folerpath = file.getParent();
			
			File folder = new File(folerpath);
			
		    if (folder.exists()) {
		    	return;
		    }
		    
		    folder.mkdirs();

			AndoWSignage.act.mScanner.notify(folder.getAbsolutePath(), false);
			
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
	
	public static String convertPathWinToLinux(String path) {
		return path.replace("\\\\", "/").replace("\\", "/");
	}
	
	public void moveToBack(View currentView) 
	{
	    ViewGroup vg = ((ViewGroup) currentView.getParent());
	    int index = vg.indexOfChild(currentView);
	    for(int i = 0; i<index; i++)
	    {
	    	vg.bringChildToFront(vg.getChildAt(i));
	    }
	    vg.invalidate();
	    vg.requestLayout();
	}
	
	public static Uri getUriFromUriString(String uriString){
		return Uri.parse(uriString);
	}
	
	public static Uri getUriFromAbsPath(String absPath) {
		return Uri.fromFile(new File(absPath));
	}
	
	public static String getUriStringFromAbsPath(String path) {
		File file = new File(path);
		String uri = "";
		if (file.exists()) {
		    uri = Uri.fromFile(file).toString();
		}
		return Uri.decode(uri);
	}
	
	public static String getMediaUriStringFromUri(Uri uri) {
		String result;
	    Cursor cursor = AndoWSignage.getCtx().getContentResolver().query(uri, null, null, null, null);
	    if (cursor == null) { 
	        result = uri.getPath();
	    } else { 
	        cursor.moveToFirst(); 
	        int idx = cursor.getColumnIndex(MediaStore.Images.ImageColumns.DATA); 
	        result = cursor.getString(idx);
	        cursor.close();
	    }
	    return result;
	}
	
	public static boolean checkFileExist(String path) {
		return new File(path).exists();
	}

	public static String getAuthFilePath() {
		return getAbsolutePath(".cache");
	}

	public static String getUSBContentsDirPath() {
		return getAbsolutePath("USBP");
	}

	public static String getUSBContentFilePath(String fname) {
		return combinePath(getUSBContentsDirPath(), fname);
	}
}


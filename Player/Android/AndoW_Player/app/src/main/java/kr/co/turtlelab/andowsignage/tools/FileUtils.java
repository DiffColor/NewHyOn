package kr.co.turtlelab.andowsignage.tools;

import android.annotation.SuppressLint;
import android.webkit.MimeTypeMap;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.FileReader;
import java.lang.reflect.Method;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

import kr.co.turtlelab.andowsignage.AndoWSignage;

public class FileUtils {

	public static String ReadTextFile(String path) {
		
		String retStr = "";
		File file = new File(path);

		if(!file.exists()) return retStr;

		try {
			StringBuilder text = new StringBuilder();
			
		    BufferedReader br = new BufferedReader(new FileReader(file));
		    String line;

		    while ((line = br.readLine()) != null) {
		        text.append(line);
		    }
		    br.close();
		    
		    retStr = text.toString();
		}
		catch (Exception e) {
		}
		
		return retStr;
	}

	public static void copyfile(String sfpath, String tfpath, boolean overwrite) {

		File sf = new File(sfpath);
		File tf = new File(tfpath);
		
		if(tf.exists() && !overwrite)
			if(tf.length()>100000)
				if(tf.length() == sf.length())
					return;
		
		FileInputStream fis = null;
		FileOutputStream fos = null;
		
		try {
			
			fis = new FileInputStream(sf);
			fos = new FileOutputStream(tf);
			
			byte[] b = new byte[4096];
			int cnt = 0;
			while((cnt=fis.read(b)) != -1){
				fos.write(b, 0, cnt);
			}

			AndoWSignage.act.mScanner.notify(tf.getAbsolutePath(), false);
		} catch (Exception e) {
			e.printStackTrace();
		} finally{
			try {
				if(fis != null)
					fis.close();

				if(fos != null)
					fos.close();

			} catch (Exception e) {
				e.printStackTrace();
			}
		}
	}
	
	public static void copyfolder(String sfDirpath, String tfDirpath, boolean overwrite){
		
		File sf = new File(sfDirpath);
		File tf = new File(tfDirpath);
		
		if(sf.exists() == false)
			return;
		
		File[] ff = sf.listFiles();
		
		for (File file : ff) {
			String tpath = tf.getAbsolutePath() + File.separator + file.getName();
			
			if(file.isDirectory()){
				File temp = new File(tpath);
				if(!temp.exists())
					LocalPathUtils.checkTargetFolders(AndoWSignage.getCtx(), tpath);
				copyfolder(file, temp, overwrite);
			} else {
				copyfile(file, new File(tpath), overwrite);
			}
		}
	}

	public static void movefile(String sfpath, String tfpath) {

		boolean succeed = false;
		
		File sf = new File(sfpath);
		File tf = new File(tfpath);
		
		if(sf.exists() == false)
			return;
		
		FileInputStream fis = null;
		FileOutputStream fos = null;
		
		try {
			
			fis = new FileInputStream(sf);
			fos = new FileOutputStream(tf);
			
			byte[] b = new byte[4096];
			int cnt = 0;
			
			while((cnt=fis.read(b)) != -1){
				fos.write(b, 0, cnt);
			}

			succeed = true;
		} catch (Exception e) {
			e.printStackTrace();
		} finally{
			try {
				if(fis != null)
					fis.close();

				if(fos != null)
					fos.close();
				
				if(succeed) {
					sf.delete();
					AndoWSignage.act.mScanner.notify(sf.getAbsolutePath(), true);
				}
			} catch (Exception e) {
				e.printStackTrace();
			}
		}
	}
	
	public static void movefolder(String sfDirpath, String tfDirpath){
		
		try {
			File sf = new File(sfDirpath);
			File tf = new File(tfDirpath);
			
			File[] ff = sf.listFiles();
			
			for (File file : ff) {
				String tpath = tf.getAbsolutePath() + File.separator + file.getName();
				
				if(file.isDirectory()){
					File temp = new File(tpath);
					if(!temp.exists())
						LocalPathUtils.checkTargetFolders(AndoWSignage.getCtx(), tpath);
					movefolder(file, temp);
				} else {
					movefile(file, new File(tpath));
				}
			}
			
			sf.delete();
			AndoWSignage.act.mScanner.notify(sf.getAbsolutePath(), true);
		}catch(Exception e) {
			e.printStackTrace();
		}
	}
	
	public static String ReadTextFile(File sf) {
		
		String retStr = "";

		if(sf.exists() == false) return retStr;

		try {
			
			StringBuilder text = new StringBuilder();
			
		    BufferedReader br = new BufferedReader(new FileReader(sf));
		    String line;

		    while ((line = br.readLine()) != null) {
		        text.append(line);
		    }
		    
		    br.close();
		    
		    retStr = text.toString();
		}
		catch (Exception e) {
			e.printStackTrace();
		}
		
		return retStr;
	}
	
	public static void CreateNewFile(String fpath, String data) {
		File target = new File(fpath);
		try {
			if(target.exists())
				return;
			
			LocalPathUtils.checkTargetFoldersFromFilePath(AndoWSignage.getCtx(), fpath);
			//target.createNewFile();
			FileOutputStream fos = new FileOutputStream(fpath,false);
			fos.write((data + System.getProperty("line.separator")).getBytes());
			fos.flush();
			fos.close();
			AndoWSignage.act.mScanner.notify(target.getAbsolutePath(), false);		
		} catch (Exception e) {
			e.printStackTrace();
		}       
	}

	public static void copyfile(File sf, File tf, boolean overwrite) {
		
		if(tf.exists() && !overwrite)
			if(tf.length()>100000)
				if(tf.length() == sf.length())
					return;
		
		FileInputStream fis = null;
		FileOutputStream fos = null;
		
		try {
			
			fis = new FileInputStream(sf);
			fos = new FileOutputStream(tf) ;
			
			byte[] b = new byte[4096];
			int cnt = 0;
			
			while((cnt=fis.read(b)) != -1){
				fos.write(b, 0, cnt);
			}

			AndoWSignage.act.mScanner.notify(tf.getAbsolutePath(), false);
		} catch (Exception e) {
			e.printStackTrace();
		} finally{
			try {
				if(fis != null)
					fis.close();

				if(fos != null)
					fos.close();

			} catch (Exception e) {
				e.printStackTrace();
			}
		}
	}
	
	public static void copyfolder(File sfDir, File tfDir, boolean overwrite){
			
		File[] ff = sfDir.listFiles();
		
		for (File file : ff) {
			String tpath = tfDir.getAbsolutePath() + File.separator + file.getName();
			
			if(file.isDirectory()){
				File temp = new File(tpath);
				if(!temp.exists())
					LocalPathUtils.checkTargetFolders(AndoWSignage.getCtx(), tpath);
				copyfolder(file, temp, overwrite);
			} else {
				copyfile(file, new File(tpath), overwrite);
			}
		}
	}

	public static void movefile(File sf, File tf) {
		
		boolean succeed = false;
		
		FileInputStream fis = null;
		FileOutputStream fos = null;
		
		try {
			
			fis = new FileInputStream(sf);
			fos = new FileOutputStream(tf) ;
			
			byte[] b = new byte[4096];
			int cnt = 0;
			
			while((cnt=fis.read(b)) != -1){
				fos.write(b, 0, cnt);
			}

			succeed = true;
			
		} catch (Exception e) {
			e.printStackTrace();
		} finally{
			
			try {
				if(fis != null)
					fis.close();

				if(fos != null)
					fos.close();
				
				if(succeed) {
					sf.delete();
					AndoWSignage.act.mScanner.notify(sf.getAbsolutePath(), true);
				}
			} catch (Exception e) {
				e.printStackTrace();
			}
		}
	}
	
	public static void movefolder(File sfDir, File tfDir){
			
		try {
			File[] ff = sfDir.listFiles();
			
			for (File file : ff) {
				String tpath = tfDir.getAbsolutePath() + File.separator + file.getName();
				
				if(file.isDirectory()){
					File temp = new File(tpath);
					if(!temp.exists())
						LocalPathUtils.checkTargetFolders(AndoWSignage.getCtx(), tpath);
					movefolder(file, temp);
				} else {
					movefile(file, new File(tpath));
				}
			}
			
			sfDir.delete();
			AndoWSignage.act.mScanner.notify(sfDir.getAbsolutePath(), true);
		}catch(Exception e) {
			e.printStackTrace();
		}
	}
	
	public static void deletefolder(String dirPath) 
	{ 
	    File file = new File(dirPath);

	    if(file.exists() == false)
	    	return;

	    File[] childFileList = file.listFiles();
	    
	    for(File childFile : childFileList)
	    {
	        if(childFile.isDirectory()) {
	        	deletefolder(childFile.getAbsolutePath());     
	        }
	        else {
	            childFile.delete();    
	    		AndoWSignage.act.mScanner.notify(childFile.getAbsolutePath(), true);
	        }
	    }      
	    
	    file.delete();
		AndoWSignage.act.mScanner.notify(file.getAbsolutePath(), true);
	}
	
	public static void deleteFiles(String dirPath, boolean recursive) {
		File file = new File(dirPath);
		
		if(file.exists() == false)
			return;
		
	    File[] childFileList = file.listFiles();
	    
	    for(File childFile : childFileList)
	    {
	        if(childFile.isDirectory() && recursive) {
	        	deletefolder(childFile.getAbsolutePath());     
	        }
	        else {
	            childFile.delete();    
	    		AndoWSignage.act.mScanner.notify(childFile.getAbsolutePath(), true);
	        }
	    }
	}

	public static String getExtension(String filename) {
		if (filename == null) {
			return null;
		}
		int extensionPos = filename.lastIndexOf('.');
		int lastUnixPos = filename.lastIndexOf('/');
		int lastWindowsPos = filename.lastIndexOf('\\');
		int lastSeparator = Math.max(lastUnixPos, lastWindowsPos);
		int index = lastSeparator > extensionPos ? -1 : extensionPos;
		if (index == -1) {
			return "";
		} else {
			return filename.substring(index + 1);
		}
	}
		
	@SuppressLint("SuspiciousIndentation")
    public static boolean changePermissions(String fpath) {

		try {

			File file = new File(fpath);
			
			if(file.exists() == false)
				return false;
			
		    file.setReadable(true, false);
		    file.setExecutable(true, false);
		    file.setWritable(true, false);
		    
			AndoWSignage.act.mScanner.notify(file.getAbsolutePath(), false);
			
		} catch (Exception e) {
			return false;
		}
		
		return true;
	}
	
	public static void changePermissions(String fpath, int mode) {
		try {
			File f = new File(fpath);
			changePermissons(f, mode);
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
	
	public static int changePermissons(File path, int mode) throws Exception {
		  Class<?> fileUtils = Class.forName("android.os.FileUtils");
		  Method setPermissions = fileUtils.getMethod("setPermissions", String.class, int.class, int.class, int.class);
		  return (Integer) setPermissions.invoke(null, path.getAbsolutePath(), mode, -1, -1);
	}
	
	public static void unzip(String _zipFile, String _targetLocation) {

		dirChecker(_targetLocation);

		try {
			FileInputStream fin = new FileInputStream(_zipFile);
			ZipInputStream zin = new ZipInputStream(fin);
			ZipEntry ze = null;
			while ((ze = zin.getNextEntry()) != null) {
				if (ze.isDirectory()) {
					dirChecker(_targetLocation+ ze.getName());
				} else {
					FileOutputStream fout = new FileOutputStream(_targetLocation + ze.getName());
					for (int c = zin.read(); c != -1; c = zin.read()) {
						fout.write(c);
					}
					zin.closeEntry();
					fout.close();
				}
			}
			zin.close();
		} catch (Exception e) {
			System.out.println(e);
		}
	}
	
	public static void dirChecker(String location) {
		File f = new File(location);
        if (!f.isDirectory()) {
            f.mkdirs();
        }
	}
	
	public static void removeZeroFiles(String dir, boolean recursive) {
		File file = new File(dir);

		if(file.exists() == false)
			return;
		
		if(file.isDirectory() == false)
			return;
		
	    File[] childFileList = file.listFiles();
	    
	    for(File childFile : childFileList)
	    {
	        if(childFile.isDirectory() && recursive) {
	        	removeZeroFiles(childFile.getAbsolutePath(), true);     
	        }
	        else {
	        	if(childFile.length() < 1) {
	        		childFile.delete();    
	        		AndoWSignage.act.mScanner.notify(childFile.getAbsolutePath(), true);
	        	}
	        }
	    }
	}
	
	public static boolean hasZeroFiles(String dir) {

		boolean ret = false;
		
		File file = new File(dir);

		if(file.exists() == false) {
			ret = true;
			return ret;
		}
		
		if(file.isDirectory() == false) {
			ret = true;
			return ret;
		}
		
	    File[] childFileList = file.listFiles();
	    
	    for(File childFile : childFileList)
	    {
	        if(childFile.isDirectory())
	        	continue;
	        
        	if(childFile.length() < 1) {
        		ret = true;
        		break;
        	}
	    }
	    
	    return ret;
	}

	public static void deleteFile(String path) {
		File file = new File(path);
		if(file.exists() == false)
			return;
		file.delete();
		AndoWSignage.act.mScanner.notify(path, true);
	}

	public static String getMimeTypeString(String fpath) {
		String _ext;
		int _idx = fpath.lastIndexOf('.');

		if(_idx > 0)
			_ext = fpath.substring(_idx+1);
		else
			return null;

		MimeTypeMap type = MimeTypeMap.getSingleton();
		return type.getMimeTypeFromExtension(_ext);
	}

	public static boolean isMediaFile(String fpath) {
		String _type = getMimeTypeString(fpath);

		if(_type == null)
			return false;

		return (_type.startsWith("image") || _type.startsWith("video"));
	}
}

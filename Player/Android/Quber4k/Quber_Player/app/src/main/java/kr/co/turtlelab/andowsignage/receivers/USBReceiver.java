package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.AsyncTask;
import android.os.Build;
import android.os.Environment;
import android.util.Log;

import java.io.File;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.FileUtils;
import kr.co.turtlelab.andowsignage.tools.ImageUtils;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class USBReceiver extends BroadcastReceiver {

    private static final String TAG = "USBReceiver";
    private static final String USB_DIRNAME = "NewHyOn";
    private static final String USB_SKIP_DIRNAME = "APKs";
    private static final String LIST_FILENAME = "listname";

    CopyWorker mCopyWorker = null;
    USBCopyWorker mUSBCopyWorker = null;

    File usbSourceDir = null;
    boolean hasKey = false;

    @Override
    public void onReceive(Context context, Intent intent) {

        if(AndoWSignage.act == null)
            return;

        List<File> searchRoots = buildSearchRoots(context, intent);
        if (searchRoots.isEmpty()) {
            return;
        }

        File skipDir = findNamedDirectory(searchRoots, USB_SKIP_DIRNAME);
        if(skipDir != null)
            return;

        File installDir = findNamedDirectory(searchRoots, USB_DIRNAME);
        if(installDir != null)
        {
            usbSourceDir = installDir;
            File authFile = new File(installDir, "AuthKeys");
            try {
                hasKey = AuthUtils.HasAuthKey(authFile.getAbsolutePath(), NetworkUtils.getMACAddress());
            } catch (Exception e1) {
                hasKey = authFile.exists();
            }
            if(hasKey) {
                mCopyWorker = new CopyWorker();
                CopyAll(installDir);
            }
            return;
        }

        File mediaDir = findMediaDirectory(searchRoots);
        if(mediaDir != null)
        {
            usbSourceDir = mediaDir;
            try {
                hasKey = AuthUtils.HasAuthKey(LocalPathUtils.getAuthFilePath(), NetworkUtils.getMACAddress());
            } catch (Exception e1){
                hasKey = new File(LocalPathUtils.getAuthFilePath()).exists();
            }
            if (!hasKey) {
                hasKey = LocalSettingsProvider.hasStoredUsbKeyForDevice();
            }

            if(hasKey) {
                mUSBCopyWorker = new USBCopyWorker();
                mUSBCopyWorker.executeOnExecutor(AsyncTask.SERIAL_EXECUTOR);
            }
        }

        hasKey = false;
        usbSourceDir = null;
    }

    private List<File> buildSearchRoots(Context context, Intent intent) {
        Set<String> uniquePaths = new LinkedHashSet<>();

        File mountedDir = resolveMountedDir(intent);
        File mountedRoot = resolveStorageRoot(mountedDir);
        addSearchPath(uniquePaths, mountedRoot);
        addSearchPath(uniquePaths, mountedDir);

        List<File> externalRoots = collectExternalStorageRoots(context);
        for (File root : externalRoots) {
            if (isRelevantStorageRoot(root, mountedDir, mountedRoot)) {
                addSearchPath(uniquePaths, root);
            }
        }

        if (uniquePaths.isEmpty()) {
            for (File root : externalRoots) {
                addSearchPath(uniquePaths, root);
            }
        }

        List<File> roots = new ArrayList<>();
        for (String path : uniquePaths) {
            roots.add(new File(path));
        }
        return roots;
    }

    private File resolveMountedDir(Intent intent) {
        if (intent == null) {
            return null;
        }

        Uri data = intent.getData();
        if (data == null) {
            return null;
        }

        String path = data.getPath();
        if (path == null || path.length() < 1) {
            return null;
        }

        File mountedDir = new File(path);
        Log.d(TAG, "Mounted path from intent: " + mountedDir.getAbsolutePath());
        return mountedDir;
    }

    private List<File> collectExternalStorageRoots(Context context) {
        Set<String> uniquePaths = new LinkedHashSet<>();
        if (context == null) {
            return new ArrayList<>();
        }

        File[] externalFilesDirs;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT) {
            externalFilesDirs = context.getExternalFilesDirs(null);
        } else {
            externalFilesDirs = new File[]{context.getExternalFilesDir(null)};
        }

        if (externalFilesDirs == null) {
            return new ArrayList<>();
        }

        for (File appSpecificDir : externalFilesDirs) {
            if (!shouldUseExternalDir(appSpecificDir)) {
                continue;
            }

            File storageRoot = resolveStorageRoot(appSpecificDir);
            addSearchPath(uniquePaths, storageRoot);
        }

        List<File> roots = new ArrayList<>();
        for (String path : uniquePaths) {
            roots.add(new File(path));
        }
        return roots;
    }

    private boolean shouldUseExternalDir(File appSpecificDir) {
        if (appSpecificDir == null) {
            return false;
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            try {
                if (!Environment.MEDIA_MOUNTED.equals(Environment.getExternalStorageState(appSpecificDir))) {
                    return false;
                }
            } catch (Throwable ignore) {
            }
        }

        return resolveStorageRoot(appSpecificDir) != null;
    }

    private File resolveStorageRoot(File dir) {
        File current = dir;
        while (current != null) {
            if ("Android".equals(current.getName())) {
                return current.getParentFile();
            }
            current = current.getParentFile();
        }
        return dir;
    }

    private boolean isRelevantStorageRoot(File root, File mountedDir, File mountedRoot) {
        if (root == null) {
            return false;
        }

        if (mountedDir == null && mountedRoot == null) {
            return true;
        }

        return isSameOrParent(root, mountedDir)
                || isSameOrParent(mountedDir, root)
                || isSameOrParent(root, mountedRoot)
                || isSameOrParent(mountedRoot, root)
                || hasSameVolumeName(root, mountedDir)
                || hasSameVolumeName(root, mountedRoot);
    }

    private boolean isSameOrParent(File parentCandidate, File childCandidate) {
        if (parentCandidate == null || childCandidate == null) {
            return false;
        }

        String parentPath = normalizePath(parentCandidate);
        String childPath = normalizePath(childCandidate);
        if (parentPath == null || childPath == null) {
            return false;
        }

        return childPath.equals(parentPath) || childPath.startsWith(parentPath + File.separator);
    }

    private boolean hasSameVolumeName(File left, File right) {
        if (left == null || right == null) {
            return false;
        }

        String leftName = left.getName();
        String rightName = right.getName();
        return leftName != null && leftName.length() > 0 && leftName.equalsIgnoreCase(rightName);
    }

    private String normalizePath(File file) {
        if (file == null) {
            return null;
        }

        try {
            return file.getCanonicalPath();
        } catch (Exception ignore) {
            return file.getAbsolutePath();
        }
    }

    private void addSearchPath(Set<String> paths, File dir) {
        if (dir == null) {
            return;
        }

        String path = normalizePath(dir);
        if (path == null || path.length() < 1) {
            return;
        }

        paths.add(path);
    }

    private File findNamedDirectory(List<File> searchRoots, String dirname) {
        if (searchRoots == null) {
            return null;
        }

        for (File root : searchRoots) {
            File found = findNamedDirectory(root, dirname);
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    private File findNamedDirectory(File base, String dirname) {
        if (base == null || !base.isDirectory()) {
            return null;
        }

        if (dirname.equalsIgnoreCase(base.getName())) {
            return base;
        }

        File directChild = new File(base, dirname);
        if (directChild.isDirectory()) {
            return directChild;
        }

        File[] list = base.listFiles();
        if (list == null) {
            return null;
        }

        for (File file : list) {
            if (file.isDirectory()) {
                File found = findNamedDirectory(file, dirname);
                if (found != null) {
                    return found;
                }
            }
        }
        return null;
    }

    private File findMediaDirectory(List<File> searchRoots) {
        if (searchRoots == null) {
            return null;
        }

        for (File root : searchRoots) {
            File found = findMediaDirectory(root);
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    private File findMediaDirectory(File base) {
        if (base == null || !base.isDirectory()) {
            return null;
        }

        File[] list = base.listFiles();
        if (list == null) {
            return null;
        }

        for (File file : list) {
            if (file.isFile() && FileUtils.isMediaFile(file.getAbsolutePath())) {
                return base;
            }
        }

        for (File file : list) {
            if (file.isDirectory()) {
                File found = findMediaDirectory(file);
                if (found != null) {
                    return found;
                }
            }
        }
        return null;
    }

    private void removeGarbages() {
        File _dir = new File(LocalPathUtils.getUSBContentsDirPath());
        if(_dir.listFiles() != null) {
            for (File file : _dir.listFiles()) {
                if (AndoWSignage.act.usbflist.contains(file.getName()) == false)
                    FileUtils.deleteFile(file.getAbsolutePath());
            }
        }
    }

    private void getUSBMedia(File base) {
        if(base == null || base.listFiles() == null) {
            return;
        }

        for (File file : base.listFiles()) {
            if (file.isDirectory())
                continue;

            if(FileUtils.isMediaFile(file.getAbsolutePath())) {
                AndoWSignage.act.usbflist.add(file.getName());
                FileUtils.copyfile(file.getAbsolutePath(), LocalPathUtils.getUSBContentFilePath(file.getName()), false);
            }
        }
    }

    public void CopyAll(File path) {
        SystemUtils.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                AndoWSignage.act.stopForUpdate();
            }
        });

        mCopyWorker.executeOnExecutor(AsyncTask.SERIAL_EXECUTOR, new File[]{path});
    }
	
	public String getFilePermissions(File file) {
		String per = "-";

		if(file.isDirectory())
			per += "d";
		if(file.canRead())
			per += "r";
		if(file.canWrite())
			per += "w";

		return per;
	}
	
	
	class CopyWorker extends AsyncTask<File, Void, File> {
	    	
    	public CopyWorker() {
		}

			@Override
			protected File doInBackground(File... path) {
			    if (path == null || path.length < 1 || path[0] == null) {
			        return null;
                }
				FileUtils.copyfolder(path[0], new File(AndoWSignageApp.getDirPath()), true);
				return new File(path[0], LIST_FILENAME);
			}

			@Override
			protected void onPostExecute(File result) {

				final File listFile = result;

				SystemUtils.runOnUiThread(new Runnable() {
					@Override
					public void run() {

	                    String listname = listFile == null ? "" : FileUtils.ReadTextFile(listFile.getAbsolutePath());
	                    PlayerDataProvider.updateCurrentPListName(listname);

						ImageUtils.cleanDiskcache();

						File contentDir = usbSourceDir == null ? null : new File(usbSourceDir, "Contents");
						File[] contentlist = contentDir == null ? null : contentDir.listFiles();
	                    List<String> clist = new ArrayList<String>();
	                    if (contentlist != null) {
	                        for (File file:contentlist) {
	                            clist.add(file.getName());
	                        }
	                    }

	                    //LocalPathUtils.RemoveGarbageContents(clist);

					AndoWSignage.act.updateAndRestart(false);
				}
			});
		}
    }

    class USBCopyWorker extends AsyncTask<Void, Void, Void> {

        @Override
        protected void onPreExecute() {
            super.onPreExecute();

            SystemUtils.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    AndoWSignage.act.stopForUpdate();
                }
            });

        }

        @Override
	        protected Void doInBackground(Void... voids) {
	            try {
	                LocalPathUtils.checkTargetFolders(AndoWSignage.getCtx(), LocalPathUtils.getUSBContentsDirPath());
	                AndoWSignage.act.usbflist.clear();
	                getUSBMedia(usbSourceDir);
	                removeGarbages();
	                PlayerDataProvider.updateCurrentPListName("USBP");
	            } catch (Exception exc) {}
            return null;
        }

        @Override
        protected void onPostExecute(Void aVoid) {
            super.onPostExecute(aVoid);

            SystemUtils.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if(AndoWSignageApp.isRunning)
                        AndoWSignage.act.updateAndRestart(false);
                }
            });
        }
    }
}

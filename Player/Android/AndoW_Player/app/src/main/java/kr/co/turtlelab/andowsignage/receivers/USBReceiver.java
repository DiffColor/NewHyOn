package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.AsyncTask;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

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

    CopyWorker mCopyWorker = null;
    USBCopyWorker mUSBCopyWorker = null;

    String BaseDir = "/mnt/usb_storage";
    String USBDirname = "AndoW USB";
    String USBSkipDirname = "TurtleAPKs";
    String listfilename = "listname";
    String USBDirPath = "";
    boolean hasKey = false;

    @Override
    public void onReceive(Context context, Intent intent) {

        if(AndoWSignage.act == null)
            return;

        if(hasDirname(BaseDir, USBSkipDirname))
            return;

        if(hasDirname(BaseDir, USBDirname))
        {
            try {
                hasKey = AuthUtils.HasAuthKey(USBDirPath + "/" + "AuthKeys", NetworkUtils.getMACAddress());
            } catch (Exception e1) {
                hasKey = new File(USBDirPath + "/" + "AuthKeys").exists();
            }
            if(hasKey) {
                mCopyWorker = new CopyWorker();
                CopyAll(USBDirPath);
            }
            return;
        }

        if(hasMediaContents(BaseDir))
        {
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
    }

    public boolean hasMediaContents(String baseDir) {
        boolean _result = false;
        File base = new File(baseDir);
        if(base.isDirectory()) {
            for (File file : base.listFiles()) {
                if (file.isDirectory()) {
                    _result = hasMediaContents(file.getAbsolutePath());
                    if(_result)
                        break;
                }
                else {
                    if(FileUtils.isMediaFile(file.getAbsolutePath()))
                    {
                        _result = true;
                        USBDirPath = file.getParent();
                        break;
                    }
                }
            }
        }

        return _result;
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

    private void getUSBMedia(String baseDir) {
        File base = new File(baseDir);
        if(base.listFiles() != null) {
            for (File file : base.listFiles()) {
                if (file.isDirectory())
                    continue;

                if(FileUtils.isMediaFile(file.getAbsolutePath())) {
                    AndoWSignage.act.usbflist.add(file.getName());
                    FileUtils.copyfile(file.getAbsolutePath(), LocalPathUtils.getUSBContentFilePath(file.getName()), false);
                }
            }
        }
    }

    public void CopyAll(String path) {
        SystemUtils.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                AndoWSignage.act.stopForUpdate();
            }
        });

        mCopyWorker.executeOnExecutor(AsyncTask.SERIAL_EXECUTOR, new String[]{path});
    }

    public boolean hasDirname(String baseDir, String dirname) {
        boolean _result = false;
        File base = new File(baseDir);
        if(base.isDirectory()) {
            if (base.getName().equalsIgnoreCase(dirname)) {
                USBDirPath = base.getAbsolutePath();
                _result = true;
            } else if(base.listFiles() != null) {
                for (File file : base.listFiles()) {
                    if (file.isDirectory()) {
                        _result = hasDirname(file.getAbsolutePath(), dirname);
                        if(_result)
                            break;
                    }
                }
            }
        }

        return _result;
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
	
	
	class CopyWorker extends AsyncTask<String, Void, String> {
	    	
    	public CopyWorker() {
		}

		@Override
		protected String doInBackground(String... path) {
			FileUtils.copyfolder(new File(path[0]), new File(AndoWSignageApp.getDirPath()), true);
			return path[0]+"/"+listfilename;
		}

		@Override
		protected void onPostExecute(String result) {

			final String path = result;

			SystemUtils.runOnUiThread(new Runnable() {
				@Override
				public void run() {

                    String listname = FileUtils.ReadTextFile(path);
                    PlayerDataProvider.updateCurrentPListName(listname);

					ImageUtils.cleanDiskcache();

					String contentDir = USBDirPath + "/" + "Contents";
					File[] contentlist = new File(contentDir).listFiles();
                    List<String> clist = new ArrayList<String>();
                    for (File file:contentlist) {
                        clist.add(file.getName());
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
                getUSBMedia(USBDirPath);
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

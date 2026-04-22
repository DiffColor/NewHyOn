package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.AsyncTask;
import android.os.Build;
import android.os.Environment;
import android.text.TextUtils;
import android.util.Log;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.update.FileIntegrityUtils;
import kr.co.turtlelab.andowsignage.data.update.UpdatePayloadModels;
import kr.co.turtlelab.andowsignage.datamodels.PlayerDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.FileUtils;
import kr.co.turtlelab.andowsignage.tools.ImageUtils;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.SecureJsonTools;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class USBReceiver extends BroadcastReceiver {

    private static final String TAG = "USBReceiver";
    private static final String USB_DIRNAME = "NewHyOn_USB";
    private static final String USB_SKIP_DIRNAME = "APKs";
    private static final String LIST_FILENAME = "listname";
    private static final String PLAYLIST_FILENAME = "playlist.bin";
    private static final String WEEKLY_SCHEDULE_FILENAME = "weekly_schedule.bin";
    private static final String SPECIAL_SCHEDULE_FILENAME = "special_schedule.bin";
    private static final int USB_COPY_BUFFER_SIZE = 4 * 1024 * 1024;

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

        File installDir = findDirectNamedDirectory(searchRoots, USB_DIRNAME);
        if (installDir == null) {
            File skipDir = findDirectNamedDirectory(searchRoots, USB_SKIP_DIRNAME);
            if(skipDir != null)
                return;

            installDir = findNamedDirectory(searchRoots, USB_DIRNAME);
        }
        if(installDir != null)
        {
            usbSourceDir = installDir;
            File authFile = new File(installDir, "AuthKeys");
            try {
                hasKey = AuthUtils.HasEncodedAuthKey(authFile.getAbsolutePath(), LocalSettingsProvider.getUsbAuthKey());
            } catch (Exception e1) {
                hasKey = false;
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

    private File findDirectNamedDirectory(List<File> searchRoots, String dirname) {
        if (searchRoots == null) {
            return null;
        }

        for (File root : searchRoots) {
            File found = findDirectNamedDirectory(root, dirname);
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    private File findDirectNamedDirectory(File base, String dirname) {
        if (base == null || !base.isDirectory()) {
            return null;
        }

        if (dirname.equalsIgnoreCase(base.getName())) {
            return base;
        }

        File directChild = new File(base, dirname);
        return directChild.isDirectory() ? directChild : null;
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
	
	
	class CopyWorker extends AsyncTask<File, Void, UsbApplyResult> {
	    	
    	public CopyWorker() {
		}

			@Override
			protected UsbApplyResult doInBackground(File... path) {
			    if (path == null || path.length < 1 || path[0] == null) {
			        return UsbApplyResult.fail("USB package path is empty");
                }
                File packageDir = path[0];
                if (new File(packageDir, PLAYLIST_FILENAME).exists()) {
                    return applyManagerUsbPackage(packageDir);
                }

                String listname = FileUtils.ReadTextFile(new File(packageDir, LIST_FILENAME).getAbsolutePath());
                if (TextUtils.isEmpty(listname)) {
                    return UsbApplyResult.fail("Legacy USB package missing listname");
                }

				FileUtils.copyfolder(packageDir, new File(AndoWSignageApp.getDirPath()), true);
				return UsbApplyResult.success(listname);
			}

			@Override
			protected void onPostExecute(UsbApplyResult result) {

				SystemUtils.runOnUiThread(new Runnable() {
					@Override
					public void run() {

                        if (result != null && result.success && !TextUtils.isEmpty(result.playlistName)) {
                            PlayerDataProvider.updateCurrentPListName(result.playlistName);
                        } else {
                            Log.e(TAG, "USB update failed: " + (result == null ? "unknown" : result.message));
                        }

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

    private UsbApplyResult applyManagerUsbPackage(File packageDir) {
        try {
            PlaylistExportBundle bundle = SecureJsonTools.readEncryptedJson(new File(packageDir, PLAYLIST_FILENAME), PlaylistExportBundle.class);
            if (bundle == null || bundle.PageList == null || bundle.Pages == null || bundle.Pages.isEmpty()) {
                return UsbApplyResult.fail("playlist.bin is empty");
            }

            if (TextUtils.isEmpty(bundle.PageList.PLI_PageListName)) {
                bundle.PageList.PLI_PageListName = bundle.PlaylistName == null ? "" : bundle.PlaylistName;
            }

            if (TextUtils.isEmpty(bundle.PageList.PLI_PageListName)) {
                return UsbApplyResult.fail("playlist name is empty");
            }

            copyManagerUsbContents(packageDir, bundle.Pages);

            WeeklyScheduleExportBundle weekly = null;
            SpecialScheduleExportBundle special = null;
            File weeklyFile = new File(packageDir, WEEKLY_SCHEDULE_FILENAME);
            if (weeklyFile.exists()) {
                weekly = SecureJsonTools.readEncryptedJson(weeklyFile, WeeklyScheduleExportBundle.class);
            }
            File specialFile = new File(packageDir, SPECIAL_SCHEDULE_FILENAME);
            if (specialFile.exists()) {
                special = SecureJsonTools.readEncryptedJson(specialFile, SpecialScheduleExportBundle.class);
            }

            UpdatePayloadModels.UpdatePayload payload = new UpdatePayloadModels.UpdatePayload();
            payload.PageList = bundle.PageList;
            payload.Pages = bundle.Pages;
            payload.Schedule = buildSchedulePayload(bundle, weekly, special);

            boolean applied = new DataSyncManager().applyUsbUpdatePayload(payload);
            return applied
                    ? UsbApplyResult.success(bundle.PageList.PLI_PageListName)
                    : UsbApplyResult.fail("Realm apply failed");
        } catch (Exception ex) {
            Log.e(TAG, "Manager USB package apply failed", ex);
            return UsbApplyResult.fail(ex.getMessage());
        }
    }

	    private void copyManagerUsbContents(File packageDir, List<UpdatePayloadModels.PageInfoClass> pages) throws IOException {
	        Map<String, ContentCopySpec> specs = collectContentCopySpecs(pages);
	        if (specs.isEmpty()) {
	            return;
        }

        File contentsDir = new File(packageDir, "Contents");
        if (!contentsDir.exists() || !contentsDir.isDirectory()) {
            throw new IOException("USB Contents directory missing");
        }

	        LocalPathUtils.checkTargetFolders(AndoWSignage.getCtx(), LocalPathUtils.getContentsDirPath());
	        File targetDir = new File(LocalPathUtils.getContentsDirPath());

	        for (ContentCopySpec spec : specs.values()) {
	            File sourceFile = resolveUsbSourceContentFile(contentsDir, spec);
	            if (sourceFile == null || !sourceFile.exists() || !sourceFile.isFile()) {
                throw new IOException("USB content missing: " + spec.fileName);
	            }
            if (!FileIntegrityUtils.verifyFile(sourceFile, spec.sizeBytes, spec.checksum)) {
                throw new IOException("USB content verification failed: " + spec.fileName);
	            }

	            File targetFile = new File(targetDir, spec.fileName);
	            if (FileIntegrityUtils.verifyFile(targetFile, spec.sizeBytes, spec.checksum)) {
	                continue;
	            }

	            copyFileAtomically(sourceFile, targetFile, spec);
	        }
	    }

    private Map<String, ContentCopySpec> collectContentCopySpecs(List<UpdatePayloadModels.PageInfoClass> pages) {
        Map<String, ContentCopySpec> result = new LinkedHashMap<>();
        if (pages == null) {
            return result;
        }

        for (UpdatePayloadModels.PageInfoClass page : pages) {
            if (page == null || page.PIC_Elements == null) {
                continue;
            }
            for (UpdatePayloadModels.ElementInfoClass element : page.PIC_Elements) {
                if (element == null || element.EIF_ContentsInfoClassList == null) {
                    continue;
                }
                if (!"Media".equalsIgnoreCase(element.EIF_Type)) {
                    continue;
                }
                for (UpdatePayloadModels.ContentsInfoClass content : element.EIF_ContentsInfoClassList) {
                    addContentCopySpec(result, content);
                }
            }
        }
        return result;
    }

    private void addContentCopySpec(Map<String, ContentCopySpec> specs, UpdatePayloadModels.ContentsInfoClass content) {
        if (specs == null || content == null) {
            return;
        }
        if (!isFileBasedContent(content)) {
            return;
        }
        String fileName = resolveContentFileName(content);
        if (TextUtils.isEmpty(fileName)) {
            return;
        }

        String key = fileName.toLowerCase(Locale.US);
        ContentCopySpec existing = specs.get(key);
        String checksum = !TextUtils.isEmpty(content.CIF_FileHash)
                ? content.CIF_FileHash
                : content.CIF_StrGUID;
        long sizeBytes = Math.max(0L, content.CIF_FileSize);
        if (existing == null) {
            specs.put(key, new ContentCopySpec(fileName, content.CIF_RelativePath, sizeBytes, checksum));
            return;
        }

        if (existing.sizeBytes <= 0L && sizeBytes > 0L) {
            existing.sizeBytes = sizeBytes;
        }
        if (TextUtils.isEmpty(existing.checksum) && !TextUtils.isEmpty(checksum)) {
            existing.checksum = checksum;
        }
        if (TextUtils.isEmpty(existing.relativePath) && !TextUtils.isEmpty(content.CIF_RelativePath)) {
            existing.relativePath = content.CIF_RelativePath;
        }
    }

    private boolean isFileBasedContent(UpdatePayloadModels.ContentsInfoClass content) {
        if (content == null || TextUtils.isEmpty(content.CIF_ContentType)) {
            return true;
        }
        return !"WebSiteURL".equalsIgnoreCase(content.CIF_ContentType)
                && !"Browser".equalsIgnoreCase(content.CIF_ContentType);
    }

    private String resolveContentFileName(UpdatePayloadModels.ContentsInfoClass content) {
        if (content == null) {
            return "";
        }
        if (!TextUtils.isEmpty(content.CIF_FileName)) {
            return extractFileName(content.CIF_FileName);
        }
        if (!TextUtils.isEmpty(content.CIF_RelativePath)) {
            return extractFileName(content.CIF_RelativePath);
        }
        if (!TextUtils.isEmpty(content.CIF_FileFullPath)) {
            return extractFileName(content.CIF_FileFullPath);
        }
        return "";
    }

    private String extractFileName(String path) {
        if (TextUtils.isEmpty(path)) {
            return "";
        }
        String normalized = path.replace("\\", "/");
        int index = normalized.lastIndexOf('/');
        return index >= 0 ? normalized.substring(index + 1) : normalized;
    }

    private File resolveUsbSourceContentFile(File contentsDir, ContentCopySpec spec) {
        if (contentsDir == null || spec == null || TextUtils.isEmpty(spec.fileName)) {
            return null;
        }

        File direct = new File(contentsDir, spec.fileName);
        if (direct.exists()) {
            return direct;
        }

        String relativeName = extractFileName(spec.relativePath);
        if (!TextUtils.isEmpty(relativeName)) {
            File relative = new File(contentsDir, relativeName);
            if (relative.exists()) {
                return relative;
            }
        }

        return direct;
    }

	    private void copyFileAtomically(File sourceFile, File targetFile, ContentCopySpec spec) throws IOException {
        File parent = targetFile.getParentFile();
        if (parent != null && !parent.exists() && !parent.mkdirs()) {
            throw new IOException("Target directory create failed: " + parent.getAbsolutePath());
        }

        File tempFile = new File(targetFile.getParentFile(), targetFile.getName() + ".usbtmp");
        if (tempFile.exists() && !tempFile.delete()) {
            throw new IOException("Temp content delete failed: " + tempFile.getAbsolutePath());
        }

        FileInputStream input = null;
        FileOutputStream output = null;
        try {
            input = new FileInputStream(sourceFile);
            output = new FileOutputStream(tempFile);
            byte[] buffer = new byte[USB_COPY_BUFFER_SIZE];
            int read;
            while ((read = input.read(buffer)) != -1) {
                output.write(buffer, 0, read);
            }
            output.flush();
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignore) {
                }
            }
            if (output != null) {
                try {
                    output.close();
                } catch (Exception ignore) {
                }
            }
        }

	        if (!FileIntegrityUtils.verifyFile(tempFile, spec.sizeBytes, spec.checksum)) {
	            tempFile.delete();
	            throw new IOException("Copied content verification failed: " + spec.fileName);
	        }

        if (targetFile.exists() && !targetFile.delete()) {
            tempFile.delete();
            throw new IOException("Target content replace failed: " + targetFile.getAbsolutePath());
        }
        if (!tempFile.renameTo(targetFile)) {
            tempFile.delete();
            throw new IOException("Target content rename failed: " + targetFile.getAbsolutePath());
        }
    }

    private UpdatePayloadModels.ScheduleUpdatePayload buildSchedulePayload(PlaylistExportBundle playlistBundle,
                                                                           WeeklyScheduleExportBundle weeklyBundle,
                                                                           SpecialScheduleExportBundle specialBundle) {
        PlayerInfoExport selectedPlayer = selectPackagePlayer(playlistBundle == null ? null : playlistBundle.Players);
        UpdatePayloadModels.ScheduleUpdatePayload schedule = new UpdatePayloadModels.ScheduleUpdatePayload();
        fillSchedulePlayer(schedule, selectedPlayer);

        WeeklyScheduleExportItem weeklyItem = selectWeeklyItem(weeklyBundle, selectedPlayer);
        if (weeklyItem != null && weeklyItem.Schedule != null) {
            schedule.WeeklySchedule = weeklyItem.Schedule;
        }

        List<PlayerSpecialScheduleExport> specialItems = selectSpecialItems(specialBundle, selectedPlayer);
        for (PlayerSpecialScheduleExport item : specialItems) {
            if (item == null || item.Schedules == null) {
                continue;
            }

            for (SpecialScheduleInfoExport source : item.Schedules) {
                UpdatePayloadModels.SpecialSchedulePayload mapped = mapSpecialSchedule(source);
                if (mapped != null) {
                    schedule.SpecialSchedules.add(mapped);
                }
            }
        }

        if (!schedule.SpecialSchedules.isEmpty() && playlistBundle != null && playlistBundle.PageList != null) {
            UpdatePayloadModels.SchedulePlaylistPayload playlist = new UpdatePayloadModels.SchedulePlaylistPayload();
            playlist.PlaylistName = playlistBundle.PageList.PLI_PageListName;
            playlist.PageList = playlistBundle.PageList;
            playlist.Pages = playlistBundle.Pages == null
                    ? new ArrayList<UpdatePayloadModels.PageInfoClass>()
                    : playlistBundle.Pages;
            schedule.Playlists.add(playlist);
        }

        if (schedule.WeeklySchedule == null && schedule.SpecialSchedules.isEmpty()) {
            return null;
        }

        return schedule;
    }

    private void fillSchedulePlayer(UpdatePayloadModels.ScheduleUpdatePayload schedule, PlayerInfoExport selectedPlayer) {
        PlayerDataModel current = PlayerDataProvider.getPlayerData();
        schedule.PlayerId = selectedPlayer != null && !TextUtils.isEmpty(selectedPlayer.PIF_GUID)
                ? selectedPlayer.PIF_GUID
                : current.getPlayerId();
        schedule.PlayerName = selectedPlayer != null && !TextUtils.isEmpty(selectedPlayer.PIF_PlayerName)
                ? selectedPlayer.PIF_PlayerName
                : current.getPlayerName();
        schedule.GeneratedAt = String.valueOf(System.currentTimeMillis());
    }

    private WeeklyScheduleExportItem selectWeeklyItem(WeeklyScheduleExportBundle bundle, PlayerInfoExport selectedPlayer) {
        if (bundle == null || bundle.Items == null || bundle.Items.isEmpty()) {
            return null;
        }

        for (WeeklyScheduleExportItem item : bundle.Items) {
            if (item != null && isSamePlayer(item.Player, selectedPlayer)) {
                return item;
            }
        }

        return bundle.Items.size() == 1 ? bundle.Items.get(0) : null;
    }

    private List<PlayerSpecialScheduleExport> selectSpecialItems(SpecialScheduleExportBundle bundle, PlayerInfoExport selectedPlayer) {
        List<PlayerSpecialScheduleExport> result = new ArrayList<>();
        if (bundle == null || bundle.Items == null || bundle.Items.isEmpty()) {
            return result;
        }

        for (PlayerSpecialScheduleExport item : bundle.Items) {
            if (item != null && isSamePlayer(item.Player, selectedPlayer)) {
                result.add(item);
            }
        }

        if (result.isEmpty() && bundle.Items.size() == 1) {
            result.add(bundle.Items.get(0));
        }

        return result;
    }

    private PlayerInfoExport selectPackagePlayer(List<PlayerInfoExport> players) {
        if (players == null || players.isEmpty()) {
            return null;
        }

        for (PlayerInfoExport player : players) {
            if (isSamePlayer(player, null)) {
                return player;
            }
        }

        return players.size() == 1 ? players.get(0) : null;
    }

    private boolean isSamePlayer(PlayerInfoExport player, PlayerInfoExport selectedPlayer) {
        if (player == null) {
            return false;
        }

        if (selectedPlayer != null) {
            if (!TextUtils.isEmpty(selectedPlayer.PIF_GUID)
                    && selectedPlayer.PIF_GUID.equalsIgnoreCase(safe(player.PIF_GUID))) {
                return true;
            }
            if (!TextUtils.isEmpty(selectedPlayer.PIF_PlayerName)
                    && selectedPlayer.PIF_PlayerName.equalsIgnoreCase(safe(player.PIF_PlayerName))) {
                return true;
            }
        }

        PlayerDataModel current = PlayerDataProvider.getPlayerData();
        if (!TextUtils.isEmpty(player.PIF_GUID) && player.PIF_GUID.equalsIgnoreCase(current.getPlayerId())) {
            return true;
        }
        if (!TextUtils.isEmpty(player.PIF_PlayerName) && player.PIF_PlayerName.equalsIgnoreCase(current.getPlayerName())) {
            return true;
        }

        String localMac = NetworkUtils.getMACAddress();
        if (!TextUtils.isEmpty(player.PIF_MacAddress)
                && normalizeMac(player.PIF_MacAddress).equalsIgnoreCase(normalizeMac(localMac))) {
            return true;
        }

        String expectedAuthKey = AuthUtils.EncodeAuthKey(normalizeMac(localMac));
        return !TextUtils.isEmpty(player.PIF_AuthKey)
                && player.PIF_AuthKey.equalsIgnoreCase(expectedAuthKey);
    }

    private UpdatePayloadModels.SpecialSchedulePayload mapSpecialSchedule(SpecialScheduleInfoExport source) {
        if (source == null || TextUtils.isEmpty(source.PageListName)) {
            return null;
        }

        UpdatePayloadModels.SpecialSchedulePayload target = new UpdatePayloadModels.SpecialSchedulePayload();
        target.Id = source.Id == null ? "" : source.Id;
        target.PageListName = source.PageListName == null ? "" : source.PageListName;
        target.DayOfWeek1 = source.DayOfWeek1;
        target.DayOfWeek2 = source.DayOfWeek2;
        target.DayOfWeek3 = source.DayOfWeek3;
        target.DayOfWeek4 = source.DayOfWeek4;
        target.DayOfWeek5 = source.DayOfWeek5;
        target.DayOfWeek6 = source.DayOfWeek6;
        target.DayOfWeek7 = source.DayOfWeek7;
        target.IsPeriodEnable = source.IsPeriodEnable;
        target.DisplayStartH = source.DisplayStartH;
        target.DisplayStartM = source.DisplayStartM;
        target.DisplayEndH = source.DisplayEndH;
        target.DisplayEndM = source.DisplayEndM;
        target.PeriodStartYear = source.PeriodStartYear;
        target.PeriodStartMonth = source.PeriodStartMonth;
        target.PeriodStartDay = source.PeriodStartDay;
        target.PeriodEndYear = source.PeriodEndYear;
        target.PeriodEndMonth = source.PeriodEndMonth;
        target.PeriodEndDay = source.PeriodEndDay;
        return target;
    }

    private String normalizeMac(String value) {
        return value == null ? "" : value.replace(":", "").replace("-", "").trim();
    }

    private String safe(String value) {
        return value == null ? "" : value;
    }

	    private static class UsbApplyResult {
	        final boolean success;
	        final String playlistName;
	        final String message;

        private UsbApplyResult(boolean success, String playlistName, String message) {
            this.success = success;
            this.playlistName = playlistName;
            this.message = message;
        }

        static UsbApplyResult success(String playlistName) {
            return new UsbApplyResult(true, playlistName, "");
        }

        static UsbApplyResult fail(String message) {
            return new UsbApplyResult(false, "", message == null ? "" : message);
	        }
	    }

	    private static class ContentCopySpec {
        final String fileName;
        String relativePath;
        long sizeBytes;
        String checksum;

        ContentCopySpec(String fileName, String relativePath, long sizeBytes, String checksum) {
            this.fileName = fileName == null ? "" : fileName;
            this.relativePath = relativePath == null ? "" : relativePath;
            this.sizeBytes = sizeBytes;
            this.checksum = checksum == null ? "" : checksum;
        }
    }

    private static class PlaylistExportBundle {
        String PlaylistName = "";
        UpdatePayloadModels.PageListInfoClass PageList;
        List<UpdatePayloadModels.PageInfoClass> Pages = new ArrayList<>();
        List<PlayerInfoExport> Players = new ArrayList<>();
    }

    private static class WeeklyScheduleExportBundle {
        List<WeeklyScheduleExportItem> Items = new ArrayList<>();
    }

    private static class WeeklyScheduleExportItem {
        PlayerInfoExport Player;
        UpdatePayloadModels.WeeklyPlayScheduleInfo Schedule;
    }

    private static class SpecialScheduleExportBundle {
        List<PlayerSpecialScheduleExport> Items = new ArrayList<>();
    }

    private static class PlayerSpecialScheduleExport {
        PlayerInfoExport Player;
        List<SpecialScheduleInfoExport> Schedules = new ArrayList<>();
    }

    private static class PlayerInfoExport {
        @com.google.gson.annotations.SerializedName("id")
        String PIF_GUID = "";
        String PIF_PlayerName = "";
        String PIF_MacAddress = "";
        String PIF_AuthKey = "";
    }

    private static class SpecialScheduleInfoExport {
        @com.google.gson.annotations.SerializedName("id")
        String Id = "";
        List<String> PlayerNames = new ArrayList<>();
        String PageListName = "";
        boolean DayOfWeek1;
        boolean DayOfWeek2;
        boolean DayOfWeek3;
        boolean DayOfWeek4;
        boolean DayOfWeek5;
        boolean DayOfWeek6;
        boolean DayOfWeek7;
        boolean IsPeriodEnable;
        int DisplayStartH;
        int DisplayStartM;
        int DisplayEndH;
        int DisplayEndM;
        int PeriodStartYear;
        int PeriodStartMonth;
        int PeriodStartDay;
        int PeriodEndYear;
        int PeriodEndMonth;
        int PeriodEndDay;
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

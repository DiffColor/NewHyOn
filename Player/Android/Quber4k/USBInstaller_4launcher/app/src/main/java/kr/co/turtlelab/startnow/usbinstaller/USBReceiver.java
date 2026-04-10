package kr.co.turtlelab.startnow.usbinstaller;

import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.os.Environment;
import android.net.Uri;
import android.content.pm.PackageInfo;
import android.util.Log;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

public class USBReceiver extends BroadcastReceiver {

    private static final String TAG = "USBInstaller";
    private static final String INTENT_MSG = "kr.co.turtlelab.notifier.msg";
    private static final String INTENT_FINISH = "kr.co.turtlelab.notifier.finish";
    private static final String WATCHDOG_DISABLE = "kr.co.turtlelab.watchdog.disable";
    private static final String WATCHDOG_ENABLE = "kr.co.turtlelab.watchdog.enable";
    private static final String PACKAGE_NOTIFIER = "kr.co.turtlelab.notifier";
    private static final String PACKAGE_WATCHDOG = "kr.co.turtlelab.startnow.watchdog";
    private static final String RECEIVER_NOTIFIER = "kr.co.turtlelab.notifier.NotiReceiver";
    private static final String RECEIVER_WATCHDOG = "kr.co.turtlelab.startnow.watchdog.WatchDogReceiver";
    private static final String USB_DIRNAME = "APKs";
    private static final long INSTALL_VERIFY_TIMEOUT_MS = 90000L;
    private static final long INSTALL_VERIFY_INTERVAL_MS = 1500L;
    private static final long NOTIFIER_BOOTSTRAP_DELAY_MS = 1200L;
    private static final long MIN_NOTIFIER_VISIBLE_MS = 2500L;
    private static final boolean INSTALL_APK_DIRECT_FROM_USB = true;
    private static final boolean ENABLE_LEGACY_STAGING_FALLBACK = false;
    private static final String STAGING_DIRNAME = "quber_apk_stage";
    private static final int MIN_COPY_BUFFER_SIZE = 1 * 1024 * 1024;
    private static final int COPY_BUFFER_SIZE = Math.max(MIN_COPY_BUFFER_SIZE, 2 * 1024 * 1024);

    @Override
    public void onReceive(final Context context, Intent intent) {
        if (context == null || intent == null) {
            return;
        }

        if (!Intent.ACTION_MEDIA_MOUNTED.equals(intent.getAction())) {
            return;
        }

        final Intent receivedIntent = new Intent(intent);
        final PendingResult pendingResult = goAsync();
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    handleMounted(context, receivedIntent);
                } finally {
                    pendingResult.finish();
                }
            }
        }, "usb-installer").start();
    }

    private void handleMounted(Context context, Intent intent) {
        List<File> searchRoots = buildSearchRoots(context, intent);
        File installDir = findInstallDir(searchRoots, USB_DIRNAME);
        if (installDir == null) {
            Log.w(TAG, "APKs directory not found. roots=" + joinPaths(searchRoots));
            return;
        }

        List<File> apkFiles = collectApkFiles(installDir);
        if (apkFiles.isEmpty()) {
            Log.w(TAG, "APKs directory found but apk file not found: " + installDir.getAbsolutePath());
            return;
        }

        Log.d(TAG, "Found APKs directory: " + installDir.getAbsolutePath() + ", count=" + apkFiles.size());
        sendMsg(context, WATCHDOG_DISABLE, null);
        sendMsg(context, INTENT_MSG, "앱 설치를 시작합니다.");
        long notifierShownAt = System.currentTimeMillis();
        sleepQuietly(NOTIFIER_BOOTSTRAP_DELAY_MS);

        try {
            for (File apkFile : apkFiles) {
                installSingleApk(context, apkFile);
            }
        } finally {
            ensureNotifierVisibleEnough(notifierShownAt);
            sendMsg(context, INTENT_FINISH, null);
            sendMsg(context, WATCHDOG_ENABLE, null);
        }
    }

    private void installSingleApk(Context context, File apkFile) {
        if (apkFile == null || !apkFile.isFile()) {
            return;
        }

        File stagedApk = null;
        try {
            if (INSTALL_APK_DIRECT_FROM_USB) {
                boolean installedFromUsb = requestInstall(context, apkFile, apkFile, "원본 경로");
                if (installedFromUsb || !ENABLE_LEGACY_STAGING_FALLBACK) {
                    return;
                }

                sendMsg(context, INTENT_MSG, apkFile.getName() + " 원본 경로 요청 실패로 스테이징 설치를 시도합니다.");
            }

            stagedApk = stageApkForInstall(context, apkFile);
            if (stagedApk == null) {
                sendMsg(context, INTENT_MSG, apkFile.getName() + " 스테이징에 실패했습니다.");
                return;
            }

            requestInstall(context, apkFile, stagedApk, "스테이징 경로");
        } finally {
            deleteQuietly(stagedApk);
        }
    }

    private boolean requestInstall(Context context, File sourceApk, File installApk, String installModeLabel) {
        if (context == null || sourceApk == null || installApk == null || !installApk.isFile()) {
            return false;
        }

        PackageInfo archiveInfo = SystemUtils.getArchivePackageInfo(context, installApk.getAbsolutePath());
        String displayName = resolveDisplayName(context, sourceApk, archiveInfo);
        sendMsg(context, INTENT_MSG, displayName + " 설치 요청을 전송합니다. (" + installModeLabel + ")");

        boolean sent = QuberInstallAgentClient.requestInstall(context, installApk.getAbsolutePath());
        if (!sent) {
            sendMsg(context, INTENT_MSG, displayName + " 설치 요청 전송에 실패했습니다. (" + installModeLabel + ")");
            return false;
        }

        boolean verified = waitForInstall(context, archiveInfo);
        if (verified) {
            sendMsg(context, INTENT_MSG, displayName + " 설치가 확인되었습니다.");
            return true;
        }

        sendMsg(context, INTENT_MSG, displayName + " 설치 확인이 지연되고 있습니다.");
        return false;
    }

    private String resolveDisplayName(Context context, File sourceApk, PackageInfo archiveInfo) {
        String displayName = sourceApk == null ? "" : sourceApk.getName();
        if (archiveInfo != null && archiveInfo.applicationInfo != null) {
            CharSequence label = archiveInfo.applicationInfo.loadLabel(context.getPackageManager());
            if (label != null && label.length() > 0) {
                displayName = label.toString();
            }
        }
        return displayName;
    }

    @SuppressWarnings("deprecation")
    private File stageApkForInstall(Context context, File sourceApk) {
        if (context == null || !isInstallableApkFile(sourceApk)) {
            return null;
        }

        File stagingDir = resolveStagingDir(context);
        if (stagingDir == null) {
            return null;
        }

        if (!stagingDir.exists() && !stagingDir.mkdirs()) {
            Log.w(TAG, "Failed to create staging dir: " + stagingDir.getAbsolutePath());
            return null;
        }

        stagingDir.setReadable(true, false);
        stagingDir.setExecutable(true, false);
        stagingDir.setWritable(true, true);
        cleanupStagingDir(stagingDir);

        File stagedApk = new File(stagingDir, buildStagedApkName(sourceApk));
        deleteQuietly(stagedApk);

        try {
            copyFile(sourceApk, stagedApk);
            stagedApk.setReadable(true, false);
            Log.d(TAG, "Staged apk for Quber install: " + stagedApk.getAbsolutePath());
            return stagedApk;
        } catch (IOException e) {
            Log.e(TAG, "Failed to stage apk: " + sourceApk.getAbsolutePath(), e);
            deleteQuietly(stagedApk);
            return null;
        }
    }

    @SuppressWarnings("deprecation")
    private File resolveStagingDir(Context context) {
        File externalAppDir = null;
        try {
            File appExternalRoot = context.getExternalFilesDir(null);
            if (appExternalRoot != null) {
                externalAppDir = new File(appExternalRoot, STAGING_DIRNAME);
            }
        } catch (Throwable ignore) {
        }

        if (canUseStagingDir(externalAppDir)) {
            Log.d(TAG, "Using app-scoped external staging dir: " + externalAppDir.getAbsolutePath());
            return externalAppDir;
        }

        return null;
    }

    private boolean canUseStagingDir(File dir) {
        if (dir == null) {
            return false;
        }

        File parent = dir.getParentFile();
        if (parent != null && !parent.exists() && !parent.mkdirs()) {
            return false;
        }

        if (!dir.exists() && !dir.mkdirs()) {
            return false;
        }

        return dir.isDirectory() && dir.canWrite();
    }

    private void cleanupStagingDir(File stagingDir) {
        if (stagingDir == null || !stagingDir.isDirectory()) {
            return;
        }

        File[] list = stagingDir.listFiles();
        if (list == null) {
            return;
        }

        for (File file : list) {
            deleteQuietly(file);
        }
    }

    private String buildStagedApkName(File sourceApk) {
        String name = sourceApk.getName();
        String sanitized = name.replaceAll("[^A-Za-z0-9._-]", "_");
        return System.currentTimeMillis() + "_" + sanitized;
    }

    private boolean isInstallableApkFile(File file) {
        if (file == null || !file.isFile()) {
            return false;
        }

        String name = file.getName();
        if (name == null || name.length() < 1) {
            return false;
        }

        if (isIgnoredMetadataFileName(name)) {
            return false;
        }

        return name.toLowerCase(Locale.US).endsWith(".apk");
    }

    private boolean isIgnoredMetadataFileName(String name) {
        if (name == null || name.length() < 1) {
            return true;
        }

        return name.startsWith(".")
                || "thumbs.db".equalsIgnoreCase(name);
    }

    private void copyFile(File source, File target) throws IOException {
        BufferedInputStream input = null;
        BufferedOutputStream output = null;
        try {
            input = new BufferedInputStream(new FileInputStream(source));
            output = new BufferedOutputStream(new FileOutputStream(target));

            byte[] buffer = new byte[COPY_BUFFER_SIZE];
            int read;
            while ((read = input.read(buffer)) != -1) {
                output.write(buffer, 0, read);
            }
            output.flush();
        } finally {
            closeQuietly(output);
            closeQuietly(input);
        }
    }

    private void closeQuietly(java.io.Closeable closeable) {
        if (closeable == null) {
            return;
        }

        try {
            closeable.close();
        } catch (IOException ignore) {
        }
    }

    private void deleteQuietly(File file) {
        if (file == null || !file.exists()) {
            return;
        }

        if (file.isDirectory()) {
            File[] list = file.listFiles();
            if (list != null) {
                for (File child : list) {
                    deleteQuietly(child);
                }
            }
        }

        if (!file.delete()) {
            Log.w(TAG, "Failed to delete: " + file.getAbsolutePath());
        }
    }

    private boolean waitForInstall(Context context, PackageInfo archiveInfo) {
        if (archiveInfo == null || archiveInfo.packageName == null || archiveInfo.packageName.length() < 1) {
            sleepQuietly(3000L);
            return true;
        }

        long startedAt = System.currentTimeMillis();
        while (System.currentTimeMillis() - startedAt < INSTALL_VERIFY_TIMEOUT_MS) {
            PackageInfo installedInfo = SystemUtils.getInstalledPackageInfo(context, archiveInfo.packageName);
            if (installedInfo != null && installedInfo.versionCode == archiveInfo.versionCode) {
                return true;
            }
            sleepQuietly(INSTALL_VERIFY_INTERVAL_MS);
        }
        return false;
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
        if (path.length() < 1) {
            return;
        }

        paths.add(path);
    }

    private List<File> collectApkFiles(File installDir) {
        List<File> apkFiles = new ArrayList<>();
        collectApkFilesRecursive(installDir, apkFiles);
        Collections.sort(apkFiles, new Comparator<File>() {
            @Override
            public int compare(File left, File right) {
                return left.getAbsolutePath().compareToIgnoreCase(right.getAbsolutePath());
            }
        });
        return apkFiles;
    }

    private void collectApkFilesRecursive(File dir, List<File> result) {
        if (dir == null || !dir.isDirectory()) {
            return;
        }

        File[] list = dir.listFiles();
        if (list == null) {
            return;
        }

        for (File file : list) {
            if (file.isDirectory()) {
                collectApkFilesRecursive(file, result);
            } else if (isInstallableApkFile(file)) {
                result.add(file);
            }
        }
    }

    private File findInstallDir(List<File> searchRoots, String dirname) {
        if (searchRoots == null) {
            return null;
        }

        for (File root : searchRoots) {
            File found = findInstallDir(root, dirname);
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    private File findInstallDir(File base, String dirname) {
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
                File found = findInstallDir(file, dirname);
                if (found != null) {
                    return found;
                }
            }
        }
        return null;
    }

    private void sendMsg(Context context, String actionStr, String msgStr) {
        Intent sendIntent = new Intent();
        sendIntent.setAction(actionStr);
        sendIntent.setFlags(Intent.FLAG_RECEIVER_FOREGROUND | Intent.FLAG_INCLUDE_STOPPED_PACKAGES);
        sendIntent.putExtra("msg", msgStr);
        if (INTENT_MSG.equals(actionStr) || INTENT_FINISH.equals(actionStr)) {
            sendIntent.setComponent(new ComponentName(PACKAGE_NOTIFIER, RECEIVER_NOTIFIER));
        } else if (WATCHDOG_DISABLE.equals(actionStr) || WATCHDOG_ENABLE.equals(actionStr)) {
            sendIntent.setComponent(new ComponentName(PACKAGE_WATCHDOG, RECEIVER_WATCHDOG));
        }
        context.sendBroadcast(sendIntent);
    }

    private void ensureNotifierVisibleEnough(long notifierShownAt) {
        if (notifierShownAt <= 0L) {
            return;
        }

        long elapsed = System.currentTimeMillis() - notifierShownAt;
        long remaining = MIN_NOTIFIER_VISIBLE_MS - elapsed;
        if (remaining > 0L) {
            sleepQuietly(remaining);
        }
    }

    private String joinPaths(List<File> files) {
        if (files == null || files.isEmpty()) {
            return "";
        }

        List<String> paths = new ArrayList<>();
        for (File file : files) {
            if (file != null) {
                paths.add(file.getAbsolutePath());
            }
        }
        return String.format(Locale.US, "%s", paths);
    }

    private void sleepQuietly(long millis) {
        try {
            Thread.sleep(millis);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }
}

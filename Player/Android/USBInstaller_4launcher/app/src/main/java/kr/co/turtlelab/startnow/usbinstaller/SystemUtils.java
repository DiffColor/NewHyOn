package kr.co.turtlelab.startnow.usbinstaller;

import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;

import java.io.File;

public class SystemUtils {

    public static void startNewActivity(Context context, String packageName) {
        if (context == null || packageName == null || packageName.length() < 1) {
            return;
        }

        try {
            Intent intent = context.getPackageManager().getLaunchIntentForPackage(packageName);
            if (intent == null) {
                return;
            }
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            context.startActivity(intent);
        } catch (Exception ignore) {
        }
    }

    public static PackageInfo getArchivePackageInfo(Context context, String apkpath) {
        if (context == null || apkpath == null || apkpath.length() < 1) {
            return null;
        }

        try {
            File file = new File(apkpath);
            if (!file.exists()) {
                return null;
            }

            PackageInfo packInfo = context.getPackageManager().getPackageArchiveInfo(apkpath, 0);
            if (packInfo != null && packInfo.applicationInfo != null) {
                packInfo.applicationInfo.sourceDir = apkpath;
                packInfo.applicationInfo.publicSourceDir = apkpath;
            }
            return packInfo;
        } catch (Exception ignore) {
            return null;
        }
    }

    public static PackageInfo getInstalledPackageInfo(Context context, String packageName) {
        if (context == null || packageName == null || packageName.length() < 1) {
            return null;
        }

        try {
            return context.getPackageManager().getPackageInfo(packageName, 0);
        } catch (PackageManager.NameNotFoundException ignore) {
            return null;
        } catch (Exception ignore) {
            return null;
        }
    }
}

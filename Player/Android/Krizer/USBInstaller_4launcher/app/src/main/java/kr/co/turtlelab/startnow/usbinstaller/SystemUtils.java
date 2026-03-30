package kr.co.turtlelab.startnow.usbinstaller;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.View;

import java.io.File;

public class SystemUtils {

    public static void uninstallAPK(String packagename) {
        try {
            Process proc = Runtime.getRuntime().exec(new String[]{"pm", "uninstall", packagename});
            proc.waitFor();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public static void installAPK(String apkpath) {
        File file = new File(apkpath);
        if (file.exists()) {
            try {
                Process proc = Runtime.getRuntime().exec(new String[]{"pm", "install", file.getAbsolutePath()});
                proc.waitFor();
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
    }

    public static void replaceAPK(String packagename, String apkpath) {
        uninstallAPK(packagename);
        installAPK(apkpath);
    }

    public static void replaceAPK(Context ctx, String apkpath) {
        String packageName = getPkgNameByAPK(ctx, apkpath);
        if (packageName != null && packageName.length() > 0) {
            uninstallAPK(packageName);
        }
        installAPK(apkpath);
    }

    public static void startNewActivity(Context context, String packageName) {
        Intent intent = context.getPackageManager().getLaunchIntentForPackage(packageName);
        if (intent == null) {
            return;
        }
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        context.startActivity(intent);
    }

    public static boolean launchAnotherAct(Activity act, String pkgname, String clsname, boolean cleartask) {
        try {
            Intent intent = new Intent();
            intent.setComponent(new ComponentName(pkgname, pkgname + "." + clsname));

            if (cleartask) {
                intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
            }

            act.startActivity(intent);
            act.finish();
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    public static boolean launchAnotherActWithData(Activity act, String pkgname, String clsname,
                                                   String data_apkpath, String data_pkgname,
                                                   String data_clsname, boolean cleartask) {
        try {
            Intent intent = new Intent();
            intent.setComponent(new ComponentName(pkgname, pkgname + "." + clsname));

            Bundle extras = new Bundle();
            extras.putString("apkpath", data_apkpath);
            extras.putString("pkgname", data_pkgname);
            extras.putString("clsname", data_clsname);

            intent.putExtras(extras);

            if (cleartask) {
                intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
            }

            act.startActivity(intent);
            act.finish();
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    public static boolean restartAct(Context ctx, boolean cleartask) {
        try {
            Intent intent = ctx.getPackageManager().getLaunchIntentForPackage(ctx.getPackageName());
            if (intent == null) {
                return false;
            }

            if (cleartask) {
                intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
            }

            ctx.startActivity(intent);
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    public static void systemBarVisibility(Activity act, boolean visible) {
        if (visible) {
            act.getWindow().getDecorView().setSystemUiVisibility(
                    View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);
        } else {
            act.getWindow().getDecorView().setSystemUiVisibility(
                    View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        }
    }

    public static void runOnUiThread(Runnable runnable) {
        final Handler uiHandler = new Handler(Looper.getMainLooper());
        uiHandler.post(runnable);
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

    public static String getPkgNameByAPK(Context ctx, String apkpath) {
        if (ctx == null || apkpath == null || apkpath.length() < 1) {
            return "";
        }

        try {
            PackageManager packMan = ctx.getPackageManager();
            PackageInfo packInfo = packMan.getPackageArchiveInfo(apkpath, 0);
            if (packInfo == null) {
                return "";
            }
            return packInfo.packageName;
        } catch (Exception e) {
            return "";
        }
    }
}

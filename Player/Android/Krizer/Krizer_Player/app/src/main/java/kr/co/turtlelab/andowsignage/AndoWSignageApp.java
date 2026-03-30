package kr.co.turtlelab.andowsignage;

import android.app.Application;
import android.content.Context;
import android.graphics.Point;
import android.os.Environment;
import android.view.Display;
import android.view.WindowManager;

import java.io.File;

import io.realm.Realm;
import io.realm.RealmConfiguration;
import kr.co.turtlelab.andowsignage.tools.CanvasUtils;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class AndoWSignageApp extends Application {
	
	public static String LOG = "AndoWSignage";

	public enum ELEMENT_TYPE { None, Media, HDTV, IPTV, ScrollText, WelcomeBoard, TemplateBoard };
	public enum CONTENT_TYPE { None, Video, Image, Browser, Flash, PPT, HDTV, IPTV, WebSiteURL };
	
	public enum DAY_OF_WEEK { MON, TUE, WED, THU, FRI, SAT, SUN };
	
    public enum RP_STATUS { playing, stopped, updating };
    public enum RP_ORDER { updatelist, updateschedule, upgrade, reboot, check, getmac, poweroff, wakeup, clearqueue };

	String APP_ROOT = Environment.getExternalStorageDirectory().getPath() + "/AndoWSignage/";
	
	public static String PLAYER_ID = "";
	public static String MANAGER_IP = "";
	public static String AUTO_IP = "";
	public static String MANUAL_IP = "";
	public static boolean IS_MANUAL = false;

	public static String MSG_ADDRESS;
	public final static int MSG_PORT = 8002;
	
	public static int FTP_PORT = 10021;
	public static String FTP_LOGIN_ID = "asdf";
	public static String FTP_LOGIN_PW = "Emfndhk!";
	
	private static AndoWSignageApp sApp = null;
	private static int sDevice_Width = 0;
	private static int sDevice_Height = 0;
	private static float sScale = 1.0f;
	private static float sScale_X = 1.0f;
	private static float sScale_Y = 1.0f;
	private static String sDir_Path = null;
	
	static final int fixed_base_width = 1920;
	static final int fixed_base_height = 1080;
	
	public static boolean isRunning = false;
	public static boolean isUpdating = false;
	public static boolean isSlept = false;
	private static volatile boolean shutdownInProgress = false;

	public static String state = RP_STATUS.stopped.toString();	// playing, stopped, updating
	public static String process = "0";		// download process
	public static String version;
	
	public static int networkState = NetworkUtils.TYPE_NOT_CONNECTED;	

	public static boolean KEEP_ASPECT_RATIO = false;
	public static boolean SWITCH_ON_CONTENT_END = false;

	@Override
	public void onCreate() {
		super.onCreate();
		sApp = this;
		clearShutdownInProgress();
		initRealm();
		init();
	}

	private void initRealm() {
		Realm.init(this);
		File realmDir = new File(APP_ROOT);
		if (!realmDir.exists()) {
			realmDir.mkdirs();
		}
		RealmConfiguration config = new RealmConfiguration.Builder()
				.directory(realmDir)
				.name("andow.realm")
				.deleteRealmIfMigrationNeeded()
				.allowWritesOnUiThread(true)
				.build();
		Realm.setDefaultConfiguration(config);
	}
	
	synchronized public static AndoWSignageApp getApplication() {
		return sApp;
	}
	
	private void init() {
		Display display = ((WindowManager) getSystemService(Context.WINDOW_SERVICE)).getDefaultDisplay();         
		Point outSize = new Point();
		display.getSize(outSize);
		sDevice_Width = outSize.x;
		sDevice_Height = outSize.y;
		
		setScaleFactors();
		checkOrCreateFolder();
		
		networkState = NetworkUtils.getConnectivityStatus(this);
	}
	
	private static void setScaleFactors() {
        float[] scales = CanvasUtils.getScaleFactors(fixed_base_width, fixed_base_height, sDevice_Width, sDevice_Height);
        sScale = scales[0];
        sScale_X = scales[1];
        sScale_Y = scales[2];
	}
	
	public static float getScale() {
		return sScale;
	}

	public static float getScaleX() {
		return sScale_X;
	}
	
	public static float getScaleY() {
		return sScale_Y;
	}
	
	public static int getDeviceWidth() {
		return sDevice_Width;
	}
	
	public static int getDeviceHeight() {
		return sDevice_Height;
	}
	
	public static void updateDisplaySize(int width, int height) {
		sDevice_Width = width;
		sDevice_Height = height;
		
		setScaleFactors();
	}
	
	private void checkOrCreateFolder() {
		LocalPathUtils.checkTargetFolders(this, APP_ROOT);
		setDirPath(APP_ROOT);

		LocalPathUtils.checkTargetFolders(this, LocalPathUtils.getContentsDirPath());
		LocalPathUtils.checkTargetFolders(this, LocalPathUtils.getFontsDirPath());
		LocalPathUtils.checkTargetFolders(this, LocalPathUtils.getUpgradeAPKDirPath());
	}
	
	public void setDirPath(String path) {
		sDir_Path = path;
	}
	
	public static String getDirPath() {
		return sDir_Path;
	}

	public static void beginShutdown() {
		shutdownInProgress = true;
	}

	public static void clearShutdownInProgress() {
		shutdownInProgress = false;
	}

	public static boolean isShutdownInProgress() {
		return shutdownInProgress;
	}
}

package kr.co.turtlelab.andowsignage;

import android.app.Application;
import android.content.Context;
import android.graphics.Point;
import android.view.Display;
import android.view.WindowManager;

import java.io.File;

import io.realm.Realm;
import io.realm.RealmConfiguration;
import kr.co.turtlelab.andowsignage.tools.CanvasUtils;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class AndoWSignageApp extends Application {

	private static final String APP_ROOT_DIRNAME = "AndoWSignage";
		
	public static String LOG = "AndoWSignage";

	public enum ELEMENT_TYPE { None, Media, HDTV, IPTV, ScrollText, WelcomeBoard, TemplateBoard };
	public enum CONTENT_TYPE { None, Video, Image, Browser, Flash, PPT, HDTV, IPTV, WebSiteURL };
	
	public enum DAY_OF_WEEK { MON, TUE, WED, THU, FRI, SAT, SUN };
	
    public enum RP_STATUS { playing, stopped, updating };
    public enum RP_ORDER { updatelist, updateschedule, updateweekly, upgrade, reboot, check, getmac, poweroff, wakeup, clearqueue };

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
	private static volatile boolean hdmiConnected = false;
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
		File appRootDir = prepareAppRoot();
		setDirPath(appRootDir.getAbsolutePath());
		initRealm(appRootDir);
		init();
	}

	private File prepareAppRoot() {
		File appRootDir = resolveAppRootDir(this);
		if (appRootDir == null) {
			throw new IllegalStateException("앱 내부 저장소를 사용할 수 없습니다.");
		}
		if (!appRootDir.exists()) {
			appRootDir.mkdirs();
		}
		return appRootDir;
	}

	private void initRealm(File appRootDir) {
		Realm.init(this);
		File realmDir = appRootDir;
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
		boolean isLandscapeDevice = sDevice_Width >= sDevice_Height;
		int baseWidth = isLandscapeDevice ? fixed_base_width : fixed_base_height;
		int baseHeight = isLandscapeDevice ? fixed_base_height : fixed_base_width;
        float[] scales = CanvasUtils.getScaleFactors(baseWidth, baseHeight, sDevice_Width, sDevice_Height);
        sScale = scales[0];
        sScale_X = scales[1];
        sScale_Y = scales[2];
	}

	public static float[] getScaleFactorsForCanvas(double baseWidth, double baseHeight) {
		int safeBaseWidth = (int)Math.round(baseWidth);
		int safeBaseHeight = (int)Math.round(baseHeight);
		if (safeBaseWidth <= 0 || safeBaseHeight <= 0) {
			safeBaseWidth = fixed_base_width;
			safeBaseHeight = fixed_base_height;
		}
		int targetWidth = sDevice_Width > 0 ? sDevice_Width : fixed_base_width;
		int targetHeight = sDevice_Height > 0 ? sDevice_Height : fixed_base_height;
		return CanvasUtils.getScaleFactors(safeBaseWidth, safeBaseHeight, targetWidth, targetHeight);
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
		String appRootPath = getDirPath();
		if (appRootPath == null || appRootPath.length() < 1) {
			File appRootDir = getAppRootDir();
			if (appRootDir != null) {
				appRootPath = appRootDir.getAbsolutePath();
				setDirPath(appRootPath);
			}
		}

		if (appRootPath == null || appRootPath.length() < 1) {
			return;
		}

		LocalPathUtils.checkTargetFolders(this, appRootPath);

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

	public static File getAppRootDir() {
		if (sDir_Path != null && sDir_Path.length() > 0) {
			return new File(sDir_Path);
		}

		if (sApp == null) {
			return null;
		}

		return resolveAppRootDir(sApp);
	}

	private static File resolveAppRootDir(Context context) {
		if (context == null) {
			return null;
		}

		File internalRoot = null;
		try {
			internalRoot = context.getFilesDir();
		} catch (Throwable ignore) {
		}
		if (internalRoot != null) {
			return new File(internalRoot, APP_ROOT_DIRNAME);
		}

		return null;
	}

	private static volatile long shutdownToken = 0L;

	public static synchronized long beginShutdown() {
		shutdownInProgress = true;
		shutdownToken++;
		return shutdownToken;
	}

	public static void markPlayingState() {
		state = RP_STATUS.playing.toString();
	}

	public static void markUpdatingState() {
		state = RP_STATUS.updating.toString();
	}

	public static void markStoppedState() {
		state = RP_STATUS.stopped.toString();
	}

	public static boolean isHdmiConnected() {
		return hdmiConnected;
	}

	public static void updateHdmiConnected(boolean connected) {
		hdmiConnected = connected;
	}

	public static void clearShutdownInProgress() {
		shutdownInProgress = false;
		synchronized (AndoWSignageApp.class) {
			shutdownToken++;
		}
	}

	public static boolean isShutdownInProgress() {
		return shutdownInProgress;
	}

	public static synchronized boolean matchesShutdownToken(long token) {
		return shutdownInProgress && shutdownToken == token;
	}

	public static boolean shouldReportStoppedState() {
		return isSlept || shutdownInProgress;
	}
}

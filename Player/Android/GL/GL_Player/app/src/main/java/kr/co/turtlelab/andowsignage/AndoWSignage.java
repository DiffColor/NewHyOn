package kr.co.turtlelab.andowsignage;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.ServiceConnection;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Typeface;
import android.media.MediaPlayer;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.SystemClock;
import android.view.inputmethod.InputMethodManager;
import android.os.IBinder;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.View.OnLongClickListener;
import android.view.View.OnTouchListener;
import android.view.ViewConfiguration;
import android.view.WindowManager;
import android.widget.ProgressBar;
import android.widget.RelativeLayout;
import android.widget.RelativeLayout.LayoutParams;
import android.widget.TextView;

import com.nostra13.universalimageloader.cache.disc.naming.Md5FileNameGenerator;
import com.nostra13.universalimageloader.core.ImageLoader;
import com.nostra13.universalimageloader.core.ImageLoaderConfiguration;
import com.nostra13.universalimageloader.core.assist.QueueProcessingType;

import org.apache.commons.io.FilenameUtils;

import java.io.File;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import kr.co.turtlelab.andowsignage.AndoWSignageApp.RP_STATUS;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.datamodels.ElementDataModel;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.datamodels.PageDataModel;
import kr.co.turtlelab.andowsignage.datamodels.PlayerDataModel;
import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.ElementDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.dataproviders.MediaDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlaylistDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.ScrolltextDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.WelcomeDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.UpdateQueueProvider;
import kr.co.turtlelab.andowsignage.receivers.SystemMsgReceiver;
import kr.co.turtlelab.andowsignage.services.ConfigLinkService;
import kr.co.turtlelab.andowsignage.services.PowerService;
import kr.co.turtlelab.andowsignage.services.UpdateManagerService;
import kr.co.turtlelab.andowsignage.services.UpdateManagerService.UpdateMgrLocalBinder;
import kr.co.turtlelab.andowsignage.services.HeartbeatService;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.ExceptionHandler;
import kr.co.turtlelab.andowsignage.tools.FileUtils;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.MediaScanner;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.SpecialScheduleEvaluator;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;
import kr.co.turtlelab.andowsignage.tools.Utils;
import kr.co.turtlelab.andowsignage.tools.WakeLocker;
import kr.co.turtlelab.andowsignage.data.update.ContentDownloadJournal;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;
import kr.co.turtlelab.andowsignage.views.GifMovieView;
import kr.co.turtlelab.andowsignage.views.KeyCaptureEditText;
import kr.co.turtlelab.andowsignage.views.MediaView;
import kr.co.turtlelab.andowsignage.views.PlaybackSlotView;
import kr.co.turtlelab.andowsignage.views.ScrollTextView;
import kr.co.turtlelab.andowsignage.views.TurtleVideoView;
import kr.co.turtlelab.andowsignage.views.TurtleWebView;
import kr.co.turtlelab.andowsignage.views.WelcomeView;


public class AndoWSignage extends Activity {

	public static AndoWSignage act;
	private static Context sCtx = null;
	
	public BroadcastReceiver mReceiver;
	SystemMsgReceiver mSystemMsgReceiver = null;	
	
    IntentFilter intentFilter;
    static final String ACTION_CALL_SETTINGS = "andowsignage.intent.action.CALL_SETTINGS";
    static final String ACTION_STOP_AND_SLEEP = "andowsignage.intent.action.STOP_AND_SLEEP";
    static final String ACTION_REFRESH_CS = "andowsignage.intent.action.REFRESH_CS";
    
    private ConfigDialog m_settingDlg;
	
	public PlayerDataModel playerData = new PlayerDataModel();
	List<PageDataModel> pageDataList = new ArrayList<PageDataModel>();
	private final Map<String, List<PageDataModel>> playlistPageCache = new ConcurrentHashMap<>();
	List<ElementDataModel> elementDataList = new ArrayList<ElementDataModel>();
	List<View> elementViewList = new ArrayList<View>();
	WelcomeDataModel wdm = new WelcomeDataModel();
	private boolean pendingUpdateReady = false;
	private boolean queuedUpdateRestartPending = false;
	private TextView debugOverlay;
	private boolean debugOverlayVisible = false;
	private KeyCaptureEditText keyInputOverlay;
	private static final long OVERLAY_SEQUENCE_TIMEOUT_MS = 1200L;
	private final StringBuilder overlayCommandBuffer = new StringBuilder();
	private long overlayCommandLastInputAt = 0L;
	private boolean suppressOverlayTextWatcher = false;
	private final SpecialScheduleEvaluator scheduleEvaluator = new SpecialScheduleEvaluator();

	RelativeLayout layout_root;
	RelativeLayout overlay_container;
	RelativeLayout activePageContainer;
	RelativeLayout stagedPageContainer;
	RelativeLayout specialPageContainer;
	RelativeLayout.LayoutParams layout_params;
	
	static int pageIdx = 0;
	
	public static String currentPageName = "";
	private String basePlaylistName = "";
	private String playbackPlaylistName = "";
	private String pendingSchedulePlaylistName = "";
	private long pendingScheduleSwitchAtMillis = -1L;
	private long currentPageDeadlineAtMillis = -1L;
	private long nextStageAllowedAtMillis = 0L;
	private PageRuntime activePageRuntime;
	private PageRuntime stagedPageRuntime;
	private PageRuntime specialPageRuntime;
	private PageRuntime pendingActivationRuntime;
	private PageBuildSpec stagedPageSpec;
	private PageBuildSpec specialPageSpec;
	private static final long NEXT_LAYOUT_STAGE_DELAY_MS = 1200L;
	private static final long POST_ACTIVATION_CLEANUP_DELAY_MS = 48L;
	private static final long SPECIAL_SCHEDULE_PRELOAD_LOOKAHEAD_MS = 60000L;
	private static final long SLOT_APPLY_FRAME_DELAY_MS = 16L;
	private static final long SLOT_PREPARE_FRAME_DELAY_MS = 16L;
	private static final long POST_SWITCH_TICK_SUPPRESS_MS = 1500L;
	private static final int PRECREATED_MEDIA_SLOT_COUNT = 4;
	private static final ExecutorService pageBuildExecutor = Executors.newSingleThreadExecutor();
	private boolean stageNextDeferredPending = false;
	private boolean usbPlaybackActive = false;
	private MediaView usbPlaybackView;
	private int pageBuildGeneration = 0;
	private long lastRuntimeActivationAtMillis = 0L;
	private String pendingStageBuildPlaylistName = "";
	private String pendingStageBuildPageName = "";
	private String pendingSpecialBuildPlaylistName = "";
	private String pendingSpecialBuildPageName = "";
	private final Runnable deferredStageNextRunnable = new Runnable() {
		@Override
		public void run() {
			stageNextDeferredPending = false;
			if (System.currentTimeMillis() < nextStageAllowedAtMillis) {
				scheduleDeferredStageNextPlayback(Math.max(0L, nextStageAllowedAtMillis - System.currentTimeMillis()));
				return;
			}
			stageNextPlaybackTarget();
		}
	};

	public List<String> usbflist = new ArrayList<>();
	public List<MediaDataModel> usbmedialist = new ArrayList<>();

	private static final class PageRuntime {
		String playlistName = "";
		String pageName = "";
		String runtimeName = "";
		PageDataModel pageData;
		RelativeLayout container;
		PageBuildSpec buildSpec;
		List<ElementDataModel> elements = new ArrayList<>();
		List<View> views = new ArrayList<>();
		List<PlaybackSlotView> slots = new ArrayList<>();
		int pendingPreparationCount = 0;
		boolean prepared = false;
		boolean prepareCancelled = false;
		boolean startInProgress = false;
		boolean activateWhenPrepared = false;
		int nextPageIndexAfterActivate = 0;
	}

	private static final class ElementBuildSpec {
		ElementDataModel element;
		AndoWSignageApp.ELEMENT_TYPE type;
		List<MediaDataModel> mediaContents = new ArrayList<>();
		List<ScrolltextDataModel> scrolltextContents = new ArrayList<>();
		WelcomeDataModel welcomeData;
	}

	private static final class PageBuildSpec {
		String playlistName = "";
		String pageName = "";
		PageDataModel pageData;
		List<ElementDataModel> elements = new ArrayList<>();
		List<ElementBuildSpec> elementSpecs = new ArrayList<>();

		boolean matches(PlaybackTarget target) {
			return target != null
					&& pageData != null
					&& !TextUtils.isEmpty(playlistName)
					&& playlistName.equals(target.playlistName)
					&& pageName.equals(target.pageData.getPageName());
		}
	}

	private static final class PlaybackTarget {
		String playlistName = "";
		PageDataModel pageData;
		int pageIndex = 0;
		boolean fromSchedule = false;

		boolean matches(PageRuntime runtime) {
			return runtime != null
					&& pageData != null
					&& !TextUtils.isEmpty(playlistName)
					&& playlistName.equals(runtime.playlistName)
					&& pageData.getPageName().equals(runtime.pageName);
		}
	}

	LightestTimer tickTimer = new LightestTimer(1000, new Runnable() {
		
		@Override
		public void run() {
			updateTimes();
			tickToAllViews();
		}
	});

	public void stopTick() { tickTimer.stop(); }
	public void startTick() {
		tickTimer.start();
	}

	int last_min = -1;
	private void updateTimes() {

		Date date_now = new Date();

		SimpleDateFormat _min = new SimpleDateFormat("m", Locale.ENGLISH);
		int min = Integer.parseInt(_min.format(date_now));

		if(min == last_min)
			return;

		last_min = min;}
	
	LightestTimer pageTimer = new LightestTimer(new Runnable() {
		
		@Override
		public void run() {
			popPage();
		}
	});
	
	Handler mPostRunHandler = new Handler();
	private static final String WATCHDOG_PACKAGE = "kr.co.turtlelab.startnow.watchdog";
	private static final String WATCHDOG_PING_ACTION = "kr.co.turtlelab.watchdog.ping";
	private static final int WATCHDOG_PING_INTERVAL_MS = 5000;
	private final Handler watchdogPingHandler = new Handler();
	private final Runnable watchdogPingRunnable = new Runnable() {
		@Override
		public void run() {
			sendWatchdogPing();
			watchdogPingHandler.postDelayed(this, WATCHDOG_PING_INTERVAL_MS);
		}
	};
	GifMovieView gifView;
		
	private float mLastMotionX = 0;
	private float mLastMotionY = 0;
	private int mTouchSlop;
	private boolean mHasPerformedLongPress;
	private CheckForLongPress mPendingCheckForLongPress;
	
	boolean needToChange = false;

	ProgressBar pbar;
	
    void initPB() {
		pbar = new ProgressBar(AndoWSignage.getCtx(), null, android.R.attr.progressBarStyleHorizontal);
		pbar.setProgressDrawable(AndoWSignage.getCtx().getResources().getDrawable(R.drawable.pbar_style));
    }
	
	// Long Click占쏙옙 처占쏙옙占쏙옙  Runnable 占쌉니댐옙. 
    class CheckForLongPress implements Runnable {
 
        public void run() {
            if (performLongClick()) {
                mHasPerformedLongPress = true;
            }
        }
    }
 
    private Handler mHandler = null;
    // Long Click 처占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙 占쌉쇽옙 
    private void postCheckForLongClick(int delayOffset) {
        mHasPerformedLongPress = false;
 
        if (mPendingCheckForLongPress == null) {
            mPendingCheckForLongPress = new CheckForLongPress();
        }
 
        mHandler.postDelayed(mPendingCheckForLongPress,
                ViewConfiguration.getLongPressTimeout() - delayOffset);
        // 占쏙옙占썩서  占시쏙옙占쏙옙占쏙옙  getLongPressTimeout() 占식울옙 message 占쏙옙占쏙옙占싹곤옙 占쌌니댐옙.  
        // 占쌩곤옙 delay占쏙옙 占십울옙占쏙옙 占쏙옙痢� 占쏙옙占쌔쇽옙  占식띰옙占쏙옙庫占� 占쏙옙占쏙옙占쏙옙占쏙옙占싹곤옙 占쌌니댐옙.
    }
    
 
    /**
     * Remove the longpress detection timer.
     * 占쌩곤옙占쏙옙 占쏙옙占쏙옙求占� 占쎈도占쌉니댐옙.
     */
    private void removeLongPressCallback() {
        if (mPendingCheckForLongPress != null) {
            mHandler.removeCallbacks(mPendingCheckForLongPress);
        }
    }
 
    public boolean performLongClick() {
		Intent configIntent = new Intent();
		configIntent.setAction("andowsignage.intent.action.CALL_SETTINGS");
		act.sendBroadcast(configIntent);
        return true;
    }
     
	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_andowsignage);

		Thread.setDefaultUncaughtExceptionHandler(new ExceptionHandler(this));
		
		initialize();
	}

	public MediaScanner mScanner;
	
	private void initialize() {
		act = AndoWSignage.this;
		sCtx = this;
		
		mScanner = new MediaScanner(this);
		
		AndoWSignageApp.version = this.getResources().getString(R.string.app_version);
		
		layout_root = (RelativeLayout) findViewById(R.id.layout_root);
		overlay_container = (RelativeLayout) findViewById(R.id.overlay_container);
		keyInputOverlay = (KeyCaptureEditText) findViewById(R.id.key_input_overlay);
		initDebugOverlay();
		initKeyInputOverlay();

		checkBakFile();
        
		playerData = PlayerDataProvider.getPlayerData();
		basePlaylistName = TextUtils.isEmpty(playerData.getPlaylist()) ? "" : playerData.getPlaylist();
		refreshSchedulePlaybackState(System.currentTimeMillis());
		
		String idStr = playerData.getPlayerName();
		
		AndoWSignageApp.MANAGER_IP = playerData.getManagerIP();
		
		AndoWSignageApp.PLAYER_ID = idStr;
		
		String manualIP = playerData.getPlayerIP();
		if(manualIP != null) {
			AndoWSignageApp.IS_MANUAL = manualIP.length()>0;
			AndoWSignageApp.MANUAL_IP = manualIP;
		}
		
		AndoWSignageApp.networkState = NetworkUtils.getConnectivityStatus(this);
//		AndoWSignageApp.AUTO_IP = NetworkUtils.getIPAddress(AndoWSignage.this);
		
		if (!TextUtils.isEmpty(playbackPlaylistName))
			syncCurrentPlaylistPages();

		boolean appliedReady = applyPendingReadyQueuesSync();
		if (appliedReady) {
			playerData = PlayerDataProvider.getPlayerData();
			basePlaylistName = TextUtils.isEmpty(playerData.getPlaylist()) ? "" : playerData.getPlaylist();
			refreshSchedulePlaybackState(System.currentTimeMillis());
		}
		if (!TextUtils.isEmpty(playbackPlaylistName)) {
			syncCurrentPlaylistPages();
		}
		
		final View decorView = getWindow().getDecorView();

		decorView.setOnSystemUiVisibilityChangeListener
		        (new View.OnSystemUiVisibilityChangeListener() {
		    @Override
		    public void onSystemUiVisibilityChange(int visibility) {
  		    	if(visibility == 0 || isConfigInputDialogShowing()) return;
		    	final int visiblityInt = visibility;
		        //if ((visibility & View.SYSTEM_UI_FLAG_FULLSCREEN) == 0) {
		    		mPostRunHandler.postDelayed(new Runnable()
		    		{
		    		  @Override     
		    		  public void run()
		    		  {
							if (isConfigInputDialogShowing()) {
								return;
							}
							updateAndRestart(true);
								
							if(visiblityInt > 0)  {
								return;
							}
							decorView.bringToFront();
							SystemUtils.setDimButtons(act, true);
							SystemUtils.systemBarVisibility(act, false);
		    		  }}, 500);
		        //} else {
		        //}
		    }
		});
		
		mHandler = new Handler(); 
        mTouchSlop = ViewConfiguration.get(this).getScaledTouchSlop();
		decorView.setOnTouchListener(new OnTouchListener() {
			@Override
			public boolean onTouch(View v, MotionEvent event) {
				int action = event.getAction();

				switch (action) {

				    case MotionEvent.ACTION_DOWN:
						mLastMotionX = event.getX();
						mLastMotionY = event.getY();
						mHasPerformedLongPress = false;
						postCheckForLongClick(0);
						break;
	
				    case MotionEvent.ACTION_MOVE:
					    final float x = event.getX();
					    final float y = event.getY();
					    final int deltaX = Math.abs((int) (mLastMotionX - x));
					    final int deltaY = Math.abs((int) (mLastMotionY - y));
					    if (deltaX >= mTouchSlop || deltaY >= mTouchSlop) {
						     if (!mHasPerformedLongPress) {
						    	 removeLongPressCallback();
						     }
					     }
					     break;
	
				    case MotionEvent.ACTION_CANCEL:
						if (!mHasPerformedLongPress) {
						 	removeLongPressCallback();
						}
						break;
	
				    case MotionEvent.ACTION_UP:
					    if (!mHasPerformedLongPress) {
					    	removeLongPressCallback();
					    }
					    break;
			   }
			   return true; // false;
			}
		});
		
		decorView.setOnLongClickListener(new OnLongClickListener() {
			
			@Override
			public boolean onLongClick(View v) {
				Intent configIntent = new Intent();
				configIntent.setAction("andowsignage.intent.action.CALL_SETTINGS");
				act.sendBroadcast(configIntent);
				return true;
			}
		});
		
        intentFilter = new IntentFilter();
        intentFilter.addAction(ACTION_CALL_SETTINGS);
        intentFilter.addAction(ACTION_STOP_AND_SLEEP);
        intentFilter.addAction(ACTION_REFRESH_CS);
        
		m_settingDlg = new ConfigDialog(act);
		m_settingDlg.setOnDismissListener(new DialogInterface.OnDismissListener() {
			
			@Override
			public void onDismiss(DialogInterface dialog) {
				requestKeyInputOverlayFocus();
			}
		});

		initPB();
	}

	public static Context getCtx() {
		return sCtx;
	}

	public static AndoWSignage getAct() {
		return act;
	}
	
	private void settingsForPlaying() {
		getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        WakeLocker.stanbyMode(this);
        SystemUtils.setDimButtons(act, true);
        SystemUtils.systemBarVisibility(act, false);
		requestKeyInputOverlayFocus();
	}
	
	private void releaseSettings() {
		getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
		WakeLocker.releaseCpuLock();
		SystemUtils.setDimButtons(act, false);
		SystemUtils.systemBarVisibility(act, true);
	}

//	public void setScreen(boolean turnon) {
////		WindowManager.LayoutParams params = getWindow().getAttributes();
//
//		if(turnon) {
////			getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
////			params.screenBrightness = 1;
//		}
//		else {
////			getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
////			params.screenBrightness = -1;
//
////			try{
////				Class c = Class.forName("android.os.PowerManager");
////				PowerManager mPowerManager = (PowerManager) this.getSystemService(Context.POWER_SERVICE);
////				for(Method m : c.getDeclaredMethods()){
////					if(m.getName().equals("goToSleep")){
////						m.setAccessible(true);
////						if(m.getParameterTypes().length == 1){
////							m.invoke(mPowerManager, SystemClock.uptimeMillis()-2);
////						}
////					}
////				}
////			} catch (Exception e){
////			}
//		}
//
////		getWindow().setAttributes(params);
//	}

	@Override
	protected void onResume() {
		super.onResume();

		setImageLoader();

		setOrientation();

		getPrefValues();

		settingsForPlaying();
		requestKeyInputOverlayFocus();

		registerRcv();
		enableWatchDog();
		startWatchdogPing();

		AndoWSignageApp.isRunning = true;
//		AndoWSignageApp.isSleeping = false;
		AndoWSignageApp.markPlayingState();

		startServices();
	}


	boolean mUpdateMgrSrvBounded;
	UpdateManagerService mUpdateMgrSrv;
	
	private void startServices() {
        AndoWSignageApp.clearShutdownInProgress();
        pendingHeartbeatServiceStop = false;
		startService(new Intent(AndoWSignage.this, ConfigLinkService.class));
		startService(new Intent(AndoWSignage.this, PowerService.class));
		startService(new Intent(AndoWSignage.this, UpdateManagerService.class));
		startService(new Intent(AndoWSignage.this, HeartbeatService.class));

		bindMgrServices();
	}

	boolean isClosing = false;
	private boolean manualStopRequested = false;
    private boolean pendingHeartbeatServiceStop = false;
	public void stopServices() {
		unbindMgrServices();
		stopService(new Intent(AndoWSignage.this, ConfigLinkService.class));

		if(AndoWSignageApp.isSlept == false) {
			stopService(new Intent(AndoWSignage.this, PowerService.class));
			stopService(new Intent(AndoWSignage.this, UpdateManagerService.class));
            if (!pendingHeartbeatServiceStop) {
			    stopService(new Intent(AndoWSignage.this, HeartbeatService.class));
            }
			return;
		}
	}
	
	public void restartNetworkSrvs() {
        AndoWSignageApp.clearShutdownInProgress();
        pendingHeartbeatServiceStop = false;
		stopService(new Intent(AndoWSignage.this, UpdateManagerService.class));
		startService(new Intent(AndoWSignage.this, UpdateManagerService.class));
		stopService(new Intent(AndoWSignage.this, HeartbeatService.class));
		startService(new Intent(AndoWSignage.this, HeartbeatService.class));
	}

	private void requestFinalHeartbeat() {
        long shutdownToken = AndoWSignageApp.beginShutdown();
        pendingHeartbeatServiceStop = true;
		Intent intent = new Intent(this, HeartbeatService.class);
		intent.setAction(HeartbeatService.ACTION_SEND_STOPPED);
        intent.putExtra(HeartbeatService.EXTRA_SHUTDOWN_TOKEN, shutdownToken);
		startService(intent);
	}
	
	void setImageLoader() {
		ImageLoaderConfiguration config = new ImageLoaderConfiguration.Builder(this)
												.threadPriority(Thread.NORM_PRIORITY - 2)
										//		.memoryCache(new WeakMemoryCache()) 
												.threadPoolSize(3)
												.diskCacheFileNameGenerator(new Md5FileNameGenerator())
												.denyCacheImageMultipleSizesInMemory()
												.tasksProcessingOrder(QueueProcessingType.LIFO)
												.build();
		ImageLoader.getInstance().init(config);
	}
	
//	LinearLayout orientationChanger;
//	WindowManager wm;
//	WindowManager.LayoutParams orientationLayout;
	
	void setOrientation() {
//		if(playerData.getIsLandscape()) {
//			setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE);
//		} else {
//			setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR_PORTRAIT);
//		}
		
		//		Settings.System.putInt(
		//	    getContentResolver(),
		//	    Settings.System.ACCELEROMETER_ROTATION,
		//	    0
		//	);
		//Settings.System.putInt(
		//	    getContentResolver(),
		//	    Settings.System.USER_ROTATION,
		//	    Surface.ROTATION_270 //Or a different ROTATION_ constant
		//	);
		
		//wm = (WindowManager) act.getSystemService(Service.WINDOW_SERVICE);
		//
		//orientationChanger = new LinearLayout(act);
		//orientationChanger.setClickable(false);
		//orientationChanger.setFocusable(false);
		//orientationChanger.setFocusableInTouchMode(false);
		//orientationChanger.setLongClickable(false);
		//
		//orientationLayout = new WindowManager.LayoutParams(
		//        LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT,
		//        1, WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
		//                | WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE,
		//        PixelFormat.RGBA_8888);
		//
		//wm.addView(orientationChanger, orientationLayout);
		//orientationChanger.setVisibility(View.GONE);
		//
		//orientationLayout.screenOrientation = ActivityInfo.SCREEN_ORIENTATION_REVERSE_PORTRAIT;
		//wm.updateViewLayout(orientationChanger, orientationLayout);
		//orientationChanger.setVisibility(View.VISIBLE);
	}

	private void getPrefValues() {
		LocalSettingsModel localSettings = LocalSettingsProvider.getLocalSettings().get(0);
		if(localSettings != null) {
			if(!TextUtils.isEmpty(localSettings.getPlayerId()))
				AndoWSignageApp.PLAYER_ID = localSettings.getPlayerId();
			if(!TextUtils.isEmpty(localSettings.getManagerIp()))
				AndoWSignageApp.MANAGER_IP = localSettings.getManagerIp();
			AndoWSignageApp.IS_MANUAL = localSettings.getManualIPState();
			AndoWSignageApp.SWITCH_ON_CONTENT_END = localSettings.getSwitchOnContentEnd();
			if(AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(localSettings.getManualIp())) {
				AndoWSignageApp.MANUAL_IP = localSettings.getManualIp();
			}
		}
		LocalSettingsProvider.applyStoredCommunicationSettings();
	}

	private void registerRcv() {
		
		if(mReceiver != null) 
			unRegistreRcv();

		if(mSystemMsgReceiver != null)
			unregisterSystemMsgReceiver();
		
		mReceiver = new BroadcastReceiver() {
 
			@Override
			public void onReceive(Context context, Intent intent) {
				
				String action = intent.getAction();
				
				if(action.equalsIgnoreCase(ACTION_STOP_AND_SLEEP)) {
					stopAndRemoveAllViews();
				} else if(action.equalsIgnoreCase(ACTION_CALL_SETTINGS)) {
					if(m_settingDlg != null) {
						if(m_settingDlg.isShowing()) return;
					}
					//showCustomDialog();
					releaseKeyInputOverlayFocus();
					m_settingDlg.show();
					SystemUtils.runOnUiThread(new Runnable() {
						@Override
						public void run() {
							//stopTimerAndElements();
							updateAndRestart(true);
						}
					});
				}
				else if(action.equalsIgnoreCase(ACTION_REFRESH_CS)) {
					SystemUtils.runOnUiThread(new Runnable() {
						@Override
						public void run() {
							updateAndRestart(false);
						}
					});
				}
			}
		};

		this.registerReceiver(mReceiver, intentFilter);
		registerSystemMsgReceiver();
	}
	
	private void unRegistreRcv() {
		try {
			this.unregisterReceiver(mReceiver);
		} catch(Exception e) { }
	}

	@SuppressLint({"SuspiciousIndentation", "UnspecifiedRegisterReceiverFlag"})
    private void registerSystemMsgReceiver() {
		 if(mSystemMsgReceiver != null) 
			unregisterSystemMsgReceiver();
		 
       mSystemMsgReceiver = new SystemMsgReceiver();
       IntentFilter filter = new IntentFilter();
       filter.addAction(Intent.ACTION_CLOSE_SYSTEM_DIALOGS);
       filter.addAction(Intent.ACTION_SCREEN_ON);
       filter.addAction(Intent.ACTION_SCREEN_OFF);

       registerReceiver(mSystemMsgReceiver, filter);
   }

   private void unregisterSystemMsgReceiver() {
	    try {
			this.unregisterReceiver(mSystemMsgReceiver);
		} catch(Exception e) { }
   }
 
//	public void reqPlaylistToManager() {
//
////		mPostRunHandler.postDelayed(new Runnable()
////		{
////		  @Override     
////		  public void run()
////		  {
//			  //rpc_client.rpCall(AndoWSignageApp.RP_CALL_TYPE.ReqCurrentPList);
//			  //Log.d(AndoWSignageApp.LOG, "ReqCurrentPList");
////		  }
////		}, 42);
//	}
	
	@Override
	protected void onPause() {
		super.onPause();
		
		releaseSettings();
		stopAllElement();
		stopWatchdogPing();
		if (manualStopRequested) {
			disableWatchDog();
		}
		
		releaseImageLoader();
		
		if(tickTimer != null)
			tickTimer.stop();
		
        if(pageTimer != null)
			pageTimer.stop();

        AndoWSignageApp.isRunning = false;
        if (manualStopRequested || AndoWSignageApp.isSlept || isFinishing()) {
            AndoWSignageApp.markStoppedState();
            requestFinalHeartbeat();
        }

//		if(!AndoWSignageApp.isForceStopped) {
			unRegistreRcv();
			unregisterSystemMsgReceiver();
//		}

		stopServices();
	}
	
	@Override
	protected void onStop() {
		super.onStop();
	}
		
	@Override
	protected void onDestroy() {
		stopWatchdogPing();
		super.onDestroy();
	}


	void restartAct() {
		Intent mIntent = new Intent(this, AndoWSignage.class);
		mIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
		mIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
		startActivity(mIntent);
	}
	
	void releaseImageLoader() {
		ImageLoader.getInstance().stop();
		ImageLoader.getInstance().destroy();
	}
	
	
	@Override
	public void onConfigurationChanged(Configuration newConfig) {
		super.onConfigurationChanged(newConfig);
		releaseSettings();
		SystemUtils.runOnUiThread(new Runnable() {
			@Override
			public void run() {
				updateAndRestart(true);
			}
		});
	}
	
	private void stopAllElement() {
		stopRuntime(activePageRuntime);
		stopRuntime(stagedPageRuntime);
		stopRuntime(specialPageRuntime);
		stopUsbPlayback();
	}
	
	public void updateAndRestart(boolean setOrientation) {
		queuedUpdateRestartPending = false;
		if(tickTimer != null)
			tickTimer.stop();
		
		if(pageTimer != null)
			pageTimer.stop();

		stopTimerAndElements();
		applyPendingReadyQueuesSync();
		
		playerData = PlayerDataProvider.getPlayerData();
		basePlaylistName = TextUtils.isEmpty(playerData.getPlaylist()) ? "" : playerData.getPlaylist();
		refreshSchedulePlaybackState(System.currentTimeMillis());
		syncCurrentPlaylistPages();
		settingsForPlaying();	

		if(setOrientation) setOrientation();
		updateScreenSize();

		tickTimer.start();
		pageIdx = 0;

		stopAnim();

		popPage();
		
//		AlarmUtils.setWeeklyAlarm(sCtx);
	}
		
    public void checkBakFile() { }

	public void GoFirstPage() {
		tickTimer.stop();
		pageTimer.stop();
		
		tickTimer.start();
		pageIdx = 0;
		
		SystemUtils.runOnUiThread(new Runnable() {
			@Override
			public void run() {
				popPage();
			}
		});
	}
	
	public void GoNextPage() {
		tickTimer.stop();
		pageTimer.stop();
		
		tickTimer.start();
		
		SystemUtils.runOnUiThread(new Runnable() {
			@Override
			public void run() {
				popPage();
			}
		});
	}
	
	public void stopAndRemoveAllViews() {
		stopTimerAndElements();
		stopAnim();
		hidePageContainers();
		clearPlaybackRuntimeState();
		pageIdx = 0;
	}
	
	public void stopForUpdate() {
		stopAndRemoveAllViews();
		showLoadingAnim();
	}
	
	public void stopTimerAndElements() {
		tickTimer.stop();
		pageTimer.stop();
		stopAllElement();
	}
	
	public void runTimerAndElements() {
		pageTimer.start();
		tickTimer.start();
		runAllElement();
	}
	
	public void showLoadingAnim() {
		gifView = new GifMovieView(sCtx);
		layout_params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
		layout_params.addRule(RelativeLayout.CENTER_IN_PARENT);
		layout_root.addView(gifView ,layout_params);
		
		gifView.setMovieResource(R.drawable.loading);
	}

	TurtleVideoView vv;
	public void showInitAnim() {
		if (vv != null) {
			if (!vv.isPlaying()) {
				try {
					vv.start();
				} catch (Exception ignored) {
				}
			}
			return;
		}
		if(gifView != null) {
			layout_root.removeView(gifView);
			gifView = null;
		}
		vv = new TurtleVideoView(sCtx);
		layout_params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
		layout_params.addRule(RelativeLayout.CENTER_IN_PARENT);
		layout_root.addView(vv ,layout_params);
		String path = "android.resource://" + getPackageName() + "/" + R.raw.intro;
		vv.setVideoURI(Uri.parse(path));
		vv.setLoop(true);
		vv.start();
	}
	
	public void addProgressbar() {
		layout_params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, 3);
		layout_params.addRule(RelativeLayout.ALIGN_PARENT_BOTTOM);
		pbar.setProgress(0);
		layout_root.addView(pbar ,layout_params);
	}
	
	public void updateprogress(int progress) {
		pbar.setProgress(progress);
		AndoWSignageApp.process = ""+progress;
	}

	public void removeProgressbar() {
		layout_root.removeView(pbar);
		AndoWSignageApp.process = "0";
	}
	
	public void stopAnim() {
		if(gifView != null)
			layout_root.removeView(gifView);
		if(vv != null) {
			vv.stopPlayback();
			layout_root.removeView(vv);
		}
		gifView = null;
		vv = null;
//		layout_root.setBackgroundColor(Color.parseColor("#000000"));
	}

	void updateScreenSize() {
		View decorView = act.getWindow().getDecorView();
		AndoWSignageApp.updateDisplaySize(decorView.getWidth(), decorView.getHeight());

		stopAnim();
		hidePageContainers();
		clearPlaybackRuntimeState();
		ensurePageContainersAttached();
    }

	private void hidePageContainers() {
		hideContainerImmediately(activePageContainer);
		hideContainerImmediately(stagedPageContainer);
		hideContainerImmediately(specialPageContainer);
	}

	private void ensureUsbPlaybackViewAttached() {
		if (layout_root == null) {
			return;
		}
		if (usbPlaybackView == null) {
			usbPlaybackView = new MediaView(act, sCtx, AndoWSignageApp.getDeviceWidth(), AndoWSignageApp.getDeviceHeight(), new ArrayList<MediaDataModel>());
			RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
					LayoutParams.MATCH_PARENT,
					LayoutParams.MATCH_PARENT);
			layout_root.addView(usbPlaybackView, params);
		} else if (usbPlaybackView.getParent() != layout_root) {
			RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
					LayoutParams.MATCH_PARENT,
					LayoutParams.MATCH_PARENT);
			layout_root.addView(usbPlaybackView, params);
		}
		hideUsbPlaybackView();
	}

	private void showUsbPlaybackView() {
		if (usbPlaybackView == null) {
			return;
		}
		usbPlaybackView.setVisibility(View.VISIBLE);
		usbPlaybackView.setTranslationX(0f);
		usbPlaybackView.setTranslationY(0f);
		usbPlaybackView.setAlpha(1f);
		usbPlaybackView.bringToFront();
	}

	private void hideUsbPlaybackView() {
		if (usbPlaybackView == null) {
			return;
		}
		usbPlaybackView.stopPlaylist();
		usbPlaybackView.setVisibility(View.GONE);
		usbPlaybackView.setTranslationX(getOffscreenTranslationX());
		usbPlaybackView.setTranslationY(0f);
		usbPlaybackView.setAlpha(1f);
	}

	private void stopUsbPlayback() {
		if (usbPlaybackView != null) {
			usbPlaybackView.stopPlaylist();
		}
	}

	private void resetUsbPlaybackMode(boolean releaseContents) {
		usbPlaybackActive = false;
		if (usbPlaybackView != null) {
			usbPlaybackView.stopPlaylist();
			if (releaseContents) {
				usbPlaybackView.releaseMediaContents();
			}
			hideUsbPlaybackView();
		}
		elementViewList.clear();
	}

	private void ensurePageContainersAttached() {
		if (layout_root == null) {
			return;
		}
		layout_root.setClipChildren(true);
		layout_root.setClipToPadding(true);
		if (activePageContainer == null) {
			activePageContainer = createPageContainer("active");
		}
		if (stagedPageContainer == null) {
			stagedPageContainer = createPageContainer("normal");
		}
		if (specialPageContainer == null) {
			specialPageContainer = createPageContainer("special");
		}
		RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
				LayoutParams.MATCH_PARENT,
				LayoutParams.MATCH_PARENT);
		if (activePageContainer.getParent() != layout_root) {
			layout_root.addView(activePageContainer, params);
		}
		if (stagedPageContainer.getParent() != layout_root) {
			layout_root.addView(stagedPageContainer, params);
		}
		if (specialPageContainer.getParent() != layout_root) {
			layout_root.addView(specialPageContainer, params);
		}
		activePageContainer.setVisibility(View.VISIBLE);
		stagedPageContainer.setVisibility(View.VISIBLE);
		specialPageContainer.setVisibility(View.VISIBLE);
		moveContainerOnScreen(activePageContainer);
		moveContainerOffScreen(stagedPageContainer);
		moveContainerOffScreen(specialPageContainer);
		activePageContainer.bringToFront();
	}

	private float getOffscreenTranslationX() {
		int width = layout_root != null ? layout_root.getWidth() : 0;
		if (width <= 0) {
			width = AndoWSignageApp.getDeviceWidth();
		}
		if (width <= 0) {
			width = 1920;
		}
		return width + 32f;
	}

	private void moveContainerOnScreen(RelativeLayout container) {
		if (container == null) {
			return;
		}
		container.setTranslationX(0f);
		container.setTranslationY(0f);
		container.setAlpha(1f);
	}

	private void moveContainerOffScreen(RelativeLayout container) {
		if (container == null) {
			return;
		}
		container.setTranslationX(getOffscreenTranslationX());
		container.setTranslationY(0f);
		container.setAlpha(1f);
	}

	private void hideContainerImmediately(RelativeLayout container) {
		if (container == null) {
			return;
		}
		container.setVisibility(View.INVISIBLE);
		moveContainerOffScreen(container);
	}

	private void prepareContainerForDeferredLoad(RelativeLayout container) {
		if (container == null) {
			return;
		}
		container.setVisibility(View.VISIBLE);
		moveContainerOffScreen(container);
	}

	private RelativeLayout createPageContainer(String runtimeName) {
		RelativeLayout container = new RelativeLayout(this);
		container.setLayoutParams(new RelativeLayout.LayoutParams(
				LayoutParams.MATCH_PARENT,
				LayoutParams.MATCH_PARENT));
		container.setBackgroundColor(Color.BLACK);
		container.setTag(runtimeName);
		prepareContainerSlots(container);
		return container;
	}

	private void prepareContainerSlots(RelativeLayout container) {
		if (container == null || container.getChildCount() > 0) {
			return;
		}
		for (int index = 0; index < PRECREATED_MEDIA_SLOT_COUNT; index++) {
			PlaybackSlotView slotView = new PlaybackSlotView(act, this);
			slotView.setVisibility(View.INVISIBLE);
			container.addView(slotView, new RelativeLayout.LayoutParams(1, 1));
		}
	}

	private void clearPlaybackRuntimeState() {
		cancelDeferredStageNextPlayback();
		pageBuildGeneration++;
		pendingStageBuildPlaylistName = "";
		pendingStageBuildPageName = "";
		pendingSpecialBuildPlaylistName = "";
		pendingSpecialBuildPageName = "";
		clearRuntime(activePageRuntime);
		clearRuntime(stagedPageRuntime);
		clearRuntime(specialPageRuntime);
		activePageRuntime = null;
		stagedPageRuntime = null;
		specialPageRuntime = null;
		stagedPageSpec = null;
		specialPageSpec = null;
		playlistPageCache.clear();
		resetUsbPlaybackMode(true);
		elementDataList.clear();
		currentPageName = "";
		currentPageDeadlineAtMillis = -1L;
		pendingActivationRuntime = null;
		nextStageAllowedAtMillis = 0L;
	}

	private void updateLayoutScales(List<ElementDataModel> targetElements, PageDataModel currentPage) {
		float[] scales = currentPage == null
				? AndoWSignageApp.getScaleFactorsForCanvas(1920, 1080)
				: AndoWSignageApp.getScaleFactorsForCanvas(currentPage.getCanvasWidth(), currentPage.getCanvasHeight());
		for (ElementDataModel elementValue : targetElements) {
			elementValue.updateScales(scales[0], scales[1], scales[2]);
		}
	}

	private PageDataModel findCurrentPageData() {
		if (activePageRuntime != null && activePageRuntime.pageData != null) {
			return activePageRuntime.pageData;
		}
		if (currentPageName == null || currentPageName.isEmpty()) {
			if (pageDataList == null || pageDataList.isEmpty() || pageIdx < 0 || pageIdx >= pageDataList.size()) {
				return null;
			}
			return pageDataList.get(pageIdx);
		}
		for (PageDataModel page : pageDataList) {
			if (page != null && currentPageName.equals(page.getPageName())) {
				return page;
			}
		}
		return null;
	}

	private boolean hasActiveContentPlaybackForHeartbeatInternal() {
		if (!AndoWSignageApp.isRunning || AndoWSignageApp.isSlept) {
			return false;
		}

		if (usbPlaybackActive && usbPlaybackView != null && usbPlaybackView.isPlaybackActiveForHeartbeat()) {
			return true;
		}

		if (activePageRuntime != null) {
			for (PlaybackSlotView slotView : activePageRuntime.slots) {
				if (slotView != null && slotView.isPlaybackActiveForHeartbeat()) {
					return true;
				}
			}

			if (activePageRuntime.container != null
					&& activePageRuntime.container.getVisibility() == View.VISIBLE
					&& !TextUtils.isEmpty(activePageRuntime.pageName)) {
				return true;
			}
		}

		return !TextUtils.isEmpty(currentPageName);
	}

	public static boolean hasActiveContentPlaybackForHeartbeat() {
		if (act != null) {
			return act.hasActiveContentPlaybackForHeartbeatInternal();
		}
		return AndoWSignageApp.isRunning
				&& !AndoWSignageApp.isSlept
				&& !TextUtils.isEmpty(currentPageName);
	}
	
	private PageRuntime buildPageRuntime(RelativeLayout container, PlaybackTarget target) {
		PageBuildSpec spec = buildPageSpec(target);
		return buildPageRuntime(container, spec);
    }

	private PageRuntime buildPageRuntime(RelativeLayout container, PageBuildSpec spec) {
		if (container == null || spec == null || spec.pageData == null) {
			return null;
		}
		PageRuntime runtime = new PageRuntime();
		runtime.runtimeName = String.valueOf(container.getTag());
		runtime.container = container;
		runtime.pageData = copyPageData(spec.pageData);
		runtime.playlistName = spec.playlistName;
		runtime.pageName = runtime.pageData.getPageName();
		runtime.buildSpec = spec;
		runtime.elements = new ArrayList<>(spec.elements);
		prepareContainerSlots(runtime.container);
		runtime.slots = collectContainerSlots(runtime.container);
		runtime.views.clear();
		runtime.views.addAll(runtime.slots);
		runtime.container.setVisibility(View.VISIBLE);
		moveContainerOffScreen(runtime.container);
		scheduleRuntimeViewBuild(runtime);
		return runtime;
    }

	private List<PlaybackSlotView> collectContainerSlots(RelativeLayout container) {
		List<PlaybackSlotView> slots = new ArrayList<>();
		if (container == null) {
			return slots;
		}
		for (int index = 0; index < container.getChildCount(); index++) {
			View child = container.getChildAt(index);
			if (child instanceof PlaybackSlotView) {
				slots.add((PlaybackSlotView) child);
			}
		}
		return slots;
	}

	private void scheduleRuntimeViewBuild(final PageRuntime runtime) {
		if (runtime == null || runtime.container == null || runtime.buildSpec == null) {
			return;
		}
		applyRuntimeSlotBuildStep(runtime, 0);
	}

	private void applyRuntimeSlotBuildStep(final PageRuntime runtime, final int slotIndex) {
		if (runtime == null || runtime.container == null || runtime.buildSpec == null || runtime.prepareCancelled) {
			return;
		}
		List<ElementBuildSpec> specs = runtime.buildSpec.elementSpecs;
		if (slotIndex >= runtime.slots.size()) {
			runtime.buildSpec = null;
			scheduleRuntimePrepare(runtime);
			return;
		}
		PlaybackSlotView slotView = runtime.slots.get(slotIndex);
		if (slotIndex < specs.size()) {
			applyPreparedElement(slotView, specs.get(slotIndex));
		} else {
			slotView.deactivateSlot();
		}
		mPostRunHandler.postDelayed(new Runnable() {
			@Override
			public void run() {
				applyRuntimeSlotBuildStep(runtime, slotIndex + 1);
			}
		}, SLOT_APPLY_FRAME_DELAY_MS);
	}

	private PageBuildSpec buildPageSpec(PlaybackTarget target) {
		if (target == null || target.pageData == null) {
			return null;
		}
		PageBuildSpec spec = new PageBuildSpec();
		spec.playlistName = target.playlistName;
		spec.pageData = copyPageData(target.pageData);
		spec.pageName = spec.pageData.getPageName();
		spec.elements = ElementDataProvider.getPageElementList(spec.pageData.getGUID());
		updateLayoutScales(spec.elements, spec.pageData);
		for (ElementDataModel edm : spec.elements) {
			ElementBuildSpec elementSpec = buildElementSpec(spec.pageData.getGUID(), edm);
			if (elementSpec != null) {
				spec.elementSpecs.add(elementSpec);
			}
		}
		return spec;
	}

	private ElementBuildSpec buildElementSpec(String pageId, ElementDataModel edm) {
		if (edm == null || TextUtils.isEmpty(edm.getType())) {
			return null;
		}
		ElementBuildSpec spec = new ElementBuildSpec();
		spec.element = edm;
		try {
			spec.type = AndoWSignageApp.ELEMENT_TYPE.valueOf(edm.getType());
		} catch (Exception ignored) {
			return null;
		}
		switch (spec.type) {
			case Media:
			case TemplateBoard:
				spec.mediaContents = MediaDataProvider.getContentList(pageId, edm.getName());
				break;
			case ScrollText:
				spec.scrolltextContents = ScrolltextDataProvider.getContentList(pageId, edm.getName());
				break;
			case WelcomeBoard:
				spec.welcomeData = WelcomeDataProvider.getContent(pageId, edm.getName());
				break;
			default:
				break;
		}
		return spec;
	}

	private PageDataModel copyPageData(PageDataModel source) {
		if (source == null) {
			return null;
		}
		PageDataModel copy = new PageDataModel();
		copy.setPageName(source.getPageName());
		String[] playTime = source.getPlayTime();
		copy.setPlayTime(playTime[0], playTime[1], playTime[2]);
		copy.setVolume(String.valueOf(source.getPlayVolume()));
		copy.setGUID(source.getGUID());
		copy.setLandscape(source.isLandscape());
		copy.setCanvasSize(source.getCanvasWidth(), source.getCanvasHeight());
		return copy;
	}

	private PlaybackTarget copyPlaybackTarget(PlaybackTarget source) {
		if (source == null || source.pageData == null) {
			return null;
		}
		PlaybackTarget copy = new PlaybackTarget();
		copy.playlistName = source.playlistName;
		copy.pageIndex = source.pageIndex;
		copy.fromSchedule = source.fromSchedule;
		copy.pageData = copyPageData(source.pageData);
		return copy;
	}

	private void scheduleRuntimePrepare(final PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		runtime.prepareCancelled = false;
		runtime.prepared = false;
		List<PlaybackSlotView> mediaSlots = new ArrayList<>();
		for (PlaybackSlotView slotView : runtime.slots) {
			if (slotView.isMediaSlot()) {
				mediaSlots.add(slotView);
			}
		}
		runtime.pendingPreparationCount = mediaSlots.size();
		if (mediaSlots.isEmpty()) {
			runtime.prepared = true;
			onRuntimePrepared(runtime);
			return;
		}
		prepareRuntimeSlotSequentially(runtime, mediaSlots, 0);
	}

	private void prepareRuntimeSlotSequentially(final PageRuntime runtime, final List<PlaybackSlotView> mediaSlots, final int slotIndex) {
		if (runtime == null || runtime.prepareCancelled || mediaSlots == null) {
			return;
		}
		if (slotIndex >= mediaSlots.size()) {
			return;
		}
		final PlaybackSlotView slotView = mediaSlots.get(slotIndex);
		slotView.prepareInitialContent(new PlaybackSlotView.SlotPreparedCallback() {
			@Override
			public void onPrepared(PlaybackSlotView view) {
				onRuntimeViewPrepared(runtime);
				if (runtime.prepareCancelled) {
					return;
				}
				if (slotIndex + 1 < mediaSlots.size()) {
					mPostRunHandler.postDelayed(new Runnable() {
						@Override
						public void run() {
							prepareRuntimeSlotSequentially(runtime, mediaSlots, slotIndex + 1);
						}
					}, SLOT_PREPARE_FRAME_DELAY_MS);
				}
			}
		});
	}

	private void onRuntimeViewPrepared(PageRuntime runtime) {
		if (runtime == null || runtime.prepareCancelled) {
			return;
		}
		runtime.pendingPreparationCount = Math.max(0, runtime.pendingPreparationCount - 1);
		if (runtime.pendingPreparationCount == 0) {
			runtime.prepared = true;
			onRuntimePrepared(runtime);
		}
	}

	private void onRuntimePrepared(PageRuntime runtime) {
		if (runtime == null || runtime.prepareCancelled) {
			return;
		}
		if (runtime.activateWhenPrepared) {
			if (pendingActivationRuntime == runtime) {
				tryActivatePendingRuntime();
			} else {
				runtime.activateWhenPrepared = false;
				stopAnim();
				beginRuntimeActivation(runtime);
			}
		}
	}

	
	private void applyPreparedElement(PlaybackSlotView slotView, ElementBuildSpec elementSpec) {
		if (slotView == null || elementSpec == null || elementSpec.element == null || elementSpec.type == null) {
			return;
		}
		ElementDataModel edm = elementSpec.element;
		AndoWSignageApp.ELEMENT_TYPE type = elementSpec.type;

		switch (type) {
			case Media:
				slotView.configureMediaSlot(edm, elementSpec.mediaContents);
				break;
				
			case ScrollText:
				slotView.configureScrollSlot(edm, elementSpec.scrolltextContents);
				break;
				
			case WelcomeBoard:
				slotView.configureWelcomeSlot(edm, elementSpec.welcomeData);
				break;
			
			case TemplateBoard:
				slotView.configureTemplateSlot(edm, elementSpec.mediaContents);
				break;
				
			default:
				slotView.deactivateSlot();
				break;
		}
	}

	private PlaybackTarget createActivePageTarget() {
		if (pageDataList == null || pageDataList.isEmpty()) {
			return null;
		}
		int safeIndex = getSafePageIndex(pageIdx);
		PlaybackTarget target = new PlaybackTarget();
		target.playlistName = playbackPlaylistName;
		target.pageIndex = safeIndex;
		target.pageData = pageDataList.get(safeIndex);
		return target;
	}

	private PlaybackTarget createNextPageTarget() {
		if (pageDataList == null || pageDataList.size() <= 1) {
			return null;
		}
		int safeIndex = getSafePageIndex(pageIdx);
		PlaybackTarget target = new PlaybackTarget();
		target.playlistName = playbackPlaylistName;
		target.pageIndex = safeIndex;
		target.pageData = pageDataList.get(safeIndex);
		return target;
	}

	private PlaybackTarget createScheduledTarget() {
		if (TextUtils.isEmpty(pendingSchedulePlaylistName)) {
			return null;
		}
		List<PageDataModel> scheduledPages = getOrLoadPageList(pendingSchedulePlaylistName);
		if (scheduledPages == null || scheduledPages.isEmpty()) {
			return null;
		}
		PlaybackTarget target = new PlaybackTarget();
		target.playlistName = pendingSchedulePlaylistName;
		target.pageIndex = 0;
		target.pageData = scheduledPages.get(0);
		target.fromSchedule = true;
		return target;
	}

	private int getSafePageIndex(int requestedIndex) {
		if (pageDataList == null || pageDataList.isEmpty()) {
			return 0;
		}
		if (requestedIndex < 0) {
			return 0;
		}
		if (requestedIndex >= pageDataList.size()) {
			return 0;
		}
		return requestedIndex;
	}

	private List<PageDataModel> getOrLoadPageList(String playlistName) {
		if (TextUtils.isEmpty(playlistName)) {
			return new ArrayList<>();
		}
		List<PageDataModel> cached = playlistPageCache.get(playlistName);
		if (cached != null) {
			return cached;
		}
		List<PageDataModel> loaded = PlaylistDataProvider.getPageList(playlistName);
		if (loaded == null) {
			loaded = new ArrayList<>();
		}
		playlistPageCache.put(playlistName, loaded);
		return loaded;
	}

	private void invalidatePageListCache(String playlistName) {
		if (TextUtils.isEmpty(playlistName)) {
			return;
		}
		playlistPageCache.remove(playlistName);
	}

	private void syncCurrentPlaylistPages() {
		pageDataList = getOrLoadPageList(playbackPlaylistName);
	}

	private void syncActiveViews(PageRuntime runtime) {
		elementViewList.clear();
		elementDataList.clear();
		if (runtime == null) {
			return;
		}
		elementViewList.addAll(runtime.views);
		elementDataList.addAll(runtime.elements);
		currentPageName = runtime.pageName;
	}

	private void startRuntime(PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		for (PlaybackSlotView slotView : runtime.slots) {
			slotView.showPreparedContent();
		}
	}

	private void startRuntimePlayback(final PageRuntime runtime) {
		if (runtime == null || !runtime.startInProgress) {
			return;
		}
		for (PlaybackSlotView slotView : runtime.slots) {
			slotView.startPreparedPlayback();
		}
	}

	private void stopRuntime(PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		for (PlaybackSlotView slotView : runtime.slots) {
			slotView.stopPlayback();
		}
	}

	private void pauseRuntime(PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		for (PlaybackSlotView slotView : runtime.slots) {
			slotView.pausePlayback();
		}
	}

	private void clearRuntime(PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		clearPendingActivation(runtime);
		runtime.prepareCancelled = true;
		stopRuntime(runtime);
		if (runtime.container != null) {
			runtime.container.setVisibility(View.VISIBLE);
			moveContainerOffScreen(runtime.container);
		}
		for (PlaybackSlotView slotView : runtime.slots) {
			slotView.releaseSlot();
		}
		runtime.elements.clear();
		runtime.pendingPreparationCount = 0;
		runtime.prepared = false;
		runtime.buildSpec = null;
		runtime.startInProgress = false;
		runtime.activateWhenPrepared = false;
	}

	private void retireRuntimeForReuse(PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		clearPendingActivation(runtime);
		runtime.prepareCancelled = true;
		runtime.pendingPreparationCount = 0;
		runtime.prepared = false;
		runtime.startInProgress = false;
		runtime.activateWhenPrepared = false;
		if (runtime.container != null) {
			runtime.container.setVisibility(View.VISIBLE);
			moveContainerOffScreen(runtime.container);
		}
		pauseRuntime(runtime);
	}

	private void beginRuntimeActivation(PageRuntime nextRuntime) {
		if (nextRuntime == null || nextRuntime.startInProgress) {
			return;
		}
		nextRuntime.startInProgress = true;
		startRuntime(nextRuntime);
		completeRuntimeActivation(nextRuntime);
	}

	private void completeRuntimeActivation(PageRuntime nextRuntime) {
		if (nextRuntime == null) {
			return;
		}
		clearPendingActivation(nextRuntime);
		PageRuntime previousRuntime = activePageRuntime;
		if (nextRuntime == stagedPageRuntime) {
			RelativeLayout previousContainer = activePageContainer;
			activePageContainer = stagedPageContainer;
			stagedPageContainer = previousContainer;
			stagedPageRuntime = null;
		} else if (nextRuntime == specialPageRuntime) {
			RelativeLayout previousContainer = activePageContainer;
			activePageContainer = specialPageContainer;
			specialPageContainer = previousContainer;
			specialPageRuntime = null;
		}
		nextRuntime.container.setVisibility(View.VISIBLE);
		moveContainerOnScreen(nextRuntime.container);
		nextRuntime.container.bringToFront();
		activePageRuntime = nextRuntime;
		lastRuntimeActivationAtMillis = System.currentTimeMillis();
		syncActiveViews(activePageRuntime);
		pageIdx = nextRuntime.nextPageIndexAfterActivate;
		configurePageTimers(nextRuntime.pageData);
		if (previousRuntime == null || previousRuntime == nextRuntime) {
			startRuntimePlayback(nextRuntime);
			nextStageAllowedAtMillis = System.currentTimeMillis() + NEXT_LAYOUT_STAGE_DELAY_MS;
			scheduleDeferredStageNextPlayback(NEXT_LAYOUT_STAGE_DELAY_MS);
		} else {
			startRuntimePlayback(nextRuntime);
			hideContainerImmediately(previousRuntime.container);
			pauseRuntime(previousRuntime);
			schedulePreviousRuntimeCleanup(previousRuntime, new Runnable() {
				@Override
				public void run() {
					nextStageAllowedAtMillis = System.currentTimeMillis() + NEXT_LAYOUT_STAGE_DELAY_MS;
					scheduleDeferredStageNextPlayback(NEXT_LAYOUT_STAGE_DELAY_MS);
				}
			});
		}
		nextRuntime.startInProgress = false;
	}

	private void schedulePreviousRuntimeCleanup(final PageRuntime previousRuntime, final Runnable onCleanupComplete) {
		if (previousRuntime == null) {
			if (onCleanupComplete != null) {
				onCleanupComplete.run();
			}
			return;
		}
		mPostRunHandler.postDelayed(new Runnable() {
			@Override
			public void run() {
				if (previousRuntime.container != null) {
					previousRuntime.container.setVisibility(View.VISIBLE);
					moveContainerOffScreen(previousRuntime.container);
				}
				previousRuntime.prepared = false;
				previousRuntime.startInProgress = false;
				previousRuntime.activateWhenPrepared = false;
				previousRuntime.prepareCancelled = true;
				previousRuntime.pendingPreparationCount = 0;
				if (onCleanupComplete != null) {
					onCleanupComplete.run();
				}
			}
		}, POST_ACTIVATION_CLEANUP_DELAY_MS);
	}

	private void cancelDeferredStageNextPlayback() {
		stageNextDeferredPending = false;
		mPostRunHandler.removeCallbacks(deferredStageNextRunnable);
	}

	private void scheduleDeferredStageNextPlayback(long delayMs) {
		cancelDeferredStageNextPlayback();
		stageNextDeferredPending = true;
		mPostRunHandler.postDelayed(deferredStageNextRunnable, Math.max(0L, delayMs));
	}

	private void requestRuntimeActivation(PageRuntime runtime, int nextPageIndex) {
		if (runtime == null) {
			return;
		}
		runtime.nextPageIndexAfterActivate = nextPageIndex;
		if (runtime == activePageRuntime) {
			clearPendingActivation(runtime);
			return;
		}
		if (shouldDelayRuntimeActivation(runtime)) {
			markPendingActivation(runtime);
			tryActivatePendingRuntime();
			return;
		}
		clearPendingActivation(runtime);
		if (runtime.prepared) {
			runtime.activateWhenPrepared = false;
			stopAnim();
			beginRuntimeActivation(runtime);
			return;
		}
		runtime.activateWhenPrepared = true;
	}

	private void configurePageTimers(PageDataModel pdm) {
		if (pdm == null) {
			return;
		}
		long interval = Math.max(1L, pdm.getPlayTimeSec()) * 1000L;
		pageTimer.changeInterval(interval);
		currentPageDeadlineAtMillis = System.currentTimeMillis() + interval;
		tickTimer.changeInterval(1000);
		if(!pageTimer.getIsTicking()) {
			pageTimer.start();
		}
	}

	private boolean shouldDelayRuntimeActivation(PageRuntime runtime) {
		return runtime != null
				&& activePageRuntime != null
				&& activePageRuntime != runtime
				&& shouldDelayActiveRuntimeSwitch();
	}

	private boolean shouldDelayActiveRuntimeSwitch() {
		if (activePageRuntime == null) {
			return false;
		}
		for (PlaybackSlotView slotView : activePageRuntime.slots) {
			if (slotView != null && slotView.shouldDelayLayoutTransition()) {
				return true;
			}
		}
		return false;
	}

	private void markPendingActivation(PageRuntime runtime) {
		if (runtime == null) {
			return;
		}
		if (pendingActivationRuntime != runtime) {
			if (pendingActivationRuntime != null) {
				pendingActivationRuntime.activateWhenPrepared = false;
			}
			pendingActivationRuntime = runtime;
		}
		runtime.activateWhenPrepared = true;
		pageTimer.stop();
	}

	private void clearPendingActivation(PageRuntime runtime) {
		if (runtime != null && pendingActivationRuntime != runtime) {
			return;
		}
		if (pendingActivationRuntime != null) {
			pendingActivationRuntime.activateWhenPrepared = false;
		}
		pendingActivationRuntime = null;
	}

	private boolean tryActivatePendingRuntime() {
		PageRuntime runtime = pendingActivationRuntime;
		if (runtime == null) {
			return false;
		}
		if (runtime == activePageRuntime) {
			clearPendingActivation(runtime);
			return false;
		}
		if (!runtime.prepared) {
			runtime.activateWhenPrepared = true;
			return false;
		}
		if (shouldDelayActiveRuntimeSwitch()) {
			runtime.activateWhenPrepared = true;
			return false;
		}
		runtime.activateWhenPrepared = false;
		clearPendingActivation(runtime);
		stopAnim();
		beginRuntimeActivation(runtime);
		return true;
	}

	private void stageNextPlaybackTarget() {
		ensurePageContainersAttached();
		PlaybackTarget target = createNextPageTarget();
		if (target == null) {
			retireRuntimeForReuse(stagedPageRuntime);
			stagedPageRuntime = null;
			stagedPageSpec = null;
			pendingStageBuildPlaylistName = "";
			pendingStageBuildPageName = "";
			return;
		}
		if (target.matches(stagedPageRuntime) || matchesPageBuildSpec(stagedPageSpec, target) || isPendingStageBuild(target)) {
			return;
		}
		prepareContainerForDeferredLoad(stagedPageContainer);
		retireRuntimeForReuse(stagedPageRuntime);
		stagedPageRuntime = null;
		stagedPageSpec = null;
		requestPageBuildSpec(target, false);
	}

	private void stageSpecialPlaybackTarget() {
		ensurePageContainersAttached();
		long now = System.currentTimeMillis();
		boolean shouldPrepareSpecial = !TextUtils.isEmpty(pendingSchedulePlaylistName)
				&& pendingScheduleSwitchAtMillis > 0
				&& pendingScheduleSwitchAtMillis <= now + SPECIAL_SCHEDULE_PRELOAD_LOOKAHEAD_MS;
		if (!shouldPrepareSpecial) {
			retireRuntimeForReuse(specialPageRuntime);
			specialPageRuntime = null;
			specialPageSpec = null;
			pendingSpecialBuildPlaylistName = "";
			pendingSpecialBuildPageName = "";
			return;
		}
		PlaybackTarget target = createScheduledTarget();
		if (target == null) {
			return;
		}
		if (target.matches(specialPageRuntime) || matchesPageBuildSpec(specialPageSpec, target) || isPendingSpecialBuild(target)) {
			return;
		}
		prepareContainerForDeferredLoad(specialPageContainer);
		retireRuntimeForReuse(specialPageRuntime);
		specialPageRuntime = null;
		specialPageSpec = null;
		requestPageBuildSpec(target, true);
	}

	private boolean matchesPageBuildSpec(PageBuildSpec spec, PlaybackTarget target) {
		return spec != null && spec.matches(target);
	}

	private boolean isPendingStageBuild(PlaybackTarget target) {
		return target != null
				&& !TextUtils.isEmpty(pendingStageBuildPlaylistName)
				&& pendingStageBuildPlaylistName.equals(target.playlistName)
				&& pendingStageBuildPageName.equals(target.pageData.getPageName());
	}

	private boolean isPendingSpecialBuild(PlaybackTarget target) {
		return target != null
				&& !TextUtils.isEmpty(pendingSpecialBuildPlaylistName)
				&& pendingSpecialBuildPlaylistName.equals(target.playlistName)
				&& pendingSpecialBuildPageName.equals(target.pageData.getPageName());
	}

	private void requestPageBuildSpec(PlaybackTarget target, final boolean specialTarget) {
		final PlaybackTarget requestTarget = copyPlaybackTarget(target);
		if (requestTarget == null || requestTarget.pageData == null) {
			return;
		}
		final int generation = pageBuildGeneration;
		if (specialTarget) {
			pendingSpecialBuildPlaylistName = requestTarget.playlistName;
			pendingSpecialBuildPageName = requestTarget.pageData.getPageName();
		} else {
			pendingStageBuildPlaylistName = requestTarget.playlistName;
			pendingStageBuildPageName = requestTarget.pageData.getPageName();
		}
		pageBuildExecutor.execute(new Runnable() {
			@Override
			public void run() {
				invalidatePageListCache(requestTarget.playlistName);
				List<PageDataModel> warmedPages = PlaylistDataProvider.getPageList(requestTarget.playlistName);
				if (warmedPages == null) {
					warmedPages = new ArrayList<>();
				}
				final PageBuildSpec spec = buildPageSpec(requestTarget);
				final List<PageDataModel> finalWarmedPages = warmedPages;
				SystemUtils.runOnUiThread(new Runnable() {
					@Override
					public void run() {
						if (generation != pageBuildGeneration) {
							return;
						}
						if (specialTarget ? !isPendingSpecialBuild(requestTarget) : !isPendingStageBuild(requestTarget)) {
							return;
						}
						if (specialTarget) {
							pendingSpecialBuildPlaylistName = "";
							pendingSpecialBuildPageName = "";
						} else {
							pendingStageBuildPlaylistName = "";
							pendingStageBuildPageName = "";
						}
						playlistPageCache.put(requestTarget.playlistName, finalWarmedPages);
						if (generation != pageBuildGeneration || activePageRuntime == null) {
							if (specialTarget) {
								specialPageSpec = spec;
							} else {
								stagedPageSpec = spec;
							}
							return;
						}
						if (specialTarget) {
							retireRuntimeForReuse(specialPageRuntime);
							specialPageRuntime = buildPageRuntime(specialPageContainer, spec);
							specialPageSpec = null;
						} else {
							retireRuntimeForReuse(stagedPageRuntime);
							stagedPageRuntime = buildPageRuntime(stagedPageContainer, spec);
							stagedPageSpec = null;
						}
					}
				});
			}
		});
	}

	private PageRuntime resolveRuntimeForTarget(PlaybackTarget target, boolean preferSpecialRuntime) {
		if (target == null) {
			return null;
		}
		if (preferSpecialRuntime && target.matches(specialPageRuntime)) {
			return specialPageRuntime;
		}
		if (!preferSpecialRuntime && target.matches(stagedPageRuntime)) {
			return stagedPageRuntime;
		}
		boolean useActiveContainer = activePageRuntime == null;
		RelativeLayout targetContainer = useActiveContainer
				? activePageContainer
				: (preferSpecialRuntime ? specialPageContainer : stagedPageContainer);
		PageRuntime runtime;
		if (preferSpecialRuntime && matchesPageBuildSpec(specialPageSpec, target)) {
			runtime = buildPageRuntime(targetContainer, specialPageSpec);
			specialPageSpec = null;
		} else if (!preferSpecialRuntime && matchesPageBuildSpec(stagedPageSpec, target)) {
			runtime = buildPageRuntime(targetContainer, stagedPageSpec);
			stagedPageSpec = null;
		} else {
			runtime = buildPageRuntime(targetContainer, target);
		}
		if (!useActiveContainer && preferSpecialRuntime) {
			specialPageRuntime = runtime;
		} else if (!useActiveContainer) {
			stagedPageRuntime = runtime;
		}
		return runtime;
	}

	private void refreshSchedulePlaybackState(long nowMillis) {
		SpecialScheduleEvaluator.ScheduleDecision decision = scheduleEvaluator.evaluate(
				AndoWSignageApp.PLAYER_ID,
				playerData != null ? playerData.getPlayerName() : "",
				basePlaylistName,
				nowMillis);
		playbackPlaylistName = decision.getResolvedPlaylistName();
		if (TextUtils.isEmpty(playbackPlaylistName)) {
			playbackPlaylistName = basePlaylistName;
		}
		pendingSchedulePlaylistName = decision.getNextPlaylistName();
		pendingScheduleSwitchAtMillis = decision.getNextSwitchAtMillis();
	}

	private boolean maybeSwitchScheduledPlayback(long nowMillis) {
		String currentRuntimePlaylist = activePageRuntime != null ? activePageRuntime.playlistName : playbackPlaylistName;
		if (TextUtils.isEmpty(playbackPlaylistName)
				|| TextUtils.equals(playbackPlaylistName, currentRuntimePlaylist)) {
			return false;
		}
		List<PageDataModel> targetPages = getOrLoadPageList(playbackPlaylistName);
		if (targetPages == null || targetPages.isEmpty()) {
			return false;
		}
		pageDataList = targetPages;
		pageIdx = 0;
		PlaybackTarget target = createActivePageTarget();
		PageRuntime nextRuntime = resolveRuntimeForTarget(target, true);
		requestRuntimeActivation(nextRuntime, 1);
		return true;
	}
	
	void tickToAllViews() {
		long now = System.currentTimeMillis();
		for (View view: elementViewList) {
			if(view instanceof PlaybackSlotView) {
				((PlaybackSlotView) view).tick();
			} else if(view instanceof MediaView) {
				((MediaView) view).count();
			}
		}
		if (usbPlaybackActive) {
			checkReadyQueue();
			if (debugOverlayVisible) {
				refreshDebugOverlay();
			}
			return;
		}
		if (lastRuntimeActivationAtMillis > 0
				&& now - lastRuntimeActivationAtMillis < POST_SWITCH_TICK_SUPPRESS_MS) {
			if (debugOverlayVisible) {
				refreshDebugOverlay();
			}
			return;
		}
		refreshSchedulePlaybackState(now);
		if (!maybeSwitchScheduledPlayback(now)
				&& !stageNextDeferredPending
				&& now >= nextStageAllowedAtMillis) {
			stageNextPlaybackTarget();
		}
		stageSpecialPlaybackTarget();
		checkReadyQueue();
		if (debugOverlayVisible) {
			refreshDebugOverlay();
		}
	}

	void popPage() {

		try {
			maybeApplyQueuedUpdate();
			refreshSchedulePlaybackState(System.currentTimeMillis());
			if (!"USBP".equalsIgnoreCase(playbackPlaylistName)) {
				resetUsbPlaybackMode(false);
			}
			syncCurrentPlaylistPages();
			if (maybeSwitchScheduledPlayback(System.currentTimeMillis())) {
				return;
			}

			if(playbackPlaylistName.equalsIgnoreCase("USBP"))
			{
				pageTimer.stop();
				if(hasAuthorizedUsbKey()) {
					RunUSBP();
					return;
				}
			}

			if(pageDataList.size() < 1)
			{
				showInitAnim();
				return;
			} else {
				stopAnim();
			}

			if(pageDataList.size() == 1 && pageIdx == 1 && activePageRuntime != null) {
				if (!stageNextDeferredPending && System.currentTimeMillis() >= nextStageAllowedAtMillis) {
					stageNextPlaybackTarget();
				}
				return;
			}

			ensurePageContainersAttached();
			PlaybackTarget target = createActivePageTarget();
			if (target == null) {
				return;
			}
			PageRuntime nextRuntime = resolveRuntimeForTarget(target, false);
			requestRuntimeActivation(nextRuntime, target.pageIndex + 1);
		} catch(Exception e) {
			e.printStackTrace();
		}
	}

	void RunUSBP() {
		stopAllElement();
		stopAnim();
		hidePageContainers();
		cancelDeferredStageNextPlayback();
		ensurePageContainersAttached();
		ensureUsbPlaybackViewAttached();
		resetUsbPlaybackMode(false);

		usbmedialist.clear();

		if(usbflist.size() < 1)
			GetUSBPFiles();

		java.util.Collections.sort(usbflist, String.CASE_INSENSITIVE_ORDER);

		for (String fname : usbflist) {
			File file = new File(LocalPathUtils.getUSBContentFilePath(fname));
			AndoWSignageApp.CONTENT_TYPE type = getMediaType(file.getAbsolutePath());
			if(type == AndoWSignageApp.CONTENT_TYPE.None)
				continue;

			MediaDataModel _model = new MediaDataModel();
			_model.setFileNamePath(file);

			int playtime = 7;

			String onlyname = FilenameUtils.removeExtension(file.getName());
			int idx = onlyname.lastIndexOf('_');
			if(idx > 0) {
				try {
					String time = Utils.parseNumber(onlyname.substring(idx + 1, onlyname.length()));
					playtime = Integer.parseInt(time);
				} catch (Exception exc) {}
			} else {
				if(type == AndoWSignageApp.CONTENT_TYPE.Video) {
					playtime = (int)(Utils.getVideoDuration(this, file.getAbsolutePath())/1000);
				}
			}

			_model.setPlayTime(playtime);
			_model.setType(type.toString());

			boolean _unique = true;
			for (MediaDataModel mm:usbmedialist) {
				if(mm.getFileName().equalsIgnoreCase(file.getName())) {
					_unique = false;
					continue;
				}
			}

			if(_unique)
				usbmedialist.add(_model);
		}

		if (usbPlaybackView == null) {
			return;
		}
		usbPlaybackView.configureMediaContents(AndoWSignageApp.getDeviceWidth(), AndoWSignageApp.getDeviceHeight(), usbmedialist);
		showUsbPlaybackView();
		elementViewList.clear();
		elementViewList.add(usbPlaybackView);
		elementDataList.clear();
		currentPageName = "USBP";
		currentPageDeadlineAtMillis = -1L;
		usbPlaybackActive = true;
		runAllElement();
	}

	private void GetUSBPFiles() {
		File base = new File(LocalPathUtils.getUSBContentsDirPath());
		if(base.listFiles() != null) {
			for (File file : base.listFiles()) {
				usbflist.add(file.getName());
			}
		}
	}

	private boolean hasAuthorizedUsbKey() {
		boolean hasKey = false;
		try {
			hasKey = AuthUtils.HasAuthKey(LocalPathUtils.getAuthFilePath(), NetworkUtils.getMACAddress());
		} catch (Exception ignored) {
		}
		if (!hasKey && new File(LocalPathUtils.getAuthFilePath()).exists()) {
			hasKey = true;
		}
		if (!hasKey) {
			hasKey = LocalSettingsProvider.hasStoredUsbKeyForDevice();
		}
		return hasKey;
	}

	void addSingleMediaElement() {
		ensureUsbPlaybackViewAttached();
		if (usbPlaybackView == null) {
			return;
		}
		usbPlaybackView.configureMediaContents(AndoWSignageApp.getDeviceWidth(), AndoWSignageApp.getDeviceHeight(), usbmedialist);
		elementViewList.clear();
		elementViewList.add(usbPlaybackView);
	}

	public AndoWSignageApp.CONTENT_TYPE getMediaType(String path) {
		AndoWSignageApp.CONTENT_TYPE retType = AndoWSignageApp.CONTENT_TYPE.None;

		String mimeType = FileUtils.getMimeTypeString(path);
		if(mimeType != null) {
			if(mimeType.startsWith("image"))
				retType = AndoWSignageApp.CONTENT_TYPE.Image;
			else if(mimeType.startsWith("video"))
				retType = AndoWSignageApp.CONTENT_TYPE.Video;
		}

		return retType;
	}

	private void runAllElement() {
		if (usbPlaybackActive && usbPlaybackView != null) {
			showUsbPlaybackView();
			usbPlaybackView.runPlaylist();
			return;
		}
		if (activePageRuntime != null) {
			startRuntime(activePageRuntime);
		} else {
			for (View view: elementViewList) {
				if(view instanceof PlaybackSlotView) {
					((PlaybackSlotView) view).showPreparedContent();
					((PlaybackSlotView) view).startPreparedPlayback();
				} else if(view instanceof MediaView) {
					((MediaView) view).runPlaylist();
				}
			}
		}
		for (View view: elementViewList) {
			if(view instanceof PlaybackSlotView) {
				((PlaybackSlotView) view).startPreparedPlayback();
			}
		}
	}

	@Override
	public boolean onKeyDown(int keyCode, KeyEvent event) {
		if (handleOverlaySequenceKey(keyCode, event)) {
			return true;
		}

		switch(keyCode) {

			case KeyEvent.KEYCODE_HOME :
			case KeyEvent.KEYCODE_MENU :
			case KeyEvent.KEYCODE_APP_SWITCH :
			case KeyEvent.KEYCODE_BACK :
				break;

			case KeyEvent.KEYCODE_E:
				if(event.isCtrlPressed() && executeOverlaySequenceCommand('e')) {
					return true;
				}
				break;

			case KeyEvent.KEYCODE_D:
				if(event.isCtrlPressed() && executeOverlaySequenceCommand('d')) {
					return true;
				}
				break;

			case KeyEvent.KEYCODE_M:
				if(event.isCtrlPressed() && executeOverlaySequenceCommand('m')) {
					return true;
				}
				break;

			case KeyEvent.KEYCODE_S:
				if(event.isCtrlPressed() && executeOverlaySequenceCommand('s')) {
					return true;
				}
				break;
				
			case KeyEvent.KEYCODE_DPAD_LEFT:
				PrevContent();
				return true;
				
			case KeyEvent.KEYCODE_DPAD_RIGHT:
				NextContent();
				return true;
			
			case KeyEvent.KEYCODE_POWER :
            case KeyEvent.KEYCODE_F1:
                toggleDebugOverlay();
                return true;
			default:
				break;
		}
		return super.onKeyDown(keyCode, event);
	}
	
	private void NextContent() {
		for (View view: elementViewList) {
			if(view instanceof PlaybackSlotView) {
				((PlaybackSlotView) view).nextContent();
				break;
			}
			if(view instanceof MediaView) {
				((MediaView) view).nextContent();
				break;
			}
		}
	}

	private void PrevContent() {		
		for (View view: elementViewList) {
			if(view instanceof PlaybackSlotView) {
				((PlaybackSlotView) view).prevContent();
				break;
			}
			if(view instanceof MediaView) {
				((MediaView) view).prevContent();
				break;
			}
		}
	}

	public void disableWatchDog() {
		sendWatchdogBroadcast("kr.co.turtlelab.watchdog.disable");
	}

	public void enableWatchDog() {
		sendWatchdogBroadcast("kr.co.turtlelab.watchdog.enable");
	}

	public void enable7waitWatchDog() {
		sendWatchdogBroadcast("kr.co.turtlelab.watchdog.enable7wait");
	}

	private void startWatchdogPing() {
		watchdogPingHandler.removeCallbacks(watchdogPingRunnable);
		watchdogPingHandler.post(watchdogPingRunnable);
	}

	private void stopWatchdogPing() {
		watchdogPingHandler.removeCallbacks(watchdogPingRunnable);
	}

	private void sendWatchdogPing() {
		sendWatchdogBroadcast(WATCHDOG_PING_ACTION);
	}

	private void sendWatchdogBroadcast(String action) {
		try {
			Intent explicitIntent = new Intent(action);
			explicitIntent.setPackage(WATCHDOG_PACKAGE);
			sendBroadcast(explicitIntent);
		} catch (Exception e) {
		}
		try {
			sendBroadcast(new Intent(action));
		} catch (Exception e) {
		}
	}

	@Override
	public boolean dispatchKeyEvent(KeyEvent event) {
		if (event != null
				&& event.getAction() == KeyEvent.ACTION_DOWN
				&& !isConfigInputDialogShowing()) {
			requestKeyInputOverlayFocus();
		}
		return super.dispatchKeyEvent(event);
	}

	@Override
	public void onWindowFocusChanged(boolean hasFocus) {
		super.onWindowFocusChanged(hasFocus);
		if (hasFocus && !isConfigInputDialogShowing()) {
			requestKeyInputOverlayFocus();
		}
	}

	void bindUpdateMgrService() {
		Intent mUpdateMgrIntent = new Intent(this, UpdateManagerService.class);
		bindService(mUpdateMgrIntent, mUpdateMgrSrvConn, BIND_AUTO_CREATE);
	}
	
	void bindMgrServices() {
		bindUpdateMgrService();
	}
	
	void unbindUpdateMgrService() {
		if(mUpdateMgrSrvBounded) {
	        unbindService(mUpdateMgrSrvConn);
	        mUpdateMgrSrvBounded = false;
	    }		
	}
	
	void unbindMgrServices() {
		unbindUpdateMgrService();
	}

	ServiceConnection mUpdateMgrSrvConn = new ServiceConnection() {

	    public void onServiceDisconnected(ComponentName name) {
	    	mUpdateMgrSrvBounded = false;
	        mUpdateMgrSrv = null;
	    }

	    public void onServiceConnected(ComponentName name, IBinder service) {
	    	mUpdateMgrSrvBounded = true;
	    	UpdateMgrLocalBinder mLocalBinder = (UpdateMgrLocalBinder)service;
	        mUpdateMgrSrv = mLocalBinder.getService();
	    }
	};
	
	public void CheckUpdateTimerNow() {
		
		if(mUpdateMgrSrv == null)
			return;
		
		mUpdateMgrSrv.CheckOrRestartTimer();
	}

	public boolean onMediaContentComplete() {
		if (pendingUpdateReady) {
			maybeApplyQueuedUpdate();
		}
		return tryActivatePendingRuntime();
	}

	private void checkReadyQueue() {
		boolean hasReady = UpdateQueueProvider.hasReadyQueue();
		if (!hasReady && !pendingUpdateReady) {
			return;
		}
		if (hasReady) {
			pendingUpdateReady = true;
		} else if (!hasReady && pendingUpdateReady) {
			pendingUpdateReady = false;
			return;
		}
		if (shouldApplyReadyImmediately()) {
			maybeApplyQueuedUpdate();
		}
	}

	private boolean shouldApplyReadyImmediately() {
		return isIntroPlaying() || AndoWSignageApp.SWITCH_ON_CONTENT_END;
	}

	private boolean isIntroPlaying() {
		return vv != null;
	}

	private boolean applyPendingReadyQueuesSync() {
		if (!UpdateQueueProvider.hasReadyQueue()) {
			pendingUpdateReady = false;
			return false;
		}
		DataSyncManager manager = new DataSyncManager();
		boolean applied = false;
		while (UpdateQueueProvider.hasReadyQueue()) {
			if (manager.applyNextReadyQueue(false)) {
				applied = true;
			} else {
				break;
			}
		}
		if (applied) {
			pendingUpdateReady = false;
		} else {
			pendingUpdateReady = UpdateQueueProvider.hasReadyQueue();
		}
		return applied;
	}

	private void maybeApplyQueuedUpdate() {
		if (!pendingUpdateReady || queuedUpdateRestartPending) {
			return;
		}
		queuedUpdateRestartPending = true;
		SystemUtils.runOnUiThread(() -> {
			if (!pendingUpdateReady) {
				queuedUpdateRestartPending = false;
				return;
			}
			updateAndRestart(true);
		});
	}

	public void showReadyUpdateIndicator() {
		pendingUpdateReady = true;
		maybeApplyQueuedUpdate();
	}

	private String formatTimestamp(long millis) {
		if (millis <= 0) {
			return "-";
		}
		SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA);
		return format.format(new Date(millis));
	}

	private void initDebugOverlay() {
		debugOverlay = new TextView(this);
		debugOverlay.setBackgroundColor(Color.argb(220, 0, 0, 0));
		debugOverlay.setTextColor(Color.GREEN);
		debugOverlay.setTypeface(Typeface.MONOSPACE);
		int pad = (int) (getResources().getDisplayMetrics().density * 8);
		debugOverlay.setPadding(pad, pad, pad, pad);
		debugOverlay.setTextSize(12);
		debugOverlay.setVisibility(View.GONE);
		RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
				RelativeLayout.LayoutParams.WRAP_CONTENT,
				RelativeLayout.LayoutParams.WRAP_CONTENT);
		params.addRule(RelativeLayout.ALIGN_PARENT_BOTTOM);
		params.addRule(RelativeLayout.ALIGN_PARENT_END);
		int margin = (int) (getResources().getDisplayMetrics().density * 16);
		params.setMargins(margin, margin, margin, margin);
		if (overlay_container != null) {
			overlay_container.addView(debugOverlay, params);
		} else {
			layout_root.addView(debugOverlay, params);
		}
	}

	private void initKeyInputOverlay() {
		if (keyInputOverlay == null) {
			return;
		}

		keyInputOverlay.setOnFocusChangeListener(new View.OnFocusChangeListener() {
			@Override
			public void onFocusChange(View v, boolean hasFocus) {
				if (!hasFocus && canTakeOverlayInputFocus()) {
					v.post(new Runnable() {
						@Override
						public void run() {
							requestKeyInputOverlayFocus();
						}
					});
				}
			}
		});

		keyInputOverlay.addTextChangedListener(new TextWatcher() {
			@Override
			public void beforeTextChanged(CharSequence s, int start, int count, int after) {
			}

			@Override
			public void onTextChanged(CharSequence s, int start, int before, int count) {
			}

			@Override
			public void afterTextChanged(Editable s) {
				if (suppressOverlayTextWatcher || s == null || s.length() == 0) {
					return;
				}

				String input = s.toString();
				suppressOverlayTextWatcher = true;
				try {
					handleOverlayContinuousInput(input);
					s.clear();
				} finally {
					suppressOverlayTextWatcher = false;
				}
			}
		});

		requestKeyInputOverlayFocus();
	}

	private boolean canTakeOverlayInputFocus() {
		return keyInputOverlay != null
				&& !isFinishing()
				&& !isConfigInputDialogShowing();
	}

	private boolean isConfigInputDialogShowing() {
		return m_settingDlg != null
				&& (m_settingDlg.isShowing() || m_settingDlg.isSubDialogShowing());
	}

	private void resetOverlayCommandBuffer() {
		overlayCommandBuffer.setLength(0);
		overlayCommandLastInputAt = 0L;
	}

		private boolean executeOverlaySequenceCommand(char commandChar) {
			switch (Character.toLowerCase(commandChar)) {
				case 'e':
					enable7waitWatchDog();
					return true;

			case 'd':
				manualStopRequested = true;
				disableWatchDog();
				stopWatchdogPing();
				finish();
				return true;

			case 'm':
				FileUtils.deleteFile(LocalPathUtils.getAuthFilePath());
				LocalSettingsProvider.updateUsbAuthKey("");
				return true;

			case 's':
				Intent configIntent = new Intent();
				configIntent.setAction("andowsignage.intent.action.CALL_SETTINGS");
				act.sendBroadcast(configIntent);
				return true;

			default:
				return false;
		}
	}

	private boolean appendOverlaySequenceChar(char inputChar) {
		char normalized = Character.toLowerCase(inputChar);
		if (normalized < 'a' || normalized > 'z') {
			resetOverlayCommandBuffer();
			return false;
		}

		long now = System.currentTimeMillis();
		if (overlayCommandLastInputAt > 0L && now - overlayCommandLastInputAt > OVERLAY_SEQUENCE_TIMEOUT_MS) {
			resetOverlayCommandBuffer();
		}
		overlayCommandLastInputAt = now;
		overlayCommandBuffer.append(normalized);
		if (overlayCommandBuffer.length() > 2) {
			overlayCommandBuffer.delete(0, overlayCommandBuffer.length() - 2);
		}

		int length = overlayCommandBuffer.length();
		if (length >= 2) {
			char prev = overlayCommandBuffer.charAt(length - 2);
			char last = overlayCommandBuffer.charAt(length - 1);
			if (prev == last && executeOverlaySequenceCommand(last)) {
				resetOverlayCommandBuffer();
				return true;
			}
		}

		return false;
	}

	private void handleOverlayContinuousInput(CharSequence input) {
		if (TextUtils.isEmpty(input)) {
			return;
		}

		for (int i = 0; i < input.length(); i++) {
			char ch = input.charAt(i);
			if (ch >= 1 && ch <= 26) {
				executeOverlaySequenceCommand((char) ('a' + ch - 1));
				resetOverlayCommandBuffer();
				continue;
			}

			if (Character.isLetter(ch)) {
				appendOverlaySequenceChar(ch);
				continue;
			}

			if (!Character.isWhitespace(ch)) {
				resetOverlayCommandBuffer();
			}
		}
	}

	private boolean handleOverlaySequenceKey(int keyCode, KeyEvent event) {
		if (event == null || event.isCtrlPressed()) {
			return false;
		}

			switch (keyCode) {
				case KeyEvent.KEYCODE_D:
					return appendOverlaySequenceChar('d');
				case KeyEvent.KEYCODE_E:
					return appendOverlaySequenceChar('e');
				case KeyEvent.KEYCODE_M:
					return appendOverlaySequenceChar('m');
				case KeyEvent.KEYCODE_S:
				return appendOverlaySequenceChar('s');
			default:
				return false;
		}
	}

	private void requestKeyInputOverlayFocus() {
		if (!canTakeOverlayInputFocus()) {
			return;
		}

		if (overlay_container != null) {
			overlay_container.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
			overlay_container.bringToFront();
		}
		keyInputOverlay.setVisibility(View.VISIBLE);
		keyInputOverlay.setEnabled(true);
		keyInputOverlay.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
		keyInputOverlay.bringToFront();
		keyInputOverlay.setSelection(keyInputOverlay.length());
		if (!keyInputOverlay.hasFocus()) {
			keyInputOverlay.requestFocus();
		}
		keyInputOverlay.requestFocusFromTouch();
		InputMethodManager imm = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
		if (imm != null) {
			imm.restartInput(keyInputOverlay);
			imm.hideSoftInputFromWindow(keyInputOverlay.getWindowToken(), 0);
		}
	}

	private void releaseKeyInputOverlayFocus() {
		if (keyInputOverlay == null) {
			return;
		}

		if (overlay_container != null) {
			overlay_container.setImportantForAccessibility(
					View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS);
		}
		keyInputOverlay.clearFocus();
		keyInputOverlay.setEnabled(false);
		keyInputOverlay.setVisibility(View.INVISIBLE);
		keyInputOverlay.setImportantForAccessibility(
				View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS);
		InputMethodManager imm = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
		if (imm != null) {
			imm.hideSoftInputFromWindow(keyInputOverlay.getWindowToken(), 0);
		}
	}

	private void toggleDebugOverlay() {
		if (debugOverlay == null) {
			return;
		}
		debugOverlayVisible = !debugOverlayVisible;
		debugOverlay.setVisibility(debugOverlayVisible ? View.VISIBLE : View.GONE);
		if (debugOverlayVisible) {
			refreshDebugOverlay();
		}
	}

	private void refreshDebugOverlay() {
		if (!debugOverlayVisible || debugOverlay == null) {
			return;
		}
		StringBuilder sb = new StringBuilder();
		sb.append("[STATE]\n");
		sb.append("Player=").append(AndoWSignageApp.state)
				.append("  PendingReady=").append(pendingUpdateReady)
				.append("\nPlaylist=").append(playerData != null ? playerData.getPlaylist() : "")
				.append("  CurrentPage=").append(currentPageName == null ? "" : currentPageName);

		sb.append("\n\n[QUEUE]\n");
		RealmUpdateQueue queue = UpdateQueueProvider.getLatestQueueSnapshot();
		if (queue == null) {
			sb.append("No queue\n");
		} else {
			sb.append("ID ").append(queue.getId())
					.append(" status=").append(queue.getStatus())
					.append(String.format(Locale.US, " progress=%.1f%%", queue.getProgress()));
			sb.append("\nRetry=").append(queue.getRetryCount())
					.append(" next=").append(queue.getNextRetryAt());
			ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
			if (journal != null) {
				journal.ensureDefaults();
				List<UpdateQueueContract.DownloadEntry> entries = journal.getEntries();
				int total = entries.size();
				int completed = 0;
				int active = 0;
				int failed = 0;
				for (UpdateQueueContract.DownloadEntry entry : entries) {
					if (entry == null) continue;
					String status = entry.Status == null ? "" : entry.Status;
					if (UpdateQueueContract.DownloadStatus.DONE.equalsIgnoreCase(status)) completed++;
					else if (UpdateQueueContract.DownloadStatus.FAILED.equalsIgnoreCase(status)) failed++;
					else if (UpdateQueueContract.DownloadStatus.DOWNLOADING.equalsIgnoreCase(status)) active++;
				}
				sb.append("\nDownloads total=").append(total)
						.append(" done=").append(completed)
						.append(" active=").append(active)
						.append(" failed=").append(failed);
			}
		}

		sb.append("\n\n[PLAYLIST]\n");
		List<PageDataModel> displayList = null;
		if (playerData != null && !TextUtils.isEmpty(playerData.getPlaylist())) {
			// 최신 Realm 상태를 반영하도록 항상 Provider에서 다시 가져온다.
			displayList = PlaylistDataProvider.getPageList(playerData.getPlaylist());
		}
		if ((displayList == null || displayList.isEmpty()) && pageDataList != null && !pageDataList.isEmpty()) {
			displayList = pageDataList;
		}
		if (displayList == null || displayList.isEmpty()) {
			sb.append("No pages");
		} else {
			int limit = Math.min(displayList.size(), 6);
			for (int i = 0; i < limit; i++) {
				PageDataModel pdm = displayList.get(i);
				String marker = currentPageName != null && currentPageName.equals(pdm.getPageName()) ? " <" : "";
				sb.append(String.format(Locale.US, "%02d. %s (%ds)%s\n",
						i + 1,
						pdm.getPageName(),
						pdm.getPlayTimeSec(),
						marker));
			}
			if (displayList.size() > limit) {
				sb.append("... ").append(displayList.size() - limit).append(" more");
			}
		}

		debugOverlay.setText(sb.toString());
	}

}

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
import kr.co.turtlelab.andowsignage.tools.SystemUtils;
import kr.co.turtlelab.andowsignage.tools.Utils;
import kr.co.turtlelab.andowsignage.tools.WakeLocker;
import kr.co.turtlelab.andowsignage.data.update.ContentDownloadJournal;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;
import kr.co.turtlelab.andowsignage.views.GifMovieView;
import kr.co.turtlelab.andowsignage.views.KeyCaptureEditText;
import kr.co.turtlelab.andowsignage.views.MediaView;
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
	List<ElementDataModel> elementDataList = new ArrayList<ElementDataModel>();
	List<View> elementViewList = new ArrayList<View>();
	WelcomeDataModel wdm = new WelcomeDataModel();
	private boolean pendingUpdateReady = false;
	private TextView debugOverlay;
	private boolean debugOverlayVisible = false;
	private KeyCaptureEditText keyInputOverlay;
	private static final long OVERLAY_SEQUENCE_TIMEOUT_MS = 1200L;
	private final StringBuilder overlayCommandBuffer = new StringBuilder();
	private long overlayCommandLastInputAt = 0L;
	private boolean suppressOverlayTextWatcher = false;

	RelativeLayout layout_root;
	RelativeLayout overlay_container;
	RelativeLayout.LayoutParams layout_params;
	
	static int pageIdx = 0;
	
	public static String currentPageName = "";

	public List<String> usbflist = new ArrayList<>();
	public List<MediaDataModel> usbmedialist = new ArrayList<>();

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
		
		if(!playerData.getPlaylist().isEmpty())	
			pageDataList = PlaylistDataProvider.getPageList(playerData.getPlaylist());

		boolean appliedReady = applyPendingReadyQueuesSync();
		if (appliedReady && !playerData.getPlaylist().isEmpty()) {
			pageDataList = PlaylistDataProvider.getPageList(playerData.getPlaylist());
		}
		
		final View decorView = getWindow().getDecorView();

		decorView.setOnSystemUiVisibilityChangeListener
		        (new View.OnSystemUiVisibilityChangeListener() {
		    @Override
		    public void onSystemUiVisibilityChange(int visibility) {
  		    	if(visibility == 0) return;
		    	final int visiblityInt = visibility;
		        //if ((visibility & View.SYSTEM_UI_FLAG_FULLSCREEN) == 0) {
		    		mPostRunHandler.postDelayed(new Runnable()
		    		{
		    		  @Override     
		    		  public void run()
		    		  {
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
		AndoWSignageApp.state = RP_STATUS.playing.toString();

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
        AndoWSignageApp.beginShutdown();
        pendingHeartbeatServiceStop = true;
		Intent intent = new Intent(this, HeartbeatService.class);
		intent.setAction(HeartbeatService.ACTION_SEND_STOPPED);
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
					Intent _intent = new Intent();
					_intent.setAction("rk.android.realsleepmode.action");
					sendBroadcast(_intent);
				} else if(action.equalsIgnoreCase(ACTION_CALL_SETTINGS)) {
					if(m_settingDlg != null) {
						if(m_settingDlg.isShowing()) return;
					}
					//showCustomDialog();
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
        AndoWSignageApp.state = RP_STATUS.stopped.toString();
        requestFinalHeartbeat();

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
		for (View view: elementViewList) {
			if(view instanceof MediaView) {
				((MediaView) view).stopPlaylist();
			}
		}
	}
	
	public void updateAndRestart(boolean setOrientation) {
		if(tickTimer != null)
			tickTimer.stop();
		
		if(pageTimer != null)
			pageTimer.stop();

		applyPendingReadyQueuesSync();
		
		playerData = PlayerDataProvider.getPlayerData();
		pageDataList = PlaylistDataProvider.getPageList(playerData.getPlaylist());
		stopTimerAndElements();
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
		layout_root.removeAllViews();
		elementViewList.clear();		
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
		vv = new TurtleVideoView(sCtx);
		layout_params = new RelativeLayout.LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
		layout_params.addRule(RelativeLayout.CENTER_IN_PARENT);
		layout_root.addView(vv ,layout_params);
		String path = "android.resource://" + getPackageName() + "/" + R.raw.intro;
		vv.setVideoURI(Uri.parse(path));
		vv.setOnCompletionListener(new MediaPlayer.OnCompletionListener() {

			@Override
			public void onCompletion(MediaPlayer mp) {
				vv.start();
			}
		});
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
//		Display display = ((WindowManager) getSystemService(Context.WINDOW_SERVICE)).getDefaultDisplay();         
//		Point outSize = new Point();
//		display.getSize(outSize);
		
//		AndoWSignageApp.updateDisplaySize(outSize.x, outSize.y);
		View decorView = act.getWindow().getDecorView();
		AndoWSignageApp.updateDisplaySize(decorView.getWidth(), decorView.getHeight());
		
		layout_root.removeAllViews();
		elementViewList.clear();
		updateLayoutScales();
		setLayout();
    }


	private void updateLayoutScales() {
		for (ElementDataModel elementValue : elementDataList) {
			elementValue.updateScales(AndoWSignageApp.getScale(), AndoWSignageApp.getScaleX(), AndoWSignageApp.getScaleY());
		}
	}
	
	void setLayout() {
		for (ElementDataModel edm : elementDataList) {
			addElement(edm);
		}
    }

	
	void addElement(ElementDataModel edm) {
		View element = null;
		AndoWSignageApp.ELEMENT_TYPE type = AndoWSignageApp.ELEMENT_TYPE.valueOf(edm.getType());
		
		layout_params = new RelativeLayout.LayoutParams(edm.getWidth(), edm.getHeight());
		layout_params.leftMargin = edm.getX();
		layout_params.topMargin = edm.getY();
		
		switch (type) {
		
			case Media:
				List<MediaDataModel> cdmList
					= MediaDataProvider.getContentList(currentPageName, edm.getName()); 
				element = new MediaView(act, this, edm.getWidth(), edm.getHeight(), cdmList);
				break;
				
			case ScrollText:
				List<ScrolltextDataModel> sdmList
					= ScrolltextDataProvider.getContentList(currentPageName, edm.getName());
				if(sdmList == null) return;
				element = new ScrollTextView(act, this, edm.getWidth(), edm.getHeight(), sdmList);		
				break;
				
			case WelcomeBoard:
				wdm	= WelcomeDataProvider.getContent(currentPageName, edm.getName());
				if (wdm == null) {
					break;
				}
				element = new WelcomeView(act, this, edm.getWidth(), edm.getHeight(), wdm);
				break;
			
			case TemplateBoard:
				List<MediaDataModel> tbList = MediaDataProvider.getContentList(currentPageName, edm.getName());
				element = new TurtleWebView(this, edm.getWidth(), edm.getHeight(), tbList);
				break;
				
			default:
				break;
		}
		if (element == null) {
			return;
		}
		layout_root.addView(element, layout_params);
		elementViewList.add(element);
	}
	
	void tickToAllViews() {
		for (View view: elementViewList) {
			if(view instanceof MediaView) {
				((MediaView) view).count();
			}
		}
		checkReadyQueue();
		if (debugOverlayVisible) {
			refreshDebugOverlay();
		}
	}

	void popPage() {

		try {

			maybeApplyQueuedUpdate();

			if(playerData.getPlaylist().equalsIgnoreCase("USBP"))
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

			if(pageDataList.size() == 1 && pageIdx == 1) {
				return;
			}

			if(pageDataList.size()-1 < pageIdx) {
				pageIdx = 0;
			}
			
			PageDataModel pdm = pageDataList.get(pageIdx);
			
			currentPageName = pdm.getPageName();
			
			elementDataList = ElementDataProvider.getPageElementList(currentPageName);
	
			stopAllElement();
			layout_root.removeAllViews();
			elementViewList.clear();
			
			System.gc();
			
			setLayout();
			
			runAllElement();
			
			pageTimer.changeInterval(pdm.getPlayTimeSec()*1000);
			tickTimer.changeInterval(1000);
			
			if(!pageTimer.getIsTicking()) {
				pageTimer.start();
			}
			
			pageIdx++;
		} catch(Exception e) {
			e.printStackTrace();
		}
	}

	void RunUSBP() {
		stopAllElement();
		layout_root.removeAllViews();
		elementViewList.clear();
		System.gc();

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

		addSingleMediaElement();
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
		layout_params = new RelativeLayout.LayoutParams(RelativeLayout.LayoutParams.MATCH_PARENT, RelativeLayout.LayoutParams.MATCH_PARENT);
		MediaView mediaview = new MediaView(act, sCtx, AndoWSignageApp.getDeviceWidth(), AndoWSignageApp.getDeviceHeight(), usbmedialist);
		layout_root.addView(mediaview, layout_params);
		elementViewList.add(mediaview);
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
		for (View view: elementViewList) {
			if(view instanceof MediaView) {
				((MediaView) view).runPlaylist();
			}
			
			if(view instanceof ScrollTextView) {
				//((ScrollTextView) view).startMarqueeAnim();
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
			if(view instanceof MediaView) {
				((MediaView) view).nextContent();
				break;
			}
		}
	}

	private void PrevContent() {		
		for (View view: elementViewList) {
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
		if (event != null && event.getAction() == KeyEvent.ACTION_DOWN) {
			requestKeyInputOverlayFocus();
		}
		return super.dispatchKeyEvent(event);
	}

	@Override
	public void onWindowFocusChanged(boolean hasFocus) {
		super.onWindowFocusChanged(hasFocus);
		if (hasFocus) {
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

	public void onMediaContentComplete() {
		if (pendingUpdateReady) {
			maybeApplyQueuedUpdate();
		}
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
			if (manager.applyNextReadyQueue()) {
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
		if (!pendingUpdateReady) {
			return;
		}
		DataSyncManager manager = new DataSyncManager();
		boolean applied = manager.applyNextReadyQueue();
		if (applied) {
			pendingUpdateReady = false;
			SystemUtils.runOnUiThread(() -> updateAndRestart(true));
		} else {
			pendingUpdateReady = UpdateQueueProvider.hasReadyQueue();
		}
	}

	public void showReadyUpdateIndicator() {
		pendingUpdateReady = true;
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
				&& (m_settingDlg == null || !m_settingDlg.isShowing());
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

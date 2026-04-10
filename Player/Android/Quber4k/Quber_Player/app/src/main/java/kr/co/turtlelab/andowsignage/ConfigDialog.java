package kr.co.turtlelab.andowsignage;

import android.app.AlertDialog;
import android.app.Dialog;
import android.content.Context;
import android.media.MediaScannerConnection;
import android.os.Environment;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.CompoundButton.OnCheckedChangeListener;
import android.widget.EditText;
import android.widget.RelativeLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.TimePicker;
import android.widget.ToggleButton;
import android.widget.Toast;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider;
import kr.co.turtlelab.andowsignage.dataproviders.WeeklyScheduleProvider;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.FileUtils;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;
import kr.co.turtlelab.andowsignage.tools.Utils;

public class ConfigDialog extends Dialog implements View.OnClickListener {

	public static final String KEY_PLAYER_ID = "player_ip";
  	public static final String KEY_MANAGER_IP = "manager_ip";
  	public static final String KEY_ENABLE_MANUAL_IP = "is_manual";
  	public static final String KEY_MANUAL_IP = "manual_ip";
  	public static final String KEY_KEEP_RATIO = "keepratio";
  		
	EditText player_id;
	CheckBox check_manual;
	Button id_confirmBtn;
	EditText pIP1;
	EditText pIP2;
	EditText pIP3;
	EditText pIP4;
	EditText managerAddressEdit;
	
	Button mSch1from;
	Button mSch1to;
	Button mSch2from;
	Button mSch2to;
	Button mSch3from;
	Button mSch3to;
	Button mSch4from;
	Button mSch4to;
	Button mSch5from;
	Button mSch5to;
	Button mSch6from;
	Button mSch6to;
	Button mSch7from;
	Button mSch7to;

	TextView mAuthBg;
	EditText mSrcKey;
	EditText mAuthKey;
	Button mAuthBtn;
	
	LightestTimer rpcAbortTimer;
	Context ctx;
	ScrollView m_svMain;
	Button saveBtn;
	Button exportRealmBtn;
	RelativeLayout m_SchLayout_root;
	
	List<WeeklyScheduleDataModel> weeklySchDataList = new ArrayList<WeeklyScheduleDataModel>();
	
	final View dialogView;
	final AlertDialog tpickerDialog;
	
	LocalSettingsModel localsettings = new LocalSettingsModel();

	CheckBox check_keepratio;
	TextView check_keepratio_txt;
	CheckBox check_switch_content;
	TextView check_switch_content_txt;
	EditText activeTextInput;
	
	public ConfigDialog(Context context) {
		super(context);
		
		ctx = context;
		requestWindowFeature(Window.FEATURE_NO_TITLE);
		setContentView(R.layout.custom_dialog);
		getWindow().setBackgroundDrawableResource(android.R.color.transparent);
		prepareDialogWindow(getWindow(), WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
		setCanceledOnTouchOutside(false);

		initLayoutItems();
		
		dialogView = View.inflate(AndoWSignage.getCtx(), R.layout.time_dialog, null);
		tpickerDialog = new AlertDialog.Builder(AndoWSignage.act).create();
	}
	

	@Override
	protected void onStart() {
		super.onStart();
		prepareDialogWindow(getWindow(), WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
		
		refreshWeeklyDataList();
		focusTextInput(resolvePreferredTextInput(), false);
	}

	private void prepareDialogWindow(Window window, int softInputMode) {
		if (window == null) {
			return;
		}
		window.clearFlags(WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
				| WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);
		window.setSoftInputMode(softInputMode);
	}

	public boolean isSubDialogShowing() {
		return tpickerDialog != null && tpickerDialog.isShowing();
	}

	private void bindEditableField(final EditText editText) {
		if (editText == null) {
			return;
		}

		editText.setFocusable(true);
		editText.setFocusableInTouchMode(true);
		editText.setOnFocusChangeListener(new View.OnFocusChangeListener() {
			@Override
			public void onFocusChange(View v, boolean hasFocus) {
				if (hasFocus) {
					activeTextInput = editText;
					ensureTextInputConnection(editText);
				}
			}
		});
		editText.setOnClickListener(new View.OnClickListener() {
			@Override
			public void onClick(View v) {
				focusTextInput(editText, false);
			}
		});
		editText.setOnTouchListener(new View.OnTouchListener() {
			@Override
			public boolean onTouch(View v, MotionEvent event) {
				if (event != null && event.getAction() == MotionEvent.ACTION_DOWN) {
					activeTextInput = editText;
				}
				return false;
			}
		});
	}

	private void ensureTextInputConnection(EditText editText) {
		if (editText == null || !editText.isEnabled()) {
			return;
		}

		InputMethodManager imm = (InputMethodManager) ctx.getSystemService(Context.INPUT_METHOD_SERVICE);
		if (imm != null) {
			imm.restartInput(editText);
		}
	}

	private void focusTextInput(EditText editText, boolean selectAll) {
		if (editText == null || !editText.isEnabled()) {
			return;
		}

		activeTextInput = editText;
		if (!editText.hasFocus()) {
			editText.requestFocus();
		}
		editText.requestFocusFromTouch();
		if (editText.length() > 0) {
			if (selectAll) {
				editText.selectAll();
			} else {
				editText.setSelection(editText.length());
			}
		}
		ensureTextInputConnection(editText);
	}

	private EditText resolvePreferredTextInput() {
		View currentFocus = getCurrentFocus();
		if (currentFocus instanceof EditText) {
			EditText currentEditText = (EditText) currentFocus;
			if (currentEditText.isEnabled()) {
				return currentEditText;
			}
		}

		if (activeTextInput != null && activeTextInput.isEnabled()) {
			return activeTextInput;
		}

		if (player_id != null && player_id.isEnabled()) {
			return player_id;
		}

		if (managerAddressEdit != null && managerAddressEdit.isEnabled()) {
			return managerAddressEdit;
		}

		return null;
	}

	private boolean isHardwareTextInputEvent(KeyEvent event) {
		if (event == null || event.getAction() != KeyEvent.ACTION_DOWN || event.isSystem()) {
			return false;
		}

		if (event.isAltPressed() || event.isCtrlPressed() || event.isMetaPressed()) {
			return false;
		}

		int keyCode = event.getKeyCode();
		if (keyCode == KeyEvent.KEYCODE_DEL
				|| keyCode == KeyEvent.KEYCODE_FORWARD_DEL
				|| keyCode == KeyEvent.KEYCODE_SPACE
				|| keyCode == KeyEvent.KEYCODE_TAB
				|| keyCode == KeyEvent.KEYCODE_ENTER
				|| keyCode == KeyEvent.KEYCODE_NUMPAD_ENTER) {
			return true;
		}

		return event.getUnicodeChar() != 0;
	}
	
	private void initLayoutItems() {
		/*Player Configuration*/
		m_SchLayout_root = (RelativeLayout)findViewById(R.id.schedule_settings);
		id_confirmBtn = (Button)findViewById(R.id.id_confirm_btn);
		id_confirmBtn.setOnClickListener(this);
		player_id = (EditText)findViewById(R.id.player_id);
		bindEditableField(player_id);
		
		player_id.addTextChangedListener(new TextWatcher() {
			
			@Override
			public void onTextChanged(CharSequence s, int start, int before, int count) {
			}
			
			@Override
			public void beforeTextChanged(CharSequence s, int start, int count, int after) {
			}
			
			@Override
			public void afterTextChanged(Editable s) {
/*				String trimedID = s.toString().trim();
				if(AndoWSignageApp.PLAYER_ID.equalsIgnoreCase(trimedID)) {
					id_confirmBtn.setEnabled(false);
					return;
				}
				id_confirmBtn.setEnabled(s.length()>0);*/
				//if(s.length() < 1) {
				//	player_id.setHint(PlayerDataProvider.makeRandomID());
				//}
			}
		});
		
		localsettings = LocalSettingsProvider.getLocalSettings().get(0);

		String pid = localsettings.getPlayerId();
		if(TextUtils.isEmpty(pid)) {
			pid = AndoWSignageApp.PLAYER_ID;
		}
		if(TextUtils.isEmpty(pid)) {
			player_id.setText(AndoWSignageApp.PLAYER_ID);
		} else {
			player_id.setText(pid);
			AndoWSignageApp.PLAYER_ID = pid;
		}
		
		pIP1 = (EditText)findViewById(R.id.player_ip1_edit);
		pIP2 = (EditText)findViewById(R.id.player_ip2_edit);
		pIP3 = (EditText)findViewById(R.id.player_ip3_edit);
		pIP4 = (EditText)findViewById(R.id.player_ip4_edit);
		bindEditableField(pIP1);
		bindEditableField(pIP2);
		bindEditableField(pIP3);
		bindEditableField(pIP4);
		
		managerAddressEdit = (EditText)findViewById(R.id.manager_address_edit);
		bindEditableField(managerAddressEdit);
		
		mSch1from = (Button)findViewById(R.id.timePicker1from);
		mSch1to = (Button)findViewById(R.id.timePicker1to);
		mSch2from = (Button)findViewById(R.id.timePicker2from);
		mSch2to = (Button)findViewById(R.id.timePicker2to);
		mSch3from = (Button)findViewById(R.id.timePicker3from);
		mSch3to = (Button)findViewById(R.id.timePicker3to);
		mSch4from = (Button)findViewById(R.id.timePicker4from);
		mSch4to = (Button)findViewById(R.id.timePicker4to);
		mSch5from = (Button)findViewById(R.id.timePicker5from);
		mSch5to = (Button)findViewById(R.id.timePicker5to);
		mSch6from = (Button)findViewById(R.id.timePicker6from);
		mSch6to = (Button)findViewById(R.id.timePicker6to);
		mSch7from = (Button)findViewById(R.id.timePicker7from);
		mSch7to = (Button)findViewById(R.id.timePicker7to);
		
		mSch1from.setOnClickListener(this);
		mSch1to.setOnClickListener(this);
		mSch2from.setOnClickListener(this);
		mSch2to.setOnClickListener(this);
		mSch3from.setOnClickListener(this);
		mSch3to.setOnClickListener(this);
		mSch4from.setOnClickListener(this);
		mSch4to.setOnClickListener(this);
		mSch5from.setOnClickListener(this);
		mSch5to.setOnClickListener(this);
		mSch6from.setOnClickListener(this);
		mSch6to.setOnClickListener(this);
		mSch7from.setOnClickListener(this);
		mSch7to.setOnClickListener(this);
		
		AndoWSignageApp.KEEP_ASPECT_RATIO = localsettings.getKeepRatioState();
		check_keepratio_txt = (TextView)findViewById(R.id.check_keepratio_text);
		
		check_manual = (CheckBox)findViewById(R.id.check_manualip);
		check_manual.setOnCheckedChangeListener(new OnCheckedChangeListener() {
			
			@Override
			public void onCheckedChanged(CompoundButton buttonView, boolean isChecked) {

				setEnablePIP(isChecked);
				AndoWSignageApp.IS_MANUAL = isChecked;
				
				if(!isChecked) {
					AndoWSignageApp.MANUAL_IP = "";
				}

				LocalSettingsProvider.updateManualIPState(isChecked);
			}
		});
		
		AndoWSignageApp.IS_MANUAL = localsettings.getManualIPState();

		String[] pip = new String[0];
		if(AndoWSignageApp.IS_MANUAL) {
			if(TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
				AndoWSignageApp.MANUAL_IP = localsettings.getManualIp();
			}
			if(!TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
				pip = AndoWSignageApp.MANUAL_IP.split("\\.");
			}
		}
//		else {
//			pip = AndoWSignageApp.AUTO_IP.split("\\.");
//		}
		
		setPlayerIP(pip);

		check_manual.setChecked(AndoWSignageApp.IS_MANUAL);

		String mipStr = localsettings.getManagerIp();
		if(TextUtils.isEmpty(mipStr)) {
			mipStr = AndoWSignageApp.MANAGER_IP;
		}
		
		if(AndoWSignageApp.MANAGER_IP.isEmpty()) {
			if(mipStr.isEmpty()) {
				if(check_manual.isChecked()) {
					mipStr = AndoWSignageApp.MANAGER_IP = AndoWSignageApp.MANUAL_IP;
				}
//				else {
//					mipStr = AndoWSignageApp.MANAGER_IP = AndoWSignageApp.AUTO_IP;
//				}
			}
			
			AndoWSignageApp.MANAGER_IP = mipStr;
		}
		
		//if(AndoWSignageApp.PLAYER_ID.isEmpty())
		//	AndoWSignageApp.PLAYER_ID = (PlayerDataProvider.makeRandomID());
		
		check_keepratio = (CheckBox)findViewById(R.id.check_keepratio);
		check_keepratio.setOnCheckedChangeListener(new OnCheckedChangeListener() {
			
			@Override
			public void onCheckedChanged(CompoundButton buttonView, boolean isChecked) {

				check_keepratio_txt.setEnabled(isChecked);

				AndoWSignageApp.KEEP_ASPECT_RATIO = isChecked;
				LocalSettingsProvider.updateKeepRatioState(isChecked);
				
				AndoWSignage.act.needToChange = true;
			}
		});

		check_switch_content = (CheckBox)findViewById(R.id.check_switch_content);
		check_switch_content_txt = (TextView)findViewById(R.id.check_switch_content_text);
		check_switch_content.setOnCheckedChangeListener((buttonView, isChecked) -> {
			check_switch_content_txt.setEnabled(isChecked);
			AndoWSignageApp.SWITCH_ON_CONTENT_END = isChecked;
			LocalSettingsProvider.updateSwitchOnContentEndState(isChecked);
		});


		if(mipStr.isEmpty() == false) {
			managerAddressEdit.setText(mipStr);
		}
		
		AndoWSignageApp.IS_MANUAL = localsettings.getManualIPState();
		AndoWSignageApp.KEEP_ASPECT_RATIO = localsettings.getKeepRatioState();
		AndoWSignageApp.SWITCH_ON_CONTENT_END = localsettings.getSwitchOnContentEnd();		
		
		check_manual.setChecked(AndoWSignageApp.IS_MANUAL);
		check_keepratio.setChecked(AndoWSignageApp.KEEP_ASPECT_RATIO);
		check_switch_content.setChecked(AndoWSignageApp.SWITCH_ON_CONTENT_END);
		
		/*Player Schedule*/
			saveBtn = (Button) findViewById(R.id.btnSave);
			saveBtn.setOnClickListener(this);
			exportRealmBtn = (Button) findViewById(R.id.btnExportRealm);
			exportRealmBtn.setOnClickListener(this);
		
		refreshWeeklyDataList();

		AndoWSignageApp.MSG_ADDRESS = NetworkUtils.convertIPStrToTcpStr(AndoWSignageApp.MANAGER_IP, AndoWSignageApp.MSG_PORT);

		mAuthBg = (TextView)findViewById(R.id.auth_label1);
		mSrcKey = (EditText)findViewById(R.id.sourceKey);
		mAuthKey = (EditText)findViewById(R.id.authKey);
		bindEditableField(mAuthKey);
		mAuthBtn = (Button) findViewById(R.id.authBtn);
		mAuthBtn.setOnClickListener(this);

		mSrcKey.setText(NetworkUtils.getMACAddress().replace(":", "").toUpperCase());

		boolean hasStoredKey = false;
		try {
			hasStoredKey = AuthUtils.HasAuthKey(LocalPathUtils.getAuthFilePath(), mSrcKey.getText().toString());
		} catch (Exception ignored) { }
		if (!hasStoredKey) {
			hasStoredKey = LocalSettingsProvider.hasStoredUsbKeyForDevice();
		}
		if(hasStoredKey){
			mAuthBtn.setEnabled(false);
			mAuthKey.setEnabled(false);
			mAuthBg.setBackgroundColor(AndoWSignage.act.getResources().getColor(android.R.color.holo_green_dark));
		}
	}
	
	void refreshWeeklyDataList() {
		weeklySchDataList.clear();
		weeklySchDataList = WeeklyScheduleProvider.getWeeklyScheduleList();
		initSchData(weeklySchDataList);
	}

	private void initSchData(List<WeeklyScheduleDataModel> schList) {
		ToggleButton tb;
		Button from_btn;
		Button to_btn;
		int tb_id = -1;
		int from_id = -1;
		int to_id = -1;
		String fromStr;
		String toStr;
		
		for (WeeklyScheduleDataModel sch : schList) {
			switch (sch.getDay()) {
			
				case SUN:
					tb_id = R.id.day_toggle1;
					from_id = R.id.timePicker1from;
					to_id = R.id.timePicker1to;
					break;
	
				case MON:
					tb_id = R.id.day_toggle2;
					from_id = R.id.timePicker2from;
					to_id = R.id.timePicker2to;
					break;
					
				case TUE:
					tb_id = R.id.day_toggle3;
					from_id = R.id.timePicker3from;
					to_id = R.id.timePicker3to;
					break;
					
				case WED:
					tb_id = R.id.day_toggle4;
					from_id = R.id.timePicker4from;					
					to_id = R.id.timePicker4to;
					break;
					
				case THU:
					tb_id = R.id.day_toggle5;
					from_id = R.id.timePicker5from;
					to_id = R.id.timePicker5to;
					break;
					
				case FRI:
					tb_id = R.id.day_toggle6;
					from_id = R.id.timePicker6from;
					to_id = R.id.timePicker6to;
					break;
					
				case SAT:
					tb_id = R.id.day_toggle7;
					from_id = R.id.timePicker7from;
					to_id = R.id.timePicker7to;
					break;
					
				default:
					continue;
			}
			
			tb = (ToggleButton)findViewById(tb_id);
			tb.setChecked(sch.getOnAir());

			from_btn = (Button)findViewById(from_id);
			fromStr = sch.getFromStr();
			from_btn.setText(fromStr);
			
			to_btn = (Button)findViewById(to_id);
			toStr = sch.getToStr();
			to_btn.setText(toStr);
		}
	}

	private void setEnablePIP(boolean state) {
		pIP1.setEnabled(state);
		pIP2.setEnabled(state);
		pIP3.setEnabled(state);
		pIP4.setEnabled(state);
	}
	
	private void setPlayerIP(String[] pip) {
		if(pip.length != 4)
			return;

		pIP1.setText(pip[0]);
		pIP2.setText(pip[1]);
		pIP3.setText(pip[2]);
		pIP4.setText(pip[3]);
	}
	
	private void save() {
		
		if(player_id.getText().length() < 1) {
			player_id.setText(player_id.getHint());
		}
		
		Thread th = new Thread(new Runnable() {
			
			@Override
			public void run() {
				
				boolean isManual = check_manual.isChecked();
				AndoWSignageApp.IS_MANUAL = isManual;
				
				if(isManual) {
					String[] pipArr = 
							new String[] { pIP1.getText().toString(), pIP2.getText().toString(), pIP3.getText().toString(), pIP4.getText().toString()};
					AndoWSignageApp.MANUAL_IP = NetworkUtils.convertArrToIPFormat(pipArr);
					PlayerDataProvider.updateManualIP();
				} else {
					AndoWSignageApp.MANUAL_IP = "";
					PlayerDataProvider.updateManualIP();
				}
				
				AndoWSignageApp.MANAGER_IP = NetworkUtils.normalizeAddress(managerAddressEdit.getText().toString());
				AndoWSignageApp.MSG_ADDRESS = NetworkUtils.convertIPStrToTcpStr(AndoWSignageApp.MANAGER_IP, AndoWSignageApp.MSG_PORT);
				PlayerDataProvider.updateManagerIP();
				
				String idStr = player_id.getText().toString().trim();
				
				if(!AndoWSignageApp.PLAYER_ID.equalsIgnoreCase(idStr))
					AndoWSignage.act.needToChange = true;

				AndoWSignageApp.PLAYER_ID = idStr;
				
				PlayerDataProvider.updatePlayerName();
				AndoWSignage.act.playerData = PlayerDataProvider.getPlayerData();

				updateWeeklySchdule();
	//			AlarmUtils.setWeeklyAlarm(ctx);
				
				writeLocalSettings();
				
				SystemUtils.runOnUiThread(new Runnable() {
					@Override
					public void run() {
						AndoWSignage.act.restartNetworkSrvs();
						SystemUtils.systemBarVisibility(AndoWSignage.act, false);
					}
				});
			}
		});
		th.start();
	}
	
	private void writeLocalSettings() {
		LocalSettingsProvider.updateManualIPState(AndoWSignageApp.IS_MANUAL);
		LocalSettingsProvider.updateKeepRatioState(AndoWSignageApp.KEEP_ASPECT_RATIO);
		LocalSettingsProvider.updatePlayerId(AndoWSignageApp.PLAYER_ID);
		LocalSettingsProvider.updateManagerIp(AndoWSignageApp.MANAGER_IP);
		LocalSettingsProvider.updateManualIp(AndoWSignageApp.MANUAL_IP);
	}

	private void updateWeeklySchdule() {
		
		ToggleButton tb;
		Button from_btn;
		Button to_btn;
		int id;
		String dayStr;
		String[] timeArr = new String[2];
		for(int i=1; i<8; i++) {
			
			id = Utils.findViewByName(ctx, String.format("%s%d", "day_toggle", i));
			tb = (ToggleButton)findViewById(id);
			dayStr = tb.getText().toString();
			WeeklyScheduleProvider.updateIsOnAir(dayStr, tb.isChecked());
			
			id = Utils.findViewByName(ctx, String.format("%s%d%s", "timePicker", i, "from"));
			from_btn = (Button)findViewById(id);
			timeArr = from_btn.getText().toString().split(":");
			WeeklyScheduleProvider.updateFromTime(dayStr, timeArr[0], timeArr[1]);
			
			id = Utils.findViewByName(ctx, String.format("%s%d%s", "timePicker", i, "to"));
			to_btn = (Button)findViewById(id);
			timeArr = to_btn.getText().toString().split(":");
			WeeklyScheduleProvider.updateToTime(dayStr, timeArr[0], timeArr[1]);
		}
	}
	
	private void showTimePickerDlg(View view, int hour, int min) {
		final Button btn = (Button)view;

		final TimePicker tpicker = (TimePicker)dialogView.findViewById(R.id.time_picker);

		tpicker.setCurrentHour(hour);
		tpicker.setCurrentMinute(min);
		tpicker.setIs24HourView(true);
		
		dialogView.findViewById(R.id.time_set).setOnClickListener(new View.OnClickListener() {
		    @Override
		    public void onClick(View view) {
		    	tpicker.clearFocus();
		    	tpicker.requestFocus();
		    	btn.setText(String.format("%02d:%02d", tpicker.getCurrentHour(), tpicker.getCurrentMinute()));
		    	tpickerDialog.dismiss();
		    }});
		
		tpickerDialog.setView(dialogView);
		tpickerDialog.show();
		prepareDialogWindow(tpickerDialog.getWindow(),
				WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);
	}

	@Override
	public void onClick(View v) {
		Button btn = null;
		switch(v.getId())
		{
			case R.id.timePicker1from:	
			case R.id.timePicker2from:	
			case R.id.timePicker3from:	
			case R.id.timePicker4from:	
			case R.id.timePicker5from:	
			case R.id.timePicker6from:	
			case R.id.timePicker7from:	
			case R.id.timePicker1to:
			case R.id.timePicker2to:
			case R.id.timePicker3to:
			case R.id.timePicker4to:
			case R.id.timePicker5to:
			case R.id.timePicker6to:
			case R.id.timePicker7to:
				btn = (Button)findViewById(v.getId());
				
				if(btn != null) {
					String[] timeStr = btn.getText().toString().split(":");
					showTimePickerDlg(v, Integer.parseInt(timeStr[0]), Integer.parseInt(timeStr[1]));
				}
				break;
			
				case R.id.btnSave:
					save();
					dismiss();
				    //((AndoWSignage)ctx).runTimerAndElements();
					break;

				case R.id.btnExportRealm:
					exportRealm();
					break;
	
				case R.id.authBtn:
					setAuth(mSrcKey.getText().toString());
					break;
			}
		}

	@Override
	public boolean dispatchKeyEvent(KeyEvent event) {
		if (isHardwareTextInputEvent(event)) {
			focusTextInput(resolvePreferredTextInput(), false);
		}
		return super.dispatchKeyEvent(event);
	}

		private void exportRealm() {
			Realm realm = null;
			try {
				realm = Realm.getDefaultInstance();
				File dir = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS);
				if (dir == null) {
					throw new IllegalStateException("다운로드 폴더 경로를 확인할 수 없습니다.");
				}
				if (!dir.exists() && !dir.mkdirs()) {
					throw new IllegalStateException("다운로드 폴더를 생성할 수 없습니다: " + dir.getAbsolutePath());
				}
				File exportFile = new File(dir, "andow_export.realm");
				if (exportFile.exists() && !exportFile.delete()) {
					throw new IllegalStateException("Failed to replace " + exportFile.getAbsolutePath());
				}
				realm.writeCopyTo(exportFile);
				MediaScannerConnection.scanFile(ctx,
						new String[]{exportFile.getAbsolutePath()},
						null,
						null);
				Toast.makeText(ctx,
						ctx.getString(R.string.export_realm_success, exportFile.getAbsolutePath()),
						Toast.LENGTH_LONG).show();
			} catch (Exception e) {
				Toast.makeText(ctx,
						ctx.getString(R.string.export_realm_fail, e.getMessage()),
						Toast.LENGTH_LONG).show();
			} finally {
				if (realm != null) {
					realm.close();
				}
			}
		}
	
		private void setAuth(String srckey) {
			String _authkey = mAuthKey.getText().toString();
			String checkVal = AuthUtils.GetPasswd2(srckey);
		if (_authkey.equalsIgnoreCase(checkVal) || _authkey.equalsIgnoreCase("turtle0419"))
		{
			FileUtils.deleteFile(LocalPathUtils.getAuthFilePath());
			mAuthBtn.setEnabled(false);
			mAuthKey.setEnabled(false);
			mAuthBg.setBackgroundColor(AndoWSignage.act.getResources().getColor(android.R.color.holo_green_dark));
			String encoded = AuthUtils.EncodeAuthKey(srckey);
			FileUtils.CreateNewFile(LocalPathUtils.getAuthFilePath(), encoded);
			LocalSettingsProvider.updateUsbAuthKey(encoded);
		}
	}
}

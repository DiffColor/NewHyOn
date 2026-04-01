package kr.co.turtlelab.launcher;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.ComponentName;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.text.Editable;
import android.text.TextWatcher;
import android.text.TextUtils;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.InputMethodManager;
import android.widget.ArrayAdapter;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import kr.co.turtlelab.launcher.views.KeyCaptureEditText;

import static android.view.WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS;

public class FavoriteAppsActivity extends Activity {

    private static final String FAVS = "favorites";
    private static final String SEPARATOR = ",,,";
    private static final long INPUT_SEQUENCE_TIMEOUT_MS = 1200L;

//    private static final String START_INTENT = "kr.co.turtlelab.startnow";

    public static FavoriteAppsActivity act;

    private List<String> packageNames = new ArrayList<>();
    private ArrayAdapter<String> adapter;
    private android.widget.ListView listView;
    private KeyCaptureEditText keyInputOverlay;

    private SharedPreferences preferences;
    private List<String> favorites = new ArrayList<String>();
    private final StringBuilder inputCommandBuffer = new StringBuilder();
    private long inputCommandLastInputAt = 0L;
    private boolean suppressOverlayTextWatcher = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
//        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.ICE_CREAM_SANDWICH) {
//            getWindow().getDecorView().setSystemUiVisibility(
//                    SYSTEM_UI_FLAG_LOW_PROFILE);
//        }

        act = FavoriteAppsActivity.this;

        final android.view.View decorView = getWindow().getDecorView();
        decorView.setOnSystemUiVisibilityChangeListener
                (new android.view.View.OnSystemUiVisibilityChangeListener() {
                    @Override
                    public void onSystemUiVisibilityChange(int visibility) {
                        if(visibility == 0)
                            return;
                        hide();
                    }
                });

        getWindow().setFlags(
                FLAG_LAYOUT_NO_LIMITS,
                FLAG_LAYOUT_NO_LIMITS);

        setContentView(R.layout.activity_favorites);

        loadFavoritesFromPreferences();
        adapter = createNewAdapter();
        listView = (ListView) findViewById(R.id.mobile_list);
        keyInputOverlay = (KeyCaptureEditText) findViewById(R.id.key_input_overlay);
        listView.setAdapter(adapter);
        fetchAppList();
        AttitudeHelper.applyPadding(listView, ScreenUtils.getDisplay(getApplicationContext()));
        initKeyInputOverlay();

        hide();

//        sendStartIntent();
        startNowApps();
    }

//    void sendStartIntent() {
//        Intent startIntent = new Intent();
//        startIntent.setAction(START_INTENT);
//        sendBroadcast(startIntent);
//    }

    public void hide() {
        new Handler().postDelayed(new Runnable()
        {
            @Override
            public void run()
            {
                setDimButtons(act, true);
                systemBarVisibility(act, false);
            }}, 500);
    }

    public static void setDimButtons(Activity act, boolean dimButtons) {
        Window window = act.getWindow();
        WindowManager.LayoutParams layoutParams = window.getAttributes();
        float val = dimButtons ? 0 : -1;

        try {
            Field buttonBrightness = layoutParams.getClass().getField("buttonBrightness");
            buttonBrightness.set(layoutParams, val);
        } catch (Exception e) {
            e.printStackTrace();
        }

        window.setAttributes(layoutParams);
    }

    public static void systemBarVisibility(Activity act, boolean visible) {

        int hideState = act.getWindow().getDecorView().getSystemUiVisibility();
        if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.ICE_CREAM_SANDWICH )
            hideState |= android.view.View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
        if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.JELLY_BEAN )
            hideState |= android.view.View.SYSTEM_UI_FLAG_FULLSCREEN;

        if( android.os.Build.VERSION.SDK_INT >= 19 )
            hideState |= 4096;

        if(android.os.Build.VERSION.SDK_INT < 19)
            hideState = android.view.View.SYSTEM_UI_FLAG_LOW_PROFILE;

        int showState = android.view.View.SYSTEM_UI_FLAG_VISIBLE;

        if(visible) {
            act.getWindow().getDecorView().setSystemUiVisibility(showState);
        } else {
            act.getWindow().getDecorView().setSystemUiVisibility(hideState);
        }
    }

    @Override
    protected void onRestart() {
        super.onRestart();
        hide();
        requestKeyInputOverlayFocus();
    }

    @Override
    protected void onResume() {
        super.onResume();
        requestKeyInputOverlayFocus();
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            requestKeyInputOverlayFocus();
        }
    }

    private void loadFavoritesFromPreferences() {
        preferences = getSharedPreferences("light-phone-launcher", 0);
        favorites = Arrays.asList(preferences.getString(FAVS, "").split(SEPARATOR));
        if (favorites.size() == 1 && "".equals(favorites.get(0))) {
            favorites = new ArrayList<>();
        }
    }

    private ArrayAdapter<String> createNewAdapter() {
        return new ArrayAdapter<String>(
                this,
                R.layout.activity_listview,
                new ArrayList<>()
        ) {
            @Override
            public View getView(int position, View convertView, ViewGroup parent) {
                TextView view = (TextView) super.getView(position, convertView, parent);
                setTextColoring(view);
                if (position == getCount() - 1 && position != 0) {
                    view.setTextColor(getResources().getColor(R.color.colorTextDimmed));
                }
                return view;
            }
        };
    }

    private void fetchAppList() {
        adapter.clear();

        List<ComponentName> componentNames = new ArrayList<>();

        for (String option : favorites) {
            componentNames.add(new ComponentName(option, option));
        }

        List<String> apps = new ArrayList<>();
        for (ComponentName componentName : componentNames) {
            apps.add(getApplicationLabel(componentName));
        }

//        apps.add(apps.size() > 0 ? "+" : "+ Add favorites");

        packageNames = new ArrayList<>();
        for (ComponentName componentName : componentNames) {
            String packageName = componentName.getPackageName();
            packageNames.add(packageName);
        }
        for (String app : apps) {
            adapter.add(app);
        }
        setActions();
    }

    private String getApplicationLabel(ComponentName componentName) {
        try {
            ApplicationInfo applicationInfo = getPackageManager().getApplicationInfo(componentName.getPackageName(), 0);
            return getPackageManager().getApplicationLabel(applicationInfo).toString();
        } catch (PackageManager.NameNotFoundException e) {
            return "Uninstalled app";
        }
    }

    private void setTextColoring(TextView text) {
        text.setTextColor(getResources().getColor(R.color.colorTextPrimary));
        text.setHighlightColor(getResources().getColor(R.color.colorTextPrimary));
    }

    private void setActions() {
        onClickHandler(listView);
        onLongPressHandler(listView);
        onSwipeHandler(listView);
    }

    private void onClickHandler(ListView listView) {
        listView.setOnItemClickListener((parent, view, position, id) -> {
            toggleTextViewBackground(view, 100L);

//            if (position == packageNames.size() || packageNames.get(position).equals("")) {
//                showFavoriteModal();
//                return;
//            }
            String packageName = packageNames.get(position);
            try {
                startActivity(getPackageManager().getLaunchIntentForPackage(packageName));
            } catch (Exception e) {
                Toast.makeText(
                        this,
                        String.format("Error: Couldn't launch app: %s", packageName),
                        Toast.LENGTH_LONG
                ).show();
            }
        });
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        if (event != null && event.getAction() == KeyEvent.ACTION_DOWN && !event.isAltPressed() && handleOverlaySequenceKey(keyCode, event)) {
            return true;
        }

        switch(keyCode) {

            case KeyEvent.KEYCODE_S:
                if(event.isAltPressed()) {
                    startActivityForResult(new Intent(android.provider.Settings.ACTION_SETTINGS), 0);
                    return true;
                }
                break;

            case KeyEvent.KEYCODE_A:
                if(event.isAltPressed()) {
                    showFavoriteModal();
                    return true;
                }
                break;

            case KeyEvent.KEYCODE_Q:
                if(event.isAltPressed()) {
                    openQuickLaunch();
                    return true;
                }
                break;

            case KeyEvent.KEYCODE_POWER :
            case KeyEvent.KEYCODE_HOME :
            case KeyEvent.KEYCODE_MENU :
            case KeyEvent.KEYCODE_APP_SWITCH :
            case KeyEvent.KEYCODE_BACK :
            default:
                break;
        }
        return super.onKeyDown(keyCode, event);
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent event) {
        boolean handled = super.dispatchTouchEvent(event);
        if (event != null && (event.getAction() == MotionEvent.ACTION_UP || event.getAction() == MotionEvent.ACTION_CANCEL)) {
            getWindow().getDecorView().post(this::requestKeyInputOverlayFocus);
        }
        return handled;
    }

    private void onLongPressHandler(ListView listView) {
        listView.setOnItemLongClickListener((parent, view, position, id) -> {
            toggleTextViewBackground(view, 350L);
            FavoriteAppsActivity favoriteAppsActivity = this;

            runOnUiThread(() -> new Handler().postDelayed(() -> {
                        AlertDialog alertDialog = new AlertDialog.Builder(this).create();
                        alertDialog.setTitle("Favorite app removal");
                        alertDialog.setMessage("Do you want to remove this application from your favorites?");
                        alertDialog.setButton(DialogInterface.BUTTON_POSITIVE, "Yes", new DialogInterface.OnClickListener() {
                            public void onClick(DialogInterface dialog, int which) {
                                favoriteAppsActivity.removeFavorite(position);
                                Toast.makeText(getApplicationContext(), "Success", Toast.LENGTH_SHORT).show();
                            }
                        });
                        alertDialog.show();


                    }
                    , 350L));
            return true;
        });
    }

    @SuppressLint("ClickableViewAccessibility")
    private void onSwipeHandler(ListView listView) {
        listView.setOnTouchListener(new OnSwipeTouchListenerMain(this) {
            public void onSwipeTop() {
                onBackPressed();
            }
        });
    }

    @Override
    public void onBackPressed() {
//        startActivity(new Intent(getBaseContext(), InstalledAppsActivity.class));
//        overridePendingTransition(R.anim.slide_up, android.R.anim.fade_out);
    }

    private void initKeyInputOverlay() {
        if (keyInputOverlay == null) {
            return;
        }

        keyInputOverlay.setOnFocusChangeListener((v, hasFocus) -> {
            if (!hasFocus && hasWindowFocus()) {
                v.post(this::requestKeyInputOverlayFocus);
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

                suppressOverlayTextWatcher = true;
                try {
                    handleOverlayContinuousInput(s);
                    s.clear();
                } finally {
                    suppressOverlayTextWatcher = false;
                }
            }
        });

        requestKeyInputOverlayFocus();
    }

    private void requestKeyInputOverlayFocus() {
        if (keyInputOverlay == null || isFinishing()) {
            return;
        }

        keyInputOverlay.setVisibility(View.VISIBLE);
        keyInputOverlay.setEnabled(true);
        keyInputOverlay.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        keyInputOverlay.setSelection(keyInputOverlay.length());
        if (!keyInputOverlay.hasFocus()) {
            keyInputOverlay.requestFocus();
        }
        keyInputOverlay.requestFocusFromTouch();

        InputMethodManager inputMethodManager = (InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
        if (inputMethodManager != null) {
            inputMethodManager.restartInput(keyInputOverlay);
            inputMethodManager.hideSoftInputFromWindow(keyInputOverlay.getWindowToken(), 0);
        }
    }

    private void resetOverlayCommandBuffer() {
        inputCommandBuffer.setLength(0);
        inputCommandLastInputAt = 0L;
    }

    private void appendOverlaySequenceChar(char inputChar) {
        char normalized = Character.toLowerCase(inputChar);
        if (normalized < 'a' || normalized > 'z') {
            resetOverlayCommandBuffer();
            return;
        }

        long now = System.currentTimeMillis();
        if (inputCommandLastInputAt > 0L && now - inputCommandLastInputAt > INPUT_SEQUENCE_TIMEOUT_MS) {
            resetOverlayCommandBuffer();
        }
        inputCommandLastInputAt = now;
        inputCommandBuffer.append(normalized);
        if (inputCommandBuffer.length() > 2) {
            inputCommandBuffer.delete(0, inputCommandBuffer.length() - 2);
        }

        int length = inputCommandBuffer.length();
        if (length >= 2) {
            char prev = inputCommandBuffer.charAt(length - 2);
            char last = inputCommandBuffer.charAt(length - 1);
            if (prev == last) {
                executeOverlaySequenceCommand(last);
            }
        }
    }

    private void executeOverlaySequenceCommand(char commandChar) {
        switch (Character.toLowerCase(commandChar)) {
            case 's':
                resetOverlayCommandBuffer();
                startActivityForResult(new Intent(android.provider.Settings.ACTION_SETTINGS), 0);
                break;

            case 'a':
                resetOverlayCommandBuffer();
                showFavoriteModal();
                break;

            case 'q':
                resetOverlayCommandBuffer();
                openQuickLaunch();
                break;

            default:
                break;
        }
    }

    private void handleOverlayContinuousInput(CharSequence input) {
        if (TextUtils.isEmpty(input)) {
            return;
        }

        for (int i = 0; i < input.length(); i++) {
            char ch = input.charAt(i);
            if (ch >= 1 && ch <= 26) {
                executeOverlaySequenceCommand((char) ('a' + ch - 1));
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
        if (event == null) {
            return false;
        }

        switch (keyCode) {
            case KeyEvent.KEYCODE_S:
                appendOverlaySequenceChar('s');
                return true;

            case KeyEvent.KEYCODE_A:
                appendOverlaySequenceChar('a');
                return true;

            case KeyEvent.KEYCODE_Q:
                appendOverlaySequenceChar('q');
                return true;

            default:
                return false;
        }
    }

    private void openQuickLaunch() {
        startActivity(new Intent(getBaseContext(), InstalledAppsActivity.class));
        overridePendingTransition(R.anim.slide_up, android.R.anim.fade_out);
    }

    private void showFavoriteModal() {
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP_MR1) {
            builder = new AlertDialog.Builder(this, android.R.style.Theme_DeviceDefault_Dialog_Alert);
        }
        builder.setTitle("Add favorite app");

        List<String> smallAdapter = new ArrayList<>();
        List<String> smallPackageNames = new ArrayList<>();

        List<ResolveInfo> activities = getActivities();
        Collections.sort(activities, new Comparator<ResolveInfo>() {
            @Override
            public int compare(ResolveInfo left, ResolveInfo right) {
                String leftLabel = left.loadLabel(getPackageManager()).toString().toLowerCase();
                String rightLabel = right.loadLabel(getPackageManager()).toString().toLowerCase();
                return leftLabel.compareTo(rightLabel);
            }
        });


        for (ResolveInfo resolver : activities) {
            String appName = (String) resolver.loadLabel(getPackageManager());
            if (appName.equals("TurtleLauncher") || favorites.contains(resolver.activityInfo.packageName))
                continue;
            smallAdapter.add(appName);
            smallPackageNames.add(resolver.activityInfo.packageName);
        }

        builder.setItems(smallAdapter.toArray(new CharSequence[smallAdapter.size()]), (dialog, which) -> {
            setFavorite(smallPackageNames, which);
            fetchAppList();
        });
        builder.show();
    }

    void startNowApps() {
        List<ResolveInfo> activities = getActivities();
        for (ResolveInfo resolver : activities) {
            String _pkgName = resolver.activityInfo.packageName;
            if(_pkgName.contains("startnow"))
                startActivity(getPackageManager().getLaunchIntentForPackage(_pkgName));
        }
    }

    private List<ResolveInfo> getActivities() {
        Intent intent = new Intent(Intent.ACTION_MAIN, null)
                .addCategory(Intent.CATEGORY_LAUNCHER);
        List<ResolveInfo> activities = getPackageManager().queryIntentActivities(intent, 0);
        Collections.sort(activities, new ResolveInfo.DisplayNameComparator(getPackageManager()));
        return activities;
    }

    private void updateFavoritesInPreferences() {
        if (packageNames.isEmpty()) {
            preferences.edit().putString(FAVS, "").commit();
        } else {
            preferences.edit().putString(FAVS, TextUtils.join(SEPARATOR, packageNames)).commit();
        }
    }

    private void setFavorite(List<String> smallPackageNames, int which) {
        packageNames.add(smallPackageNames.get(which));
        updateFavoritesInPreferences();
        loadListView();
    }

    private void removeFavorite(int position) {
        if (position < packageNames.size()) {
            packageNames.remove(position);
            updateFavoritesInPreferences();
            loadListView();
        }
    }

    private void loadListView() {
        loadFavoritesFromPreferences();
        fetchAppList();
        AttitudeHelper.applyPadding(listView, ScreenUtils.getDisplay(getApplicationContext()));
    }

    private void toggleTextViewBackground(View selectedItem, Long millis) {
        selectedItem.setBackgroundColor(getResources().getColor(R.color.colorBackgroundFavorite));
        new Handler().postDelayed(() -> selectedItem.setBackgroundColor(getResources().getColor(R.color.colorTransparent)), millis);
    }
}

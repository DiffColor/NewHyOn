package kr.co.turtlelab.quber.ntpsettings;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.os.Bundle;
import android.provider.Settings;
import android.text.TextUtils;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.content.Context;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;

public final class MainActivity extends Activity {
    private TextView currentServerView;
    private TextView autoTimeView;
    private TextView statusView;
    private EditText serverInput;
    private Button refreshButton;
    private Button applyButton;
    private QuberNtpClient client;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        currentServerView = findViewById(R.id.current_server);
        autoTimeView = findViewById(R.id.auto_time_status);
        statusView = findViewById(R.id.status);
        serverInput = findViewById(R.id.server_input);
        refreshButton = findViewById(R.id.refresh_button);
        applyButton = findViewById(R.id.apply_button);
        Button dateSettingsButton = findViewById(R.id.date_settings_button);

        client = new QuberNtpClient(this);
        refreshButton.setOnClickListener(view -> refreshNtpServer());
        applyButton.setOnClickListener(view -> confirmApply());
        dateSettingsButton.setOnClickListener(view -> openDateSettings());

        setAgentControlsEnabled(false);
        updateAutoTimeStatus();
        client.connect((connected, message) -> {
            statusView.setText(message);
            setAgentControlsEnabled(connected);
            if (connected) refreshNtpServer();
        });
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateAutoTimeStatus();
    }

    @Override
    protected void onDestroy() {
        client.disconnect();
        super.onDestroy();
    }

    private void refreshNtpServer() {
        setAgentControlsEnabled(false);
        statusView.setText(R.string.status_reading);
        client.readNtpServer((success, server, message) -> {
            setAgentControlsEnabled(true);
            statusView.setText(message);
            if (!success) return;

            String normalized = server == null ? "" : server.trim();
            currentServerView.setText(
                    TextUtils.isEmpty(normalized) ? getString(R.string.not_configured) : normalized);
            serverInput.setText(normalized);
            serverInput.setSelection(serverInput.length());
        });
    }

    private void confirmApply() {
        hideKeyboard();
        String server = serverInput.getText().toString().trim();
        if (!NtpServerValidator.isValid(server)) {
            serverInput.setError(getString(R.string.invalid_server));
            serverInput.requestFocus();
            return;
        }

        new AlertDialog.Builder(this)
                .setTitle(R.string.confirm_title)
                .setMessage(getString(R.string.confirm_message, server))
                .setNegativeButton(android.R.string.cancel, null)
                .setPositiveButton(R.string.apply_and_reboot, (dialog, which) -> applyServer(server))
                .show();
    }

    private void applyServer(String server) {
        setAgentControlsEnabled(false);
        statusView.setText(R.string.status_sending);
        boolean sent = client.updateNtpServer(server);
        if (sent) {
            statusView.setText(R.string.status_rebooting);
        } else {
            statusView.setText(R.string.status_send_failed);
            setAgentControlsEnabled(true);
        }
    }

    private void updateAutoTimeStatus() {
        boolean enabled = Settings.Global.getInt(
                getContentResolver(), Settings.Global.AUTO_TIME, 0) == 1;
        autoTimeView.setText(enabled ? R.string.auto_time_on : R.string.auto_time_off);
        autoTimeView.setTextColor(getResources().getColor(
                enabled ? android.R.color.holo_green_dark : android.R.color.holo_red_dark));
    }

    private void openDateSettings() {
        try {
            startActivity(new Intent(Settings.ACTION_DATE_SETTINGS));
        } catch (ActivityNotFoundException error) {
            startActivity(new Intent(Settings.ACTION_SETTINGS));
        }
    }

    private void setAgentControlsEnabled(boolean enabled) {
        refreshButton.setEnabled(enabled);
        applyButton.setEnabled(enabled);
        serverInput.setEnabled(enabled);
    }

    private void hideKeyboard() {
        View focused = getCurrentFocus();
        if (focused == null) return;
        InputMethodManager manager =
                (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (manager != null) manager.hideSoftInputFromWindow(focused.getWindowToken(), 0);
    }
}

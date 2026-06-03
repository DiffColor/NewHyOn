package kr.co.turtlelab.andowsignage.auth;

import android.content.Context;
import android.text.TextUtils;
import android.util.Log;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public final class LegacyAuthCleanup {
    private static final String TAG = "LegacyAuthCleanup";

    private LegacyAuthCleanup() {
    }

    public static void clearLocalAndRemoteOnce(Context context) {
        clearLocal();
        clearRemoteOnce(context);
    }

    public static void clearLocal() {
        PlayerDataProvider.updatePlayerAuthInfo("", "");
    }

    public static void clearRemoteOnce(Context context) {
        String host = resolveRethinkHost();
        String playerName = LicenseAuthManager.resolveDeviceName(context);
        if (TextUtils.isEmpty(host) || TextUtils.isEmpty(playerName)) {
            return;
        }

        try {
            RethinkDbClient client = RethinkDbClient.getInstance();
            client.updateHost(host);
            client.syncCurrentDeviceInfoClearingAuthOnce(playerName.trim());
        } catch (Throwable throwable) {
            Log.w(TAG, "Remote legacy auth cleanup skipped: " + throwable.getMessage());
        }
    }

    private static String resolveRethinkHost() {
        String host = LocalSettingsProvider.getDataServerIp();
        if (TextUtils.isEmpty(host)) {
            host = AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)
                    ? AndoWSignageApp.MANUAL_IP
                    : AndoWSignageApp.MANAGER_IP;
        }
        return NetworkUtils.normalizeAddress(host);
    }
}

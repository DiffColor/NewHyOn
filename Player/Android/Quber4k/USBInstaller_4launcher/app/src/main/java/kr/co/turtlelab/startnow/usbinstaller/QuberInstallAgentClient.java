package kr.co.turtlelab.startnow.usbinstaller;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.IBinder;
import android.os.RemoteException;
import android.util.Log;

import org.json.JSONException;
import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import net.quber.qubersignageagent.IQuberCallback;
import net.quber.qubersignageagent.IQuberManager;

public final class QuberInstallAgentClient {

    private static final String TAG = "QuberInstallAgent";
    private static final String ACTION_QUBER_AGENT = "net.quber.qubersignageagent.QUBER_AGENT_SERVICE";
    private static final String PACKAGE_QUBER_AGENT = "net.quber.qubersignageagent";
    private static final String CMD_INSTALL_APK = "215021";
    private static final long CONNECT_TIMEOUT_MS = 3000L;

    private final Context appContext;
    private final CountDownLatch connectLatch = new CountDownLatch(1);

    private IQuberManager manager;
    private boolean bound;

    private QuberInstallAgentClient(Context context) {
        this.appContext = context.getApplicationContext();
    }

    public static boolean requestInstall(Context context, String apkPath) {
        if (context == null || apkPath == null || apkPath.length() < 1) {
            return false;
        }

        QuberInstallAgentClient client = new QuberInstallAgentClient(context);
        return client.sendInstallRequest(apkPath);
    }

    private boolean sendInstallRequest(String apkPath) {
        try {
            if (!bindAndAwait()) {
                return false;
            }
            return sendInstallCommand(apkPath);
        } finally {
            close();
        }
    }

    private boolean bindAndAwait() {
        Intent intent = new Intent(ACTION_QUBER_AGENT);
        intent.setPackage(PACKAGE_QUBER_AGENT);
        bound = appContext.bindService(intent, connection, Context.BIND_AUTO_CREATE);
        if (!bound) {
            Log.w(TAG, "bindService failed");
            return false;
        }

        try {
            return connectLatch.await(CONNECT_TIMEOUT_MS, TimeUnit.MILLISECONDS) && manager != null;
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return false;
        }
    }

    private boolean sendInstallCommand(String apkPath) {
        if (manager == null) {
            return false;
        }

        String payload = buildPayload(apkPath);
        if (payload == null) {
            return false;
        }

        try {
            return manager.multiSendRequestCmd(appContext.getPackageName(), payload);
        } catch (Throwable multiError) {
            Log.w(TAG, "multiSendRequestCmd failed. fallback to single binder", multiError);
            try {
                return manager.sendRequestCmd(payload);
            } catch (Throwable singleError) {
                Log.e(TAG, "sendRequestCmd failed", singleError);
                return false;
            }
        }
    }

    private String buildPayload(String apkPath) {
        JSONObject payload = new JSONObject();
        JSONObject params = new JSONObject();

        try {
            payload.put("requestId", new SimpleDateFormat("yyyyMMddHHmmssSSS", Locale.US).format(new Date()));
            payload.put("cmdCode", CMD_INSTALL_APK);
            params.put("path", apkPath);
            payload.put("params", params);
            return payload.toString();
        } catch (JSONException e) {
            Log.e(TAG, "Failed to build install payload", e);
            return null;
        }
    }

    private void close() {
        IQuberManager currentManager = manager;
        manager = null;

        if (currentManager != null) {
            try {
                currentManager.multiClose(appContext.getPackageName());
            } catch (Throwable ignore) {
            }
        }

        if (bound) {
            try {
                appContext.unbindService(connection);
            } catch (Throwable ignore) {
            }
            bound = false;
        }
    }

    private final ServiceConnection connection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName name, IBinder service) {
            manager = IQuberManager.Stub.asInterface(service);
            IQuberManager currentManager = manager;

            if (currentManager != null) {
                try {
                    currentManager.multiAgentResponse(appContext.getPackageName(), callback);
                } catch (Throwable multiError) {
                    Log.w(TAG, "multiAgentResponse failed. fallback to single binder", multiError);
                    try {
                        currentManager.agentResponse(callback);
                    } catch (RemoteException singleError) {
                        Log.e(TAG, "agentResponse registration failed", singleError);
                    }
                }
            }
            connectLatch.countDown();
        }

        @Override
        public void onServiceDisconnected(ComponentName name) {
            manager = null;
        }
    };

    private final IQuberCallback.Stub callback = new IQuberCallback.Stub() {
        @Override
        public void responseListener(String jsonMsg) {
            Log.d(TAG, "Quber response: " + jsonMsg);
        }
    };
}

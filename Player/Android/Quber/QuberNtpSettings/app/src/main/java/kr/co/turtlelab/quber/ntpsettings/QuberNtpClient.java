package kr.co.turtlelab.quber.ntpsettings;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.RemoteException;

import net.quber.qubersignageagent.IQuberCallback;
import net.quber.qubersignageagent.IQuberManager;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;

final class QuberNtpClient {
    interface ConnectionCallback {
        void onConnectionChanged(boolean connected, String message);
    }

    interface ReadCallback {
        void onResult(boolean success, String server, String message);
    }

    private static final String ACTION_QUBER_AGENT =
            "net.quber.qubersignageagent.QUBER_AGENT_SERVICE";
    private static final String PACKAGE_QUBER_AGENT = "net.quber.qubersignageagent";
    private static final String CMD_READ_NTP_SERVER = "211046";
    private static final String CMD_UPDATE_NTP_SERVER = "213028";
    private static final long RESPONSE_TIMEOUT_MS = 3000L;

    private final Context context;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final AtomicLong requestSequence = new AtomicLong();
    private final Map<String, ReadCallback> pendingReads = new ConcurrentHashMap<>();

    private IQuberManager manager;
    private boolean bound;
    private boolean binding;
    private ConnectionCallback connectionCallback;

    QuberNtpClient(Context context) {
        this.context = context.getApplicationContext();
    }

    void connect(ConnectionCallback callback) {
        connectionCallback = callback;
        if (manager != null) {
            callback.onConnectionChanged(true, "QUBER Agent에 연결되었습니다.");
            return;
        }
        if (binding) return;

        Intent intent = new Intent(ACTION_QUBER_AGENT);
        intent.setPackage(PACKAGE_QUBER_AGENT);
        binding = true;
        try {
            bound = context.bindService(intent, serviceConnection, Context.BIND_AUTO_CREATE);
        } catch (RuntimeException error) {
            bound = false;
        }
        if (!bound) {
            binding = false;
            callback.onConnectionChanged(false, "QUBER Agent 서비스를 찾을 수 없습니다.");
        }
    }

    void disconnect() {
        pendingReads.clear();
        manager = null;
        binding = false;
        if (bound) {
            try {
                context.unbindService(serviceConnection);
            } catch (IllegalArgumentException ignored) {
            }
        }
        bound = false;
    }

    void readNtpServer(ReadCallback callback) {
        IQuberManager current = manager;
        if (current == null) {
            callback.onResult(false, null, "QUBER Agent가 연결되지 않았습니다.");
            return;
        }

        String requestId = nextRequestId();
        pendingReads.put(requestId, callback);
        boolean sent = send(current, buildPayload(requestId, CMD_READ_NTP_SERVER, null));
        if (!sent) {
            pendingReads.remove(requestId);
            callback.onResult(false, null, "NTP 서버 조회 요청 전송에 실패했습니다.");
            return;
        }

        mainHandler.postDelayed(() -> {
            ReadCallback pending = pendingReads.remove(requestId);
            if (pending != null) {
                pending.onResult(false, null, "NTP 서버 조회 응답 시간이 초과되었습니다.");
            }
        }, RESPONSE_TIMEOUT_MS);
    }

    boolean updateNtpServer(String server) {
        IQuberManager current = manager;
        if (current == null) return false;

        JSONObject params = new JSONObject();
        try {
            params.put("server", server);
        } catch (JSONException impossible) {
            return false;
        }
        return send(current, buildPayload(nextRequestId(), CMD_UPDATE_NTP_SERVER, params));
    }

    private boolean send(IQuberManager current, JSONObject payload) {
        if (payload == null) return false;
        try {
            return current.sendRequestCmd(payload.toString());
        } catch (RemoteException | RuntimeException error) {
            return false;
        }
    }

    private JSONObject buildPayload(String requestId, String command, JSONObject params) {
        JSONObject payload = new JSONObject();
        try {
            payload.put("requestId", requestId);
            payload.put("cmdCode", command);
            if (params != null) payload.put("params", params);
            return payload;
        } catch (JSONException impossible) {
            return null;
        }
    }

    private String nextRequestId() {
        return Long.toString(System.currentTimeMillis()) + "-" + requestSequence.incrementAndGet();
    }

    private final ServiceConnection serviceConnection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName name, IBinder service) {
            binding = false;
            manager = IQuberManager.Stub.asInterface(service);
            try {
                manager.agentResponse(agentCallback);
            } catch (RemoteException error) {
                manager = null;
            }
            ConnectionCallback callback = connectionCallback;
            if (callback != null) {
                callback.onConnectionChanged(
                        manager != null,
                        manager != null
                                ? "QUBER Agent에 연결되었습니다."
                                : "QUBER Agent 응답 등록에 실패했습니다.");
            }
        }

        @Override
        public void onServiceDisconnected(ComponentName name) {
            manager = null;
            binding = false;
            ConnectionCallback callback = connectionCallback;
            if (callback != null) {
                callback.onConnectionChanged(false, "QUBER Agent 연결이 끊어졌습니다.");
            }
        }
    };

    private final IQuberCallback.Stub agentCallback = new IQuberCallback.Stub() {
        @Override
        public void responseListener(String jsonMessage) {
            try {
                JSONObject response = new JSONObject(jsonMessage);
                String responseId = response.optString(
                        "responseId", response.optString("requestId", ""));
                ReadCallback callback = pendingReads.remove(responseId);
                if (callback == null) return;

                boolean success = "2000".equals(response.optString("resultCode"));
                JSONObject params = response.optJSONObject("params");
                String server = params == null ? "" : params.optString("server", "");
                mainHandler.post(() -> callback.onResult(
                        success,
                        server,
                        success ? "NTP 서버 주소를 조회했습니다." : "QUBER Agent가 조회를 거부했습니다."));
            } catch (JSONException ignored) {
            }
        }
    };
}

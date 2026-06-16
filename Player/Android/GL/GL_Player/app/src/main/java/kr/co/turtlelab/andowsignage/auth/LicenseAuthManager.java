package kr.co.turtlelab.andowsignage.auth;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.text.TextUtils;
import android.util.Log;

import org.json.JSONObject;

import java.util.Locale;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;

public final class LicenseAuthManager {
    private static final String TAG = "LicenseAuthManager";

    public static final int PRODUCT_ID = 7;
    public static final String API_BASE_URL = "https://licensehub.ilycode.app";

    private static final String AUTH_APP_PACKAGE = "com.licensehub.deviceauth.app";
    private static final String VALIDATE_RECEIVER_CLASS = "com.licensehub.deviceauth.app.ValidateRequestReceiver";

    private static final String ACTION_AUTHENTICATE = "com.licensehub.deviceauth.AUTHENTICATE";
    private static final String ACTION_VALIDATE_BACKGROUND = "com.licensehub.deviceauth.VALIDATE_BACKGROUND";

    private static final String EXTRA_REQUEST_JSON = "com.licensehub.deviceauth.extra.REQUEST_JSON";
    private static final String EXTRA_CALLBACK_INTENT = "com.licensehub.deviceauth.extra.CALLBACK_INTENT";
    private static final String EXTRA_RESPONSE_JSON = "com.licensehub.deviceauth.extra.RESPONSE_JSON";
    private static final String EXTRA_RESPONSE_KIND = "com.licensehub.deviceauth.extra.RESPONSE_KIND";

    private static final String RESPONSE_KIND_AUTHENTICATE = "AUTHENTICATE";
    private static final String RESPONSE_KIND_VALIDATE = "VALIDATE";

    private static final long AUTH_CALLBACK_TIMEOUT_MILLIS = 10 * 60 * 1000L;
    private static final long VALIDATE_CALLBACK_TIMEOUT_MILLIS = 5000L;
    private static final int ANDROID_SDK_S = 31;
    private static final int PENDING_INTENT_FLAG_MUTABLE = 0x02000000;

    private LicenseAuthManager() {
    }

    public interface AuthCallback {
        void onResult(AuthResult result);
    }

    public interface ValidationCallback {
        void onResult(ValidationResult result);
    }

    public static final class AuthResult {
        private final String requestId;
        private final String status;
        private final String message;
        private final String rawJson;
        private final String deviceId;
        private final String deviceFingerprint;
        private final String licenseToken;

        public AuthResult(String requestId, String status, String message) {
            this(requestId, status, message, "", "", "", "");
        }

        public AuthResult(String requestId,
                          String status,
                          String message,
                          String rawJson,
                          String deviceId,
                          String deviceFingerprint,
                          String licenseToken) {
            this.requestId = requestId;
            this.status = status;
            this.message = message;
            this.rawJson = rawJson;
            this.deviceId = deviceId;
            this.deviceFingerprint = deviceFingerprint;
            this.licenseToken = licenseToken;
        }

        public String getRequestId() {
            return requestId;
        }

        public String getStatus() {
            return status;
        }

        public String getMessage() {
            return message;
        }

        public String getRawJson() {
            return rawJson;
        }

        public String getDeviceId() {
            return deviceId;
        }

        public String getDeviceFingerprint() {
            return deviceFingerprint;
        }

        public String getLicenseToken() {
            return licenseToken;
        }

        public boolean isOnlineVerified() {
            return "ONLINE_VERIFIED".equalsIgnoreCase(status);
        }

        public boolean isVerified() {
            return isOnlineVerified()
                    || "OFFLINE_VERIFIED".equalsIgnoreCase(status);
        }

        public boolean hasCallbackAuthData() {
            return !TextUtils.isEmpty(rawJson)
                    && !TextUtils.isEmpty(deviceFingerprint)
                    && !TextUtils.isEmpty(status);
        }
    }

    public static final class ValidationResult {
        private final String requestId;
        private final boolean valid;
        private final String reason;
        private final String serverStatus;
        private final boolean localLicenseDiscarded;
        private final boolean serverChecked;
        private final boolean usedOfflineFallback;
        private final String payloadJson;
        private final String rawJson;

        public ValidationResult(String requestId, boolean valid, String reason, String serverStatus) {
            this(requestId, valid, reason, serverStatus, false, "", "");
        }

        public ValidationResult(String requestId, boolean valid, String reason, String serverStatus, boolean localLicenseDiscarded) {
            this(requestId, valid, reason, serverStatus, localLicenseDiscarded, "", "");
        }

        public ValidationResult(String requestId,
                                boolean valid,
                                String reason,
                                String serverStatus,
                                boolean localLicenseDiscarded,
                                String payloadJson,
                                String rawJson) {
            this(requestId, valid, reason, serverStatus, localLicenseDiscarded, false, false, payloadJson, rawJson);
        }

        public ValidationResult(String requestId,
                                boolean valid,
                                String reason,
                                String serverStatus,
                                boolean localLicenseDiscarded,
                                boolean serverChecked,
                                boolean usedOfflineFallback,
                                String payloadJson,
                                String rawJson) {
            this.requestId = requestId;
            this.valid = valid;
            this.reason = reason;
            this.serverStatus = serverStatus;
            this.localLicenseDiscarded = localLicenseDiscarded;
            this.serverChecked = serverChecked;
            this.usedOfflineFallback = usedOfflineFallback;
            this.payloadJson = payloadJson;
            this.rawJson = rawJson;
        }

        public String getRequestId() {
            return requestId;
        }

        public boolean isValid() {
            return valid;
        }

        public String getReason() {
            return reason;
        }

        public String getServerStatus() {
            return serverStatus;
        }

        public boolean isLocalLicenseDiscarded() {
            return localLicenseDiscarded;
        }

        public boolean isServerChecked() {
            return serverChecked;
        }

        public boolean isUsedOfflineFallback() {
            return usedOfflineFallback;
        }

        public String getPayloadJson() {
            return payloadJson;
        }

        public String getRawJson() {
            return rawJson;
        }
    }

    public static void authenticate(Activity activity, String deviceName, AuthCallback callback) {
        final Context appContext = activity.getApplicationContext();
        final String requestId = UUID.randomUUID().toString();

        JSONObject request = new JSONObject();
        try {
            request.put("requestId", requestId);
            request.put("apiBaseUrl", API_BASE_URL);
            request.put("productId", PRODUCT_ID);
            request.put("deviceName", safeString(deviceName));
        } catch (Exception ignored) {
        }

        dispatchActivity(
                activity,
                appContext,
                ACTION_AUTHENTICATE,
                RESPONSE_KIND_AUTHENTICATE,
                requestId,
                request.toString(),
                callback);
    }

    public static void validate(Context context, ValidationCallback callback) {
        final Context appContext = context.getApplicationContext();
        final String requestId = UUID.randomUUID().toString();

        JSONObject request = new JSONObject();
        try {
            request.put("requestId", requestId);
            request.put("apiBaseUrl", API_BASE_URL);
            request.put("productId", PRODUCT_ID);
            request.put("licenseToken", "");
            request.put("licenseFilePath", "");
            request.put("enableServerValidation", true);
            request.put("discardLocalLicenseWhenServerInvalid", true);
            request.put("serverValidationTimeoutMilliseconds", 3000);
        } catch (Exception ignored) {
        }

        dispatchBroadcast(
                appContext,
                ACTION_VALIDATE_BACKGROUND,
                RESPONSE_KIND_VALIDATE,
                requestId,
                request.toString(),
                callback);
    }

    public static boolean shouldReauthenticate(ValidationResult result) {
        if (result == null || result.isValid()) {
            return false;
        }
        if (result.isLocalLicenseDiscarded()) {
            return true;
        }

        String status = safeString(result.getServerStatus()).toLowerCase(Locale.ROOT);
        if (status.contains("expired")
                || status.contains("revoke")
                || status.contains("invalid")
                || status.contains("inactive")
                || status.contains("deactivate")) {
            return true;
        }

        String reason = safeString(result.getReason()).toLowerCase(Locale.ROOT);
        return reason.contains("만료")
                || reason.contains("폐기")
                || reason.contains("비활성")
                || reason.contains("expired")
                || reason.contains("revok")
                || reason.contains("inactive");
    }

    public static boolean launchReauthenticate(Context context, String deviceName) {
        Context appContext = context.getApplicationContext();
        if (!isAuthAppInstalled(appContext) || !canHandleActivity(appContext, ACTION_AUTHENTICATE)) {
            return false;
        }

        JSONObject request = new JSONObject();
        try {
            request.put("requestId", UUID.randomUUID().toString());
            request.put("apiBaseUrl", API_BASE_URL);
            request.put("productId", PRODUCT_ID);
            request.put("deviceName", safeString(deviceName));
        } catch (Exception ignored) {
        }

        Intent requestIntent = new Intent(ACTION_AUTHENTICATE)
                .setPackage(AUTH_APP_PACKAGE)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                .putExtra(EXTRA_REQUEST_JSON, request.toString());

        try {
            appContext.startActivity(requestIntent);
            return true;
        } catch (Throwable ignored) {
            return false;
        }
    }

    public static String resolveDeviceName(Context context) {
        String playerId = LocalSettingsProvider.getPlayerId();
        if (!TextUtils.isEmpty(playerId)) {
            return playerId.trim();
        }

        String appPlayerId = AndoWSignageApp.PLAYER_ID;
        if (!TextUtils.isEmpty(appPlayerId)) {
            return appPlayerId.trim();
        }

        String model = Build.MODEL;
        if (TextUtils.isEmpty(model)) {
            return "AndroidDevice";
        }
        return model.trim();
    }

    private static void dispatchActivity(
            Activity activity,
            Context appContext,
            String action,
            String expectedResponseKind,
            String requestId,
            String requestJson,
            AuthCallback callback) {
        if (!isAuthAppInstalled(appContext)) {
            safeInvokeAuthCallback(callback, new AuthResult(requestId, "FAILED", "인증 앱이 설치되어 있지 않습니다."));
            return;
        }
        if (!canHandleActivity(appContext, action)) {
            safeInvokeAuthCallback(callback, new AuthResult(requestId, "FAILED", "인증 앱 실행 액션을 처리할 수 없습니다."));
            return;
        }

        String callbackAction = appContext.getPackageName() + ".DEVICE_AUTH_RESULT." + requestId;
        Intent callbackIntent = new Intent(callbackAction).setPackage(appContext.getPackageName());

        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                appContext,
                requestId.hashCode(),
                callbackIntent,
                buildPendingIntentFlags());

        final Handler mainHandler = new Handler(Looper.getMainLooper());
        final AtomicBoolean finished = new AtomicBoolean(false);
        final BroadcastReceiver[] receiverRef = new BroadcastReceiver[1];

        BroadcastReceiver receiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                if (finished.get()) {
                    return;
                }

                String kind = safeString(intent != null ? intent.getStringExtra(EXTRA_RESPONSE_KIND) : "");
                if (!expectedResponseKind.equals(kind)) {
                    return;
                }

                String raw = safeString(intent != null ? intent.getStringExtra(EXTRA_RESPONSE_JSON) : "");
                if (!finished.compareAndSet(false, true)) {
                    return;
                }

                safelyUnregister(appContext, receiverRef[0]);
                mainHandler.removeCallbacksAndMessages(null);
                safeInvokeAuthCallback(callback, parseAuthResult(requestId, raw));
            }
        };
        receiverRef[0] = receiver;

        if (!registerReceiver(appContext, receiver, callbackAction)) {
            safeInvokeAuthCallback(callback, new AuthResult(requestId, "FAILED", "콜백 리시버 등록 실패"));
            return;
        }

        mainHandler.postDelayed(new Runnable() {
            @Override
            public void run() {
                if (!finished.compareAndSet(false, true)) {
                    return;
                }
                safelyUnregister(appContext, receiverRef[0]);
                safeInvokeAuthCallback(callback, new AuthResult(requestId, "FAILED", "인증 앱 응답 시간 초과"));
            }
        }, AUTH_CALLBACK_TIMEOUT_MILLIS);

        Intent requestIntent = new Intent(action)
                .setPackage(AUTH_APP_PACKAGE)
                .putExtra(EXTRA_REQUEST_JSON, requestJson)
                .putExtra(EXTRA_CALLBACK_INTENT, pendingIntent);

        try {
            activity.startActivity(requestIntent);
        } catch (Throwable throwable) {
            if (!finished.compareAndSet(false, true)) {
                return;
            }
            safelyUnregister(appContext, receiverRef[0]);
            mainHandler.removeCallbacksAndMessages(null);
            safeInvokeAuthCallback(callback, new AuthResult(requestId, "FAILED", "인증 앱 실행 실패: " + safeThrowableMessage(throwable)));
        }
    }

    private static void dispatchBroadcast(
            final Context appContext,
            String action,
            String expectedResponseKind,
            String requestId,
            String requestJson,
            ValidationCallback callback) {
        if (!isAuthAppInstalled(appContext)) {
            safeInvokeValidationCallback(callback, new ValidationResult(requestId, false, "인증 앱이 설치되어 있지 않습니다.", ""));
            return;
        }
        if (!canHandleBroadcast(appContext, action)) {
            safeInvokeValidationCallback(callback, new ValidationResult(requestId, false, "인증 앱 검증 리시버를 찾을 수 없습니다.", ""));
            return;
        }

        String callbackAction = appContext.getPackageName() + ".DEVICE_AUTH_RESULT." + requestId;
        Intent callbackIntent = new Intent(callbackAction).setPackage(appContext.getPackageName());

        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                appContext,
                requestId.hashCode(),
                callbackIntent,
                buildPendingIntentFlags());

        final Handler mainHandler = new Handler(Looper.getMainLooper());
        final AtomicBoolean finished = new AtomicBoolean(false);
        final BroadcastReceiver[] receiverRef = new BroadcastReceiver[1];

        BroadcastReceiver receiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                if (finished.get()) {
                    return;
                }

                String kind = safeString(intent != null ? intent.getStringExtra(EXTRA_RESPONSE_KIND) : "");
                if (!expectedResponseKind.equals(kind)) {
                    return;
                }

                String raw = safeString(intent != null ? intent.getStringExtra(EXTRA_RESPONSE_JSON) : "");
                if (!finished.compareAndSet(false, true)) {
                    return;
                }

                safelyUnregister(appContext, receiverRef[0]);
                mainHandler.removeCallbacksAndMessages(null);
                safeInvokeValidationCallback(callback, parseValidationResult(requestId, raw));
            }
        };
        receiverRef[0] = receiver;

        if (!registerReceiver(appContext, receiver, callbackAction)) {
            safeInvokeValidationCallback(callback, new ValidationResult(requestId, false, "콜백 리시버 등록 실패", ""));
            return;
        }

        mainHandler.postDelayed(new Runnable() {
            @Override
            public void run() {
                if (!finished.compareAndSet(false, true)) {
                    return;
                }
                safelyUnregister(appContext, receiverRef[0]);
                safeInvokeValidationCallback(callback, new ValidationResult(requestId, false, "인증 앱 응답 시간 초과", ""));
            }
        }, VALIDATE_CALLBACK_TIMEOUT_MILLIS);

        Intent requestIntent = new Intent(action)
                .setClassName(AUTH_APP_PACKAGE, VALIDATE_RECEIVER_CLASS)
                .addFlags(Intent.FLAG_RECEIVER_FOREGROUND)
                .addFlags(Intent.FLAG_INCLUDE_STOPPED_PACKAGES)
                .putExtra(EXTRA_REQUEST_JSON, requestJson)
                .putExtra(EXTRA_CALLBACK_INTENT, pendingIntent);

        try {
            appContext.sendBroadcast(requestIntent);
        } catch (Throwable throwable) {
            if (!finished.compareAndSet(false, true)) {
                return;
            }
            safelyUnregister(appContext, receiverRef[0]);
            mainHandler.removeCallbacksAndMessages(null);
            safeInvokeValidationCallback(callback, new ValidationResult(requestId, false, "인증 앱 요청 실패: " + safeThrowableMessage(throwable), ""));
        }
    }

    private static void safeInvokeAuthCallback(AuthCallback callback, AuthResult result) {
        if (callback == null) {
            return;
        }
        try {
            callback.onResult(result);
        } catch (Throwable throwable) {
            Log.e(TAG, "Auth callback failed", throwable);
        }
    }

    private static void safeInvokeValidationCallback(ValidationCallback callback, ValidationResult result) {
        if (callback == null) {
            return;
        }
        try {
            callback.onResult(result);
        } catch (Throwable throwable) {
            Log.e(TAG, "Validation callback failed", throwable);
        }
    }

    private static boolean registerReceiver(Context context, BroadcastReceiver receiver, String action) {
        try {
            context.registerReceiver(receiver, new IntentFilter(action));
            return true;
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static void safelyUnregister(Context context, BroadcastReceiver receiver) {
        if (receiver == null) {
            return;
        }
        try {
            context.unregisterReceiver(receiver);
        } catch (Throwable ignored) {
        }
    }

    private static int buildPendingIntentFlags() {
        int mutability = Build.VERSION.SDK_INT >= ANDROID_SDK_S
                ? PENDING_INTENT_FLAG_MUTABLE
                : 0;
        return PendingIntent.FLAG_UPDATE_CURRENT | mutability;
    }

    private static boolean isAuthAppInstalled(Context appContext) {
        try {
            appContext.getPackageManager().getPackageInfo(AUTH_APP_PACKAGE, 0);
            return true;
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static boolean canHandleActivity(Context appContext, String action) {
        try {
            Intent intent = new Intent(action).setPackage(AUTH_APP_PACKAGE);
            return intent.resolveActivity(appContext.getPackageManager()) != null;
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static boolean canHandleBroadcast(Context appContext, String action) {
        try {
            Intent intent = new Intent(action).setPackage(AUTH_APP_PACKAGE);
            return !appContext.getPackageManager().queryBroadcastReceivers(intent, PackageManager.GET_META_DATA).isEmpty();
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static AuthResult parseAuthResult(String requestId, String rawJson) {
        if (TextUtils.isEmpty(rawJson)) {
            return new AuthResult(requestId, "FAILED", "인증 응답이 비어 있습니다.");
        }

        try {
            JSONObject obj = new JSONObject(rawJson);
            String status = readJsonString(obj, "status");
            String message = readJsonString(obj, "message");
            String resolvedRequestId = readJsonString(obj, "requestId");
            if (TextUtils.isEmpty(resolvedRequestId)) {
                resolvedRequestId = requestId;
            }
            if (TextUtils.isEmpty(status)) {
                status = "FAILED";
            }
            return new AuthResult(
                    resolvedRequestId,
                    status,
                    message,
                    rawJson,
                    readJsonString(obj, "deviceId"),
                    readJsonString(obj, "deviceFingerprint"),
                    readJsonString(obj, "licenseToken"));
        } catch (Exception ignored) {
            return new AuthResult(requestId, "FAILED", "인증 응답 파싱 실패");
        }
    }

    private static ValidationResult parseValidationResult(String requestId, String rawJson) {
        if (TextUtils.isEmpty(rawJson)) {
            return new ValidationResult(requestId, false, "검증 응답이 비어 있습니다.", "");
        }

        try {
            JSONObject obj = new JSONObject(rawJson);
            boolean isValid = readJsonBoolean(obj, "isValid");
            String reason = readJsonString(obj, "reason");
            String serverStatus = readJsonString(obj, "serverStatus");
            boolean localLicenseDiscarded = readJsonBoolean(obj, "localLicenseDiscarded");
            boolean serverChecked = readJsonBoolean(obj, "serverChecked");
            boolean usedOfflineFallback = readJsonBoolean(obj, "usedOfflineFallback");
            String payloadJson = readJsonString(obj, "payloadJson");
            String resolvedRequestId = readJsonString(obj, "requestId");
            if (TextUtils.isEmpty(resolvedRequestId)) {
                resolvedRequestId = requestId;
            }
            return new ValidationResult(
                    resolvedRequestId,
                    isValid,
                    reason,
                    serverStatus,
                    localLicenseDiscarded,
                    serverChecked,
                    usedOfflineFallback,
                    payloadJson,
                    rawJson);
        } catch (Exception ignored) {
            return new ValidationResult(requestId, false, "검증 응답 파싱 실패", "");
        }
    }

    private static String readJsonString(JSONObject obj, String key) {
        if (obj == null || TextUtils.isEmpty(key)) {
            return "";
        }

        String value = safeString(obj.optString(key, ""));
        if (!TextUtils.isEmpty(value)) {
            return value;
        }

        try {
            for (java.util.Iterator<String> keys = obj.keys(); keys.hasNext(); ) {
                String currentKey = keys.next();
                if (key.equalsIgnoreCase(currentKey)) {
                    return safeString(obj.optString(currentKey, ""));
                }
            }
        } catch (Exception ignored) {
        }
        return "";
    }

    private static boolean readJsonBoolean(JSONObject obj, String key) {
        if (obj == null || TextUtils.isEmpty(key)) {
            return false;
        }
        if (obj.has(key)) {
            return obj.optBoolean(key, false);
        }
        try {
            for (java.util.Iterator<String> keys = obj.keys(); keys.hasNext(); ) {
                String currentKey = keys.next();
                if (key.equalsIgnoreCase(currentKey)) {
                    return obj.optBoolean(currentKey, false);
                }
            }
        } catch (Exception ignored) {
        }
        return false;
    }

    private static String safeString(String value) {
        return value == null ? "" : value.trim();
    }

    private static String safeThrowableMessage(Throwable throwable) {
        if (throwable == null || TextUtils.isEmpty(throwable.getMessage())) {
            return "unknown";
        }
        return throwable.getMessage().trim();
    }

    private static boolean isTransientValidationFailure(ValidationResult result) {
        if (result == null || result.isValid()) {
            return false;
        }

        String serverStatus = safeString(result.getServerStatus());
        if (!TextUtils.isEmpty(serverStatus)) {
            return false;
        }

        String reason = safeString(result.getReason());
        return reason.contains("시간 초과")
                || reason.contains("요청 실패")
                || reason.contains("콜백 리시버")
                || reason.contains("응답 파싱 실패")
                || reason.contains("응답이 비어");
    }
}

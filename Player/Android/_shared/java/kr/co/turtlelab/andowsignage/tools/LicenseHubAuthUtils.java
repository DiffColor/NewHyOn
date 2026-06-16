package kr.co.turtlelab.andowsignage.tools;

import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.text.TextUtils;
import android.util.Base64;

import org.json.JSONObject;

import java.io.File;
import java.security.KeyFactory;
import java.security.MessageDigest;
import java.security.Signature;
import java.security.spec.X509EncodedKeySpec;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Date;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.TimeZone;
import java.util.UUID;

public class LicenseHubAuthUtils {
    public static final int PRODUCT_ID = 7;
    public static final String API_BASE_URL = "https://licensehub.ilycode.app";
    public static final String AUTH_APP_PACKAGE = "com.licensehub.deviceauth.app";
    public static final String ACTION_AUTHENTICATE = "com.licensehub.deviceauth.AUTHENTICATE";
    public static final String EXTRA_REQUEST_JSON = "com.licensehub.deviceauth.extra.REQUEST_JSON";
    public static final String EXTRA_CALLBACK_INTENT = "com.licensehub.deviceauth.extra.CALLBACK_INTENT";
    public static final String EXTRA_RESPONSE_JSON = "com.licensehub.deviceauth.extra.RESPONSE_JSON";
    public static final String EXTRA_RESPONSE_KIND = "com.licensehub.deviceauth.extra.RESPONSE_KIND";
    public static final String RESPONSE_KIND_AUTHENTICATE = "AUTHENTICATE";
    public static final String STATUS_ONLINE_VERIFIED = "ONLINE_VERIFIED";
    public static final String STATUS_OFFLINE_PENDING_PROOF = "OFFLINE_PENDING_PROOF";
    public static final String STATUS_OFFLINE_VERIFIED = "OFFLINE_VERIFIED";
    public static final String STATUS_CANCELLED = "CANCELLED";

    private static final long AUTH_TIMEOUT_MS = 10 * 60 * 1000L;
    private static final String PUBLIC_KEY_PEM = "-----BEGIN PUBLIC KEY-----\n"
            + "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAhMxB0eP/q5vyjFOwWENS68uq/d1\n"
            + "hmq6Uv1tHpjMExWVgY3jhbDZ9dM9EyWJ3XXCI8IMgSyF6pKEm6K3LplFHQ==\n"
            + "-----END PUBLIC KEY-----";

    private static final Set<String> DUMMY_VALUES = new HashSet<String>();

    static {
        Collections.addAll(DUMMY_VALUES,
                "", "0", "00", "000", "0000", "00000000", "unknown", "none",
                "default", "n/a", "android", "alps", "generic", "goldfish",
                "default string");
    }

    private LicenseHubAuthUtils() {
    }

    public interface AuthCallback {
        void onSuccess(AuthResult result);
        void onFailure(String message);
    }

    public static class AuthResult {
        public final String status;
        public final String message;
        public final String deviceId;
        public final String deviceName;
        public final String deviceFingerprint;
        public final String licenseToken;
        public final String serializedLicense;

        AuthResult(String status,
                   String message,
                   String deviceId,
                   String deviceName,
                   String deviceFingerprint,
                   String licenseToken,
                   String serializedLicense) {
            this.status = nullToEmpty(status);
            this.message = nullToEmpty(message);
            this.deviceId = nullToEmpty(deviceId);
            this.deviceName = nullToEmpty(deviceName);
            this.deviceFingerprint = nullToEmpty(deviceFingerprint);
            this.licenseToken = nullToEmpty(licenseToken);
            this.serializedLicense = nullToEmpty(serializedLicense);
        }
    }

    public static void authenticate(Context context, String deviceName, AuthCallback callback) {
        final Context appContext = context == null ? null : context.getApplicationContext();
        if (appContext == null) {
            callback.onFailure("앱 컨텍스트를 확인할 수 없습니다.");
            return;
        }

        final String requestId = UUID.randomUUID().toString();
        final String callbackAction = appContext.getPackageName() + ".LICENSEHUB_AUTH_RESULT." + requestId;
        final Handler mainHandler = new Handler(Looper.getMainLooper());
        final boolean[] finished = new boolean[]{false};
        final BroadcastReceiver[] receiverRef = new BroadcastReceiver[1];

        BroadcastReceiver receiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                if (finished[0]) {
                    return;
                }
                String kind = intent == null ? "" : intent.getStringExtra(EXTRA_RESPONSE_KIND);
                if (!RESPONSE_KIND_AUTHENTICATE.equals(kind)) {
                    return;
                }
                finished[0] = true;
                unregisterQuietly(appContext, receiverRef[0]);

                String responseJson = intent.getStringExtra(EXTRA_RESPONSE_JSON);
                AuthResult result = parseAuthResult(appContext, responseJson);
                if (isVerifiedStatus(result.status)
                        && !TextUtils.isEmpty(result.deviceId)
                        && !TextUtils.isEmpty(result.deviceFingerprint)) {
                    callback.onSuccess(result);
                    return;
                }
                callback.onFailure(resolveFailureMessage(result));
            }
        };
        receiverRef[0] = receiver;

        try {
            appContext.registerReceiver(receiver, new IntentFilter(callbackAction));
        } catch (Exception ex) {
            callback.onFailure("인증 콜백 리시버 등록에 실패했습니다.");
            return;
        }

        mainHandler.postDelayed(new Runnable() {
            @Override
            public void run() {
                if (finished[0]) {
                    return;
                }
                finished[0] = true;
                unregisterQuietly(appContext, receiverRef[0]);
                callback.onFailure("LicenseHub 인증앱 응답 시간이 초과되었습니다.");
            }
        }, AUTH_TIMEOUT_MS);

        PendingIntent callbackIntent = PendingIntent.getBroadcast(
                appContext,
                requestId.hashCode(),
                new Intent(callbackAction).setPackage(appContext.getPackageName()),
                PendingIntent.FLAG_UPDATE_CURRENT);

        Intent requestIntent = new Intent(ACTION_AUTHENTICATE)
                .setPackage(AUTH_APP_PACKAGE)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                .putExtra(EXTRA_REQUEST_JSON, buildAuthenticateRequest(requestId, deviceName))
                .putExtra(EXTRA_CALLBACK_INTENT, callbackIntent);

        try {
            appContext.startActivity(requestIntent);
        } catch (Exception ex) {
            if (!finished[0]) {
                finished[0] = true;
                unregisterQuietly(appContext, receiverRef[0]);
                callback.onFailure("LicenseHub 인증앱을 실행할 수 없습니다. 인증앱 설치 상태를 확인해주세요.");
            }
        }
    }

    public static boolean isValidForCurrentDevice(Context context, String serializedLicense) {
        return validateLicensePayload(context, serializedLicense, true);
    }

    public static boolean isValidLicenseHubPayload(String serializedLicense) {
        return validateLicensePayload(null, serializedLicense, false);
    }

    public static boolean isLicenseHubAuthPayload(String value) {
        if (TextUtils.isEmpty(value)) {
            return false;
        }
        try {
            JSONObject json = new JSONObject(value.trim());
            return json.optInt("productId", json.optInt("ProductId", 0)) == PRODUCT_ID
                    && !TextUtils.isEmpty(readJsonString(json, "licenseToken", "LicenseToken"))
                    && !TextUtils.isEmpty(readJsonString(json, "deviceFingerprint", "DeviceFingerprint"));
        } catch (Exception ignored) {
            return false;
        }
    }

    public static String extractDeviceFingerprint(String serializedLicense) {
        if (TextUtils.isEmpty(serializedLicense)) {
            return "";
        }
        try {
            JSONObject json = new JSONObject(serializedLicense.trim());
            return readJsonString(json, "deviceFingerprint", "DeviceFingerprint");
        } catch (Exception ignored) {
            return "";
        }
    }

    public static String resolveDisplayFingerprint(Context context, String serializedLicense) {
        String storedFingerprint = extractDeviceFingerprint(serializedLicense);
        if (!TextUtils.isEmpty(storedFingerprint)) {
            return storedFingerprint;
        }
        try {
            return generateDeviceFingerprint(context);
        } catch (Exception ignored) {
            return "";
        }
    }

    public static String generateDeviceFingerprint(Context context) throws Exception {
        List<String> raw = new ArrayList<String>();
        raw.add(Build.MANUFACTURER);
        raw.add(Build.MODEL);
        raw.add(Build.BOARD);
        raw.add(Build.DEVICE);
        raw.add(Build.HARDWARE);
        raw.add(Build.PRODUCT);
        if (Build.VERSION.SDK_INT < 29) {
            raw.add(readLegacySerialCandidate());
        }
        raw.add(readSystemProperty("ro.boot.serialno"));
        raw.add(readSystemProperty("ro.serialno"));
        raw.add(readSystemProperty("ro.boot.hardware"));
        raw.add(readSystemProperty("ro.hardware"));
        raw.add(readCpuInfoHash());

        List<String> normalized = new ArrayList<String>();
        for (String value : raw) {
            String item = normalizeFingerprintSource(value);
            if (!TextUtils.isEmpty(item) && !normalized.contains(item)) {
                normalized.add(item);
            }
        }
        Collections.sort(normalized);
        if (normalized.isEmpty()) {
            throw new IllegalStateException("유효한 하드웨어 소스가 없습니다.");
        }
        return sha256(join(normalized, "|"));
    }

    private static AuthResult parseAuthResult(Context context, String responseJson) {
        if (TextUtils.isEmpty(responseJson)) {
            return new AuthResult("", "인증앱 응답이 비어 있습니다.", "", "", "", "", "");
        }
        try {
            JSONObject json = new JSONObject(responseJson);
            String status = json.optString("status");
            String message = json.optString("message");
            String deviceId = json.optString("deviceId");
            String deviceName = json.optString("deviceName");
            String deviceFingerprint = json.optString("deviceFingerprint");
            String licenseToken = json.optString("licenseToken");

            String serializedLicense = "";
            if (STATUS_ONLINE_VERIFIED.equals(status)
                    && !TextUtils.isEmpty(deviceFingerprint)
                    && !TextUtils.isEmpty(licenseToken)) {
                serializedLicense = buildSerializedLicense(deviceFingerprint, deviceId, licenseToken);
                if (!validateLicensePayload(context, serializedLicense, true)) {
                    return new AuthResult(status, "인증 토큰이 현재 장비와 일치하지 않습니다.", deviceId,
                            deviceName, deviceFingerprint, licenseToken, "");
                }
            }

            return new AuthResult(status, message, deviceId, deviceName, deviceFingerprint,
                    licenseToken, serializedLicense);
        } catch (Exception ex) {
            return new AuthResult("", "인증앱 응답을 해석하지 못했습니다.", "", "", "", "", "");
        }
    }

    private static String resolveFailureMessage(AuthResult result) {
        if (!TextUtils.isEmpty(result.message)) {
            return result.message;
        }
        if (STATUS_CANCELLED.equals(result.status)) {
            return "인증이 취소되었습니다.";
        }
        if (STATUS_OFFLINE_PENDING_PROOF.equals(result.status)) {
            return "오프라인 인증은 모바일웹 완료 후 온라인 인증 결과가 필요합니다.";
        }
        if (STATUS_OFFLINE_VERIFIED.equals(result.status)) {
            return "오프라인 인증이 완료되었지만 인증 정보를 확인하지 못했습니다.";
        }
        return "인증이 완료되지 않았습니다.";
    }

    private static boolean isVerifiedStatus(String status) {
        return STATUS_ONLINE_VERIFIED.equals(status)
                || STATUS_OFFLINE_VERIFIED.equals(status);
    }

    private static String buildAuthenticateRequest(String requestId, String deviceName) {
        try {
            return new JSONObject()
                    .put("requestId", requestId)
                    .put("apiBaseUrl", API_BASE_URL)
                    .put("productId", PRODUCT_ID)
                    .put("deviceName", nullToEmpty(deviceName))
                    .put("deviceId", "")
                    .put("localLicensePath", "")
                    .toString();
        } catch (Exception ignored) {
            return "{}";
        }
    }

    private static String buildSerializedLicense(String deviceFingerprint, String deviceId, String licenseToken) {
        try {
            return new JSONObject()
                    .put("productId", PRODUCT_ID)
                    .put("deviceFingerprint", nullToEmpty(deviceFingerprint).trim())
                    .put("deviceId", nullToEmpty(deviceId).trim())
                    .put("licenseToken", nullToEmpty(licenseToken).trim())
                    .put("savedAt", isoUtcNow())
                    .toString();
        } catch (Exception ignored) {
            return "";
        }
    }

    private static boolean validateLicensePayload(Context context, String serializedLicense, boolean requireCurrentDevice) {
        if (!isLicenseHubAuthPayload(serializedLicense)) {
            return false;
        }
        try {
            JSONObject license = new JSONObject(serializedLicense.trim());
            String token = readJsonString(license, "licenseToken", "LicenseToken");
            String expectedFingerprint = readJsonString(license, "deviceFingerprint", "DeviceFingerprint");
            if (requireCurrentDevice) {
                String currentFingerprint = generateDeviceFingerprint(context);
                if (!expectedFingerprint.equalsIgnoreCase(currentFingerprint)) {
                    return false;
                }
            }
            return validateToken(token, expectedFingerprint);
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean validateToken(String token, String expectedFingerprint) {
        try {
            String[] parts = nullToEmpty(token).trim().split("\\.");
            if (parts.length != 2) {
                return false;
            }
            byte[] payloadBytes = base64UrlDecode(parts[0]);
            byte[] signatureBytes = base64UrlDecode(parts[1]);
            if (!verifySignature(payloadBytes, signatureBytes)) {
                return false;
            }

            JSONObject payload = new JSONObject(new String(payloadBytes, "UTF-8"));
            String status = readJsonString(payload, "status", "Status");
            int productId = readJsonInt(payload, "productId", "ProductId");
            String tokenFingerprint = readJsonString(payload, "deviceFingerprint", "DeviceFingerprint");
            long expiresAt = readJsonLong(payload, "expiresAt", "ExpiresAt");

            return "active".equalsIgnoreCase(status)
                    && productId == PRODUCT_ID
                    && tokenFingerprint.equalsIgnoreCase(expectedFingerprint)
                    && expiresAt > (System.currentTimeMillis() / 1000L);
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean verifySignature(byte[] payloadBytes, byte[] signatureBytes) {
        try {
            Signature verifier = Signature.getInstance("SHA256withECDSA");
            verifier.initVerify(loadPublicKey());
            verifier.update(payloadBytes);
            return verifier.verify(signatureBytes);
        } catch (Exception ignored) {
            return false;
        }
    }

    private static java.security.PublicKey loadPublicKey() throws Exception {
        String normalized = PUBLIC_KEY_PEM
                .replace("-----BEGIN PUBLIC KEY-----", "")
                .replace("-----END PUBLIC KEY-----", "")
                .replace("-----BEGIN EC PUBLIC KEY-----", "")
                .replace("-----END EC PUBLIC KEY-----", "")
                .replace("\n", "")
                .replace("\r", "")
                .replace("\t", "")
                .replace(" ", "")
                .trim();
        byte[] keyBytes = Base64.decode(normalized, Base64.DEFAULT);
        return KeyFactory.getInstance("EC").generatePublic(new X509EncodedKeySpec(keyBytes));
    }

    private static byte[] base64UrlDecode(String value) {
        return Base64.decode(nullToEmpty(value), Base64.URL_SAFE | Base64.NO_PADDING | Base64.NO_WRAP);
    }

    private static String readSystemProperty(String key) {
        java.lang.Process process = null;
        try {
            process = Runtime.getRuntime().exec(new String[]{"getprop", key});
            java.io.BufferedReader reader = new java.io.BufferedReader(
                    new java.io.InputStreamReader(process.getInputStream()));
            try {
                return nullToEmpty(reader.readLine()).trim();
            } finally {
                reader.close();
            }
        } catch (Exception ignored) {
            return "";
        } finally {
            if (process != null) {
                process.destroy();
            }
        }
    }

    private static String readLegacySerialCandidate() {
        try {
            return nullToEmpty(Build.SERIAL);
        } catch (Exception ignored) {
            return "";
        }
    }

    private static String readCpuInfoHash() {
        try {
            File file = new File("/proc/cpuinfo");
            java.io.FileInputStream input = new java.io.FileInputStream(file);
            try {
                MessageDigest digest = MessageDigest.getInstance("SHA-256");
                byte[] buffer = new byte[4096];
                int read;
                while ((read = input.read(buffer)) != -1) {
                    digest.update(buffer, 0, read);
                }
                return toHexUpper(digest.digest());
            } finally {
                input.close();
            }
        } catch (Exception ignored) {
            return "";
        }
    }

    private static String normalizeFingerprintSource(String value) {
        if (TextUtils.isEmpty(value)) {
            return "";
        }
        String normalized = value.trim().toLowerCase(Locale.US);
        if (DUMMY_VALUES.contains(normalized)) {
            return "";
        }
        boolean allZero = true;
        for (int i = 0; i < normalized.length(); i++) {
            if (normalized.charAt(i) != '0') {
                allZero = false;
                break;
            }
        }
        return allZero ? "" : normalized;
    }

    private static String sha256(String value) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        return toHexUpper(digest.digest(nullToEmpty(value).getBytes("UTF-8")));
    }

    private static String toHexUpper(byte[] bytes) {
        StringBuilder builder = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) {
            builder.append(String.format(Locale.US, "%02X", value & 0xFF));
        }
        return builder.toString();
    }

    private static String join(List<String> values, String separator) {
        StringBuilder builder = new StringBuilder();
        for (String value : values) {
            if (builder.length() > 0) {
                builder.append(separator);
            }
            builder.append(value);
        }
        return builder.toString();
    }

    private static String isoUtcNow() {
        SimpleDateFormat formatter = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US);
        formatter.setTimeZone(TimeZone.getTimeZone("UTC"));
        return formatter.format(new Date());
    }

    private static String readJsonString(JSONObject json, String camelName, String pascalName) {
        String value = json.optString(camelName, "");
        if (!TextUtils.isEmpty(value)) {
            return value.trim();
        }
        return json.optString(pascalName, "").trim();
    }

    private static int readJsonInt(JSONObject json, String camelName, String pascalName) {
        if (json.has(camelName)) {
            return json.optInt(camelName, 0);
        }
        return json.optInt(pascalName, 0);
    }

    private static long readJsonLong(JSONObject json, String camelName, String pascalName) {
        if (json.has(camelName)) {
            return readLongValue(json.opt(camelName));
        }
        return readLongValue(json.opt(pascalName));
    }

    private static long readLongValue(Object value) {
        if (value instanceof Number) {
            return ((Number) value).longValue();
        }
        try {
            return Long.parseLong(String.valueOf(value));
        } catch (Exception ignored) {
            return 0L;
        }
    }

    private static String nullToEmpty(String value) {
        return value == null ? "" : value;
    }

    private static void unregisterQuietly(Context context, BroadcastReceiver receiver) {
        if (context == null || receiver == null) {
            return;
        }
        try {
            context.unregisterReceiver(receiver);
        } catch (Exception ignored) {
        }
    }
}

package kr.co.turtlelab.andowsignage.tools;

import android.util.Base64;

import com.google.gson.Gson;

import java.io.File;
import java.io.FileInputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.Charset;
import java.security.MessageDigest;
import java.util.Arrays;

import javax.crypto.Cipher;
import javax.crypto.Mac;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

public final class SecureJsonTools {
    private static final int IV_SIZE = 16;
    private static final int HMAC_SIZE = 32;
    private static final String DEFAULT_PASSPHRASE = "ninja04!9akftp!";
    private static final byte[] FAST_MAGIC = new byte[]{'N', 'H', 'Y', '2'};
    private static final Charset UTF8 = Charset.forName("UTF-8");
    private static final Gson GSON = new Gson();

    private SecureJsonTools() {
    }

    public static <T> T readEncryptedJson(File file, Class<T> type) throws Exception {
        if (file == null || !file.exists() || type == null) {
            return null;
        }

        String base64 = new String(readAllBytes(file), UTF8).trim();
        if (base64.length() < 1) {
            return null;
        }

        byte[] encrypted = Base64.decode(base64, Base64.DEFAULT);
        String json = decrypt(encrypted);
        if (json == null || json.length() < 1) {
            return null;
        }

        return GSON.fromJson(json, type);
    }

    private static String decrypt(byte[] encryptedPayload) throws Exception {
        if (!isFastPayload(encryptedPayload)) {
            return null;
        }

        return decryptFast(encryptedPayload);
    }

    private static String decryptFast(byte[] encryptedPayload) throws Exception {
        if (encryptedPayload == null || encryptedPayload.length < FAST_MAGIC.length + IV_SIZE + HMAC_SIZE) {
            return null;
        }

        byte[] encKey = computeSha256(DEFAULT_PASSPHRASE + ":enc");
        byte[] macKey = computeSha256(DEFAULT_PASSPHRASE + ":mac");

        int cipherOffset = FAST_MAGIC.length + IV_SIZE;
        int cipherLength = encryptedPayload.length - cipherOffset - HMAC_SIZE;
        if (cipherLength <= 0) {
            return null;
        }

        byte[] providedMac = Arrays.copyOfRange(encryptedPayload, cipherOffset + cipherLength, encryptedPayload.length);
        byte[] expectedMac = hmacSha256(macKey, encryptedPayload, 0, cipherOffset + cipherLength);
        if (!constantTimeEquals(providedMac, expectedMac)) {
            return null;
        }

        byte[] iv = Arrays.copyOfRange(encryptedPayload, FAST_MAGIC.length, FAST_MAGIC.length + IV_SIZE);
        byte[] cipherBytes = Arrays.copyOfRange(encryptedPayload, cipherOffset, cipherOffset + cipherLength);

        Cipher cipher = Cipher.getInstance("AES/CBC/PKCS5Padding");
        cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(encKey, "AES"), new IvParameterSpec(iv));
        byte[] plain = cipher.doFinal(cipherBytes);
        return new String(plain, UTF8);
    }

    private static boolean isFastPayload(byte[] payload) {
        if (payload == null || payload.length < FAST_MAGIC.length) {
            return false;
        }
        for (int i = 0; i < FAST_MAGIC.length; i++) {
            if (payload[i] != FAST_MAGIC[i]) {
                return false;
            }
        }
        return true;
    }

    private static byte[] hmacSha256(byte[] key, byte[] data, int offset, int count) throws Exception {
        Mac hmac = Mac.getInstance("HmacSHA256");
        hmac.init(new SecretKeySpec(key, "HmacSHA256"));
        hmac.update(data, offset, count);
        return hmac.doFinal();
    }

    private static byte[] computeSha256(String value) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        return digest.digest((value == null ? "" : value).getBytes(UTF8));
    }

    private static boolean constantTimeEquals(byte[] left, byte[] right) {
        if (left == null || right == null || left.length != right.length) {
            return false;
        }

        int diff = 0;
        for (int i = 0; i < left.length; i++) {
            diff |= left[i] ^ right[i];
        }

        return diff == 0;
    }

    private static byte[] readAllBytes(File file) throws Exception {
        FileInputStream input = null;
        ByteArrayOutputStream output = new ByteArrayOutputStream();
        try {
            input = new FileInputStream(file);
            byte[] buffer = new byte[8192];
            int read;
            while ((read = input.read(buffer)) > 0) {
                output.write(buffer, 0, read);
            }
            return output.toByteArray();
        } finally {
            if (input != null) {
                input.close();
            }
        }
    }
}

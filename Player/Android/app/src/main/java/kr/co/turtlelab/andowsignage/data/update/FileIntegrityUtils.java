package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.security.DigestInputStream;
import java.security.MessageDigest;
import java.util.Locale;

public final class FileIntegrityUtils {

    private FileIntegrityUtils() { }

    public static boolean verifyFile(File file, long expectedSize, String expectedHash) {
        if (file == null || !file.exists()) {
            return false;
        }
        if (expectedSize > 0 && file.length() != expectedSize) {
            return false;
        }
        if (TextUtils.isEmpty(expectedHash)) {
            return true;
        }
        if (isXxHash64(expectedHash)) {
            String hash = computePartialXxHash64(file, 1024);
            return !TextUtils.isEmpty(hash) && hash.equalsIgnoreCase(expectedHash);
        }
        // XXHash64 외의 해시는 (Windows와 동일하게) 검증하지 않는다.
        return true;
    }

    private static boolean isHex(String checksum) {
        if (TextUtils.isEmpty(checksum)) {
            return false;
        }
        for (int i = 0; i < checksum.length(); i++) {
            char c = checksum.charAt(i);
            boolean hex = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F');
            if (!hex) {
                return false;
            }
        }
        return true;
    }

    private static boolean isXxHash64(String checksum) {
        return checksum != null && checksum.length() == 16 && isHex(checksum);
    }

    public static String computeSha256(File file) {
        if (file == null || !file.exists()) {
            return "";
        }
        InputStream input = null;
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            input = new DigestInputStream(new FileInputStream(file), digest);
            byte[] buffer = new byte[8192];
            while (input.read(buffer) != -1) {
                // consume stream
            }
            byte[] hash = digest.digest();
            StringBuilder sb = new StringBuilder(hash.length * 2);
            for (byte b : hash) {
                sb.append(String.format(Locale.US, "%02x", b));
            }
            return sb.toString();
        } catch (Exception e) {
            return "";
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignore) { }
            }
        }
    }

    /**
     * Windows 플레이어와 동일한 XXHash64 부분 해시(1KB x 3 + 파일 길이)를 계산한다.
     */
    public static String computePartialXxHash64(File file, int blockSize) {
        if (file == null || !file.exists() || blockSize <= 0) {
            return "";
        }
        long fileSize = file.length();
        try {
            if (fileSize < blockSize * 3) {
                // 작은 파일은 전체를 해시
                try (FileInputStream fis = new FileInputStream(file)) {
                    XxHash64Digest digest = new XxHash64Digest(0);
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = fis.read(buffer)) > 0) {
                        digest.update(buffer, 0, read);
                    }
                    return String.format(Locale.US, "%016x", digest.getValue()).toUpperCase(Locale.US);
                }
            }

            byte[] block1 = new byte[blockSize];
            byte[] block2 = new byte[blockSize];
            byte[] block3 = new byte[blockSize];
            try (FileInputStream fis = new FileInputStream(file)) {
                // 앞 1KB
                fis.read(block1, 0, blockSize);
                // 중간 1KB
                long midPos = fileSize / 2;
                fis.getChannel().position(midPos);
                fis.read(block2, 0, blockSize);
                // 끝 1KB
                fis.getChannel().position(Math.max(0, fileSize - blockSize));
                fis.read(block3, 0, blockSize);
            }

            XxHash64Digest digest = new XxHash64Digest(0);
            digest.update(block1, 0, block1.length);
            digest.update(block2, 0, block2.length);
            digest.update(block3, 0, block3.length);
            // 파일 길이를 big-endian 바이트로 추가
            byte[] sizeBytes = ByteBuffer.allocate(8)
                    .order(ByteOrder.BIG_ENDIAN)
                    .putLong(fileSize)
                    .array();
            digest.update(sizeBytes, 0, sizeBytes.length);
            return String.format(Locale.US, "%016x", digest.getValue()).toUpperCase(Locale.US);
        } catch (Exception e) {
            return "";
        }
    }
}

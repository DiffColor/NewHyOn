package kr.co.turtlelab.andowsignage.tools;

import android.content.Context;
import android.text.TextUtils;

import java.io.File;

import it.sauronsoftware.ftp4j.FTPAbortedException;
import it.sauronsoftware.ftp4j.FTPClient;
import it.sauronsoftware.ftp4j.FTPDataTransferException;
import it.sauronsoftware.ftp4j.FTPDataTransferListener;
import it.sauronsoftware.ftp4j.FTPException;
import it.sauronsoftware.ftp4j.FTPIllegalReplyException;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueLogger;

/**
 * FTP4J 기반의 단순/안정적인 Resume 다운로드 유틸.
 * 연결/로그인/수동 수명 관리를 한 곳에 모아둔다.
 */
public class FTP4JUtil {

    private final String host;
    private final int port;
    private final String id;
    private final String pw;
    private final String charset;
    private final int bufferSize;

    public FTP4JUtil(Context ctx, String host, int port, String id, String pw) {
        this(ctx, host, port, id, pw, "UTF-8", 1024 * 512);
    }

    public FTP4JUtil(Context ctx, String host, int port, String id, String pw, String charset, int bufferSize) {
        this.host = TextUtils.isEmpty(host) ? "127.0.0.1" : host;
        this.port = port > 0 ? port : AndoWSignageApp.FTP_PORT;
        this.id = TextUtils.isEmpty(id) ? "" : id;
        this.pw = TextUtils.isEmpty(pw) ? "" : pw;
        this.charset = TextUtils.isEmpty(charset) ? "UTF-8" : charset;
        this.bufferSize = Math.max(64 * 1024, bufferSize);
    }

    public static final class DownloadResult {
        public boolean success;
        public boolean missing;
        public long downloadedBytes;
    }

    public DownloadResult downloadWithResume(String remotePath, File targetFile, long resumeHint) {
        DownloadResult result = new DownloadResult();
        FTPClient client = new FTPClient();
        try {
            if (targetFile == null || TextUtils.isEmpty(remotePath)) {
                return result;
            }
            File parent = targetFile.getParentFile();
            if (parent != null && !parent.exists()) {
                parent.mkdirs();
            }

            client.setPassive(true);
            client.setCharset(charset);
            client.setType(FTPClient.TYPE_BINARY);
            client.setAutoNoopTimeout(30_000);
            client.connect(host, port);
            client.login(id, pw);

            long localSize = targetFile.exists() ? targetFile.length() : 0L;
            long resumeAt = Math.max(0L, Math.max(resumeHint, localSize));
            resumeAt = clampResume(remotePath, client, resumeAt);

            FTPDataTransferListener listener = new FTPDataTransferListener() {
                @Override
                public void started() { }
                @Override
                public void transferred(int length) {
                    // 누적 전송량만 기록해두고, 필요 시 호출자가 파일 길이를 통해 확인한다.
                }
                @Override
                public void completed() { }
                @Override
                public void aborted() { }
                @Override
                public void failed() { }
            };

            client.download(remotePath, targetFile, resumeAt, listener);
            result.success = true;
            result.downloadedBytes = targetFile.length();
        } catch (FTPException e) {
            int code = e.getCode();
            if (code == 550 || code == 553) {
                result.missing = true;
            }
            UpdateQueueLogger.log("FTP4J FTPException code=" + e.getCode() + " msg=" + e.getMessage());
        } catch (FTPDataTransferException | FTPAbortedException | FTPIllegalReplyException | java.io.IOException e) {
            UpdateQueueLogger.log("FTP4J download failed: " + e.getMessage());
        } catch (Exception e) {
            UpdateQueueLogger.log("FTP4J unexpected error: " + e.getMessage());
        } finally {
            try {
                if (client.isConnected()) {
                    client.logout();
                    client.disconnect(true);
                }
            } catch (Exception ignore) { }
        }
        return result;
    }
    private long clampResume(String remotePath, FTPClient client, long resumeAt) {
        long safeResume = Math.max(0L, resumeAt);
        try {
            long remoteSize = client.fileSize(remotePath);
            if (remoteSize > 0 && safeResume >= remoteSize) {
                // 이미 모두 내려받았으므로 재개 없이 검증 단계로 넘긴다.
                return remoteSize;
            }
        } catch (Exception ignore) {
            // size 조회 실패 시 기존 resume 값을 그대로 사용한다.
        }
        return safeResume;
    }
}

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
    private static final int DEFAULT_BUFFER_SIZE = 2 * 1024 * 1024;
    private static final int MIN_BUFFER_SIZE = 64 * 1024;

    private final String host;
    private final int port;
    private final String id;
    private final String pw;
    private final String charset;
    private final int bufferSize;

    public FTP4JUtil(Context ctx, String host, int port, String id, String pw) {
        this(ctx, host, port, id, pw, "UTF-8", DEFAULT_BUFFER_SIZE);
    }

    public FTP4JUtil(Context ctx, String host, int port, String id, String pw, String charset, int bufferSize) {
        this.host = TextUtils.isEmpty(host) ? "127.0.0.1" : host;
        this.port = port > 0 ? port : AndoWSignageApp.FTP_PORT;
        this.id = TextUtils.isEmpty(id) ? "" : id;
        this.pw = TextUtils.isEmpty(pw) ? "" : pw;
        this.charset = TextUtils.isEmpty(charset) ? "UTF-8" : charset;
        this.bufferSize = Math.max(MIN_BUFFER_SIZE, bufferSize);
    }

    public static final class DownloadResult {
        public boolean success;
        public boolean missing;
        public long downloadedBytes;
        public String errorMessage;
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

            prepareClient(client);
            client.connect(host, port);
            client.login(id, pw);

            long localSize = targetFile.exists() ? targetFile.length() : 0L;
            long resumeAt = Math.max(0L, Math.max(resumeHint, localSize));
            resumeAt = clampResume(remotePath, client, resumeAt);
            long remoteSize = queryRemoteSize(client, remotePath);

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

            try (RandomAccessBufferedOutputStream output = openBufferedOutput(targetFile, resumeAt, remoteSize, null, null)) {
                client.download(remotePath, output, resumeAt, listener);
            }
            result.success = true;
            result.downloadedBytes = targetFile.length();
        } catch (FTPException e) {
            int code = e.getCode();
            if (code == 550 || code == 553) {
                result.missing = true;
                result.errorMessage = "MISSING_FILE";
            } else {
                result.errorMessage = "FTP_ERROR:" + e.getMessage();
            }
            UpdateQueueLogger.log("FTP4J FTPException code=" + e.getCode() + " msg=" + e.getMessage());
        } catch (FTPDataTransferException | FTPAbortedException | FTPIllegalReplyException | java.io.IOException e) {
            result.errorMessage = "IO_ERROR:" + e.getMessage();
            UpdateQueueLogger.log("FTP4J download failed: " + e.getMessage());
        } catch (Exception e) {
            result.errorMessage = "FTP_ERROR:" + e.getMessage();
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

    public DownloadResult downloadRange(String remotePath,
                                        File targetFile,
                                        long offset,
                                        long length,
                                        long totalSize,
                                        java.util.concurrent.atomic.AtomicBoolean stopFlag) {
        DownloadResult result = new DownloadResult();
        FTPClient client = new FTPClient();
        java.util.concurrent.atomic.AtomicLong written = new java.util.concurrent.atomic.AtomicLong(0L);
        java.util.concurrent.atomic.AtomicBoolean abortedByLimit = new java.util.concurrent.atomic.AtomicBoolean(false);
        java.util.concurrent.atomic.AtomicBoolean abortedByStop = new java.util.concurrent.atomic.AtomicBoolean(false);
        long safeLength = Math.max(0L, length);
        if (targetFile == null || TextUtils.isEmpty(remotePath)) {
            result.errorMessage = "REMOTE_PATH_EMPTY";
            return result;
        }
        if (safeLength == 0L) {
            result.success = true;
            return result;
        }
        try {
            File parent = targetFile.getParentFile();
            if (parent != null && !parent.exists()) {
                parent.mkdirs();
            }

            prepareClient(client);
            client.connect(host, port);
            client.login(id, pw);

            long ensureSize = totalSize;
            if (ensureSize <= 0) {
                ensureSize = queryRemoteSize(client, remotePath);
            }

            try (RandomAccessBufferedOutputStream output =
                         openBufferedOutput(targetFile, offset, ensureSize, written, safeLength, stopFlag, abortedByStop)) {

                FTPDataTransferListener listener = new FTPDataTransferListener() {
                    @Override
                    public void started() { }
                    @Override
                    public void transferred(int length) {
                        if (stopFlag != null && stopFlag.get()) {
                            abortedByStop.set(true);
                            try {
                                client.abortCurrentDataTransfer(true);
                            } catch (Exception ignore) { }
                            return;
                        }
                        if (!abortedByLimit.get() && written.get() >= safeLength) {
                            abortedByLimit.set(true);
                            try {
                                client.abortCurrentDataTransfer(true);
                            } catch (Exception ignore) { }
                        }
                    }
                    @Override
                    public void completed() { }
                    @Override
                    public void aborted() { }
                    @Override
                    public void failed() { }
                };

                client.download(remotePath, output, Math.max(0L, offset), listener);
            }

            result.downloadedBytes = written.get();
            result.success = written.get() >= safeLength;
            if (!result.success) {
                result.errorMessage = "INCOMPLETE_RANGE";
            }
        } catch (FTPAbortedException e) {
            result.downloadedBytes = written.get();
            if (abortedByStop.get()) {
                result.errorMessage = "LEASE_LOST";
            } else if (abortedByLimit.get() && written.get() >= safeLength) {
                result.success = true;
            } else {
                result.errorMessage = "FTP_ABORTED";
            }
        } catch (FTPException e) {
            int code = e.getCode();
            if (code == 550 || code == 553) {
                result.missing = true;
                result.errorMessage = "MISSING_FILE";
            } else {
                result.errorMessage = "FTP_ERROR:" + e.getMessage();
            }
            UpdateQueueLogger.log("FTP4J FTPException code=" + e.getCode() + " msg=" + e.getMessage());
        } catch (FTPDataTransferException | FTPIllegalReplyException | java.io.IOException e) {
            if (abortedByStop.get()) {
                result.errorMessage = "LEASE_LOST";
            } else {
                result.errorMessage = "IO_ERROR:" + e.getMessage();
            }
            UpdateQueueLogger.log("FTP4J download failed: " + e.getMessage());
        } catch (Exception e) {
            result.errorMessage = "FTP_ERROR:" + e.getMessage();
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

    private void prepareClient(FTPClient client) {
        client.setPassive(true);
        client.setCharset(charset);
        client.setType(FTPClient.TYPE_BINARY);
        client.setAutoNoopTimeout(30_000);
    }

    private long queryRemoteSize(FTPClient client, String remotePath) {
        try {
            return client.fileSize(remotePath);
        } catch (Exception ignore) {
            return 0L;
        }
    }

    private RandomAccessBufferedOutputStream openBufferedOutput(File targetFile,
                                                                long offset,
                                                                long ensureSize,
                                                                java.util.concurrent.atomic.AtomicLong written,
                                                                long maxBytes,
                                                                java.util.concurrent.atomic.AtomicBoolean stopFlag,
                                                                java.util.concurrent.atomic.AtomicBoolean abortedByStop) throws java.io.IOException {
        return openBufferedOutput(targetFile, offset, ensureSize, written, maxBytes, stopFlag, abortedByStop, false);
    }

    private RandomAccessBufferedOutputStream openBufferedOutput(File targetFile,
                                                                long offset,
                                                                long ensureSize,
                                                                java.util.concurrent.atomic.AtomicLong written,
                                                                java.util.concurrent.atomic.AtomicBoolean abortedByStop) throws java.io.IOException {
        return openBufferedOutput(targetFile, offset, ensureSize, written, Long.MAX_VALUE, null, abortedByStop, true);
    }

    private RandomAccessBufferedOutputStream openBufferedOutput(File targetFile,
                                                                long offset,
                                                                long ensureSize,
                                                                java.util.concurrent.atomic.AtomicLong written,
                                                                long maxBytes,
                                                                java.util.concurrent.atomic.AtomicBoolean stopFlag,
                                                                java.util.concurrent.atomic.AtomicBoolean abortedByStop,
                                                                boolean useFileLengthOnClose) throws java.io.IOException {
        return new RandomAccessBufferedOutputStream(targetFile,
                Math.max(0L, offset),
                ensureSize,
                bufferSize,
                written,
                maxBytes,
                stopFlag,
                abortedByStop,
                useFileLengthOnClose);
    }

    private static final class RandomAccessBufferedOutputStream extends java.io.OutputStream {
        private final java.io.RandomAccessFile raf;
        private final byte[] buffer;
        private final java.util.concurrent.atomic.AtomicLong written;
        private final long maxBytes;
        private final java.util.concurrent.atomic.AtomicBoolean stopFlag;
        private final java.util.concurrent.atomic.AtomicBoolean abortedByStop;
        private final boolean useFileLengthOnClose;
        private int bufferedCount = 0;

        RandomAccessBufferedOutputStream(File targetFile,
                                         long offset,
                                         long ensureSize,
                                         int bufferSize,
                                         java.util.concurrent.atomic.AtomicLong written,
                                         long maxBytes,
                                         java.util.concurrent.atomic.AtomicBoolean stopFlag,
                                         java.util.concurrent.atomic.AtomicBoolean abortedByStop,
                                         boolean useFileLengthOnClose) throws java.io.IOException {
            this.raf = new java.io.RandomAccessFile(targetFile, "rw");
            if (ensureSize > 0 && raf.length() < ensureSize) {
                raf.setLength(ensureSize);
            }
            raf.seek(Math.max(0L, offset));
            this.buffer = new byte[Math.max(MIN_BUFFER_SIZE, bufferSize)];
            this.written = written;
            this.maxBytes = maxBytes <= 0 ? Long.MAX_VALUE : maxBytes;
            this.stopFlag = stopFlag;
            this.abortedByStop = abortedByStop;
            this.useFileLengthOnClose = useFileLengthOnClose;
        }

        @Override
        public void write(int b) throws java.io.IOException {
            byte[] single = new byte[] { (byte) b };
            write(single, 0, 1);
        }

        @Override
        public void write(byte[] b, int off, int len) throws java.io.IOException {
            if (b == null) {
                throw new NullPointerException("buffer");
            }
            if (off < 0 || len < 0 || off + len > b.length) {
                throw new IndexOutOfBoundsException("Invalid write bounds");
            }
            checkStopRequested();
            int remainingToCopy = len;
            int cursor = off;
            while (remainingToCopy > 0) {
                long remaining = remainingCapacity();
                if (remaining <= 0) {
                    return;
                }
                if (bufferedCount == buffer.length) {
                    flushBuffer();
                }
                int toCopy = (int) Math.min(Math.min((long) (buffer.length - bufferedCount), remaining), remainingToCopy);
                System.arraycopy(b, cursor, buffer, bufferedCount, toCopy);
                bufferedCount += toCopy;
                cursor += toCopy;
                remainingToCopy -= toCopy;
                if (written != null) {
                    written.addAndGet(toCopy);
                }
            }
        }

        @Override
        public void flush() throws java.io.IOException {
            flushBuffer();
        }

        @Override
        public void close() throws java.io.IOException {
            try {
                flushBuffer();
                if (useFileLengthOnClose && written != null) {
                    written.set(Math.max(0L, raf.length()));
                }
            } finally {
                raf.close();
            }
        }

        private long remainingCapacity() {
            if (maxBytes == Long.MAX_VALUE) {
                return Long.MAX_VALUE;
            }
            long logicalWritten = written == null ? 0L : written.get();
            return maxBytes - logicalWritten;
        }

        private void flushBuffer() throws java.io.IOException {
            checkStopRequested();
            if (bufferedCount <= 0) {
                return;
            }
            raf.write(buffer, 0, bufferedCount);
            bufferedCount = 0;
        }

        private void checkStopRequested() throws java.io.IOException {
            if (stopFlag != null && stopFlag.get()) {
                if (abortedByStop != null) {
                    abortedByStop.set(true);
                }
                throw new java.io.IOException("LEASE_LOST");
            }
        }
    }
}

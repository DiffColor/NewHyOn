package kr.co.turtlelab.andowsignage.data.update;

import android.content.Context;
import android.text.TextUtils;

import java.io.File;
import java.util.List;
import java.util.Locale;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.tools.FTP4JUtil;

/**
 * UpdateQueue 의 downloadContentsJson 을 기준으로 실제 FTP 다운로드 및 검증을 수행한다.
 */
public class UpdateQueueDownloader {

    public interface LeaseHandler {
        UpdateThrottleModels.UpdateThrottleSettings getSettings();
        boolean ensureLease(RealmUpdateQueue queue, UpdateThrottleModels.UpdateThrottleSettings settings);
        boolean tryRenewLeaseIfNeeded(UpdateThrottleModels.UpdateThrottleSettings settings);
    }

    public static final class DownloadOutcome {
        public boolean success;
        public boolean missing;
        public boolean leaseBusy;
        public boolean leaseLost;
        public String lastError;
    }

    private LeaseHandler leaseHandler;

    public UpdateQueueDownloader() {
        this(null);
    }

    public UpdateQueueDownloader(LeaseHandler leaseHandler) {
        this.leaseHandler = leaseHandler;
    }

    public void setLeaseHandler(LeaseHandler leaseHandler) {
        this.leaseHandler = leaseHandler;
    }

    public DownloadOutcome download(RealmUpdateQueue queue, UpdateProgressTracker tracker) {
        return download(queue, tracker, false);
    }

    public DownloadOutcome download(RealmUpdateQueue queue, UpdateProgressTracker tracker, boolean ignoreLease) {
        DownloadOutcome outcome = new DownloadOutcome();
        UpdateThrottleModels.UpdateThrottleSettings settings = ignoreLease ? null
                : (leaseHandler == null ? null : leaseHandler.getSettings());
        if (!ignoreLease && leaseHandler != null && !leaseHandler.ensureLease(queue, settings)) {
            outcome.leaseBusy = true;
            outcome.success = false;
            return outcome;
        }
        PendingDownloadWatcher.resetStaleDownloads(queue);
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        journal.ensureDefaults();
        List<UpdateQueueContract.DownloadEntry> entries = journal.getEntries();
        if (entries == null || entries.isEmpty()) {
            tracker.stepDownload(1f);
            outcome.success = true;
            return outcome;
        }
        final java.util.concurrent.atomic.AtomicBoolean stopRenew = new java.util.concurrent.atomic.AtomicBoolean(false);
        final java.util.concurrent.atomic.AtomicBoolean renewFailed = new java.util.concurrent.atomic.AtomicBoolean(false);
        Thread renewThread = null;
        if (!ignoreLease && leaseHandler != null && settings != null) {
            renewThread = new Thread(() -> {
                while (!stopRenew.get()) {
                    try {
                        Thread.sleep(1000);
                    } catch (InterruptedException ignore) {
                    }
                    if (!leaseHandler.tryRenewLeaseIfNeeded(settings)) {
                        renewFailed.set(true);
                        return;
                    }
                }
            });
            renewThread.start();
        }
        tracker.stepDownload(calculateDownloadProgress(entries));
        try {
            for (UpdateQueueContract.DownloadEntry entry : entries) {
                if (entry == null) {
                    continue;
                }
                try {
                    if (UpdateQueueContract.DownloadStatus.DONE.equalsIgnoreCase(entry.Status)) {
                        continue;
                    }
                    if (!ignoreLease && renewFailed.get()) {
                        outcome.leaseLost = true;
                        outcome.success = false;
                        return outcome;
                    }
                    if (!ignoreLease && leaseHandler != null && !leaseHandler.ensureLease(queue, settings)) {
                        outcome.leaseBusy = true;
                        outcome.success = false;
                        return outcome;
                    }
                    entry.Status = UpdateQueueContract.DownloadStatus.DOWNLOADING;
                    markChunkStatus(entry, UpdateQueueContract.ChunkStatus.DOWNLOADING, 0L);
                    UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
                    boolean success = downloadSingle(entry, renewFailed);
                    if (!success) {
                        String lastError = entry.LastError == null ? "" : entry.LastError;
                        outcome.lastError = TextUtils.isEmpty(lastError)
                                ? ("Download failed: " + entry.FileName)
                                : (entry.FileName + ": " + lastError);
                        if (!ignoreLease && lastError.toUpperCase(Locale.US).startsWith("LEASE")) {
                            outcome.leaseLost = true;
                            outcome.success = false;
                            return outcome;
                        }
                        entry.Status = UpdateQueueContract.DownloadStatus.QUEUED;
                        entry.Attempts += 1;
                        resetChunksToPending(entry);
                        UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
                        tracker.stepDownload(calculateDownloadProgress(entries));
                        outcome.success = false;
                        return outcome;
                    }
                    entry.Status = UpdateQueueContract.DownloadStatus.DONE;
                    entry.Attempts += 1;
                    markChunkDone(entry, entry.SizeBytes);
                    entry.LastError = "";
                    UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
                    tracker.stepDownload(calculateDownloadProgress(entries));
                } catch (Exception ex) {
                    entry.Status = UpdateQueueContract.DownloadStatus.QUEUED;
                    entry.LastError = ex.getMessage();
                    entry.Attempts += 1;
                    resetChunksToPending(entry);
                    UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
                    tracker.stepDownload(calculateDownloadProgress(entries));
                    outcome.lastError = "Download failed: " + entry.FileName + " / " + ex.getMessage();
                    outcome.success = false;
                    return outcome;
                }
            }
            outcome.success = true;
            return outcome;
        } finally {
            stopRenew.set(true);
            if (renewThread != null) {
                try {
                    renewThread.join(1000);
                } catch (InterruptedException ignore) {
                }
            }
        }
    }

    private boolean downloadSingle(UpdateQueueContract.DownloadEntry entry,
                                   java.util.concurrent.atomic.AtomicBoolean renewFailed) {
        if (entry == null) {
            return true;
        }
        String relativeFilePath = TextUtils.isEmpty(entry.RemotePath) ? entry.FileName : entry.RemotePath;
        if (TextUtils.isEmpty(relativeFilePath)) {
            entry.LastError = "REMOTE_PATH_EMPTY";
            return false;
        }
        String localFileName = UpdateQueueHelper.normalizeFileName(entry.FileName);
        if (TextUtils.isEmpty(localFileName)) {
            localFileName = UpdateQueueHelper.normalizeFileName(relativeFilePath);
        }
        if (TextUtils.isEmpty(localFileName)) {
            entry.LastError = "LOCAL_FILENAME_EMPTY";
            return false;
        }
        String finalPath = UpdateQueueHelper.getFinalContentPath(localFileName);
        ensureParentDir(finalPath);
        File finalFile = new File(finalPath);
        String stagingPath = UpdateQueueHelper.getTempContentPath(localFileName);
        ensureParentDir(stagingPath);
        File stagingFile = new File(stagingPath);
        // 기존 파일이 유효하면 다운로드를 건너뛴다.
        if (FileIntegrityUtils.verifyFile(finalFile, entry.SizeBytes, entry.Checksum)) {
            return true;
        }
        UpdateQueueLogger.log("Staging download to " + stagingPath + " (final: " + finalPath + ")");
        Context ctx = AndoWSignage.getCtx();
        if (ctx == null) {
            return false;
        }

        ensureTempFileLength(stagingFile, entry.SizeBytes);

        String ftpHost = resolveFtpHost();
        int ftpPort = resolveFtpPort();
        String remotePath = buildRemotePath(resolveFtpRootPath(), relativeFilePath);
        FTP4JUtil ftp = new FTP4JUtil(ctx,
                ftpHost,
                ftpPort,
                AndoWSignageApp.FTP_LOGIN_ID,
                AndoWSignageApp.FTP_LOGIN_PW);
        UpdateQueueLogger.log("Downloading " + entry.FileName + " from " + remotePath + " via FTP " + ftpHost + ":" + ftpPort);
        ensureChunks(entry);
        List<UpdateQueueContract.DownloadChunk> chunks = entry.Chunks;
        for (UpdateQueueContract.DownloadChunk chunk : chunks) {
            if (chunk == null) {
                continue;
            }
            if (UpdateQueueContract.ChunkStatus.DONE.equalsIgnoreCase(chunk.Status)) {
                continue;
            }
            if (renewFailed != null && renewFailed.get()) {
                entry.LastError = "LEASE_LOST";
                return false;
            }
            chunk.Status = UpdateQueueContract.ChunkStatus.DOWNLOADING;
            chunk.LastUpdatedTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
            long length = chunk.Length > 0 ? chunk.Length : Math.max(0L, entry.SizeBytes - chunk.Offset);
            FTP4JUtil.DownloadResult result = ftp.downloadRange(remotePath,
                    stagingFile,
                    chunk.Offset,
                    length,
                    entry.SizeBytes,
                    renewFailed);
            if (result.missing) {
                entry.LastError = "MISSING_FILE";
                return false;
            }
            if (!result.success) {
                entry.LastError = TextUtils.isEmpty(result.errorMessage)
                        ? "CHUNK_FAIL"
                        : result.errorMessage;
                chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
                chunk.DownloadedBytes = 0L;
                chunk.LastUpdatedTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
                return false;
            }
            chunk.Status = UpdateQueueContract.ChunkStatus.DONE;
            chunk.DownloadedBytes = length;
            chunk.LastUpdatedTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
        }
        if (!FileIntegrityUtils.verifyFile(stagingFile, entry.SizeBytes, entry.Checksum)) {
            entry.LastError = "HASH_MISMATCH";
            return false;
        }
        try {
            if (finalFile.exists() && !finalFile.delete()) {
                entry.LastError = "FINAL_DELETE_FAIL";
                return false;
            }
            File parent = finalFile.getParentFile();
            if (parent != null && !parent.exists()) {
                parent.mkdirs();
            }
            if (!stagingFile.renameTo(finalFile)) {
                // rename 실패 시 스트림 복사로 대체
                try (java.io.InputStream in = new java.io.FileInputStream(stagingFile);
                     java.io.OutputStream out = new java.io.FileOutputStream(finalFile)) {
                    byte[] buf = new byte[8192];
                    int read;
                    while ((read = in.read(buf)) > 0) {
                        out.write(buf, 0, read);
                    }
                }
                stagingFile.delete();
            }
        } catch (Exception ex) {
            entry.LastError = "MOVE_FAIL";
            return false;
        }
        return true;
    }

    private float calculateDownloadProgress(List<UpdateQueueContract.DownloadEntry> entries) {
        if (entries == null || entries.isEmpty()) {
            return 1f;
        }
        int total = entries.size();
        int done = 0;
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry != null && UpdateQueueContract.DownloadStatus.DONE.equalsIgnoreCase(entry.Status)) {
                done++;
            }
        }
        return Math.min(1f, (float) done / Math.max(1, total));
    }

    private void resetChunksToPending(UpdateQueueContract.DownloadEntry entry) {
        if (entry == null || entry.Chunks == null) {
            return;
        }
        long nowTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
        for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
            if (chunk == null) {
                continue;
            }
            chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
            chunk.DownloadedBytes = 0L;
            chunk.LastUpdatedTicks = nowTicks;
        }
    }

    private void markChunkStatus(UpdateQueueContract.DownloadEntry entry, String status, long downloadedBytes) {
        if (entry == null || entry.Chunks == null) {
            return;
        }
        long nowTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
        for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
            if (chunk == null) {
                continue;
            }
            chunk.Status = status;
            if (downloadedBytes >= 0) {
                chunk.DownloadedBytes = downloadedBytes;
            }
            chunk.LastUpdatedTicks = nowTicks;
        }
    }

    private void markChunkDone(UpdateQueueContract.DownloadEntry entry, long downloadedBytes) {
        markChunkStatus(entry, UpdateQueueContract.ChunkStatus.DONE, downloadedBytes);
    }

    private void ensureParentDir(String filePath) {
        if (TextUtils.isEmpty(filePath)) {
            return;
        }
        try {
            File parent = new File(filePath).getParentFile();
            if (parent != null && !parent.exists()) {
                parent.mkdirs();
            }
        } catch (Exception ignore) {
        }
    }

    private void cleanupTempFile(String relativeFilePath) {
        if (TextUtils.isEmpty(relativeFilePath)) {
            return;
        }
        try {
            String name = UpdateQueueHelper.normalizeFileName(relativeFilePath);
            String tempPath = UpdateQueueHelper.getTempContentPath(name);
            File tempFile = new File(tempPath);
            if (tempFile.exists()) {
                tempFile.delete();
            }
        } catch (Exception ignore) {
        }
    }

    private void ensureTempFileLength(File file, long size) {
        if (file == null) {
            return;
        }
        try {
            ensureParentDir(file.getAbsolutePath());
            if (size <= 0) {
                return;
            }
            if (!file.exists()) {
                file.createNewFile();
            }
            try (java.io.RandomAccessFile raf = new java.io.RandomAccessFile(file, "rw")) {
                if (raf.length() < size) {
                    raf.setLength(size);
                }
            }
        } catch (Exception ignore) {
        }
    }

    private void ensureChunks(UpdateQueueContract.DownloadEntry entry) {
        if (entry == null) {
            return;
        }
        if (entry.Chunks == null) {
            entry.Chunks = new java.util.ArrayList<>();
        }
        if (!entry.Chunks.isEmpty()) {
            return;
        }
        long size = Math.max(0L, entry.SizeBytes);
        long nowTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
        if (size <= 0) {
            UpdateQueueContract.DownloadChunk chunk = new UpdateQueueContract.DownloadChunk();
            chunk.Index = 0;
            chunk.Offset = 0;
            chunk.Length = 0;
            chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
            chunk.DownloadedBytes = 0;
            chunk.LastUpdatedTicks = nowTicks;
            entry.Chunks.add(chunk);
            return;
        }
        final long chunkSize = 4L * 1024L * 1024L;
        int idx = 0;
        long offset = 0;
        while (offset < size) {
            long length = Math.min(chunkSize, size - offset);
            UpdateQueueContract.DownloadChunk chunk = new UpdateQueueContract.DownloadChunk();
            chunk.Index = idx;
            chunk.Offset = offset;
            chunk.Length = length;
            chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
            chunk.DownloadedBytes = 0;
            chunk.LastUpdatedTicks = nowTicks;
            entry.Chunks.add(chunk);
            offset += length;
            idx++;
        }
    }

    private String resolveFtpHost() {
        String dataServerIp = LocalSettingsProvider.getDataServerIp();
        if (!TextUtils.isEmpty(dataServerIp)) {
            return dataServerIp;
        }
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return AndoWSignageApp.MANUAL_IP;
        }
        return AndoWSignageApp.MANAGER_IP;
    }

    private int resolveFtpPort() {
        int configured = LocalSettingsProvider.getFtpPort();
        if (configured > 0 && configured <= 65535) {
            return configured;
        }
        return AndoWSignageApp.FTP_PORT;
    }

    private String resolveFtpRootPath() {
        String root = LocalSettingsProvider.getFtpRootPath();
        return normalizeRemotePath(root, "/NewHyOnEnt");
    }

    private String buildRemotePath(String rootPath, String relativePath) {
        String normalizedRoot = normalizeRemotePath(rootPath, "/NewHyOnEnt");
        String normalizedRelative = normalizeRemotePath(relativePath, "/");
        if (TextUtils.isEmpty(normalizedRelative) || "/".equals(normalizedRelative)) {
            return normalizedRoot;
        }
        if (normalizedRelative.equalsIgnoreCase(normalizedRoot)
                || normalizedRelative.toLowerCase(Locale.US).startsWith((normalizedRoot + "/").toLowerCase(Locale.US))) {
            return normalizedRelative;
        }
        return normalizedRoot + "/" + normalizedRelative.substring(1);
    }

    private String normalizeRemotePath(String path, String fallback) {
        if (TextUtils.isEmpty(path)) {
            return fallback;
        }
        String normalized = path.replace("\\", "/").trim();
        if (!normalized.startsWith("/")) {
            normalized = "/" + normalized;
        }
        while (normalized.length() > 1 && normalized.endsWith("/")) {
            normalized = normalized.substring(0, normalized.length() - 1);
        }
        return TextUtils.isEmpty(normalized) ? fallback : normalized;
    }

}

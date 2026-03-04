package kr.co.turtlelab.andowsignage.data.update;

import android.content.Context;
import android.text.TextUtils;

import java.io.File;
import java.util.List;
import java.util.Locale;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.tools.FTP4JUtil;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import io.realm.Realm;

/**
 * UpdateQueue 의 downloadContentsJson 을 기준으로 실제 FTP 다운로드 및 검증을 수행한다.
 */
public class UpdateQueueDownloader {

    public static final class DownloadOutcome {
        public boolean success;
        public boolean missing;
    }

    public DownloadOutcome download(RealmUpdateQueue queue, UpdateProgressTracker tracker) {
        DownloadOutcome outcome = new DownloadOutcome();
        PendingDownloadWatcher.resetStaleDownloads(queue);
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        journal.ensureDefaults();
        List<UpdateQueueContract.DownloadContentEntry> pending = journal.getPendingEntriesSortedBySize();
        if (pending.isEmpty()) {
            tracker.stepDownload(1f);
            outcome.success = true;
            return outcome;
        }
        long totalWeight = calculateTotalWeight(journal.getEntries());
        long doneWeight = calculateDoneWeight(journal.getEntries());
        if (totalWeight > 0) {
            tracker.stepDownload(Math.min(1f, (float) doneWeight / (float) totalWeight));
        }
        for (UpdateQueueContract.DownloadContentEntry entry : pending) {
            journal.updateEntryStatus(entry.contentUid, UpdateQueueContract.DownloadStatus.DOWNLOADING, clampDownloadedBytes(entry, entry.downloadedBytes));
            UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
            DownloadOutcome singleOutcome = downloadSingle(queue, entry);
            if (singleOutcome.missing) {
                outcome.missing = true;
                outcome.success = false;
                return outcome;
            }
            if (!singleOutcome.success) {
                long delay = UpdateQueueContract.RetryPolicy.getDelayMs(queue.getRetryCount() + 1);
                UpdateQueueHelper.incrementRetry(queue.getId(), System.currentTimeMillis() + delay);
                // 실패 시 진행률을 초기화해 재시도하도록 한다.
                journal.resetAllToPending();
                UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
                UpdateQueueHelper.updateProgress(queue.getId(), 0f, 0f, 0f);
                outcome.success = false;
                return outcome;
            }
            // 파일 상태 변화가 반영된 최신 저널을 저장
            UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
            doneWeight = calculateDoneWeight(journal.getEntries());
            float dlUnit = totalWeight == 0 ? 1f : Math.min(1f, (float) doneWeight / (float) totalWeight);
            tracker.stepDownload(dlUnit);
        }
        outcome.success = true;
        return outcome;
    }

    private DownloadOutcome downloadSingle(RealmUpdateQueue queue, UpdateQueueContract.DownloadContentEntry entry) {
        DownloadOutcome outcome = new DownloadOutcome();
        if (entry == null || TextUtils.isEmpty(entry.fileName)) {
            outcome.success = true;
            return outcome;
        }
        String relativeFilePath = entry.remotePath;
        String finalPath = LocalPathUtils.getAbsolutePath(relativeFilePath);
        ensureParentDir(finalPath);
        File finalFile = new File(finalPath);
        String stagingPath = LocalPathUtils.getTempPath(relativeFilePath);
        ensureParentDir(stagingPath);
        File stagingFile = new File(stagingPath);
        // 먼저 스테이징 파일이 유효하면 그대로 사용
        if (FileIntegrityUtils.verifyFile(stagingFile, entry.sizeBytes, entry.checksum)) {
            entry.status = UpdateQueueContract.DownloadStatus.DONE;
            entry.downloadedBytes = entry.sizeBytes > 0 ? entry.sizeBytes : stagingFile.length();
            entry.lastUpdatedAt = System.currentTimeMillis();
            outcome.success = true;
            return outcome;
        }
        // 기존 최종 파일이 유효하면 다운로드를 건너뛴다.
        if (FileIntegrityUtils.verifyFile(finalFile, entry.sizeBytes, entry.checksum)) {
            entry.status = UpdateQueueContract.DownloadStatus.DONE;
            entry.downloadedBytes = entry.sizeBytes > 0 ? entry.sizeBytes : finalFile.length();
            entry.lastUpdatedAt = System.currentTimeMillis();
            outcome.success = true;
            return outcome;
        }
        UpdateQueueLogger.log("Staging download to " + stagingPath + " (final: " + finalPath + ")");
        Context ctx = AndoWSignage.getCtx();
        if (ctx == null) {
            outcome.success = false;
            return outcome;
        }

        long existingBytes = safeFileLength(stagingFile);
        long resumeFrom = clampDownloadedBytes(entry, Math.max(existingBytes, entry.downloadedBytes));
        entry.status = UpdateQueueContract.DownloadStatus.DOWNLOADING;
        entry.downloadedBytes = resumeFrom;
        entry.lastUpdatedAt = System.currentTimeMillis();

        String ftpHost = resolveDataServerHost();
        int ftpPort = resolveFtpPort();
        FTP4JUtil ftp = new FTP4JUtil(ctx,
                ftpHost,
                ftpPort,
                AndoWSignageApp.FTP_LOGIN_ID,
                AndoWSignageApp.FTP_LOGIN_PW);
        String remotePath = entry.remotePath;
        UpdateQueueLogger.log("Downloading " + entry.fileName + " from " + remotePath + " via FTP " + ftpHost + ":" + ftpPort);
        FTP4JUtil.DownloadResult result = ftp.downloadWithResume(remotePath, stagingFile, resumeFrom);
        if (result.missing) {
            entry.status = UpdateQueueContract.DownloadStatus.FAILED;
            entry.lastUpdatedAt = System.currentTimeMillis();
            stagingFile.delete();
            cleanupTempFile(relativeFilePath);
            handleMissingFile(queue, entry);
            outcome.success = false;
            outcome.missing = true;
            return outcome;
        }
        if (!result.success) {
            entry.status = UpdateQueueContract.DownloadStatus.PENDING;
            entry.downloadedBytes = clampDownloadedBytes(entry, safeFileLength(stagingFile));
            entry.lastUpdatedAt = System.currentTimeMillis();
            outcome.success = false;
            return outcome;
        }
        entry.downloadedBytes = clampDownloadedBytes(entry, safeFileLength(stagingFile));
        entry.lastUpdatedAt = System.currentTimeMillis();

        if (!FileIntegrityUtils.verifyFile(stagingFile, entry.sizeBytes, entry.checksum)) {
            stagingFile.delete();
            entry.status = UpdateQueueContract.DownloadStatus.FAILED;
            entry.lastUpdatedAt = System.currentTimeMillis();
            handleMissingFile(queue, entry);
            outcome.success = false;
            outcome.missing = true;
            return outcome;
        }
        entry.status = UpdateQueueContract.DownloadStatus.DONE;
        entry.downloadedBytes = entry.sizeBytes > 0 ? entry.sizeBytes : stagingFile.length();
        entry.lastUpdatedAt = System.currentTimeMillis();
        outcome.success = true;
        return outcome;
    }

    private long calculateTotalWeight(List<UpdateQueueContract.DownloadContentEntry> entries) {
        long total = 0L;
        if (entries == null) {
            return 0L;
        }
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            long weight = entry.sizeBytes > 0 ? entry.sizeBytes : 1L;
            total += weight;
        }
        return total;
    }

    private long calculateDoneWeight(List<UpdateQueueContract.DownloadContentEntry> entries) {
        long done = 0L;
        if (entries == null) {
            return 0L;
        }
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            long weight = entry.sizeBytes > 0 ? entry.sizeBytes : 1L;
            if (UpdateQueueContract.DownloadStatus.DONE.equals(entry.status)) {
                done += weight;
            } else {
                long partial = clampDownloadedBytes(entry, entry.downloadedBytes);
                done += Math.min(weight, partial);
            }
        }
        return done;
    }

    private long clampDownloadedBytes(UpdateQueueContract.DownloadContentEntry entry, long bytes) {
        long safe = Math.max(0L, bytes);
        if (entry != null && entry.sizeBytes > 0) {
            return Math.min(entry.sizeBytes, safe);
        }
        return safe;
    }

    private long safeFileLength(File file) {
        return (file != null && file.exists()) ? file.length() : 0L;
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

    private String resolveDataServerHost() {
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return AndoWSignageApp.MANUAL_IP;
        }
        String host = LocalSettingsProvider.getDataServerIp();
        if (TextUtils.isEmpty(host)) {
            host = AndoWSignageApp.MANAGER_IP;
        }
        return TextUtils.isEmpty(host) ? "127.0.0.1" : host;
    }

    private int resolveFtpPort() {
        int port = LocalSettingsProvider.getFtpPort();
        if (port > 0) {
            return port;
        }
        return AndoWSignageApp.FTP_PORT;
    }

    private void cleanupTempFile(String relativeFilePath) {
        if (TextUtils.isEmpty(relativeFilePath)) {
            return;
        }
        try {
            String tempPath = LocalPathUtils.getTempPath(relativeFilePath);
            File tempFile = new File(tempPath);
            if (tempFile.exists()) {
                tempFile.delete();
            }
        } catch (Exception ignore) {
        }
    }

    private void handleMissingFile(RealmUpdateQueue queue, UpdateQueueContract.DownloadContentEntry entry) {
        if (queue == null) {
            return;
        }
        String message = "Missing file: " + (entry == null ? "" : entry.fileName);
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.FAILED, "MISSING_FILE", message);
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        try {
            if (!TextUtils.isEmpty(playerId)) {
                RethinkDbClient.getInstance().deleteQueueRecord(String.valueOf(queue.getId()), playerId);
            } else {
                RethinkDbClient.getInstance().deleteQueueRecord(String.valueOf(queue.getId()));
            }
        } catch (Exception ignore) {
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            realm.executeTransaction(r -> {
                RealmUpdateQueue target = r.where(RealmUpdateQueue.class)
                        .equalTo("id", queue.getId())
                        .findFirst();
                if (target != null) {
                    target.deleteFromRealm();
                }
            });
        } finally {
            realm.close();
        }
    }
}

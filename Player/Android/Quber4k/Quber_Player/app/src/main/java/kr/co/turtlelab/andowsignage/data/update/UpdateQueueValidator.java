package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import java.io.File;
import java.util.List;

import kr.co.turtlelab.andowsignage.data.store.StoredUpdateQueue;

/**
 * READY 상태로 전환하기 전에 다운로드된 파일들이 모두 존재하고 무결한지 검증한다.
 */
public class UpdateQueueValidator {

    private String lastError = "";

    public String getLastError() {
        return lastError;
    }

    public boolean validate(StoredUpdateQueue queue, UpdateProgressTracker tracker) {
        lastError = "";
        if (queue == null) {
            return false;
        }
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        journal.ensureDefaults();
        List<UpdateQueueContract.DownloadEntry> entries = journal.getEntries();
        if (entries == null || entries.isEmpty()) {
            return true;
        }
        int total = entries.size();
        int validated = 0;
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry == null) {
                validated++;
                tracker.stepValidate((float) validated / Math.max(1, total));
                continue;
            }
            String fileName = UpdateQueueHelper.normalizeFileName(entry.FileName);
            if (TextUtils.isEmpty(fileName)) {
                fileName = UpdateQueueHelper.normalizeFileName(entry.RemotePath);
                if (TextUtils.isEmpty(fileName)) {
                    validated++;
                    tracker.stepValidate((float) validated / Math.max(1, total));
                    continue;
                }
            }
            String tempPath = UpdateQueueHelper.getTempContentPath(fileName);
            File tempFile = new File(tempPath);
            String finalPath = UpdateQueueHelper.getFinalContentPath(fileName);
            File finalFile = new File(finalPath);
            File targetFile = tempFile.exists() ? tempFile : finalFile;
            if (!FileIntegrityUtils.verifyFile(targetFile, entry.SizeBytes, entry.Checksum)) {
                try {
                    if (tempFile.exists()) {
                        tempFile.delete();
                    }
                } catch (Exception ignore) {
                }
                entry.Status = UpdateQueueContract.DownloadStatus.QUEUED;
                entry.Attempts += 1;
                entry.LastError = "Validation failed: " + fileName;
                resetChunksToPending(entry);
                lastError = entry.LastError;
                UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
                return false;
            }
            validated++;
            tracker.stepValidate((float) validated / Math.max(1, total));
        }
        return true;
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
}

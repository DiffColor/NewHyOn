package kr.co.turtlelab.andowsignage.data.update;

import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;

/**
 * 다운로드 도중 장시간 정체된 항목을 초기화하여 재시도하도록 만드는 간단한 워처.
 */
public final class PendingDownloadWatcher {

    private static final long STALE_THRESHOLD_MS = 2 * 60 * 1000; // 2분

    private PendingDownloadWatcher() { }

    public static void resetStaleDownloads(RealmUpdateQueue queue) {
        if (queue == null) {
            return;
        }
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        journal.ensureDefaults();
        long now = System.currentTimeMillis();
        boolean changed = false;
        for (UpdateQueueContract.DownloadContentEntry entry : journal.getEntries()) {
            if (entry == null) {
                continue;
            }
            if (UpdateQueueContract.DownloadStatus.DOWNLOADING.equals(entry.status)
                    && entry.lastUpdatedAt > 0
                    && (now - entry.lastUpdatedAt) > STALE_THRESHOLD_MS) {
                entry.status = UpdateQueueContract.DownloadStatus.PENDING;
                entry.lastUpdatedAt = now;
                changed = true;
            }
        }
        if (changed) {
            UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
        }
    }
}

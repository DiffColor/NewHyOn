package kr.co.turtlelab.andowsignage.data.update;

import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;

/**
 * 다운로드 도중 장시간 정체된 항목을 초기화하여 재시도하도록 만드는 간단한 워처.
 */
public final class PendingDownloadWatcher {

    private static final long STALE_THRESHOLD_TICKS = 2L * 60L * 10_000_000L; // 2분

    private PendingDownloadWatcher() { }

    public static void resetStaleDownloads(RealmUpdateQueue queue) {
        if (queue == null) {
            return;
        }
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        journal.ensureDefaults();
        long nowTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
        boolean changed = false;
        for (UpdateQueueContract.DownloadEntry entry : journal.getEntries()) {
            if (entry == null) {
                continue;
            }
            if (entry.Chunks == null) {
                continue;
            }
            for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
                if (chunk == null) {
                    continue;
                }
                if (UpdateQueueContract.ChunkStatus.DOWNLOADING.equals(chunk.Status)
                        && chunk.LastUpdatedTicks > 0
                        && (nowTicks - chunk.LastUpdatedTicks) > STALE_THRESHOLD_TICKS) {
                    chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
                    chunk.DownloadedBytes = 0L;
                    chunk.LastUpdatedTicks = nowTicks;
                    changed = true;
                }
            }
        }
        if (changed) {
            UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
        }
    }
}

package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import com.google.gson.Gson;

import java.io.File;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

import io.realm.Realm;
import io.realm.RealmResults;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

/**
 * UpdateQueue Realm 모델을 다루는 헬퍼.
 * 향후 UpdateQueueProcessor 가 동일한 패턴으로 재사용할 수 있도록
 * 기본적인 enqueue/update/delete 유틸을 모아둔다.
 */
public final class UpdateQueueHelper {

    private static final Gson GSON = new Gson();
    private static final ExecutorService STATUS_EXECUTOR = Executors.newSingleThreadExecutor();

    private UpdateQueueHelper() { }

    private static String resolveOwnerPlayerIdFromPayload(String payloadJson) {
        try {
            UpdateQueueContract.PlaylistPayload payload = GSON.fromJson(
                    payloadJson, UpdateQueueContract.PlaylistPayload.class);
            if (payload != null && !TextUtils.isEmpty(payload.playerId)) {
                return payload.playerId;
            }
        } catch (Exception ignore) {
        }
        String owner = RethinkDbClient.getInstance().getStoredPlayerGuid();
        if (TextUtils.isEmpty(owner)) {
            owner = RethinkDbClient.getInstance().ensurePlayerGuid();
        }
        return owner;
    }
    public static RealmUpdateQueue enqueue(String type,
                                           String payloadJson,
                                           String downloadContentsJson,
                                           long ttlMillis) {
        Realm realm = Realm.getDefaultInstance();
        try {
            long now = System.currentTimeMillis();
            long expires = ttlMillis > 0 ? now + ttlMillis : 0;
            AtomicReference<RealmUpdateQueue> ref = new AtomicReference<>();
            realm.executeTransaction(r -> {
                Number maxId = r.where(RealmUpdateQueue.class).max("id");
                long nextId = maxId == null ? 1 : maxId.longValue() + 1;
                String owner = resolveOwnerPlayerIdFromPayload(payloadJson);
                long ticks = toDotNetLocalTicks(now);
                String externalId = TextUtils.isEmpty(owner) ? String.valueOf(nextId) : owner + ":" + ticks;
                RealmUpdateQueue queue = r.createObject(RealmUpdateQueue.class, nextId);
                queue.setType(type);
                queue.setPayloadJson(payloadJson);
                queue.setDownloadContentsJson(downloadContentsJson);
                queue.setStatus(UpdateQueueContract.Status.QUEUED);
                queue.setProgress(0f);
                queue.setDownloadProgress(0f);
                queue.setValidateProgress(0f);
                queue.setCreatedAt(now);
                queue.setUpdatedAt(now);
                queue.setExpiresAt(expires);
                queue.setExternalId(externalId);
                ref.set(r.copyFromRealm(queue));
                UpdateQueueLogger.log("Queue #" + nextId + " enqueued type=" + type);
            });
            return ref.get();
        } finally {
            realm.close();
        }
    }

    public static void updateStatus(long queueId, String status) {
        updateStatus(queueId, status, null, null);
    }

    public static void updateStatus(long queueId,
                                    String status,
                                    String errorCode,
                                    String errorMessage) {
        Realm realm = Realm.getDefaultInstance();
        try {
            long now = System.currentTimeMillis();
            AtomicReference<StatusSnapshot> statusSnapshot = new AtomicReference<>();
            AtomicBoolean deleteRemoteRecord = new AtomicBoolean(false);
            AtomicReference<String> playerRef = new AtomicReference<>("");
            AtomicBoolean removeLocal = new AtomicBoolean(false);
            realm.executeTransaction(r -> {
                RealmUpdateQueue queue = r.where(RealmUpdateQueue.class)
                        .equalTo("id", queueId)
                        .findFirst();
                if (queue == null) {
                    return;
                }
                String playerId = getPlayerId(queue);
                playerRef.set(playerId);
                String oldStatus = queue.getStatus();
                queue.setStatus(status);
                queue.setUpdatedAt(now);
                if (UpdateQueueContract.Status.READY.equals(status)) {
                    queue.setProgress(100f);
                    queue.setDownloadProgress(100f);
                    queue.setValidateProgress(100f);
                }
                if (errorCode != null) {
                    queue.setErrorCode(errorCode);
                }
                if (errorMessage != null) {
                    queue.setErrorMessage(errorMessage);
                }
                if (UpdateQueueContract.Status.DONE.equals(status)
                        || UpdateQueueContract.Status.CANCELLED.equals(status)) {
                    queue.setExpiresAt(0);
                    removeLocal.set(true);
                } else if (UpdateQueueContract.Status.FAILED.equals(status)) {
                    queue.setExpiresAt(0);
                }
                if (!TextUtils.equals(oldStatus, status)) {
                    UpdateQueueLogger.log("Queue #" + queueId
                            + " status " + String.valueOf(oldStatus)
                            + " -> " + String.valueOf(status));
                }
                if (UpdateQueueContract.Status.FAILED.equals(status)) {
                    UpdateQueueLogger.log("Queue #" + queueId
                            + " failed: " + String.valueOf(errorCode)
                            + " / " + String.valueOf(errorMessage));
                    deleteRemoteRecord.set(true);
                    RethinkDbClient.getInstance().updateCommandHistoryByQueue(queue.getExternalId(),
                            UpdateQueueContract.Status.FAILED.toLowerCase(),
                            errorCode,
                            errorMessage,
                            playerRef.get(),
                            toDotNetLocalTicks(queue.getCreatedAt()));
                } else if (UpdateQueueContract.Status.DONE.equals(status)) {
                    UpdateQueueLogger.log("Queue #" + queueId + " completed.");
                    deleteRemoteRecord.set(true);
                    RethinkDbClient.getInstance().updateCommandHistoryByQueue(queue.getExternalId(),
                            UpdateQueueContract.Status.DONE.toLowerCase(),
                            null,
                            null,
                            playerRef.get(),
                            toDotNetLocalTicks(queue.getCreatedAt()));
                } else if (UpdateQueueContract.Status.CANCELLED.equals(status)) {
                    deleteRemoteRecord.set(true);
                    RethinkDbClient.getInstance().updateCommandHistoryByQueue(queue.getExternalId(),
                            UpdateQueueContract.Status.CANCELLED.toLowerCase(),
                            errorCode,
                            errorMessage,
                            playerRef.get(),
                            toDotNetLocalTicks(queue.getCreatedAt()));
                }
                statusSnapshot.set(buildSnapshot(queue, playerId));
                if (removeLocal.get()) {
                    queue.deleteFromRealm();
                }
            });
            if (deleteRemoteRecord.get()) {
                deleteQueueRecordAsync(queueId,
                        statusSnapshot.get() == null ? "" : statusSnapshot.get().externalId,
                        playerRef.get());
            }
            sendStatusAsync(statusSnapshot.get());
        } finally {
            realm.close();
        }
    }

    public static void updateProgress(long queueId, float weightProgress) {
        updateProgress(queueId, weightProgress, weightProgress, weightProgress);
    }

    public static void updateProgress(long queueId,
                                      float downloadPercent,
                                      float validatePercent,
                                      float overallPercent) {
        Realm realm = Realm.getDefaultInstance();
        try {
            long now = System.currentTimeMillis();
            realm.executeTransaction(r -> {
                RealmUpdateQueue queue = r.where(RealmUpdateQueue.class)
                        .equalTo("id", queueId)
                        .findFirst();
                if (queue == null) {
                    return;
                }
                float clampedOverall = Math.max(0f, Math.min(100f, overallPercent));
                float clampedDownload = Math.max(0f, Math.min(100f, downloadPercent));
                float clampedValidate = Math.max(0f, Math.min(100f, validatePercent));
                if (Math.abs(queue.getProgress() - clampedOverall) < UpdateQueueContract.ProgressWeight.EPSILON
                        && Math.abs(queue.getDownloadProgress() - clampedDownload) < UpdateQueueContract.ProgressWeight.EPSILON
                        && Math.abs(queue.getValidateProgress() - clampedValidate) < UpdateQueueContract.ProgressWeight.EPSILON) {
                    return;
                }
                queue.setProgress(clampedOverall);
                queue.setDownloadProgress(clampedDownload);
                queue.setValidateProgress(clampedValidate);
                queue.setUpdatedAt(now);
                sendStatus(queue);
            });
        } finally {
            realm.close();
        }
    }

    public static void updateDownloadJournal(long queueId, String downloadJson) {
        Realm realm = Realm.getDefaultInstance();
        try {
            realm.executeTransaction(r -> {
                RealmUpdateQueue queue = r.where(RealmUpdateQueue.class)
                        .equalTo("id", queueId)
                        .findFirst();
                if (queue == null) {
                    return;
                }
                queue.setDownloadContentsJson(downloadJson);
                queue.setUpdatedAt(System.currentTimeMillis());
                sendStatus(queue);
            });
        } finally {
            realm.close();
        }
    }

    /**
     * 만료 기간이 지난 큐 레코드를 정리한다.
     */
    public static void purgeExpired(long now) {
        Realm realm = Realm.getDefaultInstance();
        try {
            realm.executeTransaction(r -> {
                RealmResults<RealmUpdateQueue> results = r.where(RealmUpdateQueue.class)
                        .greaterThan("expiresAt", 0)
                        .lessThan("expiresAt", now)
                        .findAll();
                results.deleteAllFromRealm();
            });
        } finally {
            realm.close();
        }
    }

    public static void incrementRetry(long queueId, long nextRetryAt) {
        Realm realm = Realm.getDefaultInstance();
        try {
            realm.executeTransaction(r -> {
                RealmUpdateQueue queue = r.where(RealmUpdateQueue.class)
                        .equalTo("id", queueId)
                        .findFirst();
                if (queue == null) {
                    return;
                }
                int nextRetryCount = queue.getRetryCount() + 1;
                queue.setRetryCount(nextRetryCount);
                queue.setNextRetryAt(nextRetryAt);
                queue.setUpdatedAt(System.currentTimeMillis());
                UpdateQueueLogger.log("Queue #" + queueId + " retry scheduled (attempt "
                        + nextRetryCount + ") at " + nextRetryAt);
                if (nextRetryCount >= UpdateQueueContract.RetryPolicy.MAX_ATTEMPTS) {
                    UpdateQueueLogger.log("Queue #" + queueId
                            + " reached retry cap, awaiting manual intervention.");
                }
                sendStatus(queue);
            });
        } finally {
            realm.close();
        }
    }

    public static boolean hasPendingQueue() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = realm.where(RealmUpdateQueue.class)
                    .in("status", new String[]{
                            UpdateQueueContract.Status.QUEUED,
                            UpdateQueueContract.Status.DOWNLOADING,
                            UpdateQueueContract.Status.DOWNLOADED,
                            UpdateQueueContract.Status.VALIDATING})
                    .sort("id")
                    .findFirst();
            return queue != null;
        } finally {
            realm.close();
        }
    }

    public static boolean hasActiveQueue() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = realm.where(RealmUpdateQueue.class)
                    .notEqualTo("status", UpdateQueueContract.Status.DONE)
                    .notEqualTo("status", UpdateQueueContract.Status.FAILED)
                    .notEqualTo("status", UpdateQueueContract.Status.CANCELLED)
                    .sort("id")
                    .findFirst();
            return queue != null;
        } finally {
            realm.close();
        }
    }

    public static void recoverInterruptedQueues() {
        Realm realm = Realm.getDefaultInstance();
        try {
            long now = System.currentTimeMillis();
            realm.executeTransaction(r -> {
                RealmResults<RealmUpdateQueue> stuckDownloading = r.where(RealmUpdateQueue.class)
                        .equalTo("status", UpdateQueueContract.Status.DOWNLOADING)
                        .findAll();
                if (stuckDownloading != null) {
                    for (RealmUpdateQueue queue : stuckDownloading) {
                        queue.setStatus(UpdateQueueContract.Status.QUEUED);
                        queue.setErrorCode(null);
                        queue.setErrorMessage(null);
                        queue.setUpdatedAt(now);
                        queue.setNextRetryAt(0L);
                        sendStatus(queue);
                    }
                }

                RealmResults<RealmUpdateQueue> stuckApplying = r.where(RealmUpdateQueue.class)
                        .equalTo("status", UpdateQueueContract.Status.APPLYING)
                        .findAll();
                if (stuckApplying != null) {
                    for (RealmUpdateQueue queue : stuckApplying) {
                        queue.setStatus(UpdateQueueContract.Status.READY);
                        queue.setUpdatedAt(now);
                        queue.setNextRetryAt(0L);
                        sendStatus(queue);
                    }
                }
            });
        } finally {
            realm.close();
        }
    }

    public static void requeueFailedQueuesIfDue() {
        requeueFailedQueues(System.currentTimeMillis(), UpdateQueueContract.RetryPolicy.MAX_ATTEMPTS);
    }

    public static int cancelActiveQueues(String reason) {
        Realm realm = Realm.getDefaultInstance();
        String message = (reason == null || reason.trim().isEmpty())
                ? "Cancelled by manager"
                : reason.trim();
        try {
            AtomicInteger cancelled = new AtomicInteger();
            realm.executeTransaction(r -> {
                RealmResults<RealmUpdateQueue> results = r.where(RealmUpdateQueue.class)
                        .notEqualTo("status", UpdateQueueContract.Status.DONE)
                        .notEqualTo("status", UpdateQueueContract.Status.FAILED)
                        .findAll();
                long now = System.currentTimeMillis();
                for (RealmUpdateQueue queue : results) {
                    queue.setStatus(UpdateQueueContract.Status.CANCELLED);
                    queue.setErrorCode("CANCELLED");
                    queue.setErrorMessage(message);
                    queue.setUpdatedAt(now);
                    queue.setExpiresAt(0L);
                    queue.setRetryCount(0);
                    queue.setNextRetryAt(0L);
                    UpdateQueueLogger.log("Queue #" + queue.getId() + " cancelled");
                    cleanupTempFiles(queue);
                    sendStatus(queue);
                    String playerId = getPlayerId(queue);
                    RethinkDbClient.getInstance().deleteQueueRecord(String.valueOf(queue.getId()), playerId);
                    queue.deleteFromRealm();
                    cancelled.incrementAndGet();
                }
            });
            return cancelled.get();
        } finally {
            realm.close();
        }
    }

    public static int requeueFailedQueues(long now, int maxRetryCount) {
        Realm realm = Realm.getDefaultInstance();
        try {
            AtomicInteger requeued = new AtomicInteger();
            realm.executeTransaction(r -> {
                RealmResults<RealmUpdateQueue> results = r.where(RealmUpdateQueue.class)
                        .equalTo("status", UpdateQueueContract.Status.FAILED)
                        .greaterThan("nextRetryAt", 0L)
                        .lessThanOrEqualTo("nextRetryAt", now)
                        .lessThan("retryCount", maxRetryCount)
                        .findAll();
                for (RealmUpdateQueue queue : results) {
                    queue.setStatus(UpdateQueueContract.Status.QUEUED);
                    queue.setErrorCode(null);
                    queue.setErrorMessage(null);
                    queue.setNextRetryAt(0L);
                    queue.setUpdatedAt(now);
                    queue.setExpiresAt(0L);
                    requeued.incrementAndGet();
                    UpdateQueueLogger.log("Queue #" + queue.getId() + " re-queued after failure");
                    sendStatus(queue);
                }
            });
            return requeued.get();
        } finally {
            realm.close();
        }
    }

    private static void sendStatus(RealmUpdateQueue queue) {
        sendStatus(queue, getPlayerId(queue));
    }

    private static void sendStatus(RealmUpdateQueue queue, String playerId) {
        sendStatusAsync(buildSnapshot(queue, playerId));
    }

    private static void sendStatusAsync(StatusSnapshot snapshot) {
        if (snapshot == null) {
            return;
        }
        STATUS_EXECUTOR.execute(() -> {
            try {
                RethinkDbClient.getInstance()
                        .sendProgress(snapshot.externalId,
                                snapshot.progress,
                                snapshot.status,
                                snapshot.retryCount,
                                snapshot.nextRetryAt,
                                snapshot.errorCode,
                                snapshot.errorMessage,
                                snapshot.downloadContentsJson,
                                snapshot.playerId,
                                snapshot.downloadProgress,
                                snapshot.validateProgress,
                                snapshot.playerName,
                                snapshot.playlistId,
                                snapshot.playlistName,
                                snapshot.payloadJson,
                                snapshot.createdAt);
            } catch (Exception ignore) {
            }
        });
    }

    private static void deleteQueueRecordAsync(long queueId, String externalId, String playerId) {
        STATUS_EXECUTOR.execute(() -> {
            try {
                String idToUse = TextUtils.isEmpty(externalId) ? String.valueOf(queueId) : externalId;
                RethinkDbClient.getInstance().deleteQueueRecord(idToUse, playerId);
            } catch (Exception ignore) {
            }
        });
    }

    private static StatusSnapshot buildSnapshot(RealmUpdateQueue queue, String playerId) {
        if (queue == null) {
            return null;
        }
        UpdateQueueContract.PlaylistPayload payload = null;
        String playerName = "";
        String playlistId = "";
        String playlistName = "";
        try {
            payload = GSON.fromJson(queue.getPayloadJson(), UpdateQueueContract.PlaylistPayload.class);
            if (payload != null) {
                playerName = payload.playerName == null ? "" : payload.playerName;
                playlistId = payload.playlistId == null ? "" : payload.playlistId;
                playlistName = payload.playlistName == null ? "" : payload.playlistName;
                if (TextUtils.isEmpty(playerId) && !TextUtils.isEmpty(payload.playerId)) {
                    playerId = payload.playerId;
                }
            }
        } catch (Exception ignore) {
        }
        long createdTicks = queue.getCreatedAt() <= 0
                ? 0L
                : toDotNetLocalTicks(queue.getCreatedAt());
        String externalId = queue.getExternalId();
        if (TextUtils.isEmpty(externalId)) {
            String owner = TextUtils.isEmpty(playerId) ? resolveOwnerPlayerIdFromPayload(queue.getPayloadJson()) : playerId;
            if (!TextUtils.isEmpty(owner) && createdTicks > 0) {
                externalId = owner + ":" + createdTicks;
            } else {
                externalId = String.valueOf(queue.getId());
            }
        }

        return new StatusSnapshot(queue.getId(),
                externalId,
                queue.getProgress(),
                queue.getDownloadProgress(),
                queue.getValidateProgress(),
                queue.getStatus(),
                queue.getRetryCount(),
                queue.getNextRetryAt(),
                queue.getErrorCode(),
                queue.getErrorMessage(),
                queue.getDownloadContentsJson(),
                TextUtils.isEmpty(playerId) ? "" : playerId,
                playerName,
                playlistId,
                playlistName,
                queue.getPayloadJson(),
                createdTicks);
    }

    public static String getPlayerId(RealmUpdateQueue queue) {
        if (queue == null) {
            return "";
        }
        try {
            if (UpdateQueueContract.Type.PLAYLIST.equals(queue.getType())) {
                UpdateQueueContract.PlaylistPayload payload = GSON.fromJson(
                        queue.getPayloadJson(), UpdateQueueContract.PlaylistPayload.class);
                if (payload != null && !TextUtils.isEmpty(payload.playerId)) {
                    return payload.playerId;
                }
            }
        } catch (Exception ignore) {
        }
        return "";
    }

    private static void cleanupTempFiles(RealmUpdateQueue queue) {
        if (queue == null) {
            return;
        }
        try {
            ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
            for (UpdateQueueContract.DownloadContentEntry entry : journal.getEntries()) {
                if (entry == null) {
                    continue;
                }
                String tempPath = LocalPathUtils.getTempPath(entry.remotePath);
                File file = new File(tempPath);
                if (file.exists()) {
                    file.delete();
                }
            }
        } catch (Exception ignore) {
        }
    }

    /**
     * DateTime.Now.Ticks(로컬)과 동일한 기준으로 epoch ms를 변환.
     */
    public static long toDotNetLocalTicks(long epochMillis) {
        long offset = java.util.TimeZone.getDefault().getOffset(epochMillis);
        long localMillis = epochMillis + offset;
        return (localMillis * 10_000L) + 621355968000000000L;
    }

    private static final class StatusSnapshot {
        final long queueId;
        final String externalId;
        final float progress;
        final float downloadProgress;
        final float validateProgress;
        final String status;
        final int retryCount;
        final long nextRetryAt;
        final String errorCode;
        final String errorMessage;
        final String downloadContentsJson;
        final String playerId;
        final String playerName;
        final String playlistId;
        final String playlistName;
        final String payloadJson;
        final long createdAt;

        StatusSnapshot(long queueId,
                       String externalId,
                       float progress,
                       float downloadProgress,
                       float validateProgress,
                       String status,
                       int retryCount,
                       long nextRetryAt,
                       String errorCode,
                       String errorMessage,
                       String downloadContentsJson,
                       String playerId,
                       String playerName,
                       String playlistId,
                       String playlistName,
                       String payloadJson,
                       long createdAt) {
            this.queueId = queueId;
            this.externalId = externalId;
            this.progress = progress;
            this.downloadProgress = downloadProgress;
            this.validateProgress = validateProgress;
            this.status = status;
            this.retryCount = retryCount;
            this.nextRetryAt = nextRetryAt;
            this.errorCode = errorCode;
            this.errorMessage = errorMessage;
            this.downloadContentsJson = downloadContentsJson;
            this.playerId = playerId;
            this.playerName = playerName;
            this.playlistId = playlistId;
            this.playlistName = playlistName;
            this.payloadJson = payloadJson;
            this.createdAt = createdAt;
        }
    }


}

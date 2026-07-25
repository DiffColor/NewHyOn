package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import java.util.List;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxResults;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.store.StoredUpdateQueue;
import kr.co.turtlelab.andowsignage.data.update.UpdateThrottleModels.UpdateLeaseEntry;
import kr.co.turtlelab.andowsignage.data.update.UpdateThrottleModels.UpdateThrottleSettings;
import kr.co.turtlelab.andowsignage.data.update.UpdateLeaseClient;
import kr.co.turtlelab.andowsignage.data.update.UpdateThrottleSettingsClient;

/**
 * UpdateQueue 상태 머신을 순차적으로 실행하는 기본 Processor.
 * 현재는 구조체/통신 정도만 정의하고, 실제 다운로드/검증/적용 로직은 이후 단계에서 채운다.
 */
public class UpdateQueueProcessor implements UpdateQueueDownloader.LeaseHandler {

    public interface QueueApplier {
        boolean apply(StoredUpdateQueue queue);
    }

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final QueueApplier queueApplier;
    private final UpdateQueueDownloader downloader;
    private final UpdateQueueValidator validator;
    private final AtomicBoolean running = new AtomicBoolean(false);
    private final UpdateLeaseClient leaseClient;
    private final UpdateThrottleSettingsClient throttleSettingsClient;
    private UpdateThrottleModels.UpdateLeaseEntry activeLease;
    private long nextLeaseRenewAt;
    private final Object leaseLock = new Object();
    private String managerHost = AndoWSignageApp.MANAGER_IP;

    public UpdateQueueProcessor(QueueApplier queueApplier) {
        this(queueApplier, new UpdateQueueDownloader(), new UpdateQueueValidator());
    }

    public UpdateQueueProcessor(QueueApplier queueApplier,
                                UpdateQueueDownloader downloader) {
        this(queueApplier, downloader, new UpdateQueueValidator());
    }

    public UpdateQueueProcessor(QueueApplier queueApplier,
                                UpdateQueueDownloader downloader,
                                UpdateQueueValidator validator) {
        this.queueApplier = queueApplier;
        this.validator = validator;
        this.leaseClient = new UpdateLeaseClient(managerHost);
        this.throttleSettingsClient = new UpdateThrottleSettingsClient(managerHost);
        this.downloader = downloader == null ? new UpdateQueueDownloader() : downloader;
        this.downloader.setLeaseHandler(this);
    }

    public void updateHost(String host) {
        if (TextUtils.isEmpty(host)) {
            return;
        }
        managerHost = host;
        leaseClient.updateHost(host);
        throttleSettingsClient.updateHost(host);
    }

    public void schedule() {
        UpdateQueueHelper.recoverInterruptedQueues();
        UpdateQueueHelper.requeueFailedQueuesIfDue();
        if (!running.compareAndSet(false, true)) {
            return;
        }
        executor.execute(() -> {
            try {
                processNext();
            } finally {
                running.set(false);
                if (UpdateQueueHelper.hasPendingQueue()) {
                    schedule();
                }
            }
        });
    }

    public boolean processImmediate(long queueId, boolean ignoreLease) {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredUpdateQueue queue = storeDb.where(StoredUpdateQueue.class)
                    .equalTo("id", queueId)
                    .findFirst();
            if (queue == null) {
                return false;
            }
            StoredUpdateQueue snapshot = storeDb.copyEntity(queue);
            processQueueInternal(snapshot, ignoreLease);
            return true;
        } finally {
            storeDb.close();
        }
    }

    public void releaseLeaseIfAny() {
        releaseLease();
    }

    public void releasePlayerLeaseByPlayer(String playerId) {
        if (TextUtils.isEmpty(playerId)) {
            return;
        }
        try {
            leaseClient.releaseByPlayer(playerId);
        } catch (Exception ignore) {
        }
    }

    @Override
    public UpdateThrottleSettings getSettings() {
        try {
            return throttleSettingsClient == null ? new UpdateThrottleSettings() : throttleSettingsClient.getSettings();
        } catch (Exception ignore) {
            return new UpdateThrottleSettings();
        }
    }

    @Override
    public boolean ensureLease(StoredUpdateQueue queue, UpdateThrottleSettings settings) {
        if (queue == null) {
            return false;
        }
        if (settings == null) {
            return true;
        }
        String queueKey = getQueueKey(queue);
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        synchronized (leaseLock) {
            try {
                leaseClient.releaseStaleByLastRenew(60);
            } catch (Exception ignore) {
            }
            if (activeLease != null && equalsIgnoreCase(activeLease.QueueId, queueKey)) {
                return tryRenewLeaseIfNeeded(settings);
            }
            releaseLeaseInternal();
            UpdateLeaseEntry lease = leaseClient.tryAcquire(playerId, queueKey, settings.MaxConcurrentDownloads, settings.LeaseTtlSeconds);
            if (lease == null && !hasLocalActiveDownloads(playerId)) {
                leaseClient.releaseByPlayer(playerId);
                lease = leaseClient.tryAcquire(playerId, queueKey, settings.MaxConcurrentDownloads, settings.LeaseTtlSeconds);
            }
            if (lease == null) {
                return false;
            }
            activeLease = lease;
            int renewSeconds = settings.LeaseRenewIntervalSeconds <= 0 ? 30 : settings.LeaseRenewIntervalSeconds;
            nextLeaseRenewAt = System.currentTimeMillis() + (renewSeconds * 1000L);
            return true;
        }
    }

    @Override
    public boolean tryRenewLeaseIfNeeded(UpdateThrottleSettings settings) {
        synchronized (leaseLock) {
            if (settings == null || activeLease == null) {
                return false;
            }
            if (System.currentTimeMillis() < nextLeaseRenewAt) {
                return true;
            }
            boolean renewed = leaseClient.renew(activeLease.Id, settings.LeaseTtlSeconds);
            if (!renewed) {
                releaseLeaseInternal();
                return false;
            }
            int renewSeconds = settings.LeaseRenewIntervalSeconds <= 0 ? 30 : settings.LeaseRenewIntervalSeconds;
            nextLeaseRenewAt = System.currentTimeMillis() + (renewSeconds * 1000L);
            return true;
        }
    }

    private void processNext() {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        StoredUpdateQueue queue;
        try {
            storeDb.beginTransaction();
            ObjectBoxResults<StoredUpdateQueue> pending = storeDb.where(StoredUpdateQueue.class)
                    .in("status", new String[]{
                            UpdateQueueContract.Status.QUEUED,
                            UpdateQueueContract.Status.DOWNLOADING,
                            UpdateQueueContract.Status.DOWNLOADED,
                            UpdateQueueContract.Status.VALIDATING,
                            UpdateQueueContract.Status.READY})
                    .sort("id")
                    .findAll();
            if (pending.isEmpty()) {
                storeDb.cancelTransaction();
                storeDb.close();
                return;
            }
            queue = storeDb.copyEntity(pending.first());
            storeDb.commitTransaction();
        } catch (Exception e) {
            if (storeDb.isInTransaction()) {
                storeDb.cancelTransaction();
            }
            storeDb.close();
            return;
        }
        storeDb.close();

        long now = System.currentTimeMillis();
        // 재시도 대기 중이면 다음 스케줄까지 대기
        if (queue.getNextRetryAt() > 0 && queue.getNextRetryAt() > now
                && (UpdateQueueContract.Status.QUEUED.equals(queue.getStatus())
                || UpdateQueueContract.Status.DOWNLOADED.equals(queue.getStatus())
                || UpdateQueueContract.Status.VALIDATING.equals(queue.getStatus())
                || UpdateQueueContract.Status.DOWNLOADING.equals(queue.getStatus()))) {
            return;
        }
        processQueueInternal(queue, false);
    }

    private void processQueueInternal(StoredUpdateQueue queue, boolean ignoreLease) {
        if (queue == null) {
            return;
        }
        switch (queue.getStatus()) {
            case UpdateQueueContract.Status.QUEUED:
                handleQueued(queue, ignoreLease);
                break;
            case UpdateQueueContract.Status.DOWNLOADING:
                handleDownloading(queue, ignoreLease);
                break;
            case UpdateQueueContract.Status.DOWNLOADED:
                handleDownloaded(queue);
                break;
            case UpdateQueueContract.Status.VALIDATING:
                handleValidating(queue);
                break;
            case UpdateQueueContract.Status.READY:
                handleReady(queue);
                break;
            default:
                break;
        }
    }

    private void handleQueued(StoredUpdateQueue queue, boolean ignoreLease) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DOWNLOADING);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        handleDownloading(queue, ignoreLease);
    }

    private void handleDownloading(StoredUpdateQueue queue, boolean ignoreLeaseFlag) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        tracker.stepDownload(0f);
        boolean ignoreLease = ignoreLeaseFlag;
        UpdateThrottleSettings settings = ignoreLease ? null : getSettings();
        if (!ignoreLease && hasDownloads(queue) && !ensureLease(queue, settings)) {
            scheduleLeaseRetry(queue, settings, "LEASE_BUSY");
            return;
        }
        UpdateQueueDownloader.DownloadOutcome outcome = new UpdateQueueDownloader.DownloadOutcome();
        if (downloader != null) {
            outcome = downloader.download(queue, tracker, ignoreLease);
        } else {
            outcome.success = true;
        }
        if (outcome.leaseBusy) {
            scheduleLeaseRetry(queue, settings, "LEASE_BUSY");
            return;
        }
        if (outcome.leaseLost) {
            scheduleLeaseRetry(queue, settings, "LEASE_LOST");
            return;
        }
        if (outcome.success) {
            tracker.stepDownload(1f);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DOWNLOADED);
            if (!ignoreLease) {
                releaseLeaseIfOwner(queue);
            }
            handleDownloaded(queue);
        } else {
            String errorMessage = TextUtils.isEmpty(outcome.lastError)
                    ? "Download failed"
                    : outcome.lastError;
            handleFailure(queue, errorMessage, "DOWNLOAD_RETRY_EXHAUSTED");
            if (!ignoreLease) {
                releaseLeaseIfOwner(queue);
            }
        }
    }

    private void handleDownloaded(StoredUpdateQueue queue) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.VALIDATING);
        tracker.stepValidate(0f);
        boolean valid = validator != null && validator.validate(queue, tracker);
        if (valid) {
            tracker.stepValidate(1f);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.READY);
            if (queueApplier != null) {
                queueApplier.apply(queue);
            }
        } else {
            String errorMessage = (validator == null || TextUtils.isEmpty(validator.getLastError()))
                    ? "File validation failed"
                    : validator.getLastError();
            handleFailure(queue, errorMessage, "VALIDATION_RETRY_EXHAUSTED");
        }
    }

    private void handleFailure(StoredUpdateQueue queue, String errorMessage, String exhaustedErrorCode) {
        int attemptNumber = queue.getRetryCount() + 1;
        boolean retry = UpdateFailurePolicy.shouldRetry(errorMessage, attemptNumber);
        long nextRetryAt = retry
                ? System.currentTimeMillis() + UpdateQueueContract.RetryPolicy.getDelayMs(attemptNumber)
                : 0L;
        UpdateQueueHelper.incrementRetry(queue.getId(), nextRetryAt);
        if (retry) {
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.QUEUED,
                    "TRANSIENT_UPDATE_ERROR", errorMessage);
            return;
        }
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.FAILED,
                UpdateFailurePolicy.getFinalErrorCode(errorMessage, attemptNumber, exhaustedErrorCode),
                errorMessage);
    }

    private void handleValidating(StoredUpdateQueue queue) {
        handleDownloaded(queue);
    }

    private void handleReady(StoredUpdateQueue queue) {
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.READY);
    }

    private void scheduleLeaseRetry(StoredUpdateQueue queue, UpdateThrottleSettings settings, String reason) {
        if (queue == null) {
            return;
        }
        int attemptNumber = queue.getRetryCount() + 1;
        if (attemptNumber >= UpdateFailurePolicy.MAX_ATTEMPTS) {
            if (queue.getRetryCount() < UpdateFailurePolicy.MAX_ATTEMPTS) {
                UpdateQueueHelper.incrementRetry(queue.getId(), 0L);
            }
            UpdateQueueHelper.updateStatus(queue.getId(),
                    UpdateQueueContract.Status.FAILED,
                    "LEASE_RETRY_EXHAUSTED",
                    reason);
            return;
        }
        int retrySeconds = settings == null ? 60 : settings.RetryIntervalSeconds;
        if (retrySeconds <= 0) {
            retrySeconds = 60;
        }
        long nextRetryAt = System.currentTimeMillis() + (retrySeconds * 1000L);
        UpdateQueueHelper.scheduleLeaseRetry(queue.getId(), nextRetryAt, "LEASE", reason);
    }

    private boolean hasDownloads(StoredUpdateQueue queue) {
        if (queue == null) {
            return false;
        }
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        return journal != null && journal.getEntries() != null && !journal.getEntries().isEmpty();
    }

    private String getQueueKey(StoredUpdateQueue queue) {
        if (queue == null) {
            return "";
        }
        String externalId = queue.getExternalId();
        if (!TextUtils.isEmpty(externalId)) {
            return externalId;
        }
        return String.valueOf(queue.getId());
    }

    private boolean hasLocalActiveDownloads(String playerId) {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            List<StoredUpdateQueue> list = storeDb.where(StoredUpdateQueue.class)
                    .in("status", new String[]{
                            UpdateQueueContract.Status.DOWNLOADING,
                            UpdateQueueContract.Status.DOWNLOADED,
                            UpdateQueueContract.Status.VALIDATING,
                            UpdateQueueContract.Status.APPLYING
                    })
                    .findAll();
            if (list == null || list.isEmpty()) {
                return false;
            }
            if (TextUtils.isEmpty(playerId)) {
                return true;
            }
            for (StoredUpdateQueue q : list) {
                String owner = UpdateQueueHelper.getPlayerId(q);
                if (!TextUtils.isEmpty(owner) && owner.equalsIgnoreCase(playerId)) {
                    return true;
                }
            }
            return false;
        } finally {
            storeDb.close();
        }
    }

    private void releaseLease() {
        synchronized (leaseLock) {
            releaseLeaseInternal();
        }
    }

    private void releaseLeaseInternal() {
        if (activeLease == null) {
            return;
        }
        try {
            leaseClient.release(activeLease.Id);
        } catch (Exception ignore) {
        }
        activeLease = null;
        nextLeaseRenewAt = 0L;
    }

    private void releaseLeaseIfOwner(StoredUpdateQueue queue) {
        if (queue == null) {
            return;
        }
        String queueKey = getQueueKey(queue);
        synchronized (leaseLock) {
            if (activeLease != null && equalsIgnoreCase(activeLease.QueueId, queueKey)) {
                releaseLeaseInternal();
            }
        }
    }

    private boolean equalsIgnoreCase(String a, String b) {
        if (a == null && b == null) {
            return true;
        }
        if (a == null || b == null) {
            return false;
        }
        return a.equalsIgnoreCase(b);
    }
}

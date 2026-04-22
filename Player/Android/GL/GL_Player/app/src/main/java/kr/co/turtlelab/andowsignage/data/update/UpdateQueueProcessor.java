package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

import io.realm.Realm;
import io.realm.RealmResults;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
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
        boolean apply(RealmUpdateQueue queue);
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
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = realm.where(RealmUpdateQueue.class)
                    .equalTo("id", queueId)
                    .findFirst();
            if (queue == null) {
                return false;
            }
            RealmUpdateQueue snapshot = realm.copyFromRealm(queue);
            processQueueInternal(snapshot, ignoreLease);
            return true;
        } finally {
            realm.close();
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
    public boolean ensureLease(RealmUpdateQueue queue, UpdateThrottleSettings settings) {
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
        Realm realm = Realm.getDefaultInstance();
        RealmUpdateQueue queue;
        try {
            realm.beginTransaction();
            RealmResults<RealmUpdateQueue> pending = realm.where(RealmUpdateQueue.class)
                    .in("status", new String[]{
                            UpdateQueueContract.Status.QUEUED,
                            UpdateQueueContract.Status.DOWNLOADING,
                            UpdateQueueContract.Status.DOWNLOADED,
                            UpdateQueueContract.Status.VALIDATING,
                            UpdateQueueContract.Status.READY})
                    .sort("id")
                    .findAll();
            if (pending.isEmpty()) {
                realm.cancelTransaction();
                realm.close();
                return;
            }
            queue = realm.copyFromRealm(pending.first());
            realm.commitTransaction();
        } catch (Exception e) {
            if (realm.isInTransaction()) {
                realm.cancelTransaction();
            }
            realm.close();
            return;
        }
        realm.close();

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

    private void processQueueInternal(RealmUpdateQueue queue, boolean ignoreLease) {
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

    private void handleQueued(RealmUpdateQueue queue, boolean ignoreLease) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DOWNLOADING);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        handleDownloading(queue, ignoreLease);
    }

    private void handleDownloading(RealmUpdateQueue queue, boolean ignoreLeaseFlag) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        tracker.stepDownload(0f);
        boolean ignoreLease = ignoreLeaseFlag || shouldIgnoreLease(queue);
        UpdateThrottleSettings settings = ignoreLease ? null : getSettings();
        if (!hasDownloads(queue)) {
            tracker.stepDownload(1f);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DOWNLOADED);
            handleDownloaded(queue);
            return;
        }
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
            // 다운로드 실패 시 Windows와 동일하게 재시도 가능 상태로 되돌린다.
            long delay = UpdateQueueContract.RetryPolicy.getDelayMs(queue.getRetryCount() + 1);
            UpdateQueueHelper.incrementRetry(queue.getId(), System.currentTimeMillis() + delay);
            String errorMessage = TextUtils.isEmpty(outcome.lastError)
                    ? "Download failed"
                    : outcome.lastError;
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.QUEUED,
                    "DOWNLOAD", errorMessage);
            if (!ignoreLease) {
                releaseLeaseIfOwner(queue);
            }
        }
    }

    private void handleDownloaded(RealmUpdateQueue queue) {
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
            long delay = UpdateQueueContract.RetryPolicy.getDelayMs(queue.getRetryCount() + 1);
            UpdateQueueHelper.incrementRetry(queue.getId(), System.currentTimeMillis() + delay);
            // 검증 실패도 재시도 대상이므로 QUEUED로 복귀시킨다.
            // Windows와 동일하게 현재 진행률은 유지하고 재시도 시점만 갱신한다.
            String errorMessage = (validator == null || TextUtils.isEmpty(validator.getLastError()))
                    ? "File validation failed"
                    : validator.getLastError();
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.QUEUED,
                    "VALIDATE", errorMessage);
        }
    }

    private void handleValidating(RealmUpdateQueue queue) {
        handleDownloaded(queue);
    }

    private void handleReady(RealmUpdateQueue queue) {
        if (queueApplier != null) {
            queueApplier.apply(queue);
        } else {
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.READY);
        }
    }

    private void scheduleLeaseRetry(RealmUpdateQueue queue, UpdateThrottleSettings settings, String reason) {
        if (queue == null) {
            return;
        }
        int retrySeconds = settings == null ? 60 : settings.RetryIntervalSeconds;
        if (retrySeconds <= 0) {
            retrySeconds = 60;
        }
        long nextRetryAt = System.currentTimeMillis() + (retrySeconds * 1000L);
        UpdateQueueHelper.scheduleLeaseRetry(queue.getId(), nextRetryAt, "LEASE", reason);
    }

    private boolean shouldIgnoreLease(RealmUpdateQueue queue) {
        if (queue == null) {
            return false;
        }
        if (queue.getRetryCount() < 3) {
            return false;
        }
        String error = queue.getErrorMessage();
        return !TextUtils.isEmpty(error) && error.toUpperCase(Locale.US).startsWith("LEASE");
    }

    private boolean hasDownloads(RealmUpdateQueue queue) {
        if (queue == null) {
            return false;
        }
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        return journal != null && journal.getEntries() != null && !journal.getEntries().isEmpty();
    }

    private String getQueueKey(RealmUpdateQueue queue) {
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
        Realm realm = Realm.getDefaultInstance();
        try {
            List<RealmUpdateQueue> list = realm.where(RealmUpdateQueue.class)
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
            for (RealmUpdateQueue q : list) {
                String owner = UpdateQueueHelper.getPlayerId(q);
                if (!TextUtils.isEmpty(owner) && owner.equalsIgnoreCase(playerId)) {
                    return true;
                }
            }
            return false;
        } finally {
            realm.close();
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

    private void releaseLeaseIfOwner(RealmUpdateQueue queue) {
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

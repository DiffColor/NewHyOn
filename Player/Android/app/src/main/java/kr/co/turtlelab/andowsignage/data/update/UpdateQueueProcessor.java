package kr.co.turtlelab.andowsignage.data.update;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

import io.realm.Realm;
import io.realm.RealmResults;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;

/**
 * UpdateQueue 상태 머신을 순차적으로 실행하는 기본 Processor.
 * 현재는 구조체/통신 정도만 정의하고, 실제 다운로드/검증/적용 로직은 이후 단계에서 채운다.
 */
public class UpdateQueueProcessor {

    public interface QueueApplier {
        boolean apply(RealmUpdateQueue queue);
    }

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final QueueApplier queueApplier;
    private final UpdateQueueDownloader downloader;
    private final UpdateQueueValidator validator;
    private final AtomicBoolean running = new AtomicBoolean(false);

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
        this.downloader = downloader;
        this.validator = validator;
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
        // 최대 재시도 횟수 초과 시 실패 처리
        if (queue.getRetryCount() >= UpdateQueueContract.RetryPolicy.MAX_ATTEMPTS
                && !UpdateQueueContract.Status.DONE.equals(queue.getStatus())
                && !UpdateQueueContract.Status.FAILED.equals(queue.getStatus())) {
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.FAILED,
                    "RETRY_LIMIT", "Reached max retry attempts");
            return;
        }

        switch (queue.getStatus()) {
            case UpdateQueueContract.Status.QUEUED:
                handleQueued(queue);
                break;
            case UpdateQueueContract.Status.DOWNLOADING:
                handleDownloading(queue);
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

    private void handleQueued(RealmUpdateQueue queue) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DOWNLOADING);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        handleDownloading(queue);
    }

    private void handleDownloading(RealmUpdateQueue queue) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        tracker.stepDownload(0f);
        UpdateQueueDownloader.DownloadOutcome outcome = new UpdateQueueDownloader.DownloadOutcome();
        if (downloader != null) {
            outcome = downloader.download(queue, tracker);
        } else {
            outcome.success = true;
        }
        if (outcome.missing) {
            return;
        }
        if (outcome.success) {
            tracker.stepDownload(1f);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DOWNLOADED);
            handleDownloaded(queue);
        } else {
            // 다운로드 실패 시 Windows와 동일하게 재시도 가능 상태로 되돌린다.
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.QUEUED,
                    "DOWNLOAD", "Failed to download contents");
        }
    }

    private void handleDownloaded(RealmUpdateQueue queue) {
        String playerId = UpdateQueueHelper.getPlayerId(queue);
        UpdateProgressTracker tracker = new UpdateProgressTracker(queue.getId(), playerId, queue.getExternalId());
        tracker.stepValidate(0f);
        boolean valid = validator != null && validator.validate(queue, tracker);
        if (valid) {
            tracker.stepValidate(1f);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.READY);
        } else {
            long delay = UpdateQueueContract.RetryPolicy.getDelayMs(queue.getRetryCount() + 1);
            UpdateQueueHelper.incrementRetry(queue.getId(), System.currentTimeMillis() + delay);
            // 검증 실패도 재시도 대상이므로 QUEUED로 복귀시킨다.
            // 다운로드 진행률을 초기화해 다시 다운로드/검증하도록 한다.
            ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
            journal.resetAllToPending();
            UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
            UpdateQueueHelper.updateProgress(queue.getId(), 0f, 0f, 0f);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.QUEUED,
                    "VALIDATE", "File validation failed");
        }
    }

    private void handleValidating(RealmUpdateQueue queue) {
        handleDownloaded(queue);
    }

    private void handleReady(RealmUpdateQueue queue) {
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.READY);
    }
}

package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import java.util.concurrent.atomic.AtomicReference;

import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.services.HeartbeatService;

/**
 * UpdateQueue 진행률을 일관되게 계산하고 서버에도 보고하는 유틸.
 * 다운로드 진행률을 기준으로 보고하고, 다운로드가 0일 때만 검증 진행률을 보조 값으로 사용한다.
 */
public class UpdateProgressTracker {

    private final long queueId;
    private final String externalQueueId;
    private final AtomicReference<Float> lastReported = new AtomicReference<>(0f);
    private final AtomicReference<String> lastReportedStatus = new AtomicReference<>("");
    private final String playerId;
    private final AtomicReference<Float> downloadProgress = new AtomicReference<>(0f);
    private final AtomicReference<Float> validateProgress = new AtomicReference<>(0f);

    public UpdateProgressTracker(long queueId) {
        this(queueId, null, null);
    }

    public UpdateProgressTracker(long queueId, String playerId) {
        this(queueId, playerId, null);
    }

    public UpdateProgressTracker(long queueId, String playerId, String externalQueueId) {
        this.queueId = queueId;
        this.playerId = TextUtils.isEmpty(playerId) ? "" : playerId;
        this.externalQueueId = TextUtils.isEmpty(externalQueueId) ? String.valueOf(queueId) : externalQueueId;
    }

    public float stepDownload(float unit) {
        float dl = Math.min(1f, Math.max(unit, 0f)) * 100f;
        downloadProgress.set(dl);
        float percent = setProgress(dl, validateProgress.get());
        maybeReport(percent, dl, validateProgress.get(), UpdateQueueContract.Status.DOWNLOADING);
        return percent;
    }

    public float stepValidate(float unit) {
        float vl = Math.min(1f, Math.max(unit, 0f)) * 100f;
        validateProgress.set(vl);
        float percent = setProgress(downloadProgress.get(), vl);
        maybeReport(percent, downloadProgress.get(), vl, UpdateQueueContract.Status.VALIDATING);
        return percent;
    }

    public float reportApplyProgress(float unit) {
        float applyUnit = Math.min(1f, Math.max(unit, 0f));
        float dl = Math.max(100f, downloadProgress.get());
        float vl = Math.max(100f, validateProgress.get());
        float percent = setProgress(dl, vl);
        percent = Math.max(percent, 100f * applyUnit);
        maybeReport(percent, dl, vl, UpdateQueueContract.Status.APPLYING);
        return percent;
    }

    public void reportReady() {
        float dl = Math.max(100f, downloadProgress.get());
        float vl = Math.max(100f, validateProgress.get());
        maybeReport(100f, dl, vl, UpdateQueueContract.Status.READY);
    }

    private float setProgress(float downloadPercent, float validatePercent) {
        float dl = Math.min(100f, Math.max(0f, downloadPercent));
        float vl = Math.min(100f, Math.max(0f, validatePercent));
        float overall = dl;
        if (overall <= 0f && vl > 0f) {
            overall = vl;
        }
        overall = Math.min(100f, Math.max(0f, overall));
        UpdateQueueHelper.updateProgress(queueId, dl, vl, overall);
        return overall;
    }

    private void maybeReport(float percent, float downloadPercent, float validatePercent, String status) {
        Float last = lastReported.get();
        String lastStatus = lastReportedStatus.get();
        if (Math.abs(percent - last) < UpdateQueueContract.ProgressWeight.EPSILON
                && ((status == null && lastStatus == null)
                || (status != null && status.equals(lastStatus)))) {
            return;
        }
        lastReported.set(percent);
        lastReportedStatus.set(status);
        RethinkDbClient.getInstance()
                .sendProgress(externalQueueId,
                        percent,
                        status,
                        0,
                        0,
                        null,
                        null,
                        null,
                        playerId,
                        downloadPercent,
                        validatePercent,
                        null,
                        null,
                        null,
                        null,
                        null);
        HeartbeatService.reportUpdateProgress(status, percent, false);
    }
}

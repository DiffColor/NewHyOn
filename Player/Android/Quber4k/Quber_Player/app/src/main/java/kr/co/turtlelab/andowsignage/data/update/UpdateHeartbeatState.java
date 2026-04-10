package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

public final class UpdateHeartbeatState {
    private static final long UPDATE_KEEPALIVE_MS = 5000L;
    private static final long UPDATE_REQUEST_MIN_INTERVAL_MS = 500L;
    private static final Object LOCK = new Object();
    private static final String SIGNALR_UPDATE_STATUS = "updating";

    private static boolean active = false;
    private static String lastStatus = SIGNALR_UPDATE_STATUS;
    private static int lastProgress = 0;
    private static long lastSentAtMs = 0L;
    private static long lastRequestedAtMs = 0L;
    private static long revision = 0L;

    private UpdateHeartbeatState() {
    }

    public static DispatchRequest reportProgress(String rawStatus, float rawProgress, boolean force) {
        synchronized (LOCK) {
            if (!isActiveUpdateStatus(rawStatus)) {
                return DispatchRequest.none();
            }

            int normalizedProgress = normalizeProgress(rawProgress);
            if (active && normalizedProgress < lastProgress) {
                normalizedProgress = lastProgress;
            }

            boolean wasActive = active;
            boolean changed = !wasActive || normalizedProgress != lastProgress;
            if (changed) {
                revision++;
            }

            active = true;
            lastStatus = SIGNALR_UPDATE_STATUS;
            lastProgress = normalizedProgress;

            long now = System.currentTimeMillis();
            if (force || changed || now - lastRequestedAtMs >= UPDATE_REQUEST_MIN_INTERVAL_MS) {
                lastRequestedAtMs = now;
                return DispatchRequest.send(revision);
            }

            return DispatchRequest.none();
        }
    }

    public static DispatchRequest reportQueueStatus(String rawStatus, float rawProgress, boolean isScheduleQueue) {
        String normalizedStatus = normalizeStatus(rawStatus);
        if (isActiveUpdateStatus(normalizedStatus)) {
            return reportProgress(normalizedStatus, rawProgress, false);
        }

        synchronized (LOCK) {
            if (active) {
                active = false;
                lastStatus = SIGNALR_UPDATE_STATUS;
                lastProgress = 0;
                lastSentAtMs = 0L;
                lastRequestedAtMs = 0L;
                revision++;
            }
        }

        return shouldSendNormalHeartbeatNow(normalizedStatus, isScheduleQueue)
                ? DispatchRequest.sendNormal()
                : DispatchRequest.none();
    }

    public static Snapshot captureForPublish(boolean forceUpdateReport, long expectedRevision) {
        synchronized (LOCK) {
            if (!active) {
                return Snapshot.none();
            }

            if (expectedRevision > 0L && revision != expectedRevision) {
                return Snapshot.cancel();
            }

            long now = System.currentTimeMillis();
            if (!forceUpdateReport && lastSentAtMs > 0L && now - lastSentAtMs < UPDATE_KEEPALIVE_MS) {
                return Snapshot.suppress();
            }

            long currentRevision = revision;
            String currentStatus = lastStatus;
            int currentProgress = lastProgress;
            lastSentAtMs = now;
            return Snapshot.active(currentRevision, currentStatus, currentProgress);
        }
    }

    public static boolean canSend(long expectedRevision) {
        synchronized (LOCK) {
            return active && revision == expectedRevision;
        }
    }

    public static void reset() {
        synchronized (LOCK) {
            active = false;
            lastStatus = SIGNALR_UPDATE_STATUS;
            lastProgress = 0;
            lastSentAtMs = 0L;
            lastRequestedAtMs = 0L;
            revision++;
        }
    }

    private static boolean shouldSendNormalHeartbeatNow(String normalizedStatus, boolean isScheduleQueue) {
        // 재생이 이미 복귀한 뒤에도 updating heartbeat가 유지되지 않도록
        // 완료 시점에는 일반 heartbeat를 즉시 한 번 더 보낸다.
        return isScheduleQueue
                || UpdateQueueContract.Status.DONE.equalsIgnoreCase(normalizedStatus);
    }

    private static boolean isActiveUpdateStatus(String rawStatus) {
        String normalizedStatus = normalizeStatus(rawStatus);
        return "queued".equals(normalizedStatus)
                || "downloading".equals(normalizedStatus)
                || "downloaded".equals(normalizedStatus)
                || "validating".equals(normalizedStatus)
                || "ready".equals(normalizedStatus)
                || "applying".equals(normalizedStatus)
                || SIGNALR_UPDATE_STATUS.equals(normalizedStatus);
    }

    private static String normalizeStatus(String rawStatus) {
        if (TextUtils.isEmpty(rawStatus)) {
            return "";
        }
        return rawStatus.trim().toLowerCase();
    }

    private static int normalizeProgress(float rawProgress) {
        return Math.max(0, Math.min(100, Math.round(rawProgress)));
    }

    public static final class DispatchRequest {
        public final boolean shouldSendUpdateNow;
        public final boolean shouldSendNormalNow;
        public final long revision;

        private DispatchRequest(boolean shouldSendUpdateNow, boolean shouldSendNormalNow, long revision) {
            this.shouldSendUpdateNow = shouldSendUpdateNow;
            this.shouldSendNormalNow = shouldSendNormalNow;
            this.revision = revision;
        }

        public static DispatchRequest send(long revision) {
            return new DispatchRequest(true, false, revision);
        }

        public static DispatchRequest sendNormal() {
            return new DispatchRequest(false, true, 0L);
        }

        public static DispatchRequest none() {
            return new DispatchRequest(false, false, 0L);
        }
    }

    public static final class Snapshot {
        public final boolean hasUpdatePayload;
        public final boolean suppressNormalHeartbeat;
        public final long revision;
        public final String status;
        public final int progress;

        private Snapshot(boolean hasUpdatePayload,
                         boolean suppressNormalHeartbeat,
                         long revision,
                         String status,
                         int progress) {
            this.hasUpdatePayload = hasUpdatePayload;
            this.suppressNormalHeartbeat = suppressNormalHeartbeat;
            this.revision = revision;
            this.status = status;
            this.progress = progress;
        }

        public static Snapshot active(long revision, String status, int progress) {
            return new Snapshot(true, false, revision, status, progress);
        }

        public static Snapshot suppress() {
            return new Snapshot(false, true, 0L, null, 0);
        }

        public static Snapshot cancel() {
            return new Snapshot(false, false, 0L, null, 0);
        }

        public static Snapshot none() {
            return new Snapshot(false, false, 0L, null, 0);
        }
    }
}

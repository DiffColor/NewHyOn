package kr.co.turtlelab.andowsignage.data.update;

/**
 * Computes the next wake-up from the FIFO head only. Later queues never bypass it.
 */
public final class UpdateQueueWakePolicy {
    public static final long NO_QUEUE = Long.MIN_VALUE;
    public static final long NO_WAKEUP = -1L;

    private UpdateQueueWakePolicy() { }

    public static long delayForHead(long headNextRetryAt, long now) {
        if (headNextRetryAt == NO_QUEUE) {
            return NO_WAKEUP;
        }
        if (headNextRetryAt <= 0L || headNextRetryAt <= now) {
            return 0L;
        }
        return Math.max(1L, headNextRetryAt - now);
    }
}

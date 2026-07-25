package kr.co.turtlelab.andowsignage.data.update;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class UpdateQueueWakePolicyTest {

    @Test
    public void emptyQueueDoesNotScheduleAnotherRun() {
        assertEquals(UpdateQueueWakePolicy.NO_WAKEUP,
                UpdateQueueWakePolicy.delayForHead(UpdateQueueWakePolicy.NO_QUEUE, 1_000L));
    }

    @Test
    public void dueHeadRunsImmediately() {
        assertEquals(0L, UpdateQueueWakePolicy.delayForHead(0L, 1_000L));
        assertEquals(0L, UpdateQueueWakePolicy.delayForHead(900L, 1_000L));
    }

    @Test
    public void futureRetryWaitsForTheHeadAndPreservesFifo() {
        assertEquals(500L, UpdateQueueWakePolicy.delayForHead(1_500L, 1_000L));
    }
}

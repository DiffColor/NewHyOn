package kr.co.turtlelab.andowsignage.data.update;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class UpdateFailurePolicyTest {

    @Test
    public void retriesTransientFailuresOnlyUntilThirdAttempt() {
        assertTrue(UpdateFailurePolicy.shouldRetry("FTP_SESSION_FAIL", 1));
        assertTrue(UpdateFailurePolicy.shouldRetry("connection timed out", 2));
        assertFalse(UpdateFailurePolicy.shouldRetry("connection timed out", 3));
    }

    @Test
    public void permanentContentErrorsFailWithoutRetry() {
        assertFalse(UpdateFailurePolicy.shouldRetry("poster.mp4: MISSING_FILE", 1));
        assertFalse(UpdateFailurePolicy.shouldRetry("REMOTE_PATH_EMPTY", 1));
        assertFalse(UpdateFailurePolicy.shouldRetry("LOCAL_FILENAME_EMPTY", 1));
    }

    @Test
    public void exposesActionableFinalErrorCodes() {
        assertEquals("MISSING_FILE", UpdateFailurePolicy.getFinalErrorCode("poster.mp4: MISSING_FILE", 1));
        assertEquals("DOWNLOAD_RETRY_EXHAUSTED", UpdateFailurePolicy.getFinalErrorCode("socket timeout", 3));
    }

    @Test
    public void invalidSchedulePayloadFailsImmediately() {
        assertFalse(UpdateFailurePolicy.shouldRetry("INVALID_SCHEDULE_PAYLOAD: player identifier is empty", 1));
        assertEquals(
                "INVALID_SCHEDULE_PAYLOAD",
                UpdateFailurePolicy.getFinalErrorCode(
                        "INVALID_SCHEDULE_PAYLOAD: player identifier is empty",
                        1,
                        "APPLY_RETRY_EXHAUSTED"));
    }

    @Test
    public void leaseFailureUsesTheSameThreeAttemptLimit() {
        assertTrue(UpdateFailurePolicy.shouldRetry("LEASE_BUSY", 1));
        assertTrue(UpdateFailurePolicy.shouldRetry("LEASE_BUSY", 2));
        assertFalse(UpdateFailurePolicy.shouldRetry("LEASE_BUSY", 3));
        assertEquals(
                "LEASE_RETRY_EXHAUSTED",
                UpdateFailurePolicy.getFinalErrorCode("LEASE_BUSY", 3, "LEASE_RETRY_EXHAUSTED"));
    }
}

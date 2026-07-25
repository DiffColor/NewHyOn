package kr.co.turtlelab.andowsignage.data.update;

import org.junit.Test;

import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicBoolean;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class UpdateExecutionControllerTest {

    @Test
    public void cancellationStopsTheCurrentExecutionAndItsTransfer() {
        UpdateExecutionController controller = new UpdateExecutionController();
        UpdateExecutionController.Token token = controller.begin(1L, "queue-1");

        controller.cancelActive();

        assertFalse(controller.isCurrent(token));
        assertTrue(token.isCancelled());
        assertTrue(token.getTransferStopFlag().get());
    }

    @Test
    public void secondExecutionCannotImplicitlySupersedeTheCurrentExecution() {
        UpdateExecutionController controller = new UpdateExecutionController();
        UpdateExecutionController.Token oldToken = controller.begin(1L, "queue-1");
        UpdateExecutionController.Token newToken = controller.begin(2L, "queue-2");

        assertTrue(controller.isCurrent(oldToken));
        assertFalse(oldToken.isCancelled());
        assertTrue(newToken == null);
    }

    @Test
    public void finalApplyRunsOnlyWhileTokenIsCurrent() {
        UpdateExecutionController controller = new UpdateExecutionController();
        UpdateExecutionController.Token oldToken = controller.begin(1L, "queue-1");
        controller.cancelActive();
        AtomicBoolean applied = new AtomicBoolean(false);

        boolean staleResult = controller.runIfCurrent(oldToken, () -> {
            applied.set(true);
            return true;
        });

        UpdateExecutionController.Token currentToken = controller.begin(2L, "queue-2");
        boolean currentResult = controller.runIfCurrent(currentToken, () -> {
            applied.set(true);
            return true;
        });

        assertFalse(staleResult);
        assertTrue(currentResult);
        assertTrue(applied.get());
    }

    @Test
    public void cancellationInvokesRegisteredTransferAbortExactlyOnce() {
        UpdateExecutionController controller = new UpdateExecutionController();
        UpdateExecutionController.Token token = controller.begin(1L, "queue-a");
        AtomicInteger abortCount = new AtomicInteger();
        token.setTransferAbortAction(abortCount::incrementAndGet);

        controller.cancelActive();
        token.requestTransferStop();

        assertEquals(1, abortCount.get());
        assertTrue(token.getTransferStopFlag().get());
    }

    @Test
    public void idleActionCannotRunWhileExecutionIsActive() {
        UpdateExecutionController controller = new UpdateExecutionController();
        UpdateExecutionController.Token token = controller.begin(1L, "queue-a");

        assertFalse(controller.runIfIdle(() -> true));

        controller.complete(token);
        assertTrue(controller.runIfIdle(() -> true));
    }
}

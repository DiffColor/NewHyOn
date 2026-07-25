package kr.co.turtlelab.andowsignage.data.update;

import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Coordinates cancellation and final application for a single player update stream.
 */
public final class UpdateExecutionController {

    public interface StartCondition {
        boolean canStart();
    }

    public interface CancellationAction {
        int run();
    }

    public interface CurrentAction {
        boolean run();
    }

    public interface CheckedCurrentAction {
        boolean run() throws Exception;
    }

    public static final class Token {
        private final UpdateExecutionController owner;
        private final long generation;
        private final long executionId;
        private final long queueId;
        private final String externalId;
        private final AtomicBoolean cancelled = new AtomicBoolean(false);
        private final AtomicBoolean transferStopFlag = new AtomicBoolean(false);
        private final AtomicReference<Runnable> transferAbortAction = new AtomicReference<>();

        private Token(UpdateExecutionController owner,
                      long generation,
                      long executionId,
                      long queueId,
                      String externalId) {
            this.owner = owner;
            this.generation = generation;
            this.executionId = executionId;
            this.queueId = queueId;
            this.externalId = externalId == null ? "" : externalId;
        }

        public long getGeneration() {
            return generation;
        }

        public long getExecutionId() {
            return executionId;
        }

        public long getQueueId() {
            return queueId;
        }

        public String getExternalId() {
            return externalId;
        }

        public boolean isCancelled() {
            return cancelled.get();
        }

        public AtomicBoolean getTransferStopFlag() {
            return transferStopFlag;
        }

        public void requestTransferStop() {
            transferStopFlag.set(true);
            Runnable action = transferAbortAction.getAndSet(null);
            if (action != null) {
                action.run();
            }
        }

        public void setTransferAbortAction(Runnable action) {
            transferAbortAction.set(action);
            if (transferStopFlag.get()
                    && action != null
                    && transferAbortAction.compareAndSet(action, null)) {
                action.run();
            }
        }

        public void clearTransferAbortAction(Runnable action) {
            if (action != null) {
                transferAbortAction.compareAndSet(action, null);
            }
        }

        public boolean runIfCurrent(CurrentAction action) {
            return owner != null && owner.runIfCurrent(this, action);
        }

        public boolean runIfCurrentChecked(CheckedCurrentAction action) throws Exception {
            return owner != null && owner.runIfCurrentChecked(this, action);
        }

        private Runnable cancelAndTakeTransferAbortAction() {
            cancelled.set(true);
            transferStopFlag.set(true);
            return transferAbortAction.getAndSet(null);
        }
    }

    private final Object finalizationLock = new Object();
    private final AtomicLong generation = new AtomicLong(0L);
    private final AtomicLong executionSequence = new AtomicLong(0L);
    private final AtomicReference<Token> active = new AtomicReference<>();

    public Token begin(long queueId, String externalId) {
        return beginIf(queueId, externalId, () -> true);
    }

    public Token beginIf(long queueId, String externalId, StartCondition condition) {
        synchronized (finalizationLock) {
            if (active.get() != null || condition == null || !condition.canStart()) {
                return null;
            }
            Token token = new Token(this,
                    generation.get(),
                    executionSequence.incrementAndGet(),
                    queueId,
                    externalId);
            active.set(token);
            return token;
        }
    }

    public void cancelActive() {
        cancelActiveAndRun(() -> 0);
    }

    public int cancelActiveAndRun(CancellationAction action) {
        Runnable transferAbort = null;
        int result;
        synchronized (finalizationLock) {
            generation.incrementAndGet();
            Token token = active.getAndSet(null);
            if (token != null) {
                transferAbort = token.cancelAndTakeTransferAbortAction();
            }
            result = action == null ? 0 : action.run();
        }
        runTransferAbort(transferAbort);
        return result;
    }

    public boolean isCurrent(Token token) {
        return token != null
                && !token.isCancelled()
                && token.generation == generation.get()
                && active.get() == token;
    }

    public boolean runIfCurrent(Token token, CurrentAction action) {
        if (action == null) {
            return false;
        }
        synchronized (finalizationLock) {
            if (!isCurrent(token)) {
                return false;
            }
            return action.run();
        }
    }

    public boolean runIfCurrentChecked(Token token, CheckedCurrentAction action) throws Exception {
        if (action == null) {
            return false;
        }
        synchronized (finalizationLock) {
            if (!isCurrent(token)) {
                return false;
            }
            return action.run();
        }
    }

    public boolean runIfIdle(CurrentAction action) {
        if (action == null) {
            return false;
        }
        synchronized (finalizationLock) {
            if (active.get() != null) {
                return false;
            }
            return action.run();
        }
    }

    public void complete(Token token) {
        if (token != null) {
            active.compareAndSet(token, null);
        }
    }

    private static void runTransferAbort(Runnable action) {
        if (action != null) {
            action.run();
        }
    }
}

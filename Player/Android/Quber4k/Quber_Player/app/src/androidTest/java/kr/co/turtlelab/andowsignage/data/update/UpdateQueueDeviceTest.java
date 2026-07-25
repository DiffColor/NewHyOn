package kr.co.turtlelab.andowsignage.data.update;

import android.content.Context;
import android.os.Bundle;
import android.os.SystemClock;
import android.support.test.InstrumentationRegistry;
import android.support.test.runner.AndroidJUnit4;

import com.google.gson.Gson;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.File;
import java.lang.reflect.Field;
import java.util.Collections;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredUpdateQueue;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

/**
 * 실장비 ObjectBox와 FTP transport를 사용해 update execution 취소 경계를 검증한다.
 */
@RunWith(AndroidJUnit4.class)
public class UpdateQueueDeviceTest {

    @Test
    public void testUnknownPayloadSizeDownloadsUsingRealFtpSize() throws Exception {
        assertFalse("기존 active queue가 있으면 장비 상태 보호를 위해 테스트를 중단한다.",
                UpdateQueueHelper.hasActiveQueue());

        Bundle arguments = InstrumentationRegistry.getArguments();
        String remotePath = arguments.getString("ftp_remote_path", "");
        long expectedRemoteSize = Long.parseLong(arguments.getString("ftp_expected_size", "0"));
        assertFalse("검증할 FTP remote path가 필요하다.", remotePath.isEmpty());
        assertTrue("검증할 FTP remote size가 필요하다.", expectedRemoteSize > 0L);

        String localName = "device-e2e-unknown-size-" + System.currentTimeMillis() + ".mp4";
        UpdateQueueContract.DownloadEntry entry = new UpdateQueueContract.DownloadEntry();
        entry.FileName = localName;
        entry.RemotePath = remotePath;
        entry.SizeBytes = 0L;
        entry.Checksum = "1399c3c4-3750-494f-8757-d58a7791ffe1";
        String downloadJson = new Gson().toJson(Collections.singletonList(entry));

        StoredUpdateQueue queue = enqueueTestQueue("device-e2e-unknown-size", downloadJson);
        assertNotNull(queue);
        UpdateExecutionController controller = getExecutionController();
        controller.cancelActive();
        UpdateExecutionController.Token token = controller.begin(queue.getId(), queue.getExternalId());
        assertNotNull(token);

        Field contextField = AndoWSignage.class.getDeclaredField("sCtx");
        contextField.setAccessible(true);
        Context previousContext = (Context) contextField.get(null);
        contextField.set(null, InstrumentationRegistry.getTargetContext());

        UpdateProgressTracker localOnlyTracker = new UpdateProgressTracker(
                queue.getId(), "", queue.getExternalId(), token) {
            @Override
            public float stepDownload(float unit) {
                return unit * 100f;
            }
        };
        try {
            UpdateQueueDownloader.DownloadOutcome outcome = new UpdateQueueDownloader().download(
                    queue,
                    localOnlyTracker,
                    true,
                    token);

            assertTrue("크기가 0인 payload도 FTP 원격 크기로 다운로드돼야 한다.", outcome.success);
            StoredUpdateQueue persistedQueue = findQueue(queue.getId());
            assertNotNull(persistedQueue);
            ContentDownloadJournal persistedJournal = ContentDownloadJournal.fromJson(
                    persistedQueue.getDownloadContentsJson());
            assertEquals(expectedRemoteSize, persistedJournal.getEntries().get(0).SizeBytes);
            File stagingFile = new File(UpdateQueueHelper.getTempContentPath(localName));
            assertTrue(stagingFile.exists());
            assertEquals(expectedRemoteSize, stagingFile.length());
        } finally {
            controller.cancelActive();
            deleteQueue(queue.getId());
            deleteTestFiles(localName);
            contextField.set(null, previousContext);
        }
    }

    @Test
    public void testCancellationStopsTransportAndRejectsStaleMutationAfterIdReuse() throws Exception {
        assertFalse("기존 active queue가 있으면 장비 상태 보호를 위해 테스트를 중단한다.",
                UpdateQueueHelper.hasActiveQueue());

        DataSyncManager syncManager = new DataSyncManager();
        UpdateExecutionController controller = getExecutionController();
        controller.cancelActive();

        StoredUpdateQueue first = null;
        StoredUpdateQueue replacement = null;
        try {
            first = enqueueTestQueue("device-e2e-a", "[]");
            assertNotNull(first);

            UpdateExecutionController.Token staleToken = controller.begin(
                    first.getId(), first.getExternalId());
            assertNotNull(staleToken);

            AtomicBoolean transportAborted = new AtomicBoolean(false);
            staleToken.setTransferAbortAction(() -> transportAborted.set(true));

            final long firstId = first.getId();
            controller.cancelActiveAndRun(() -> {
                deleteQueue(firstId);
                return 1;
            });
            assertTrue("execution 취소가 transport abort callback까지 전달돼야 한다.",
                    transportAborted.get());

            replacement = enqueueTestQueue("device-e2e-b", "[]");
            assertNotNull(replacement);
            assertEquals("삭제된 queue id를 재사용하는 실제 ObjectBox 경로여야 한다.",
                    firstId, replacement.getId());

            boolean staleMutation = UpdateQueueHelper.updateStatus(
                    staleToken,
                    replacement.getId(),
                    UpdateQueueContract.Status.READY);
            assertFalse("stale execution은 재사용된 id의 후속 queue를 변경하면 안 된다.",
                    staleMutation);

            StoredUpdateQueue persisted = findQueue(replacement.getId());
            assertNotNull(persisted);
            assertEquals(UpdateQueueContract.Status.QUEUED, persisted.getStatus());

            deleteQueue(replacement.getId());
            replacement = null;
            UpdateExecutionController.Token wrapperToken = controller.begin(
                    firstId + 1L, "device-e2e-wrapper");
            assertNotNull(wrapperToken);
            AtomicBoolean wrapperAbort = new AtomicBoolean(false);
            wrapperToken.setTransferAbortAction(() -> wrapperAbort.set(true));
            assertEquals(0, syncManager.cancelActiveQueues("device instrumentation cancellation gate"));
            assertTrue("DataSyncManager 취소가 processor의 transport token까지 전달돼야 한다.",
                    wrapperAbort.get());
            assertTrue(wrapperToken.isCancelled());
        } finally {
            controller.cancelActive();
            if (first != null) {
                deleteQueue(first.getId());
            }
            if (replacement != null) {
                deleteQueue(replacement.getId());
            }
            syncManager.shutdownUpdateQueueProcessor();
        }
    }

    @Test
    public void testRealFtpTransferIsAbortedByExecutionCancellation() throws Exception {
        assertFalse("기존 active queue가 있으면 장비 상태 보호를 위해 테스트를 중단한다.",
                UpdateQueueHelper.hasActiveQueue());

        Bundle arguments = InstrumentationRegistry.getArguments();
        String remotePath = arguments.getString("ftp_remote_path", "");
        String checksum = arguments.getString("ftp_checksum", "");
        long sizeBytes = Long.parseLong(arguments.getString("ftp_size", "0"));
        assertFalse("검증할 FTP remote path가 필요하다.", remotePath.isEmpty());
        assertFalse("검증할 FTP checksum이 필요하다.", checksum.isEmpty());
        assertTrue("검증할 FTP 파일 크기가 필요하다.", sizeBytes > 0L);

        String localName = "device-e2e-cancel-" + System.currentTimeMillis() + ".mp4";
        UpdateQueueContract.DownloadEntry entry = new UpdateQueueContract.DownloadEntry();
        entry.FileName = localName;
        entry.RemotePath = remotePath;
        entry.SizeBytes = sizeBytes;
        entry.Checksum = checksum;
        String downloadJson = new Gson().toJson(Collections.singletonList(entry));

        StoredUpdateQueue queue = enqueueTestQueue("device-e2e-ftp-cancel", downloadJson);
        assertNotNull(queue);

        UpdateExecutionController controller = getExecutionController();
        controller.cancelActive();
        UpdateExecutionController.Token token = controller.begin(queue.getId(), queue.getExternalId());
        assertNotNull(token);

        AtomicReference<UpdateQueueDownloader.DownloadOutcome> outcomeRef = new AtomicReference<>();
        Thread downloadThread = new Thread(() -> outcomeRef.set(
                new UpdateQueueDownloader().download(
                        queue,
                        new UpdateProgressTracker(queue.getId(), "", queue.getExternalId(), token),
                        true,
                        token)));
        try {
            downloadThread.start();
            SystemClock.sleep(500L);
            long cancelStartedAt = SystemClock.elapsedRealtime();
            controller.cancelActive();
            downloadThread.join(8000L);

            assertFalse("FTP worker가 취소 후 8초 안에 종료돼야 한다.", downloadThread.isAlive());
            assertTrue("FTP abort가 지연 없이 worker를 종료해야 한다.",
                    SystemClock.elapsedRealtime() - cancelStartedAt < 8000L);
            assertNotNull(outcomeRef.get());
            assertTrue("download outcome이 cancelled여야 한다.", outcomeRef.get().cancelled);
            assertFalse("취소된 execution이 canonical staging을 publish하면 안 된다.",
                    new File(UpdateQueueHelper.getTempContentPath(localName)).exists());
            assertFalse("취소된 execution이 final content를 생성하면 안 된다.",
                    new File(UpdateQueueHelper.getFinalContentPath(localName)).exists());
        } finally {
            controller.cancelActive();
            if (downloadThread.isAlive()) {
                downloadThread.interrupt();
                downloadThread.join(2000L);
            }
            deleteQueue(queue.getId());
            deleteTestFiles(localName);
        }
    }

    private static StoredUpdateQueue enqueueTestQueue(String externalId, String downloadJson) {
        return UpdateQueueHelper.enqueue(
                UpdateQueueContract.Type.PLAYLIST,
                "{}",
                downloadJson,
                0L,
                false,
                externalId);
    }

    private static StoredUpdateQueue findQueue(long queueId) {
        ObjectBoxDb db = ObjectBoxDb.getDefaultInstance();
        try {
            return db.where(StoredUpdateQueue.class).equalTo("id", queueId).findFirst();
        } finally {
            db.close();
        }
    }

    private static void deleteQueue(long queueId) {
        ObjectBoxDb db = ObjectBoxDb.getDefaultInstance();
        try {
            StoredUpdateQueue queue = db.where(StoredUpdateQueue.class)
                    .equalTo("id", queueId)
                    .findFirst();
            if (queue != null) {
                db.delete(queue);
            }
        } finally {
            db.close();
        }
    }

    private static void deleteTestFiles(String localName) {
        File canonical = new File(UpdateQueueHelper.getTempContentPath(localName));
        File parent = canonical.getParentFile();
        if (parent != null) {
            File[] executionFiles = parent.listFiles((dir, name) ->
                    name.startsWith(canonical.getName() + ".exec."));
            if (executionFiles != null) {
                for (File executionFile : executionFiles) {
                    if (executionFile != null) {
                        executionFile.delete();
                    }
                }
            }
        }
        canonical.delete();
        new File(UpdateQueueHelper.getFinalContentPath(localName)).delete();
    }

    private static UpdateExecutionController getExecutionController() throws Exception {
        Field field = UpdateQueueProcessor.class.getDeclaredField("executionController");
        field.setAccessible(true);
        return (UpdateExecutionController) field.get(null);
    }
}

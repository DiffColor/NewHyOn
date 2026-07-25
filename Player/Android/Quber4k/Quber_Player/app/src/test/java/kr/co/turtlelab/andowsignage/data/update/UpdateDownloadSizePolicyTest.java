package kr.co.turtlelab.andowsignage.data.update;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class UpdateDownloadSizePolicyTest {

    @Test
    public void usesRemoteSizeWhenPayloadSizeIsUnknown() {
        long resolved = UpdateDownloadSizePolicy.resolveExpectedSize(0L, 9_433_294L);

        assertEquals(9_433_294L, resolved);
    }

    @Test
    public void expandsUnknownSingleChunkToResolvedFileSize() {
        long length = UpdateDownloadSizePolicy.resolveChunkLength(0L, 9_433_294L, 0L);

        assertEquals(9_433_294L, length);
    }

    @Test
    public void preservesDeclaredSizeAndChunkLength() {
        assertEquals(1_024L, UpdateDownloadSizePolicy.resolveExpectedSize(1_024L, 2_048L));
        assertEquals(512L, UpdateDownloadSizePolicy.resolveChunkLength(512L, 1_024L, 0L));
    }
}

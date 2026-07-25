package kr.co.turtlelab.andowsignage.views;

import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

import static org.junit.Assert.assertEquals;

public class MediaTransitionLayerOrderTest {

    @Test
    public void imageOverlayIsRaisedAfterVideoSurfaceIsStaged() {
        List<String> operations = new ArrayList<>();

        MediaTransitionLayerOrder.stageVideoBehindOverlay(
                () -> operations.add("stage-video"),
                () -> operations.add("raise-image-overlay"));

        assertEquals(
                Arrays.asList("stage-video", "raise-image-overlay"),
                operations);
    }
}

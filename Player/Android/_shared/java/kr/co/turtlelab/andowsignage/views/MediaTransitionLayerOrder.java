package kr.co.turtlelab.andowsignage.views;

/**
 * VideoView의 Surface를 화면에 배치한 직후 현재 이미지 overlay를 다시 최상단으로 올린다.
 * 첫 영상 프레임이 준비되기 전 Surface의 빈 프레임이 노출되는 것을 막는다.
 */
public final class MediaTransitionLayerOrder {

    private MediaTransitionLayerOrder() { }

    public static void stageVideoBehindOverlay(Runnable stageVideo, Runnable raiseImageOverlay) {
        if (stageVideo != null) {
            stageVideo.run();
        }
        if (raiseImageOverlay != null) {
            raiseImageOverlay.run();
        }
    }
}

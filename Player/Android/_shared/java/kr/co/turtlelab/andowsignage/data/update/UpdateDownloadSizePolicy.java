package kr.co.turtlelab.andowsignage.data.update;

/**
 * 다운로드 payload에 파일 크기가 누락됐을 때 FTP 원격 크기로 보완하는 정책.
 */
public final class UpdateDownloadSizePolicy {

    private UpdateDownloadSizePolicy() { }

    public static long resolveExpectedSize(long declaredSize, long remoteSize) {
        return declaredSize > 0L ? declaredSize : Math.max(0L, remoteSize);
    }

    public static long resolveChunkLength(long declaredLength, long expectedSize, long offset) {
        if (declaredLength > 0L) {
            return declaredLength;
        }
        return Math.max(0L, expectedSize - Math.max(0L, offset));
    }
}

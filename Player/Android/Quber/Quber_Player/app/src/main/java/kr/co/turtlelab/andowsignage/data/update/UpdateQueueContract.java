package kr.co.turtlelab.andowsignage.data.update;

import java.util.ArrayList;
import java.util.List;

/**
 * UpdateQueue 레코드가 따르는 공통 스키마와 상태 정의.
 * JSON 페이로드 구조를 명시해 두면 추후 검증/파싱 시점에 재사용이 쉬워진다.
 */
public final class UpdateQueueContract {

    private UpdateQueueContract() { }

    public static final class Type {
        public static final String PLAYLIST = "playlist";
        public static final String WELCOME = "welcome";
        public static final String WEEKLY = "weekly";
        private Type() { }
    }

    public static final class Status {
        public static final String QUEUED = "QUEUED";
        public static final String DOWNLOADING = "DOWNLOADING";
        public static final String DOWNLOADED = "DOWNLOADED";
        public static final String VALIDATING = "VALIDATING";
        public static final String READY = "READY";
        public static final String APPLYING = "APPLYING";
        public static final String DONE = "DONE";
        public static final String FAILED = "FAILED";
        public static final String CANCELLED = "CANCELLED";
        private Status() { }
    }

    /**
     * 진행률 가중치: JSON 수신(0.1) + 다운로드(0.7) + 검증(0.1) + 적용(0.1)
     */
    public static final class ProgressWeight {
        public static final float PAYLOAD = 0.10f;
        public static final float DOWNLOAD = 0.70f;
        public static final float VALIDATE = 0.10f;
        public static final float APPLY = 0.10f;
        public static final float EPSILON = 0.5f; // 보고 트리거 임계값(%)
        private ProgressWeight() { }
    }

    public static final class RetryPolicy {
        // Windows UpdateService와 동일: 30s → 60s → 120s → 180s(상한)
        public static final int MAX_ATTEMPTS = Integer.MAX_VALUE;
        private static final long BASE_DELAY_MS = 30_000L;
        private static final long MAX_DELAY_MS = 180_000L;

        private RetryPolicy() { }

        public static long getDelayMs(int attemptIndex) {
            int idx = Math.max(1, attemptIndex);
            double delay = BASE_DELAY_MS * Math.pow(2, Math.max(0, idx - 1));
            return (long) Math.min(delay, MAX_DELAY_MS);
        }
    }

    /**
     * payloadJson 에 저장되는 기본 구조. Playlist 를 중심으로 정의한다.
     */
    public static final class PlaylistPayload {
        public String playerId;
        public String playerName;
        public boolean playerLandscape;
        public String playlistId;
        public String playlistName;
        public List<PagePayload> pages = new ArrayList<>();
    }

    public static final class PagePayload {
        public String pageId;
        public String pageName;
        public int orderIndex;
        public int playHour;
        public int playMinute;
        public int playSecond;
        public int volume;
        public boolean landscape;
        public List<ElementPayload> elements = new ArrayList<>();
    }

    public static final class ElementPayload {
        public String elementId;
        public String pageId;
        public String name;
        public String type;
        public double width;
        public double height;
        public double posTop;
        public double posLeft;
        public int zIndex;
        public List<ContentPayload> contents = new ArrayList<>();
    }

    public static final class ContentPayload {
        public String uid;
        public String elementId;
        public String fileName;
        public String fileFullPath;
        public String contentType;
        public String playMinute;
        public String playSecond;
        public boolean valid;
        public int scrollSpeedSec;
        public String remoteChecksum;
        public long fileSize;
        public boolean fileExist;
    }

    /**
     * downloadContentsJson 에 저장되는 파일 다운로드 정의.
     */
    public static final class DownloadContentEntry {
        public String contentUid;
        public String fileName;
        public long sizeBytes;
        public String checksum;
        public String remotePath;
        public String status; // pending/downloading/done/failed
        public long downloadedBytes;
        public long lastUpdatedAt;
    }

    public static final class DownloadStatus {
        public static final String QUEUED = "QUEUED";
        public static final String DOWNLOADING = "DOWNLOADING";
        public static final String DONE = "DONE";
        public static final String FAILED = "FAILED";
        private DownloadStatus() { }
    }

    /**
     * Windows UpdateQueue 다운로드 JSON과 동일한 구조.
     */
    public static final class DownloadEntry {
        public String FileName;
        public String RemotePath;
        public long SizeBytes;
        public String Checksum;
        public String Status = DownloadStatus.QUEUED;
        public int Attempts;
        public String LastError = "";
        public List<DownloadChunk> Chunks = new ArrayList<>();
    }

    public static final class DownloadChunk {
        public int Index;
        public long Offset;
        public long Length;
        public String Status = ChunkStatus.PENDING;
        public long DownloadedBytes;
        public long LastUpdatedTicks;
    }

    public static final class ChunkStatus {
        public static final String PENDING = "pending";
        public static final String DOWNLOADING = "downloading";
        public static final String DONE = "done";
        public static final String FAILED = "failed";
        private ChunkStatus() { }
    }
}

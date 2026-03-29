package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import com.google.gson.Gson;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/**
 * downloadContentsJson 을 파싱/관리하는 헬퍼.
 * 파일 단위 상태를 갱신하고, JSON 문자열로 다시 직렬화할 수 있다.
 */
public class ContentDownloadJournal {

    private static final Gson GSON = new Gson();
    private final List<UpdateQueueContract.DownloadEntry> entries;

    private ContentDownloadJournal(List<UpdateQueueContract.DownloadEntry> entries) {
        this.entries = entries;
    }

    public static ContentDownloadJournal fromJson(String json) {
        if (TextUtils.isEmpty(json)) {
            return new ContentDownloadJournal(new ArrayList<>());
        }
        try {
            java.lang.reflect.Type type = new com.google.gson.reflect.TypeToken<List<UpdateQueueContract.DownloadEntry>>() {}.getType();
            List<UpdateQueueContract.DownloadEntry> items = GSON.fromJson(json, type);
            if (items == null) {
                items = new ArrayList<>();
            }
            return new ContentDownloadJournal(items);
        } catch (Exception e) {
            return new ContentDownloadJournal(new ArrayList<>());
        }
    }

    public String toJson() {
        return GSON.toJson(entries);
    }

    public List<UpdateQueueContract.DownloadEntry> getEntries() {
        return entries;
    }

    public void ensureDefaults() {
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            if (TextUtils.isEmpty(entry.Status)) {
                entry.Status = UpdateQueueContract.DownloadStatus.QUEUED;
            }
            if (entry.Chunks == null) {
                entry.Chunks = new ArrayList<>();
            }
            for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
                if (chunk == null) {
                    continue;
                }
                if (TextUtils.isEmpty(chunk.Status)) {
                    chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
                }
                if (chunk.DownloadedBytes < 0) {
                    chunk.DownloadedBytes = 0L;
                }
                if (chunk.LastUpdatedTicks < 0) {
                    chunk.LastUpdatedTicks = 0L;
                }
            }
        }
    }

    public List<UpdateQueueContract.DownloadEntry> getPendingEntriesSortedBySize() {
        List<UpdateQueueContract.DownloadEntry> pending = new ArrayList<>();
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (!isEntryComplete(entry)) {
                pending.add(entry);
            }
        }
        Collections.sort(pending, new Comparator<UpdateQueueContract.DownloadEntry>() {
            @Override
            public int compare(UpdateQueueContract.DownloadEntry o1, UpdateQueueContract.DownloadEntry o2) {
                return Long.compare(safeSize(o1), safeSize(o2));
            }
        });
        return pending;
    }

    public void updateEntryStatus(String fileName, String status) {
        UpdateQueueContract.DownloadEntry entry = findEntry(fileName);
        if (entry == null) {
            return;
        }
        entry.Status = status;
        if (entry.Chunks != null) {
            long nowTicks = System.currentTimeMillis();
            for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
                if (chunk == null) {
                    continue;
                }
                if (UpdateQueueContract.ChunkStatus.DONE.equals(chunk.Status)) {
                    continue;
                }
                chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
                chunk.DownloadedBytes = 0L;
                chunk.LastUpdatedTicks = nowTicks;
            }
        }
    }

    public boolean isComplete() {
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (!isEntryComplete(entry)) {
                return false;
            }
        }
        return true;
    }

    public void resetAllToPending() {
        long now = System.currentTimeMillis();
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            entry.Status = UpdateQueueContract.DownloadStatus.QUEUED;
            entry.LastError = "";
            if (entry.Chunks != null) {
                for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
                    if (chunk == null) {
                        continue;
                    }
                    chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
                    chunk.DownloadedBytes = 0L;
                    chunk.LastUpdatedTicks = now;
                }
            }
        }
    }

    private boolean isEntryComplete(UpdateQueueContract.DownloadEntry entry) {
        if (entry == null) {
            return true;
        }
        return UpdateQueueContract.DownloadStatus.DONE.equals(entry.Status);
    }

    private long safeSize(UpdateQueueContract.DownloadEntry entry) {
        if (entry == null) {
            return Long.MAX_VALUE;
        }
        if (entry.SizeBytes > 0) {
            return entry.SizeBytes;
        }
        long done = 0L;
        if (entry.Chunks != null) {
            for (UpdateQueueContract.DownloadChunk chunk : entry.Chunks) {
                if (chunk == null) {
                    continue;
                }
                done += Math.max(0L, chunk.DownloadedBytes);
            }
        }
        return done > 0 ? done : (Long.MAX_VALUE - 1);
    }

    private UpdateQueueContract.DownloadEntry findEntry(String fileName) {
        if (TextUtils.isEmpty(fileName)) {
            return null;
        }
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry != null && fileName.equalsIgnoreCase(entry.FileName)) {
                return entry;
            }
        }
        return null;
    }
}

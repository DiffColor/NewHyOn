package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;

import java.lang.reflect.Type;
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
    private static final Type ENTRY_LIST = new TypeToken<List<UpdateQueueContract.DownloadContentEntry>>() {}.getType();

    private final List<UpdateQueueContract.DownloadContentEntry> entries;

    private ContentDownloadJournal(List<UpdateQueueContract.DownloadContentEntry> entries) {
        this.entries = entries;
    }

    public static ContentDownloadJournal fromJson(String json) {
        if (TextUtils.isEmpty(json)) {
            return new ContentDownloadJournal(new ArrayList<>());
        }
        try {
            List<UpdateQueueContract.DownloadContentEntry> items = GSON.fromJson(json, ENTRY_LIST);
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

    public List<UpdateQueueContract.DownloadContentEntry> getEntries() {
        return entries;
    }

    public void ensureDefaults() {
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            if (TextUtils.isEmpty(entry.status)) {
                entry.status = UpdateQueueContract.DownloadStatus.PENDING;
            }
            if (entry.downloadedBytes < 0) {
                entry.downloadedBytes = 0L;
            }
            if (entry.sizeBytes > 0 && entry.downloadedBytes > entry.sizeBytes) {
                entry.downloadedBytes = entry.sizeBytes;
            }
            if (entry.lastUpdatedAt < 0) {
                entry.lastUpdatedAt = 0L;
            }
        }
    }

    public List<UpdateQueueContract.DownloadContentEntry> getPendingEntriesSortedBySize() {
        List<UpdateQueueContract.DownloadContentEntry> pending = new ArrayList<>();
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (!isEntryComplete(entry)) {
                pending.add(entry);
            }
        }
        Collections.sort(pending, new Comparator<UpdateQueueContract.DownloadContentEntry>() {
            @Override
            public int compare(UpdateQueueContract.DownloadContentEntry o1, UpdateQueueContract.DownloadContentEntry o2) {
                return Long.compare(safeSize(o1), safeSize(o2));
            }
        });
        return pending;
    }

    public void updateEntryStatus(String contentUid, String status, long downloadedBytes) {
        UpdateQueueContract.DownloadContentEntry entry = findEntry(contentUid);
        if (entry == null) {
            return;
        }
        long now = System.currentTimeMillis();
        entry.status = status;
        entry.downloadedBytes = downloadedBytes;
        entry.lastUpdatedAt = now;
    }

    public boolean isComplete() {
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (!isEntryComplete(entry)) {
                return false;
            }
        }
        return true;
    }

    public void resetAllToPending() {
        long now = System.currentTimeMillis();
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            entry.status = UpdateQueueContract.DownloadStatus.PENDING;
            entry.downloadedBytes = 0L;
            entry.lastUpdatedAt = now;
        }
    }

    private boolean isEntryComplete(UpdateQueueContract.DownloadContentEntry entry) {
        if (entry == null) {
            return true;
        }
        return UpdateQueueContract.DownloadStatus.DONE.equals(entry.status);
    }

    private long safeSize(UpdateQueueContract.DownloadContentEntry entry) {
        if (entry == null) {
            return Long.MAX_VALUE;
        }
        if (entry.sizeBytes > 0) {
            return entry.sizeBytes;
        }
        if (entry.downloadedBytes > 0) {
            return entry.downloadedBytes;
        }
        return Long.MAX_VALUE - 1;
    }

    private UpdateQueueContract.DownloadContentEntry findEntry(String contentUid) {
        if (TextUtils.isEmpty(contentUid)) {
            return null;
        }
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (contentUid.equals(entry.contentUid)) {
                return entry;
            }
        }
        return null;
    }
}

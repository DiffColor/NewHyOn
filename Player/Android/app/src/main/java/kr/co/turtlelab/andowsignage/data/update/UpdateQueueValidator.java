package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

import com.google.gson.Gson;

import java.io.File;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

/**
 * READY 상태로 전환하기 전에 다운로드된 파일들이 모두 존재하고 무결한지 검증한다.
 */
public class UpdateQueueValidator {

    private final Gson gson = new Gson();

    public boolean validate(RealmUpdateQueue queue, UpdateProgressTracker tracker) {
        if (queue == null) {
            return false;
        }
        ContentDownloadJournal journal = ContentDownloadJournal.fromJson(queue.getDownloadContentsJson());
        journal.ensureDefaults();
        Map<String, UpdateQueueContract.DownloadContentEntry> downloadMap = indexByUid(journal.getEntries());
        UpdateQueueContract.PlaylistPayload payload = gson.fromJson(
                queue.getPayloadJson(), UpdateQueueContract.PlaylistPayload.class);
        if (payload == null || payload.pages == null) {
            return false;
        }
        int total = 0;
        for (UpdateQueueContract.PagePayload page : payload.pages) {
            if (page == null || page.elements == null) {
                continue;
            }
            for (UpdateQueueContract.ElementPayload element : page.elements) {
                if (element == null || element.contents == null) {
                    continue;
                }
                total += element.contents.size();
            }
        }
        int validated = 0;
        boolean journalChanged = false;
        for (UpdateQueueContract.PagePayload page : payload.pages) {
            if (page == null || page.elements == null) {
                continue;
            }
            for (UpdateQueueContract.ElementPayload element : page.elements) {
                if (element == null || element.contents == null) {
                    continue;
                }
                for (UpdateQueueContract.ContentPayload content : element.contents) {
                    if (!requiresFile(content)) {
                        validated++;
                        tracker.stepValidate((float) validated / Math.max(1, total));
                        continue;
                    }
                    UpdateQueueContract.DownloadContentEntry dl = downloadMap.get(content.uid);
                    String relative = dl != null ? dl.remotePath : content.fileName;
                    boolean ok = verifyStagedOrFinal(relative, content);
                    if (!ok) {
                        if (dl != null) {
                            dl.status = UpdateQueueContract.DownloadStatus.FAILED;
                            dl.downloadedBytes = 0L;
                            dl.lastUpdatedAt = System.currentTimeMillis();
                            journalChanged = true;
                        }
                        return false;
                    }
                    if (dl != null) {
                        dl.status = UpdateQueueContract.DownloadStatus.DONE;
                        File finalFile = new File(LocalPathUtils.getAbsolutePath(relative));
                        File tempFile = new File(LocalPathUtils.getTempPath(relative));
                        long size = finalFile.exists() ? finalFile.length() : tempFile.length();
                        dl.downloadedBytes = dl.sizeBytes > 0 ? dl.sizeBytes : size;
                        dl.lastUpdatedAt = System.currentTimeMillis();
                        journalChanged = true;
                    }
                    validated++;
                    tracker.stepValidate((float) validated / Math.max(1, total));
                }
            }
        }
        if (journalChanged) {
            UpdateQueueHelper.updateDownloadJournal(queue.getId(), journal.toJson());
        }
        return true;
    }

    private boolean requiresFile(UpdateQueueContract.ContentPayload content) {
        if (content == null) {
            return false;
        }
        if (TextUtils.isEmpty(content.fileName)) {
            return false;
        }
        String type = content.contentType == null ? "" : content.contentType;
        return !type.equalsIgnoreCase("WebSiteURL");
    }

    private boolean verifyStagedOrFinal(String relativePath, UpdateQueueContract.ContentPayload content) {
        String raw = TextUtils.isEmpty(relativePath) ? content.fileName : relativePath;
        if (TextUtils.isEmpty(raw)) {
            return false;
        }
        String normalized = normalize(raw);
        if (verifyByRelative(raw, content)) {
            return true;
        }
        if (!TextUtils.equals(raw, normalized)) {
            return verifyByRelative(normalized, content);
        }
        return false;
    }

    private boolean verifyByRelative(String relative, UpdateQueueContract.ContentPayload content) {
        if (TextUtils.isEmpty(relative)) {
            return false;
        }
        File tempFile = new File(LocalPathUtils.getTempPath(relative));
        if (FileIntegrityUtils.verifyFile(tempFile, content.fileSize, content.remoteChecksum)) {
            return true;
        }
        File finalFile = new File(LocalPathUtils.getAbsolutePath(relative));
        return FileIntegrityUtils.verifyFile(finalFile, content.fileSize, content.remoteChecksum);
    }

    private Map<String, UpdateQueueContract.DownloadContentEntry> indexByUid(List<UpdateQueueContract.DownloadContentEntry> entries) {
        Map<String, UpdateQueueContract.DownloadContentEntry> map = new HashMap<>();
        if (entries == null) {
            return map;
        }
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null || TextUtils.isEmpty(entry.contentUid)) {
                continue;
            }
            map.put(entry.contentUid, entry);
        }
        return map;
    }

    private String normalize(String path) {
        if (TextUtils.isEmpty(path)) {
            return path;
        }
        String p = path.replace("\\", "/");
        while (p.startsWith("/")) {
            p = p.substring(1);
        }
        return p;
    }
}

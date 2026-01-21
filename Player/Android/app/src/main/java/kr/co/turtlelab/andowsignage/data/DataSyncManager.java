package kr.co.turtlelab.andowsignage.data;

import android.text.TextUtils;

import com.google.gson.Gson;

import java.io.File;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.Collections;
import java.util.Comparator;
import java.util.Deque;
import java.util.List;
import java.util.Set;
import java.util.Map;
import java.util.HashMap;

import io.realm.Realm;
import io.realm.RealmList;
import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.data.realm.RealmContent;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.data.realm.RealmWelcome;
import kr.co.turtlelab.andowsignage.data.realm.RealmWeeklySchedule;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkModels;
import kr.co.turtlelab.andowsignage.data.update.ContentDownloadJournal;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueDownloader;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueHelper;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueProcessor;
import kr.co.turtlelab.andowsignage.dataproviders.UpdateQueueProvider;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

/**
 * RethinkDB -> Realm 으로 데이터를 동기화한다.
 */
public class DataSyncManager {

    private final RethinkDbClient rethinkClient = RethinkDbClient.getInstance();
    private final Gson gson = new Gson();
    private final UpdateQueueProcessor queueProcessor;

    public DataSyncManager() {
        queueProcessor = new UpdateQueueProcessor(this::applyQueueEntry);
    }

    public void updateEndpoint(String host) {
        rethinkClient.updateHost(host);
    }

    public boolean syncPlayerData(RethinkModels.PlayerInfoRecord player) {
        if (player == null)
            return false;

        if(TextUtils.isEmpty(player.getPlaylist()))
            return false;

        RethinkModels.PageListRecord pageList = rethinkClient.fetchPageList(player.getPlaylist());
        if (pageList == null) {
            return false;
        }
        List<RethinkModels.PageInfoRecord> pages = rethinkClient.fetchPagesByIds(pageList.getPages());
        writeToRealm(player, pageList, pages);
        rethinkClient.clearCommand(player.getGuid());
        return true;
    }

    public boolean syncWeeklySchedule(RethinkModels.PlayerInfoRecord player) {
        if (player == null)
            return false;

        String realmPlayerKey = player.getPlayerName();
        RethinkModels.WeeklyScheduleRecord weekly = rethinkClient.fetchWeeklySchedule(player.getGuid());
        if (TextUtils.isEmpty(realmPlayerKey)) {
            realmPlayerKey = player.getGuid();
        }

        storeWeeklySchedule(realmPlayerKey, weekly);
        return true;
    }

    public long enqueuePlaylistUpdate(RethinkModels.PlayerInfoRecord player) {
        if (player == null || TextUtils.isEmpty(player.getPlaylist())) {
            return -1;
        }
        RethinkModels.PageListRecord pageList = rethinkClient.fetchPageList(player.getPlaylist());
        if (pageList == null) {
            return -1;
        }
        List<RethinkModels.PageInfoRecord> pages = rethinkClient.fetchPagesByIds(pageList.getPages());
        return enqueuePlaylist(player, pageList, pages);
    }

    private void writeToRealm(RethinkModels.PlayerInfoRecord player,
                              RethinkModels.PageListRecord pageList,
                              List<RethinkModels.PageInfoRecord> pages) {
        Realm realm = Realm.getDefaultInstance();
        final Set<String> usedContentPaths = new HashSet<>();
        realm.executeTransaction(r -> {
            r.delete(RealmPage.class);
            r.delete(RealmElement.class);
            r.delete(RealmContent.class);
            r.delete(RealmWelcome.class);

            RealmPlayer realmPlayer = r.where(RealmPlayer.class).findFirst();
            if (realmPlayer != null && TextUtils.isEmpty(realmPlayer.getPlayerId()) == false
                    && TextUtils.equals(realmPlayer.getPlayerId(), player.getGuid()) == false) {
                realmPlayer.deleteFromRealm();
                realmPlayer = null;
            }
            if (realmPlayer == null) {
                realmPlayer = r.createObject(RealmPlayer.class, player.getGuid());
            }
            realmPlayer.setPlayerName(player.getPlayerName());
            realmPlayer.setPlaylistName(player.getPlaylist());
            realmPlayer.setLandscape(player.isLandscape());

            List<String> orderedIds = pageList.getPages();
            for (int i = 0; i < pages.size(); i++) {
                RethinkModels.PageInfoRecord page = pages.get(i);
                int orderIndex = orderedIds.indexOf(page.getGuid());
                if (orderIndex < 0) {
                    orderIndex = i;
                }

                RealmPage realmPage = r.createObject(RealmPage.class, page.getGuid());
                realmPage.setPageName(page.getPageName());
                realmPage.setPlaylistName(pageList.getName());
                realmPage.setOrderIndex(orderIndex);
                realmPage.setPlayHour(page.getPlayHour());
                realmPage.setPlayMinute(page.getPlayMinute());
                realmPage.setPlaySecond(page.getPlaySecond());
                realmPage.setVolume(page.getVolume());
                realmPage.setLandscape(page.isLandscape());

                RealmList<RealmElement> realmElements = new RealmList<>();
                        if (page.getElements() != null) {
                            for (RethinkModels.ElementInfoRecord element : page.getElements()) {
                                String elementId = page.getGuid() + "_" + element.getName();
                                RealmElement realmElement = r.createObject(RealmElement.class, elementId);
                                realmElement.setPageId(page.getGuid());
                                realmElement.setName(element.getName());
                        realmElement.setType(element.getType());
                        realmElement.setWidth(element.getWidth());
                        realmElement.setHeight(element.getHeight());
                        realmElement.setPosLeft(element.getPosLeft());
                        realmElement.setPosTop(element.getPosTop());
                        realmElement.setzIndex(element.getzIndex());

                        RealmList<RealmContent> contents = new RealmList<>();
                                List<RethinkModels.ContentInfoRecord> contentRecords = element.getContents();
                                if (contentRecords != null) {
                                    for (int index = 0; index < contentRecords.size(); index++) {
                                        RethinkModels.ContentInfoRecord contentRecord = contentRecords.get(index);
                                        String uid = elementId + "_" + index;
                                        RealmContent realmContent = r.createObject(RealmContent.class, uid);
                                        realmContent.setElementId(elementId);
                                        realmContent.setFileName(contentRecord.getFileName());
                                        String localAbsolutePath = LocalPathUtils.getContentPath(contentRecord.getFileName());
                                        realmContent.setFileFullPath(localAbsolutePath);
                                        usedContentPaths.add(new File(localAbsolutePath).getAbsolutePath());
                                        realmContent.setContentType(contentRecord.getContentType());
                                        realmContent.setPlayMinute(contentRecord.getPlayMinute());
                                        realmContent.setPlaySecond(contentRecord.getPlaySecond());
                                        realmContent.setValid(contentRecord.isValidTime());
                                        realmContent.setFileExist(contentRecord.isFileExist());
                                realmContent.setScrollSpeedSec(contentRecord.getScrollSpeedSec());
                                contents.add(realmContent);
                            }
                        }
                            realmElement.setContents(contents);
                            realmElements.add(realmElement);

                            if ("TemplateBoard".equalsIgnoreCase(element.getType())
                                    || "WelcomeBoard".equalsIgnoreCase(element.getType())) {
                            storeWelcome(r, page, element, usedContentPaths);
                            }
                        }
                    }
                    realmPage.setElements(realmElements);
                }
        });
        realm.close();
        cleanupUnusedContents(usedContentPaths);
    }

    private long enqueuePlaylist(RethinkModels.PlayerInfoRecord player,
                                 RethinkModels.PageListRecord pageList,
                                 List<RethinkModels.PageInfoRecord> pages) {
        UpdateQueueContract.PlaylistPayload payload = new UpdateQueueContract.PlaylistPayload();
        payload.playerId = player.getGuid();
        payload.playerName = player.getPlayerName();
        payload.playerLandscape = player.isLandscape();
        payload.playlistId = pageList.getId();
        payload.playlistName = pageList.getName();
        payload.pages = new ArrayList<>();
        List<UpdateQueueContract.DownloadContentEntry> downloadEntries = new ArrayList<>();

        for (int i = 0; i < pages.size(); i++) {
            RethinkModels.PageInfoRecord page = pages.get(i);
            UpdateQueueContract.PagePayload pageEntry = new UpdateQueueContract.PagePayload();
            pageEntry.pageId = page.getGuid();
            pageEntry.pageName = page.getPageName();
            pageEntry.orderIndex = pageList.getPages().indexOf(page.getGuid());
            if (pageEntry.orderIndex < 0) {
                pageEntry.orderIndex = i;
            }
            pageEntry.playHour = page.getPlayHour();
            pageEntry.playMinute = page.getPlayMinute();
            pageEntry.playSecond = page.getPlaySecond();
            pageEntry.volume = page.getVolume();
            pageEntry.landscape = page.isLandscape();
            pageEntry.elements = new ArrayList<>();

            if (page.getElements() != null) {
                for (RethinkModels.ElementInfoRecord element : page.getElements()) {
                    UpdateQueueContract.ElementPayload elementEntry = new UpdateQueueContract.ElementPayload();
                    elementEntry.elementId = page.getGuid() + "_" + element.getName();
                    elementEntry.pageId = page.getGuid();
                    elementEntry.name = element.getName();
                    elementEntry.type = element.getType();
                    elementEntry.width = element.getWidth();
                    elementEntry.height = element.getHeight();
                    elementEntry.posLeft = element.getPosLeft();
                    elementEntry.posTop = element.getPosTop();
                    elementEntry.zIndex = element.getzIndex();
                    elementEntry.contents = new ArrayList<>();

                    List<RethinkModels.ContentInfoRecord> contents = element.getContents();
                    if (contents != null) {
                        for (int idx = 0; idx < contents.size(); idx++) {
                            RethinkModels.ContentInfoRecord contentRecord = contents.get(idx);
                            UpdateQueueContract.ContentPayload contentEntry = new UpdateQueueContract.ContentPayload();
                            contentEntry.uid = elementEntry.elementId + "_" + idx;
                            contentEntry.elementId = elementEntry.elementId;
                            contentEntry.fileName = contentRecord.getFileName();
                            contentEntry.fileFullPath = contentRecord.getFileFullPath();
                            contentEntry.contentType = contentRecord.getContentType();
                            contentEntry.playMinute = contentRecord.getPlayMinute();
                            contentEntry.playSecond = contentRecord.getPlaySecond();
                            contentEntry.valid = contentRecord.isValidTime();
                            contentEntry.scrollSpeedSec = contentRecord.getScrollSpeedSec();
                            contentEntry.fileSize = contentRecord.getFileSize();
                            contentEntry.fileExist = contentRecord.isFileExist();
                            contentEntry.remoteChecksum = TextUtils.isEmpty(contentRecord.getFileHash())
                                    ? contentRecord.getGuid()
                                    : contentRecord.getFileHash();
                            elementEntry.contents.add(contentEntry);

                            UpdateQueueContract.DownloadContentEntry entry = new UpdateQueueContract.DownloadContentEntry();
                            entry.contentUid = contentEntry.uid;
                            entry.fileName = contentRecord.getFileName();
                            entry.remotePath = contentRecord.getRelativePath();
                            entry.sizeBytes = contentRecord.getFileSize();
                            entry.checksum = TextUtils.isEmpty(contentRecord.getFileHash())
                                    ? contentRecord.getGuid()
                                    : contentRecord.getFileHash();
                            downloadEntries.add(entry);
                        }
                    }
                    pageEntry.elements.add(elementEntry);
                }
            }
            payload.pages.add(pageEntry);
        }

        String payloadJson = gson.toJson(payload);
        String downloadJson = gson.toJson(downloadEntries);
        RealmUpdateQueue enqueued = UpdateQueueHelper.enqueue(UpdateQueueContract.Type.PLAYLIST, payloadJson, downloadJson, 0);
        queueProcessor.schedule();
        return enqueued != null ? enqueued.getId() : -1;
    }

    private boolean applyQueueEntry(RealmUpdateQueue queue) {
        if (queue == null) {
            return false;
        }
        boolean applied = false;
        String playerGUID = "";
        MoveResult moveResult = new MoveResult();
        ApplyBackup backup = createApplyBackup();
        java.util.List<UpdateQueueContract.DownloadContentEntry> downloads =
                ContentDownloadJournal.fromJson(queue.getDownloadContentsJson()).getEntries();
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.APPLYING);
        if (TextUtils.equals(queue.getType(), UpdateQueueContract.Type.PLAYLIST)) {
            UpdateQueueContract.PlaylistPayload payload = gson.fromJson(
                    queue.getPayloadJson(),
                    UpdateQueueContract.PlaylistPayload.class);
            if (payload == null) {
                return false;
            }
            try {
                moveResult = moveStagedDownloadsToFinal(downloads);
            } catch (Exception ignore) { }
            if (moveResult.allMoved) {
                try {
                    playerGUID = payload.playerId;
                    writePlaylistPayload(payload, downloads);
                    if (!TextUtils.isEmpty(payload.playerId)) {
                        rethinkClient.clearCommand(payload.playerId);
                    }
                    applied = true;
                } catch (Exception ignore) {
                    applied = false;
                    restoreApplyBackup(backup);
                }
            } else {
                applied = false;
            }
        }
        if (applied) {
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DONE);
            // 원격 UpdateQueue 레코드는 삭제하되 PlayerInfo의 최종 상태/진행률은 남긴다.
            if (!TextUtils.isEmpty(playerGUID)) {
                RethinkDbClient.getInstance().deleteQueueRecord(String.valueOf(queue.getId()), playerGUID);
            } else {
                RethinkDbClient.getInstance().deleteQueueRecord(String.valueOf(queue.getId()));
            }
        } else {
            long delay = UpdateQueueContract.RetryPolicy.getDelayMs(queue.getRetryCount() + 1);
            UpdateQueueHelper.incrementRetry(queue.getId(), System.currentTimeMillis() + delay);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.FAILED,
                    "APPLY", "Failed to apply queue");
        }
        if (applied) {
            cleanupTempDownloads(downloads, moveResult);
        }
        return applied;
    }

    public boolean applyNextReadyQueue() {
        return UpdateQueueProvider.consumeNextReadyQueue(this::applyQueueEntry);
    }

    public void resumePendingQueues() {
        queueProcessor.schedule();
    }

    private void storeWelcome(Realm realm,
                              RethinkModels.PageInfoRecord page,
                              RethinkModels.ElementInfoRecord element,
                              Set<String> usedContentPaths) {
        if (element == null) {
            return;
        }
        RethinkModels.TextInfoRecord textInfo = rethinkClient.fetchTextInfo(page.getPageName(), element.getName());
        if (textInfo == null) {
            return;
        }
        String elementId = page.getGuid() + "_" + element.getName();
        RealmWelcome welcome = realm.createObject(RealmWelcome.class, elementId);
        welcome.setPageId(page.getGuid());
        welcome.setElementName(element.getName());
        welcome.setImageFileName(textInfo.getImageFileName());
        String localImagePath = LocalPathUtils.getContentPath(textInfo.getImageFileName());
        welcome.setImageFilePath(localImagePath);
        if (usedContentPaths != null && !TextUtils.isEmpty(localImagePath)) {
            usedContentPaths.add(new File(localImagePath).getAbsolutePath());
        }
    }

    private void storeWeeklySchedule(String playerId, RethinkModels.WeeklyScheduleRecord record) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmWeeklySchedule schedule = r.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", playerId)
                    .findFirst();

            if (record == null) {
                if (schedule != null) {
                    schedule.deleteFromRealm();
                }
                return;
            }

            if (schedule == null) {
                schedule = r.createObject(RealmWeeklySchedule.class, playerId);
            }

            applyDay(schedule, "MON", record.getMonday());
            applyDay(schedule, "TUE", record.getTuesday());
            applyDay(schedule, "WED", record.getWednesday());
            applyDay(schedule, "THU", record.getThursday());
            applyDay(schedule, "FRI", record.getFriday());
            applyDay(schedule, "SAT", record.getSaturday());
            applyDay(schedule, "SUN", record.getSunday());
        });
        realm.close();
    }

    private void writePlaylistPayload(UpdateQueueContract.PlaylistPayload payload,
                                      List<UpdateQueueContract.DownloadContentEntry> downloads) {
        if (payload == null) {
            return;
        }
        final Map<String, String> downloadPathMap = indexDownloadPaths(downloads);
        Realm realm = Realm.getDefaultInstance();
        final Set<String> usedContentPaths = new HashSet<>();
        realm.executeTransaction(r -> {
            r.delete(RealmPage.class);
            r.delete(RealmElement.class);
            r.delete(RealmContent.class);
            r.delete(RealmWelcome.class);

            String playerId = !TextUtils.isEmpty(payload.playerId)
                    ? payload.playerId
                    : payload.playlistId;
            RealmPlayer realmPlayer = r.where(RealmPlayer.class).findFirst();
            if (realmPlayer != null && !TextUtils.isEmpty(playerId)
                    && !TextUtils.equals(realmPlayer.getPlayerId(), playerId)) {
                realmPlayer.deleteFromRealm();
                realmPlayer = null;
            }
            if (realmPlayer == null && !TextUtils.isEmpty(playerId)) {
                realmPlayer = r.createObject(RealmPlayer.class, playerId);
            }
            if (realmPlayer != null) {
                if (!TextUtils.isEmpty(payload.playerName)) {
                    realmPlayer.setPlayerName(payload.playerName);
                }
                realmPlayer.setPlaylistName(payload.playlistName);
                realmPlayer.setLandscape(payload.playerLandscape);
            }

            List<UpdateQueueContract.PagePayload> pagePayloads = new ArrayList<>();
            if (payload.pages != null) {
                pagePayloads.addAll(payload.pages);
            }
            Collections.sort(pagePayloads, new Comparator<UpdateQueueContract.PagePayload>() {
                @Override
                public int compare(UpdateQueueContract.PagePayload o1, UpdateQueueContract.PagePayload o2) {
                    return o1.orderIndex - o2.orderIndex;
                }
            });

            for (UpdateQueueContract.PagePayload page : pagePayloads) {
                RealmPage realmPage = r.createObject(RealmPage.class, page.pageId);
                realmPage.setPageName(page.pageName);
                realmPage.setPlaylistName(payload.playlistName);
                realmPage.setOrderIndex(page.orderIndex);
                realmPage.setPlayHour(page.playHour);
                realmPage.setPlayMinute(page.playMinute);
                realmPage.setPlaySecond(page.playSecond);
                realmPage.setVolume(page.volume);
                realmPage.setLandscape(page.landscape);

                RealmList<RealmElement> realmElements = new RealmList<>();
                if (page.elements != null) {
                    for (UpdateQueueContract.ElementPayload element : page.elements) {
                        RealmElement realmElement = r.createObject(RealmElement.class, element.elementId);
                        realmElement.setPageId(page.pageId);
                        realmElement.setName(element.name);
                        realmElement.setType(element.type);
                        realmElement.setWidth(element.width);
                        realmElement.setHeight(element.height);
                        realmElement.setPosLeft(element.posLeft);
                        realmElement.setPosTop(element.posTop);
                        realmElement.setzIndex(element.zIndex);

                        RealmList<RealmContent> contents = new RealmList<>();
                            if (element.contents != null) {
                                for (UpdateQueueContract.ContentPayload content : element.contents) {
                        RealmContent realmContent = r.createObject(RealmContent.class, content.uid);
                        realmContent.setElementId(element.elementId);
                        realmContent.setFileName(content.fileName);
                        String relativePath = downloadPathMap.get(content.uid);
                        String absolutePath;
                        if (!TextUtils.isEmpty(relativePath)) {
                            absolutePath = LocalPathUtils.getAbsolutePath(relativePath);
                        } else {
                            absolutePath = toLocalAbsolutePath(content.fileFullPath);
                        }
                        realmContent.setFileFullPath(absolutePath);
                        usedContentPaths.add(new File(absolutePath).getAbsolutePath());
                        realmContent.setContentType(content.contentType);
                        realmContent.setPlayMinute(content.playMinute);
                        realmContent.setPlaySecond(content.playSecond);
                                realmContent.setValid(content.valid);
                                realmContent.setFileExist(content.fileExist);
                                realmContent.setScrollSpeedSec(content.scrollSpeedSec);
                                contents.add(realmContent);
                            }
                        }
                        realmElement.setContents(contents);
                        realmElements.add(realmElement);
                    }
                }
                realmPage.setElements(realmElements);
            }
        });
        realm.close();
        cleanupUnusedContents(usedContentPaths);
    }

    private void cleanupUnusedContents(Set<String> usedPaths) {
        File contentsRoot = new File(LocalPathUtils.getContentsDirPath());
        if (!contentsRoot.exists() || !contentsRoot.isDirectory()) {
            return;
        }
        Set<String> normalized = new HashSet<>();
        if (usedPaths != null) {
            for (String path : usedPaths) {
                if (TextUtils.isEmpty(path)) {
                    continue;
                }
                normalized.add(new File(path).getAbsolutePath());
            }
        }
        Deque<File> stack = new ArrayDeque<>();
        stack.push(contentsRoot);
        while (!stack.isEmpty()) {
            File current = stack.pop();
            File[] children = current.listFiles();
            if (children == null) {
                continue;
            }
            for (File child : children) {
                if (child.isDirectory()) {
                    stack.push(child);
                    continue;
                }
                String absPath = child.getAbsolutePath();
                if (!normalized.contains(absPath)) {
                    child.delete();
                }
            }
        }
        deleteEmptyDirectories(contentsRoot);
    }

    private void deleteEmptyDirectories(File dir) {
        if (dir == null || !dir.exists() || !dir.isDirectory()) {
            return;
        }
        File[] children = dir.listFiles();
        if (children == null) {
            return;
        }
        for (File child : children) {
            if (!child.isDirectory()) {
                continue;
            }
            deleteEmptyDirectories(child);
            File[] remaining = child.listFiles();
            if (remaining != null && remaining.length == 0) {
                child.delete();
            }
        }
    }

    private void cleanupTempDownloads(List<UpdateQueueContract.DownloadContentEntry> entries,
                                      MoveResult moveResult) {
        if (entries == null || moveResult == null || !moveResult.allMoved) {
            return;
        }
        Set<String> moved = (moveResult == null) ? null : moveResult.movedRelativePaths;
        boolean deleteAll = moved == null || moved.isEmpty();
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            String relative = entry.remotePath;
            if (!deleteAll && (TextUtils.isEmpty(relative) || !moved.contains(relative))) {
                continue;
            }
            try {
                String tempPath = LocalPathUtils.getTempPath(relative);
                File tempFile = new File(tempPath);
                if (tempFile.exists()) {
                    tempFile.delete();
                }
            } catch (Exception ignore) {
            }
        }
    }

    private MoveResult moveStagedDownloadsToFinal(List<UpdateQueueContract.DownloadContentEntry> entries) {
        MoveResult result = new MoveResult();
        if (entries == null) {
            return result;
        }
        for (UpdateQueueContract.DownloadContentEntry entry : entries) {
            if (entry == null || TextUtils.isEmpty(entry.fileName)) {
                continue;
            }
            try {
                String relative = entry.remotePath;
                if (TextUtils.isEmpty(relative)) {
                    continue;
                }
                String tempPath = LocalPathUtils.getTempPath(relative);
                String finalPath = LocalPathUtils.getAbsolutePath(relative);
                File tempFile = new File(tempPath);
                File finalFile = new File(finalPath);
                if (!tempFile.exists()) {
                    if (finalFile.exists()) {
                        result.movedRelativePaths.add(relative);
                        continue;
                    }
                    result.allMoved = false;
                    continue;
                }
                boolean moved = false;
                try {
                    if (finalFile.exists() && !finalFile.delete()) {
                        result.allMoved = false;
                        continue;
                    }
                    ensureParentDir(finalFile.getAbsolutePath());
                    moved = tempFile.renameTo(finalFile);
                    if (!moved) {
                        try (java.io.InputStream in = new java.io.FileInputStream(tempFile);
                             java.io.OutputStream out = new java.io.FileOutputStream(finalFile)) {
                            byte[] buf = new byte[8192];
                            int read;
                            while ((read = in.read(buf)) > 0) {
                                out.write(buf, 0, read);
                            }
                            moved = true;
                        }
                    }
                } catch (Exception moveEx) {
                    moved = false;
                }
                if (moved) {
                    tempFile.delete();
                    result.movedRelativePaths.add(relative);
                    AndoWSignage.act.mScanner.notify(finalFile.getAbsolutePath(), false);
                    AndoWSignage.act.mScanner.notify(tempFile.getAbsolutePath(), true);
                } else {
                    result.allMoved = false;
                }
            } catch (Exception ignore) { }
        }
        return result;
    }

    private void ensureParentDir(String absolutePath) {
        if (TextUtils.isEmpty(absolutePath)) {
            return;
        }
        try {
            File parent = new File(absolutePath).getParentFile();
            if (parent != null && !parent.exists()) {
                parent.mkdirs();
            }
        } catch (Exception ignore) {
        }
    }

    private void applyDay(RealmWeeklySchedule schedule,
                          String day,
                          RethinkModels.DayScheduleRecord record) {
        if (record == null) {
            schedule.setSchedule(day, 0, 0, 0, 0);
            schedule.setOnAir(day, true);
            return;
        }
        schedule.setSchedule(day,
                record.getStartHour(),
                record.getStartMinute(),
                record.getEndHour(),
                record.getEndMinute());
        schedule.setOnAir(day, record.isOnAir());
    }

    private String toLocalAbsolutePath(String relativePath) {
        String candidate = relativePath;
        if (TextUtils.isEmpty(candidate)) {
            return LocalPathUtils.getContentsDirPath();
        }
        return LocalPathUtils.getAbsolutePath(candidate);
    }

    private Map<String, String> indexDownloadPaths(List<UpdateQueueContract.DownloadContentEntry> downloads) {
        Map<String, String> map = new HashMap<>();
        if (downloads == null) {
            return map;
        }
        for (UpdateQueueContract.DownloadContentEntry entry : downloads) {
            if (entry == null || TextUtils.isEmpty(entry.contentUid) || TextUtils.isEmpty(entry.remotePath)) {
                continue;
            }
            map.put(entry.contentUid, entry.remotePath);
        }
        return map;
    }

    private ApplyBackup createApplyBackup() {
        Realm realm = Realm.getDefaultInstance();
        try {
            ApplyBackup backup = new ApplyBackup();
            backup.pages = realm.copyFromRealm(realm.where(RealmPage.class).findAll());
            backup.elements = realm.copyFromRealm(realm.where(RealmElement.class).findAll());
            backup.contents = realm.copyFromRealm(realm.where(RealmContent.class).findAll());
            backup.welcomes = realm.copyFromRealm(realm.where(RealmWelcome.class).findAll());
            RealmPlayer player = realm.where(RealmPlayer.class).findFirst();
            if (player != null) {
                backup.player = realm.copyFromRealm(player);
            }
            return backup;
        } finally {
            realm.close();
        }
    }

    private void restoreApplyBackup(ApplyBackup backup) {
        if (backup == null) {
            return;
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            realm.executeTransaction(r -> {
                r.delete(RealmPage.class);
                r.delete(RealmElement.class);
                r.delete(RealmContent.class);
                r.delete(RealmWelcome.class);
                r.delete(RealmPlayer.class);
                if (backup.player != null) {
                    r.insertOrUpdate(backup.player);
                }
                if (backup.pages != null && !backup.pages.isEmpty()) {
                    r.insertOrUpdate(backup.pages);
                }
                if (backup.elements != null && !backup.elements.isEmpty()) {
                    r.insertOrUpdate(backup.elements);
                }
                if (backup.contents != null && !backup.contents.isEmpty()) {
                    r.insertOrUpdate(backup.contents);
                }
                if (backup.welcomes != null && !backup.welcomes.isEmpty()) {
                    r.insertOrUpdate(backup.welcomes);
                }
            });
        } finally {
            realm.close();
        }
    }

    private static final class ApplyBackup {
        RealmPlayer player;
        java.util.List<RealmPage> pages;
        java.util.List<RealmElement> elements;
        java.util.List<RealmContent> contents;
        java.util.List<RealmWelcome> welcomes;
    }

    private static final class MoveResult {
        boolean allMoved = true;
        Set<String> movedRelativePaths = new HashSet<>();
    }
}

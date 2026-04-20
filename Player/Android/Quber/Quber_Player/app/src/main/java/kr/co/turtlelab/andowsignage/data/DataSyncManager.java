package kr.co.turtlelab.andowsignage.data;

import android.text.TextUtils;

import com.google.gson.Gson;

import java.io.File;
import java.net.InetSocketAddress;
import java.net.Socket;
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
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import io.realm.Realm;
import io.realm.RealmList;
import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmContent;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.data.realm.RealmSpecialScheduleCache;
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
import kr.co.turtlelab.andowsignage.data.update.UpdatePayloadModels;
import kr.co.turtlelab.andowsignage.dataproviders.UpdateQueueProvider;
import kr.co.turtlelab.andowsignage.services.HeartbeatService;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

/**
 * RethinkDB -> Realm 으로 데이터를 동기화한다.
 */
public class DataSyncManager {
    private static final String TAG = "DataSyncManager";
    private static final int RETHINK_PORT = 28015;
    private static final int CONNECT_TIMEOUT_MS = 1000;
    private static final long REMOTE_RECONNECT_SKIP_MS = 5000L;
    private static final ExecutorService REMOTE_IO_EXECUTOR = Executors.newSingleThreadExecutor();
    private static volatile long remoteBlockedUntilMs = 0L;

    private final RethinkDbClient rethinkClient = RethinkDbClient.getInstance();
    private final Gson gson = new Gson();
    private final UpdateQueueProcessor queueProcessor;

    public DataSyncManager() {
        queueProcessor = new UpdateQueueProcessor(this::applyQueueEntry);
    }

    public void updateEndpoint(String host) {
        rethinkClient.updateHost(host);
        if (queueProcessor != null) {
            queueProcessor.updateHost(host);
        }
        String selfId = rethinkClient.getStoredPlayerGuid();
        if (TextUtils.isEmpty(selfId)) {
            selfId = rethinkClient.ensurePlayerGuid();
        }
        if (!TextUtils.isEmpty(selfId) && queueProcessor != null) {
            queueProcessor.releasePlayerLeaseByPlayer(selfId);
        }
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


    public boolean syncSpecialSchedule(RethinkModels.PlayerInfoRecord player) {
        if (player == null || TextUtils.isEmpty(player.getPlayerName())) {
            return false;
        }

        List<RethinkModels.SpecialScheduleRecord> records = rethinkClient.fetchSpecialSchedules(player.getPlayerName());
        UpdatePayloadModels.ScheduleUpdatePayload payload = buildSpecialSchedulePayloadFromRemote(player, records);
        if (payload == null) {
            return false;
        }

        return !TextUtils.isEmpty(enqueueScheduleUpdate(payload));
    }


    private UpdatePayloadModels.ScheduleUpdatePayload buildSpecialSchedulePayloadFromRemote(
            RethinkModels.PlayerInfoRecord player,
            List<RethinkModels.SpecialScheduleRecord> records) {
        if (player == null) {
            return null;
        }

        UpdatePayloadModels.ScheduleUpdatePayload payload = new UpdatePayloadModels.ScheduleUpdatePayload();
        payload.PlayerId = TextUtils.isEmpty(player.getGuid()) ? "" : player.getGuid();
        payload.PlayerName = TextUtils.isEmpty(player.getPlayerName()) ? "" : player.getPlayerName();
        payload.GeneratedAt = new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm:ss", java.util.Locale.KOREA)
                .format(new java.util.Date());
        payload.SpecialSchedules = new ArrayList<>();
        payload.Playlists = new ArrayList<>();

        List<String> playlistNames = new ArrayList<>();
        if (records != null) {
            for (RethinkModels.SpecialScheduleRecord record : records) {
                UpdatePayloadModels.SpecialSchedulePayload schedule = mapSpecialSchedule(record);
                if (schedule == null) {
                    continue;
                }
                payload.SpecialSchedules.add(schedule);
                addUniquePlaylistName(playlistNames, schedule.PageListName);
            }
        }

        for (String playlistName : playlistNames) {
            UpdatePayloadModels.SchedulePlaylistPayload playlist = buildSchedulePlaylistPayload(player, playlistName);
            if (playlist != null) {
                payload.Playlists.add(playlist);
            }
        }

        return payload;
    }

    private UpdatePayloadModels.SchedulePlaylistPayload buildSchedulePlaylistPayload(
            RethinkModels.PlayerInfoRecord player,
            String playlistName) {
        if (TextUtils.isEmpty(playlistName)) {
            return null;
        }
        RethinkModels.PageListRecord pageList = rethinkClient.fetchPageList(playlistName);
        if (pageList == null || pageList.getPages() == null || pageList.getPages().isEmpty()) {
            return null;
        }
        List<RethinkModels.PageInfoRecord> pages = rethinkClient.fetchPagesByIds(pageList.getPages());
        if (pages == null || pages.isEmpty()) {
            return null;
        }
        UpdatePayloadModels.UpdatePayload updatePayload = buildUpdatePayloadFromRecords(player, pageList, pages);
        if (updatePayload == null || updatePayload.PageList == null || updatePayload.Pages == null || updatePayload.Pages.isEmpty()) {
            return null;
        }

        UpdatePayloadModels.SchedulePlaylistPayload playlist = new UpdatePayloadModels.SchedulePlaylistPayload();
        playlist.PlaylistName = playlistName;
        playlist.PageList = updatePayload.PageList;
        playlist.Pages = updatePayload.Pages;
        playlist.Contract = updatePayload.Contract;
        return playlist;
    }

    private UpdatePayloadModels.SpecialSchedulePayload mapSpecialSchedule(RethinkModels.SpecialScheduleRecord record) {
        if (record == null) {
            return null;
        }
        UpdatePayloadModels.SpecialSchedulePayload payload = new UpdatePayloadModels.SpecialSchedulePayload();
        payload.Id = TextUtils.isEmpty(record.getId()) ? "" : record.getId();
        payload.PageListName = TextUtils.isEmpty(record.getPageListName()) ? "" : record.getPageListName();
        payload.DayOfWeek1 = record.isDayOfWeek1();
        payload.DayOfWeek2 = record.isDayOfWeek2();
        payload.DayOfWeek3 = record.isDayOfWeek3();
        payload.DayOfWeek4 = record.isDayOfWeek4();
        payload.DayOfWeek5 = record.isDayOfWeek5();
        payload.DayOfWeek6 = record.isDayOfWeek6();
        payload.DayOfWeek7 = record.isDayOfWeek7();
        payload.IsPeriodEnable = record.isPeriodEnable();
        payload.DisplayStartH = record.getDisplayStartH();
        payload.DisplayStartM = record.getDisplayStartM();
        payload.DisplayEndH = record.getDisplayEndH();
        payload.DisplayEndM = record.getDisplayEndM();
        payload.PeriodStartYear = record.getPeriodStartYear();
        payload.PeriodStartMonth = record.getPeriodStartMonth();
        payload.PeriodStartDay = record.getPeriodStartDay();
        payload.PeriodEndYear = record.getPeriodEndYear();
        payload.PeriodEndMonth = record.getPeriodEndMonth();
        payload.PeriodEndDay = record.getPeriodEndDay();
        return payload;
    }

    private void addUniquePlaylistName(List<String> playlistNames, String playlistName) {
        if (playlistNames == null || TextUtils.isEmpty(playlistName)) {
            return;
        }
        for (String existing : playlistNames) {
            if (playlistName.equalsIgnoreCase(existing)) {
                return;
            }
        }
        playlistNames.add(playlistName);
    }

    public boolean applyWeeklyScheduleRecord(String playerKey, RethinkModels.WeeklyScheduleRecord record) {
        if (TextUtils.isEmpty(playerKey)) {
            return false;
        }
        storeWeeklySchedule(playerKey, record);
        return true;
    }

    public boolean applyWeeklySchedulePayload(String playerKey, UpdatePayloadModels.WeeklyPlayScheduleInfo weekly) {
        if (TextUtils.isEmpty(playerKey) || weekly == null) {
            return false;
        }
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmWeeklySchedule schedule = r.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", playerKey)
                    .findFirst();
            if (schedule == null) {
                schedule = r.createObject(RealmWeeklySchedule.class, playerKey);
            }
            applyDayPayload(schedule, "MON", weekly.MonSch);
            applyDayPayload(schedule, "TUE", weekly.TueSch);
            applyDayPayload(schedule, "WED", weekly.WedSch);
            applyDayPayload(schedule, "THU", weekly.ThuSch);
            applyDayPayload(schedule, "FRI", weekly.FriSch);
            applyDayPayload(schedule, "SAT", weekly.SatSch);
            applyDayPayload(schedule, "SUN", weekly.SunSch);
        });
        realm.close();
        return true;
    }

    public boolean saveScheduleCache(String cacheId, UpdatePayloadModels.ScheduleUpdatePayload payload) {
        if (TextUtils.isEmpty(cacheId) || payload == null) {
            return false;
        }
        String updatedAt = TextUtils.isEmpty(payload.GeneratedAt)
                ? new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm:ss", java.util.Locale.KOREA)
                        .format(new java.util.Date())
                : payload.GeneratedAt;
        Realm realm = Realm.getDefaultInstance();
        try {
            realm.executeTransaction(r -> {
                RealmSpecialScheduleCache cache = r.where(RealmSpecialScheduleCache.class)
                        .equalTo("id", cacheId)
                        .findFirst();
                if (cache == null) {
                    cache = r.createObject(RealmSpecialScheduleCache.class, cacheId);
                }
                cache.setPlayerId(payload.PlayerId == null ? "" : payload.PlayerId);
                cache.setPlayerName(payload.PlayerName == null ? "" : payload.PlayerName);
                cache.setUpdatedAt(updatedAt);
                cache.setSchedulesJson(gson.toJson(payload.SpecialSchedules));
                cache.setPlaylistsJson(gson.toJson(payload.Playlists));
            });
            return true;
        } finally {
            realm.close();
        }
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
        return enqueuePlaylist(player, pageList, pages, false);
    }

    public long enqueuePayloadUpdate(UpdatePayloadModels.UpdatePayload payload, boolean isScheduleQueue) {
        return enqueuePayloadUpdate(payload, isScheduleQueue, true);
    }

    private long enqueuePayloadUpdate(UpdatePayloadModels.UpdatePayload payload,
                                      boolean isScheduleQueue,
                                      boolean scheduleProcessor) {
        if (payload == null || payload.PageList == null || payload.Pages == null || payload.Pages.isEmpty()) {
            return -1;
        }
        UpdateQueueContract.PlaylistPayload contract = buildContractPayload(payload);
        if (contract == null || TextUtils.isEmpty(contract.playlistName)) {
            return -1;
        }
        String payloadJson = UpdatePayloadModels.UpdatePayloadCodec.encode(payload);
        if (TextUtils.isEmpty(payloadJson)) {
            return -1;
        }
        String downloadJson = gson.toJson(buildDownloadEntries(payload));
        if (isDuplicateQueue(contract, payloadJson, isScheduleQueue)) {
            return -1;
        }
        RealmUpdateQueue enqueued = UpdateQueueHelper.enqueue(UpdateQueueContract.Type.PLAYLIST,
                payloadJson,
                downloadJson,
                0,
                isScheduleQueue);
        if (enqueued != null) {
            Long createdTicks = UpdateQueueHelper.toDotNetLocalTicks(enqueued.getCreatedAt());
            String externalId = TextUtils.isEmpty(enqueued.getExternalId())
                    ? String.valueOf(enqueued.getId())
                    : enqueued.getExternalId();
            rethinkClient.upsertCommandHistoryForQueue(contract.playerId,
                    contract.playerName,
                    "updatelist",
                    externalId,
                    "queued",
                    null,
                    null,
                    null,
                    createdTicks);
        }
        if (scheduleProcessor) {
            queueProcessor.schedule();
        }
        return enqueued != null ? enqueued.getId() : -1;
    }

    public String enqueueScheduleUpdate(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return "";
        }
        String cacheId = resolveScheduleCacheId(schedule);
        if (TextUtils.isEmpty(cacheId)) {
            return "";
        }

        if (schedule.Playlists != null) {
            for (UpdatePayloadModels.SchedulePlaylistPayload playlist : schedule.Playlists) {
                if (playlist == null || playlist.PageList == null || playlist.Pages == null || playlist.Pages.isEmpty()) {
                    continue;
                }
                UpdatePayloadModels.UpdatePayload payload = new UpdatePayloadModels.UpdatePayload();
                payload.PageList = playlist.PageList;
                payload.Pages = playlist.Pages;
                payload.Contract = playlist.Contract;
                long queueId = enqueuePayloadUpdate(payload, true, false);
                if (queueId <= 0 && !hasActivePayloadQueue(payload, true)) {
                    return "";
                }
            }
        }

        long scheduleQueueId = enqueueScheduleApplyQueue(schedule, false);
        if (scheduleQueueId <= 0) {
            return "";
        }

        queueProcessor.schedule();
        return getQueueExternalId(scheduleQueueId);
    }

    private String resolveScheduleCacheId(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return "";
        }
        if (!TextUtils.isEmpty(schedule.PlayerId)) {
            return schedule.PlayerId;
        }
        if (!TextUtils.isEmpty(schedule.PlayerName)) {
            return schedule.PlayerName;
        }
        if (schedule.WeeklySchedule != null) {
            if (!TextUtils.isEmpty(schedule.WeeklySchedule.PlayerID)) {
                return schedule.WeeklySchedule.PlayerID;
            }
            if (!TextUtils.isEmpty(schedule.WeeklySchedule.PlayerName)) {
                return schedule.WeeklySchedule.PlayerName;
            }
        }
        String stored = rethinkClient.getStoredPlayerGuid();
        if (TextUtils.isEmpty(stored)) {
            stored = rethinkClient.ensurePlayerGuid();
        }
        return TextUtils.isEmpty(stored) ? "" : stored;
    }

    private String resolveSchedulePlayerId(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return "";
        }
        if (!TextUtils.isEmpty(schedule.PlayerId)) {
            return schedule.PlayerId;
        }
        if (schedule.WeeklySchedule != null && !TextUtils.isEmpty(schedule.WeeklySchedule.PlayerID)) {
            return schedule.WeeklySchedule.PlayerID;
        }
        String stored = rethinkClient.getStoredPlayerGuid();
        if (TextUtils.isEmpty(stored)) {
            stored = rethinkClient.ensurePlayerGuid();
        }
        return TextUtils.isEmpty(stored) ? "" : stored;
    }

    private String resolveSchedulePlayerName(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return "";
        }
        if (!TextUtils.isEmpty(schedule.PlayerName)) {
            return schedule.PlayerName;
        }
        if (schedule.WeeklySchedule != null && !TextUtils.isEmpty(schedule.WeeklySchedule.PlayerName)) {
            return schedule.WeeklySchedule.PlayerName;
        }
        String storedPlayerName = rethinkClient.getStoredPlayerName();
        return TextUtils.isEmpty(storedPlayerName)
                ? (TextUtils.isEmpty(AndoWSignageApp.PLAYER_ID) ? "" : AndoWSignageApp.PLAYER_ID)
                : storedPlayerName;
    }

    private String buildSchedulePayloadJson(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return "";
        }
        UpdatePayloadModels.UpdatePayload payload = new UpdatePayloadModels.UpdatePayload();
        payload.Schedule = schedule;
        return UpdatePayloadModels.UpdatePayloadCodec.encode(payload);
    }

    private long enqueueScheduleApplyQueue(UpdatePayloadModels.ScheduleUpdatePayload schedule,
                                           boolean allowDuplicate) {
        String payloadJson = buildSchedulePayloadJson(schedule);
        if (TextUtils.isEmpty(payloadJson)) {
            return -1;
        }
        if (!allowDuplicate && hasActiveScheduleApplyQueue(payloadJson)) {
            return findActiveScheduleApplyQueueId(payloadJson);
        }
        String downloadJson = gson.toJson(new ArrayList<UpdateQueueContract.DownloadEntry>());
        RealmUpdateQueue enqueued = UpdateQueueHelper.enqueue(UpdateQueueContract.Type.SCHEDULE,
                payloadJson,
                downloadJson,
                0,
                true);
        if (enqueued != null) {
            Long createdTicks = UpdateQueueHelper.toDotNetLocalTicks(enqueued.getCreatedAt());
            String externalId = TextUtils.isEmpty(enqueued.getExternalId())
                    ? String.valueOf(enqueued.getId())
                    : enqueued.getExternalId();
            rethinkClient.upsertCommandHistoryForQueue(resolveSchedulePlayerId(schedule),
                    resolveSchedulePlayerName(schedule),
                    "updateschedule",
                    externalId,
                    "queued",
                    null,
                    null,
                    null,
                    createdTicks);
        }
        return enqueued != null ? enqueued.getId() : -1;
    }

    private boolean hasActivePayloadQueue(UpdatePayloadModels.UpdatePayload payload, boolean isScheduleQueue) {
        UpdateQueueContract.PlaylistPayload contract = buildContractPayload(payload);
        if (contract == null || TextUtils.isEmpty(contract.playlistName)) {
            return false;
        }
        String payloadJson = UpdatePayloadModels.UpdatePayloadCodec.encode(payload);
        if (TextUtils.isEmpty(payloadJson)) {
            return false;
        }
        return isDuplicateQueue(contract, payloadJson, isScheduleQueue);
    }

    private boolean hasActiveScheduleApplyQueue(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        String payloadJson = buildSchedulePayloadJson(schedule);
        return !TextUtils.isEmpty(payloadJson) && hasActiveScheduleApplyQueue(payloadJson);
    }

    private boolean hasActiveScheduleApplyQueue(String payloadJson) {
        return findActiveScheduleApplyQueueId(payloadJson) > 0;
    }

    private long findActiveScheduleApplyQueueId(String payloadJson) {
        if (TextUtils.isEmpty(payloadJson)) {
            return -1;
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            List<RealmUpdateQueue> list = realm.where(RealmUpdateQueue.class)
                    .equalTo("type", UpdateQueueContract.Type.SCHEDULE)
                    .findAll();
            if (list == null || list.isEmpty()) {
                return -1;
            }
            for (RealmUpdateQueue queue : list) {
                if (queue == null || !isActiveStatus(queue.getStatus())) {
                    continue;
                }
                if (TextUtils.equals(payloadJson, queue.getPayloadJson())) {
                    return queue.getId();
                }
            }
            return -1;
        } finally {
            realm.close();
        }
    }

    private String getQueueExternalId(long queueId) {
        if (queueId <= 0) {
            return "";
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = realm.where(RealmUpdateQueue.class)
                    .equalTo("id", queueId)
                    .findFirst();
            if (queue == null) {
                return "";
            }
            return TextUtils.isEmpty(queue.getExternalId())
                    ? String.valueOf(queue.getId())
                    : queue.getExternalId();
        } finally {
            realm.close();
        }
    }

    public boolean processQueueImmediate(long queueId, boolean ignoreLease) {
        if (queueProcessor == null || queueId <= 0) {
            return false;
        }
        return queueProcessor.processImmediate(queueId, ignoreLease);
    }

    public void releaseActiveLease() {
        if (queueProcessor != null) {
            queueProcessor.releaseLeaseIfAny();
        }
    }

    public void releasePlayerLease(String playerId) {
        if (queueProcessor != null) {
            queueProcessor.releasePlayerLeaseByPlayer(playerId);
        }
    }

    private void releasePlayerLeaseAsync(String playerId) {
        if (TextUtils.isEmpty(playerId)) {
            return;
        }
        runRemoteAsync("releasePlayerLease", () -> {
            if (queueProcessor != null) {
                queueProcessor.releasePlayerLeaseByPlayer(playerId);
            }
        });
    }

    private void clearCommandAsync(String playerId) {
        if (TextUtils.isEmpty(playerId)) {
            return;
        }
        runRemoteAsync("clearCommand", () -> rethinkClient.clearCommand(playerId));
    }

    private void deleteQueueRecordAsync(String externalId, String playerId) {
        if (TextUtils.isEmpty(externalId)) {
            return;
        }
        runRemoteAsync("deleteQueueRecord", () -> {
            if (!TextUtils.isEmpty(playerId)) {
                rethinkClient.deleteQueueRecord(externalId, playerId);
            } else {
                rethinkClient.deleteQueueRecord(externalId);
            }
        });
    }

    private void runRemoteAsync(String operation, Runnable action) {
        if (action == null) {
            return;
        }
        if (!canAttemptRemoteNow()) {
            return;
        }
        String host = resolveRethinkHostForRemote();
        if (TextUtils.isEmpty(host) || !canReachRethink(host)) {
            blockRemoteTemporarily();
            android.util.Log.w(TAG, operation + ": skipped. host unreachable=" + host);
            return;
        }
        rethinkClient.updateHost(host);
        REMOTE_IO_EXECUTOR.execute(() -> {
            try {
                action.run();
            } catch (Exception ex) {
                blockRemoteTemporarily();
                android.util.Log.w(TAG, operation + ": skipped due to communication failure", ex);
            }
        });
    }

    private static boolean canAttemptRemoteNow() {
        return System.currentTimeMillis() >= remoteBlockedUntilMs;
    }

    private static void blockRemoteTemporarily() {
        remoteBlockedUntilMs = System.currentTimeMillis() + REMOTE_RECONNECT_SKIP_MS;
    }

    private String resolveRethinkHostForRemote() {
        String dataServerIp = kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider.getDataServerIp();
        if (!TextUtils.isEmpty(dataServerIp)) {
            return NetworkUtils.extractHost(dataServerIp);
        }
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return NetworkUtils.extractHost(AndoWSignageApp.MANUAL_IP);
        }
        return NetworkUtils.extractHost(AndoWSignageApp.MANAGER_IP);
    }

    private boolean canReachRethink(String host) {
        String resolvedHost = NetworkUtils.extractHost(host);
        if (TextUtils.isEmpty(resolvedHost)) {
            return false;
        }
        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress(resolvedHost, RETHINK_PORT), CONNECT_TIMEOUT_MS);
            return true;
        } catch (Exception ex) {
            return false;
        }
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
                realmPage.setCanvasWidth(page.getCanvasWidth() > 0 ? page.getCanvasWidth() : (page.isLandscape() ? 1920 : 1080));
                realmPage.setCanvasHeight(page.getCanvasHeight() > 0 ? page.getCanvasHeight() : (page.isLandscape() ? 1080 : 1920));

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
                                realmElement.setMuted(element.isMuted());

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

    private UpdatePayloadModels.UpdatePayload buildUpdatePayloadFromRecords(RethinkModels.PlayerInfoRecord player,
                                                                           RethinkModels.PageListRecord pageList,
                                                                           List<RethinkModels.PageInfoRecord> pages) {
        if (player == null || pageList == null || pages == null) {
            return null;
        }
        UpdatePayloadModels.UpdatePayload payload = new UpdatePayloadModels.UpdatePayload();
        UpdatePayloadModels.PageListInfoClass list = new UpdatePayloadModels.PageListInfoClass();
        list.Id = pageList.getId();
        list.PLI_PageListName = pageList.getName();
        list.PLI_PageDirection = pageList.getDirection();
        list.PLI_Pages = pageList.getPages() == null ? new ArrayList<>() : new ArrayList<>(pageList.getPages());
        payload.PageList = list;

        List<RethinkModels.PageInfoRecord> orderedPages = new ArrayList<>();
        if (pageList.getPages() != null && !pageList.getPages().isEmpty()) {
            for (String pageId : pageList.getPages()) {
                if (TextUtils.isEmpty(pageId)) {
                    continue;
                }
                for (RethinkModels.PageInfoRecord page : pages) {
                    if (page != null && pageId.equals(page.getGuid())) {
                        orderedPages.add(page);
                        break;
                    }
                }
            }
        }
        if (orderedPages.isEmpty()) {
            orderedPages.addAll(pages);
        }

        payload.Pages = new ArrayList<>();
        for (RethinkModels.PageInfoRecord page : orderedPages) {
            if (page == null) {
                continue;
            }
            UpdatePayloadModels.PageInfoClass pageEntry = new UpdatePayloadModels.PageInfoClass();
            pageEntry.Id = page.getGuid();
            pageEntry.PIC_GUID = page.getGuid();
            pageEntry.PIC_PageName = page.getPageName();
            pageEntry.PIC_PlaytimeHour = page.getPlayHour();
            pageEntry.PIC_PlaytimeMinute = page.getPlayMinute();
            pageEntry.PIC_PlaytimeSecond = page.getPlaySecond();
            pageEntry.PIC_Volume = page.getVolume();
            pageEntry.PIC_IsLandscape = page.isLandscape();
            pageEntry.PIC_Elements = new ArrayList<>();

            if (page.getElements() != null) {
                for (RethinkModels.ElementInfoRecord element : page.getElements()) {
                    if (element == null) {
                        continue;
                    }
                    UpdatePayloadModels.ElementInfoClass elementEntry = new UpdatePayloadModels.ElementInfoClass();
                    elementEntry.EIF_Name = element.getName();
                    elementEntry.EIF_Type = element.getType();
                    elementEntry.EIF_Width = element.getWidth();
                    elementEntry.EIF_Height = element.getHeight();
                    elementEntry.EIF_PosLeft = element.getPosLeft();
                    elementEntry.EIF_PosTop = element.getPosTop();
                    elementEntry.EIF_ZIndex = element.getzIndex();
                    elementEntry.EIF_IsMuted = element.isMuted();
                    elementEntry.EIF_DataFileName = element.getDataFileName();
                    elementEntry.EIF_DataFileFullPath = element.getDataFileFullPath();
                    elementEntry.EIF_ContentsInfoClassList = new ArrayList<>();

                    List<RethinkModels.ContentInfoRecord> contents = element.getContents();
                    if (contents != null) {
                        for (RethinkModels.ContentInfoRecord content : contents) {
                            if (content == null) {
                                continue;
                            }
                            UpdatePayloadModels.ContentsInfoClass contentEntry = new UpdatePayloadModels.ContentsInfoClass();
                            contentEntry.CIF_FileName = content.getFileName();
                            contentEntry.CIF_FileFullPath = content.getFileFullPath();
                            contentEntry.CIF_RelativePath = content.getRelativePath();
                            contentEntry.CIF_StrGUID = content.getGuid();
                            contentEntry.CIF_PlayMinute = content.getPlayMinute();
                            contentEntry.CIF_PlaySec = content.getPlaySecond();
                            contentEntry.CIF_ContentType = content.getContentType();
                            contentEntry.CIF_ValidTime = content.isValidTime();
                            contentEntry.CIF_FileExist = content.isFileExist();
                            contentEntry.CIF_ScrollTextSpeedSec = content.getScrollSpeedSec();
                            contentEntry.CIF_FileSize = content.getFileSize();
                            contentEntry.CIF_FileHash = content.getFileHash();
                            elementEntry.EIF_ContentsInfoClassList.add(contentEntry);
                        }
                    }
                    pageEntry.PIC_Elements.add(elementEntry);
                }
            }
            payload.Pages.add(pageEntry);
        }

        payload.Contract = buildContractPayloadFromRecords(player, pageList, orderedPages);
        return payload;
    }

    private UpdatePayloadModels.ContractPlaylistPayload buildContractPayloadFromRecords(RethinkModels.PlayerInfoRecord player,
                                                                                       RethinkModels.PageListRecord pageList,
                                                                                       List<RethinkModels.PageInfoRecord> pages) {
        UpdatePayloadModels.ContractPlaylistPayload payload = new UpdatePayloadModels.ContractPlaylistPayload();
        payload.PlayerId = player == null ? "" : player.getGuid();
        payload.PlayerName = player == null ? "" : player.getPlayerName();
        payload.PlayerLandscape = player != null && player.isLandscape();
        String listName = pageList == null ? "" : pageList.getName();
        payload.PlaylistId = listName;
        payload.PlaylistName = listName;

        List<String> orderedIds = pageList == null ? new ArrayList<>() : pageList.getPages();
        if (pages != null) {
            for (int i = 0; i < pages.size(); i++) {
                RethinkModels.PageInfoRecord page = pages.get(i);
                if (page == null) {
                    continue;
                }
                UpdatePayloadModels.ContractPagePayload pageEntry = new UpdatePayloadModels.ContractPagePayload();
                pageEntry.PageId = page.getGuid();
                pageEntry.PageName = page.getPageName();
                pageEntry.OrderIndex = orderedIds == null ? i : orderedIds.indexOf(page.getGuid());
                if (pageEntry.OrderIndex < 0) {
                    pageEntry.OrderIndex = i;
                }
                pageEntry.PlayHour = page.getPlayHour();
                pageEntry.PlayMinute = page.getPlayMinute();
                pageEntry.PlaySecond = page.getPlaySecond();
                pageEntry.Volume = page.getVolume();
                pageEntry.Landscape = page.isLandscape();
                pageEntry.Elements = new ArrayList<>();

                if (page.getElements() != null) {
                    for (RethinkModels.ElementInfoRecord element : page.getElements()) {
                        if (element == null) {
                            continue;
                        }
                        UpdatePayloadModels.ContractElementPayload elementEntry = new UpdatePayloadModels.ContractElementPayload();
                        elementEntry.ElementId = page.getGuid() + "_" + element.getName();
                        elementEntry.PageId = page.getGuid();
                        elementEntry.Name = element.getName();
                        elementEntry.Type = element.getType();
                        elementEntry.Width = element.getWidth();
                        elementEntry.Height = element.getHeight();
                        elementEntry.PosLeft = element.getPosLeft();
                        elementEntry.PosTop = element.getPosTop();
                        elementEntry.ZIndex = element.getzIndex();
                        elementEntry.Muted = element.isMuted();
                        elementEntry.Contents = new ArrayList<>();

                        List<RethinkModels.ContentInfoRecord> contents = element.getContents();
                        if (contents != null) {
                            for (int idx = 0; idx < contents.size(); idx++) {
                                RethinkModels.ContentInfoRecord content = contents.get(idx);
                                if (content == null) {
                                    continue;
                                }
                                UpdatePayloadModels.ContractContentPayload contentEntry = new UpdatePayloadModels.ContractContentPayload();
                                contentEntry.Uid = elementEntry.ElementId + "_" + idx;
                                contentEntry.ElementId = elementEntry.ElementId;
                                contentEntry.FileName = content.getFileName();
                                contentEntry.FileFullPath = content.getFileFullPath();
                                contentEntry.ContentType = content.getContentType();
                                contentEntry.PlayMinute = content.getPlayMinute();
                                contentEntry.PlaySecond = content.getPlaySecond();
                                contentEntry.Valid = content.isValidTime();
                                contentEntry.ScrollSpeedSec = content.getScrollSpeedSec();
                                contentEntry.RemoteChecksum = TextUtils.isEmpty(content.getFileHash())
                                        ? content.getGuid()
                                        : content.getFileHash();
                                contentEntry.FileSize = content.getFileSize();
                                contentEntry.FileExist = content.isFileExist();
                                elementEntry.Contents.add(contentEntry);
                            }
                        }
                        pageEntry.Elements.add(elementEntry);
                    }
                }
                payload.Pages.add(pageEntry);
            }
        }
        return payload;
    }

    private long enqueuePlaylist(RethinkModels.PlayerInfoRecord player,
                                 RethinkModels.PageListRecord pageList,
                                 List<RethinkModels.PageInfoRecord> pages,
                                 boolean isScheduleQueue) {
        UpdatePayloadModels.UpdatePayload updatePayload = buildUpdatePayloadFromRecords(player, pageList, pages);
        if (updatePayload == null || updatePayload.PageList == null || updatePayload.Pages == null) {
            return -1;
        }
        UpdateQueueContract.PlaylistPayload contract = buildContractPayload(updatePayload);
        if (contract == null || TextUtils.isEmpty(contract.playlistName)) {
            return -1;
        }
        String payloadJson = UpdatePayloadModels.UpdatePayloadCodec.encode(updatePayload);
        if (TextUtils.isEmpty(payloadJson)) {
            return -1;
        }
        String downloadJson = gson.toJson(buildDownloadEntries(updatePayload));
        RealmUpdateQueue enqueued = UpdateQueueHelper.enqueue(UpdateQueueContract.Type.PLAYLIST,
                payloadJson,
                downloadJson,
                0,
                isScheduleQueue);
        if (enqueued != null) {
            Long createdTicks = UpdateQueueHelper.toDotNetLocalTicks(enqueued.getCreatedAt());
            String externalId = TextUtils.isEmpty(enqueued.getExternalId())
                    ? String.valueOf(enqueued.getId())
                    : enqueued.getExternalId();
            rethinkClient.upsertCommandHistoryForQueue(contract.playerId,
                    contract.playerName,
                    "updatelist",
                    externalId,
                    "queued",
                    null,
                    null,
                    null,
                    createdTicks);
        }
        queueProcessor.schedule();
        return enqueued != null ? enqueued.getId() : -1;
    }

    private UpdateQueueContract.PlaylistPayload buildContractPayload(UpdatePayloadModels.UpdatePayload payload) {
        if (payload == null) {
            return null;
        }
        UpdatePayloadModels.ContractPlaylistPayload contract = payload.Contract;
        UpdateQueueContract.PlaylistPayload result = new UpdateQueueContract.PlaylistPayload();
        if (contract != null) {
            result.playerId = contract.PlayerId;
            result.playerName = contract.PlayerName;
            result.playerLandscape = contract.PlayerLandscape;
            result.playlistId = contract.PlaylistId;
            result.playlistName = contract.PlaylistName;
            Map<String, UpdatePayloadModels.PageInfoClass> payloadPages = indexPayloadPages(payload);
            if (contract.Pages != null) {
                for (UpdatePayloadModels.ContractPagePayload page : contract.Pages) {
                    UpdateQueueContract.PagePayload pageEntry = new UpdateQueueContract.PagePayload();
                    pageEntry.pageId = page.PageId;
                    pageEntry.pageName = page.PageName;
                    pageEntry.orderIndex = page.OrderIndex;
                    pageEntry.playHour = page.PlayHour;
                    pageEntry.playMinute = page.PlayMinute;
                    pageEntry.playSecond = page.PlaySecond;
                    pageEntry.volume = page.Volume;
                    pageEntry.landscape = page.Landscape;
                    UpdatePayloadModels.PageInfoClass rawPage = payloadPages.get(page.PageId);
                    pageEntry.canvasWidth = page.CanvasWidth > 0 ? page.CanvasWidth : getCanvasWidth(rawPage, page.Landscape);
                    pageEntry.canvasHeight = page.CanvasHeight > 0 ? page.CanvasHeight : getCanvasHeight(rawPage, page.Landscape);
                    if (page.Elements != null) {
                        for (UpdatePayloadModels.ContractElementPayload element : page.Elements) {
                            UpdateQueueContract.ElementPayload elementEntry = new UpdateQueueContract.ElementPayload();
                            elementEntry.elementId = element.ElementId;
                            elementEntry.pageId = element.PageId;
                            elementEntry.name = element.Name;
                            elementEntry.type = element.Type;
                            elementEntry.width = element.Width;
                            elementEntry.height = element.Height;
                            elementEntry.posLeft = element.PosLeft;
                            elementEntry.posTop = element.PosTop;
                            elementEntry.zIndex = element.ZIndex;
                            elementEntry.muted = element.Muted;
                            if (element.Contents != null) {
                                for (UpdatePayloadModels.ContractContentPayload content : element.Contents) {
                                    UpdateQueueContract.ContentPayload contentEntry = new UpdateQueueContract.ContentPayload();
                                    contentEntry.uid = content.Uid;
                                    contentEntry.elementId = content.ElementId;
                                    contentEntry.fileName = content.FileName;
                                    contentEntry.fileFullPath = content.FileFullPath;
                                    contentEntry.contentType = content.ContentType;
                                    contentEntry.playMinute = content.PlayMinute;
                                    contentEntry.playSecond = content.PlaySecond;
                                    contentEntry.valid = content.Valid;
                                    contentEntry.scrollSpeedSec = content.ScrollSpeedSec;
                                    contentEntry.remoteChecksum = content.RemoteChecksum;
                                    contentEntry.fileSize = content.FileSize;
                                    contentEntry.fileExist = content.FileExist;
                                    elementEntry.contents.add(contentEntry);
                                }
                            }
                            pageEntry.elements.add(elementEntry);
                        }
                    }
                    result.pages.add(pageEntry);
                }
            }
            fillFallbackPlayerInfo(result);
            if (payload.PageList != null && !TextUtils.isEmpty(payload.PageList.PLI_PageListName)) {
                result.playlistId = payload.PageList.PLI_PageListName;
                result.playlistName = payload.PageList.PLI_PageListName;
            }
            return result;
        }

        UpdatePayloadModels.PageListInfoClass pageList = payload.PageList;
        List<UpdatePayloadModels.PageInfoClass> pages = payload.Pages;
        if (pageList == null || pages == null) {
            return null;
        }
        result.playerId = "";
        result.playerName = "";
        result.playerLandscape = false;
        String listName = pageList.PLI_PageListName == null ? "" : pageList.PLI_PageListName;
        result.playlistId = listName;
        result.playlistName = listName;
        List<String> orderedIds = pageList.PLI_Pages == null ? new ArrayList<>() : pageList.PLI_Pages;
        for (int i = 0; i < pages.size(); i++) {
            UpdatePayloadModels.PageInfoClass page = pages.get(i);
            UpdateQueueContract.PagePayload pageEntry = new UpdateQueueContract.PagePayload();
            pageEntry.pageId = page.PIC_GUID;
            pageEntry.pageName = page.PIC_PageName;
            int orderIndex = orderedIds.indexOf(page.PIC_GUID);
            if (orderIndex < 0) {
                orderIndex = i;
            }
            pageEntry.orderIndex = orderIndex;
            pageEntry.playHour = page.PIC_PlaytimeHour;
            pageEntry.playMinute = page.PIC_PlaytimeMinute;
            pageEntry.playSecond = page.PIC_PlaytimeSecond;
            pageEntry.volume = page.PIC_Volume;
            pageEntry.landscape = page.PIC_IsLandscape;
            pageEntry.canvasWidth = getCanvasWidth(page, page.PIC_IsLandscape);
            pageEntry.canvasHeight = getCanvasHeight(page, page.PIC_IsLandscape);
            if (page.PIC_Elements != null) {
                for (UpdatePayloadModels.ElementInfoClass element : page.PIC_Elements) {
                    UpdateQueueContract.ElementPayload elementEntry = new UpdateQueueContract.ElementPayload();
                    String elementId = page.PIC_GUID + "_" + element.EIF_Name;
                    elementEntry.elementId = elementId;
                    elementEntry.pageId = page.PIC_GUID;
                    elementEntry.name = element.EIF_Name;
                    elementEntry.type = element.EIF_Type;
                    elementEntry.width = element.EIF_Width;
                    elementEntry.height = element.EIF_Height;
                    elementEntry.posLeft = element.EIF_PosLeft;
                    elementEntry.posTop = element.EIF_PosTop;
                    elementEntry.zIndex = element.EIF_ZIndex;
                    elementEntry.muted = element.EIF_IsMuted;
                    if (element.EIF_ContentsInfoClassList != null) {
                        for (int idx = 0; idx < element.EIF_ContentsInfoClassList.size(); idx++) {
                            UpdatePayloadModels.ContentsInfoClass content = element.EIF_ContentsInfoClassList.get(idx);
                            UpdateQueueContract.ContentPayload contentEntry = new UpdateQueueContract.ContentPayload();
                            contentEntry.uid = elementId + "_" + idx;
                            contentEntry.elementId = elementId;
                            contentEntry.fileName = content.CIF_FileName;
                            contentEntry.fileFullPath = content.CIF_FileFullPath;
                            contentEntry.contentType = content.CIF_ContentType;
                            contentEntry.playMinute = content.CIF_PlayMinute;
                            contentEntry.playSecond = content.CIF_PlaySec;
                            contentEntry.valid = content.CIF_ValidTime;
                            contentEntry.scrollSpeedSec = content.CIF_ScrollTextSpeedSec;
                            contentEntry.remoteChecksum = TextUtils.isEmpty(content.CIF_FileHash)
                                    ? content.CIF_StrGUID
                                    : content.CIF_FileHash;
                            contentEntry.fileSize = content.CIF_FileSize;
                            contentEntry.fileExist = content.CIF_FileExist;
                            elementEntry.contents.add(contentEntry);
                        }
                    }
                    pageEntry.elements.add(elementEntry);
                }
            }
            result.pages.add(pageEntry);
        }
        fillFallbackPlayerInfo(result);
        return result;
    }

    private void fillFallbackPlayerInfo(UpdateQueueContract.PlaylistPayload payload) {
        if (payload == null) {
            return;
        }
        if (TextUtils.isEmpty(payload.playerId)) {
            String stored = rethinkClient.getStoredPlayerGuid();
            if (TextUtils.isEmpty(stored)) {
                stored = rethinkClient.ensurePlayerGuid();
            }
            payload.playerId = TextUtils.isEmpty(stored) ? "" : stored;
        }
        if (TextUtils.isEmpty(payload.playerName)) {
            String storedPlayerName = rethinkClient.getStoredPlayerName();
            payload.playerName = TextUtils.isEmpty(storedPlayerName)
                    ? (TextUtils.isEmpty(AndoWSignageApp.PLAYER_ID) ? "" : AndoWSignageApp.PLAYER_ID)
                    : storedPlayerName;
        }
    }

    private List<UpdateQueueContract.DownloadEntry> buildDownloadEntries(UpdatePayloadModels.UpdatePayload payload) {
        List<UpdateQueueContract.DownloadEntry> entries = new ArrayList<>();
        if (payload == null || payload.Pages == null) {
            return entries;
        }
        for (UpdatePayloadModels.PageInfoClass page : payload.Pages) {
            if (page == null || page.PIC_Elements == null) {
                continue;
            }
            for (UpdatePayloadModels.ElementInfoClass element : page.PIC_Elements) {
                if (element == null || element.EIF_ContentsInfoClassList == null) {
                    continue;
                }
                for (int idx = 0; idx < element.EIF_ContentsInfoClassList.size(); idx++) {
                    UpdatePayloadModels.ContentsInfoClass content = element.EIF_ContentsInfoClassList.get(idx);
                    if (content == null || TextUtils.isEmpty(content.CIF_FileName)) {
                        continue;
                    }
                    String elementId = page.PIC_GUID + "_" + element.EIF_Name;
                    UpdateQueueContract.DownloadEntry entry = new UpdateQueueContract.DownloadEntry();
                    entry.FileName = content.CIF_FileName;
                    entry.RemotePath = resolveRelativePath(content);
                    entry.SizeBytes = content.CIF_FileSize;
                    entry.Checksum = TextUtils.isEmpty(content.CIF_FileHash)
                            ? content.CIF_StrGUID
                            : content.CIF_FileHash;
                    buildChunks(entry);
                    entries.add(entry);
                }
            }
        }
        return entries;
    }

    private void buildChunks(UpdateQueueContract.DownloadEntry entry) {
        if (entry == null) {
            return;
        }
        if (entry.Chunks == null) {
            entry.Chunks = new ArrayList<>();
        }
        entry.Chunks.clear();
        long size = Math.max(0, entry.SizeBytes);
        long nowTicks = UpdateQueueHelper.toDotNetLocalTicks(System.currentTimeMillis());
        if (size <= 0) {
            UpdateQueueContract.DownloadChunk chunk = new UpdateQueueContract.DownloadChunk();
            chunk.Index = 0;
            chunk.Offset = 0;
            chunk.Length = 0;
            chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
            chunk.DownloadedBytes = 0;
            chunk.LastUpdatedTicks = nowTicks;
            entry.Chunks.add(chunk);
            return;
        }
        final long chunkSize = 4L * 1024L * 1024L;
        int idx = 0;
        long offset = 0;
        while (offset < size) {
            long length = Math.min(chunkSize, size - offset);
            UpdateQueueContract.DownloadChunk chunk = new UpdateQueueContract.DownloadChunk();
            chunk.Index = idx;
            chunk.Offset = offset;
            chunk.Length = length;
            chunk.Status = UpdateQueueContract.ChunkStatus.PENDING;
            chunk.DownloadedBytes = 0;
            chunk.LastUpdatedTicks = nowTicks;
            entry.Chunks.add(chunk);
            offset += length;
            idx++;
        }
    }

    private String resolveRelativePath(UpdatePayloadModels.ContentsInfoClass content) {
        if (content == null) {
            return "";
        }
        if (!TextUtils.isEmpty(content.CIF_RelativePath)) {
            return content.CIF_RelativePath;
        }
        if (!TextUtils.isEmpty(content.CIF_FileFullPath)) {
            String full = content.CIF_FileFullPath.replace("\\", "/");
            String contentsRoot = LocalPathUtils.getContentsDirPath().replace("\\", "/");
            if (full.startsWith(contentsRoot)) {
                String trimmed = full.substring(contentsRoot.length());
                while (trimmed.startsWith("/")) {
                    trimmed = trimmed.substring(1);
                }
                return trimmed;
            }
        }
        return content.CIF_FileName == null ? "" : content.CIF_FileName;
    }

    private String resolveRelativePathFromPayload(UpdateQueueContract.ContentPayload content) {
        if (content == null) {
            return "";
        }
        if (!TextUtils.isEmpty(content.fileFullPath)) {
            String full = content.fileFullPath.replace("\\", "/");
            String contentsRoot = LocalPathUtils.getContentsDirPath().replace("\\", "/");
            if (full.startsWith(contentsRoot)) {
                String trimmed = full.substring(contentsRoot.length());
                while (trimmed.startsWith("/")) {
                    trimmed = trimmed.substring(1);
                }
                return trimmed;
            }
        }
        return content.fileName == null ? "" : content.fileName;
    }

    private boolean isDuplicateQueue(UpdateQueueContract.PlaylistPayload payload, String payloadJson, boolean isScheduleQueue) {
        if (payload == null) {
            return false;
        }
        String playerId = payload.playerId == null ? "" : payload.playerId;
        String playlistId = payload.playlistId == null ? "" : payload.playlistId;
        Realm realm = Realm.getDefaultInstance();
        try {
            List<RealmUpdateQueue> list = realm.where(RealmUpdateQueue.class)
                    .findAll();
            if (list == null || list.isEmpty()) {
                return false;
            }
            for (RealmUpdateQueue queue : list) {
                if (queue == null) {
                    continue;
                }
                if (!isActiveStatus(queue.getStatus())) {
                    continue;
                }
                if (queue.isScheduleQueue() != isScheduleQueue) {
                    continue;
                }
                String existingPlayer = UpdateQueueHelper.getPlayerId(queue);
                String existingPlaylist = getPlaylistId(queue.getPayloadJson());
                if (!TextUtils.isEmpty(playerId) && !playerId.equalsIgnoreCase(existingPlayer)) {
                    continue;
                }
                if (!TextUtils.isEmpty(playlistId) && !playlistId.equalsIgnoreCase(existingPlaylist)) {
                    continue;
                }
                if (TextUtils.equals(payloadJson, queue.getPayloadJson())) {
                    return true;
                }
            }
            return false;
        } finally {
            realm.close();
        }
    }

    private boolean isActiveStatus(String status) {
        return UpdateQueueContract.Status.QUEUED.equals(status)
                || UpdateQueueContract.Status.DOWNLOADING.equals(status)
                || UpdateQueueContract.Status.DOWNLOADED.equals(status)
                || UpdateQueueContract.Status.VALIDATING.equals(status)
                || UpdateQueueContract.Status.READY.equals(status)
                || UpdateQueueContract.Status.APPLYING.equals(status);
    }

    private String getPlaylistId(String payloadJson) {
        if (TextUtils.isEmpty(payloadJson)) {
            return "";
        }
        try {
            UpdateQueueContract.PlaylistPayload payload = gson.fromJson(payloadJson, UpdateQueueContract.PlaylistPayload.class);
            if (payload != null && !TextUtils.isEmpty(payload.playlistId)) {
                return payload.playlistId;
            }
        } catch (Exception ignore) {
        }
        try {
            UpdatePayloadModels.UpdatePayload updatePayload = UpdatePayloadModels.UpdatePayloadCodec.decode(payloadJson);
            if (updatePayload != null) {
                if (updatePayload.PageList != null && !TextUtils.isEmpty(updatePayload.PageList.PLI_PageListName)) {
                    return updatePayload.PageList.PLI_PageListName;
                }
                if (updatePayload.Contract != null) {
                    if (!TextUtils.isEmpty(updatePayload.Contract.PlaylistId)) {
                        return updatePayload.Contract.PlaylistId;
                    }
                    if (!TextUtils.isEmpty(updatePayload.Contract.PlaylistName)) {
                        return updatePayload.Contract.PlaylistName;
                    }
                }
            }
        } catch (Exception ignore) {
        }
        return "";
    }

    private boolean applyQueueEntry(RealmUpdateQueue queue) {
        return applyQueueEntry(queue, true);
    }

    private boolean applyQueueEntry(RealmUpdateQueue queue, boolean requestUiRestart) {
        if (queue == null) {
            return false;
        }
        boolean applied = false;
        String playerGUID = "";
        boolean isScheduleQueue = queue.isScheduleQueue();
        MoveResult moveResult = new MoveResult();
        ApplyBackup backup = createApplyBackup();
        java.util.List<UpdateQueueContract.DownloadEntry> downloads =
                ContentDownloadJournal.fromJson(queue.getDownloadContentsJson()).getEntries();
        UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.APPLYING);
        String lastError = "";
        String errorCode = "APPLY_FAIL";
        if (TextUtils.equals(queue.getType(), UpdateQueueContract.Type.PLAYLIST)) {
            UpdatePayloadModels.UpdatePayload updatePayload =
                    UpdatePayloadModels.UpdatePayloadCodec.decode(queue.getPayloadJson());
            UpdateQueueContract.PlaylistPayload payload = buildContractPayload(updatePayload);
            if (payload == null) {
                lastError = "Invalid payload";
                errorCode = "INVALID_PAYLOAD";
                applied = false;
            } else {
                try {
                    moveResult = moveStagedDownloadsToFinal(downloads);
                    if (!moveResult.allMoved) {
                        lastError = TextUtils.isEmpty(moveResult.lastError) ? "MOVE_FAIL" : moveResult.lastError;
                        applied = false;
                    } else {
                        playerGUID = payload.playerId;
                        writePlaylistPayload(payload, downloads, !isScheduleQueue);
                        invalidateActivityPlaylistCache(payload.playlistName);
                        if (!isScheduleQueue && !TextUtils.isEmpty(payload.playerId)) {
                            clearCommandAsync(payload.playerId);
                        }
                        applied = true;
                    }
                } catch (Exception applyEx) {
                    applied = false;
                    lastError = applyEx.getMessage();
                    restoreApplyBackup(backup);
                }
            }
        } else if (TextUtils.equals(queue.getType(), UpdateQueueContract.Type.SCHEDULE)) {
            UpdatePayloadModels.UpdatePayload updatePayload =
                    UpdatePayloadModels.UpdatePayloadCodec.decode(queue.getPayloadJson());
            UpdatePayloadModels.ScheduleUpdatePayload schedulePayload =
                    updatePayload == null ? null : updatePayload.Schedule;
            if (schedulePayload == null) {
                lastError = "Invalid schedule payload";
                errorCode = "INVALID_SCHEDULE_PAYLOAD";
                applied = false;
            } else {
                playerGUID = resolveSchedulePlayerId(schedulePayload);
                try {
                    applied = applyQueuedSchedulePayload(schedulePayload);
                    if (!applied) {
                        lastError = "Failed to apply schedule payload";
                        errorCode = "SCHEDULE_APPLY_FAIL";
                    }
                } catch (Exception applyEx) {
                    applied = false;
                    lastError = applyEx.getMessage();
                    errorCode = "SCHEDULE_APPLY_FAIL";
                }
            }
        }
        if (applied) {
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.DONE);
            HeartbeatService.reportQueueStatus(UpdateQueueContract.Status.DONE, 100f, isScheduleQueue);
            // 원격 UpdateQueue 레코드는 삭제하되 PlayerInfo의 최종 상태/진행률은 남긴다.
            String externalId = TextUtils.isEmpty(queue.getExternalId())
                    ? String.valueOf(queue.getId())
                    : queue.getExternalId();
            deleteQueueRecordAsync(externalId, playerGUID);
            if (!TextUtils.isEmpty(playerGUID)) {
                releasePlayerLeaseAsync(playerGUID);
            }
            if (!isScheduleQueue) {
                String playlistName = "";
                UpdatePayloadModels.UpdatePayload appliedPayload =
                        UpdatePayloadModels.UpdatePayloadCodec.decode(queue.getPayloadJson());
                if (appliedPayload != null && appliedPayload.PageList != null
                        && !TextUtils.isEmpty(appliedPayload.PageList.PLI_PageListName)) {
                    playlistName = appliedPayload.PageList.PLI_PageListName;
                }
                if (!TextUtils.isEmpty(playlistName)) {
                    kr.co.turtlelab.andowsignage.dataproviders.PlayerDataProvider.updateCurrentPListName(playlistName);
                }
                if (requestUiRestart) {
                    SystemUtils.runOnUiThread(() -> {
                        if (AndoWSignage.act != null) {
                            AndoWSignage.act.updateAndRestart(true);
                        }
                    });
                }
            }
        } else {
            long delay = UpdateQueueContract.RetryPolicy.getDelayMs(queue.getRetryCount() + 1);
            UpdateQueueHelper.incrementRetry(queue.getId(), System.currentTimeMillis() + delay);
            UpdateQueueHelper.updateStatus(queue.getId(), UpdateQueueContract.Status.FAILED,
                    errorCode, TextUtils.isEmpty(lastError) ? "Failed to apply queue" : lastError);
            if (!TextUtils.isEmpty(playerGUID)) {
                releasePlayerLeaseAsync(playerGUID);
            }
        }
        if (applied) {
            cleanupTempDownloads(downloads, moveResult);
        }
        return applied;
    }

    private void invalidateActivityPlaylistCache(String playlistName) {
        if (TextUtils.isEmpty(playlistName)) {
            return;
        }
        if (AndoWSignage.act != null) {
            AndoWSignage.act.invalidatePlaylistPageCache(playlistName);
        }
    }

    private boolean applyQueuedSchedulePayload(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return false;
        }
        String cacheId = resolveScheduleCacheId(schedule);
        if (TextUtils.isEmpty(cacheId)) {
            return false;
        }
        if (!saveScheduleCache(cacheId, schedule)) {
            return false;
        }
        if (schedule.WeeklySchedule != null) {
            String weeklyKey = resolveWeeklyScheduleKey(schedule);
            if (TextUtils.isEmpty(weeklyKey)
                    || !applyWeeklySchedulePayload(weeklyKey, schedule.WeeklySchedule)) {
                return false;
            }
        }
        return true;
    }

    private String resolveWeeklyScheduleKey(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return "";
        }
        if (!TextUtils.isEmpty(schedule.PlayerId)) {
            return schedule.PlayerId;
        }
        if (schedule.WeeklySchedule != null && !TextUtils.isEmpty(schedule.WeeklySchedule.PlayerID)) {
            return schedule.WeeklySchedule.PlayerID;
        }
        if (!TextUtils.isEmpty(schedule.PlayerName)) {
            return schedule.PlayerName;
        }
        if (schedule.WeeklySchedule != null && !TextUtils.isEmpty(schedule.WeeklySchedule.PlayerName)) {
            return schedule.WeeklySchedule.PlayerName;
        }
        return resolveScheduleCacheId(schedule);
    }

    public boolean applyNextReadyQueue() {
        return UpdateQueueProvider.consumeNextReadyQueue(this::applyQueueEntry);
    }

    public boolean applyNextReadyQueue(boolean requestUiRestart) {
        return UpdateQueueProvider.consumeNextReadyQueue(queue -> applyQueueEntry(queue, requestUiRestart));
    }

    public boolean applyNextSilentReadyQueue() {
        return UpdateQueueProvider.consumeNextSilentReadyQueue(queue -> applyQueueEntry(queue, false));
    }

    public boolean applyNextPlaybackRestartReadyQueue(boolean requestUiRestart) {
        return UpdateQueueProvider.consumeNextPlaybackRestartReadyQueue(queue -> applyQueueEntry(queue, requestUiRestart));
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
                                      List<UpdateQueueContract.DownloadEntry> downloads,
                                      boolean updatePlayerInfo) {
        if (payload == null) {
            return;
        }
        Realm realm = Realm.getDefaultInstance();
        final Set<String> usedContentPaths = new HashSet<>();
        realm.executeTransaction(r -> {
            List<String> existingPageIds = new ArrayList<>();
            if (!TextUtils.isEmpty(payload.playlistName)) {
                List<RealmPage> existingPages = r.where(RealmPage.class)
                        .equalTo("playlistName", payload.playlistName)
                        .findAll();
                if (existingPages != null) {
                    for (RealmPage existingPage : existingPages) {
                        if (existingPage != null && !TextUtils.isEmpty(existingPage.getPageId())) {
                            existingPageIds.add(existingPage.getPageId());
                        }
                    }
                }
            }
            if (!existingPageIds.isEmpty()) {
                List<String> existingElementIds = new ArrayList<>();
                List<RealmElement> existingElements = r.where(RealmElement.class)
                        .in("pageId", existingPageIds.toArray(new String[0]))
                        .findAll();
                if (existingElements != null) {
                    for (RealmElement existingElement : existingElements) {
                        if (existingElement != null && !TextUtils.isEmpty(existingElement.getElementId())) {
                            existingElementIds.add(existingElement.getElementId());
                        }
                    }
                }
                if (!existingElementIds.isEmpty()) {
                    r.where(RealmContent.class)
                            .in("elementId", existingElementIds.toArray(new String[0]))
                            .findAll()
                            .deleteAllFromRealm();
                }
                r.where(RealmWelcome.class).in("pageId", existingPageIds.toArray(new String[0])).findAll().deleteAllFromRealm();
                r.where(RealmElement.class).in("pageId", existingPageIds.toArray(new String[0])).findAll().deleteAllFromRealm();
                r.where(RealmPage.class).in("pageId", existingPageIds.toArray(new String[0])).findAll().deleteAllFromRealm();
            }

            if (updatePlayerInfo) {
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
                realmPage.setCanvasWidth(page.canvasWidth > 0 ? page.canvasWidth : (page.landscape ? 1920 : 1080));
                realmPage.setCanvasHeight(page.canvasHeight > 0 ? page.canvasHeight : (page.landscape ? 1080 : 1920));

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
                        realmElement.setMuted(element.muted);

                        RealmList<RealmContent> contents = new RealmList<>();
                            if (element.contents != null) {
                                for (UpdateQueueContract.ContentPayload content : element.contents) {
                        RealmContent realmContent = r.createObject(RealmContent.class, content.uid);
                        realmContent.setElementId(element.elementId);
                        realmContent.setFileName(content.fileName);
                        String relativePath = resolveRelativePathFromPayload(content);
                        String absolutePath = toLocalAbsolutePath(relativePath);
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
        Realm realm = Realm.getDefaultInstance();
        try {
            List<RealmContent> allContents = realm.copyFromRealm(realm.where(RealmContent.class).findAll());
            for (RealmContent content : allContents) {
                if (content == null || TextUtils.isEmpty(content.getFileFullPath())) {
                    continue;
                }
                normalized.add(new File(content.getFileFullPath()).getAbsolutePath());
            }
        } catch (Exception ignore) {
        } finally {
            realm.close();
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

    private void cleanupTempDownloads(List<UpdateQueueContract.DownloadEntry> entries,
                                      MoveResult moveResult) {
        if (entries == null || moveResult == null || !moveResult.allMoved) {
            return;
        }
        Set<String> moved = (moveResult == null) ? null : moveResult.movedRelativePaths;
        boolean deleteAll = moved == null || moved.isEmpty();
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry == null) {
                continue;
            }
            String fileName = UpdateQueueHelper.normalizeFileName(entry.FileName);
            if (TextUtils.isEmpty(fileName)) {
                fileName = UpdateQueueHelper.normalizeFileName(entry.RemotePath);
            }
            if (!deleteAll && (TextUtils.isEmpty(fileName) || !moved.contains(fileName))) {
                continue;
            }
            try {
                String tempPath = UpdateQueueHelper.getTempContentPath(fileName);
                File tempFile = new File(tempPath);
                if (tempFile.exists()) {
                    tempFile.delete();
                }
            } catch (Exception ignore) {
            }
        }
    }

    private MoveResult moveStagedDownloadsToFinal(List<UpdateQueueContract.DownloadEntry> entries) {
        MoveResult result = new MoveResult();
        if (entries == null) {
            return result;
        }
        for (UpdateQueueContract.DownloadEntry entry : entries) {
            if (entry == null || TextUtils.isEmpty(entry.FileName)) {
                continue;
            }
            try {
                String fileName = UpdateQueueHelper.normalizeFileName(entry.FileName);
                if (TextUtils.isEmpty(fileName)) {
                    fileName = UpdateQueueHelper.normalizeFileName(entry.RemotePath);
                }
                if (TextUtils.isEmpty(fileName)) {
                    continue;
                }
                String tempPath = UpdateQueueHelper.getTempContentPath(fileName);
                String finalPath = UpdateQueueHelper.getFinalContentPath(fileName);
                File tempFile = new File(tempPath);
                File finalFile = new File(finalPath);
                if (!tempFile.exists()) {
                    if (finalFile.exists()) {
                        result.movedRelativePaths.add(fileName);
                        continue;
                    }
                    result.lastError = "MOVE_MISSING:" + fileName;
                    result.allMoved = false;
                    continue;
                }
                boolean moved = false;
                try {
                    if (finalFile.exists() && !finalFile.delete()) {
                        result.lastError = "MOVE_DELETE_FAIL:" + fileName;
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
                    result.movedRelativePaths.add(fileName);
                    AndoWSignage.act.mScanner.notify(finalFile.getAbsolutePath(), false);
                    AndoWSignage.act.mScanner.notify(tempFile.getAbsolutePath(), true);
                } else {
                    result.lastError = "MOVE_FAIL:" + fileName;
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

    private void applyDayPayload(RealmWeeklySchedule schedule,
                                 String day,
                                 UpdatePayloadModels.DaySchedule record) {
        if (record == null) {
            schedule.setSchedule(day, 0, 0, 0, 0);
            schedule.setOnAir(day, true);
            return;
        }
        schedule.setSchedule(day,
                record.StartHour,
                record.StartMinute,
                record.EndHour,
                record.EndMinute);
        schedule.setOnAir(day, record.IsOnAir);
    }

    private String toLocalAbsolutePath(String relativePath) {
        if (TextUtils.isEmpty(relativePath)) {
            return LocalPathUtils.getContentsDirPath();
        }

        String normalized = relativePath.replace("\\", "/").trim();
        while (normalized.startsWith("/")) {
            normalized = normalized.substring(1);
        }

        if (TextUtils.isEmpty(normalized)) {
            return LocalPathUtils.getContentsDirPath();
        }

        if (normalized.equalsIgnoreCase("Contents")
                || normalized.regionMatches(true, 0, "Contents/", 0, "Contents/".length())) {
            return LocalPathUtils.getAbsolutePath(normalized);
        }

        // Payload path can arrive as plain filename. Keep all media under Contents for playback parity.
        String fileName = UpdateQueueHelper.normalizeFileName(normalized);
        if (TextUtils.isEmpty(fileName)) {
            return LocalPathUtils.getContentsDirPath();
        }
        return LocalPathUtils.getContentPath(fileName);
    }

    private Map<String, UpdatePayloadModels.PageInfoClass> indexPayloadPages(UpdatePayloadModels.UpdatePayload payload) {
        Map<String, UpdatePayloadModels.PageInfoClass> index = new HashMap<>();
        if (payload == null || payload.Pages == null) {
            return index;
        }
        for (UpdatePayloadModels.PageInfoClass page : payload.Pages) {
            if (page != null && !TextUtils.isEmpty(page.PIC_GUID)) {
                index.put(page.PIC_GUID, page);
            }
        }
        return index;
    }

    private double getCanvasWidth(UpdatePayloadModels.PageInfoClass page, boolean landscape) {
        if (page != null && page.PIC_CanvasWidth > 0) {
            return page.PIC_CanvasWidth;
        }
        return landscape ? 1920 : 1080;
    }

    private double getCanvasHeight(UpdatePayloadModels.PageInfoClass page, boolean landscape) {
        if (page != null && page.PIC_CanvasHeight > 0) {
            return page.PIC_CanvasHeight;
        }
        return landscape ? 1080 : 1920;
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
        String lastError = "";
    }
}

package kr.co.turtlelab.andowsignage.services;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;
import android.text.TextUtils;
import android.util.Log;
import android.widget.Toast;

import com.google.gson.Gson;

import java.net.InetSocketAddress;
import java.net.Socket;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.AndoWSignageApp.RP_STATUS;
import kr.co.turtlelab.andowsignage.data.CommunicationSettingsSync;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.rethink.CommandQueueClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkModels;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueHelper;
import kr.co.turtlelab.andowsignage.data.update.UpdatePayloadModels;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;
import kr.co.turtlelab.andowsignage.tools.PowerApi;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class UpdateManagerService extends Service implements SignalRClientService.Listener {
    private static final String TAG = "UpdateManagerService";
    private static final int RETHINK_PORT = 28015;
    private static final int CONNECT_TIMEOUT_MS = 1000;
    private static final long RECONNECT_SKIP_MS = 5000L;

    private final IBinder binder = new UpdateMgrLocalBinder();
    private final DataSyncManager syncManager = new DataSyncManager();
    private final CommandQueueClient commandQueueClient = new CommandQueueClient(AndoWSignageApp.MANAGER_IP);
    private final Gson gson = new Gson();
    private LightestTimer updateTimer;
    private boolean loaded = false;
    private final java.util.concurrent.ExecutorService commandExecutor = java.util.concurrent.Executors.newSingleThreadExecutor();
    private final java.util.concurrent.atomic.AtomicBoolean urgentUpdateInProgress = new java.util.concurrent.atomic.AtomicBoolean(false);
    private final java.util.concurrent.atomic.AtomicBoolean communicationSyncInProgress = new java.util.concurrent.atomic.AtomicBoolean(false);
    private final java.util.concurrent.atomic.AtomicBoolean commandPollQueued = new java.util.concurrent.atomic.AtomicBoolean(false);
    private String cachedPlayerGuid = null;
    private volatile String activeRethinkHost = "";
    private volatile long reconnectBlockedUntilMs = 0L;
    private SignalRClientService signalRClient;

    public class UpdateMgrLocalBinder extends Binder {
        public UpdateManagerService getService() {
            return UpdateManagerService.this;
        }
    }

    @Override
    public void onCreate() {
        super.onCreate();
        initTimer();
        syncManager.resumePendingQueues();
        signalRClient = SignalRClientService.getShared(gson);
        signalRClient.setListener(this);
    }

    private void initTimer() {
        updateTimer = new LightestTimer(5000, this::getOrderByManager);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        String host = resolveBootstrapHost();
        String managerHost = host;
        LocalSettingsProvider.applyStoredCommunicationSettings();
        String rethinkHost = resolveRethinkHost(host);
        activeRethinkHost = rethinkHost;
        if (signalRClient != null) {
            signalRClient.start();
        }
        requestHeartbeatNow();
        if (canAttemptCommunicationNow()) {
            executeCommandAsync(() -> {
                if (!canReachRethink(rethinkHost)) {
                    blockReconnectTemporarily();
                    Log.w(TAG, "onStartCommand: skip communication. host unreachable=" + rethinkHost);
                    return;
                }
                syncManager.updateEndpoint(rethinkHost);
                commandQueueClient.updateHost(rethinkHost);
                syncManager.resumePendingQueues();
                syncPlayerGuidAndRecoverCommunication(false);
                if (!TextUtils.isEmpty(managerHost) && canReachRethink(managerHost)) {
                    syncCommunicationSettingsAsync(managerHost);
                }
            });
        } else {
            blockReconnectTemporarily();
            Log.w(TAG, "onStartCommand: skip communication. host unreachable=" + rethinkHost);
        }
        if (loaded) {
            if (updateTimer == null) {
                initTimer();
            } else {
                updateTimer.stop();
            }
        }
        loaded = true;
        updateTimer.start();
        return super.onStartCommand(intent, flags, startId);
    }

    private void syncCommunicationSettingsAsync(String bootstrapHost) {
        if (communicationSyncInProgress.getAndSet(true)) {
            return;
        }
        new Thread(() -> {
            boolean synced = false;
            try {
                synced = CommunicationSettingsSync.syncFromServerAndApply(bootstrapHost);
            } catch (Exception ignore) {
            } finally {
                communicationSyncInProgress.set(false);
            }
            if (synced) {
                String fallbackHost = AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)
                        ? AndoWSignageApp.MANUAL_IP
                        : AndoWSignageApp.MANAGER_IP;
                String rethinkHost = resolveRethinkHost(fallbackHost);
                syncManager.updateEndpoint(rethinkHost);
                commandQueueClient.updateHost(rethinkHost);
                if (signalRClient != null) {
                    signalRClient.reconnect();
                }
                syncPlayerGuidAndRecoverCommunication(true);
                requestHeartbeatNow();
            }
        }).start();
    }

    private String resolveRethinkHost(String fallbackHost) {
        String dataServerIp = LocalSettingsProvider.getDataServerIp();
        if (!TextUtils.isEmpty(dataServerIp)) {
            return NetworkUtils.extractHost(dataServerIp);
        }
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return NetworkUtils.extractHost(AndoWSignageApp.MANUAL_IP);
        }
        return NetworkUtils.extractHost(fallbackHost);
    }

    private String resolveBootstrapHost() {
        if (AndoWSignageApp.IS_MANUAL && !TextUtils.isEmpty(AndoWSignageApp.MANUAL_IP)) {
            return NetworkUtils.extractHost(AndoWSignageApp.MANUAL_IP);
        }
        String localManual = LocalSettingsProvider.getManualIp();
        if (!TextUtils.isEmpty(localManual)) {
            return NetworkUtils.extractHost(localManual);
        }
        if (!TextUtils.isEmpty(AndoWSignageApp.MANAGER_IP)) {
            return NetworkUtils.extractHost(AndoWSignageApp.MANAGER_IP);
        }
        String localManager = LocalSettingsProvider.getManagerIp();
        if (!TextUtils.isEmpty(localManager)) {
            return NetworkUtils.extractHost(localManager);
        }
        String dataServerIp = LocalSettingsProvider.getDataServerIp();
        if (!TextUtils.isEmpty(dataServerIp)) {
            return NetworkUtils.extractHost(dataServerIp);
        }
        return "";
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        if (updateTimer != null) {
            updateTimer.stop();
        }
        if (signalRClient != null) {
            signalRClient.setListener(null);
            if (!AndoWSignageApp.isShutdownInProgress()) {
                signalRClient.stop();
            } else {
                Log.i(TAG, "onDestroy: skip SignalR stop because terminal shutdown is in progress.");
            }
        }
        commandExecutor.shutdownNow();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return binder;
    }

    public void getOrderByManager() {
        if (!canAttemptCommunicationNow()) {
            return;
        }
        if (!commandPollQueued.compareAndSet(false, true)) {
            return;
        }
        executeCommandAsync(() -> {
            try {
                String host = resolveRethinkHost(resolveBootstrapHost());
                activeRethinkHost = host;
                if (!canReachRethink(host)) {
                    blockReconnectTemporarily();
                    return;
                }
                syncManager.updateEndpoint(host);
                commandQueueClient.updateHost(host);
                syncManager.resumePendingQueues();
                syncPlayerGuidAndRecoverCommunication(true);
                checkCommand();
            } finally {
                commandPollQueued.set(false);
            }
        });
    }

    private void checkCommand() {
        String playerGuid = resolvePlayerGuid();
        if (TextUtils.isEmpty(playerGuid)) {
            return;
        }
        CommandQueueClient.CommandQueueEntry entry = commandQueueClient.fetchNextPending(playerGuid);
        if (entry == null) {
            return;
        }
        commandQueueClient.markAttempt(entry.id);
        handleCommandEntry(playerGuid, entry, false);
    }

    private void handleCommandEntry(String playerGuid, CommandQueueClient.CommandQueueEntry entry, boolean isUrgent) {
        if (entry == null) {
            return;
        }
        String command = entry.Command;
        if (TextUtils.isEmpty(command)) {
            return;
        }
        UpdatePayloadModels.UpdatePayload payload = UpdatePayloadModels.UpdatePayloadCodec.decode(entry.payloadJson);
        boolean handled = handleCommand(playerGuid, command.trim().toLowerCase(), payload, isUrgent, entry.id);
        if (handled) {
            commandQueueClient.markAck(entry.id, playerGuid);
        } else {
            commandQueueClient.markFailed(entry.id, playerGuid);
        }
    }

    private void handleCommandFromSignalR(String command) {
        if (TextUtils.isEmpty(command)) {
            return;
        }
        executeCommandAsync(() -> {
            String playerGuid = resolvePlayerGuid();
            if (TextUtils.isEmpty(playerGuid)) {
                return;
            }
            handleCommand(playerGuid, command.trim().toLowerCase(), null, false, null);
        });
    }

    private void handleCommandFromSignalR(SignalRClientService.SignalRCommandEnvelope envelope) {
        if (envelope == null || TextUtils.isEmpty(envelope.Command)) {
            return;
        }
        executeCommandAsync(() -> {
            String playerGuid = resolvePlayerGuid();
            if (TextUtils.isEmpty(playerGuid)) {
                return;
            }
            UpdatePayloadModels.UpdatePayload payload = UpdatePayloadModels.UpdatePayloadCodec.decode(envelope.PayloadJson);
            boolean handled = handleCommand(playerGuid,
                    envelope.Command.trim().toLowerCase(),
                    payload,
                    envelope.IsUrgent,
                    envelope.CommandId);
            if (!TextUtils.isEmpty(envelope.CommandId)) {
                if (handled) {
                    commandQueueClient.markAck(envelope.CommandId, playerGuid);
                } else {
                    commandQueueClient.markFailed(envelope.CommandId, playerGuid);
                }
            }
        });
    }

    private void handleWeeklyScheduleFromSignalR(RethinkModels.WeeklyScheduleRecord weekly) {
        if (weekly == null) {
            return;
        }
        executeCommandAsync(() -> {
            String playerKey = weekly.getPlayerId();
            if (TextUtils.isEmpty(playerKey)) {
                playerKey = resolvePlayerGuid();
            }
            syncManager.applyWeeklyScheduleRecord(playerKey, weekly);
        });
    }

    private boolean applySchedulePayload(UpdatePayloadModels.ScheduleUpdatePayload schedule) {
        if (schedule == null) {
            return false;
        }
        String cacheId = !TextUtils.isEmpty(schedule.PlayerId)
                ? schedule.PlayerId
                : schedule.PlayerName;
        if (TextUtils.isEmpty(cacheId)) {
            return false;
        }
        syncManager.saveScheduleCache(cacheId, schedule);
        if (schedule.Playlists != null) {
            for (UpdatePayloadModels.SchedulePlaylistPayload playlist : schedule.Playlists) {
                if (playlist == null || playlist.PageList == null || playlist.Pages == null || playlist.Pages.isEmpty()) {
                    continue;
                }
                UpdatePayloadModels.UpdatePayload payload = new UpdatePayloadModels.UpdatePayload();
                payload.PageList = playlist.PageList;
                payload.Pages = playlist.Pages;
                payload.Contract = playlist.Contract;
                syncManager.enqueuePayloadUpdate(payload, true);
            }
        }
        if (schedule.WeeklySchedule != null) {
            String weeklyKey = !TextUtils.isEmpty(schedule.PlayerId)
                    ? schedule.PlayerId
                    : schedule.PlayerName;
            if (TextUtils.isEmpty(weeklyKey)) {
                weeklyKey = resolvePlayerGuid();
            }
            syncManager.applyWeeklySchedulePayload(weeklyKey, schedule.WeeklySchedule);
        }
        return true;
    }

    private void executeCommandAsync(Runnable task) {
        if (task == null) {
            return;
        }
        commandExecutor.execute(() -> {
            try {
                task.run();
            } catch (Exception ex) {
                Log.e(TAG, "executeCommandAsync: task failed", ex);
            }
        });
    }

    private boolean handleCommand(String playerGuid,
                                  String command,
                                  UpdatePayloadModels.UpdatePayload payload,
                                  boolean isUrgent,
                                  String commandId) {
        if (TextUtils.isEmpty(playerGuid) || TextUtils.isEmpty(command)) {
            return false;
        }
        RethinkDbClient client = RethinkDbClient.getInstance();
        RethinkModels.PlayerInfoRecord playerInfo = client.fetchPlayerByGuid(playerGuid);
        String playerName = playerInfo != null ? playerInfo.getPlayerName() : "";
        String historyId = null;
        if (!"updatelist".equals(command)) {
            historyId = client.createCommandHistory(playerGuid, playerName, command);
        }
        boolean isClearQueue = "clearqueue".equals(command);
        boolean hasActiveQueue = UpdateQueueHelper.hasActiveQueue();
        if (hasActiveQueue && !isClearQueue) {
            int cancelled = UpdateQueueHelper.cancelActiveQueues("Cancelled due to new command");
            UpdateQueueHelper.requeueFailedQueuesIfDue();
            syncManager.releaseActiveLease();
//            SystemUtils.runOnUiThread(() -> Toast.makeText(AndoWSignage.getCtx(),
//                    "Cancelling active queue (" + cancelled + ") and executing " + command,
//                    Toast.LENGTH_SHORT).show());
        }
        client.clearCommand(playerGuid);
//        SystemUtils.runOnUiThread(() -> Toast.makeText(AndoWSignage.getCtx(), command, Toast.LENGTH_SHORT).show());
        boolean handled = true;
        switch (command) {
            case "updatelist":
                if (isUrgent && !urgentUpdateInProgress.compareAndSet(false, true)) {
                    // Windows와 동일하게 이미 urgent 업데이트가 진행 중이면 무시(Handled 처리)
                    handled = true;
                    break;
                }
                try {
                    if (payload == null || payload.PageList == null || payload.Pages == null || payload.Pages.isEmpty()) {
                        historyId = client.createCommandHistory(playerGuid, playerName, command);
                        client.updateCommandHistory(historyId, "failed", "PAYLOAD_MISSING", "Missing update payload", null);
                        handled = false;
                        break;
                    }
                    long queueId = syncManager.enqueuePayloadUpdate(payload, false);
                    if (queueId > 0) {
                        Long createdTicks = null;
                        String externalId = String.valueOf(queueId);
                        try {
                            io.realm.Realm realm = io.realm.Realm.getDefaultInstance();
                            try {
                                kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue q = realm.where(kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue.class)
                                        .equalTo("id", queueId)
                                        .findFirst();
                                if (q != null) {
                                    createdTicks = kr.co.turtlelab.andowsignage.data.update.UpdateQueueHelper.toDotNetLocalTicks(q.getCreatedAt());
                                    if (!TextUtils.isEmpty(q.getExternalId())) {
                                        externalId = q.getExternalId();
                                    }
                                }
                            } finally {
                                realm.close();
                            }
                        } catch (Exception ignored) { }
                        historyId = client.upsertCommandHistoryForQueue(playerGuid,
                                playerName,
                                command,
                                externalId,
                                "queued",
                                null,
                                null,
                                null,
                                createdTicks);
                        if (isUrgent) {
                            syncManager.releaseActiveLease();
                            syncManager.processQueueImmediate(queueId, true);
                        }
                    } else {
                        historyId = client.createCommandHistory(playerGuid, playerName, command);
                        client.updateCommandHistory(historyId, "failed", "ENQUEUE_FAIL", "Failed to enqueue playlist", null);
                        handled = false;
                    }
                } finally {
                    if (isUrgent) {
                        urgentUpdateInProgress.set(false);
                    }
                }
                break;
            case "updateschedule":
                client.updateCommandHistory(historyId, "in_progress", null, null, null);
                if (payload == null || payload.Schedule == null) {
                    client.updateCommandHistory(historyId, "failed", "SCHEDULE_PAYLOAD", "payload missing", null);
                    handled = false;
                    break;
                }
                boolean scheduleApplied = applySchedulePayload(payload.Schedule);
                if (scheduleApplied) {
                    client.updateCommandHistory(historyId, "done", null, null, null);
                } else {
                    client.updateCommandHistory(historyId, "failed", "SCHEDULE_PAYLOAD", "payload missing", null);
                    handled = false;
                }
                //PowerApi.pushScheduleToDevice();
                break;
            case "updateweekly":
                client.updateCommandHistory(historyId, "in_progress", null, null, null);
                if (payload == null || payload.Schedule == null || payload.Schedule.WeeklySchedule == null) {
                    client.updateCommandHistory(historyId, "failed", "WEEKLY_PAYLOAD", "payload missing", null);
                    handled = false;
                    break;
                }
                String weeklyKey = !TextUtils.isEmpty(payload.Schedule.PlayerId)
                        ? payload.Schedule.PlayerId
                        : payload.Schedule.PlayerName;
                if (TextUtils.isEmpty(weeklyKey)) {
                    weeklyKey = playerGuid;
                }
                if (syncManager.applyWeeklySchedulePayload(weeklyKey, payload.Schedule.WeeklySchedule)) {
                    client.updateCommandHistory(historyId, "done", null, null, null);
                } else {
                    client.updateCommandHistory(historyId, "failed", "WEEKLY_PAYLOAD", "payload missing", null);
                    handled = false;
                }
                break;
            case "reboot":
                client.updateCommandHistory(historyId, "in_progress", null, null, null);
                PowerApi.requestReboot(this);
                client.updateCommandHistory(historyId, "done", null, null, null);
                break;
            case "poweroff":
                client.updateCommandHistory(historyId, "in_progress", null, null, null);
                PowerApi.requestPowerOff(this);
                client.updateCommandHistory(historyId, "done", null, null, null);
                break;
            case "clearqueue":
                client.updateCommandHistory(historyId, "in_progress", null, null, null);
                int cancelled = UpdateQueueHelper.cancelActiveQueues("Cancelled via command");
                syncManager.releaseActiveLease();
                SystemUtils.runOnUiThread(() -> {
                    String msg = cancelled > 0
                            ? "Update queue cancelled"
                            : "No active update queue";
                    //Toast.makeText(AndoWSignage.getCtx(), msg, Toast.LENGTH_SHORT).show();
                });
                String metadata = "cancelled=" + cancelled;
                client.updateCommandHistory(historyId,
                        cancelled > 0 ? "cancelled" : "done",
                        null,
                        null,
                        null,
                        metadata);
                break;
            default:
                client.updateCommandHistory(historyId, "failed", "UNKNOWN", "Unknown command", null);
                handled = false;
                break;
        }
        return handled;
    }

    @Override
    public void onCommand(String command) {
        handleCommandFromSignalR(command);
    }

    @Override
    public void onCommandEnvelope(SignalRClientService.SignalRCommandEnvelope envelope) {
        handleCommandFromSignalR(envelope);
    }

    @Override
    public void onWeeklySchedule(RethinkModels.WeeklyScheduleRecord weekly) {
        handleWeeklyScheduleFromSignalR(weekly);
    }

    private void requestPlaylistEnqueue(RethinkModels.PlayerInfoRecord player) {
        if (player == null) {
            return;
        }
        AndoWSignageApp.state = RP_STATUS.updating.toString();
        long queueId = syncManager.enqueuePlaylistUpdate(player);
        AndoWSignageApp.state = RP_STATUS.playing.toString();
        if (queueId <= 0) {
            return;
        }
        SystemUtils.runOnUiThread(() -> {
            AndoWSignage.act.showReadyUpdateIndicator();
        });
    }

    public void CheckOrRestartTimer() {
        if (updateTimer == null) {
            initTimer();
        } else {
            updateTimer.stop();
        }
        updateTimer.start();
    }

    private String resolvePlayerGuid() {
        String syncedGuid = syncPlayerGuidAndRecoverCommunication(false);
        if (!TextUtils.isEmpty(syncedGuid)) {
            return syncedGuid;
        }
        if (!canAttemptCommunicationNow()) {
            return cachedPlayerGuid;
        }
        if (!canReachRethink(activeRethinkHost)) {
            blockReconnectTemporarily();
            return cachedPlayerGuid;
        }
        RethinkDbClient client = RethinkDbClient.getInstance();
        String ensuredGuid = client.ensurePlayerGuid();
        if (TextUtils.isEmpty(ensuredGuid)) {
            blockReconnectTemporarily();
            return null;
        }
        if (!TextUtils.equals(cachedPlayerGuid, ensuredGuid)) {
            cachedPlayerGuid = ensuredGuid;
        }
        return cachedPlayerGuid;
    }

    private String syncPlayerGuidAndRecoverCommunication(boolean recoverCommunication) {
        if (!canAttemptCommunicationNow()) {
            return cachedPlayerGuid;
        }
        if (!canReachRethink(activeRethinkHost)) {
            blockReconnectTemporarily();
            return cachedPlayerGuid;
        }
        RethinkDbClient client = RethinkDbClient.getInstance();
        String ensuredGuid = client.ensurePlayerGuid();
        if (TextUtils.isEmpty(ensuredGuid)) {
            blockReconnectTemporarily();
            return cachedPlayerGuid;
        }

        String previousGuid = cachedPlayerGuid;
        boolean isFirstSync = TextUtils.isEmpty(previousGuid);
        boolean guidChanged = !isFirstSync && !ensuredGuid.equalsIgnoreCase(previousGuid);
        cachedPlayerGuid = ensuredGuid;

        if (guidChanged) {
            syncManager.releasePlayerLease(previousGuid);
            if (recoverCommunication && signalRClient != null) {
                signalRClient.reconnect();
            }
            if (recoverCommunication) {
                requestHeartbeatNow();
            }
        }
        return cachedPlayerGuid;
    }

    private boolean canAttemptCommunicationNow() {
        return System.currentTimeMillis() >= reconnectBlockedUntilMs;
    }

    private void blockReconnectTemporarily() {
        reconnectBlockedUntilMs = System.currentTimeMillis() + RECONNECT_SKIP_MS;
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

    private void requestHeartbeatNow() {
        Intent heartbeatIntent = new Intent(this, HeartbeatService.class);
        heartbeatIntent.setAction(HeartbeatService.ACTION_SEND_NOW);
        startService(heartbeatIntent);
    }

}

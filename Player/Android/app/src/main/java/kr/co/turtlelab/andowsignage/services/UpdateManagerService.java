package kr.co.turtlelab.andowsignage.services;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;
import android.text.TextUtils;
import android.widget.Toast;

import kr.co.turtlelab.andowsignage.AndoWSignage;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.AndoWSignageApp.RP_STATUS;
import kr.co.turtlelab.andowsignage.data.DataSyncManager;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkModels;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueHelper;
import kr.co.turtlelab.andowsignage.tools.LightestTimer;
import kr.co.turtlelab.andowsignage.tools.PowerApi;
import kr.co.turtlelab.andowsignage.tools.SystemUtils;

public class UpdateManagerService extends Service {

    private final IBinder binder = new UpdateMgrLocalBinder();
    private final DataSyncManager syncManager = new DataSyncManager();
    private LightestTimer updateTimer;
    private boolean loaded = false;
    private boolean isExecuting = false;
    private String cachedPlayerGuid = null;

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
    }

    private void initTimer() {
        updateTimer = new LightestTimer(5000, this::getOrderByManager);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        syncManager.updateEndpoint(AndoWSignageApp.MANAGER_IP);
        syncManager.resumePendingQueues();
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

    @Override
    public void onDestroy() {
        super.onDestroy();
        if (updateTimer != null) {
            updateTimer.stop();
        }
    }

    @Override
    public IBinder onBind(Intent intent) {
        return binder;
    }

    public void getOrderByManager() {
        syncManager.resumePendingQueues();
        if (AndoWSignageApp.isUpdating) {
            return;
        }
        synchronized (this) {
            if (isExecuting) {
                return;
            }
            isExecuting = true;
        }
        new Thread(() -> {
            try {
                checkCommand();
            } finally {
                isExecuting = false;
            }
        }).start();
    }

    private void checkCommand() {
        String playerGuid = resolvePlayerGuid();
        if (TextUtils.isEmpty(playerGuid)) {
            return;
        }
        RethinkDbClient client = RethinkDbClient.getInstance();
        String command = client.fetchPlayerCommand(playerGuid);
        if (TextUtils.isEmpty(command)) {
            return;
        }
        handleCommand(playerGuid, command.trim().toLowerCase());
    }

    private void handleCommand(String playerGuid, String command) {
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
            SystemUtils.runOnUiThread(() -> Toast.makeText(AndoWSignage.getCtx(),
                    "Cancelling active queue (" + cancelled + ") and executing " + command,
                    Toast.LENGTH_SHORT).show());
        }
        client.clearCommand(playerGuid);
        SystemUtils.runOnUiThread(() -> Toast.makeText(AndoWSignage.getCtx(), command, Toast.LENGTH_SHORT).show());
        switch (command) {
            case "updatelist":
                RethinkModels.PlayerInfoRecord player = client.fetchPlayerByGuid(playerGuid);
                long queueId = syncManager.enqueuePlaylistUpdate(player);
                if (queueId > 0) {
                    Long createdTicks = null;
                    try {
                        io.realm.Realm realm = io.realm.Realm.getDefaultInstance();
                        try {
                            kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue q = realm.where(kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue.class)
                                    .equalTo("id", queueId)
                                    .findFirst();
                            if (q != null) {
                                createdTicks = kr.co.turtlelab.andowsignage.data.update.UpdateQueueHelper.toDotNetLocalTicks(q.getCreatedAt());
                            }
                        } finally {
                            realm.close();
                        }
                    } catch (Exception ignored) { }
                    historyId = client.upsertCommandHistoryForQueue(playerGuid,
                            playerName,
                            command,
                            queueId,
                            "in_progress",
                            null,
                            null,
                            null,
                            createdTicks);
                } else {
                    historyId = client.createCommandHistory(playerGuid, playerName, command);
                    client.updateCommandHistory(historyId, "failed", "ENQUEUE_FAIL", "Failed to enqueue playlist", null);
                }
                break;
            case "updateschedule":
                client.updateCommandHistory(historyId, "in_progress", null, null, null);
                client.fetchInitialWeeklySchedule();
                client.updateCommandHistory(historyId, "done", null, null, null);
                //PowerApi.pushScheduleToDevice();
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
                SystemUtils.runOnUiThread(() -> {
                    String msg = cancelled > 0
                            ? "Update queue cancelled"
                            : "No active update queue";
                    Toast.makeText(AndoWSignage.getCtx(), msg, Toast.LENGTH_SHORT).show();
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
                break;
        }
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
        RethinkDbClient client = RethinkDbClient.getInstance();
        if (!client.isDeviceInfoSynced()) {
            client.ensurePlayerGuid(AndoWSignageApp.PLAYER_ID);
            if (!client.isDeviceInfoSynced()) {
                return null;
            }
        }
        if (TextUtils.isEmpty(cachedPlayerGuid)) {
            cachedPlayerGuid = client.ensurePlayerGuid(AndoWSignageApp.PLAYER_ID);
        }
        return cachedPlayerGuid;
    }

}

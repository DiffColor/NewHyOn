package kr.co.turtlelab.andowsignage.dataproviders;

import io.realm.Realm;
import io.realm.Sort;
import kr.co.turtlelab.andowsignage.data.realm.RealmUpdateQueue;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;

public final class UpdateQueueProvider {

    private UpdateQueueProvider() { }

    public static boolean hasReadyQueue() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = realm.where(RealmUpdateQueue.class)
                    .equalTo("status", UpdateQueueContract.Status.READY)
                    .sort("id")
                    .findFirst();
            return queue != null;
        } finally {
            realm.close();
        }
    }

    public static boolean hasSilentReadyQueue() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = findReadyQueue(realm, true);
            return queue != null;
        } finally {
            realm.close();
        }
    }

    public static boolean hasReadyQueueRequiringPlaybackRestart() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = findReadyQueue(realm, false);
            return queue != null;
        } finally {
            realm.close();
        }
    }

    public static boolean consumeNextReadyQueue(DataConsumer consumer) {
        return consumeNextReadyQueue(consumer, null);
    }

    public static boolean consumeNextSilentReadyQueue(DataConsumer consumer) {
        return consumeNextReadyQueue(consumer, true);
    }

    public static boolean consumeNextPlaybackRestartReadyQueue(DataConsumer consumer) {
        return consumeNextReadyQueue(consumer, false);
    }

    private static boolean consumeNextReadyQueue(DataConsumer consumer, Boolean silentOnly) {
        if (consumer == null) {
            return false;
        }
        Realm realm = Realm.getDefaultInstance();
        RealmUpdateQueue queue;
        try {
            realm.beginTransaction();
            queue = findReadyQueue(realm, silentOnly);
            if (queue == null) {
                realm.cancelTransaction();
                return false;
            }
            queue = realm.copyFromRealm(queue);
            realm.commitTransaction();
        } catch (Exception e) {
            if (realm.isInTransaction()) {
                realm.cancelTransaction();
            }
            realm.close();
            return false;
        }
        realm.close();
        return consumer.consume(queue);
    }

    private static RealmUpdateQueue findReadyQueue(Realm realm, Boolean silentOnly) {
        if (realm == null) {
            return null;
        }
        io.realm.RealmResults<RealmUpdateQueue> queues = realm.where(RealmUpdateQueue.class)
                .equalTo("status", UpdateQueueContract.Status.READY)
                .sort("id")
                .findAll();
        if (queues == null || queues.isEmpty()) {
            return null;
        }
        for (RealmUpdateQueue queue : queues) {
            if (queue == null) {
                continue;
            }
            boolean silent = isSilentQueue(queue);
            if (silentOnly == null || silentOnly == silent) {
                return queue;
            }
        }
        return null;
    }

    private static boolean isSilentQueue(RealmUpdateQueue queue) {
        if (queue == null) {
            return false;
        }
        return queue.isScheduleQueue()
                || UpdateQueueContract.Type.SCHEDULE.equals(queue.getType());
    }

    public static RealmUpdateQueue getLatestQueueSnapshot() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmUpdateQueue queue = realm.where(RealmUpdateQueue.class)
                    .sort("updatedAt", Sort.DESCENDING)
                    .findFirst();
            return queue == null ? null : realm.copyFromRealm(queue);
        } finally {
            realm.close();
        }
    }

    public interface DataConsumer {
        boolean consume(RealmUpdateQueue queue);
    }
}

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

    public static boolean consumeNextReadyQueue(DataConsumer consumer) {
        if (consumer == null) {
            return false;
        }
        Realm realm = Realm.getDefaultInstance();
        RealmUpdateQueue queue;
        try {
            realm.beginTransaction();
            queue = realm.where(RealmUpdateQueue.class)
                    .equalTo("status", UpdateQueueContract.Status.READY)
                    .sort("id")
                    .findFirst();
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

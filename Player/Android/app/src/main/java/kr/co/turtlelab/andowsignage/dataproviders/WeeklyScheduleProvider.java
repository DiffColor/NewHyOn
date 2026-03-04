package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmWeeklySchedule;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;

public class WeeklyScheduleProvider {

    private static final String[] DAYS = {"MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"};

    private WeeklyScheduleProvider() {
    }

    public static List<WeeklyScheduleDataModel> getWeeklyScheduleList() {
        List<WeeklyScheduleDataModel> list = new ArrayList<>();
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmWeeklySchedule schedule = resolveScheduleForRead(realm);
            if (schedule == null) {
                realm.executeTransaction(r -> ensureScheduleForWrite(r));
                schedule = resolveScheduleForRead(realm);
            }
            if (schedule == null) {
                return list;
            }
            RealmWeeklySchedule detached = realm.copyFromRealm(schedule);
            for (String day : DAYS) {
                addModel(list, detached, day);
            }
        } finally {
            realm.close();
        }
        return list;
    }

    public static void updateFromTime(String day, String hour, String minute) {
        updateDay(day, true, hour, minute);
    }

    public static void updateToTime(String day, String hour, String minute) {
        updateDay(day, false, hour, minute);
    }

    public static void updateIsOnAir(String day, boolean isOnAir) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmWeeklySchedule schedule = ensureScheduleForWrite(r);
            if (schedule == null) {
                return;
            }
            schedule.setOnAir(day, isOnAir);
        });
        realm.close();
    }

    private static void updateDay(String day, boolean isFrom, String hour, String minute) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmWeeklySchedule schedule = ensureScheduleForWrite(r);
            if (schedule == null) {
                return;
            }
            int h = safeParse(hour);
            int m = safeParse(minute);
            int startHour = schedule.getStartHour(day);
            int startMinute = schedule.getStartMinute(day);
            int endHour = schedule.getEndHour(day);
            int endMinute = schedule.getEndMinute(day);
            if (isFrom) {
                schedule.setSchedule(day, h, m, endHour, endMinute);
            } else {
                schedule.setSchedule(day, startHour, startMinute, h, m);
            }
        });
        realm.close();
    }

    private static void addModel(List<WeeklyScheduleDataModel> list,
                                 RealmWeeklySchedule schedule,
                                 String day) {
        WeeklyScheduleDataModel model = new WeeklyScheduleDataModel();
        model.setDay(day);
        model.setFrom(String.valueOf(schedule.getStartHour(day)), String.valueOf(schedule.getStartMinute(day)));
        model.setTo(String.valueOf(schedule.getEndHour(day)), String.valueOf(schedule.getEndMinute(day)));
        model.setOnAir(String.valueOf(schedule.isOnAir(day)));
        list.add(model);
    }

    private static int safeParse(String value) {
        try {
            return Integer.parseInt(value);
        } catch (Exception e) {
            return 0;
        }
    }

    private static RealmWeeklySchedule ensureScheduleForWrite(Realm realm) {
        RealmWeeklySchedule schedule = resolveScheduleForRead(realm);
        String preferredKey = resolvePreferredScheduleKey();
        if (schedule != null) {
            if (TextUtils.isEmpty(preferredKey) || TextUtils.equals(schedule.getPlayerId(), preferredKey)) {
                return schedule;
            }
            RealmWeeklySchedule preferred = realm.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", preferredKey)
                    .findFirst();
            if (preferred == null) {
                preferred = realm.createObject(RealmWeeklySchedule.class, preferredKey);
                copySchedule(schedule, preferred);
                schedule.deleteFromRealm();
            }
            return preferred;
        }

        if (TextUtils.isEmpty(preferredKey)) {
            return null;
        }
        RealmWeeklySchedule created = realm.where(RealmWeeklySchedule.class)
                .equalTo("playerId", preferredKey)
                .findFirst();
        if (created == null) {
            created = realm.createObject(RealmWeeklySchedule.class, preferredKey);
            applyDefault(created);
        }
        return created;
    }

    private static RealmWeeklySchedule resolveScheduleForRead(Realm realm) {
        String guid = RethinkDbClient.getInstance().getStoredPlayerGuid();
        if (!TextUtils.isEmpty(guid)) {
            RealmWeeklySchedule byGuid = realm.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", guid)
                    .findFirst();
            if (byGuid != null) {
                return byGuid;
            }
        }

        String configuredName = AndoWSignageApp.PLAYER_ID;
        if (!TextUtils.isEmpty(configuredName)) {
            RealmWeeklySchedule byConfiguredName = realm.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", configuredName)
                    .findFirst();
            if (byConfiguredName != null) {
                return byConfiguredName;
            }
        }

        String storedName = RethinkDbClient.getInstance().getStoredPlayerName();
        if (!TextUtils.isEmpty(storedName)) {
            RealmWeeklySchedule byStoredName = realm.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", storedName)
                    .findFirst();
            if (byStoredName != null) {
                return byStoredName;
            }
        }

        return realm.where(RealmWeeklySchedule.class).findFirst();
    }

    private static String resolvePreferredScheduleKey() {
        String guid = RethinkDbClient.getInstance().getStoredPlayerGuid();
        if (!TextUtils.isEmpty(guid)) {
            return guid;
        }
        if (!TextUtils.isEmpty(AndoWSignageApp.PLAYER_ID)) {
            return AndoWSignageApp.PLAYER_ID;
        }
        String storedName = RethinkDbClient.getInstance().getStoredPlayerName();
        if (!TextUtils.isEmpty(storedName)) {
            return storedName;
        }
        return null;
    }

    private static void applyDefault(RealmWeeklySchedule schedule) {
        if (schedule == null) {
            return;
        }
        for (String day : DAYS) {
            schedule.setSchedule(day, 0, 0, 0, 0);
            schedule.setOnAir(day, true);
        }
    }

    private static void copySchedule(RealmWeeklySchedule src, RealmWeeklySchedule dst) {
        for (String day : DAYS) {
            dst.setSchedule(day,
                    src.getStartHour(day),
                    src.getStartMinute(day),
                    src.getEndHour(day),
                    src.getEndMinute(day));
            dst.setOnAir(day, src.isOnAir(day));
        }
    }
}

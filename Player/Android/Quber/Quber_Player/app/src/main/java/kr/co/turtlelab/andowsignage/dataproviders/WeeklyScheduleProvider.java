package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmPlayer;
import kr.co.turtlelab.andowsignage.data.realm.RealmWeeklySchedule;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;

public class WeeklyScheduleProvider {

    private static final String[] DAYS = {
            "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"
    };

    private WeeklyScheduleProvider() {
    }

    public static List<WeeklyScheduleDataModel> getWeeklyScheduleList() {
        List<WeeklyScheduleDataModel> list = new ArrayList<>();
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmWeeklySchedule schedule = findSchedule(realm);
            if (schedule == null) {
                addDefaultModels(list);
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
            RealmWeeklySchedule schedule = ensureScheduleInTransaction(r);
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
            RealmWeeklySchedule schedule = ensureScheduleInTransaction(r);
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

    private static void addDefaultModels(List<WeeklyScheduleDataModel> list) {
        for (String day : DAYS) {
            WeeklyScheduleDataModel model = new WeeklyScheduleDataModel();
            model.setDay(day);
            model.setFrom("0", "0");
            model.setTo("0", "0");
            model.setOnAir("true");
            list.add(model);
        }
    }

    private static RealmWeeklySchedule ensureScheduleInTransaction(Realm realm) {
        if (realm == null) {
            return null;
        }
        RealmWeeklySchedule schedule = findSchedule(realm);
        if (schedule != null) {
            return schedule;
        }
        String scheduleKey = resolvePreferredScheduleKey(realm);
        if (TextUtils.isEmpty(scheduleKey)) {
            return null;
        }
        schedule = realm.createObject(RealmWeeklySchedule.class, scheduleKey);
        applyDefaultSchedule(schedule);
        return schedule;
    }

    private static RealmWeeklySchedule findSchedule(Realm realm) {
        if (realm == null) {
            return null;
        }
        for (String key : resolveScheduleKeys(realm)) {
            RealmWeeklySchedule schedule = realm.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", key)
                    .findFirst();
            if (schedule != null) {
                return schedule;
            }
        }
        return null;
    }

    private static String resolvePreferredScheduleKey(Realm realm) {
        for (RealmPlayer player : realm.where(RealmPlayer.class).findAll()) {
            if (!TextUtils.isEmpty(player.getPlayerId())
                    && !PlayerDataProvider.isLegacyLocalPlayerId(player.getPlayerId())) {
                return player.getPlayerId();
            }
        }
        return null;
    }

    private static List<String> resolveScheduleKeys(Realm realm) {
        Set<String> keys = new LinkedHashSet<>();
        List<RealmPlayer> players = realm.where(RealmPlayer.class).findAll();
        for (RealmPlayer player : players) {
            if (!PlayerDataProvider.isLegacyLocalPlayerId(player.getPlayerId())) {
                addKey(keys, player.getPlayerId());
                addKey(keys, player.getPlayerName());
            }
        }
        if (keys.isEmpty() && !PlayerDataProvider.isLegacyLocalPlayerId(AndoWSignageApp.PLAYER_ID)) {
            // 이전 버전이 이름으로 저장한 캐시를 GUID 동기화 전 한 번만 읽는다.
            addKey(keys, AndoWSignageApp.PLAYER_ID);
        }
        return new ArrayList<>(keys);
    }

    private static void addKey(Set<String> keys, String value) {
        if (!TextUtils.isEmpty(value)) {
            keys.add(value);
        }
    }

    private static void applyDefaultSchedule(RealmWeeklySchedule schedule) {
        if (schedule == null) {
            return;
        }
        for (String day : DAYS) {
            schedule.setSchedule(day, 0, 0, 0, 0);
            schedule.setOnAir(day, true);
        }
    }

    private static int safeParse(String value) {
        try {
            return Integer.parseInt(value);
        } catch (Exception e) {
            return 0;
        }
    }
}

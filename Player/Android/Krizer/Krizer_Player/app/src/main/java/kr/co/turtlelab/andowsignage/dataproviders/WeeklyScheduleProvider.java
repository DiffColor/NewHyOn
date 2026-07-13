package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import java.util.ArrayList;
import java.util.List;

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
            RealmWeeklySchedule schedule = findSchedule(r);
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
            RealmWeeklySchedule schedule = findSchedule(r);
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

    private static RealmWeeklySchedule findSchedule(Realm realm) {
        for (RealmPlayer player : realm.where(RealmPlayer.class).findAll()) {
            if (PlayerDataProvider.isLegacyLocalPlayerId(player.getPlayerId())) {
                continue;
            }
            RealmWeeklySchedule schedule = findScheduleByKey(realm, player.getPlayerId());
            if (schedule != null) {
                return schedule;
            }
            schedule = findScheduleByKey(realm, player.getPlayerName());
            if (schedule != null) {
                return schedule;
            }
        }
        if (!PlayerDataProvider.isLegacyLocalPlayerId(AndoWSignageApp.PLAYER_ID)) {
            return findScheduleByKey(realm, AndoWSignageApp.PLAYER_ID);
        }
        return null;
    }

    private static RealmWeeklySchedule findScheduleByKey(Realm realm, String playerId) {
        if (TextUtils.isEmpty(playerId)) {
            return null;
        }
        return realm.where(RealmWeeklySchedule.class)
                .equalTo("playerId", playerId)
                .findFirst();
    }

    private static int safeParse(String value) {
        try {
            return Integer.parseInt(value);
        } catch (Exception e) {
            return 0;
        }
    }
}

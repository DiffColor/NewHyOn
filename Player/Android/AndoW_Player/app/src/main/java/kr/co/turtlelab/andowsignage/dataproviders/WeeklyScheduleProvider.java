package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmWeeklySchedule;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;

public class WeeklyScheduleProvider {

    private WeeklyScheduleProvider() {
    }

    public static List<WeeklyScheduleDataModel> getWeeklyScheduleList() {
        List<WeeklyScheduleDataModel> list = new ArrayList<>();
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmWeeklySchedule schedule = realm.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", AndoWSignageApp.PLAYER_ID)
                    .findFirst();
            if (schedule == null) {
                return list;
            }
            RealmWeeklySchedule detached = realm.copyFromRealm(schedule);
            addModel(list, detached, "MON");
            addModel(list, detached, "TUE");
            addModel(list, detached, "WED");
            addModel(list, detached, "THU");
            addModel(list, detached, "FRI");
            addModel(list, detached, "SAT");
            addModel(list, detached, "SUN");
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
            RealmWeeklySchedule schedule = r.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", AndoWSignageApp.PLAYER_ID)
                    .findFirst();
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
            RealmWeeklySchedule schedule = r.where(RealmWeeklySchedule.class)
                    .equalTo("playerId", AndoWSignageApp.PLAYER_ID)
                    .findFirst();
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
}

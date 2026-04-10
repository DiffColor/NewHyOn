package kr.co.turtlelab.andowsignage.tools;

import android.text.TextUtils;

import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;

import java.lang.reflect.Type;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.data.realm.RealmSpecialScheduleCache;
import kr.co.turtlelab.andowsignage.data.update.UpdatePayloadModels;

public class SpecialScheduleEvaluator {

    private static final Type SCHEDULE_LIST_TYPE =
            new TypeToken<List<UpdatePayloadModels.SpecialSchedulePayload>>() { }.getType();

    private final Gson gson = new Gson();

    public ScheduleDecision evaluate(String playerId,
                                     String playerName,
                                     String fallbackPlaylist,
                                     long nowMillis) {
        String safeFallback = fallbackPlaylist == null ? "" : fallbackPlaylist;
        List<UpdatePayloadModels.SpecialSchedulePayload> schedules = loadSchedules(playerId, playerName);
        if (schedules.isEmpty()) {
            return new ScheduleDecision(safeFallback, "", "", -1L);
        }

        UpdatePayloadModels.SpecialSchedulePayload current = selectActiveSchedule(schedules, nowMillis);
        String currentPlaylist = current != null && !TextUtils.isEmpty(current.PageListName)
                ? current.PageListName
                : safeFallback;

        String nextPlaylist = "";
        long nextSwitchAtMillis = -1L;
        List<Long> candidates = collectCandidateTimes(schedules, nowMillis);
        for (Long candidate : candidates) {
            if (candidate == null || candidate <= nowMillis) {
                continue;
            }
            String resolved = resolvePlaylistAt(schedules, safeFallback, candidate + 1000L);
            if (!TextUtils.equals(currentPlaylist, resolved)) {
                nextPlaylist = resolved;
                nextSwitchAtMillis = candidate;
                break;
            }
        }

        return new ScheduleDecision(
                currentPlaylist,
                current != null ? safe(current.Id) : "",
                nextPlaylist,
                nextSwitchAtMillis);
    }

    private List<UpdatePayloadModels.SpecialSchedulePayload> loadSchedules(String playerId, String playerName) {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmSpecialScheduleCache cache = null;
            if (!TextUtils.isEmpty(playerId)) {
                cache = realm.where(RealmSpecialScheduleCache.class)
                        .equalTo("id", playerId)
                        .findFirst();
            }
            if (cache == null && !TextUtils.isEmpty(playerName)) {
                cache = realm.where(RealmSpecialScheduleCache.class)
                        .equalTo("playerName", playerName)
                        .findFirst();
            }
            if (cache == null || TextUtils.isEmpty(cache.getSchedulesJson())) {
                return Collections.emptyList();
            }
            List<UpdatePayloadModels.SpecialSchedulePayload> schedules =
                    gson.fromJson(cache.getSchedulesJson(), SCHEDULE_LIST_TYPE);
            return schedules == null ? Collections.emptyList() : schedules;
        } catch (Exception ignored) {
            return Collections.emptyList();
        } finally {
            realm.close();
        }
    }

    private UpdatePayloadModels.SpecialSchedulePayload selectActiveSchedule(
            List<UpdatePayloadModels.SpecialSchedulePayload> schedules,
            long timeMillis) {
        List<UpdatePayloadModels.SpecialSchedulePayload> active = new ArrayList<>();
        for (UpdatePayloadModels.SpecialSchedulePayload schedule : schedules) {
            if (isActive(schedule, timeMillis)) {
                active.add(schedule);
            }
        }
        if (active.isEmpty()) {
            return null;
        }
        Collections.sort(active, new Comparator<UpdatePayloadModels.SpecialSchedulePayload>() {
            @Override
            public int compare(UpdatePayloadModels.SpecialSchedulePayload left,
                               UpdatePayloadModels.SpecialSchedulePayload right) {
                return safe(left.Id).compareToIgnoreCase(safe(right.Id));
            }
        });
        return active.get(0);
    }

    private List<Long> collectCandidateTimes(List<UpdatePayloadModels.SpecialSchedulePayload> schedules,
                                             long nowMillis) {
        List<Long> candidates = new ArrayList<>();
        for (UpdatePayloadModels.SpecialSchedulePayload schedule : schedules) {
            long nextStart = findNextStartTime(schedule, nowMillis);
            if (nextStart > 0) {
                candidates.add(nextStart);
            }
            long activeEnd = findActiveEndTime(schedule, nowMillis);
            if (activeEnd > 0) {
                candidates.add(activeEnd);
            }
        }
        Collections.sort(candidates);
        return candidates;
    }

    private String resolvePlaylistAt(List<UpdatePayloadModels.SpecialSchedulePayload> schedules,
                                     String fallbackPlaylist,
                                     long timeMillis) {
        UpdatePayloadModels.SpecialSchedulePayload active = selectActiveSchedule(schedules, timeMillis);
        if (active == null || TextUtils.isEmpty(active.PageListName)) {
            return fallbackPlaylist == null ? "" : fallbackPlaylist;
        }
        return active.PageListName;
    }

    private long findNextStartTime(UpdatePayloadModels.SpecialSchedulePayload schedule, long nowMillis) {
        if (schedule == null || TextUtils.isEmpty(schedule.PageListName)) {
            return -1L;
        }
        Calendar day = Calendar.getInstance(Locale.KOREA);
        day.setTimeInMillis(nowMillis);
        day.set(Calendar.SECOND, 0);
        day.set(Calendar.MILLISECOND, 0);
        Calendar baseDay = (Calendar) day.clone();
        baseDay.set(Calendar.HOUR_OF_DAY, 0);
        baseDay.set(Calendar.MINUTE, 0);

        for (int offset = 0; offset <= 7; offset++) {
            Calendar candidateDay = (Calendar) baseDay.clone();
            candidateDay.add(Calendar.DAY_OF_YEAR, offset);
            if (!isPeriodValid(schedule, candidateDay)) {
                continue;
            }
            if (!isDayEnabled(schedule, candidateDay.get(Calendar.DAY_OF_WEEK))) {
                continue;
            }
            Calendar start = (Calendar) candidateDay.clone();
            start.set(Calendar.HOUR_OF_DAY, schedule.DisplayStartH);
            start.set(Calendar.MINUTE, schedule.DisplayStartM);
            if (start.getTimeInMillis() > nowMillis) {
                return start.getTimeInMillis();
            }
        }
        return -1L;
    }

    private long findActiveEndTime(UpdatePayloadModels.SpecialSchedulePayload schedule, long nowMillis) {
        if (!isActive(schedule, nowMillis)) {
            return -1L;
        }
        if (schedule.DisplayStartH == schedule.DisplayEndH
                && schedule.DisplayStartM == schedule.DisplayEndM) {
            return -1L;
        }
        Calendar now = Calendar.getInstance(Locale.KOREA);
        now.setTimeInMillis(nowMillis);
        boolean crossesMidnight = crossesMidnight(schedule);
        boolean usePreviousDay = false;
        if (crossesMidnight) {
            int endMinutes = (schedule.DisplayEndH * 60) + schedule.DisplayEndM;
            int currentMinutes = (now.get(Calendar.HOUR_OF_DAY) * 60) + now.get(Calendar.MINUTE);
            if (currentMinutes < endMinutes) {
                usePreviousDay = true;
            }
        }
        Calendar end = Calendar.getInstance(Locale.KOREA);
        end.setTimeInMillis(nowMillis);
        if (usePreviousDay) {
            end.add(Calendar.DAY_OF_YEAR, -1);
        }
        end.set(Calendar.HOUR_OF_DAY, schedule.DisplayEndH);
        end.set(Calendar.MINUTE, schedule.DisplayEndM);
        end.set(Calendar.SECOND, 0);
        end.set(Calendar.MILLISECOND, 0);
        if (crossesMidnight) {
            end.add(Calendar.DAY_OF_YEAR, 1);
        }
        return end.getTimeInMillis();
    }

    private boolean isActive(UpdatePayloadModels.SpecialSchedulePayload schedule, long timeMillis) {
        if (schedule == null || TextUtils.isEmpty(schedule.PageListName)) {
            return false;
        }
        Calendar now = Calendar.getInstance(Locale.KOREA);
        now.setTimeInMillis(timeMillis);
        boolean crossesMidnight = crossesMidnight(schedule);
        boolean usePreviousDay = false;
        if (crossesMidnight) {
            int endMinutes = (schedule.DisplayEndH * 60) + schedule.DisplayEndM;
            int currentMinutes = (now.get(Calendar.HOUR_OF_DAY) * 60) + now.get(Calendar.MINUTE);
            if (currentMinutes < endMinutes) {
                usePreviousDay = true;
            }
        }

        Calendar effectiveDate = (Calendar) now.clone();
        if (usePreviousDay) {
            effectiveDate.add(Calendar.DAY_OF_YEAR, -1);
        }

        if (!isPeriodValid(schedule, effectiveDate)) {
            return false;
        }

        if (!isDayEnabled(schedule, effectiveDate.get(Calendar.DAY_OF_WEEK))) {
            return false;
        }

        return isTimeValid(schedule, now);
    }

    private boolean isPeriodValid(UpdatePayloadModels.SpecialSchedulePayload schedule, Calendar date) {
        if (schedule == null || !schedule.IsPeriodEnable) {
            return true;
        }
        if (schedule.PeriodStartYear <= 0 || schedule.PeriodEndYear <= 0) {
            return true;
        }
        try {
            Calendar start = Calendar.getInstance(Locale.KOREA);
            start.clear();
            start.set(schedule.PeriodStartYear, schedule.PeriodStartMonth - 1, schedule.PeriodStartDay, 0, 0, 0);
            Calendar end = Calendar.getInstance(Locale.KOREA);
            end.clear();
            end.set(schedule.PeriodEndYear, schedule.PeriodEndMonth - 1, schedule.PeriodEndDay, 23, 59, 59);
            long compareMillis = date.getTimeInMillis();
            return compareMillis >= start.getTimeInMillis() && compareMillis <= end.getTimeInMillis();
        } catch (Exception ignored) {
            return true;
        }
    }

    private boolean isTimeValid(UpdatePayloadModels.SpecialSchedulePayload schedule, Calendar now) {
        int start = (schedule.DisplayStartH * 60) + schedule.DisplayStartM;
        int end = (schedule.DisplayEndH * 60) + schedule.DisplayEndM;
        int current = (now.get(Calendar.HOUR_OF_DAY) * 60) + now.get(Calendar.MINUTE);
        if (start == end) {
            return true;
        }
        if (end > start) {
            return current >= start && current < end;
        }
        return current >= start || current < end;
    }

    private boolean isDayEnabled(UpdatePayloadModels.SpecialSchedulePayload schedule, int dayOfWeek) {
        switch (dayOfWeek) {
            case Calendar.SUNDAY:
                return schedule.DayOfWeek1;
            case Calendar.MONDAY:
                return schedule.DayOfWeek2;
            case Calendar.TUESDAY:
                return schedule.DayOfWeek3;
            case Calendar.WEDNESDAY:
                return schedule.DayOfWeek4;
            case Calendar.THURSDAY:
                return schedule.DayOfWeek5;
            case Calendar.FRIDAY:
                return schedule.DayOfWeek6;
            case Calendar.SATURDAY:
                return schedule.DayOfWeek7;
            default:
                return false;
        }
    }

    private boolean crossesMidnight(UpdatePayloadModels.SpecialSchedulePayload schedule) {
        return schedule.DisplayEndH < schedule.DisplayStartH
                || (schedule.DisplayEndH == schedule.DisplayStartH
                && schedule.DisplayEndM < schedule.DisplayStartM);
    }

    private String safe(String value) {
        return value == null ? "" : value;
    }

    public static final class ScheduleDecision {
        private final String resolvedPlaylistName;
        private final String scheduleId;
        private final String nextPlaylistName;
        private final long nextSwitchAtMillis;

        ScheduleDecision(String resolvedPlaylistName,
                         String scheduleId,
                         String nextPlaylistName,
                         long nextSwitchAtMillis) {
            this.resolvedPlaylistName = resolvedPlaylistName == null ? "" : resolvedPlaylistName;
            this.scheduleId = scheduleId == null ? "" : scheduleId;
            this.nextPlaylistName = nextPlaylistName == null ? "" : nextPlaylistName;
            this.nextSwitchAtMillis = nextSwitchAtMillis;
        }

        public String getResolvedPlaylistName() {
            return resolvedPlaylistName;
        }

        public String getScheduleId() {
            return scheduleId;
        }

        public String getNextPlaylistName() {
            return nextPlaylistName;
        }

        public long getNextSwitchAtMillis() {
            return nextSwitchAtMillis;
        }
    }
}

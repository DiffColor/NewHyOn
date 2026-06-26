import { describe, expect, it } from 'vitest';
import {
  buildManifestFromRemoteSchedulePlaylist,
  evaluateRemoteSchedule,
  loadRemoteSchedule,
  saveRemoteScheduleFromUpdatePayload,
} from '../src/app/remote-schedule';

describe('remote schedule', () => {
  it('실제 서버 updateschedule payload의 Schedule 원본을 로컬에 저장한다', () => {
    const snapshot = saveRemoteScheduleFromUpdatePayload({
      Schedule: {
        PlayerId: 'player-guid-1',
        PlayerName: 'tizen',
        GeneratedAt: '2026-06-17 03:01:17',
        SpecialSchedules: [],
        Playlists: {},
        ContentPeriods: {},
      },
    });

    expect(snapshot).toMatchObject({
      generatedAt: '2026-06-17 03:01:17',
      playerId: 'player-guid-1',
      playerName: 'tizen',
      specialScheduleCount: 0,
      playlistScheduleCount: 0,
      contentPeriodCount: 0,
    });
    expect(loadRemoteSchedule()).toMatchObject({
      generatedAt: '2026-06-17 03:01:17',
      playerId: 'player-guid-1',
    });
  });

  it('배열/객체 형태의 스케줄 컬렉션 수를 모두 계산한다', () => {
    const snapshot = saveRemoteScheduleFromUpdatePayload({
      Schedule: {
        SpecialSchedules: [{ id: 'special-1' }],
        Playlists: { morning: {}, evening: {} },
        ContentPeriods: [{ id: 'content-period-1' }],
      },
    });

    expect(snapshot.specialScheduleCount).toBe(1);
    expect(snapshot.playlistScheduleCount).toBe(2);
    expect(snapshot.contentPeriodCount).toBe(1);
  });

  it('Schedule이 없으면 명령 실패로 처리할 수 있게 예외를 낸다', () => {
    expect(() => saveRemoteScheduleFromUpdatePayload({})).toThrow('updateschedule payload');
  });

  it('현재 시각에 활성화된 SpecialSchedule의 playlist를 결정한다', () => {
    const snapshot = saveRemoteScheduleFromUpdatePayload({
      Schedule: {
        GeneratedAt: '2026-06-22 08:50:00',
        SpecialSchedules: [
          {
            Id: 'schedule-1',
            PageListName: 'scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        ],
        Playlists: [
          {
            PlaylistName: 'scheduled-list',
            PageList: { PLI_PageListName: 'scheduled-list' },
            Pages: [{ PIC_PageName: 'scheduled-page', PIC_PlaytimeSecond: 10, PIC_Elements: [] }],
          },
        ],
      },
    });

    const decision = evaluateRemoteSchedule(snapshot, new Date(2026, 5, 22, 9, 1), 'default-list');
    expect(decision).toMatchObject({
      playlistName: 'scheduled-list',
      scheduleId: 'schedule-1',
      isFromSchedule: true,
    });
    expect(evaluateRemoteSchedule(snapshot, new Date(2026, 5, 22, 18, 0), 'default-list')).toMatchObject({
      playlistName: 'default-list',
      isFromSchedule: false,
    });
  });

  it('Schedule.Playlists의 PageList/Pages로 재생 manifest를 만든다', () => {
    const snapshot = saveRemoteScheduleFromUpdatePayload({
      Schedule: {
        SpecialSchedules: [],
        Playlists: [
          {
            PlaylistName: 'scheduled-list',
            PageList: { PLI_PageListName: 'scheduled-list' },
            Pages: [{ PIC_PageName: 'scheduled-page', PIC_PlaytimeSecond: 10, PIC_Elements: [] }],
          },
        ],
      },
    });

    expect(buildManifestFromRemoteSchedulePlaylist(snapshot, 'scheduled-list', false)).toMatchObject({
      playlistName: 'scheduled-list',
      preserveAspectRatio: false,
      pages: [expect.objectContaining({ PIC_PageName: 'scheduled-page' })],
    });
  });

  it('Schedule 컬렉션이 객체 형태여도 예약 playlist manifest를 만든다', () => {
    const snapshot = saveRemoteScheduleFromUpdatePayload({
      Schedule: {
        SpecialSchedules: {
          schedule1: {
            Id: 'schedule-1',
            PageListName: 'scheduled-list',
            DayOfWeek1: false,
            DayOfWeek2: true,
            DayOfWeek3: false,
            DayOfWeek4: false,
            DayOfWeek5: false,
            DayOfWeek6: false,
            DayOfWeek7: false,
            IsPeriodEnable: false,
            DisplayStartH: 9,
            DisplayStartM: 0,
            DisplayEndH: 18,
            DisplayEndM: 0,
          },
        },
        Playlists: {
          scheduledList: {
            PlaylistName: 'scheduled-list',
            PageList: { PLI_PageListName: 'scheduled-list' },
            Pages: [{ PIC_PageName: 'scheduled-page', PIC_PlaytimeSecond: 10, PIC_Elements: [] }],
          },
        },
      },
    });

    expect(evaluateRemoteSchedule(snapshot, new Date(2026, 5, 22, 9, 1), 'default-list')).toMatchObject({
      playlistName: 'scheduled-list',
      scheduleId: 'schedule-1',
      isFromSchedule: true,
    });
    expect(buildManifestFromRemoteSchedulePlaylist(snapshot, 'scheduled-list', false)).toMatchObject({
      playlistName: 'scheduled-list',
      pages: [expect.objectContaining({ PIC_PageName: 'scheduled-page' })],
    });
  });
});

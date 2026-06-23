import { describe, expect, it } from 'vitest';
import { loadRemoteSchedule, saveRemoteScheduleFromUpdatePayload } from '../src/app/remote-schedule';

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
});

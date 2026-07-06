import { afterEach, describe, expect, it, vi } from 'vitest';
import { AvplaySession, createAvplaySessionPair } from '../src/player/avplay-session';
import { RingLogger } from '../src/core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../src/domain/page-plan';

function createPlayer(): AVPlayApi {
  return {
    open: vi.fn(),
    prepare: vi.fn(),
    prepareAsync: vi.fn((successCallback: () => void) => successCallback()),
    play: vi.fn(),
    pause: vi.fn(),
    stop: vi.fn(),
    close: vi.fn(),
    setListener: vi.fn(),
    setDisplayRect: vi.fn(),
    setDisplayMethod: vi.fn(),
    setStreamingProperty: vi.fn(),
    setVideoStillMode: vi.fn(),
    setLooping: vi.fn(),
    disableAudioStream: vi.fn(),
    enableAudioStream: vi.fn(),
    getState: vi.fn(() => 'IDLE'),
  };
}

function createVideoItem(fileName = 'video.mp4'): SeamlessContentItem {
  return {
    source: {
      CIF_FileName: fileName,
      CIF_FileFullPath: `https://example.com/${fileName}`,
      CIF_ContentType: 'Video',
    },
    id: fileName,
    name: fileName,
    sourceUrl: `https://example.com/${fileName}`,
    contentType: 'Video',
    durationSeconds: 10,
    actualDurationSeconds: 10,
    shouldLoop: false,
    transitionByTimer: true,
    loopDisableAfterEndCount: 0,
    transitionEndEventCount: 0,
  };
}

function createSlotPlan(): SeamlessSlotPlan {
  return {
    elementName: 'video',
    isMuted: true,
    width: 1920,
    height: 1080,
    left: 0,
    top: 0,
    zIndex: 0,
    items: [],
  };
}

describe('AvplaySession', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('AVPlayStore 플레이어 네 개로 영상 슬롯용 고정 세션을 만든다', () => {
    const getPlayer = vi.fn(createPlayer);
    window.webapis = {
      avplay: createPlayer(),
      avplaystore: {
        getPlayer,
      },
    };

    const session = createAvplaySessionPair(0, document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    expect(session).toBeInstanceOf(AvplaySession);
    expect(getPlayer).toHaveBeenCalledTimes(4);
    expect(document.body.querySelectorAll('object.avplay-object')).toHaveLength(4);
  });

  it('재생 시작 시 AVPlay display method에 비율 유지 설정을 적용한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slotElement = document.createElement('section');
    document.body.appendChild(slotElement);

    await session.play(createVideoItem(), createSlotPlan(), slotElement, true, vi.fn());
    expect(playerA.prepare).toHaveBeenCalledTimes(1);
    expect(playerA.prepareAsync).not.toHaveBeenCalled();
    expect(playerA.setDisplayMethod).toHaveBeenCalledWith('PLAYER_DISPLAY_MODE_LETTER_BOX');

    await session.play(createVideoItem(), createSlotPlan(), slotElement, false, vi.fn());
    expect(playerB.prepare).toHaveBeenCalledTimes(1);
    expect(playerB.prepareAsync).not.toHaveBeenCalled();
    expect(playerB.setDisplayMethod).toHaveBeenCalledWith('PLAYER_DISPLAY_MODE_FULL_SCREEN');
  });

  it('런타임 화면 비율 유지 OFF는 IDLE 상태 응답이어도 FULL_SCREEN을 재적용한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slotElement = document.createElement('section');
    document.body.appendChild(slotElement);

    await session.play(createVideoItem(), createSlotPlan(), slotElement, true, vi.fn());
    session.applyDisplayMethod(false);

    expect(playerA.setDisplayRect).toHaveBeenCalled();
    expect(playerA.setDisplayMethod).toHaveBeenLastCalledWith('PLAYER_DISPLAY_MODE_FULL_SCREEN');
    expect(playerB.setDisplayMethod).toHaveBeenLastCalledWith('PLAYER_DISPLAY_MODE_FULL_SCREEN');
  });

  it('런타임 화면 비율 유지 ON은 LETTER_BOX를 재적용한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slotElement = document.createElement('section');
    document.body.appendChild(slotElement);

    await session.play(createVideoItem(), createSlotPlan(), slotElement, false, vi.fn());
    session.applyDisplayMethod(true);

    expect(playerA.setDisplayMethod).toHaveBeenLastCalledWith('PLAYER_DISPLAY_MODE_LETTER_BOX');
    expect(playerB.setDisplayMethod).toHaveBeenLastCalledWith('PLAYER_DISPLAY_MODE_LETTER_BOX');
  });

  it('AVPlay object는 슬롯 표시 상태와 z-index를 따라간다', async () => {
    const host = document.createElement('div');
    const slotElement = document.createElement('section');
    const playerA = createPlayer();
    const playerB = createPlayer();
    const slotPlan = {
      ...createSlotPlan(),
      zIndex: 12,
    };
    document.body.append(host, slotElement);
    const laneA = document.createElement('object');
    const laneB = document.createElement('object');
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: laneA },
      { player: playerB, objectElement: laneB },
    ], host, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    expect(laneA.style.visibility).toBe('hidden');

    slotElement.style.visibility = 'hidden';
    await session.play(createVideoItem(), slotPlan, slotElement, false, vi.fn());

    expect(laneA.style.zIndex).toBe('14');
    expect(laneA.style.visibility).toBe('hidden');

    slotElement.style.visibility = '';
    session.applyDisplayRect(slotPlan, slotElement);

    expect(laneA.style.visibility).toBe('visible');

    session.stop();
    expect(laneA.style.visibility).toBe('hidden');
    host.remove();
    slotElement.remove();
  });

  it('재생 hot path는 샘플처럼 loop, still, audio 제어를 호출하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    playerA.getState = vi.fn(() => 'PLAYING');
    playerB.getState = vi.fn(() => 'READY');
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const loopItem = {
      ...createVideoItem(),
      shouldLoop: true,
    };

    await session.play(loopItem, createSlotPlan(), document.createElement('section'), false, vi.fn());
    await session.play(createVideoItem(), createSlotPlan(), document.createElement('section'), false, vi.fn());

    expect(playerA.setVideoStillMode).not.toHaveBeenCalled();
    expect(playerA.setLooping).not.toHaveBeenCalled();
    expect(playerA.disableAudioStream).not.toHaveBeenCalled();
    expect(playerB.setVideoStillMode).not.toHaveBeenCalled();
    expect(playerB.setLooping).not.toHaveBeenCalled();
    expect(playerB.disableAudioStream).not.toHaveBeenCalled();
  });

  it('전환 후 현재 lane만 위에 두고 이전 lane은 숨긴다', async () => {
    const host = document.createElement('div');
    const slotElement = document.createElement('section');
    const playerA = createPlayer();
    const playerB = createPlayer();
    const laneA = document.createElement('object');
    const laneB = document.createElement('object');
    const slotPlan = {
      ...createSlotPlan(),
      zIndex: 20,
    };
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: laneA },
      { player: playerB, objectElement: laneB },
    ], host, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await session.play(createVideoItem('first.mp4'), slotPlan, slotElement, false, vi.fn());
    await session.play(createVideoItem('second.mp4'), slotPlan, slotElement, false, vi.fn());
    expect(laneB.style.zIndex).toBe('22');
    expect(laneB.style.visibility).toBe('visible');
    expect(laneA.style.zIndex).toBe('19');
    expect(laneA.style.visibility).toBe('hidden');

    await session.play(createVideoItem('third.mp4'), slotPlan, slotElement, false, vi.fn());
    expect(laneA.style.zIndex).toBe('22');
    expect(laneA.style.visibility).toBe('visible');
    expect(laneB.style.zIndex).toBe('19');
    expect(laneB.style.visibility).toBe('hidden');
  });

  it('영상 전환 시 샘플처럼 현재 lane을 먼저 stop하고 다음 lane을 prepare/play한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const callOrder: string[] = [];
    playerA.getState = vi.fn(() => 'PLAYING');
    playerA.stop = vi.fn(() => {
      callOrder.push('a.stop');
    });
    playerB.prepare = vi.fn(() => {
      callOrder.push('b.prepare');
    });
    playerB.play = vi.fn(() => {
      callOrder.push('b.play');
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slot = createSlotPlan();
    const slotElement = document.createElement('section');
    const first = createVideoItem('first.mp4');
    const second = createVideoItem('second.mp4');

    await session.play(first, slot, slotElement, false, vi.fn());
    callOrder.splice(0);

    await session.play(second, slot, slotElement, false, vi.fn());

    expect(callOrder).toEqual(['a.stop', 'b.prepare', 'b.play']);
    expect(playerB.play).toHaveBeenCalledTimes(1);
  });

  it('사전 준비된 영상 lane은 화면 밖 준비 후 전환 시 rect와 play만 실행한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const callOrder: string[] = [];
    let playerAState = 'IDLE';
    let playerBState = 'IDLE';
    const laneB = document.createElement('object');
    playerA.getState = vi.fn(() => playerAState);
    playerB.getState = vi.fn(() => playerBState);
    playerA.play = vi.fn(() => {
      callOrder.push('a.play');
      playerAState = 'PLAYING';
    });
    playerA.stop = vi.fn(() => {
      callOrder.push('a.stop');
      playerAState = 'IDLE';
    });
    playerB.open = vi.fn(() => {
      callOrder.push('b.open');
      playerBState = 'IDLE';
    });
    playerB.prepare = vi.fn(() => {
      callOrder.push('b.prepare');
      playerBState = 'READY';
    });
    playerB.setDisplayRect = vi.fn(() => {
      callOrder.push('b.rect');
    });
    playerB.play = vi.fn(() => {
      callOrder.push('b.play');
      playerBState = 'PLAYING';
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: laneB },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slot = createSlotPlan();
    const slotElement = document.createElement('section');

    await session.play(createVideoItem('first.mp4'), slot, slotElement, false, vi.fn());
    callOrder.length = 0;

    session.prepareNextVideo(createVideoItem('second.mp4'));
    expect(callOrder).toEqual([
      'b.open',
      'b.rect',
      'b.prepare',
    ]);
    callOrder.length = 0;

    await session.play(createVideoItem('second.mp4'), slot, slotElement, false, vi.fn());

    expect(callOrder).toEqual([
      'b.rect',
      'b.play',
      'a.stop',
    ]);
    expect(playerB.open).toHaveBeenCalledTimes(1);
    expect(playerB.prepare).toHaveBeenCalledTimes(1);
  });

  it('사전 준비 prepare 실패 lane은 close로 초기화한 뒤 다음 재생에 재사용한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    let playerAState = 'IDLE';
    let playerBState = 'IDLE';
    playerA.getState = vi.fn(() => playerAState);
    playerB.getState = vi.fn(() => playerBState);
    playerA.play = vi.fn(() => {
      playerAState = 'PLAYING';
    });
    playerA.stop = vi.fn(() => {
      playerAState = 'IDLE';
    });
    playerB.open = vi.fn(() => {
      playerBState = 'IDLE';
    });
    playerB.prepare = vi.fn()
      .mockImplementationOnce(() => {
        throw new Error('prepare failed');
      })
      .mockImplementationOnce(() => {
        playerBState = 'READY';
      });
    playerB.close = vi.fn(() => {
      playerBState = 'NONE';
    });
    playerB.play = vi.fn(() => {
      playerBState = 'PLAYING';
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slot = createSlotPlan();
    const slotElement = document.createElement('section');

    await session.play(createVideoItem('current.mp4'), slot, slotElement, false, vi.fn());

    expect(() => session.prepareNextVideo(createVideoItem('next.mp4'))).toThrow('AVPlay prepare 오류');
    expect(playerB.close).toHaveBeenCalledTimes(1);
    expect(session.debugSnapshot().lanes[1]?.role).toBe('idle');

    await session.play(createVideoItem('next.mp4'), slot, slotElement, false, vi.fn());

    expect(playerB.open).toHaveBeenCalledTimes(2);
    expect(playerB.prepare).toHaveBeenCalledTimes(2);
    expect(playerB.play).toHaveBeenCalledTimes(1);
  });

  it('설정 저장 중 display rect 재적용은 준비 lane을 화면으로 올리지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    let playerAState = 'IDLE';
    let playerBState = 'IDLE';
    const laneB = document.createElement('object');
    playerA.getState = vi.fn(() => playerAState);
    playerB.getState = vi.fn(() => playerBState);
    playerA.play = vi.fn(() => {
      playerAState = 'PLAYING';
    });
    playerB.prepare = vi.fn(() => {
      playerBState = 'READY';
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: laneB },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slot = createSlotPlan();
    const slotElement = document.createElement('section');

    await session.play(createVideoItem('current.mp4'), slot, slotElement, false, vi.fn());
    session.prepareNextVideo(createVideoItem('prepared.mp4'));
    const preparedRectCalls = vi.mocked(playerB.setDisplayRect).mock.calls.length;

    session.applyDisplayRect(slot, slotElement);
    session.applyDisplayMethod(true);

    expect(playerA.setDisplayRect).toHaveBeenCalled();
    expect(playerB.setDisplayRect).toHaveBeenCalledTimes(preparedRectCalls);
    expect(laneB.style.width).toBe('1px');
    expect(laneB.style.height).toBe('1px');
    expect(session.debugSnapshot().lanes[1]?.role).toBe('next-content');
  });

  it('다음 컨텐츠와 다음 전환 컨텐츠를 서로 다른 lane에 준비하고 전환 컨텐츠 승격 시 기존 다음 컨텐츠를 폐기한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const playerC = createPlayer();
    const playerD = createPlayer();
    let playerAState = 'IDLE';
    let playerBState = 'IDLE';
    let playerCState = 'IDLE';
    let playerDState = 'IDLE';
    playerA.getState = vi.fn(() => playerAState);
    playerB.getState = vi.fn(() => playerBState);
    playerC.getState = vi.fn(() => playerCState);
    playerD.getState = vi.fn(() => playerDState);
    playerA.play = vi.fn(() => {
      playerAState = 'PLAYING';
    });
    playerA.stop = vi.fn(() => {
      playerAState = 'IDLE';
    });
    playerB.open = vi.fn(() => {
      playerBState = 'IDLE';
    });
    playerB.prepare = vi.fn(() => {
      playerBState = 'READY';
    });
    playerB.stop = vi.fn(() => {
      playerBState = 'IDLE';
    });
    playerC.open = vi.fn(() => {
      playerCState = 'IDLE';
    });
    playerC.prepare = vi.fn(() => {
      playerCState = 'READY';
    });
    playerC.play = vi.fn(() => {
      playerCState = 'PLAYING';
    });
    playerD.open = vi.fn(() => {
      playerDState = 'IDLE';
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
      { player: playerC, objectElement: document.createElement('object') },
      { player: playerD, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slot = createSlotPlan();
    const slotElement = document.createElement('section');

    await session.play(createVideoItem('current.mp4'), slot, slotElement, false, vi.fn());
    session.prepareNextVideo(createVideoItem('next-content.mp4'), 'next-content');
    session.prepareNextVideo(createVideoItem('next-schedule.mp4'), 'next-transition-content');

    expect(session.debugSnapshot().lanes.map((lane) => lane.role)).toEqual([
      'current',
      'next-content',
      'next-transition-content',
      'idle',
    ]);

    await session.play(createVideoItem('next-schedule.mp4'), slot, slotElement, false, vi.fn());

    expect(playerC.open).toHaveBeenCalledTimes(1);
    expect(playerC.prepare).toHaveBeenCalledTimes(1);
    expect(playerC.play).toHaveBeenCalledTimes(1);
    expect(playerA.stop).toHaveBeenCalledTimes(1);
    expect(playerB.stop).toHaveBeenCalledTimes(1);
    expect(session.debugSnapshot().lanes.map((lane) => lane.role)).toEqual([
      'idle',
      'idle',
      'current',
      'idle',
    ]);
  });

  it('종료 이벤트 핸들러가 완료를 보류하면 현재 lane을 stop하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const listenerRef: { current: AVPlayListener | null } = { current: null };
    const onEnded = vi.fn();
    playerA.getState = vi.fn(() => 'PLAYING');
    playerA.setListener = vi.fn((nextListener) => {
      listenerRef.current = nextListener;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded,
      onError: vi.fn(),
    });

    await session.play(createVideoItem('deferred-completion.mp4'), createSlotPlan(), document.createElement('section'), false, () => false);
    playerA.setVideoStillMode = vi.fn();
    playerA.stop = vi.fn();

    listenerRef.current?.onstreamcompleted?.();
    await Promise.resolve();
    await Promise.resolve();

    expect(playerA.setVideoStillMode).not.toHaveBeenCalledWith('true');
    expect(playerA.stop).not.toHaveBeenCalled();
    expect(onEnded).not.toHaveBeenCalled();
  });

  it('종료 이벤트 핸들러가 이미 다음 콘텐츠로 전환했으면 같은 lane을 다시 stop하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const listenerRef: { current: AVPlayListener | null } = { current: null };
    playerA.getState = vi.fn(() => 'PLAYING');
    playerA.setListener = vi.fn((nextListener) => {
      listenerRef.current = nextListener;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await session.play(createVideoItem('video-before-image.mp4'), createSlotPlan(), document.createElement('section'), false, () => {
      session.hide();
      return true;
    });
    playerA.setVideoStillMode = vi.fn();
    playerA.stop = vi.fn();

    listenerRef.current?.onstreamcompleted?.();
    await Promise.resolve();
    await Promise.resolve();

    expect(playerA.setVideoStillMode).not.toHaveBeenCalledWith('true');
    expect(playerA.stop).not.toHaveBeenCalled();
  });

  it('stream completed 이후 완료 상태 lane에는 stop을 호출하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const listenerRef: { current: AVPlayListener | null } = { current: null };
    playerA.setListener = vi.fn((nextListener) => {
      listenerRef.current = nextListener;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await session.play(createVideoItem('ended.mp4'), createSlotPlan(), document.createElement('section'), false, () => true);
    playerA.getState = vi.fn(() => 'FINISHED');
    playerA.stop = vi.fn();

    listenerRef.current?.onstreamcompleted?.();
    await Promise.resolve();
    await Promise.resolve();

    expect(playerA.stop).not.toHaveBeenCalled();
  });

  it('정지된 lane을 새 영상으로 재사용할 때 샘플처럼 close 없이 stop 후 open한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const callOrder: string[] = [];
    let playerAState = 'IDLE';
    playerA.getState = vi.fn(() => playerAState);
    playerA.open = vi.fn(() => {
      callOrder.push('a.open');
      playerAState = 'READY';
    });
    playerA.play = vi.fn(() => {
      callOrder.push('a.play');
      playerAState = 'PLAYING';
    });
    playerA.stop = vi.fn(() => {
      callOrder.push('a.stop');
      playerAState = 'READY';
    });
    playerA.close = vi.fn(() => {
      callOrder.push('a.close');
      playerAState = 'IDLE';
    });

    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await session.play(createVideoItem('first.mp4'), createSlotPlan(), document.createElement('section'), false, vi.fn());
    await session.play(createVideoItem('second.mp4'), createSlotPlan(), document.createElement('section'), false, vi.fn());
    callOrder.length = 0;

    await session.play(createVideoItem('third.mp4'), createSlotPlan(), document.createElement('section'), false, vi.fn());

    expect(callOrder.slice(0, 3)).toEqual(['a.stop', 'a.open', 'a.play']);
    expect(playerA.close).not.toHaveBeenCalled();
    expect(playerA.play).toHaveBeenCalledTimes(2);
  });

  it('완료된 현재 lane은 즉시 재사용하지 않고 다른 lane으로 다음 영상을 연다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const callOrder: string[] = [];
    let playerAState = 'IDLE';
    let playerBState = 'IDLE';
    playerA.getState = vi.fn(() => playerAState);
    playerB.getState = vi.fn(() => playerBState);
    playerA.open = vi.fn(() => {
      callOrder.push('a.open');
      playerAState = 'IDLE';
    });
    playerA.prepare = vi.fn(() => {
      callOrder.push('a.prepare');
      playerAState = 'READY';
    });
    playerA.play = vi.fn(() => {
      callOrder.push('a.play');
      playerAState = 'PLAYING';
    });
    playerA.stop = vi.fn(() => {
      callOrder.push('a.stop');
      playerAState = 'IDLE';
    });
    playerB.open = vi.fn(() => {
      callOrder.push('b.open');
      playerBState = 'IDLE';
    });
    playerB.prepare = vi.fn(() => {
      callOrder.push('b.prepare');
      playerBState = 'READY';
    });
    playerB.play = vi.fn(() => {
      callOrder.push('b.play');
      playerBState = 'PLAYING';
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await session.play(createVideoItem('first.mp4'), createSlotPlan(), document.createElement('section'), false, vi.fn());
    session.stopCurrentForCompletedStream();
    callOrder.length = 0;

    await session.play(createVideoItem('second.mp4'), createSlotPlan(), document.createElement('section'), false, vi.fn());

    expect(callOrder.slice(0, 4)).toEqual(['a.stop', 'b.open', 'b.prepare', 'b.play']);
    expect(playerA.open).toHaveBeenCalledTimes(1);
    expect(playerB.open).toHaveBeenCalledTimes(1);
  });

  it('첫 프레임 대기 옵션이 들어와도 샘플 흐름처럼 play 직후 완료한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const listenerRef: { current: AVPlayListener | null } = { current: null };
    const laneA = document.createElement('object');
    playerA.setListener = vi.fn((nextListener) => {
      listenerRef.current = nextListener;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: laneA },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await expect(session.play(
      createVideoItem('ready.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
      { waitForFirstFrame: true },
    )).resolves.toEqual({ durationMs: null });
    expect(playerA.play).toHaveBeenCalledTimes(1);
    expect(laneA.style.visibility).toBe('visible');
    listenerRef.current?.oncurrentplaytime?.(0);
  });

  it('첫 프레임 이벤트가 없어도 play 직후 영상을 표출한다', async () => {
    vi.useFakeTimers();
    const playerA = createPlayer();
    const playerB = createPlayer();
    const laneA = document.createElement('object');
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: laneA },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await expect(session.play(
      createVideoItem('timeout.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
      { waitForFirstFrame: true },
    )).resolves.toEqual({ durationMs: null });
    expect(laneA.style.visibility).toBe('visible');
  });

  it('첫 프레임 대기 중 AVPlay 오류가 와도 재생 중인 영상을 강제로 표출한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const laneA = document.createElement('object');
    const listenerRef: { current: AVPlayListener | null } = { current: null };
    const onError = vi.fn();
    playerA.setListener = vi.fn((nextListener) => {
      listenerRef.current = nextListener;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: laneA },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError,
    });

    const playPromise = session.play(
      createVideoItem('error-before-frame.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
      { waitForFirstFrame: true },
    );
    await Promise.resolve();

    listenerRef.current?.onerror?.({ message: 'decode delayed' });
    await expect(playPromise).resolves.toEqual({ durationMs: null });

    expect(onError).toHaveBeenCalledTimes(1);
    expect(playerA.play).toHaveBeenCalledTimes(1);
    expect(laneA.style.visibility).toBe('visible');
  });

  it('display rect 적용이 실패해도 AVPlay 재생은 계속 진행한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    playerA.setDisplayRect = vi.fn(() => {
      throw new Error('rect unavailable');
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await expect(session.play(
      createVideoItem('rect-failed.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
    )).resolves.toEqual({ durationMs: null });

    expect(playerA.play).toHaveBeenCalledTimes(1);
  });

  it('prepare 실패 시 play까지 진행하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    playerA.prepare = vi.fn(() => {
      throw new Error('prepare failed');
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    await expect(session.play(
      createVideoItem('prepare-failed.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
    )).rejects.toThrow('AVPlay prepare 오류');
    expect(playerA.play).not.toHaveBeenCalled();
  });

});

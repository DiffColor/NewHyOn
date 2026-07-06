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

  it('AVPlayStore 플레이어 두 개로 고정 세션 페어 하나를 만든다', () => {
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
    expect(getPlayer).toHaveBeenCalledTimes(2);
    expect(document.body.querySelectorAll('object.avplay-object')).toHaveLength(2);
  });

  it('화면 비율 유지 설정에 맞춰 AVPlay display method를 적용한다', async () => {
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
    expect(playerA.prepareAsync).toHaveBeenCalledTimes(1);
    expect(playerA.prepare).not.toHaveBeenCalled();
    expect(playerA.setDisplayMethod).toHaveBeenLastCalledWith('PLAYER_DISPLAY_MODE_LETTER_BOX');

    await session.play(createVideoItem(), createSlotPlan(), slotElement, false, vi.fn());
    expect(playerB.prepareAsync).toHaveBeenCalledTimes(1);
    expect(playerB.prepare).not.toHaveBeenCalled();
    expect(playerB.setDisplayMethod).toHaveBeenLastCalledWith('PLAYER_DISPLAY_MODE_FULL_SCREEN');
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

  it('루프 영상은 검은 프레임 방지를 위해 still mode를 켠다', async () => {
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

    expect(playerA.setVideoStillMode).toHaveBeenCalledWith('true');
    expect(playerA.setLooping).toHaveBeenCalledWith(true);
    expect(playerB.setVideoStillMode).toHaveBeenCalledWith('false');
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

  it('영상 전환 시 다음 lane 준비 전에는 현재 lane을 멈추지 않고 play 후 이전 lane을 정지한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const callOrder: string[] = [];
    playerA.getState = vi.fn(() => 'PLAYING');
    playerA.setVideoStillMode = vi.fn((mode) => {
      callOrder.push(`a.still:${mode}`);
    });
    playerA.disableAudioStream = vi.fn(() => {
      callOrder.push('a.disableAudioStream');
    });
    playerA.stop = vi.fn(() => {
      callOrder.push('a.stop');
    });
    playerB.prepareAsync = vi.fn((successCallback: () => void) => {
      callOrder.push('b.prepareAsync');
      successCallback();
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

    expect(callOrder[0]).toBe('b.prepareAsync');
    expect(callOrder[1]).toBe('b.play');
    expect(callOrder[2]).toBe('a.disableAudioStream');
    expect(callOrder[3]).toBe('a.still:true');
    expect(callOrder[4]).toBe('a.stop');
    expect(playerB.play).toHaveBeenCalledTimes(1);
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

  it('정지된 lane을 새 영상으로 재사용하기 전 close로 IDLE 전환을 보장한다', async () => {
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

    expect(callOrder.slice(0, 3)).toEqual(['a.stop', 'a.close', 'a.open']);
    expect(playerA.play).toHaveBeenCalledTimes(2);
  });

  it('첫 프레임 대기 옵션은 재생 시간 이벤트 전까지 play 완료를 보류한다', async () => {
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

    let resolved = false;
    const playPromise = session.play(
      createVideoItem('ready.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
      { waitForFirstFrame: true },
    ).then(() => {
      resolved = true;
    });
    await Promise.resolve();

    expect(resolved).toBe(false);
    expect(playerA.play).toHaveBeenCalledTimes(1);
    expect(laneA.style.visibility).toBe('hidden');
    listenerRef.current?.oncurrentplaytime?.(0);
    await playPromise;

    expect(resolved).toBe(true);
    expect(laneA.style.visibility).toBe('visible');
  });

  it('첫 프레임 대기는 500ms 안에 재생 시간 이벤트가 없으면 영상을 강제로 표출한다', async () => {
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

    const playPromise = session.play(
      createVideoItem('timeout.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
      { waitForFirstFrame: true },
    );
    let settled = false;
    void playPromise.then(() => {
      settled = true;
    });

    await vi.advanceTimersByTimeAsync(499);
    await Promise.resolve();
    expect(settled).toBe(false);
    expect(laneA.style.visibility).toBe('hidden');

    await vi.advanceTimersByTimeAsync(1);
    await playPromise;
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

  it('현재 영상 첫 프레임 대기 중 다음 lane prepare가 들어와도 현재 play를 폐기하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const listenerRef: { current: AVPlayListener | null } = { current: null };
    const nextPrepareGate = {
      resolve: null as (() => void) | null,
    };
    playerA.setListener = vi.fn((nextListener) => {
      listenerRef.current = nextListener;
    });
    playerB.prepareAsync = vi.fn((successCallback: () => void) => {
      nextPrepareGate.resolve = successCallback;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(20), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    const playPromise = session.play(
      createVideoItem('current.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
      vi.fn(),
      { waitForFirstFrame: true },
    );
    await Promise.resolve();
    const preparePromise = session.prepare(
      createVideoItem('next.mp4'),
      createSlotPlan(),
      document.createElement('section'),
      false,
    );
    await Promise.resolve();

    listenerRef.current?.oncurrentplaytime?.(1);
    await expect(playPromise).resolves.toEqual({ durationMs: null });

    nextPrepareGate.resolve?.();
    await expect(preparePromise).resolves.toEqual({ durationMs: null });
    expect(playerA.play).toHaveBeenCalledTimes(1);
  });

  it('같은 영상 prepare가 겹치면 기존 prepare 작업에 합류한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const prepareGate = {
      resolve: null as (() => void) | null,
    };
    playerA.prepareAsync = vi.fn((successCallback: () => void) => {
      prepareGate.resolve = successCallback;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(20), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const item = createVideoItem('same-next.mp4');
    const slot = createSlotPlan();
    const element = document.createElement('section');

    const firstPrepare = session.prepare(item, slot, element, false);
    await Promise.resolve();
    const secondPrepare = session.prepare(item, slot, element, false);
    await Promise.resolve();

    expect(playerA.prepareAsync).toHaveBeenCalledTimes(1);
    prepareGate.resolve?.();
    await expect(firstPrepare).resolves.toEqual({ durationMs: null });
    await expect(secondPrepare).resolves.toEqual({ durationMs: null });
    expect(playerB.prepareAsync).not.toHaveBeenCalled();
  });

  it('같은 소스 영상 prepare는 id가 달라도 기존 prepare 작업에 합류한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const prepareGate = {
      resolve: null as (() => void) | null,
    };
    playerA.prepareAsync = vi.fn((successCallback: () => void) => {
      prepareGate.resolve = successCallback;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(20), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const firstItem = {
      ...createVideoItem('same-source.mp4'),
      id: 'page-a-content-id',
    };
    const secondItem = {
      ...createVideoItem('same-source.mp4'),
      id: 'page-b-content-id',
    };
    const slot = createSlotPlan();
    const element = document.createElement('section');

    const firstPrepare = session.prepare(firstItem, slot, element, false);
    await Promise.resolve();
    const secondPrepare = session.prepare(secondItem, slot, element, false);
    await Promise.resolve();

    expect(playerA.prepareAsync).toHaveBeenCalledTimes(1);
    prepareGate.resolve?.();
    await expect(firstPrepare).resolves.toEqual({ durationMs: null });
    await expect(secondPrepare).resolves.toEqual({ durationMs: null });
    expect(playerB.prepareAsync).not.toHaveBeenCalled();
  });

  it('준비 중 clearPrepared가 들어와도 AVPlay prepareAsync 완료 전에는 작업을 폐기하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const prepareGate = {
      resolve: null as (() => void) | null,
    };
    playerA.prepareAsync = vi.fn((successCallback: () => void) => {
      prepareGate.resolve = successCallback;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(20), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const item = createVideoItem('prepared-next.mp4');
    const slot = createSlotPlan();
    const element = document.createElement('section');

    const preparePromise = session.prepare(item, slot, element, false);
    await Promise.resolve();
    session.clearPrepared();

    prepareGate.resolve?.();

    await expect(preparePromise).resolves.toEqual({ durationMs: null });
    await session.play(item, slot, element, false, vi.fn());
    expect(playerA.prepareAsync).toHaveBeenCalledTimes(1);
    expect(playerA.play).toHaveBeenCalledTimes(1);
  });

  it('다른 영상 prepare 요청은 진행 중인 prepareAsync가 끝난 뒤 같은 lane을 정리하고 시작한다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    let playerAState = 'IDLE';
    const prepareCallbacks: Array<() => void> = [];
    playerA.getState = vi.fn(() => playerAState);
    playerA.prepareAsync = vi.fn((successCallback: () => void) => {
      prepareCallbacks.push(() => {
        playerAState = 'READY';
        successCallback();
      });
    });
    playerA.stop = vi.fn(() => {
      playerAState = 'IDLE';
    });
    playerA.close = vi.fn(() => {
      playerAState = 'IDLE';
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(20), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });
    const slot = createSlotPlan();
    const element = document.createElement('section');

    const firstPrepare = session.prepare(createVideoItem('first-next.mp4'), slot, element, false);
    await Promise.resolve();
    const secondPrepare = session.prepare(createVideoItem('second-next.mp4'), slot, element, false);
    await Promise.resolve();

    expect(playerA.prepareAsync).toHaveBeenCalledTimes(1);
    expect(playerA.stop).not.toHaveBeenCalled();
    prepareCallbacks[0]?.();
    await expect(firstPrepare).resolves.toEqual({ durationMs: null });
    await Promise.resolve();

    expect(playerA.stop).toHaveBeenCalledTimes(1);
    expect(playerA.open).toHaveBeenLastCalledWith('https://example.com/second-next.mp4');
    expect(playerA.prepareAsync).toHaveBeenCalledTimes(2);
    prepareCallbacks[1]?.();

    await expect(secondPrepare).resolves.toEqual({ durationMs: null });
    expect(playerB.prepareAsync).not.toHaveBeenCalled();
  });

  it('폐기된 prepareAsync 완료는 play까지 진행하지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const prepareGate = {
      resolve: null as (() => void) | null,
    };
    playerA.prepareAsync = vi.fn((successCallback: () => void) => {
      prepareGate.resolve = successCallback;
    });
    const session = new AvplaySession(0, [
      { player: playerA, objectElement: document.createElement('object') },
      { player: playerB, objectElement: document.createElement('object') },
    ], document.body, new RingLogger(1), {
      onEnded: vi.fn(),
      onError: vi.fn(),
    });

    const playPromise = session.play(createVideoItem('stale.mp4'), createSlotPlan(), document.createElement('section'), false, vi.fn());
    await Promise.resolve();

    session.stop();
    prepareGate.resolve?.();

    await expect(playPromise).rejects.toThrow('AVPlay 작업이 폐기되었습니다');
    expect(playerA.play).not.toHaveBeenCalled();
  });

});

import { describe, expect, it, vi } from 'vitest';
import { AvplaySession, AvplaySessionPool } from '../src/player/avplay-session';
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
    getState: vi.fn(() => 'IDLE'),
  };
}

describe('AvplaySessionPool', () => {
  it('acquire 시 AVPlayStore 플레이어 두 개로 심리스 세션 하나를 만든다', () => {
    const getPlayer = vi.fn(createPlayer);
    window.webapis = {
      avplay: createPlayer(),
      avplaystore: {
        getPlayer,
      },
    };

    const pool = new AvplaySessionPool(document.body, new RingLogger(1), () => ({
      onEnded: vi.fn(),
      onError: vi.fn(),
    }));

    expect(getPlayer).not.toHaveBeenCalled();

    const first = pool.acquire(0);
    const firstAgain = pool.acquire(0);
    expect(firstAgain).toBe(first);
    expect(getPlayer).toHaveBeenCalledTimes(2);

    pool.acquire(1);
    expect(getPlayer).toHaveBeenCalledTimes(4);
  });

  it('AVPlayStore 공식 한도 이상으로 getPlayer를 호출하지 않는다', () => {
    const getPlayer = vi.fn(createPlayer);
    window.webapis = {
      avplay: createPlayer(),
      avplaystore: {
        getPlayer,
      },
    };

    const pool = new AvplaySessionPool(document.body, new RingLogger(1), () => ({
      onEnded: vi.fn(),
      onError: vi.fn(),
    }));

    pool.acquire(0);
    pool.acquire(1);

    expect(getPlayer).toHaveBeenCalledTimes(4);
    expect(() => pool.acquire(2)).toThrow('Tizen AVPlayStore 세션 한도를 초과했습니다');
    expect(getPlayer).toHaveBeenCalledTimes(4);
  });

  it('반납된 idle 세션은 다음 슬롯에서 재사용한다', () => {
    const getPlayer = vi.fn(createPlayer);
    window.webapis = {
      avplay: createPlayer(),
      avplaystore: {
        getPlayer,
      },
    };

    const pool = new AvplaySessionPool(document.body, new RingLogger(1), () => ({
      onEnded: vi.fn(),
      onError: vi.fn(),
    }));

    const first = pool.acquire(0);
    expect(getPlayer).toHaveBeenCalledTimes(2);

    pool.release(first);
    const reused = pool.acquire(100);

    expect(reused).toBe(first);
    expect(getPlayer).toHaveBeenCalledTimes(2);
  });
});

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

  it('전환 중 현재 lane을 이전 still lane보다 위에 둔다', async () => {
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
    expect(laneA.style.zIndex).toBe('21');

    await session.play(createVideoItem('third.mp4'), slotPlan, slotElement, false, vi.fn());
    expect(laneA.style.zIndex).toBe('22');
    expect(laneB.style.zIndex).toBe('21');
  });

  it('다음 영상 prepare 중에는 현재 lane을 still mode로 멈추지 않는다', async () => {
    const playerA = createPlayer();
    const playerB = createPlayer();
    const callOrder: string[] = [];
    playerA.getState = vi.fn(() => 'PLAYING');
    playerA.setVideoStillMode = vi.fn((mode) => {
      callOrder.push(`a.still:${mode}`);
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
    expect(callOrder[2]).toBe('a.still:true');
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

    await session.play(createVideoItem('boundary.mp4'), createSlotPlan(), document.createElement('section'), false, () => false);
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
    listenerRef.current?.oncurrentplaytime?.(0);
    await playPromise;

    expect(resolved).toBe(true);
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

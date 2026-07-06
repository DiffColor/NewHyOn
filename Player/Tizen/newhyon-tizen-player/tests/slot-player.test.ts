import { afterEach, describe, expect, it, vi } from 'vitest';
import { RingLogger } from '../src/core/logger';
import type { SeamlessContentItem, SeamlessSlotPlan } from '../src/domain/page-plan';
import type { AvplaySession } from '../src/player/avplay-session';
import { SlotPlayer } from '../src/player/slot-player';

function createVideoSlot(): SeamlessSlotPlan {
  return {
    elementName: 'video-slot',
    isMuted: true,
    width: 1920,
    height: 1080,
    left: 0,
    top: 0,
    zIndex: 0,
    items: [
      {
        source: {
          CIF_FileName: 'video.mp4',
          CIF_ContentType: 'Video',
        },
        id: 'video.mp4',
        name: 'video.mp4',
        sourceUrl: 'video.mp4',
        contentType: 'Video',
        durationSeconds: 10,
        actualDurationSeconds: 10,
        shouldLoop: false,
        transitionByTimer: true,
        loopDisableAfterEndCount: 0,
        transitionEndEventCount: 0,
      },
    ],
  };
}

function createVideoItem(name: string): SeamlessContentItem {
  return {
    source: {
      CIF_FileName: name,
      CIF_ContentType: 'Video',
    },
    id: name,
    name,
    sourceUrl: name,
    contentType: 'Video',
    durationSeconds: 10,
    actualDurationSeconds: 10,
    shouldLoop: false,
    transitionByTimer: true,
    loopDisableAfterEndCount: 0,
    transitionEndEventCount: 0,
  };
}

function createImageItem(name: string): SeamlessContentItem {
  return {
    source: {
      CIF_FileName: name,
      CIF_ContentType: 'Image',
    },
    id: name,
    name,
    sourceUrl: name,
    contentType: 'Image',
    durationSeconds: 10,
    actualDurationSeconds: 10,
    shouldLoop: false,
    transitionByTimer: true,
    loopDisableAfterEndCount: 0,
    transitionEndEventCount: 0,
  };
}

function createTwoVideoSlot(): SeamlessSlotPlan {
  return {
    ...createVideoSlot(),
    items: [createVideoItem('first.mp4'), createVideoItem('second.mp4')],
  };
}

function createTwoImageSlot(): SeamlessSlotPlan {
  return {
    ...createVideoSlot(),
    elementName: 'image-slot',
    items: [createImageItem('first.png'), createImageItem('second.png')],
  };
}

function createVideoThenImageSlot(): SeamlessSlotPlan {
  return {
    ...createVideoSlot(),
    items: [createVideoItem('first.mp4'), createImageItem('second.png')],
  };
}

function createImageThenVideoSlot(): SeamlessSlotPlan {
  return {
    ...createVideoSlot(),
    items: [createImageItem('first.png'), createVideoItem('second.mp4')],
  };
}

function createVideoImageVideoSlot(): SeamlessSlotPlan {
  return {
    ...createVideoSlot(),
    items: [createVideoItem('first.mp4'), createImageItem('second.png'), createVideoItem('third.mp4')],
  };
}

describe('SlotPlayer', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('영상 세션 확보 실패가 앱 시작 실패로 전파되지 않고 슬롯 오류로 기록된다', async () => {
    const element = document.createElement('section');
    const slot = new SlotPlayer(
      5,
      element,
      createVideoSlot(),
      false,
      false,
      () => {
        throw new Error('Tizen AVPlayStore 세션 한도를 초과했습니다');
      },
      new RingLogger(5),
    );

    await expect(slot.start()).resolves.toBe(false);

    expect(element.classList.contains('slot--empty')).toBe(true);
    expect(slot.snapshot()).toContain('ERROR: Tizen AVPlayStore 세션 한도를 초과했습니다');
    vi.clearAllTimers();
  });

  it('영상 재생 중에는 슬롯 배경이 AVPlay 화면을 가리지 않도록 영상 활성 상태를 표시한다', async () => {
    const element = document.createElement('section');
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, element, createVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();

    expect(element.classList.contains('slot--video-active')).toBe(true);
    expect(play.mock.calls[0]?.[5]).toMatchObject({ waitForFirstFrame: false });

    slot.stop();
    expect(element.classList.contains('slot--video-active')).toBe(false);
  });

  it('단일 콘텐츠는 재생 시간이 지나도 같은 콘텐츠를 다시 열지 않는다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(10000);
    expect(play).toHaveBeenCalledTimes(1);
  });

  it('기간 스케줄상 현재 재생 불가한 콘텐츠는 건너뛰고 허용 콘텐츠로 시작한다', async () => {
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slotPlan: SeamlessSlotPlan = {
      ...createTwoVideoSlot(),
      items: [createVideoItem('blocked.mp4'), createVideoItem('allowed.mp4')],
    };
    const slot = new SlotPlayer(
      0,
      document.createElement('section'),
      slotPlan,
      false,
      false,
      () => session,
      new RingLogger(5),
      undefined,
      false,
      undefined,
      undefined,
      undefined,
      (item) => item.name !== 'blocked.mp4',
    );

    await slot.start();

    expect(play).toHaveBeenCalledTimes(1);
    expect(play.mock.calls[0]?.[0]).toMatchObject({ name: 'allowed.mp4' });
    expect(slot.timelineSnapshot().itemName).toBe('allowed.mp4');
  });

  it('기간 스케줄로 유효 콘텐츠가 1개만 남은 단일 페이지 영상은 AVPlay 루프를 켠다', async () => {
    const play = vi.fn(async (..._args: unknown[]) => ({ durationMs: 21632 }));
    const setLooping = vi.fn();
    const session = {
      play,
      setLooping,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slotPlan: SeamlessSlotPlan = {
      ...createTwoVideoSlot(),
      items: [createVideoItem('expired.mp4'), createVideoItem('allowed.mp4')],
    };
    const slot = new SlotPlayer(
      0,
      document.createElement('section'),
      slotPlan,
      false,
      false,
      () => session,
      new RingLogger(5),
      undefined,
      false,
      undefined,
      undefined,
      undefined,
      (item) => item.name === 'allowed.mp4',
    );
    slot.setPageTimeline(0, 21000, true);

    await slot.start();

    expect(play).toHaveBeenCalledTimes(1);
    expect(play.mock.calls[0]?.[0]).toMatchObject({ name: 'allowed.mp4' });
    expect(setLooping).toHaveBeenLastCalledWith(true);
  });

  it('다음 콘텐츠 표시는 기간 스케줄상 재생 불가한 콘텐츠를 건너뛴다', async () => {
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const prepare = vi.fn(async (..._args: unknown[]) => undefined);
    const session = {
      play,
      prepare,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      clearPrepared: vi.fn(),
    } as unknown as AvplaySession;
    const slotPlan: SeamlessSlotPlan = {
      ...createTwoVideoSlot(),
      items: [
        createVideoItem('first.mp4'),
        createVideoItem('blocked.mp4'),
        createVideoItem('third.mp4'),
      ],
    };
    const slot = new SlotPlayer(
      0,
      document.createElement('section'),
      slotPlan,
      false,
      false,
      () => session,
      new RingLogger(5),
      undefined,
      false,
      undefined,
      undefined,
      undefined,
      (item) => item.name !== 'blocked.mp4',
    );

    await slot.start();
    const snapshot = slot.timelineSnapshot();

    expect(snapshot.itemName).toBe('first.mp4');
    expect(snapshot.nextItemName).toBe('third.mp4');
  });

  it('단일 이미지 타임라인은 한 바퀴가 지나면 0초부터 다시 표시한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        {
          ...createTwoImageSlot(),
          items: [{ ...createImageItem('single.png'), durationSeconds: 1, actualDurationSeconds: 1 }],
        },
        false,
        false,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;

      await vi.advanceTimersByTimeAsync(1200);
      const snapshot = slot.timelineSnapshot();

      expect(snapshot.itemName).toBe('single.png');
      expect(snapshot.elapsedMs).toBeGreaterThanOrEqual(200);
      expect(snapshot.elapsedMs).toBeLessThan(300);
      expect(snapshot.remainingMs).toBeGreaterThan(700);
      expect(snapshot.nextTransitionText).toContain('표시 타이머 반복');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('페이지 종료가 콘텐츠 전환보다 빠르면 다음 콘텐츠 이미지를 준비하지 않는다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const element = document.createElement('section');
      const slot = new SlotPlayer(
        0,
        element,
        createTwoImageSlot(),
        false,
        false,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );
      slot.setPageTimeline(0, 9000, false);

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;

      const imageSources = Array.from(element.querySelectorAll<HTMLImageElement>('img')).map((image) => image.getAttribute('src'));
      expect(imageSources).toContain('first.png');
      expect(imageSources).not.toContain('second.png');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('일시정지 후 재개하면 다중 콘텐츠의 전체 시간이 아니라 남은 시간만 재생한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);

    await slot.syncToPageElapsed(4000);
    slot.pause();
    await vi.advanceTimersByTimeAsync(10000);
    expect(play).toHaveBeenCalledTimes(1);

    slot.resume();
    await slot.syncToPageElapsed(9999);
    expect(play).toHaveBeenCalledTimes(1);
    await slot.syncToPageElapsed(10000);
    expect(play).toHaveBeenCalledTimes(2);
  });

  it('영상에서 영상으로 이어질 때는 사전 prepare 없이 전환 시점에 다음 영상을 재생한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      clearPrepared: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    await vi.runAllTicks();

    expect(play).toHaveBeenCalledTimes(1);

    await slot.syncToPageElapsed(10000);
    expect(play).toHaveBeenCalledTimes(2);
    expect(play.mock.calls[1]?.[0]).toMatchObject({ id: 'second.mp4' });
    expect(play.mock.calls[0]?.[5]).toMatchObject({ waitForFirstFrame: false });
    expect(play.mock.calls[1]?.[5]).toMatchObject({ waitForFirstFrame: false });
  });

  it('현재 영상 재생 중에는 일반 다음 영상을 AVPlay로 사전 prepare한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const prepareNextVideo = vi.fn();
    const session = {
      play,
      prepareNextVideo,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      clearPrepared: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    await vi.runAllTicks();
    await vi.advanceTimersByTimeAsync(32);

    expect(play).toHaveBeenCalledTimes(1);
    expect(prepareNextVideo).toHaveBeenCalledWith(expect.objectContaining({ id: 'second.mp4' }), 'next-content');
  });

  it('단일 페이지 루프에서 첫 콘텐츠 재생 중에도 다음 영상을 prepare한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const prepareNextVideo = vi.fn();
    const clearPrepared = vi.fn();
    const session = {
      play,
      prepareNextVideo,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      clearPrepared,
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));
    slot.setPageTimeline(0, 20000, true);

    await slot.start();
    await vi.runAllTicks();
    await vi.advanceTimersByTimeAsync(32);

    expect(play).toHaveBeenCalledTimes(1);
    expect(prepareNextVideo).toHaveBeenCalledWith(expect.objectContaining({ id: 'second.mp4' }), 'next-content');
    expect(clearPrepared).not.toHaveBeenCalled();
  });

  it('스케줄 전환용 다음 영상은 지정 role로 사전 prepare한다', async () => {
    const play = vi.fn(async (..._args: unknown[]) => undefined);
    const prepareNextVideo = vi.fn();
    const session = {
      play,
      prepareNextVideo,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      clearPrepared: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    slot.prepareTransitionContentTarget({
      item: createVideoItem('schedule.mp4'),
      itemIndex: 0,
      slot: createTwoVideoSlot(),
    }, 'next-schedule-content');

    expect(prepareNextVideo).toHaveBeenCalledWith(expect.objectContaining({ id: 'schedule.mp4' }), 'next-schedule-content');
  });

  it('현재 콘텐츠 종료 후 전환 설정이 켜지면 영상 종료 이벤트로 다음 콘텐츠를 재생한다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => void,
    ) => {
      ended.handler = onStreamEnded;
    });
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, true, () => session, new RingLogger(5));

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);
    expect((play.mock.calls[0]?.[0] as SeamlessContentItem).name).toBe('first.mp4');

    await vi.advanceTimersByTimeAsync(10000);
    expect(play).toHaveBeenCalledTimes(1);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }
    ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('타이머 전환이 꺼진 영상은 전환 설정이 꺼져 있어도 종료 이벤트까지 재생한다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => void,
    ) => {
      ended.handler = onStreamEnded;
    });
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slotPlan = {
      ...createTwoVideoSlot(),
      items: [
        { ...createVideoItem('first.mp4'), transitionByTimer: false },
        { ...createVideoItem('second.mp4'), transitionByTimer: false },
      ],
    };
    const slot = new SlotPlayer(0, document.createElement('section'), slotPlan, false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);
    expect((play.mock.calls[0]?.[0] as SeamlessContentItem).name).toBe('first.mp4');

    await vi.advanceTimersByTimeAsync(10000);
    expect(play).toHaveBeenCalledTimes(1);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }
    ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('영상 설정 시간이 원본 길이의 정수 배이면 마지막 종료 이벤트로 다음 컨텐츠를 재생한다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => boolean | Promise<boolean> | void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => boolean | Promise<boolean> | void,
    ) => {
      ended.handler = onStreamEnded;
      return { durationMs: 5000 };
    });
    const session = {
      play,
      prepare: vi.fn(async () => ({ durationMs: 5000 })),
      clearPrepared: vi.fn(),
      setLooping: vi.fn(),
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(session.setLooping).toHaveBeenCalledWith(true);

    await vi.advanceTimersByTimeAsync(5000);
    await slot.syncToPageElapsed(5000);
    expect(session.setLooping).toHaveBeenLastCalledWith(false);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }

    await ended.handler();
    await vi.runOnlyPendingTimersAsync();
    expect(play).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(5000);
    await slot.syncToPageElapsed(10000);
    await ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('영상 설정 시간과 원본 길이 차이가 1초 이내이면 1회 재생 종료 이벤트로 다음 컨텐츠를 재생한다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => boolean | Promise<boolean> | void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => boolean | Promise<boolean> | void,
    ) => {
      ended.handler = onStreamEnded;
      return { durationMs: 9100 };
    });
    const session = {
      play,
      prepare: vi.fn(async () => ({ durationMs: 9100 })),
      clearPrepared: vi.fn(),
      setLooping: vi.fn(),
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(session.setLooping).toHaveBeenLastCalledWith(false);

    await slot.syncToPageElapsed(10000);
    expect(play).toHaveBeenCalledTimes(1);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }

    await ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('영상 설정 시간이 원본 길이의 정수 배가 아니면 타이머로 다음 컨텐츠를 재생한다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async (..._args: unknown[]) => ({ durationMs: 5000 }));
    const session = {
      play,
      prepare: vi.fn(async () => ({ durationMs: 5000 })),
      clearPrepared: vi.fn(),
      setLooping: vi.fn(),
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slotPlan = {
      ...createTwoVideoSlot(),
      items: [
        { ...createVideoItem('first.mp4'), durationSeconds: 12 },
        createVideoItem('second.mp4'),
      ],
    };
    const slot = new SlotPlayer(0, document.createElement('section'), slotPlan, false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(session.setLooping).toHaveBeenCalledWith(true);

    await slot.syncToPageElapsed(10000);
    expect(play).toHaveBeenCalledTimes(1);

    await slot.syncToPageElapsed(12000);
    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('타이머 전환 영상의 종료 이벤트가 전환 직전에 들어와도 AVPlaySession 정리를 요청하지 않는다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => boolean | Promise<boolean> | void | Promise<void>) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => boolean | Promise<boolean> | void | Promise<void>,
    ) => {
      ended.handler = onStreamEnded;
      return { durationMs: 5000 };
    });
    const session = {
      play,
      prepare: vi.fn(async () => ({ durationMs: 5000 })),
      clearPrepared: vi.fn(),
      setLooping: vi.fn(),
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slotPlan = {
      ...createTwoVideoSlot(),
      items: [
        { ...createVideoItem('first.mp4'), durationSeconds: 12 },
        createVideoItem('second.mp4'),
      ],
    };
    const slot = new SlotPlayer(0, document.createElement('section'), slotPlan, false, false, () => session, new RingLogger(5));

    await slot.start();
    await vi.advanceTimersByTimeAsync(5000);
    await slot.syncToPageElapsed(5000);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }

    await expect(Promise.resolve(ended.handler())).resolves.toBe(false);
    expect(play).toHaveBeenCalledTimes(1);

    await slot.syncToPageElapsed(12000);

    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('현재가 이미지이면 다음 영상이 종료 이벤트 대기 대상이어도 이미지 시간 후 다음 컨텐츠를 재생한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const play = vi.fn(async (..._args: unknown[]) => undefined);
      const session = {
        play,
        clearPrepared: vi.fn(),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
      } as unknown as AvplaySession;
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        createImageThenVideoSlot(),
        false,
        true,
        () => session,
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;

      expect(slot.snapshot()).toContain('first.png');

      await slot.syncToPageElapsed(10000);
      await vi.runAllTicks();

      expect(play).toHaveBeenCalledTimes(1);
      expect((play.mock.calls[0]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('현재 콘텐츠 종료 후 전환 설정이 켜진 영상은 재개 후에도 타이머로 전환하지 않는다', async () => {
    vi.useFakeTimers();
    const play = vi.fn(async () => undefined);
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, true, () => session, new RingLogger(5));

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(4000);
    slot.pause();
    await vi.advanceTimersByTimeAsync(10000);
    slot.resume();
    await vi.advanceTimersByTimeAsync(10000);

    expect(play).toHaveBeenCalledTimes(1);
  });

  it('단일 페이지 루프에서는 영상 종료 대기보다 페이지 타임라인을 우선해 첫 컨텐츠로 돌아간다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const play = vi.fn(async () => undefined);
      const session = {
        play,
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
        clearPrepared: vi.fn(),
      } as unknown as AvplaySession;
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        {
          ...createImageThenVideoSlot(),
          items: [
            { ...createImageItem('first.png'), durationSeconds: 5, actualDurationSeconds: 5 },
            { ...createVideoItem('second.mp4'), durationSeconds: 5, actualDurationSeconds: 5 },
          ],
        },
        false,
        true,
        () => session,
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;

      await slot.syncToPageElapsed(5000, 10000, true);
      expect(slot.snapshot()).toContain('second.mp4');
      expect(play).toHaveBeenCalledTimes(1);

      await slot.syncToPageElapsed(0, 10000, true);
      await vi.runAllTicks();

      expect(slot.snapshot()).toContain('first.png');
      await vi.runAllTicks();
      await Promise.resolve();
      await Promise.resolve();
      expect(session.stop).toHaveBeenCalledTimes(1);
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('런타임 설정 변경 후 컨텐츠 종료시 전환을 즉시 적용한다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => void,
    ) => {
      ended.handler = onStreamEnded;
    });
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      applyDisplayMethod: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);

    slot.updatePlaybackSettings(false, true);
    await slot.syncToPageElapsed(10000);
    expect(play).toHaveBeenCalledTimes(1);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }
    ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(play).toHaveBeenCalledTimes(2);
    expect((play.mock.calls[1]?.[0] as SeamlessContentItem).name).toBe('second.mp4');
  });

  it('컨텐츠 종료 전환 대기 중이면 페이지 전환을 막고 종료 이벤트 후 해제한다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => void,
    ) => {
      ended.handler = onStreamEnded;
    });
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      applyDisplayMethod: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, true, () => session, new RingLogger(5));

    await slot.start();
    slot.setPageTimeline(10000, 10000, false);

    expect(slot.blocksPageTransitionForContentEnd()).toBe(true);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }
    ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(play).toHaveBeenCalledTimes(1);
    expect(slot.blocksPageTransitionForContentEnd()).toBe(false);
  });

  it('영상 종료 이벤트 모드이면 컨텐츠 종료 전환 설정이 꺼져도 페이지 전환을 막는다', async () => {
    const ended = {
      handler: null as (() => void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => void,
    ) => {
      ended.handler = onStreamEnded;
      return { durationMs: 10000 };
    });
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      applyDisplayMethod: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(
      0,
      document.createElement('section'),
      createTwoVideoSlot(),
      false,
      false,
      () => session,
      new RingLogger(5),
    );

    await slot.start();
    slot.setPageTimeline(10000, 10000, false);

    expect(slot.blocksPageTransitionForContentEnd()).toBe(true);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }
    ended.handler();
    await Promise.resolve();

    expect(slot.blocksPageTransitionForContentEnd()).toBe(false);
  });

  it('페이지 끝에서 실제 영상 종료가 1초 이내이면 타이머 전환 영상도 종료 이벤트를 기다린다', async () => {
    vi.useFakeTimers();
    const ended = {
      handler: null as (() => boolean | Promise<boolean> | void | Promise<void>) | null,
    };
    const onPageTransitionContentEnd = vi.fn();
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => void,
    ) => {
      ended.handler = onStreamEnded;
      return { durationMs: 11000 };
    });
    const session = {
      play,
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      applyDisplayMethod: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(
      0,
      document.createElement('section'),
      {
        ...createVideoSlot(),
        items: [
          {
            ...createVideoItem('first.mp4'),
            durationSeconds: 10,
            transitionByTimer: true,
          },
        ],
      },
      false,
      false,
      () => session,
      new RingLogger(5),
      () => undefined,
      false,
      () => undefined,
      onPageTransitionContentEnd,
      () => true,
    );

    await slot.start();
    await vi.advanceTimersByTimeAsync(10000);
    slot.setPageTimeline(10000, 10000, false);

    expect(slot.blocksPageTransitionForContentEnd()).toBe(true);

    if (!ended.handler) {
      throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
    }
    const shouldComplete = await ended.handler();
    await vi.runOnlyPendingTimersAsync();

    expect(shouldComplete).toBe(false);
    expect(onPageTransitionContentEnd).toHaveBeenCalledTimes(1);
    expect(slot.blocksPageTransitionForContentEnd()).toBe(false);
  });

  it('페이지 콘텐츠 전환 중 이전 영상 종료 이벤트는 현재 페이지 콘텐츠 advance를 발생시키지 않는다', async () => {
    const endedHandlers: Array<() => boolean | Promise<boolean> | void | Promise<void>> = [];
    const secondPlayGate = {
      release: null as (() => void) | null,
    };
    const play = vi.fn(async (
      _item: SeamlessContentItem,
      _slot: SeamlessSlotPlan,
      _element: HTMLElement,
      _preserveAspectRatio: boolean,
      onStreamEnded: () => boolean | Promise<boolean> | void | Promise<void>,
    ) => {
      endedHandlers.push(onStreamEnded);
      if (play.mock.calls.length === 2) {
        await new Promise<void>((resolve) => {
          secondPlayGate.release = resolve;
        });
      }
      return { durationMs: 10000 };
    });
    const session = {
      play,
      prepare: vi.fn(async () => ({ durationMs: 10000 })),
      clearPrepared: vi.fn(),
      setLooping: vi.fn(),
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
      applyDisplayMethod: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, true, () => session, new RingLogger(5));
    const nextPageSlot: SeamlessSlotPlan = {
      ...createVideoSlot(),
      items: [
        createVideoItem('page2-first.mp4'),
        createVideoItem('page2-second.mp4'),
      ],
    };

    await slot.start();
    expect(play).toHaveBeenCalledTimes(1);

    const switchPromise = slot.switchToSlotPlan(nextPageSlot, 1920, 1080);
    await Promise.resolve();
    expect(play).toHaveBeenCalledTimes(2);

    const shouldComplete = await endedHandlers[0]?.();
    expect(shouldComplete).toBe(false);
    expect(play).toHaveBeenCalledTimes(2);

    secondPlayGate.release?.();
    await switchPromise;

    expect(slot.snapshot()).toContain('page2-first.mp4');
    expect(play).toHaveBeenCalledTimes(2);
  });

  it('이미지는 표시 시간이 끝나기 전까지 컨텐츠 종료 전환으로 페이지 전환을 막는다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        {
          ...createTwoImageSlot(),
          items: [createImageItem('first.png')],
        },
        false,
        true,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;

      await vi.advanceTimersByTimeAsync(5000);
      slot.setPageTimeline(5000, 5000, false);
      expect(slot.blocksPageTransitionForContentEnd()).toBe(true);

      await vi.advanceTimersByTimeAsync(5000);
      slot.setPageTimeline(10000, 5000, false);
      expect(slot.blocksPageTransitionForContentEnd()).toBe(false);
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('이미지는 컨텐츠 종료시 전환 설정이 켜져도 표시 시간 만료로 다음 이미지로 전환한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        createTwoImageSlot(),
        false,
        true,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;

      await slot.syncToPageElapsed(10000);
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);

      expect(slot.snapshot()).toContain('second.png');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('런타임 비율 설정 변경을 이미지와 영상 세션에 즉시 반영한다', async () => {
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const imageElement = document.createElement('section');
      const imageSlot = new SlotPlayer(
        0,
        imageElement,
        createTwoImageSlot(),
        false,
        false,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );
      await imageSlot.start();

      imageSlot.updatePlaybackSettings(true, false);
      imageElement.querySelectorAll<HTMLImageElement>('.slot-image').forEach((image) => {
        expect(image.style.objectFit).toBe('contain');
      });
      imageSlot.updatePlaybackSettings(false, false);
      imageElement.querySelectorAll<HTMLImageElement>('.slot-image').forEach((image) => {
        expect(image.style.objectFit).toBe('fill');
      });

      const session = {
        play: vi.fn(async () => undefined),
        prepare: vi.fn(async () => undefined),
        clearPrepared: vi.fn(),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
        applyDisplayMethod: vi.fn(),
      } as unknown as AvplaySession;
      const videoSlot = new SlotPlayer(0, document.createElement('section'), createVideoSlot(), false, false, () => session, new RingLogger(5));
      await videoSlot.start();
      videoSlot.updatePlaybackSettings(true, false);
      videoSlot.updatePlaybackSettings(false, false);

      expect(session.applyDisplayMethod).toHaveBeenCalledWith(true);
      expect(session.applyDisplayMethod).toHaveBeenLastCalledWith(false);
      vi.mocked(session.applyDisplayMethod).mockClear();
      videoSlot.updatePlaybackSettings(true, false, { applyVideoDisplayMethod: false });

      expect(session.applyDisplayMethod).not.toHaveBeenCalled();
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('다중 이미지 슬롯은 페이지 수와 무관하게 시간이 지나면 다음 이미지로 전환한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const element = document.createElement('section');
      const slot = new SlotPlayer(
        0,
        element,
        createTwoImageSlot(),
        false,
        false,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      await startPromise;
      expect(slot.snapshot()).toContain('first.png');

      let hadNoVisibleImage = false;
      const originalRemove = DOMTokenList.prototype.remove;
      const removeSpy = vi.spyOn(DOMTokenList.prototype, 'remove').mockImplementation(function (
        this: DOMTokenList,
        ...tokens: string[]
      ) {
        originalRemove.apply(this, tokens);
        if (tokens.includes('slot-image--visible') && element.querySelectorAll('.slot-image--visible').length === 0) {
          hadNoVisibleImage = true;
        }
      });

      await slot.syncToPageElapsed(10000);
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);
      expect(slot.snapshot()).toContain('second.png');
      expect(hadNoVisibleImage).toBe(false);
      removeSpy.mockRestore();
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('이미지 슬롯은 AVPlay 세션 없음 상태를 오류처럼 표시하지 않는다', async () => {
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        {
          ...createTwoImageSlot(),
          items: [createImageItem('first.png')],
        },
        false,
        false,
        () => {
          throw new Error('이미지 슬롯은 AVPlay 세션을 사용하지 않습니다.');
        },
        new RingLogger(5),
      );

      await slot.start();

      expect(slot.snapshot()).toContain('(IMAGE)');
      expect(slot.snapshot()).not.toContain('NO_VIDEO_SESSION');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('영상에서 이미지로 전환되면 AVPlay 세션을 정리한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const callOrder: string[] = [];
      const element = document.createElement('section');
      const session = {
        play: vi.fn(async () => undefined),
        prepare: vi.fn(async () => undefined),
        clearPrepared: vi.fn(),
        hide: vi.fn(() => {
          callOrder.push('hide');
        }),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(() => {
          callOrder.push('stop');
        }),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
      } as unknown as AvplaySession;
      const release = vi.fn();
      const slot = new SlotPlayer(
        0,
        element,
        createVideoThenImageSlot(),
        false,
        false,
        () => session,
        new RingLogger(5),
        () => undefined,
        false,
        release,
      );

      await slot.start();
      expect(release).not.toHaveBeenCalled();

      const syncPromise = slot.syncToPageElapsed(10000);
      let syncResolved = false;
      void syncPromise.then(() => {
        syncResolved = true;
      });
      await vi.runAllTicks();
      await syncPromise;
      expect(syncResolved).toBe(true);
      expect(session.hide).toHaveBeenCalledTimes(1);
      await vi.runAllTicks();
      expect(callOrder).toEqual(['hide']);
      await vi.runOnlyPendingTimersAsync();
      await vi.runOnlyPendingTimersAsync();
      expect(session.stop).toHaveBeenCalledTimes(1);
      expect(release).toHaveBeenCalledWith(session);
      expect(slot.snapshot()).toContain('second.png (IMAGE)');
      expect(callOrder).toEqual(['hide', 'stop']);
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('종료 이벤트 대기 영상의 다음 이미지도 페이지 남은 시간 조건과 무관하게 미리 준비한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    const loadedSources: string[] = [];
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        loadedSources.push(value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const ended = {
        handler: null as (() => boolean | Promise<boolean> | void | Promise<void>) | null,
      };
      const session = {
        play: vi.fn(async (
          _item: SeamlessContentItem,
          _slot: SeamlessSlotPlan,
          _element: HTMLElement,
          _preserveAspectRatio: boolean,
          onStreamEnded: () => boolean | Promise<boolean> | void | Promise<void>,
        ) => {
          ended.handler = onStreamEnded;
          return { durationMs: 10000 };
        }),
        prepare: vi.fn(async () => undefined),
        clearPrepared: vi.fn(),
        hide: vi.fn(),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
        applyDisplayMethod: vi.fn(),
      } as unknown as AvplaySession;
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        createVideoThenImageSlot(),
        false,
        false,
        () => session,
        new RingLogger(5),
      );

      await slot.start();
      slot.setPageTimeline(0, 10000, false);
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);

      expect(loadedSources.some((source) => source.includes('second.png'))).toBe(true);

      if (!ended.handler) {
        throw new Error('영상 종료 핸들러가 등록되지 않았습니다.');
      }
      await ended.handler();
      await vi.runAllTicks();

      expect(slot.snapshot()).toContain('second.png (IMAGE)');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('영상 첫 프레임이 표시된 뒤 다음 이미지를 준비한다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    const loadedSources: string[] = [];
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        loadedSources.push(value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const playGate: { release: (() => void) | null } = { release: null };
      const session = {
        play: vi.fn(async () => {
          await new Promise<void>((resolve) => {
            playGate.release = resolve;
          });
          return { durationMs: 10000 };
        }),
        prepare: vi.fn(async () => undefined),
        clearPrepared: vi.fn(),
        hide: vi.fn(),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
        applyDisplayMethod: vi.fn(),
      } as unknown as AvplaySession;
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        createVideoThenImageSlot(),
        false,
        false,
        () => session,
        new RingLogger(5),
      );

      const startPromise = slot.start();
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);

      expect(loadedSources.some((source) => source.includes('second.png'))).toBe(false);

      if (!playGate.release) {
        throw new Error('영상 play 대기 해제 함수가 등록되지 않았습니다.');
      }
      playGate.release();
      await startPromise;
      await vi.runAllTicks();
      await vi.advanceTimersByTimeAsync(32);

      expect(loadedSources.some((source) => source.includes('second.png'))).toBe(true);
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

  it('영상에서 이미지로 전환한 뒤에도 다음 영상은 선행 prepare하지 않는다', async () => {
    vi.useFakeTimers();
    const descriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
    Object.defineProperty(HTMLImageElement.prototype, 'src', {
      configurable: true,
      get() {
        return this.getAttribute('src') ?? '';
      },
      set(value: string) {
        this.setAttribute('src', value);
        queueMicrotask(() => this.onload?.(new Event('load')));
      },
    });

    try {
      const callOrder: string[] = [];
      const session = {
        play: vi.fn(async () => undefined),
        clearPrepared: vi.fn(),
        hide: vi.fn(() => {
          callOrder.push('hide');
        }),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(() => {
          callOrder.push('stop');
        }),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
      } as unknown as AvplaySession;
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
        createVideoImageVideoSlot(),
        false,
        false,
        () => session,
        new RingLogger(5),
        () => undefined,
        false,
        vi.fn(),
      );

      await slot.start();

      await slot.syncToPageElapsed(10000);
      await vi.runAllTicks();

      expect(slot.snapshot()).toContain('second.png (IMAGE)');
      expect(callOrder).toEqual(['hide']);
      await vi.runOnlyPendingTimersAsync();
      await vi.runOnlyPendingTimersAsync();
      await vi.runAllTicks();
      expect(callOrder).toEqual(['hide', 'stop']);
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });

});

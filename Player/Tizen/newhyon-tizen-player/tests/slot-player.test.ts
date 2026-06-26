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
    const session = {
      play: vi.fn(async () => undefined),
      pause: vi.fn(),
      resume: vi.fn(),
      stop: vi.fn(),
      state: vi.fn(() => 'PLAYING'),
      applyDisplayRect: vi.fn(),
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, element, createVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();

    expect(element.classList.contains('slot--video-active')).toBe(true);

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

  it('다중 영상 슬롯은 현재 영상 재생 중 다음 영상 prepare를 호출하지 않는다', async () => {
    vi.useFakeTimers();
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
    } as unknown as AvplaySession;
    const slot = new SlotPlayer(0, document.createElement('section'), createTwoVideoSlot(), false, false, () => session, new RingLogger(5));

    await slot.start();
    await vi.runAllTicks();

    expect(play).toHaveBeenCalledTimes(1);
    expect(prepare).not.toHaveBeenCalled();

    await slot.syncToPageElapsed(10000);
    expect(prepare).not.toHaveBeenCalled();
    expect(play).toHaveBeenCalledTimes(2);
    expect(play.mock.calls[0]?.[5]).toMatchObject({ waitForFirstFrame: true });
    expect(play.mock.calls[1]?.[5]).toMatchObject({ waitForFirstFrame: true });
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

      const session = {
        play: vi.fn(async () => undefined),
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

      expect(session.applyDisplayMethod).toHaveBeenCalledWith(true);
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

  it('영상에서 이미지로 전환되면 idle AVPlay 세션 임대를 반납한다', async () => {
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
      const session = {
        play: vi.fn(async () => undefined),
        pause: vi.fn(),
        resume: vi.fn(),
        stop: vi.fn(),
        state: vi.fn(() => 'PLAYING'),
        applyDisplayRect: vi.fn(),
      } as unknown as AvplaySession;
      const release = vi.fn();
      const slot = new SlotPlayer(
        0,
        document.createElement('section'),
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

      await slot.syncToPageElapsed(10000);
      await vi.runAllTicks();

      expect(session.stop).toHaveBeenCalledTimes(1);
      expect(release).toHaveBeenCalledWith(session);
      expect(slot.snapshot()).toContain('second.png (IMAGE)');
    } finally {
      if (descriptor) {
        Object.defineProperty(HTMLImageElement.prototype, 'src', descriptor);
      }
    }
  });
});

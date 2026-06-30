import type { ContentsInfoClass, ContentType, ElementInfoClass, PageInfoClass } from './models';

export interface SeamlessContentItem {
  readonly source: ContentsInfoClass;
  readonly id: string;
  readonly name: string;
  readonly sourceUrl: string;
  readonly contentType: ContentType;
  readonly durationSeconds: number;
  readonly actualDurationSeconds: number;
  readonly shouldLoop: boolean;
  readonly transitionByTimer: boolean;
  readonly loopDisableAfterEndCount: number;
  readonly transitionEndEventCount: number;
}

export interface SeamlessSlotPlan {
  readonly elementName: string;
  readonly isMuted: boolean;
  readonly width: number;
  readonly height: number;
  readonly left: number;
  readonly top: number;
  readonly zIndex: number;
  readonly items: SeamlessContentItem[];
}

export interface SeamlessPagePlan {
  readonly playlistName: string;
  readonly pageName: string;
  readonly canvasWidth: number;
  readonly canvasHeight: number;
  readonly durationSeconds: number;
  readonly volume: number;
  readonly hasExplicitVolume: boolean;
  readonly slots: SeamlessSlotPlan[];
}

export interface BuildPagePlanOptions {
  readonly defaultVolume?: number;
  readonly hasContentPeriod?: (content: ContentsInfoClass, item: SeamlessContentItem) => boolean;
  readonly isContentAllowed?: (content: ContentsInfoClass, item: SeamlessContentItem) => boolean;
}

const MAX_MEDIA_SLOTS = 6;

function normalizeEnum(value: string | undefined): string {
  return (value ?? '').trim().toLowerCase();
}

function isMediaElement(element: ElementInfoClass): boolean {
  return normalizeEnum(element.EIF_Type) === 'media';
}

function parseContentType(value: string | undefined): ContentType | null {
  const normalized = normalizeEnum(value);
  if (normalized === 'video') {
    return 'Video';
  }

  if (normalized === 'image') {
    return 'Image';
  }

  return null;
}

function parseTwoDigitSeconds(minuteText: string | undefined, secondText: string | undefined): number {
  const minute = Number.parseInt(minuteText ?? '0', 10);
  const second = Number.parseInt(secondText ?? '0', 10);
  const normalizedMinute = Number.isFinite(minute) ? minute : 0;
  const normalizedSecond = Number.isFinite(second) ? second : 0;
  return Math.max(1, normalizedMinute * 60 + normalizedSecond);
}

function resolveContentSourceUrl(content: ContentsInfoClass): string {
  const fullPath = content.CIF_FileFullPath?.trim();
  if (fullPath) {
    return fullPath;
  }

  const relativePath = content.CIF_RelativePath?.trim();
  if (relativePath) {
    return relativePath;
  }

  return `Contents/${content.CIF_FileName.trim()}`;
}

function buildContentItem(content: ContentsInfoClass): SeamlessContentItem | null {
  if (!content.CIF_FileName?.trim()) {
    return null;
  }

  if (content.CIF_ValidTime === false || content.CIF_FileExist === false) {
    return null;
  }

  if ((content.CIF_PlayMinute ?? '00') === '00' && (content.CIF_PlaySec ?? '00') === '00') {
    return null;
  }

  const contentType = parseContentType(String(content.CIF_ContentType));
  if (!contentType) {
    return null;
  }

  const durationSeconds = parseTwoDigitSeconds(content.CIF_PlayMinute, content.CIF_PlaySec);
  const actualDurationSeconds = durationSeconds;
  let shouldLoop = false;
  let transitionByTimer = true;
  let loopDisableAfterEndCount = 0;
  let transitionEndEventCount = 0;

  if (contentType === 'Video') {
    const safeActualDuration = Math.max(1, actualDurationSeconds);
    const remainder = durationSeconds % safeActualDuration;

    if (durationSeconds < safeActualDuration) {
      shouldLoop = false;
      transitionByTimer = true;
    } else if (durationSeconds === safeActualDuration) {
      shouldLoop = false;
      transitionByTimer = false;
      transitionEndEventCount = 1;
    } else {
      shouldLoop = true;
      transitionByTimer = remainder !== 0;

      const fullPlaybackCount = Math.floor(durationSeconds / safeActualDuration);
      if (remainder === 0) {
        loopDisableAfterEndCount = Math.max(0, fullPlaybackCount - 1);
        transitionEndEventCount = Math.max(1, fullPlaybackCount);
      } else {
        loopDisableAfterEndCount = Math.max(1, fullPlaybackCount);
      }
    }
  }

  return {
    source: content,
    id: content.CIF_StrGUID?.trim() || content.CIF_FileName.trim(),
    name: content.CIF_FileName.trim(),
    sourceUrl: resolveContentSourceUrl(content),
    contentType,
    durationSeconds,
    actualDurationSeconds,
    shouldLoop,
    transitionByTimer,
    loopDisableAfterEndCount,
    transitionEndEventCount,
  };
}

function configureSingleVideoSlotLoop(slotPlan: SeamlessSlotPlan): SeamlessSlotPlan {
  if (slotPlan.items.length !== 1 || slotPlan.items[0]?.contentType !== 'Video') {
    return slotPlan;
  }

  const onlyItem = slotPlan.items[0];
  return {
    ...slotPlan,
    items: [
      {
        ...onlyItem,
        shouldLoop: true,
        transitionByTimer: true,
        loopDisableAfterEndCount: 0,
        transitionEndEventCount: 0,
      },
    ],
  };
}

function pageDurationSeconds(page: PageInfoClass): number {
  return Math.max(
    1,
    (page.PIC_PlaytimeHour ?? 0) * 3600 + (page.PIC_PlaytimeMinute ?? 0) * 60 + (page.PIC_PlaytimeSecond ?? 10),
  );
}

function pageVolume(page: PageInfoClass, defaultVolume = 100): number {
  const volume = page.PIC_Volume ?? defaultVolume;
  if (!Number.isFinite(volume)) {
    return Math.min(100, Math.max(0, Math.round(defaultVolume)));
  }

  return Math.min(100, Math.max(0, Math.round(volume)));
}

export function buildPagePlan(page: PageInfoClass, playlistName: string, options: BuildPagePlanOptions = {}): SeamlessPagePlan {
  const playableElements = [...(page.PIC_Elements ?? [])]
    .filter(isMediaElement)
    .sort((left, right) => (left.EIF_ZIndex ?? 0) - (right.EIF_ZIndex ?? 0))
    .slice(0, MAX_MEDIA_SLOTS);
  let hasPeriodRestrictedContent = false;
  let dynamicDurationSeconds = 0;

  const slots = playableElements.map((element): SeamlessSlotPlan => {
    const items = (element.EIF_ContentsInfoClassList ?? [])
      .map(buildContentItem)
      .filter((item): item is SeamlessContentItem => item !== null);
    let slotDurationSeconds = 0;
    items.forEach((item) => {
      const hasPeriod = options.hasContentPeriod?.(item.source, item) === true;
      if (hasPeriod) {
        hasPeriodRestrictedContent = true;
      }

      if (!hasPeriod || options.isContentAllowed?.(item.source, item) !== false) {
        slotDurationSeconds += Math.max(1, item.durationSeconds);
      }
    });
    dynamicDurationSeconds = Math.max(dynamicDurationSeconds, slotDurationSeconds);

    return configureSingleVideoSlotLoop({
      elementName: element.EIF_Name ?? '',
      isMuted: element.EIF_IsMuted ?? true,
      width: Math.max(0, element.EIF_Width),
      height: Math.max(0, element.EIF_Height),
      left: element.EIF_PosLeft,
      top: element.EIF_PosTop,
      zIndex: element.EIF_ZIndex ?? 0,
      items,
    });
  });

  while (slots.length < MAX_MEDIA_SLOTS) {
    slots.push({
      elementName: '',
      isMuted: true,
      width: 0,
      height: 0,
      left: 0,
      top: 0,
      zIndex: 0,
      items: [],
    });
  }

  return {
    playlistName,
    pageName: page.PIC_PageName ?? '',
    canvasWidth: page.PIC_CanvasWidth && page.PIC_CanvasWidth > 0 ? page.PIC_CanvasWidth : 1920,
    canvasHeight: page.PIC_CanvasHeight && page.PIC_CanvasHeight > 0 ? page.PIC_CanvasHeight : 1080,
    durationSeconds: hasPeriodRestrictedContent ? Math.max(1, dynamicDurationSeconds) : pageDurationSeconds(page),
    volume: pageVolume(page, options.defaultVolume),
    hasExplicitVolume: Number.isFinite(page.PIC_Volume),
    slots,
  };
}

import { describe, expect, it } from 'vitest';
import { buildPagePlan } from '../src/domain/page-plan';
import type { PageInfoClass } from '../src/domain/models';

describe('buildPagePlan', () => {
  it('Windows PageInfoClass 모델에서 최대 6개 Media 슬롯을 z-index 순서로 만든다', () => {
    const page: PageInfoClass = {
      PIC_PageName: 'page',
      PIC_PlaytimeSecond: 9,
      PIC_Elements: Array.from({ length: 8 }, (_, index) => ({
        EIF_Name: `element-${index}`,
        EIF_Type: 'Media',
        EIF_Width: 100,
        EIF_Height: 100,
        EIF_PosLeft: index,
        EIF_PosTop: index,
        EIF_ZIndex: 8 - index,
        EIF_ContentsInfoClassList: [
          {
            CIF_FileName: `content-${index}.png`,
            CIF_ContentType: 'Image',
            CIF_PlayMinute: '00',
            CIF_PlaySec: '05',
          },
        ],
      })),
    };

    const plan = buildPagePlan(page, 'playlist');

    expect(plan.durationSeconds).toBe(9);
    expect(plan.slots).toHaveLength(6);
    expect(plan.slots[0].elementName).toBe('element-7');
    expect(plan.slots[5].elementName).toBe('element-2');
  });

  it('단일 영상 슬롯은 원본 Windows PlaybackCoordinator처럼 loop + timer 전환으로 구성한다', () => {
    const plan = buildPagePlan(
      {
        PIC_Elements: [
          {
            EIF_Name: 'video',
            EIF_Type: 'Media',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'video.mp4',
                CIF_ContentType: 'Video',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '10',
              },
            ],
          },
        ],
      },
      'playlist',
    );

    expect(plan.slots[0].items[0]).toMatchObject({
      contentType: 'Video',
      shouldLoop: true,
      transitionByTimer: true,
      durationSeconds: 10,
    });
  });

  it('원본 모델에서 재생 불가로 표시된 콘텐츠는 슬롯 플랜에서 제외한다', () => {
    const plan = buildPagePlan(
      {
        PIC_Elements: [
          {
            EIF_Name: 'media',
            EIF_Type: 'Media',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'valid.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
                CIF_ValidTime: true,
                CIF_FileExist: true,
              },
              {
                CIF_FileName: 'invalid-time.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
                CIF_ValidTime: false,
              },
              {
                CIF_FileName: 'missing.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
                CIF_FileExist: false,
              },
            ],
          },
        ],
      },
      'playlist',
    );

    expect(plan.slots[0].items.map((item) => item.name)).toEqual(['valid.png']);
  });

  it('원본 seamless 재생 경로처럼 Media가 아닌 요소는 슬롯 플랜에서 제외한다', () => {
    const plan = buildPagePlan(
      {
        PIC_Elements: [
          {
            EIF_Name: 'scroll',
            EIF_Type: 'ScrollText',
            EIF_Width: 1920,
            EIF_Height: 100,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_ZIndex: 0,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'scroll.txt',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
              },
            ],
          },
          {
            EIF_Name: 'welcome',
            EIF_Type: 'WelcomeBoard',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_ZIndex: 1,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'welcome.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
              },
            ],
          },
          {
            EIF_Name: 'media',
            EIF_Type: 'Media',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosLeft: 0,
            EIF_PosTop: 0,
            EIF_ZIndex: 2,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'media.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
              },
            ],
          },
        ],
      },
      'playlist',
    );

    expect(plan.slots[0].elementName).toBe('media');
    expect(plan.slots[0].items.map((item) => item.name)).toEqual(['media.png']);
    expect(plan.slots.slice(1).every((slot) => slot.items.length === 0)).toBe(true);
  });

  it('기간 스케줄이 있는 콘텐츠가 포함되면 현재 허용된 콘텐츠 시간 합으로 페이지 duration을 재계산한다', () => {
    const page: PageInfoClass = {
      PIC_PageName: 'period-page',
      PIC_PlaytimeSecond: 30,
      PIC_Elements: [
        {
          EIF_Name: 'media',
          EIF_Type: 'Media',
          EIF_Width: 1920,
          EIF_Height: 1080,
          EIF_PosLeft: 0,
          EIF_PosTop: 0,
          EIF_ZIndex: 0,
          EIF_ContentsInfoClassList: [
            {
              CIF_StrGUID: 'always',
              CIF_FileName: 'always.png',
              CIF_ContentType: 'Image',
              CIF_PlayMinute: '00',
              CIF_PlaySec: '10',
            },
            {
              CIF_StrGUID: 'allowed',
              CIF_FileName: 'allowed.png',
              CIF_ContentType: 'Image',
              CIF_PlayMinute: '00',
              CIF_PlaySec: '10',
            },
            {
              CIF_StrGUID: 'blocked',
              CIF_FileName: 'blocked.png',
              CIF_ContentType: 'Image',
              CIF_PlayMinute: '00',
              CIF_PlaySec: '10',
            },
          ],
        },
      ],
    };

    const plan = buildPagePlan(page, 'playlist', {
      hasContentPeriod: (content) => content.CIF_StrGUID === 'allowed' || content.CIF_StrGUID === 'blocked',
      isContentAllowed: (content) => content.CIF_StrGUID !== 'blocked',
    });

    expect(plan.durationSeconds).toBe(20);
    expect(plan.slots[0].items.map((item) => item.id)).toEqual(['always', 'allowed', 'blocked']);
  });
});

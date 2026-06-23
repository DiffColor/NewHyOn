import type { PlayerManifest } from '../domain/models';

export const ANDROID_INTRO_VIDEO_FILE = 'media/intro.mp4';
export const ANDROID_INTRO_VIDEO_PATH = `wgt-package/${ANDROID_INTRO_VIDEO_FILE}`;

export function createAndroidIntroManifest(preserveAspectRatio: boolean): PlayerManifest {
  return {
    playlistName: 'Android Intro',
    preserveAspectRatio,
    pages: [
      {
        PIC_PageName: 'Android Intro',
        PIC_PlaytimeSecond: 15,
        PIC_CanvasWidth: 1920,
        PIC_CanvasHeight: 1080,
        PIC_Elements: [
          {
            EIF_Name: 'Android Intro Video',
            EIF_Type: 'Media',
            EIF_Width: 1920,
            EIF_Height: 1080,
            EIF_PosTop: 0,
            EIF_PosLeft: 0,
            EIF_ZIndex: 0,
            EIF_IsMuted: false,
            EIF_ContentsInfoClassList: [
              {
                CIF_FileName: 'intro.mp4',
                CIF_FileFullPath: ANDROID_INTRO_VIDEO_PATH,
                CIF_ContentType: 'Video',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '15',
                CIF_StrGUID: 'android-intro-video',
                CIF_FileExist: true,
              },
              {
                CIF_FileName: 'intro.mp4',
                CIF_FileFullPath: ANDROID_INTRO_VIDEO_PATH,
                CIF_ContentType: 'Video',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '15',
                CIF_StrGUID: 'android-intro-video-repeat',
                CIF_FileExist: true,
              },
            ],
          },
        ],
      },
    ],
  };
}

export const DEFAULT_MANIFEST: PlayerManifest = createAndroidIntroManifest(false);

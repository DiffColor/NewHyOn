export const REMOTE_KEY_REGISTRATION_LIST = [
  'MediaPlay',
  'MediaPause',
  'MediaPlayPause',
  'MediaStop',
  'MediaFastForward',
  'MediaRewind',
  'ColorF0Red',
  'ColorF1Green',
  'ColorF2Yellow',
  'ColorF3Blue',
  '0',
  'Menu',
  'Tools',
  'Info',
] as const;

export type RemoteControlAction =
  | 'toggle-playback'
  | 'stop-playback'
  | 'next-page'
  | 'previous-page'
  | 'open-settings'
  | 'toggle-hud'
  | 'unhandled';

export interface RemoteKeyboardEventLike {
  readonly key?: string;
  readonly code?: string;
  readonly keyCode?: number;
  readonly which?: number;
}

export function resolveRemoteControlAction(event: RemoteKeyboardEventLike): RemoteControlAction {
  const keyCode = event.keyCode ?? event.which;

  switch (event.key) {
    case 'MediaPlay':
    case 'MediaPause':
    case 'MediaPlayPause':
      return 'toggle-playback';
    case 'MediaStop':
      return 'stop-playback';
    case 'MediaFastForward':
      return 'next-page';
    case 'MediaRewind':
      return 'previous-page';
    case 'ColorF0Red':
    case 'Red':
    case 'ColorRed':
    case 'A':
    case 'a':
    case '0':
    case 'Digit0':
    case 'Numpad0':
    case 'Info':
    case 'Menu':
    case 'Tools':
    case 'Settings':
      return 'open-settings';
    case 'ColorF1Green':
    case 'Green':
    case 'ColorGreen':
    case 'B':
    case 'b':
      return 'toggle-hud';
    case 'ColorF2Yellow':
    case 'Yellow':
    case 'ColorYellow':
    case 'C':
    case 'c':
      return 'previous-page';
    case 'ColorF3Blue':
    case 'Blue':
    case 'ColorBlue':
    case 'D':
    case 'd':
      return 'next-page';
    default:
      break;
  }

  switch (event.code) {
    case 'Digit0':
    case 'Numpad0':
      return 'open-settings';
    default:
      break;
  }

  switch (keyCode) {
    case 19:
    case 415:
    case 10252:
      return 'toggle-playback';
    case 413:
      return 'stop-playback';
    case 417:
    case 406:
    case 68:
      return 'next-page';
    case 412:
    case 405:
    case 67:
      return 'previous-page';
    case 48:
    case 96:
    case 403:
    case 65:
    case 18:
    case 10135:
    case 457:
      return 'open-settings';
    case 404:
    case 66:
      return 'toggle-hud';
    default:
      return 'unhandled';
  }
}

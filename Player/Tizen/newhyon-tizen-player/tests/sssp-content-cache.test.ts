import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  cacheRemoteManifestContent,
  countRemoteManifestContent,
  countRemoteManifestContentForOptions,
} from '../src/app/sssp-content-cache';
import type { PlayerManifest } from '../src/domain/models';

function createManifest(): PlayerManifest {
  return {
    playlistName: 'remote-list',
    preserveAspectRatio: false,
    pages: [
      {
        PIC_PageName: 'page',
        PIC_PlaytimeSecond: 10,
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
                CIF_FileName: 'video.mp4',
                CIF_FileFullPath: 'https://cdn.example.com/media/video.mp4',
                CIF_ContentType: 'Video',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '10',
                CIF_StrGUID: 'content-guid-1',
              },
              {
                CIF_FileName: 'local.png',
                CIF_FileFullPath: 'downloads/local.png',
                CIF_ContentType: 'Image',
                CIF_PlayMinute: '00',
                CIF_PlaySec: '05',
              },
            ],
          },
        ],
      },
    ],
  };
}

describe('cacheRemoteManifestContent', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    window.localStorage.clear();
    window.tizen = {
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: () => false,
      },
      DownloadRequest: vi.fn(function DownloadRequest(
        this: DownloadRequest,
        url: string,
        destination?: string | null,
        fileName?: string | null,
        networkType?: 'CELLULAR' | 'WIFI' | 'ALL' | null,
      ) {
        this.url = url;
        this.destination = destination;
        this.fileName = fileName;
        this.networkType = networkType;
      }) as unknown as DownloadRequestConstructor,
      download: {
        start: vi.fn((_request, callback) => {
          callback?.oncompleted?.(1, 'downloads/content-guid-1-test.mp4');
          return 1;
        }),
      },
      ApplicationControlData: vi.fn(function ApplicationControlData(
        this: TizenApplicationControlData,
        key: string,
        value: string[],
      ) {
        this.key = key;
        this.value = value;
      }) as unknown as TizenApplicationControlDataConstructor,
      ApplicationControl: vi.fn(function ApplicationControl(
        this: TizenApplicationControl,
        operation: string,
        uri?: string | null,
        mime?: string | null,
        category?: string | null,
        data?: TizenApplicationControlData[] | null,
        launchMode?: 'SINGLE' | 'GROUP' | null,
      ) {
        this.operation = operation;
        this.uri = uri;
        this.mime = mime;
        this.category = category;
        this.data = data;
        this.launchMode = launchMode;
      }) as unknown as TizenApplicationControlConstructor,
      application: {
        launchAppControl: vi.fn((appControl: TizenApplicationControl, _appId, _onsuccess, _onerror, replyCallback) => {
          const fileName = appControl.data?.find((item) => item.key === 'fileName')?.value[0] ?? 'content.bin';
          replyCallback?.onsuccess?.([
            { key: 'status', value: ['ok'] },
            { key: 'path', value: [`downloads/${fileName}`] },
          ]);
        }),
      },
    };
  });

  it('원격 HTTP 콘텐츠를 Tizen Download API로 받고 manifest 경로를 downloads 가상 경로로 바꾼다', async () => {
    const progress = vi.fn();
    const manifest = createManifest();

    const cached = await cacheRemoteManifestContent(manifest, { onProgress: progress });

    const content = cached.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    expect(content?.CIF_FileFullPath).toBe('downloads/content-guid-1-test.mp4');
    expect(content?.CIF_RelativePath).toBe('downloads/content-guid-1-test.mp4');
    expect(content?.CIF_FileExist).toBe(true);
    expect(window.tizen?.download?.start).toHaveBeenCalledTimes(1);
    expect(progress).toHaveBeenCalledWith({
      completed: 1,
      total: 1,
      fileName: 'video.mp4',
    });
  });

  it('원격 HTTP 콘텐츠 다운로드가 완료되지 않으면 timeout으로 실패하고 다운로드를 취소한다', async () => {
    vi.useFakeTimers();
    const cancel = vi.fn();
    window.tizen = {
      ...window.tizen,
      download: {
        start: vi.fn(() => 7),
        cancel,
      },
    };

    const cachePromise = cacheRemoteManifestContent(createManifest());
    const rejection = expect(cachePromise).rejects.toThrow(
      /^콘텐츠 다운로드 시간 초과: content-guid-1-[0-9a-f]{8}\.mp4 \(60000ms; url=https:\/\/cdn\.example\.com\/media\/video\.mp4\)$/,
    );
    await vi.advanceTimersByTimeAsync(60000);

    await rejection;
    expect(cancel).toHaveBeenCalledWith(7);
  });

  it('이미 저장된 콘텐츠는 다운로드를 다시 시작하지 않는다', async () => {
    window.tizen = {
      ...window.tizen,
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path.startsWith('downloads/content-guid-1-'),
      },
    };

    const cached = await cacheRemoteManifestContent(createManifest());

    expect(window.tizen?.download?.start).not.toHaveBeenCalled();
    expect(cached.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0]?.CIF_FileFullPath)
      .toMatch(/^downloads\/content-guid-1-/);
  });

  it('HTTP 이미지는 Contents/tizen 리사이즈 이미지를 먼저 받아 같은 로컬 캐시 파일명으로 저장한다', async () => {
    const manifest = createManifest();
    manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList = [
      {
        CIF_FileName: 'hero.jpg',
        CIF_FileFullPath: 'https://cdn.example.com/media/hero.jpg',
        CIF_ContentType: 'Image',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '05',
        CIF_StrGUID: 'image-guid-1',
      },
    ];
    window.tizen = {
      ...window.tizen,
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: () => false,
      },
      download: {
        start: vi.fn((request, callback) => {
          callback?.oncompleted?.(1, `downloads/${request.fileName}`);
          return 1;
        }),
      },
    };
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, {
      status: 200,
      headers: { 'content-length': '12345' },
    })));

    const cached = await cacheRemoteManifestContent(manifest);
    const request = vi.mocked(window.tizen!.DownloadRequest!).mock.calls[0];

    expect(fetch).toHaveBeenCalledWith('https://cdn.example.com/Contents/tizen/hero.jpg', {
      method: 'HEAD',
      cache: 'no-store',
    });
    expect(request?.[0]).toBe('https://cdn.example.com/Contents/tizen/hero.jpg');
    expect(window.tizen?.download?.start).toHaveBeenCalledTimes(1);
    expect(cached.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0]?.CIF_FileFullPath)
      .toMatch(/^downloads\/image-guid-1-/);
  });

  it('업데이트 네임스페이스가 있으면 기존 재생 파일과 다른 캐시 파일명으로 다운로드한다', async () => {
    const pathExists = vi.fn((_path: string) => false);
    window.tizen = {
      ...window.tizen,
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists,
      },
    };

    await cacheRemoteManifestContent(createManifest(), { cacheNamespace: 'cmd-1' });

    const request = vi.mocked(window.tizen!.DownloadRequest!).mock.calls[0];
    expect(pathExists.mock.calls[0]?.[0]).toMatch(/^downloads\/cmd-1-[0-9a-f]{8}-content-guid-1-/);
    expect(request?.[2]).toMatch(/^cmd-1-[0-9a-f]{8}-content-guid-1-/);
  });

  it('리사이즈 원격 크기와 저장된 리사이즈 캐시 크기가 같으면 다운로드를 건너뛴다', async () => {
    const manifest = createManifest();
    manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList = [
      {
        CIF_FileName: 'hero.jpg',
        CIF_FileFullPath: 'https://cdn.example.com/media/hero.jpg',
        CIF_ContentType: 'Image',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '05',
        CIF_StrGUID: 'image-guid-1',
      },
    ];
    window.tizen = {
      ...window.tizen,
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path.startsWith('downloads/image-guid-1-'),
      },
    };
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, {
      status: 200,
      headers: { 'content-length': '12345' },
    })));
    window.localStorage.setItem(
      'newhyon-tizen-player.tizen-image-cache.v1',
      JSON.stringify({ 'downloads/image-guid-1-f5607053.jpg': { size: 12345 } }),
    );

    const cached = await cacheRemoteManifestContent(manifest);

    expect(window.tizen?.download?.start).not.toHaveBeenCalled();
    expect(cached.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0]?.CIF_FileFullPath)
      .toBe('downloads/image-guid-1-f5607053.jpg');
  });

  it('같은 로컬 캐시 파일명이 있어도 리사이즈 원격 크기가 다르면 다시 다운로드해 교체한다', async () => {
    const manifest = createManifest();
    manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList = [
      {
        CIF_FileName: 'hero.jpg',
        CIF_FileFullPath: 'https://cdn.example.com/media/hero.jpg',
        CIF_ContentType: 'Image',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '05',
        CIF_StrGUID: 'image-guid-1',
      },
    ];
    window.tizen = {
      ...window.tizen,
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: (path) => path.startsWith('downloads/image-guid-1-'),
      },
      download: {
        start: vi.fn((request, callback) => {
          callback?.oncompleted?.(1, `downloads/${request.fileName}`);
          return 1;
        }),
      },
    };
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, {
      status: 200,
      headers: { 'content-length': '12345' },
    })));
    window.localStorage.setItem(
      'newhyon-tizen-player.tizen-image-cache.v1',
      JSON.stringify({ 'downloads/image-guid-1-f5607053.jpg': { size: 54321 } }),
    );

    await cacheRemoteManifestContent(manifest);

    expect(window.tizen?.download?.start).toHaveBeenCalledTimes(1);
    expect(window.localStorage.getItem('newhyon-tizen-player.tizen-image-cache.v1'))
      .toContain('"size":12345');
  });

  it('리사이즈 이미지가 없으면 원본 캐시 경로로 manifest를 바꾼다', async () => {
    const getContext = vi.spyOn(HTMLCanvasElement.prototype, 'getContext');
    const toBlob = vi.spyOn(HTMLCanvasElement.prototype, 'toBlob');
    const openFile = vi.fn();
    const manifest = createManifest();
    manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList = [
      {
        CIF_FileName: 'hero.jpg',
        CIF_FileFullPath: 'https://cdn.example.com/media/hero.jpg',
        CIF_ContentType: 'Image',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '05',
        CIF_StrGUID: 'image-guid-1',
      },
    ];
    window.tizen = {
      ...window.tizen,
      filesystem: {
        toURI: (path) => `file:///opt/usr/home/owner/content/${path}`,
        pathExists: () => false,
        openFile,
      },
      download: {
        start: vi.fn((_request, callback) => {
          callback?.oncompleted?.(1, 'downloads/image-guid-1-source.jpg');
          return 1;
        }),
      },
    };
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, {
      status: 404,
      statusText: 'Not Found',
    })));

    const cached = await cacheRemoteManifestContent(manifest);
    const content = cached.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    const request = vi.mocked(window.tizen!.DownloadRequest!).mock.calls[0];

    expect(request?.[0]).toBe('https://cdn.example.com/media/hero.jpg');
    expect(content?.CIF_FileFullPath).toBe('downloads/image-guid-1-source.jpg');
    expect(content?.CIF_RelativePath).toBe('downloads/image-guid-1-source.jpg');
    expect(openFile).not.toHaveBeenCalled();
    expect(getContext).not.toHaveBeenCalled();
    expect(toBlob).not.toHaveBeenCalled();
  });

  it('FTP 설정과 콘텐츠 상대경로를 분리해 downloader service에 전달한다', async () => {
    const manifest = createManifest();
    const content = manifest.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    if (!content) {
      throw new Error('테스트 콘텐츠가 없습니다.');
    }
    content.CIF_FileFullPath = '';
    content.CIF_RelativePath = 'Campaign/video.mp4';

    await cacheRemoteManifestContent(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 10021,
        basePath: '/NewHyOnEnt',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    });

    const launch = vi.mocked(window.tizen!.application!.launchAppControl!);
    const request = launch.mock.calls[0]?.[0];
    expect(request?.data?.find((item) => item.key === 'host')?.value[0]).toBe('192.168.50.10');
    expect(request?.data?.find((item) => item.key === 'port')?.value[0]).toBe('10021');
    expect(request?.data?.find((item) => item.key === 'userName')?.value[0]).toBe('asdf');
    expect(request?.data?.find((item) => item.key === 'password')?.value[0]).toBe('Emfndhk!');
    expect(request?.data?.find((item) => item.key === 'remotePath')?.value[0])
      .toBe('/NewHyOnEnt/Campaign/video.mp4');
    expect(window.tizen?.download?.start).not.toHaveBeenCalled();
    expect(countRemoteManifestContentForOptions(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 10021,
        basePath: '/NewHyOnEnt',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    })).toBe(1);
  });

  it('Windows 절대 CIF_FileFullPath가 있어도 FTP 업데이트는 CIF_RelativePath를 사용한다', async () => {
    const manifest = createManifest();
    const content = manifest.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    if (!content) {
      throw new Error('테스트 콘텐츠가 없습니다.');
    }
    content.CIF_FileFullPath = 'C:\\Users\\Turtle-MSI\\Documents\\Turtle Lab\\NewHyOn Manager\\Contents\\video.mp4';
    content.CIF_RelativePath = 'Contents/video.mp4';

    await cacheRemoteManifestContent(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 21,
        basePath: '/NewHyOn',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    });

    const launch = vi.mocked(window.tizen!.application!.launchAppControl!);
    const request = launch.mock.calls[0]?.[0];
    expect(request?.data?.find((item) => item.key === 'remotePath')?.value[0])
      .toBe('/NewHyOn/Contents/video.mp4');
    expect(countRemoteManifestContentForOptions(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 21,
        basePath: '/NewHyOn',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    })).toBe(1);
  });

  it('Windows 절대 CIF_FileFullPath만 있으면 FTP 업데이트는 CIF_FileName을 사용한다', async () => {
    const manifest = createManifest();
    const content = manifest.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    if (!content) {
      throw new Error('테스트 콘텐츠가 없습니다.');
    }
    content.CIF_FileName = 'server-image.jpg';
    content.CIF_FileFullPath = 'C:\\Users\\Turtle-MSI\\Documents\\Turtle Lab\\NewHyOn Manager\\Contents\\server-image.jpg';
    content.CIF_RelativePath = '';

    await cacheRemoteManifestContent(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 21,
        basePath: '/NewHyOn',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    });

    const launch = vi.mocked(window.tizen!.application!.launchAppControl!);
    const request = launch.mock.calls[0]?.[0];
    expect(request?.data?.find((item) => item.key === 'remotePath')?.value[0])
      .toBe('/NewHyOn/server-image.jpg');
  });

  it('FTP 콘텐츠 상대경로가 서버 기본 디렉터리를 이미 포함하면 중복으로 붙이지 않는다', async () => {
    const manifest = createManifest();
    const content = manifest.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    if (!content) {
      throw new Error('테스트 콘텐츠가 없습니다.');
    }
    content.CIF_FileFullPath = '';
    content.CIF_RelativePath = '/NewHyOn/Campaign/video.mp4';

    await cacheRemoteManifestContent(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 21,
        basePath: '/NewHyOn',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    });

    const launch = vi.mocked(window.tizen!.application!.launchAppControl!);
    const request = launch.mock.calls[0]?.[0];
    expect(launch.mock.calls[0]?.[1]).toBe('NewHyOnFtpD01.Downloader');
    expect(request?.data?.find((item) => item.key === 'remotePath')?.value[0])
      .toBe('/NewHyOn/Campaign/video.mp4');
  });

  it('FTP URL 형식 콘텐츠도 서비스 호출에서는 접속정보와 경로를 분리한다', async () => {
    const manifest = createManifest();
    const content = manifest.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    if (!content) {
      throw new Error('테스트 콘텐츠가 없습니다.');
    }
    content.CIF_FileFullPath = 'ftp://ftp-user:ftp-pass@192.168.50.10:21/NewHyOn/Campaign/video.mp4';
    content.CIF_RelativePath = '';

    await cacheRemoteManifestContent(manifest);

    const launch = vi.mocked(window.tizen!.application!.launchAppControl!);
    const request = launch.mock.calls[0]?.[0];
    expect(request?.data?.find((item) => item.key === 'host')?.value[0]).toBe('192.168.50.10');
    expect(request?.data?.find((item) => item.key === 'port')?.value[0]).toBe('21');
    expect(request?.data?.find((item) => item.key === 'userName')?.value[0]).toBe('ftp-user');
    expect(request?.data?.find((item) => item.key === 'password')?.value[0]).toBe('ftp-pass');
    expect(request?.data?.find((item) => item.key === 'remotePath')?.value[0])
      .toBe('/NewHyOn/Campaign/video.mp4');
  });

  it('SSSP 다운로드 한도를 넘지 않도록 원격 콘텐츠를 하나씩 순차 다운로드한다', async () => {
    const callbacks: DownloadCallback[] = [];
    const manifest = createManifest();
    manifest.pages[0]!.PIC_Elements![0]!.EIF_ContentsInfoClassList = [
      {
        CIF_FileName: 'first.mp4',
        CIF_FileFullPath: 'https://cdn.example.com/media/first.mp4',
        CIF_ContentType: 'Video',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '10',
        CIF_StrGUID: 'content-guid-1',
      },
      {
        CIF_FileName: 'second.mp4',
        CIF_FileFullPath: 'https://cdn.example.com/media/second.mp4',
        CIF_ContentType: 'Video',
        CIF_PlayMinute: '00',
        CIF_PlaySec: '10',
        CIF_StrGUID: 'content-guid-2',
      },
    ];
    window.tizen = {
      ...window.tizen,
      download: {
        start: vi.fn((_request, callback) => {
          if (callback) {
            callbacks.push(callback);
          }
          return callbacks.length;
        }),
      },
    };

    const cachePromise = cacheRemoteManifestContent(manifest);
    await Promise.resolve();

    expect(window.tizen?.download?.start).toHaveBeenCalledTimes(1);
    callbacks[0]?.oncompleted?.(1, 'downloads/first.mp4');
    await Promise.resolve();
    await Promise.resolve();
    expect(window.tizen?.download?.start).toHaveBeenCalledTimes(2);
    callbacks[1]?.oncompleted?.(2, 'downloads/second.mp4');

    const cached = await cachePromise;
    const contents = cached.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList ?? [];
    expect(contents.map((content) => content.CIF_FileFullPath)).toEqual([
      'downloads/first.mp4',
      'downloads/second.mp4',
    ]);
  });

  it('다운로드 실패 메시지에는 FTP 계정정보를 노출하지 않고 포트와 경로만 남긴다', async () => {
    const manifest = createManifest();
    const content = manifest.pages[0]?.PIC_Elements?.[0]?.EIF_ContentsInfoClassList?.[0];
    if (!content) {
      throw new Error('테스트 콘텐츠가 없습니다.');
    }
    content.CIF_FileFullPath = '';
    content.CIF_RelativePath = 'Campaign/video.mp4';
    window.tizen = {
      ...window.tizen,
      application: {
        launchAppControl: vi.fn((_appControl, _appId, _onsuccess, _onerror, replyCallback) => {
          replyCallback?.onsuccess?.([
            { key: 'status', value: ['error'] },
            { key: 'error', value: ['Input/output error'] },
          ]);
        }),
      },
    };

    await expect(cacheRemoteManifestContent(manifest, {
      ftp: {
        host: '192.168.50.10',
        port: 21,
        basePath: '/NewHyOn',
        userName: 'asdf',
        password: 'Emfndhk!',
      },
    })).rejects.toThrow(
      /^FTP 콘텐츠 다운로드 실패: content-guid-1-[0-9a-f]{8}\.mp4 \(Input\/output error; path=192\.168\.50\.10:21\/NewHyOn\/Campaign\/video\.mp4\)$/,
    );
  });

  it('원격 콘텐츠 수를 계산한다', () => {
    expect(countRemoteManifestContent(createManifest())).toBe(1);
  });
});

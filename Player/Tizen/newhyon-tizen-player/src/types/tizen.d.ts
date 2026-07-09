export {};

declare global {
  interface Window {
    tizen?: TizenGlobal;
    webapis?: WebApisGlobal;
    NEWHYON_PLAYER_MANIFEST?: unknown;
    NEWHYON_PLAYER_HEALTH?: RuntimeHealthSnapshot;
    NEWHYON_AVPLAY_DEBUG?: () => RuntimeAvplaySessionSnapshot[];
  }

  interface TizenGlobal {
    tvinputdevice?: {
      getSupportedKeys?(): InputDeviceKey[];
      getKey?(keyName: string): InputDeviceKey | null;
      registerKey(keyName: string): void;
      registerKeyBatch?(keys: string[], successCallback?: () => void, errorCallback?: (error: unknown) => void): void;
    };
    tvaudiocontrol?: {
      setMute?(mute: boolean): void;
      isMute?(): boolean;
      setVolume?(volume: number): void;
      getVolume?(): number;
      setVolumeChangeListener?(listener: (volume: number) => void): void;
      unsetVolumeChangeListener?(): void;
    };
    filesystem?: {
      toURI(path: string): string;
      pathExists?(path: string): boolean;
      openFile?(
        path: string,
        mode: 'r' | 'rw' | 'w' | 'a',
        onsuccess: (file: TizenFileHandle) => void,
        onerror?: (error: unknown) => void,
        makeParents?: boolean,
      ): void;
      resolve?(
        location: string,
        onsuccess: (file: TizenFile) => void,
        onerror?: (error: unknown) => void,
        mode?: 'r' | 'rw',
      ): void;
    };
    application?: {
      getCurrentApplication?(): TizenApplication;
      launchAppControl?(
        appControl: TizenApplicationControl,
        appId?: string | null,
        onsuccess?: () => void,
        onerror?: (error: unknown) => void,
        replyCallback?: TizenApplicationControlReplyCallback | null,
      ): void;
    };
    ApplicationControl?: TizenApplicationControlConstructor;
    ApplicationControlData?: TizenApplicationControlDataConstructor;
    systeminfo?: {
      getPropertyValue?(
        property: string,
        onsuccess: (info: unknown) => void,
        onerror?: (error: unknown) => void,
      ): void;
    };
    download?: DownloadManager;
    DownloadRequest?: DownloadRequestConstructor;
  }

  interface TizenApplication {
    readonly appInfo?: {
      readonly version?: string;
    };
  }

  interface TizenApplicationControlConstructor {
    new (
      operation: string,
      uri?: string | null,
      mime?: string | null,
      category?: string | null,
      data?: TizenApplicationControlData[] | null,
      launchMode?: 'SINGLE' | 'GROUP' | null,
    ): TizenApplicationControl;
  }

  interface TizenApplicationControl {
    operation: string;
    uri?: string | null;
    mime?: string | null;
    category?: string | null;
    data?: TizenApplicationControlData[] | null;
    launchMode?: 'SINGLE' | 'GROUP' | null;
  }

  interface TizenApplicationControlDataConstructor {
    new (key: string, value: string[]): TizenApplicationControlData;
  }

  interface TizenApplicationControlData {
    key: string;
    value: string[];
  }

  interface TizenApplicationControlReplyCallback {
    onsuccess?(data?: TizenApplicationControlData[] | null): void;
    onfailure?(): void;
  }

  interface RuntimeHealthSnapshot {
    schemaVersion: 1;
    updatedAt: string;
    stage: string;
    state: string;
    playlist: string;
    page: string;
    elapsed: string;
    lastKey: string;
    lastAction: string;
    platform: string;
    slots: string[];
    avplaySessions?: RuntimeAvplaySessionSnapshot[];
    message: string;
    recentLogs: Array<{
      readonly timestamp: string;
      readonly level: string;
      readonly scope: string;
      readonly message: string;
    }>;
    diagnostics: {
      pageStartCount: number;
      contentShowCount: number;
      lastContent: string;
      masterTickDelayMs: number;
      masterTickIntervalMs: number;
      renderIntervalMs: number;
      communicationStatus: string;
      dbStatus: string;
      dbStatusDetail: string;
      signalrStatus: string;
      signalrStatusDetail: string;
      ftpStatus: string;
      ftpStatusDetail: string;
      heartbeatStatus: string;
      heartbeatStatusDetail: string;
      authStatus: string;
      authStatusDetail: string;
      updateStatus: string;
      updatePhase: string;
      updateProgress: number;
      updateCompleted: number;
      updateTotal: number;
      updateCurrentFile: string;
      updateCommandId: string;
      playerGuid: string;
      playerName: string;
      dataServerAddress: string;
      messageServerAddress: string;
      signalrUrl: string;
      ftpEndpoint: string;
    };
    settings: {
      preserveAspectRatio: boolean;
      switchOnContentEnd: boolean;
      defaultVolume: number;
      hudInitiallyVisible: boolean;
    };
  }

  interface RuntimeAvplaySessionSnapshot {
    readonly sessionIndex: number;
    readonly currentLaneIndex: number | null;
    readonly heldLaneIndex: number | null;
    readonly currentItemName: string | null;
    readonly lanes: RuntimeAvplayLaneSnapshot[];
  }

  interface RuntimeAvplayLaneSnapshot {
    readonly laneIndex: number;
    readonly role: 'current' | 'held' | 'next-content' | 'next-schedule-content' | 'next-update-content' | 'idle';
    readonly itemName: string | null;
    readonly playbackMode: 'mixedframe' | 'direct' | null;
    readonly state: string;
    readonly queriedCurrentTimeMs: number | null;
    readonly queriedDurationMs: number | null;
    readonly callbackCurrentTimeMs: number | null;
    readonly callbackAgeMs: number | null;
    readonly buffering: boolean;
    readonly audioMuted: boolean | null;
    readonly lastPlayAt: string | null;
    readonly lastPrepareCompletedAt: string | null;
    readonly lastBufferingStartAt: string | null;
    readonly lastBufferingCompleteAt: string | null;
    readonly lastStreamCompletedAt: string | null;
    readonly lastError: string | null;
    readonly visibility: string;
    readonly zIndex: string;
    readonly rect: string;
  }

  interface TizenFile {
    readonly name?: string;
    readonly fileSize?: number;
    resolve(path: string): TizenFile;
    createFile(relativeFilePath: string): TizenFile;
    openStream(
      mode: 'r' | 'w' | 'a',
      onsuccess: (stream: TizenFileStream) => void,
      onerror?: (error: unknown) => void,
      encoding?: string,
    ): void;
  }

  interface TizenFileStream {
    position?: number;
    readonly bytesAvailable?: number;
    read?(count: number): string;
    readBytes?(byteCount: number): number[];
    write(text: string): void;
    close(): void;
  }

  interface TizenFileHandle {
    writeBlob?(blob: Blob): void;
    writeBlobNonBlocking?(blob: Blob, onsuccess?: () => void, onerror?: (error: unknown) => void): void;
    close?(): void;
    closeNonBlocking?(onsuccess?: () => void, onerror?: (error: unknown) => void): void;
  }

  interface InputDeviceKey {
    name: string;
    code: number;
  }

  interface WebApisGlobal {
    avplay?: AVPlayApi;
    avplaystore?: AVPlayStoreManager;
    network?: NetworkManager;
    productinfo?: ProductInfoManager;
    remotepower?: RemotePowerManager;
    systemcontrol?: SystemControlManager;
    widgetdata?: WidgetDataManager;
  }

  interface ProductInfoManager {
    getDuid?(): string;
    getModelCode?(): string;
  }

  interface NetworkManager {
    getVersion?(): string;
    isConnectedToGateway?(): boolean;
    getMac?(): string;
    getIp?(): string;
    getActiveConnectionType?(): string | number;
  }

  interface DownloadRequestConstructor {
    new (
      url: string,
      destination?: string | null,
      fileName?: string | null,
      networkType?: 'CELLULAR' | 'WIFI' | 'ALL' | null,
      httpHeader?: Record<string, string> | null,
    ): DownloadRequest;
  }

  interface DownloadRequest {
    url: string;
    destination?: string | null;
    fileName?: string | null;
    networkType?: 'CELLULAR' | 'WIFI' | 'ALL' | null;
    httpHeader?: Record<string, string> | null;
  }

  interface DownloadManager {
    start(downloadRequest: DownloadRequest, downloadCallback?: DownloadCallback | null): number;
    cancel?(downloadId: number): void;
    pause?(downloadId: number): void;
    resume?(downloadId: number): void;
    getState?(downloadId: number): 'QUEUED' | 'DOWNLOADING' | 'PAUSED' | 'CANCELED' | 'COMPLETED' | 'FAILED';
  }

  interface DownloadCallback {
    onprogress?(downloadId: number, receivedSize: number, totalSize: number): void;
    onpaused?(downloadId: number): void;
    oncanceled?(downloadId: number): void;
    oncompleted?(downloadId: number, path: string): void;
    onfailed?(downloadId: number, error: unknown): void;
  }

  interface WidgetDataManager {
    read(onsuccess: (data: string) => void, onerror?: (error: unknown) => void): void;
    write(data: string, onsuccess?: () => void, onerror?: (error: unknown) => void): void;
    remove(onsuccess?: () => void, onerror?: (error: unknown) => void): void;
  }

  interface RemotePowerManager {
    getVersion?(): string;
    powerOn?(): void;
    powerOff?(): void;
    getPowerState?(): 'NORMAL' | 'STANDBY';
    getVirtualStandbyMode?(): 'ACTIVATION' | 'DEACTIVATION';
    getRemoteConfiguration?(): 'ON' | 'OFF';
    setRemoteConfiguration?(info: 'ON' | 'OFF'): void;
  }

  interface SystemControlManager {
    getVersion?(): string;
    rebootDevice?(): void;
    captureScreen?(location?: 'wgt-private-data' | 'wgt-private-tmp'): void;
    getFirmwareVersion?(): string;
    getSerialNumber?(): string;
    setPanelMute?(state: 'ON' | 'OFF'): void;
    getPanelMute?(): 'ON' | 'OFF';
    setMessageDisplay?(state: 'ON' | 'OFF'): void;
    getMessageDisplay?(): 'ON' | 'OFF';
    setSafetyLock?(state: 'ON' | 'OFF'): void;
    getSafetyLock?(): 'ON' | 'OFF';
    setPCConnection?(connection: 'RJ45' | 'RS232'): void;
    getPCConnection?(): 'RJ45' | 'RS232';
    setScreenLampSchedule?(info: {
      use: 'ON' | 'OFF';
      firstTime: string;
      level1: number;
      secondTime: string;
      level2: number;
    }): void;
    getScreenLampSchedule?(): unknown;
  }

  interface AVPlayApi {
    open(url: string): void;
    prepare(): void;
    prepareAsync(successCallback: () => void, errorCallback?: (error: AVPlayErrorLike) => void): void;
    play(): void;
    pause(): void;
    stop(): void;
    close(): void;
    setListener(listener: AVPlayListener): void;
    setDisplayRect(left: number, top: number, width: number, height: number): void;
    setDisplayMethod?(method: string): void;
    setStreamingProperty?(name: string, value?: string): void;
    setTimeoutForBuffering?(seconds: number): void;
    setLooping?(isLooping: boolean): void;
    setVideoStillMode?(mode: string): void;
    disableAudioStream?(): void;
    enableAudioStream?(): void;
    getState?(): string;
    getCurrentTime?(): number;
    getDuration?(): number;
  }

  interface AVPlayStoreManager {
    getPlayer(): AVPlayApi;
  }

  interface AVPlayListener {
    onbufferingstart?: () => void;
    onbufferingcomplete?: () => void;
    oncurrentplaytime?: (currentTime: number) => void;
    onstreamcompleted?: () => void;
    onerror?: (error: AVPlayErrorLike) => void;
    onerrormsg?: (error: AVPlayErrorLike, message: string) => void;
  }

  interface AVPlayErrorLike {
    name?: string;
    message?: string;
    code?: string | number;
  }
}

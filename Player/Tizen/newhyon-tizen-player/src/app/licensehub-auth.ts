import { sha256 } from '@noble/hashes/sha2.js';

const PRODUCT_ID = 8;
const API_BASE_URL = 'https://licensehub.ilycode.app';
const STORAGE_KEY = 'newhyon-tizen-player.licensehub.v1';
const OFFLINE_SLOT_SECONDS = 300;
const OFFLINE_CODE_CHARSET = '23456789ABCDEFGHJKLMNPQRSTUVWXYZ';
const PUBLIC_KEY_PEM = `-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAhMxB0eP/q5vyjFOwWENS68uq/d1
hmq6Uv1tHpjMExWVgY3jhbDZ9dM9EyWJ3XXCI8IMgSyF6pKEm6K3LplFHQ==
-----END PUBLIC KEY-----`;

export type LicenseAuthMode = 'ONLINE' | 'OFFLINE';

export interface LicenseAuthContext {
  readonly playerName: string;
  readonly playerGuid: string;
  readonly appVersion: string;
}

export interface LicenseAuthState {
  readonly isValid: boolean;
  readonly mode: LicenseAuthMode | 'NONE';
  readonly status: string;
  readonly reason: string;
  readonly deviceFingerprint: string;
  readonly deviceId: string;
  readonly licenseToken: string;
  readonly serverChecked: boolean;
  readonly usedOfflineFallback: boolean;
}

export interface OnlineQrResult {
  readonly qrText: string;
  readonly deviceFingerprint: string;
}

export interface OnlineClaimResult extends LicenseAuthState {
  readonly saved: boolean;
}

export interface OfflineStartResult {
  readonly qrText: string;
  readonly context: OfflineAuthContext;
}

export interface OfflineProofResult {
  readonly qrText: string;
  readonly context: OfflineAuthContext;
}

export interface LicenseStorageSnapshot {
  readonly productId: number;
  readonly deviceFingerprint: string;
  readonly deviceId: string;
  readonly licenseToken: string;
  readonly authMarker: string;
  readonly savedAt: string;
}

interface LicenseTokenPayload {
  readonly version?: number;
  readonly licenseId?: string;
  readonly deviceId?: string;
  readonly deviceFingerprint?: string;
  readonly softwareId?: string;
  readonly productId?: number;
  readonly status?: string;
  readonly issuedAt?: number;
  readonly expiresAt?: number;
}

interface DeviceLicenseBootstrapResponse {
  readonly isIssued?: boolean;
  readonly status?: string;
  readonly reason?: string;
  readonly licenseToken?: string;
  readonly deviceId?: string;
}

interface DeviceLicenseValidateResponse {
  readonly isValid?: boolean;
  readonly status?: string;
  readonly reason?: string;
  readonly deviceId?: string;
}

interface ClaimDeviceLicenseResponse {
  readonly licenseToken?: string;
  readonly deviceId?: string;
  readonly status?: string;
}

export interface OfflineAuthContext {
  readonly productId: number;
  readonly deviceId: string;
  readonly deviceFingerprint: string;
  readonly deviceName: string;
  readonly platform: string;
  readonly osVersion: string;
  readonly sessionId: string;
  readonly nonce: string;
  readonly authMode: 'NORMAL_OFFLINE';
  readonly keyVersion: number;
  readonly keyId: string;
  readonly issuedSlot: number;
  readonly issuedAt: Date;
  readonly proofNonce: string;
}

export class LicenseHubAuthService {
  constructor(
    private readonly context: LicenseAuthContext,
    private readonly storage: LicenseHubAuthStorage = new LicenseHubAuthStorage(),
  ) {}

  async validateStoredOrBootstrap(): Promise<LicenseAuthState> {
    const deviceFingerprint = await resolveDeviceFingerprint();
    const stored = await this.storage.read();
    if (isOfflineMarkerValidForDevice(stored, deviceFingerprint)) {
      return this.validateOfflineMarkerWithServer(deviceFingerprint, stored as LicenseStorageSnapshot);
    }

    const storedToken = stored?.deviceFingerprint.toLowerCase() === deviceFingerprint.toLowerCase()
      ? stored.licenseToken.trim()
      : '';
    const storedDeviceId = stored?.deviceFingerprint.toLowerCase() === deviceFingerprint.toLowerCase()
      ? stored.deviceId.trim()
      : '';

    if (!storedToken) {
      return this.bootstrapFromServer(deviceFingerprint);
    }

    const local = await validateLicenseToken(storedToken, deviceFingerprint);

    try {
      const server = await postJson<DeviceLicenseValidateResponse>('/api/device/license/validate', {
        productId: PRODUCT_ID,
        deviceFingerprint,
        licenseToken: storedToken,
        deviceId: storedDeviceId,
      });
      if (server.isValid) {
        const deviceId = (server.deviceId ?? storedDeviceId ?? local.payload.deviceId ?? '').trim();
        await this.saveLicense(deviceFingerprint, deviceId, storedToken, '');
        return validState('ONLINE', 'server-valid', deviceFingerprint, deviceId, storedToken, true, false);
      }

      await this.storage.clear();
      return invalidState(server.status ?? 'server-invalid', server.reason ?? '서버 라이선스 검증 실패', deviceFingerprint);
    } catch {
      if (local.isValid) {
        return validState('ONLINE', 'offline-fallback', deviceFingerprint, storedDeviceId, storedToken, false, true);
      }

      return invalidState('local-invalid', local.reason || '서버 검증 실패 및 로컬 토큰 검증 실패', deviceFingerprint);
    }
  }

  async buildOnlineQr(): Promise<OnlineQrResult> {
    const deviceFingerprint = await resolveDeviceFingerprint();
    const qrText = buildOnlineQrText({
      productId: PRODUCT_ID,
      deviceFingerprint,
      platform: 'tizen',
      osVersion: this.context.appVersion,
      deviceName: this.context.playerName,
      nonce: randomHex(8),
      issuedAt: new Date(),
    });
    return { qrText, deviceFingerprint };
  }

  async claimOnline(otp: string): Promise<OnlineClaimResult> {
    const deviceFingerprint = await resolveDeviceFingerprint();
    const normalizedOtp = normalizeOtp(otp);
    if (normalizedOtp.length !== 5) {
      return { ...invalidState('otp-empty', 'OTP 5자리를 입력해 주세요.', deviceFingerprint), saved: false };
    }

    const response = await postJson<ClaimDeviceLicenseResponse>('/api/device/activation/claim', {
      productId: PRODUCT_ID,
      deviceFingerprint,
      oneTimePassword: normalizedOtp,
    });
    const token = (response.licenseToken ?? '').trim();
    if (!token) {
      return { ...invalidState(response.status ?? 'claim-empty', '온라인 인증 응답에 라이선스 토큰이 없습니다.', deviceFingerprint), saved: false };
    }

    const local = await validateLicenseToken(token, deviceFingerprint);
    const deviceId = (response.deviceId ?? local.payload.deviceId ?? deviceFingerprint.slice(0, 32)).trim();
    await this.saveLicense(deviceFingerprint, deviceId, token, '');
    return { ...validState('ONLINE', 'online-verified', deviceFingerprint, deviceId, token, true, false), saved: true };
  }

  async buildOfflineStart(): Promise<OfflineStartResult> {
    const deviceFingerprint = await resolveDeviceFingerprint();
    const stored = await this.storage.read();
    const deviceId = normalizeDeviceId(stored?.deviceId || deviceFingerprint.slice(0, 32));
    const context = createOfflineContext(deviceId, deviceFingerprint, this.context.playerName, this.context.appVersion);
    return {
      context,
      qrText: buildOfflineStartQrText(context),
    };
  }

  async buildOfflineProof(context: OfflineAuthContext, otp: string): Promise<OfflineProofResult> {
    const normalizedOtp = normalizeOtp(otp);
    if (normalizedOtp.length !== 5) {
      throw new Error('OTP 5자리를 입력해 주세요.');
    }

    return {
      context,
      qrText: buildOfflineProofQrText({ ...context, proofNonce: `pn_${randomHex(8)}` }, normalizedOtp),
    };
  }

  async completeOffline(context: OfflineAuthContext): Promise<LicenseAuthState> {
    const serverState = await this.tryBootstrapIssuedLicense(context.deviceFingerprint, 'OFFLINE', 'offline-server-verified');
    if (serverState.serverReachable) {
      return serverState.state;
    }

    const marker = buildAuthMarker(context.deviceId);
    await this.saveLicense(context.deviceFingerprint, context.deviceId, '', marker);
    return validState('OFFLINE', 'offline-verified', context.deviceFingerprint, context.deviceId, '', false, true);
  }

  private async bootstrapFromServer(deviceFingerprint: string): Promise<LicenseAuthState> {
    try {
      const response = await postJson<DeviceLicenseBootstrapResponse>('/api/device/license/bootstrap', {
        productId: PRODUCT_ID,
        deviceFingerprint,
      });
      if (!response.isIssued || !response.licenseToken?.trim()) {
        return invalidState(response.status ?? 'bootstrap-empty', response.reason ?? '활성 라이선스 토큰을 발급받지 못했습니다.', deviceFingerprint);
      }

      const token = response.licenseToken.trim();
      const local = await validateLicenseToken(token, deviceFingerprint);
      const deviceId = (response.deviceId ?? local.payload.deviceId ?? deviceFingerprint.slice(0, 32)).trim();
      await this.saveLicense(deviceFingerprint, deviceId, token, '');
      return validState('ONLINE', 'bootstrap-valid', deviceFingerprint, deviceId, token, true, false);
    } catch (error) {
      return invalidState('bootstrap-failed', formatError(error), deviceFingerprint);
    }
  }

  private async saveLicense(deviceFingerprint: string, deviceId: string, licenseToken: string, authMarker: string): Promise<void> {
    await this.storage.write({
      productId: PRODUCT_ID,
      deviceFingerprint,
      deviceId,
      licenseToken,
      authMarker,
      savedAt: new Date().toISOString(),
    });
  }

  private async validateOfflineMarkerWithServer(deviceFingerprint: string, stored: LicenseStorageSnapshot): Promise<LicenseAuthState> {
    const serverState = await this.tryBootstrapIssuedLicense(deviceFingerprint, 'OFFLINE', 'offline-server-verified');
    if (serverState.serverReachable) {
      return serverState.state;
    }

    return {
      isValid: true,
      mode: 'OFFLINE',
      status: 'offline-marker',
      reason: '',
      deviceFingerprint,
      deviceId: stored.deviceId,
      licenseToken: stored.licenseToken,
      serverChecked: false,
      usedOfflineFallback: true,
    };
  }

  private async tryBootstrapIssuedLicense(
    deviceFingerprint: string,
    mode: LicenseAuthMode,
    successStatus: string,
  ): Promise<{ serverReachable: boolean; state: LicenseAuthState }> {
    try {
      const response = await postJson<DeviceLicenseBootstrapResponse>('/api/device/license/bootstrap', {
        productId: PRODUCT_ID,
        deviceFingerprint,
      });
      if (!response.isIssued || !response.licenseToken?.trim()) {
        await this.storage.clear();
        return {
          serverReachable: true,
          state: invalidState(response.status ?? 'bootstrap-empty', response.reason ?? '활성 라이선스 토큰을 발급받지 못했습니다.', deviceFingerprint),
        };
      }

      const token = response.licenseToken.trim();
      const local = await validateLicenseToken(token, deviceFingerprint);
      const deviceId = (response.deviceId ?? local.payload.deviceId ?? deviceFingerprint.slice(0, 32)).trim();
      await this.saveLicense(deviceFingerprint, deviceId, token, '');
      return {
        serverReachable: true,
        state: validState(mode, successStatus, deviceFingerprint, deviceId, token, true, false),
      };
    } catch {
      return {
        serverReachable: false,
        state: invalidState('bootstrap-unreachable', 'LicenseHub 서버에 연결할 수 없습니다.', deviceFingerprint),
      };
    }
  }
}

export class LicenseHubAuthStorage {
  async read(): Promise<LicenseStorageSnapshot | null> {
    const raw = await readSecureStorage();
    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as Partial<LicenseStorageSnapshot>;
      if (parsed.productId !== PRODUCT_ID || typeof parsed.deviceFingerprint !== 'string') {
        return null;
      }

      return {
        productId: PRODUCT_ID,
        deviceFingerprint: parsed.deviceFingerprint.trim(),
        deviceId: typeof parsed.deviceId === 'string' ? parsed.deviceId.trim() : '',
        licenseToken: typeof parsed.licenseToken === 'string' ? parsed.licenseToken.trim() : '',
        authMarker: typeof parsed.authMarker === 'string' ? parsed.authMarker.trim() : '',
        savedAt: typeof parsed.savedAt === 'string' ? parsed.savedAt : '',
      };
    } catch {
      await this.clear();
      return null;
    }
  }

  async write(snapshot: LicenseStorageSnapshot): Promise<void> {
    await writeSecureStorage(JSON.stringify(snapshot));
  }

  async clear(): Promise<void> {
    await removeSecureStorage();
  }
}

async function resolveDeviceFingerprint(): Promise<string> {
  const productInfo = window.webapis?.productinfo;
  const duid = productInfo?.getDuid?.().trim() ?? '';
  if (!duid) {
    throw new Error('Tizen DUID를 확인하지 못해 LicenseHub 인증을 진행할 수 없습니다.');
  }

  return sha256Hex(['NewHyOn', 'Tizen', duid].join('|'));
}

async function validateLicenseToken(token: string, expectedFingerprint: string): Promise<{ isValid: boolean; reason: string; payload: LicenseTokenPayload }> {
  const parts = token.trim().split('.');
  if (parts.length !== 2) {
    return { isValid: false, reason: '토큰 형식 불일치', payload: {} };
  }

  try {
    const payloadBytes = base64UrlToBytes(parts[0]);
    const signatureDer = base64UrlToBytes(parts[1]);
    const validSignature = await verifyEcdsaSignature(payloadBytes, signatureDer);
    if (!validSignature) {
      return { isValid: false, reason: '서명 검증 실패', payload: {} };
    }

    const payload = JSON.parse(new TextDecoder().decode(payloadBytes)) as LicenseTokenPayload;
    if (payload.status?.toLowerCase() !== 'active') {
      return { isValid: false, reason: '비활성 라이선스', payload };
    }
    if (payload.productId !== PRODUCT_ID) {
      return { isValid: false, reason: 'productId 불일치', payload };
    }
    if ((payload.deviceFingerprint ?? '').toLowerCase() !== expectedFingerprint.toLowerCase()) {
      return { isValid: false, reason: '디바이스 지문 불일치', payload };
    }
    if ((payload.expiresAt ?? 0) <= Math.floor(Date.now() / 1000)) {
      return { isValid: false, reason: '라이선스 만료', payload };
    }

    return { isValid: true, reason: '', payload };
  } catch (error) {
    return { isValid: false, reason: formatError(error), payload: {} };
  }
}

async function verifyEcdsaSignature(payloadBytes: Uint8Array, signatureDer: Uint8Array): Promise<boolean> {
  const signature = derEcdsaToRaw(signatureDer);
  const publicKey = await crypto.subtle.importKey(
    'spki',
    toArrayBuffer(pemToBytes(PUBLIC_KEY_PEM)),
    { name: 'ECDSA', namedCurve: 'P-256' },
    false,
    ['verify'],
  );
  return crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, publicKey, toArrayBuffer(signature), toArrayBuffer(payloadBytes));
}

function derEcdsaToRaw(signature: Uint8Array): Uint8Array {
  if (signature[0] !== 0x30) {
    throw new Error('ECDSA DER 서명 형식이 올바르지 않습니다.');
  }
  let offset = 2;
  if (signature[1] & 0x80) {
    offset = 2 + (signature[1] & 0x7f);
  }
  const r = readDerInteger(signature, offset);
  const s = readDerInteger(signature, r.nextOffset);
  return new Uint8Array([...leftPad32(r.value), ...leftPad32(s.value)]);
}

function readDerInteger(signature: Uint8Array, offset: number): { value: Uint8Array; nextOffset: number } {
  if (signature[offset] !== 0x02) {
    throw new Error('ECDSA DER integer 형식이 올바르지 않습니다.');
  }
  const length = signature[offset + 1];
  const start = offset + 2;
  let value = signature.slice(start, start + length);
  while (value.length > 32 && value[0] === 0) {
    value = value.slice(1);
  }
  return { value, nextOffset: start + length };
}

function leftPad32(value: Uint8Array): Uint8Array {
  if (value.length > 32) {
    throw new Error('ECDSA integer 길이가 올바르지 않습니다.');
  }
  const out = new Uint8Array(32);
  out.set(value, 32 - value.length);
  return out;
}

function buildOnlineQrText(context: {
  readonly productId: number;
  readonly deviceFingerprint: string;
  readonly platform: string;
  readonly osVersion: string;
  readonly deviceName: string;
  readonly nonce: string;
  readonly issuedAt: Date;
}): string {
  return `LICENSEHUB-DEVICE:${JSON.stringify({
    Version: 1,
    ProductId: context.productId,
    DeviceFingerprint: context.deviceFingerprint,
    Platform: context.platform,
    OsVersion: context.osVersion,
    DeviceName: context.deviceName,
    Nonce: context.nonce,
    IssuedAt: context.issuedAt.toISOString(),
  })}`;
}

function createOfflineContext(deviceId: string, deviceFingerprint: string, deviceName: string, osVersion: string): OfflineAuthContext {
  return {
    productId: PRODUCT_ID,
    deviceId: normalizeDeviceId(deviceId),
    deviceFingerprint: normalizeHex(deviceFingerprint, 32),
    deviceName,
    platform: 'tizen',
    osVersion,
    sessionId: `sess_${randomHex(16)}`,
    nonce: `n_${randomHex(8)}`,
    authMode: 'NORMAL_OFFLINE',
    keyVersion: 1,
    keyId: 'off_k_v1',
    issuedSlot: Math.floor(Date.now() / 1000 / OFFLINE_SLOT_SECONDS),
    issuedAt: new Date(),
    proofNonce: `pn_${randomHex(8)}`,
  };
}

function buildOfflineStartQrText(context: OfflineAuthContext): string {
  return `LH3-DEVICE:${JSON.stringify({
    Version: 3,
    Type: 'DEVICE_START',
    DeviceId: context.deviceId,
    DeviceFingerprint: context.deviceFingerprint,
    Platform: context.platform,
    OsVersion: context.osVersion,
    ProductId: context.productId,
    SessionId: context.sessionId,
    Nonce: context.nonce,
    AuthMode: context.authMode,
    KeyId: context.keyId,
    IssuedSlot: context.issuedSlot,
    IssuedAt: context.issuedAt.toISOString(),
  })}`;
}

function buildOfflineProofQrText(context: OfflineAuthContext, otp: string): string {
  return `LH3-PROOF:${JSON.stringify({
    Version: 3,
    Type: 'DEVICE_PROOF',
    DeviceId: context.deviceId,
    SessionId: context.sessionId,
    AuthMode: context.authMode,
    KeyId: context.keyId,
    ProofNonce: context.proofNonce,
    Otp: otp,
  })}`;
}

function buildAuthMarker(deviceId: string): string {
  return JSON.stringify({
    AuthProvider: 'LicenseHub',
    AuthSchema: 'ValidationResult',
    AuthVersion: 2,
    ProductId: PRODUCT_ID,
    IsValid: true,
    DeviceId: deviceId.trim(),
  });
}

export function buildLicenseHubAuthMarker(deviceId: string): string {
  return buildAuthMarker(deviceId);
}

function isOfflineMarkerValidForDevice(stored: LicenseStorageSnapshot | null, deviceFingerprint: string): boolean {
  if (!stored?.authMarker || stored.deviceFingerprint.toLowerCase() !== deviceFingerprint.toLowerCase()) {
    return false;
  }

  try {
    const marker = JSON.parse(stored.authMarker) as {
      AuthProvider?: string;
      AuthSchema?: string;
      AuthVersion?: number;
      ProductId?: number;
      IsValid?: boolean;
      DeviceId?: string;
    };
    return marker.AuthProvider?.toLowerCase() === 'licensehub'
      && marker.AuthSchema?.toLowerCase() === 'validationresult'
      && (marker.AuthVersion ?? 0) >= 2
      && marker.ProductId === PRODUCT_ID
      && marker.IsValid === true
      && Boolean(marker.DeviceId?.trim());
  } catch {
    return false;
  }
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    cache: 'no-store',
  });
  const raw = await response.text();
  if (!response.ok) {
    throw new Error(readApiError(raw, `LicenseHub 요청 실패: ${response.status}`));
  }
  return raw ? JSON.parse(raw) as T : {} as T;
}

async function readSecureStorage(): Promise<string> {
  const widgetdata = window.webapis?.widgetdata;
  if (!widgetdata?.read) {
    return window.localStorage.getItem(STORAGE_KEY) ?? '';
  }
  return new Promise((resolve) => {
    widgetdata.read((data) => resolve(data || ''), () => resolve(''));
  });
}

async function writeSecureStorage(data: string): Promise<void> {
  const widgetdata = window.webapis?.widgetdata;
  if (!widgetdata?.write) {
    window.localStorage.setItem(STORAGE_KEY, data);
    return;
  }
  await new Promise<void>((resolve, reject) => {
    widgetdata.write(data, resolve, reject);
  });
}

async function removeSecureStorage(): Promise<void> {
  const widgetdata = window.webapis?.widgetdata;
  if (!widgetdata?.remove) {
    window.localStorage.removeItem(STORAGE_KEY);
    return;
  }
  await new Promise<void>((resolve, reject) => {
    widgetdata.remove(resolve, reject);
  });
}

function validState(
  mode: LicenseAuthMode,
  status: string,
  deviceFingerprint: string,
  deviceId: string,
  licenseToken: string,
  serverChecked: boolean,
  usedOfflineFallback: boolean,
): LicenseAuthState {
  return {
    isValid: true,
    mode,
    status,
    reason: '',
    deviceFingerprint,
    deviceId,
    licenseToken,
    serverChecked,
    usedOfflineFallback,
  };
}

function invalidState(status: string, reason: string, deviceFingerprint: string): LicenseAuthState {
  return {
    isValid: false,
    mode: 'NONE',
    status,
    reason,
    deviceFingerprint,
    deviceId: '',
    licenseToken: '',
    serverChecked: false,
    usedOfflineFallback: false,
  };
}

function normalizeOtp(value: string): string {
  return value.trim().toUpperCase().replace(/[^23456789ABCDEFGHJKLMNPQRSTUVWXYZ]/g, '').slice(0, 5);
}

function normalizeDeviceId(value: string): string {
  return normalizeHex(value, 16);
}

function normalizeHex(value: string, bytes: number): string {
  const normalized = value.trim().replace(/^(sess_|n_|pn_)/i, '');
  if (!new RegExp(`^[0-9a-fA-F]{${bytes * 2}}$`).test(normalized)) {
    throw new Error('LicenseHub hex 토큰 형식이 올바르지 않습니다.');
  }
  return normalized.toLowerCase();
}

function stripPrefix(value: string, prefix: string): string {
  return value.toLowerCase().startsWith(prefix) ? value.slice(prefix.length) : value;
}

async function sha256Hex(value: string): Promise<string> {
  return bytesToHex(sha256(new TextEncoder().encode(value))).toUpperCase();
}

function randomHex(bytes: number): string {
  const values = new Uint8Array(bytes);
  crypto.getRandomValues(values);
  return bytesToHex(values);
}

function bytesToHex(bytes: Uint8Array): string {
  return Array.from(bytes).map((byte) => byte.toString(16).padStart(2, '0')).join('');
}

function hexToBase64Url(hex: string): string {
  const normalized = hex.trim().replace(/^(sess_|n_|pn_)/i, '');
  const bytes = new Uint8Array(normalized.match(/.{1,2}/g)?.map((part) => Number.parseInt(part, 16)) ?? []);
  return bytesToBase64Url(bytes);
}

function bytesToBase64Url(bytes: Uint8Array): string {
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function base64UrlToBytes(value: string): Uint8Array {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(value.length / 4) * 4, '=');
  const binary = atob(normalized);
  return Uint8Array.from(binary, (char) => char.charCodeAt(0));
}

function pemToBytes(pem: string): Uint8Array {
  const base64 = pem
    .replace(/-----BEGIN PUBLIC KEY-----/g, '')
    .replace(/-----END PUBLIC KEY-----/g, '')
    .replace(/\s/g, '');
  return base64UrlToBytes(base64.replace(/\+/g, '-').replace(/\//g, '_'));
}

function toArrayBuffer(bytes: Uint8Array): ArrayBuffer {
  const out = new ArrayBuffer(bytes.byteLength);
  new Uint8Array(out).set(bytes);
  return out;
}

function readApiError(raw: string, fallback: string): string {
  if (!raw.trim()) {
    return fallback;
  }
  try {
    const parsed = JSON.parse(raw) as { message?: string; error?: string };
    return parsed.message?.trim() || parsed.error?.trim() || fallback;
  } catch {
    return raw.trim();
  }
}

function formatError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

export const LICENSEHUB_PRODUCT_ID = PRODUCT_ID;
export const LICENSEHUB_OFFLINE_CODE_CHARSET = OFFLINE_CODE_CHARSET;

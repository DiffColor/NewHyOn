# Multi-User + External Services + LiteDB Local Settings Plan

## Goals
- Allow multiple AndoW_Manager instances to run concurrently.
- Move local-only settings to LiteDB (local.db, password: turtle04!9).
- Connect to external RethinkDB and external SignalR endpoints.
- Broadcast player SignalR messages to all Managers.
- Remove any local service startup (RethinkDB + SignalR self-host).

## Current State (code references)
- SignalR server moved to external .NET 4.7.2 host.
  - SignalR_Net472/SignalR_Net472/SignalRServerHost.cs
  - SignalR_Net472/SignalR_Net472/Program.cs
- Manager acts as SignalR client.
  - AndoW_Manager/TurtleTools/SignalRClientTools.cs
  - AndoW_Manager/MainWindow.xaml.cs (StartSignalRClient/StopSignalRClient)
- RethinkDB is external; Manager waits for connectivity and retries every 15s.
  - AndoW_Manager/MainWindow.xaml.cs (WaitForDatabaseReadyAsync)
- app.config values are used only for first-run seeding.
  - AndoW_Manager/app.config
  - AndoW_Settings/app.config
- Settings UI now reads/writes local.db values.
  - AndoW_Settings/Form1.cs
  - AndoW_Manager/DataManager/ServerSettingsManager.cs

## New Local Storage (LiteDB)
- File: local.db
- Password: turtle04!9
- Connection string: Filename=...\local.db;Password=turtle04!9;Connection=Shared;
- Location decision (confirmed):
  - <AppBaseDir>\local.db

### Collections (suggested)
- local_connection (singleton)
  - id: "singleton"
  - rethink_host
  - rethink_port
  - rethink_database
  - rethink_user
  - rethink_password
  - signalr_base_url (or signalr_url)
  - signalr_hub_path (if separate)
- local_ftp (singleton)
  - id: "singleton"
  - ftp_port
  - ftp_pasv_min
  - ftp_pasv_max
- local_ui (singleton) [confirmed: UI defaults stored locally]
  - id: "singleton"
  - preserve_aspect_ratio
  - default_welcome_font_family
  - default_welcome_font_size
  - default_welcome_font_color
  - default_welcome_background_color
  - default_welcome_font_color_index
  - default_welcome_background_color_index
  - default_resolution_orientation
  - default_resolution_rows
  - default_resolution_columns
  - default_resolution_width_pixels
  - default_resolution_height_pixels

## SignalR Externalization (Manager becomes client)
- Create or reuse an external SignalR service (not in Manager).
- Managers connect as SignalR clients to the external service.
- When a player message arrives at the hub, broadcast to all Managers.
- Managers handle incoming messages and update UI (heartbeats, status, etc.).
- Shared message models must be aligned between server and Manager.

## RethinkDB Externalization
- Managers/Settings connect to external RethinkDB using LiteDB-stored settings.
- Remove local bootstrapper usage (no process/firewall management).
- Add connection validation and clear error handling when DB is unavailable.
- If DB connection fails: show user guidance immediately, do NOT fallback, retry on a fixed 15s interval.

## Connection Failure Policy (DB + SignalR)
- No fallback modes that mask connectivity problems.
- On failure: show guidance/status to the user and keep the app in a waiting state.
- Retry both DB and SignalR connections every 15 seconds (fixed interval, no backoff).

## Migration Strategy
- On first run (local.db not found):
  1) Read app.config values as defaults.
  2) Create local.db and save local_connection.
  3) If RethinkDB is reachable, load existing ServerSettings and save to local_ftp/local_ui.
  4) If DB is not reachable, use defaults for local_ftp/local_ui.

## Required Code Changes (high level)
- Add LiteDB dependency (Manager + Settings projects).
- Add LocalSettingsStore (read/write local.db).
- Update RethinkDbConfigurator to read from LocalSettingsStore instead of app.config.
- Update Settings UI to edit LocalSettingsStore values.
- Update Manager startup to use external SignalR client (no self-host).
- Remove calls to RethinkDbBootstrapper and SignalRServerTools start/stop.
- Update ServerSettings usage if UI defaults move to local db.

## Open Decisions
- Confirm external SignalR server ownership (new service vs existing HyonMessageServer).

## Checklist
- [x] Decide local.db location and finalize path helper.
- [x] Add LiteDB package to AndoW_Manager and AndoW_Settings.
- [x] Implement LocalSettingsStore (open/close, CRUD, singleton document).
- [x] Define local_connection/local_ftp/local_ui schema and seed singleton documents.
- [x] Add first-run migration from app.config to local.db.
- [x] If RethinkDB is reachable, migrate ServerSettings to local_ftp/local_ui.
- [x] Update RethinkDbConfigurator (Manager + Settings) to read local.db.
- [x] Update Settings UI to view/edit local.db values.
- [x] Remove FTP/UI defaults dependency on RethinkDB.
- [x] Decide and migrate UI default settings if needed.
- [x] Add external SignalR host project (SignalR_Net472).
- [x] Add external SignalR client connection for Manager.
- [x] Broadcast player heartbeats to all Managers (Managers group + ReceiveHeartbeat).
- [x] Broadcast non-heartbeat player messages to all Managers (if needed). (현재 플레이어 비하트비트 송신 없음 확인)
- [x] Verify shared message model compatibility (server <-> Manager).
- [x] Remove SignalR self-host startup/stop in Manager.
- [x] Remove SignalR server tools from Manager (no in-process hub).
- [x] Add SignalR host/port/hubPath config for Manager client.
- [x] Add SignalR server resilience (unhandled exception logging + restart loop).
- [x] Update Player to connect to external SignalR host (not Manager).
- [x] Move FTP server out of Manager (no local StartFTPSrv/StopFTPSrv).
- [x] Add external FTP connection settings and validation (host/port/passive range).
- [x] Remove RethinkDB bootstrapper startup in Manager/Settings.
- [x] Add connection failure UX: guidance + waiting state (no fallback).
- [x] Implement fixed 15s retry for DB + SignalR.
- [x] 매니저/플레이어 단일 인스턴스 뮤텍스 유지 (멀티유저=다중 네트워크 접속).
- [ ] Smoke test: Settings UI loads and saves without RethinkDB.
- [ ] Smoke test: Manager connects to external DB/SignalR and handles messages.
- [x] Decide logging scope for connection failures (what/where to log).


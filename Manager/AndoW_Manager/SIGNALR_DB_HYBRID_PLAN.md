# SignalR + DB Hybrid Plan (Manager Not Always Running)

## Goals
- Keep commands and playlist updates reliable when Manager is closed.
- Avoid a separate always-on SignalR service.
- Use DB as the source of truth for offline delivery.
- Use SignalR only for immediate push when Manager is running.
- Enforce concurrent FTP download limits across players.
- Never cap retry attempts for lease acquisition when failures are due to outages.

## Constraints
- Manager is not always running.
- SignalR is not separated into its own service.
- DB is assumed to be available even when Manager is closed.
- FTP bandwidth is limited (need global concurrency control).

## Current Behavior Summary
- Manager writes a single pending command into PlayerInfoManager.command.
- Player polls DB every ~5s and executes the command, then clears it.
- Playlist updates use RethinkDB for PageList/Page data + FTP downloads.
- SignalR is currently used for heartbeat and optional command handling only.
- Update progress/history are stored in UpdateQueue/CommandHistory tables.

## Target Architecture (Hybrid)
- DB is the durable queue for commands and update triggers.
- SignalR is used only to push commands immediately when Manager is running.
- Player always falls back to DB polling for pending commands.
- Playlist data stays in DB for now (no SignalR payload transfer).
- Download concurrency is enforced via DB leases (global throttle).

## Data Model Changes (DB)

### 1) CommandQueue (new table)
Purpose: store command queue per player (durable, multi-command).

Suggested fields:
- id (string, unique, GUID)
- playerId (string, Player GUID)
- command (string, e.g. updatelist, reboot)
- payloadJson (string, optional)
- status (string: pending, sent, ack, failed, superseded, expired)
- createdAt (string local time)
- updatedAt (string local time)
- expiresAt (string local time, optional)
- attemptCount (int)
- lastAttemptAt (string local time)
- source (string: manager, signalr)
- replacedBy (string, optional)

### 2) UpdateThrottle (singleton doc) OR UpdateLease (table)
Purpose: global concurrency limit for FTP downloads.

Option A (single document):
- id = "global"
- maxConcurrent (int)
- leases (array of lease objects)

Lease object:
- leaseId (string, GUID)
- playerId (string)
- queueId (string)
- leaseExpiresAt (string local time)
- lastRenewAt (string local time)

Option B (table per lease):
- id (leaseId)
- playerId, queueId
- leaseExpiresAt, lastRenewAt

## Command Flow (Hybrid)
1) Manager creates a CommandQueue entry per target player (status=pending).
2) Manager removes or supersedes older pending commands for the same player
   (prevents stale commands from stacking).
3) If Manager is running, it pushes the command via SignalR with commandId.
4) Player executes:
   - If from SignalR: run immediately, then write ACK to DB.
   - If offline or missed: DB poll picks it up and runs it later.
5) Player writes command status (ack/failed) back to DB.

## Playlist Update Flow
- Keep existing DB-based playlist/page fetch in UpdateService.
- Command trigger is "updatelist" in CommandQueue.
- DB remains the source for playlist data (no SignalR payload).

## Offline Behavior
- Offline players: commands remain pending in DB until the player returns.
- Manager closed: DB is still authoritative; players continue polling.
- When Manager reopens, it can push pending commands again if desired.

## FTP Download Concurrency (Global Throttle)

### Lease Acquire
- Player requests a lease before starting any download.
- Atomic DB update:
  - Remove expired leases.
  - If active leases < maxConcurrent, insert a new lease.
  - If not, retry later (no retry cap).

### Lease Renew
- Player renews lease periodically (e.g. every 10s).
- If renew fails or lease expires, player must pause/stop and re-acquire.

### Lease Release
- On completion/failure, player deletes the lease record.

## Failure Handling
- Manager offline: no SignalR push; DB poll continues.
- Player crash: lease expires; another player can acquire.
- Network loss: lease expires; retry continues without limit.
- Stale commands: supersede on new command insert; optional expiresAt cleanup.

## Implementation Checklist (Detailed)

### A) DB Schema
- [ ] Create CommandQueue table with indexes on playerId, status, createdAt.
- [ ] Create UpdateThrottle (or UpdateLease) table/document.
- [ ] Decide local time format (consistent with existing logs).
- [ ] Add cleanup policy for expired commands (optional).

### B) Manager: Command Write Path
- [ ] Replace PlayerInfoManager.command writes with CommandQueue inserts.
- [ ] Supersede/delete older pending commands for the same player.
- [ ] Add payloadJson support for commands that need metadata.
- [ ] Log commandId, playerId, command on creation.

### C) Manager: SignalR Push (Best Effort)
- [ ] When Manager is running, push new commandId via SignalR.
- [ ] If push fails, keep DB pending status unchanged.
- [ ] Optional: update status to "sent" when push succeeds.

### D) Player: Command Polling
- [ ] Poll CommandQueue for pending commands for this playerId.
- [ ] Ensure idempotency (skip commands already acked).
- [ ] Update status to "ack" or "failed" after execution.
- [ ] Support "superseded" commands (do not run).

### E) Player: SignalR Command Handling
- [ ] Map SignalR message to commandId + command payload.
- [ ] Execute immediately if not already acked.
- [ ] Write ACK to DB after execution.

### F) UpdateService Throttle Integration
- [ ] Before download: acquire lease from UpdateThrottle.
- [ ] While downloading: renew lease at fixed interval.
- [ ] If lease lost: stop or pause downloads; retry acquire.
- [ ] After completion or failure: release lease.
- [ ] Never cap retry count (keep trying until command expires or superseded).

### G) Queue/History Sync
- [ ] Continue using UpdateQueue/CommandHistory for status visibility.
- [ ] On ACK, update CommandHistory with done/failed.

### H) UI / Ops
- [ ] Show pending/sent/ack/failed per player in Manager UI.
- [ ] Expose maxConcurrent setting (config or DB setting).
- [ ] Add basic admin cleanup (expired commands).

### I) Failure and Recovery Tests
- [ ] Manager closed: player executes pending command via DB poll.
- [ ] Player offline: command stays pending and runs on reconnect.
- [ ] Mass update: only maxConcurrent downloads at once.
- [ ] Lease expiration: stalled player releases slot after TTL.
- [ ] Duplicate commands: new command supersedes old pending.
- [ ] SignalR push failure: DB poll still works.


export interface PairSchedulerRect {
    x: number;
    y: number;
    width: number;
    height: number;
}

export interface PairSchedulerItem {
    id: string;
    title: string;
    url: string;
}

export interface PairSchedulerPairConfig<TItem extends PairSchedulerItem> {
    id: string;
    label: string;
    items: TItem[];
}

export interface PairSchedulerConfig<TItem extends PairSchedulerItem> {
    swapsBeforePairHandoff: number;
    transitionIntervalMs?: number;
    displayRect: PairSchedulerRect;
    lowerRect: PairSchedulerRect;
    displayMethod: string;
    prebufferProperty: string;
    prebufferStartPosition: string;
    bufferingTimeoutSeconds: number;
    pairs: Array<PairSchedulerPairConfig<TItem>>;
}

export type PairSchedulerLayerRole = 'hidden' | 'warming' | 'held' | 'current';
export type PairSchedulerSessionState = 'idle' | 'preparing' | 'ready' | 'playing' | 'held' | 'stopped';

export interface PairSchedulerPlayer {
    open(url: string): void;
    close(): void;
    play(): void;
    stop(): void;
    prepareAsync(onSuccess: () => void, onError: (error: unknown) => void): void;
    setDisplayRect(x: number, y: number, width: number, height: number): void;
    setListener(listener: PairSchedulerPlayerListener): void;
    getState?: () => string;
    setDisplayMethod?: (method: string) => void;
    setTimeoutForBuffering?: (seconds: number) => void;
    setStreamingProperty?: (property: string, value: string) => void;
    setVideoStillMode?: (enabled: string) => void;
    revealLayer?: () => void;
}

export interface PairSchedulerPlayerListener {
    onbufferingstart(): void;
    onbufferingprogress(percent: number): void;
    onbufferingcomplete(): void;
    onstreamcompleted(): void;
    oncurrentplaytime(currentTime: number): void;
    onerror(eventType: string): void;
    onerrormsg(error: string, message: string): void;
    onevent(eventType: string, eventData: string): void;
    ondrmevent(drmEvent: string, drmData: string): void;
}

export interface PairSchedulerSessionSnapshot<TItem extends PairSchedulerItem> {
    pairId: string;
    slot: number;
    state: PairSchedulerSessionState;
    item: TItem | null;
}

export interface PairSchedulerPairSnapshot<TItem extends PairSchedulerItem> {
    id: string;
    label: string;
    activeSlot: number;
    prepareSlot: number;
    swapsInTurn: number;
    handoffReady: boolean;
    sessions: Array<PairSchedulerSessionSnapshot<TItem>>;
}

export interface PairSchedulerSnapshot<TItem extends PairSchedulerItem> {
    running: boolean;
    transitioning: boolean;
    activePairId: string | null;
    preparePairId: string | null;
    swapsBeforePairHandoff: number;
    transitionIntervalMs: number;
    pairs: Array<PairSchedulerPairSnapshot<TItem>>;
}

export interface PairSchedulerOptions<TItem extends PairSchedulerItem> {
    config: PairSchedulerConfig<TItem>;
    createPlayer(): PairSchedulerPlayer;
    resolveUrl(url: string): string;
    log(message: string): void;
    onLayerRoleChange(pairId: string, slot: number, role: PairSchedulerLayerRole): void;
    onStateChange(snapshot: PairSchedulerSnapshot<TItem>): void;
}

interface PairSchedulerSession<TItem extends PairSchedulerItem> {
    pairId: string;
    slot: number;
    player: PairSchedulerPlayer;
    item: TItem | null;
    state: PairSchedulerSessionState;
    firstFrameHandler: ((currentTime: number) => void) | null;
}

interface PairSchedulerPair<TItem extends PairSchedulerItem> {
    id: string;
    label: string;
    items: TItem[];
    cursor: number;
    activeSlot: number;
    prepareSlot: number;
    swapsInTurn: number;
    handoffReady: boolean;
    sessions: Array<PairSchedulerSession<TItem>>;
}

const DEFAULT_TRANSITION_INTERVAL_MS = 10000;

export class PairScheduler<TItem extends PairSchedulerItem> {
    private readonly options: PairSchedulerOptions<TItem>;
    private readonly config: PairSchedulerConfig<TItem>;
    private readonly transitionIntervalMs: number;
    private readonly pairs: Array<PairSchedulerPair<TItem>> = [];
    private activePairIndex = 0;
    private preparePairIndex = 1;
    private running = false;
    private transitioning = false;
    private transitionTimerId: number | null = null;
    private runToken = 0;

    constructor(options: PairSchedulerOptions<TItem>) {
        this.options = options;
        this.config = options.config;
        this.transitionIntervalMs = options.config.transitionIntervalMs || DEFAULT_TRANSITION_INTERVAL_MS;
    }

    init(): void {
        this.assertConfig();

        for (let i = 0; i < this.config.pairs.length; i += 1) {
            this.pairs.push(this.createPair(this.config.pairs[i]));
        }

        this.emitState();
        this.options.log('initialized with ' + this.pairs.length + ' AVPlay pairs');
    }

    start(): void {
        if (this.running) {
            this.options.log('already running');
            return;
        }

        this.options.log('start scheduler');
        this.resetRuntime();
        this.running = true;

        const token = this.runToken;
        const activePair = this.pairs[this.activePairIndex];

        this.prepareActivePairStart(activePair)
            .then(() => {
                if (!this.isRunCurrent(token)) {
                    return null;
                }
                this.playSession(activePair.sessions[activePair.activeSlot]);
                return this.prepareNextInActivePair(activePair);
            })
            .then(() => {
                if (!this.isRunCurrent(token)) {
                    return;
                }
                this.scheduleNextTransition('startup-ready');
            })
            .catch((error: Error) => {
                this.failRun('start failed: ' + error.message);
            });
    }

    stop(): void {
        if (!this.running) {
            this.options.log('stop requested while idle');
        } else {
            this.options.log('stop scheduler');
        }

        this.running = false;
        this.transitioning = false;
        this.runToken += 1;
        this.clearTransitionTimer();

        for (let i = 0; i < this.pairs.length; i += 1) {
            for (let j = 0; j < this.pairs[i].sessions.length; j += 1) {
                this.releaseSession(this.pairs[i].sessions[j], 'manual-stop');
            }
            this.pairs[i].handoffReady = false;
        }

        this.emitState();
    }

    prepareHandoffPair(): void {
        const pair = this.pairs[this.preparePairIndex];
        if (!pair) {
            this.failRun('prepare handoff pair failed: prepare pair is not initialized');
            return;
        }

        this.preparePairForHandoff(pair)
            .catch((error: Error) => {
                this.failRun('prepare handoff pair failed: ' + error.message);
            });
    }

    getSnapshot(): PairSchedulerSnapshot<TItem> {
        const activePair = this.pairs[this.activePairIndex] || null;
        const preparePair = this.pairs[this.preparePairIndex] || null;

        return {
            running: this.running,
            transitioning: this.transitioning,
            activePairId: activePair ? activePair.id : null,
            preparePairId: preparePair ? preparePair.id : null,
            swapsBeforePairHandoff: this.config.swapsBeforePairHandoff,
            transitionIntervalMs: this.transitionIntervalMs,
            pairs: this.pairs.map((pair) => ({
                id: pair.id,
                label: pair.label,
                activeSlot: pair.activeSlot,
                prepareSlot: pair.prepareSlot,
                swapsInTurn: pair.swapsInTurn,
                handoffReady: pair.handoffReady,
                sessions: pair.sessions.map((session) => ({
                    pairId: session.pairId,
                    slot: session.slot,
                    state: session.state,
                    item: session.item
                }))
            }))
        };
    }

    private assertConfig(): void {
        if (this.config.pairs.length !== 2) {
            throw new Error('PairScheduler requires exactly two pairs.');
        }

        if (this.config.swapsBeforePairHandoff < 1) {
            throw new Error('swapsBeforePairHandoff must be greater than zero.');
        }

        for (let i = 0; i < this.config.pairs.length; i += 1) {
            if (this.config.pairs[i].items.length < 2) {
                throw new Error('Pair ' + this.config.pairs[i].id + ' requires at least two items.');
            }
        }
    }

    private createPair(pairConfig: PairSchedulerPairConfig<TItem>): PairSchedulerPair<TItem> {
        const pair: PairSchedulerPair<TItem> = {
            id: pairConfig.id,
            label: pairConfig.label,
            items: pairConfig.items,
            cursor: 0,
            activeSlot: 0,
            prepareSlot: 1,
            swapsInTurn: 0,
            handoffReady: false,
            sessions: []
        };

        pair.sessions.push(this.createSession(pair, 0));
        pair.sessions.push(this.createSession(pair, 1));
        return pair;
    }

    private createSession(pair: PairSchedulerPair<TItem>, slot: number): PairSchedulerSession<TItem> {
        const session: PairSchedulerSession<TItem> = {
            pairId: pair.id,
            slot,
            player: this.options.createPlayer(),
            item: null,
            state: 'idle',
            firstFrameHandler: null
        };

        session.player.setListener(this.createListener(pair.id, slot));
        this.options.onLayerRoleChange(session.pairId, session.slot, 'hidden');
        return session;
    }

    private createListener(pairId: string, slot: number): PairSchedulerPlayerListener {
        return {
            onbufferingstart: () => {
                this.options.log(pairId + slot + ' buffering start');
            },
            onbufferingprogress: (percent: number) => {
                if (percent === 100) {
                    this.options.log(pairId + slot + ' buffering 100%');
                }
            },
            onbufferingcomplete: () => {
                this.options.log(pairId + slot + ' buffering complete');
            },
            onstreamcompleted: () => {
                this.onStreamCompleted(pairId, slot);
            },
            oncurrentplaytime: (currentTime: number) => {
                this.onCurrentPlaytime(pairId, slot, currentTime);
            },
            onerror: (eventType: string) => {
                this.options.log(pairId + slot + ' error: ' + eventType);
            },
            onerrormsg: (_error: string, message: string) => {
                this.options.log(pairId + slot + ' error message: ' + message);
            },
            onevent: (eventType: string, eventData: string) => {
                this.options.log(pairId + slot + ' event: ' + eventType + ' ' + eventData);
            },
            ondrmevent: (drmEvent: string, drmData: string) => {
                this.options.log(pairId + slot + ' drm: ' + drmEvent + ' ' + drmData);
            }
        };
    }

    private resetRuntime(): void {
        this.activePairIndex = 0;
        this.preparePairIndex = 1;
        this.transitioning = false;
        this.runToken += 1;
        this.clearTransitionTimer();

        for (let i = 0; i < this.pairs.length; i += 1) {
            this.pairs[i].cursor = 0;
            this.pairs[i].activeSlot = 0;
            this.pairs[i].prepareSlot = 1;
            this.pairs[i].swapsInTurn = 0;
            this.pairs[i].handoffReady = false;

            for (let j = 0; j < this.pairs[i].sessions.length; j += 1) {
                this.releaseSession(this.pairs[i].sessions[j], 'reset');
            }
        }
    }

    private prepareActivePairStart(pair: PairSchedulerPair<TItem>): Promise<void> {
        const session = pair.sessions[pair.activeSlot];
        const item = this.nextItem(pair);

        return this.prepareSession(session, item, this.config.displayRect).then(() => {
            this.options.log(pair.label + ' active loaded: ' + item.id);
            this.emitState();
        });
    }

    private prepareNextInActivePair(pair: PairSchedulerPair<TItem>): Promise<void> {
        const session = pair.sessions[pair.prepareSlot];
        const item = this.nextItem(pair);

        return this.prepareSession(session, item, this.config.lowerRect).then(() => {
            this.options.log(pair.label + ' hidden prepared: ' + session.pairId + session.slot + ' ' + item.id);
            this.emitState();
        });
    }

    private preparePairForHandoff(pair: PairSchedulerPair<TItem>): Promise<void> {
        if (pair.handoffReady) {
            return Promise.resolve();
        }

        pair.activeSlot = 0;
        pair.prepareSlot = 1;
        pair.swapsInTurn = 0;

        const activeSession = pair.sessions[pair.activeSlot];
        const prepareSession = pair.sessions[pair.prepareSlot];
        const activeItem = this.nextItem(pair);
        const prepareItem = this.nextItem(pair);

        return Promise.all([
            this.prepareSession(activeSession, activeItem, this.config.lowerRect),
            this.prepareSession(prepareSession, prepareItem, this.config.lowerRect)
        ]).then(() => {
            pair.handoffReady = true;
            this.options.log(pair.label + ' handoff prepared: ' + activeItem.id + ', ' + prepareItem.id);
            this.emitState();
        });
    }

    private nextItem(pair: PairSchedulerPair<TItem>): TItem {
        const item = pair.items[pair.cursor % pair.items.length];
        pair.cursor += 1;
        return item;
    }

    private prepareSession(session: PairSchedulerSession<TItem>, item: TItem, rect: PairSchedulerRect): Promise<void> {
        const contentUrl = this.options.resolveUrl(item.url);

        this.releaseSession(session, 'prepare');
        session.item = item;
        session.state = 'preparing';
        session.player.open(contentUrl);
        session.player.setListener(this.createListener(session.pairId, session.slot));
        this.applyDisplayRect(session, rect);
        this.callOptional(session.player, 'setDisplayMethod', [this.config.displayMethod]);
        this.callOptional(session.player, 'setTimeoutForBuffering', [this.config.bufferingTimeoutSeconds]);
        this.callOptional(session.player, 'setStreamingProperty', [this.config.prebufferProperty, this.config.prebufferStartPosition]);
        this.options.log(session.pairId + session.slot + ' prepareAsync start: ' + contentUrl);
        this.emitState();

        return new Promise((resolve, reject) => {
            session.player.prepareAsync(
                () => {
                    session.state = 'ready';
                    this.options.log(session.pairId + session.slot + ' prepareAsync ready: ' + contentUrl);
                    this.emitState();
                    resolve();
                },
                (error: unknown) => {
                    reject(new Error(session.pairId + session.slot + ' prepareAsync error: ' + JSON.stringify(error)));
                }
            );
        });
    }

    private playSession(session: PairSchedulerSession<TItem>): void {
        session.firstFrameHandler = null;
        this.applyDisplayRect(session, this.config.displayRect);
        this.callOptional(session.player, 'setVideoStillMode', ['false']);
        session.player.play();
        session.state = 'playing';
        this.options.onLayerRoleChange(session.pairId, session.slot, 'current');
        this.options.log(session.pairId + session.slot + ' playing: ' + session.item!.id + ' ' + session.item!.title);
        this.emitState();
    }

    private playSessionForFrameHandoff(session: PairSchedulerSession<TItem>, onFirstFrame: (currentTime: number) => void): void {
        session.firstFrameHandler = onFirstFrame;
        this.applyDisplayRect(session, this.config.displayRect);
        this.callOptional(session.player, 'setVideoStillMode', ['false']);
        this.options.onLayerRoleChange(session.pairId, session.slot, 'warming');
        session.player.play();
        session.state = 'playing';
        this.options.log(session.pairId + session.slot + ' warming: ' + session.item!.id + ' ' + session.item!.title);
        this.emitState();
    }

    private onCurrentPlaytime(pairId: string, slot: number, currentTime: number): void {
        const session = this.findSession(pairId, slot);

        if (!session || !session.firstFrameHandler) {
            return;
        }

        const handler = session.firstFrameHandler;
        session.firstFrameHandler = null;
        this.callOptional(session.player, 'revealLayer', []);
        this.options.log(pairId + slot + ' first frame: ' + currentTime + 'ms');
        handler(currentTime);
    }

    private onStreamCompleted(pairId: string, slot: number): void {
        this.options.log(pairId + slot + ' stream completed before scheduled transition');
    }

    private scheduleNextTransition(reason: string): void {
        const token = this.runToken;

        this.clearTransitionTimer();
        this.options.log('next frame handoff in ' + (this.transitionIntervalMs / 1000) + 's: ' + reason);
        this.transitionTimerId = window.setTimeout(() => {
            this.transitionTimerId = null;
            if (!this.isRunCurrent(token)) {
                return;
            }
            this.transitionActivePair();
        }, this.transitionIntervalMs);
    }

    private clearTransitionTimer(): void {
        if (this.transitionTimerId !== null) {
            window.clearTimeout(this.transitionTimerId);
            this.transitionTimerId = null;
        }
    }

    private isRunCurrent(token: number): boolean {
        return this.running && this.runToken === token;
    }

    private failRun(message: string): void {
        this.options.log(message);
        this.stop();
    }

    private transitionActivePair(): void {
        if (!this.running || this.transitioning) {
            return;
        }

        const pair = this.pairs[this.activePairIndex];
        if (pair.swapsInTurn < this.config.swapsBeforePairHandoff) {
            this.switchInsideActivePair(pair);
            return;
        }

        const preparePair = this.pairs[this.preparePairIndex];
        if (preparePair?.handoffReady) {
            this.handoffToPreparedPair(pair);
            return;
        }

        this.switchInsideActivePair(pair);
    }

    private switchInsideActivePair(pair: PairSchedulerPair<TItem>): void {
        const token = this.runToken;
        const completedSlot = pair.activeSlot;
        const completedSession = pair.sessions[completedSlot];
        const nextSlot = pair.prepareSlot;
        const nextSession = pair.sessions[nextSlot];

        this.transitioning = true;
        this.holdCompletedFrame(completedSession);
        pair.activeSlot = nextSlot;
        pair.prepareSlot = completedSlot;
        pair.swapsInTurn += 1;

        this.playSessionForFrameHandoff(nextSession, () => {
            this.lowerAndStopCompletedSession(completedSession);
            this.options.onLayerRoleChange(nextSession.pairId, nextSession.slot, 'current');

            this.prepareNextInActivePair(pair)
                .then(() => {
                    if (this.isRunCurrent(token)) {
                        this.transitioning = false;
                        this.scheduleNextTransition('pair-internal-ready');
                    }
                })
                .catch((error: Error) => {
                    this.failRun('prepare next failed: ' + error.message);
                });
        });
    }

    private handoffToPreparedPair(oldPair: PairSchedulerPair<TItem>): void {
        const token = this.runToken;
        const newPair = this.pairs[this.preparePairIndex];

        this.transitioning = true;

        if (!newPair.handoffReady) {
            this.preparePairForHandoff(newPair)
                .then(() => {
                    if (this.isRunCurrent(token)) {
                        this.playPreparedPair(oldPair, newPair, token);
                    }
                })
                .catch((error: Error) => {
                    this.failRun('prepare handoff pair failed: ' + error.message);
                });
            return;
        }

        this.playPreparedPair(oldPair, newPair, token);
    }

    private playPreparedPair(oldPair: PairSchedulerPair<TItem>, newPair: PairSchedulerPair<TItem>, token: number): void {
        const completedSession = oldPair.sessions[oldPair.activeSlot];
        const oldPairIndex = this.activePairIndex;
        const newSession = newPair.sessions[newPair.activeSlot];

        this.holdCompletedFrame(completedSession);
        this.playSessionForFrameHandoff(newSession, () => {
            this.releaseInactivePair(oldPair);
            this.options.onLayerRoleChange(newSession.pairId, newSession.slot, 'current');

            this.activePairIndex = this.preparePairIndex;
            this.preparePairIndex = oldPairIndex;
            newPair.handoffReady = false;

            this.options.log('pair handoff complete: active=' + newPair.label + ', prepare=' + oldPair.label);
            if (this.isRunCurrent(token)) {
                this.transitioning = false;
                this.scheduleNextTransition('new-active-pair-ready');
            }
            this.emitState();
        });
    }

    private holdCompletedFrame(session: PairSchedulerSession<TItem>): void {
        this.callOptional(session.player, 'setVideoStillMode', ['true']);
        session.state = 'held';
        this.options.onLayerRoleChange(session.pairId, session.slot, 'held');
        this.options.log(session.pairId + session.slot + ' holding last frame');
        this.emitState();
    }

    private releaseInactivePair(pair: PairSchedulerPair<TItem>): void {
        for (let i = 0; i < pair.sessions.length; i += 1) {
            this.releaseSession(pair.sessions[i], 'pair-handoff');
        }
        pair.activeSlot = 0;
        pair.prepareSlot = 1;
        pair.swapsInTurn = 0;
        pair.handoffReady = false;
    }

    private lowerAndStopCompletedSession(session: PairSchedulerSession<TItem>): void {
        session.firstFrameHandler = null;
        this.callOptional(session.player, 'setVideoStillMode', ['true']);
        this.lowerSession(session);
        this.safeStop(session.player);
        this.safeClose(session.player);
        session.state = 'stopped';
        session.item = null;
        this.options.log(session.pairId + session.slot + ' lowered and stopped');
        this.emitState();
    }

    private releaseSession(session: PairSchedulerSession<TItem>, reason: string): void {
        session.firstFrameHandler = null;
        this.lowerSession(session);
        this.safeStop(session.player);
        this.safeClose(session.player);
        session.item = null;
        session.state = 'idle';
        this.options.log(session.pairId + session.slot + ' released: ' + reason);
    }

    private lowerSession(session: PairSchedulerSession<TItem>): void {
        if (session.state === 'preparing' || session.state === 'ready' || session.state === 'playing' || session.state === 'held') {
            this.applyDisplayRect(session, this.config.lowerRect);
        }
        this.options.onLayerRoleChange(session.pairId, session.slot, 'hidden');
    }

    private applyDisplayRect(session: PairSchedulerSession<TItem>, rect: PairSchedulerRect): void {
        session.player.setDisplayRect(rect.x, rect.y, rect.width, rect.height);
    }

    private findSession(pairId: string, slot: number): PairSchedulerSession<TItem> | null {
        const pair = this.findPair(pairId);

        if (!pair) {
            return null;
        }
        return pair.sessions[slot] || null;
    }

    private findPair(pairId: string): PairSchedulerPair<TItem> | null {
        for (let i = 0; i < this.pairs.length; i += 1) {
            if (this.pairs[i].id === pairId) {
                return this.pairs[i];
            }
        }
        return null;
    }

    private safeStop(player: PairSchedulerPlayer): void {
        const state = this.getPlayerState(player);

        if (state === 'READY' || state === 'PLAYING' || state === 'PAUSED') {
            player.stop();
        }
    }

    private safeClose(player: PairSchedulerPlayer): void {
        const state = this.getPlayerState(player);

        if (state === 'IDLE' || state === 'READY' || state === 'PLAYING' || state === 'PAUSED') {
            player.close();
        }
    }

    private getPlayerState(player: PairSchedulerPlayer): string {
        if (typeof player.getState === 'function') {
            return player.getState();
        }
        return 'UNKNOWN';
    }

    private callOptional(player: PairSchedulerPlayer, methodName: keyof PairSchedulerPlayer, args: unknown[]): void {
        const method = player[methodName];

        if (typeof method === 'function') {
            (method as (...methodArgs: unknown[]) => void).apply(player, args);
        }
    }

    private emitState(): void {
        this.options.onStateChange(this.getSnapshot());
    }
}

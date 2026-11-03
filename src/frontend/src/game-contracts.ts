interface CommonContext {
    gameType: string;
    currentUser: {
        id: string;
        displayName: string;
    };
    /** Canal local con el Shell, propio de cada partida. No lleva tokens ni acredita identidad. */
    events: EventTarget;
}

/** Integrante de la sala, tal como lo entrega el Shell al iniciar la partida. */
export interface GameParticipant {
    userId: string;
    displayName: string;
}

/** Partida normal: viene de una sala real de Matchmaking. */
export type GameContext = CommonContext & {
    matchId: string;
    mode?: never;
    /**
     * Foto de los integrantes de la sala al iniciar, incluido el usuario actual. Typing no depende de
     * ella: los jugadores le llegan por el hub, y la lista no acredita identidad ante el servidor.
     */
    participants?: GameParticipant[];
};

/** Práctica (R-44): sin sala, sin conexiones y sin registros. */
export type PracticeContext = CommonContext & {
    mode: 'practice';
    matchId?: never;
};

export type GameLaunchContext = GameContext | PracticeContext;

export interface GameModule {
    initialize(context: GameLaunchContext): Promise<void>;
    start(): Promise<void>;
    pause(): Promise<void>;
    dispose(): Promise<void>;
}

/**
 * Evento con el que el juego avisa al Shell que la partida terminó (R-46). Se emite en
 * `context.events` como un CustomEvent con un GameFinishedDetail. El Shell lo recibe y notifica
 * a Matchmaking: el juego no llama a Matchmaking.
 */
export const GAME_FINISHED_EVENT = 'battlehub:game-finished';

export interface GameFinishedDetail {
    matchId: string;
    gameType: string;
}

/* =========================
   SignalR - Requests
   ========================= */

export interface JoinMatchRequest {
    matchId: string;
    currentUser: string;
    displayName: string | null;
}

export interface LeaveMatchRequest {
    matchId: string;
    currentUser: string;
}

export interface PlayerUpdateDto {
    matchId: string;
    currentUser: string;
    score: number | null;
    wpm: number | null;
    accuracy: number | null;
    timestamp: string | null;
}

export interface PlayerSummaryDto {
    userId: string;
    displayName: string | null;
    score: number;
}

export interface EndMatchRequest {
    matchId: string;
    currentUser: string;
    players: (PlayerSummaryDto | null)[] | null;
    startedAt: string | null;
    finishedAt: string | null;
    winnerUserId: string | null;
    metadata: unknown | null;
}

/* =========================
   SignalR - Notifications
   ========================= */

export interface PlayerJoinedNotification {
    userId: string;
}

export interface PlayerLeftNotification {
    userId: string;
}

export interface PlayerUpdateNotification {
    userId: string;
    score: number | null;
    wpm: number | null;
    accuracy: number | null;
}

export interface MatchEndedNotification {
    matchId: string;
    outcome: string;
    resultOrErrors: unknown | null;
}

/* =========================
   SignalR - Errors
   ========================= */

export interface JoinFailedNotification {
    matchId: string;
    reason: string;
}

export interface UpdateFailedNotification {
    matchId: string;
    reason: string;
}
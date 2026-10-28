export interface GameContext {
    matchId?: string;
    gameType: string;
    currentUser: {
        id: string;
        displayName: string;
    };
}

export interface GameModule {
    initialize(context: GameContext): Promise<void>;
    start(): Promise<void>;
    pause(): Promise<void>;
    dispose(): Promise<void>;
}

/**
 * Evento con el que el juego avisa al Shell que la partida terminó (R-46). Se emite en `window`
 * como un CustomEvent con un GameFinishedDetail. El Shell lo recibe y notifica a Matchmaking:
 * el juego no llama a Matchmaking.
 */
export const GAME_FINISHED_EVENT = 'battlehub:game-finished';

export interface GameFinishedPlayer {
    userId: string;
    displayName: string;
    score: number;
    /** Puesto final: 1 es el ganador. */
    position: number;
}

export interface GameFinishedDetail {
    matchId: string;
    gameType: string;
    winnerUserId: string | null;
    /** En orden de ranking. Vacío si este cliente no recibió el resultado (ya lo había guardado otro jugador). */
    players: GameFinishedPlayer[];
    /** Fecha de fin en UTC (ISO-8601 con Z). */
    finishedAt: string | null;
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
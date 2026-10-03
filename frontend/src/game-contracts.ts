export interface GameContext {
    matchId: string;
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
 * Extensiones opcionales que el Shell puede agregar al contexto (no forman
 * parte del contrato 03 §5). getAccessToken devuelve el access token de
 * Auth0 del usuario para la API y el hub del juego. Sin ella, el juego usa
 * la identidad de desarrollo, que la API solo acepta con Auth:Mode=Development.
 */
export interface GameContextExtensions {
    getAccessToken?: () => Promise<string | null | undefined>;
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
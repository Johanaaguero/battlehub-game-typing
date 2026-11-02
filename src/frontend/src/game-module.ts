import { customElement } from 'aurelia';

import {
    GAME_FINISHED_EVENT,
    type EndMatchRequest,
    type GameContext,
    type GameFinishedDetail,
    type GameModule as IGameModule,
    type JoinMatchRequest,
    type LeaveMatchRequest,
    type PlayerUpdateDto,
} from './game-contracts';

import { RESULTS_API_URL, TYPING_HUB_URL } from './config';
import { SignalRClient } from './signalr-client';

interface TypingPlayer {
    userId: string;
    displayName: string;
    score: number | null;
    wpm: number | null;
    accuracy: number | null;
}

interface ResultPlayer {
    userId: string;
    displayName: string;
    score: number;
}

interface GameResult {
    matchId: string;
    gameType: string;
    players: ResultPlayer[];
    startedAt: string;
    finishedAt: string;
    winnerUserId: string | null;
    metadata: unknown;
}

interface PlayerHistoryItem {
    matchId: string;
    startedAt: string;
    finishedAt: string;
    score: number;
    wpm: number | null;
    accuracy: number | null;
    position: number;
    playersCount: number;
    won: boolean;
}

interface PlayerStats {
    userId: string;
    gamesPlayed: number;
    wins: number;
    winRate: number;
    averageScore: number;
    bestScore: number;
    averageWpm: number | null;
    bestWpm: number | null;
    averageAccuracy: number | null;
    lastPlayedAt: string | null;
}

type ActiveSection =
    | 'game'
    | 'history'
    | 'stats';

@customElement('typing-game-module')
export class GameModule implements IGameModule {
    public context: GameContext | null = null;

    /**
     * Indica que el juego se está ejecutando localmente,
     * sin Shell, matchId ni conexión SignalR.
     */
    public isPracticeMode = false;


    // =========================================================
    // INTERFAZ - WAYNER
    // =========================================================

    public targetText =
        'La tecnología permite conectar personas, compartir información y crear soluciones para los desafíos del mundo moderno.';

    public typedText = '';

    public progress = 0;

    public correctCharacters = 0;

    public incorrectCharacters = 0;

import { customElement } from 'aurelia';

import type {
    EndMatchRequest,
    GameContext,
    GameModule as IGameModule,
    JoinMatchRequest,
    LeaveMatchRequest,
    PlayerUpdateDto,
} from './game-contracts';

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

    // =========================================================
    // INTERFAZ - WAYNER
    // =========================================================

    public targetText =
        'La tecnología permite conectar personas, compartir información y crear soluciones para los desafíos del mundo moderno.';

    public typedText = '';

    public progress = 0;

    public correctCharacters = 0;

    public incorrectCharacters = 0;

    public accuracy = 100;

    // ---------------------------------------------------------
    // TEMPORIZADOR
    // ---------------------------------------------------------

    public timeRemaining = 60;

    public isTimeUp = false;

    private timerStarted = false;

    private timerId:
        ReturnType<typeof setInterval> | null = null;

    // ---------------------------------------------------------
    // VELOCIDAD
    // ---------------------------------------------------------

    public wpm = 0;

    // ---------------------------------------------------------
    // JUGADORES
    // ---------------------------------------------------------

    public players: TypingPlayer[] = [];

    // ---------------------------------------------------------
    // RESULTADOS
    // ---------------------------------------------------------

    public matchFinished = false;

    public matchOutcome: string | null = null;

    public resultMessage = '';

    public finalResult: GameResult | null = null;

    // ---------------------------------------------------------
    // NAVEGACIÓN DE LA INTERFAZ
    // ---------------------------------------------------------

    public activeSection: ActiveSection = 'game';

    // ---------------------------------------------------------
    // HISTORIAL
    // ---------------------------------------------------------

    public history: PlayerHistoryItem[] = [];

    public historyLoading = false;

    public historyLoaded = false;

    public historyError = '';

    // ---------------------------------------------------------
    // ESTADÍSTICAS
    // ---------------------------------------------------------

    public stats: PlayerStats | null = null;

    public statsLoading = false;

    public statsLoaded = false;

    public statsError = '';

    // =========================================================
    // FIN INTERFAZ - WAYNER
    // =========================================================

    private running = false;

    private readonly apiBaseUrl =
        'http://localhost:5015/api/games/typing';

    private readonly signalR = new SignalRClient(
        'http://localhost:5015/hubs/typing'
    );

    public async initialize(
        context: GameContext
    ): Promise<void> {
        if (!context.matchId) {
            throw new Error(
                'matchId es requerido'
            );
        }

        if (!context.gameType) {
            throw new Error(
                'gameType es requerido'
            );
        }

        if (!context.currentUser?.id) {
            throw new Error(
                'currentUser.id es requerido'
            );
        }

        this.context = context;

        this.running = false;

        this.resetGameInterface();

        this.players = [
            {
                userId:
                    context.currentUser.id,

                displayName:
                    context.currentUser.displayName,

                score: null,

                wpm: 0,

                accuracy: 100,
            },
        ];

        this.registerSignalREvents();

        console.log(
            'Typing Battle inicializado:',
            context
        );
    }

    public async start(): Promise<void> {
        if (!this.context) {
            throw new Error(
                'El juego debe inicializarse antes de comenzar'
            );
        }

        if (this.running) {
            return;
        }

        await this.signalR.connect();

        const joinRequest: JoinMatchRequest = {
            matchId:
                this.context.matchId,

            currentUser:
                this.context.currentUser.id,

            displayName:
                this.context.currentUser.displayName,
        };

        const joined =
            await this.signalR.joinMatch(
                joinRequest
            );

        if (!joined) {
            throw new Error(
                'No fue posible unirse a la partida'
            );
        }

        this.running = true;

        console.log(
            'Typing Battle iniciado y unido a la partida:',
            this.context.matchId
        );
    }

    public async pause(): Promise<void> {
        this.running = false;

        this.stopTimer();

        console.log(
            'Typing Battle pausado'
        );
    }

    public async dispose(): Promise<void> {
        this.stopTimer();

        if (this.context) {
            const leaveRequest:
                LeaveMatchRequest = {

                matchId:
                    this.context.matchId,

                currentUser:
                    this.context.currentUser.id,
            };

            try {
                if (
                    this.signalR.state ===
                    'Connected'
                ) {
                    await this.signalR.leaveMatch(
                        leaveRequest
                    );
                }
            } catch (error) {
                console.error(
                    'Error al salir de la partida:',
                    error
                );
            }
        }

        await this.signalR.disconnect();

        this.running = false;

        this.context = null;

        this.players = [];

        console.log(
            'Typing Battle liberado'
        );
    }

    /**
     * Eventos recibidos desde SignalR.
     */
    private registerSignalREvents(): void {
        this.signalR.onPlayerJoined(
            (notification) => {

                const exists =
                    this.players.some(
                        player =>
                            player.userId ===
                            notification.userId
                    );

                if (!exists) {
                    this.players = [
                        ...this.players,
                        {
                            userId:
                                notification.userId,

                            displayName:
                                notification.userId,

                            score: null,

                            wpm: 0,

                            accuracy: null,
                        },
                    ];
                }

                console.log(
                    'Jugador se unió:',
                    notification.userId
                );
            }
        );

        this.signalR.onPlayerLeft(
            (notification) => {

                this.players =
                    this.players.filter(
                        player =>
                            player.userId !==
                            notification.userId
                    );

                console.log(
                    'Jugador salió:',
                    notification.userId
                );
            }
        );

        this.signalR.onPlayerUpdate(
            (notification) => {

                const existingPlayer =
                    this.players.find(
                        player =>
                            player.userId ===
                            notification.userId
                    );

                if (existingPlayer) {
                    existingPlayer.score =
                        notification.score;

                    existingPlayer.wpm =
                        notification.wpm;

                    existingPlayer.accuracy =
                        notification.accuracy;
                } else {
                    this.players = [
                        ...this.players,
                        {
                            userId:
                                notification.userId,

                            displayName:
                                notification.userId,

                            score:
                                notification.score,

                            wpm:
                                notification.wpm,

                            accuracy:
                                notification.accuracy,
                        },
                    ];
                }

                console.log(
                    'Actualización del jugador:',
                    notification
                );
            }
        );

        this.signalR.onMatchEnded(
            (notification) => {

                console.log(
                    'Partida finalizada:',
                    notification
                );

                this.running = false;

                this.isTimeUp = true;

                this.matchFinished = true;

                this.matchOutcome =
                    notification.outcome;

                this.stopTimer();

                this.processMatchResult(
                    notification.outcome,
                    notification.resultOrErrors
                );

                /*
                 * Cuando termina oficialmente una partida,
                 * invalidamos historial y estadísticas para
                 * que la próxima consulta incluya la partida
                 * recién terminada.
                 */
                this.historyLoaded = false;

                this.statsLoaded = false;
            }
        );

        this.signalR.onJoinFailed(
            (notification) => {

                console.error(
                    'No se pudo unir a la partida:',
                    notification
                );
            }
        );

        this.signalR.onUpdateFailed(
            (notification) => {

                console.error(
                    'No se pudo actualizar al jugador:',
                    notification
                );
            }
        );
    }

    // =========================================================
    // INTERFAZ - WAYNER
    // =========================================================

    /**
     * Mostrar pantalla del juego.
     */
    public showGame(): void {
        this.activeSection = 'game';
    }

    /**
     * Mostrar y cargar historial.
     */
    public async showHistory(): Promise<void> {
        this.activeSection = 'history';

        if (!this.historyLoaded) {
            await this.loadHistory();
        }
    }

    /**
     * Mostrar y cargar estadísticas.
     */
    public async showStats(): Promise<void> {
        this.activeSection = 'stats';

        if (!this.statsLoaded) {
            await this.loadStats();
        }
    }

    /**
     * Se ejecuta cada vez que el jugador escribe.
     */
    public handleTyping(): void {
        if (
            this.isTimeUp ||
            this.matchFinished
        ) {
            return;
        }

        if (!this.timerStarted) {
            this.startTimer();
        }

        if (
            this.targetText.length === 0
        ) {
            this.progress = 0;

            this.correctCharacters = 0;

            this.incorrectCharacters = 0;

            this.accuracy = 100;

            this.wpm = 0;

            this.updateCurrentPlayerInterface();

            return;
        }

        this.calculateProgress();

        this.calculateAccuracy();

        this.calculateWpm();

        this.updateCurrentPlayerInterface();

        void this.sendCurrentMetrics();
    }

    /**
     * Calcula el porcentaje de avance.
     */
    private calculateProgress(): void {
        const charactersTyped =
            Math.min(
                this.typedText.length,
                this.targetText.length
            );

        this.progress =
            Math.round(
                (
                    charactersTyped /
                    this.targetText.length
                ) * 100
            );
    }

    /**
     * Calcula caracteres correctos,
     * errores y precisión.
     */
    private calculateAccuracy(): void {
        let correct = 0;

        let incorrect = 0;

        for (
            let i = 0;
            i < this.typedText.length;
            i++
        ) {
            if (
                i >=
                this.targetText.length
            ) {
                incorrect++;

                continue;
            }

            if (
                this.typedText[i] ===
                this.targetText[i]
            ) {
                correct++;
            } else {
                incorrect++;
            }
        }

        this.correctCharacters =
            correct;

        this.incorrectCharacters =
            incorrect;

        const totalTyped =
            this.correctCharacters +
            this.incorrectCharacters;

        if (totalTyped === 0) {
            this.accuracy = 100;

            return;
        }

        this.accuracy =
            Math.round(
                (
                    this.correctCharacters /
                    totalTyped
                ) * 100
            );
    }

    /**
     * Calcula palabras por minuto.
     * 5 caracteres correctos = 1 palabra.
     */
    private calculateWpm(): void {
        const elapsedSeconds =
            60 - this.timeRemaining;

        if (elapsedSeconds <= 0) {
            this.wpm = 0;

            return;
        }

        const elapsedMinutes =
            elapsedSeconds / 60;

        const words =
            this.correctCharacters / 5;

        this.wpm =
            Math.round(
                words /
                elapsedMinutes
            );
    }

    /**
     * Inicia temporizador.
     */
    private startTimer(): void {
        if (
            this.timerStarted ||
            this.isTimeUp ||
            this.matchFinished
        ) {
            return;
        }

        this.timerStarted = true;

        this.timerId =
            setInterval(() => {

                if (
                    this.timeRemaining > 0
                ) {
                    this.timeRemaining--;

                    this.calculateWpm();

                    this.updateCurrentPlayerInterface();
                }

                if (
                    this.timeRemaining <= 0
                ) {
                    this.timeRemaining = 0;

                    this.isTimeUp = true;

                    this.stopTimer();

                    this.updateCurrentPlayerInterface();

                    void this.sendCurrentMetrics();

                    console.log(
                        'Tiempo finalizado.',
                        {
                            wpm:
                                this.wpm,

                            accuracy:
                                this.accuracy,

                            progress:
                                this.progress,
                        }
                    );
                }
            }, 1000);
    }

    /**
     * Detiene temporizador.
     */
    private stopTimer(): void {
        if (
            this.timerId !== null
        ) {
            clearInterval(
                this.timerId
            );

            this.timerId = null;
        }
    }

    /**
     * Reinicia datos visuales.
     */
    private resetGameInterface(): void {
        this.stopTimer();

        this.typedText = '';

        this.progress = 0;

        this.correctCharacters = 0;

        this.incorrectCharacters = 0;

        this.accuracy = 100;

        this.wpm = 0;

        this.timeRemaining = 60;

        this.isTimeUp = false;

        this.timerStarted = false;

        this.players = [];

        this.matchFinished = false;

        this.matchOutcome = null;

        this.resultMessage = '';

        this.finalResult = null;

        this.activeSection = 'game';

        this.history = [];

        this.historyLoading = false;

        this.historyLoaded = false;

        this.historyError = '';

        this.stats = null;

        this.statsLoading = false;

        this.statsLoaded = false;

        this.statsError = '';
    }

    /**
     * Actualiza métricas del jugador actual
     * dentro del panel lateral.
     */
    private updateCurrentPlayerInterface(): void {
        if (!this.context) {
            return;
        }

        const currentPlayer =
            this.players.find(
                player =>
                    player.userId ===
                    this.context
                        ?.currentUser.id
            );

        if (!currentPlayer) {
            return;
        }

        currentPlayer.wpm =
            this.wpm;

        currentPlayer.accuracy =
            this.accuracy;
    }

    /**
     * Envía métricas por SignalR
     * si existe conexión.
     */
    private async sendCurrentMetrics():
        Promise<void> {

        if (!this.context) {
            return;
        }

        if (
            this.signalR.state !==
            'Connected'
        ) {
            return;
        }

        try {
            await this.sendPlayerUpdate(
                null,
                this.wpm,
                this.accuracy
            );
        } catch (error) {
            console.error(
                'No fue posible enviar las métricas del jugador:',
                error
            );
        }
    }

    // =========================================================
    // HISTORIAL
    // =========================================================

    /**
     * Consulta el historial real
     * del jugador en la API.
     */
    public async loadHistory(): Promise<void> {
        if (!this.context) {
            return;
        }

        this.historyLoading = true;

        this.historyError = '';

        try {
            const userId =
                encodeURIComponent(
                    this.context.currentUser.id
                );

            const response =
                await fetch(
                    `${this.apiBaseUrl}/players/${userId}/history?limit=50&offset=0`
                );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status}`
                );
            }

            const history =
                await response.json() as
                PlayerHistoryItem[];

            this.history = history;

            this.historyLoaded = true;
        } catch (error) {
            this.history = [];

            this.historyLoaded = false;

            this.historyError =
                'No fue posible cargar el historial. Verificá que el backend esté disponible.';

            console.error(
                'Error cargando historial:',
                error
            );
        } finally {
            this.historyLoading = false;
        }
    }

    // =========================================================
    // ESTADÍSTICAS
    // =========================================================

    /**
     * Consulta estadísticas agregadas
     * del jugador en la API.
     */
    public async loadStats(): Promise<void> {
        if (!this.context) {
            return;
        }

        this.statsLoading = true;

        this.statsError = '';

        try {
            const userId =
                encodeURIComponent(
                    this.context.currentUser.id
                );

            const response =
                await fetch(
                    `${this.apiBaseUrl}/players/${userId}/stats`
                );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status}`
                );
            }

            const stats =
                await response.json() as
                PlayerStats;

            this.stats = stats;

            this.statsLoaded = true;
        } catch (error) {
            this.stats = null;

            this.statsLoaded = false;

            this.statsError =
                'No fue posible cargar las estadísticas. Verificá que el backend esté disponible.';

            console.error(
                'Error cargando estadísticas:',
                error
            );
        } finally {
            this.statsLoading = false;
        }
    }

    /**
     * Recarga manualmente historial.
     */
    public async refreshHistory():
        Promise<void> {

        this.historyLoaded = false;

        await this.loadHistory();
    }

    /**
     * Recarga manualmente estadísticas.
     */
    public async refreshStats():
        Promise<void> {

        this.statsLoaded = false;

        await this.loadStats();
    }

    /**
     * Convierte winRate 0..1
     * a porcentaje.
     */
    public get winRatePercentage():
        number {

        if (!this.stats) {
            return 0;
        }

        return Math.round(
            this.stats.winRate *
            1000
        ) / 10;
    }

    /**
     * Formatea una fecha ISO para
     * mostrarla al usuario.
     */
    public formatDate(
        value: string | null
    ): string {

        if (!value) {
            return 'Sin datos';
        }

        const date =
            new Date(value);

        if (
            Number.isNaN(
                date.getTime()
            )
        ) {
            return 'Sin datos';
        }

        return date.toLocaleString(
            'es-CR',
            {
                dateStyle: 'medium',
                timeStyle: 'short',
            }
        );
    }

    /**
     * Formatea valores opcionales.
     */
    public formatMetric(
        value: number | null
    ): string {

        if (value === null) {
            return '—';
        }

        return value.toFixed(1);
    }

    // =========================================================
    // RESULTADOS
    // =========================================================

    private processMatchResult(
        outcome: string,
        resultOrErrors: unknown
    ): void {

        switch (outcome) {
            case 'created':

                if (
                    this.isGameResult(
                        resultOrErrors
                    )
                ) {
                    this.finalResult =
                        resultOrErrors;

                    this.resultMessage =
                        'Partida finalizada correctamente.';

                    this.updatePlayersFromFinalResult(
                        resultOrErrors
                    );
                } else {
                    this.resultMessage =
                        'La partida terminó, pero no fue posible leer el resultado.';
                }

                break;

            case 'already_exists':

                this.resultMessage =
                    'El resultado de esta partida ya había sido registrado.';

                break;

            case 'invalid':

                this.resultMessage =
                    'No fue posible guardar el resultado porque contiene datos inválidos.';

                console.error(
                    'Errores de validación:',
                    resultOrErrors
                );

                break;

            case 'error':

                this.resultMessage =
                    'Ocurrió un error al finalizar la partida.';

                console.error(
                    'Error al finalizar la partida:',
                    resultOrErrors
                );

                break;

            default:

                this.resultMessage =
                    'La partida ha finalizado.';

                console.warn(
                    'Resultado no reconocido:',
                    outcome,
                    resultOrErrors
                );

                break;
        }
    }

    private isGameResult(
        value: unknown
    ): value is GameResult {

        if (
            typeof value !==
            'object' ||
            value === null
        ) {
            return false;
        }

        const candidate =
            value as Partial<GameResult>;

        return (
            typeof candidate.matchId ===
            'string' &&
            typeof candidate.gameType ===
            'string' &&
            Array.isArray(
                candidate.players
            )
        );
    }

    private updatePlayersFromFinalResult(
        result: GameResult
    ): void {

        for (
            const resultPlayer
            of result.players
        ) {
            const existingPlayer =
                this.players.find(
                    player =>
                        player.userId ===
                        resultPlayer.userId
                );

            if (existingPlayer) {
                existingPlayer.displayName =
                    resultPlayer.displayName;

                existingPlayer.score =
                    resultPlayer.score;
            } else {
                this.players = [
                    ...this.players,
                    {
                        userId:
                            resultPlayer.userId,

                        displayName:
                            resultPlayer.displayName,

                        score:
                            resultPlayer.score,

                        wpm: null,

                        accuracy: null,
                    },
                ];
            }
        }
    }

    public isWinner(
        userId: string
    ): boolean {

        return (
            this.finalResult
                ?.winnerUserId ===
            userId
        );
    }

    public get winnerName():
        string {

        if (
            !this.finalResult
                ?.winnerUserId
        ) {
            return '';
        }

        const winner =
            this.finalResult
                .players.find(
                    player =>
                        player.userId ===
                        this.finalResult
                            ?.winnerUserId
                );

        return (
            winner?.displayName ??
            this.finalResult
                .winnerUserId
        );
    }

    // =========================================================
    // FIN INTERFAZ - WAYNER
    // =========================================================

    /**
     * Envía estadísticas del jugador.
     */
    public async sendPlayerUpdate(
        score: number | null,
        wpm: number | null,
        accuracy: number | null
    ): Promise<boolean> {

        if (!this.context) {
            throw new Error(
                'El juego debe inicializarse antes de enviar estadísticas'
            );
        }

        const update:
            PlayerUpdateDto = {

            matchId:
                this.context.matchId,

            currentUser:
                this.context
                    .currentUser.id,

            score,

            wpm,

            accuracy,

            timestamp:
                new Date()
                    .toISOString(),
        };

        return await this.signalR
            .sendPlayerUpdate(
                update
            );
    }

    /**
     * Finaliza una partida.
     */
    public async endMatch(
        players:
            EndMatchRequest['players'],

        startedAt:
            string | null,

        finishedAt:
            string | null,

        winnerUserId:
            string | null,

        metadata:
            unknown | null
    ): Promise<void> {

        if (!this.context) {
            throw new Error(
                'El juego debe inicializarse antes de finalizar'
            );
        }

        const request:
            EndMatchRequest = {

            matchId:
                this.context.matchId,

            currentUser:
                this.context
                    .currentUser.id,

            players,

            startedAt,

            finishedAt,

            winnerUserId,

            metadata,
        };

        await this.signalR.endMatch(
            request
        );

        this.running = false;

        this.stopTimer();
    }
}
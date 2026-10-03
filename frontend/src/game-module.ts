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

@customElement('typing-game-module')
export class GameModule implements IGameModule {
    public context: GameContext | null = null;

    private running = false;

    private readonly signalR = new SignalRClient(
        'http://localhost:5015/hubs/typing'
    );

    public async initialize(context: GameContext): Promise<void> {
        if (!context.matchId) {
            throw new Error('matchId es requerido');
        }

        if (!context.gameType) {
            throw new Error('gameType es requerido');
        }

        if (!context.currentUser?.id) {
            throw new Error('currentUser.id es requerido');
        }

        this.context = context;
        this.running = false;

        this.registerSignalREvents();

        console.log('Typing Battle inicializado:', context);
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
            matchId: this.context.matchId,
            currentUser: this.context.currentUser.id,
            displayName: this.context.currentUser.displayName,
        };

        const joined = await this.signalR.joinMatch(joinRequest);

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

        console.log('Typing Battle pausado');
    }

    public async dispose(): Promise<void> {
        if (this.context) {
            const leaveRequest: LeaveMatchRequest = {
                matchId: this.context.matchId,
                currentUser: this.context.currentUser.id,
            };

            try {
                if (this.signalR.state === 'Connected') {
                    await this.signalR.leaveMatch(leaveRequest);
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

        console.log('Typing Battle liberado');
    }

    /**
     * Registra los eventos que llegan desde el backend
     * a través de SignalR.
     */
    private registerSignalREvents(): void {
        this.signalR.onPlayerJoined((notification) => {
            console.log(
                'Jugador se unió:',
                notification.userId
            );
        });

        this.signalR.onPlayerLeft((notification) => {
            console.log(
                'Jugador salió:',
                notification.userId
            );
        });

        this.signalR.onPlayerUpdate((notification) => {
            console.log(
                'Actualización del jugador:',
                notification
            );
        });

        this.signalR.onMatchEnded((notification) => {
            console.log(
                'Partida finalizada:',
                notification
            );

            this.running = false;
        });

        this.signalR.onJoinFailed((notification) => {
            console.error(
                'No se pudo unir a la partida:',
                notification
            );
        });

        this.signalR.onUpdateFailed((notification) => {
            console.error(
                'No se pudo actualizar al jugador:',
                notification
            );
        });
    }

    /**
     * Envía las estadísticas actuales del jugador.
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

        const update: PlayerUpdateDto = {
            matchId: this.context.matchId,
            currentUser: this.context.currentUser.id,
            score,
            wpm,
            accuracy,
            timestamp: new Date().toISOString(),
        };

        return await this.signalR.sendPlayerUpdate(update);
    }

    /**
     * Finaliza la partida y envía el resultado al backend.
     */
    public async endMatch(
        players: EndMatchRequest['players'],
        startedAt: string | null,
        finishedAt: string | null,
        winnerUserId: string | null,
        metadata: unknown | null
    ): Promise<void> {
        if (!this.context) {
            throw new Error(
                'El juego debe inicializarse antes de finalizar'
            );
        }

        const request: EndMatchRequest = {
            matchId: this.context.matchId,
            currentUser: this.context.currentUser.id,
            players,
            startedAt,
            finishedAt,
            winnerUserId,
            metadata,
        };

        await this.signalR.endMatch(request);

        this.running = false;
    }
}
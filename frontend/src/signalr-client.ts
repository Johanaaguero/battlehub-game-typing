import {
    HubConnection,
    HubConnectionBuilder,
    HubConnectionState,
} from '@microsoft/signalr';

import type {
    EndMatchRequest,
    JoinMatchRequest,
    LeaveMatchRequest,
    PlayerUpdateDto,
} from './game-contracts';

export class SignalRClient {
    private readonly connection: HubConnection;

    public constructor(
        private readonly hubUrl: string
    ) {
        this.connection = new HubConnectionBuilder()
            .withUrl(this.hubUrl)
            .withAutomaticReconnect()
            .build();
    }

    /**
     * Inicia la conexión con el Hub de SignalR.
     */
    public async connect(): Promise<void> {
        if (
            this.connection.state === HubConnectionState.Connected ||
            this.connection.state === HubConnectionState.Connecting
        ) {
            return;
        }

        await this.connection.start();

        console.log('SignalR conectado');
    }

    /**
     * Se une a una partida.
     */
    public async joinMatch(
        request: JoinMatchRequest
    ): Promise<boolean> {
        return await this.connection.invoke<boolean>(
            'JoinMatch',
            request
        );
    }

    /**
     * Sale de una partida.
     */
    public async leaveMatch(
        request: LeaveMatchRequest
    ): Promise<boolean> {
        return await this.connection.invoke<boolean>(
            'LeaveMatch',
            request
        );
    }

    /**
     * Envía las estadísticas actuales del jugador.
     */
    public async sendPlayerUpdate(
        update: PlayerUpdateDto
    ): Promise<boolean> {
        return await this.connection.invoke<boolean>(
            'SendPlayerUpdate',
            update
        );
    }

    /**
     * Finaliza una partida.
     */
    public async endMatch(
        request: EndMatchRequest
    ): Promise<void> {
        await this.connection.invoke(
            'EndMatch',
            request
        );
    }

    /**
     * Escucha cuando un jugador entra a la partida.
     */
    public onPlayerJoined(
        callback: (notification: { userId: string }) => void
    ): void {
        this.connection.on('playerJoined', callback);
    }

    /**
     * Escucha cuando un jugador sale de la partida.
     */
    public onPlayerLeft(
        callback: (notification: { userId: string }) => void
    ): void {
        this.connection.on('playerLeft', callback);
    }

    /**
     * Escucha las actualizaciones de los jugadores.
     */
    public onPlayerUpdate(
        callback: (notification: {
            userId: string;
            score: number | null;
            wpm: number | null;
            accuracy: number | null;
        }) => void
    ): void {
        this.connection.on('playerUpdate', callback);
    }

    /**
     * Escucha cuando termina la partida.
     */
    public onMatchEnded(
        callback: (notification: {
            matchId: string;
            outcome: string;
            resultOrErrors: unknown;
        }) => void
    ): void {
        this.connection.on('matchEnded', callback);
    }

    /**
     * Escucha errores al intentar unirse.
     */
    public onJoinFailed(
        callback: (notification: {
            matchId: string;
            reason: string;
        }) => void
    ): void {
        this.connection.on('joinFailed', callback);
    }

    /**
     * Escucha errores al enviar estadísticas.
     */
    public onUpdateFailed(
        callback: (notification: {
            matchId: string;
            reason: string;
        }) => void
    ): void {
        this.connection.on('updateFailed', callback);
    }

    /**
     * Elimina todos los listeners registrados.
     */
    public removeAllListeners(): void {
        this.connection.off('playerJoined');
        this.connection.off('playerLeft');
        this.connection.off('playerUpdate');
        this.connection.off('matchEnded');
        this.connection.off('joinFailed');
        this.connection.off('updateFailed');
    }

    /**
     * Cierra la conexión con SignalR.
     */
    public async disconnect(): Promise<void> {
        this.removeAllListeners();

        if (
            this.connection.state !== HubConnectionState.Disconnected
        ) {
            await this.connection.stop();
        }

        console.log('SignalR desconectado');
    }

    /**
     * Permite consultar el estado actual de la conexión.
     */
    public get state(): HubConnectionState {
        return this.connection.state;
    }
}
import { SignalRClient } from '../src/signalr-client';
import type {
    EndMatchRequest,
    JoinMatchRequest,
    LeaveMatchRequest,
    PlayerUpdateDto,
} from '../src/game-contracts';

// La conexión real de @microsoft/signalr se reemplaza: aquí solo se comprueba cómo la usa el cliente.
const mockConnection = {
    state: 'Disconnected',
    start: jest.fn(),
    stop: jest.fn(),
    invoke: jest.fn(),
    on: jest.fn(),
    off: jest.fn(),
};

const mockBuilder = {
    withUrl: jest.fn(),
    withAutomaticReconnect: jest.fn(),
    build: jest.fn(),
};

jest.mock('@microsoft/signalr', () => ({
    HubConnectionBuilder: jest.fn(() => mockBuilder),
    HubConnectionState: {
        Disconnected: 'Disconnected',
        Connecting: 'Connecting',
        Connected: 'Connected',
        Disconnecting: 'Disconnecting',
        Reconnecting: 'Reconnecting',
    },
}));

const HUB_URL = 'http://typing-api.test/hubs/typing';

describe('SignalRClient', () => {
    let client: SignalRClient;

    beforeEach(() => {
        jest.clearAllMocks();
        jest.spyOn(console, 'log').mockImplementation(() => undefined);
        mockConnection.state = 'Disconnected';
        mockConnection.start.mockResolvedValue(undefined);
        mockConnection.stop.mockResolvedValue(undefined);
        mockConnection.invoke.mockResolvedValue(true);
        mockBuilder.withUrl.mockReturnValue(mockBuilder);
        mockBuilder.withAutomaticReconnect.mockReturnValue(mockBuilder);
        mockBuilder.build.mockReturnValue(mockConnection);
        client = new SignalRClient(HUB_URL);
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    describe('construcción', () => {
        it('se conecta solo con la URL del hub: sin credenciales ni proveedores de token (R-30)', () => {
            expect(mockBuilder.withUrl).toHaveBeenCalledTimes(1);
            expect(mockBuilder.withUrl).toHaveBeenCalledWith(HUB_URL);
        });

        it('reconecta automáticamente si se cae la conexión', () => {
            expect(mockBuilder.withAutomaticReconnect).toHaveBeenCalledTimes(1);
            expect(mockBuilder.build).toHaveBeenCalledTimes(1);
        });
    });

    describe('connect', () => {
        it('inicia la conexión cuando está desconectado', async () => {
            await client.connect();

            expect(mockConnection.start).toHaveBeenCalledTimes(1);
        });

        it.each(['Connected', 'Connecting'])('no vuelve a iniciar si ya está %s', async (state) => {
            mockConnection.state = state;

            await client.connect();

            expect(mockConnection.start).not.toHaveBeenCalled();
        });

        it('propaga el error si el servidor no responde', async () => {
            mockConnection.start.mockRejectedValue(new Error('sin servidor'));

            await expect(client.connect()).rejects.toThrow('sin servidor');
        });
    });

    describe('invocaciones al hub', () => {
        it('joinMatch invoca JoinMatch con la solicitud y devuelve la respuesta', async () => {
            const request: JoinMatchRequest = { matchId: 'm-1', currentUser: 'u-1', displayName: 'Ana' };

            await expect(client.joinMatch(request)).resolves.toBe(true);

            expect(mockConnection.invoke).toHaveBeenCalledWith('JoinMatch', request);
        });

        it('leaveMatch invoca LeaveMatch', async () => {
            const request: LeaveMatchRequest = { matchId: 'm-1', currentUser: 'u-1' };
            mockConnection.invoke.mockResolvedValue(false);

            await expect(client.leaveMatch(request)).resolves.toBe(false);

            expect(mockConnection.invoke).toHaveBeenCalledWith('LeaveMatch', request);
        });

        it('sendPlayerUpdate invoca SendPlayerUpdate', async () => {
            const update: PlayerUpdateDto = {
                matchId: 'm-1',
                currentUser: 'u-1',
                score: null,
                wpm: 40,
                accuracy: 95,
                timestamp: '2026-11-02T12:00:00Z',
            };

            await expect(client.sendPlayerUpdate(update)).resolves.toBe(true);

            expect(mockConnection.invoke).toHaveBeenCalledWith('SendPlayerUpdate', update);
        });

        it('endMatch invoca EndMatch sin esperar un resultado', async () => {
            const request: EndMatchRequest = {
                matchId: 'm-1',
                currentUser: 'u-1',
                players: null,
                startedAt: null,
                finishedAt: null,
                winnerUserId: null,
                metadata: null,
            };

            await expect(client.endMatch(request)).resolves.toBeUndefined();

            expect(mockConnection.invoke).toHaveBeenCalledWith('EndMatch', request);
        });

        it('propaga el error si el hub rechaza la invocación', async () => {
            mockConnection.invoke.mockRejectedValue(new Error('hub caído'));

            await expect(client.joinMatch({ matchId: 'm-1', currentUser: 'u-1', displayName: null }))
                .rejects.toThrow('hub caído');
        });
    });

    describe('suscripciones', () => {
        const subscriptions: [string, (target: SignalRClient, callback: () => void) => void][] = [
            ['playerJoined', (target, callback) => target.onPlayerJoined(callback)],
            ['playerLeft', (target, callback) => target.onPlayerLeft(callback)],
            ['playerUpdate', (target, callback) => target.onPlayerUpdate(callback)],
            ['matchEnded', (target, callback) => target.onMatchEnded(callback)],
            ['joinFailed', (target, callback) => target.onJoinFailed(callback)],
            ['updateFailed', (target, callback) => target.onUpdateFailed(callback)],
        ];

        it.each(subscriptions)('escucha el mensaje %s del hub', (eventName, subscribe) => {
            const callback = jest.fn();

            subscribe(client, callback);

            expect(mockConnection.on).toHaveBeenCalledWith(eventName, callback);
        });

        it('removeAllListeners deja de escuchar los seis mensajes', () => {
            client.removeAllListeners();

            expect(mockConnection.off.mock.calls.map(([eventName]) => eventName)).toEqual(
                subscriptions.map(([eventName]) => eventName),
            );
        });
    });

    describe('disconnect', () => {
        it('quita los listeners y cierra la conexión si estaba abierta', async () => {
            mockConnection.state = 'Connected';

            await client.disconnect();

            expect(mockConnection.off).toHaveBeenCalledTimes(6);
            expect(mockConnection.stop).toHaveBeenCalledTimes(1);
        });

        it('quita los listeners pero no cierra una conexión que ya estaba cerrada', async () => {
            await client.disconnect();

            expect(mockConnection.off).toHaveBeenCalledTimes(6);
            expect(mockConnection.stop).not.toHaveBeenCalled();
        });
    });

    describe('state', () => {
        it('refleja el estado actual de la conexión', () => {
            expect(client.state).toBe('Disconnected');

            mockConnection.state = 'Reconnecting';

            expect(client.state).toBe('Reconnecting');
        });
    });
});

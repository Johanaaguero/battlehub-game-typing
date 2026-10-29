
import {
    HubConnectionBuilder,
    HubConnectionState,
} from '@microsoft/signalr';
import { SignalRClient } from '../src/signalr-client';

jest.mock('@microsoft/signalr', () => {
    const connection = {
        state: 'Disconnected',
        start: jest.fn(),
        stop: jest.fn(),
        invoke: jest.fn(),
        on: jest.fn(),
        off: jest.fn(),
    };

    const builder = {
        withUrl: jest.fn().mockReturnThis(),
        withAutomaticReconnect: jest.fn().mockReturnThis(),
        build: jest.fn(() => connection),
    };

    return {
        HubConnectionState: {
            Disconnected: 'Disconnected',
            Connecting: 'Connecting',
            Connected: 'Connected',
        },
        HubConnectionBuilder: jest.fn(() => builder),
        __connection: connection,
        __builder: builder,
    };
});

const signalr = jest.requireMock('@microsoft/signalr') as {
    __connection: {
        state: string;
        start: jest.Mock;
        stop: jest.Mock;
        invoke: jest.Mock;
        on: jest.Mock;
        off: jest.Mock;
    };
    __builder: {
        withUrl: jest.Mock;
        withAutomaticReconnect: jest.Mock;
        build: jest.Mock;
    };
};

const connection = signalr.__connection;
const builder = signalr.__builder;

describe('SignalRClient', () => {
    let client: SignalRClient;

    beforeEach(() => {
        jest.clearAllMocks();
        connection.state = HubConnectionState.Disconnected;
        connection.start.mockResolvedValue(undefined);
        connection.stop.mockResolvedValue(undefined);
        connection.invoke.mockResolvedValue(true);
        builder.withUrl.mockReturnThis();
        builder.withAutomaticReconnect.mockReturnThis();
        builder.build.mockReturnValue(connection);
        jest.spyOn(console, 'log').mockImplementation(() => undefined);
        client = new SignalRClient('http://localhost/gameHub');
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('configura la URL y la reconexión automática', () => {
        expect(HubConnectionBuilder).toHaveBeenCalled();
        expect(builder.withUrl).toHaveBeenCalledWith(
            'http://localhost/gameHub',
            {},
        );
        expect(builder.withAutomaticReconnect).toHaveBeenCalled();
        expect(builder.build).toHaveBeenCalled();
    });

    it('agrega las credenciales de desarrollo a la URL sin parámetros previos', () => {
        new SignalRClient('http://localhost/gameHub', {
            devUser: { id: 'usuario 1', displayName: 'Ana María' },
        });

        expect(builder.withUrl).toHaveBeenCalledWith(
            'http://localhost/gameHub?dev_user=usuario%201&dev_name=Ana%20Mar%C3%ADa',
            {},
        );
    });

    it('agrega las credenciales de desarrollo usando & si ya hay parámetros', () => {
        new SignalRClient('http://localhost/gameHub?x=1', {
            devUser: { id: 'u2', displayName: 'Luis' },
        });

        expect(builder.withUrl).toHaveBeenCalledWith(
            'http://localhost/gameHub?x=1&dev_user=u2&dev_name=Luis',
            {},
        );
    });

    it('configura el proveedor de token cuando se proporciona', () => {
        const accessTokenFactory = jest.fn().mockResolvedValue('token-prueba');

        new SignalRClient('http://localhost/gameHub', { accessTokenFactory });

        expect(builder.withUrl).toHaveBeenCalledWith(
            'http://localhost/gameHub',
            { accessTokenFactory },
        );
    });

    it('inicia la conexión cuando está desconectada', async () => {
        await client.connect();

        expect(connection.start).toHaveBeenCalledTimes(1);
    });

    it('no inicia otra conexión si ya está conectado', async () => {
        connection.state = HubConnectionState.Connected;

        await client.connect();

        expect(connection.start).not.toHaveBeenCalled();
    });

    it('no inicia otra conexión si está conectándose', async () => {
        connection.state = HubConnectionState.Connecting;

        await client.connect();

        expect(connection.start).not.toHaveBeenCalled();
    });

    it('propaga el error cuando falla el inicio de conexión', async () => {
        connection.start.mockRejectedValueOnce(new Error('falló la conexión'));

        await expect(client.connect()).rejects.toThrow('falló la conexión');
    });

    it('invoca JoinMatch con la solicitud recibida', async () => {
        const request = { matchId: 'm1', userId: 'u1' } as never;

        await client.joinMatch(request);

        expect(connection.invoke).toHaveBeenCalledWith('JoinMatch', request);
    });

    it('invoca LeaveMatch con la solicitud recibida', async () => {
        const request = { matchId: 'm1', userId: 'u1' } as never;

        await client.leaveMatch(request);

        expect(connection.invoke).toHaveBeenCalledWith('LeaveMatch', request);
    });

    it('invoca SendPlayerUpdate con las métricas recibidas', async () => {
        const update = { matchId: 'm1', wpm: 50, accuracy: 95 } as never;

        await client.sendPlayerUpdate(update);

        expect(connection.invoke).toHaveBeenCalledWith('SendPlayerUpdate', update);
    });

    it('invoca EndMatch con la solicitud recibida', async () => {
        const request = { matchId: 'm1' } as never;

        await client.endMatch(request);

        expect(connection.invoke).toHaveBeenCalledWith('EndMatch', request);
    });

    it('registra los seis eventos del hub', () => {
        const callback = jest.fn();

        client.onPlayerJoined(callback);
        client.onPlayerLeft(callback);
        client.onPlayerUpdate(callback);
        client.onMatchEnded(callback);
        client.onJoinFailed(callback);
        client.onUpdateFailed(callback);

        expect(connection.on).toHaveBeenNthCalledWith(1, 'playerJoined', callback);
        expect(connection.on).toHaveBeenNthCalledWith(2, 'playerLeft', callback);
        expect(connection.on).toHaveBeenNthCalledWith(3, 'playerUpdate', callback);
        expect(connection.on).toHaveBeenNthCalledWith(4, 'matchEnded', callback);
        expect(connection.on).toHaveBeenNthCalledWith(5, 'joinFailed', callback);
        expect(connection.on).toHaveBeenNthCalledWith(6, 'updateFailed', callback);
    });

    it('elimina todos los eventos registrados', () => {
        client.removeAllListeners();

        expect(connection.off).toHaveBeenCalledTimes(6);
        expect(connection.off).toHaveBeenCalledWith('playerJoined');
        expect(connection.off).toHaveBeenCalledWith('playerLeft');
        expect(connection.off).toHaveBeenCalledWith('playerUpdate');
        expect(connection.off).toHaveBeenCalledWith('matchEnded');
        expect(connection.off).toHaveBeenCalledWith('joinFailed');
        expect(connection.off).toHaveBeenCalledWith('updateFailed');
    });

    it('cierra la conexión cuando no está desconectada', async () => {
        connection.state = HubConnectionState.Connected;

        await client.disconnect();

        expect(connection.stop).toHaveBeenCalledTimes(1);
        expect(connection.off).toHaveBeenCalledTimes(6);
    });

    it('no detiene una conexión que ya está desconectada', async () => {
        await client.disconnect();

        expect(connection.stop).not.toHaveBeenCalled();
    });

    it('devuelve el estado actual de la conexión', () => {
        connection.state = HubConnectionState.Connected;

        expect(client.state).toBe(HubConnectionState.Connected);
    });
});

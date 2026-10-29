
import { GameModule } from '../src/game-module';
import type { GameContext } from '../src/game-contracts';

type GameModuleTestAccess = {
    processMatchResult: (
        outcome: string,
        resultOrErrors: unknown
    ) => void;
};


const mockClient = {
    connect: jest.fn(),
    joinMatch: jest.fn(),
    leaveMatch: jest.fn(),
    sendPlayerUpdate: jest.fn(),
    endMatch: jest.fn(),
    disconnect: jest.fn(),
    removeAllListeners: jest.fn(),
    onPlayerJoined: jest.fn(),
    onPlayerLeft: jest.fn(),
    onPlayerUpdate: jest.fn(),
    onMatchEnded: jest.fn(),
    onJoinFailed: jest.fn(),
    onUpdateFailed: jest.fn(),
    state: 'Connected',
};

jest.mock('../src/signalr-client', () => ({
    SignalRClient: jest.fn(() => mockClient),
}));

const context: GameContext = {
    matchId: 'match-coverage-001',
    gameType: 'typing',
    currentUser: { id: 'user-coverage', displayName: 'Jugador' },
};

describe('GameModule: cobertura de historial y estadísticas', () => {
    const originalFetch = globalThis.fetch;
    let fetchMock: jest.Mock;

    beforeEach(() => {
        jest.clearAllMocks();

        mockClient.connect.mockResolvedValue(undefined);
        mockClient.joinMatch.mockResolvedValue(true);
        mockClient.leaveMatch.mockResolvedValue(true);
        mockClient.sendPlayerUpdate.mockResolvedValue(true);
        mockClient.endMatch.mockResolvedValue(undefined);
        mockClient.disconnect.mockResolvedValue(undefined);

        fetchMock = jest.fn();
        globalThis.fetch = fetchMock as unknown as typeof fetch;

        jest.spyOn(console, 'error').mockImplementation(() => undefined);
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
        jest.restoreAllMocks();
    });

    it('carga el historial correctamente', async () => {
        const history = [
            { matchId: 'match-1', gameType: 'typing' },
        ];
        fetchMock.mockResolvedValue({
            ok: true,
            json: async () => history,
        });

        const game = new GameModule();
        await game.initialize(context);
        await game.loadHistory();

        expect(game.history).toEqual(history);
        expect(game.historyLoaded).toBe(true);
        expect(game.historyError).toBe('');
        expect(game.historyLoading).toBe(false);
    });

    it('maneja un error HTTP al cargar el historial', async () => {
        fetchMock.mockResolvedValue({
            ok: false,
            status: 500,
            json: async () => ({}),
        });

        const game = new GameModule();
        await game.initialize(context);
        await game.loadHistory();

        expect(game.history).toEqual([]);
        expect(game.historyLoaded).toBe(false);
        expect(game.historyError).toContain('No fue posible cargar el historial');
        expect(game.historyLoading).toBe(false);
        expect(console.error).toHaveBeenCalled();
    });

    it('maneja un error de conexión al cargar el historial', async () => {
        fetchMock.mockRejectedValue(new Error('Sin conexión'));

        const game = new GameModule();
        await game.initialize(context);
        await game.loadHistory();

        expect(game.history).toEqual([]);
        expect(game.historyLoaded).toBe(false);
        expect(game.historyLoading).toBe(false);
        expect(game.historyError).toContain('No fue posible cargar el historial');
    });

    it('carga las estadísticas correctamente', async () => {
        const stats = {
            gamesPlayed: 5,
            wins: 3,
        };
        fetchMock.mockResolvedValue({
            ok: true,
            json: async () => stats,
        });

        const game = new GameModule();
        await game.initialize(context);
        await game.loadStats();

        expect(game.stats).toEqual(stats);
        expect(game.statsLoaded).toBe(true);
        expect(game.statsError).toBe('');
        expect(game.statsLoading).toBe(false);
    });

    it('maneja un error HTTP al cargar las estadísticas', async () => {
        fetchMock.mockResolvedValue({
            ok: false,
            status: 500,
            json: async () => ({}),
        });

        const game = new GameModule();
        await game.initialize(context);
        await game.loadStats();

        expect(game.stats).toBeNull();
        expect(game.statsLoaded).toBe(false);
        expect(game.statsError).toContain('No fue posible cargar las estadísticas');
        expect(game.statsLoading).toBe(false);
        expect(console.error).toHaveBeenCalled();
    });

    it('maneja un error de conexión al cargar las estadísticas', async () => {
        fetchMock.mockRejectedValue(new Error('Sin conexión'));

        const game = new GameModule();
        await game.initialize(context);
        await game.loadStats();

        expect(game.stats).toBeNull();
        expect(game.statsLoaded).toBe(false);
        expect(game.statsLoading).toBe(false);
        expect(game.statsError).toContain('No fue posible cargar las estadísticas');
    });

    it('codifica el identificador del jugador en las consultas', async () => {
        fetchMock.mockResolvedValue({
            ok: true,
            json: async () => [],
        });

        const game = new GameModule();
        await game.initialize({
            ...context,
            currentUser: { id: 'user/ana 1', displayName: 'Ana' },
        });

        await game.loadHistory();

        expect(fetchMock).toHaveBeenCalledWith(
            expect.stringContaining('/players/user%2Fana%201/history'),
            expect.any(Object),
        );
    });

    it('recarga el historial y restablece historyLoaded', async () => {
        const game = new GameModule();
        await game.initialize(context);

        game.historyLoaded = true;
        const loadHistorySpy = jest
            .spyOn(game, 'loadHistory')
            .mockResolvedValue(undefined);

        await game.refreshHistory();

        expect(game.historyLoaded).toBe(false);
        expect(loadHistorySpy).toHaveBeenCalledTimes(1);
    });

    it('recarga las estadísticas y restablece statsLoaded', async () => {
        const game = new GameModule();
        await game.initialize(context);

        game.statsLoaded = true;
        const loadStatsSpy = jest
            .spyOn(game, 'loadStats')
            .mockResolvedValue(undefined);

        await game.refreshStats();

        expect(game.statsLoaded).toBe(false);
        expect(loadStatsSpy).toHaveBeenCalledTimes(1);
    });

});

describe('GameModule: formato de datos y métricas', () => {
    let game: GameModule;

    beforeEach(() => {
        game = new GameModule();
    });

    it('devuelve Sin datos cuando la fecha es null', () => {
        expect(game.formatDate(null)).toBe('Sin datos');
    });

    it('devuelve Sin datos cuando la fecha está vacía', () => {
        expect(game.formatDate('')).toBe('Sin datos');
    });

    it('devuelve Sin datos cuando la fecha no es válida', () => {
        expect(game.formatDate('fecha-invalida')).toBe('Sin datos');
    });

    it('formatea una fecha ISO válida', () => {
        const result = game.formatDate('2026-10-28T12:00:00Z');

        expect(result).not.toBe('Sin datos');
        expect(result.length).toBeGreaterThan(0);
    });

    it('devuelve un guion largo cuando la métrica es null', () => {
        expect(game.formatMetric(null)).toBe('—');
    });

    it('formatea métricas numéricas con un decimal', () => {
        expect(game.formatMetric(12)).toBe('12.0');
        expect(game.formatMetric(12.345)).toBe('12.3');
        expect(game.formatMetric(0)).toBe('0.0');
    });

    it('devuelve cero por ciento de victorias cuando no hay estadísticas', () => {
        expect(game.winRatePercentage).toBe(0);
    });

    it('convierte la tasa de victorias a porcentaje con un decimal', () => {
        game.stats = {
            gamesPlayed: 10,
            wins: 7,
            winRate: 0.75,
        } as NonNullable<typeof game.stats>;

        expect(game.winRatePercentage).toBe(75);
    });

    it('redondea el porcentaje de victorias a un decimal', () => {
        game.stats = {
            gamesPlayed: 10,
            wins: 7,
            winRate: 0.756,
        } as NonNullable<typeof game.stats>;

        expect(game.winRatePercentage).toBe(75.6);
    });
});


describe('GameModule result handling coverage', () => {
    let game: GameModule;

    beforeEach(() => {
        jest.clearAllMocks();
        game = new GameModule();
    });

    test('formatDate returns Sin datos for an empty value', () => {
        expect(game.formatDate('')).toBe('Sin datos');
        expect(game.formatDate(null)).toBe('Sin datos');
    });

    test('formatDate returns Sin datos for an invalid date', () => {
        expect(game.formatDate('not-a-date')).toBe('Sin datos');
    });

    test('formatMetric formats a number with one decimal', () => {
        expect(game.formatMetric(12)).toBe('12.0');
        expect(game.formatMetric(12.34)).toBe('12.3');
    });

    test('formatMetric returns an em dash for null', () => {
        expect(game.formatMetric(null)).toBe('—');
    });
});




describe('GameModule match result handling', () => {
    let game: GameModule;

    beforeEach(() => {
        game = new GameModule();
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    test('handles an already registered result', () => {
        (game as unknown as GameModuleTestAccess).processMatchResult('already_exists', null);

        expect(game.resultMessage).toContain('ya había sido registrado');
    });

    test('handles an invalid result', () => {
        const errorSpy = jest
            .spyOn(console, 'error')
            .mockImplementation(() => undefined);

        const errors = { players: ['Invalid player'] };

        (game as unknown as GameModuleTestAccess).processMatchResult('invalid', errors);

        expect(game.resultMessage).toContain('datos inválidos');
        expect(errorSpy).toHaveBeenCalled();
    });

    test('handles an error while saving the result', () => {
        const errorSpy = jest
            .spyOn(console, 'error')
            .mockImplementation(() => undefined);

        (game as unknown as GameModuleTestAccess).processMatchResult('error', new Error('Network error'));

        expect(game.resultMessage).toContain('error al finalizar');
        expect(errorSpy).toHaveBeenCalled();
    });

    test('handles an unknown result outcome', () => {
        const warnSpy = jest
            .spyOn(console, 'warn')
            .mockImplementation(() => undefined);

        (game as unknown as GameModuleTestAccess).processMatchResult('unexpected', null);

        expect(game.resultMessage).toBe('La partida ha finalizado.');
        expect(warnSpy).toHaveBeenCalled();
    });

    test('handles a created outcome with invalid result data', () => {
        (game as unknown as GameModuleTestAccess).processMatchResult('created', { unexpected: true });

        expect(game.resultMessage).toContain(
            'no fue posible leer el resultado'
        );
        expect(game.finalResult).toBeNull();
    });


    test.each([
        ['null', null],
        ['a string', 'resultado'],
        ['a number', 123],
        ['an array', []],
        ['missing matchId', {
            gameType: 'typing',
            players: [],
        }],
        ['non-string matchId', {
            matchId: 123,
            gameType: 'typing',
            players: [],
        }],
        ['non-string gameType', {
            matchId: 'match-1',
            gameType: 123,
            players: [],
        }],
        ['non-array players', {
            matchId: 'match-1',
            gameType: 'typing',
            players: {},
        }],
    ])('rejects invalid result data: %s', (_description, result) => {
        (game as unknown as GameModuleTestAccess)
            .processMatchResult('created', result);

        expect(game.resultMessage).toContain(
            'no fue posible leer el resultado'
        );
        expect(game.finalResult).toBeNull();
    });


    test('handles a created outcome with a valid result', () => {
        const result = {
            matchId: 'match-1',
            gameType: 'typing',
            players: [
                {
                    userId: 'user-1',
                    displayName: 'Ana',
                    score: 120,
                },
            ],
            startedAt: '2026-10-28T10:00:00Z',
            finishedAt: '2026-10-28T10:01:00Z',
            winnerUserId: 'user-1',
            metadata: {},
        };

        game.players = [
            {
                userId: 'user-1',
                displayName: 'Jugador',
                score: 0,
                wpm: null,
                accuracy: null,
            },
        ];

        (game as unknown as GameModuleTestAccess).processMatchResult('created', result);

        expect(game.finalResult).toEqual(result);
        expect(game.resultMessage).toBe(
            'Partida finalizada correctamente.'
        );
        expect(game.players[0].displayName).toBe('Ana');
        expect(game.players[0].score).toBe(120);
    });
});




describe('GameModule winner and player updates', () => {
    const createGame = () => {
        const game = new GameModule();
        return game;
    };

    it('returns false when there is no final result', () => {
        const game = createGame();
        expect(game.isWinner('user-1')).toBe(false);
    });

    it('identifies the winner', () => {
        const game = createGame();

        game.finalResult = {
            matchId: 'match-1',
            gameType: 'typing',
            players: [
                { userId: 'user-1', displayName: 'Ana', score: 10 },
            ],
            startedAt: '',
            finishedAt: '',
            winnerUserId: 'user-1',
            metadata: {},
        };

        expect(game.isWinner('user-1')).toBe(true);
        expect(game.isWinner('user-2')).toBe(false);
    });

    it('returns an empty winner name when there is no winner', () => {
        const game = createGame();

        game.finalResult = {
            matchId: 'match-1',
            gameType: 'typing',
            players: [],
            startedAt: '',
            finishedAt: '',
            winnerUserId: null,
            metadata: {},
        };

        expect(game.winnerName).toBe('');
    });

    it('returns the winner display name', () => {
        const game = createGame();

        game.finalResult = {
            matchId: 'match-1',
            gameType: 'typing',
            players: [
                { userId: 'user-1', displayName: 'Ana', score: 10 },
            ],
            startedAt: '',
            finishedAt: '',
            winnerUserId: 'user-1',
            metadata: {},
        };

        expect(game.winnerName).toBe('Ana');
    });

    it('falls back to the winner user ID if the player is not found', () => {
        const game = createGame();

        game.finalResult = {
            matchId: 'match-1',
            gameType: 'typing',
            players: [],
            startedAt: '',
            finishedAt: '',
            winnerUserId: 'user-2',
            metadata: {},
        };

        expect(game.winnerName).toBe('user-2');
    });

    it('updates an existing player from the final result', () => {
        const game = createGame();
        game.players = [
            {
                userId: 'user-1',
                displayName: 'Ana',
                score: 10,
                wpm: null,
                accuracy: null,
            },
        ];

        (game as unknown as {
            updatePlayersFromFinalResult: (result: {
                players: {
                    userId: string;
                    displayName: string;
                    score: number;
                }[];
            }) => void;
        }).updatePlayersFromFinalResult({
            players: [
                { userId: 'user-1', displayName: 'Ana María', score: 25 },
            ],
        });

        expect(game.players[0].displayName).toBe('Ana María');
        expect(game.players[0].score).toBe(25);
    });

    it('adds a player who was not already in the list', () => {
        const game = createGame();
        game.players = [];

        (game as unknown as {
            updatePlayersFromFinalResult: (result: {
                players: {
                    userId: string;
                    displayName: string;
                    score: number;
                }[];
            }) => void;
        }).updatePlayersFromFinalResult({
            players: [
                { userId: 'user-2', displayName: 'Luis', score: 18 },
            ],
        });

        expect(game.players).toHaveLength(1);
        expect(game.players[0]).toEqual({
            userId: 'user-2',
            displayName: 'Luis',
            score: 18,
            wpm: null,
            accuracy: null,
        });
    });
});



describe('GameModule remote match operations', () => {
    type RemoteGameTestAccess = {
        context: GameContext | null;
        isPracticeMode: boolean;
        running: boolean;
        signalR: {
            sendPlayerUpdate: jest.Mock;
            endMatch: jest.Mock;
        };
        stopTimer: jest.Mock;
    };

    let game: RemoteGameTestAccess & {
        sendPlayerUpdate: GameModule['sendPlayerUpdate'];
        endMatch: GameModule['endMatch'];
    };

    beforeEach(() => {
        const instance = Object.create(GameModule.prototype) as GameModule;

        game = Object.assign(instance, {
            context: {
                matchId: 'match-1',
                gameType: 'typing',
                currentUser: {
                    id: 'user-1',
                    displayName: 'Ana',
                },
            },
            isPracticeMode: false,
            running: true,
            stopTimer: jest.fn(),
            signalR: {
                sendPlayerUpdate: jest.fn().mockResolvedValue(true),
                endMatch: jest.fn().mockResolvedValue(undefined),
            },
        }) as RemoteGameTestAccess & {
            sendPlayerUpdate: GameModule['sendPlayerUpdate'];
            endMatch: GameModule['endMatch'];
        };
    });

    it('rejects player updates without an integrated match', async () => {
        game.context = null;

        await expect(
            game.sendPlayerUpdate(100, 40, 98),
        ).rejects.toThrow(
            'Las estadísticas remotas requieren una partida integrada',
        );
    });

    it('rejects player updates in practice mode', async () => {
        game.isPracticeMode = true;

        await expect(
            game.sendPlayerUpdate(100, 40, 98),
        ).rejects.toThrow(
            'Las estadísticas remotas requieren una partida integrada',
        );
    });

    it('sends player statistics to SignalR', async () => {
        await expect(
            game.sendPlayerUpdate(100, 40, 98),
        ).resolves.toBe(true);

        expect(game.signalR.sendPlayerUpdate).toHaveBeenCalledWith(
            expect.objectContaining({
                matchId: 'match-1',
                currentUser: 'user-1',
                score: 100,
                wpm: 40,
                accuracy: 98,
                timestamp: expect.any(String),
            }),
        );
    });

    it('rejects ending a match without an integrated match', async () => {
        game.context = null;

        await expect(
            game.endMatch([], null, null, null, null),
        ).rejects.toThrow(
            'Finalizar en el servidor requiere una partida integrada',
        );
    });

    it('rejects ending a match in practice mode', async () => {
        game.isPracticeMode = true;

        await expect(
            game.endMatch([], null, null, null, null),
        ).rejects.toThrow(
            'Finalizar en el servidor requiere una partida integrada',
        );
    });

    it('sends the final result and stops the timer', async () => {
        const players = [
            { userId: 'user-1', displayName: 'Ana', score: 100 },
        ];

        await game.endMatch(
            players,
            '2026-10-28T20:00:00.000Z',
            '2026-10-28T20:01:00.000Z',
            'user-1',
            { source: 'test' },
        );

        expect(game.signalR.endMatch).toHaveBeenCalledWith({
            matchId: 'match-1',
            currentUser: 'user-1',
            players,
            startedAt: '2026-10-28T20:00:00.000Z',
            finishedAt: '2026-10-28T20:01:00.000Z',
            winnerUserId: 'user-1',
            metadata: { source: 'test' },
        });

        expect(game.running).toBe(false);
        expect(game.stopTimer).toHaveBeenCalledTimes(1);
    });

    it('does not stop the timer if SignalR fails to end the match', async () => {
        game.signalR.endMatch.mockRejectedValue(
            new Error('Connection failed'),
        );

        await expect(
            game.endMatch([], null, null, null, null),
        ).rejects.toThrow('Connection failed');

        expect(game.stopTimer).not.toHaveBeenCalled();
    });
});


describe('GameModule: ramas pendientes de cobertura', () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        jest.clearAllMocks();
        globalThis.fetch = jest.fn().mockResolvedValue({
            ok: true,
            json: async () => [],
        }) as unknown as typeof fetch;
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
    });

    it('no carga el historial si no existe contexto', async () => {
        const game = new GameModule();

        await game.loadHistory();

        expect(game.historyLoading).toBe(false);
        expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('no carga estadísticas si no existe contexto', async () => {
        const game = new GameModule();

        await game.loadStats();

        expect(game.statsLoading).toBe(false);
        expect(globalThis.fetch).not.toHaveBeenCalled();
    });

    it('usa encabezados vacíos cuando el token está vacío', async () => {
        const game = new GameModule();

        await game.initialize({
            ...context,
            getAccessToken: async () => '',
        } as GameContext & {
            getAccessToken: () => Promise<string>;
        });

        await game.loadHistory();

        expect(globalThis.fetch).toHaveBeenCalledWith(
            expect.stringContaining('/history'),
            expect.objectContaining({
                headers: {},
            }),
        );
    });

    it('no actualiza al jugador actual si no hay contexto', () => {
        const game = new GameModule();

        (
            game as unknown as {
                updateCurrentPlayerInterface: () => void;
            }
        ).updateCurrentPlayerInterface();

        expect(game.players).toEqual([]);
    });

    it('no actualiza al jugador si no aparece en la lista', async () => {
        const game = new GameModule();

        await game.initialize(context);
        game.players = [];

        (
            game as unknown as {
                updateCurrentPlayerInterface: () => void;
            }
        ).updateCurrentPlayerInterface();

        expect(game.players).toEqual([]);
    });
});

describe('GameModule: navegación y eventos', () => {
    beforeEach(() => {
        jest.clearAllMocks();

        mockClient.connect.mockResolvedValue(undefined);
        mockClient.joinMatch.mockResolvedValue(true);
        mockClient.sendPlayerUpdate.mockResolvedValue(true);
        mockClient.endMatch.mockResolvedValue(undefined);
        mockClient.disconnect.mockResolvedValue(undefined);
    });

    it('cambia a historial y lo carga', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const loadSpy = jest
            .spyOn(game, 'loadHistory')
            .mockResolvedValue(undefined);

        await game.showHistory();

        expect(game.activeSection).toBe('history');
        expect(loadSpy).toHaveBeenCalledTimes(1);
    });

    it('cambia a estadísticas y las carga', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const loadSpy = jest
            .spyOn(game, 'loadStats')
            .mockResolvedValue(undefined);

        await game.showStats();

        expect(game.activeSection).toBe('stats');
        expect(loadSpy).toHaveBeenCalledTimes(1);
    });

    it('vuelve a mostrar el juego sin consultar el backend', async () => {
        const game = new GameModule();
        await game.initialize(context);

        game.activeSection = 'history';
        game.showGame();

        expect(game.activeSection).toBe('game');
    });

    it('maneja la llegada de un jugador nuevo por SignalR', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const callback = mockClient.onPlayerJoined.mock.calls[0][0];
        callback({ userId: 'user-nuevo' });

        expect(game.players.some(
            player => player.userId === 'user-nuevo'
        )).toBe(true);
    });
});

describe('GameModule: cobertura de eventos de jugadores', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockClient.state = 'Connected';
        mockClient.connect.mockResolvedValue(undefined);
        mockClient.joinMatch.mockResolvedValue(true);
        mockClient.sendPlayerUpdate.mockResolvedValue(true);
        mockClient.endMatch.mockResolvedValue(undefined);
        mockClient.disconnect.mockResolvedValue(undefined);
    });

    it('elimina un jugador cuando recibe el evento de salida', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const callback = mockClient.onPlayerLeft.mock.calls[0][0];
        callback({ userId: 'user-coverage' });

        expect(game.players).toHaveLength(0);
    });

    it('actualiza los datos de un jugador existente', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const callback = mockClient.onPlayerUpdate.mock.calls[0][0];
        callback({
            userId: 'user-coverage',
            score: 25,
            wpm: 40,
            accuracy: 95,
        });

        expect(game.players[0]).toMatchObject({
            userId: 'user-coverage',
            score: 25,
            wpm: 40,
            accuracy: 95,
        });
    });

    it('agrega un jugador cuando recibe una actualización de uno desconocido', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const callback = mockClient.onPlayerUpdate.mock.calls[0][0];
        callback({
            userId: 'user-nuevo',
            score: 10,
            wpm: 20,
            accuracy: 90,
        });

        expect(game.players).toHaveLength(2);
        expect(game.players[1]).toMatchObject({
            userId: 'user-nuevo',
            score: 10,
            wpm: 20,
            accuracy: 90,
        });
    });

    it('registra los eventos de error de SignalR', async () => {
        const game = new GameModule();
        await game.initialize(context);

        const error = new Error('Error de conexión');
        const consoleError = jest
            .spyOn(console, 'error')
            .mockImplementation(() => { });

        mockClient.onJoinFailed.mock.calls[0][0](error);
        mockClient.onUpdateFailed.mock.calls[0][0](error);

        expect(consoleError).toHaveBeenCalledTimes(2);

        consoleError.mockRestore();
    });
});
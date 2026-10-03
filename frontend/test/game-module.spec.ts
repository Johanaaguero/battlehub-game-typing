import { createFixture } from '@aurelia/testing';
import { GameModule } from '../src/game-module';
import type { GameContext } from '../src/game-contracts';
import { SignalRClient } from '../src/signalr-client';
import template from '../src/game-module.html';

// Cliente SignalR simulado: las pruebas no abren conexiones reales.
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
  matchId: 'match-test-001',
  gameType: 'typing',
  currentUser: { id: 'user-ana', displayName: 'Ana' },
};

function resetClient(): void {
  jest.clearAllMocks();
  mockClient.state = 'Connected';
  mockClient.connect.mockResolvedValue(undefined);
  mockClient.joinMatch.mockResolvedValue(true);
  mockClient.leaveMatch.mockResolvedValue(true);
  mockClient.sendPlayerUpdate.mockResolvedValue(true);
  mockClient.endMatch.mockResolvedValue(undefined);
  mockClient.disconnect.mockResolvedValue(undefined);
}

describe('GameModule (ciclo de vida)', () => {
  beforeEach(() => {
    resetClient();
    jest.useFakeTimers();
    jest.spyOn(console, 'log').mockImplementation(() => undefined);
  });

  afterEach(() => {
    jest.useRealTimers();
    jest.restoreAllMocks();
  });

  async function startGame(): Promise<GameModule> {
    const game = new GameModule();
    await game.initialize(context);
    await game.start();
    return game;
  }

  it('rechaza start() si no puede unirse a la partida (el Shell lo muestra como LIFECYCLE_ERROR)', async () => {
    mockClient.joinMatch.mockResolvedValue(false);
    const game = new GameModule();
    await game.initialize(context);

    await expect(game.start()).rejects.toThrow('No fue posible unirse a la partida');
  });

  it('no deja escribir antes de start()', async () => {
    const game = new GameModule();
    await game.initialize(context);

    game.typedText = 'La';
    game.handleTyping();

    expect(game.canType).toBe(false);
    expect(game.progress).toBe(0);
    expect(mockClient.sendPlayerUpdate).not.toHaveBeenCalled();
  });

  it('el temporizador arranca con start(), sin esperar la primera tecla', async () => {
    const game = await startGame();

    await jest.advanceTimersByTimeAsync(3_000);

    expect(game.timeRemaining).toBe(57);
  });

  it('al acabarse el tiempo envía las métricas finales y pide terminar la partida una sola vez', async () => {
    const game = await startGame();
    game.typedText = 'La tec';
    game.handleTyping();

    await jest.advanceTimersByTimeAsync(60_000);

    expect(game.isTimeUp).toBe(true);
    expect(mockClient.endMatch).toHaveBeenCalledTimes(1);
    expect(mockClient.endMatch).toHaveBeenCalledWith(expect.objectContaining({
      matchId: context.matchId,
      currentUser: context.currentUser.id,
      players: null,
      startedAt: null,
      finishedAt: null,
      winnerUserId: null,
    }));

    const updates = mockClient.sendPlayerUpdate.mock.invocationCallOrder;
    expect(updates[updates.length - 1]).toBeLessThan(mockClient.endMatch.mock.invocationCallOrder[0]);

    await jest.advanceTimersByTimeAsync(5_000);
    expect(mockClient.endMatch).toHaveBeenCalledTimes(1);
  });

  it('completar el texto congela la velocidad y bloquea la escritura hasta el final', async () => {
    const game = await startGame();
    await jest.advanceTimersByTimeAsync(10_000);

    game.typedText = game.targetText;
    game.handleTyping();
    const wpmAtCompletion = game.wpm;

    expect(game.completed).toBe(true);
    expect(game.canType).toBe(false);
    expect(wpmAtCompletion).toBeGreaterThan(0);

    await jest.advanceTimersByTimeAsync(20_000);

    expect(game.wpm).toBe(wpmAtCompletion);
    expect(mockClient.endMatch).not.toHaveBeenCalled();
  });

  it('pause() detiene el temporizador y start() lo reanuda donde quedó', async () => {
    const game = await startGame();
    await jest.advanceTimersByTimeAsync(5_000);

    await game.pause();
    await jest.advanceTimersByTimeAsync(10_000);
    expect(game.timeRemaining).toBe(55);

    await game.start();
    await jest.advanceTimersByTimeAsync(5_000);
    expect(game.timeRemaining).toBe(50);
  });

  it('conserva el resultado si después le llega already_exists', async () => {
    const game = await startGame();
    const onMatchEnded = mockClient.onMatchEnded.mock.calls[0][0];
    const result = {
      matchId: context.matchId,
      gameType: 'typing',
      players: [{ userId: 'user-ana', displayName: 'Ana', score: 600 }],
      startedAt: '2026-10-03T20:00:00Z',
      finishedAt: '2026-10-03T20:01:00Z',
      winnerUserId: 'user-ana',
      metadata: {},
    };

    onMatchEnded({ matchId: context.matchId, outcome: 'created', resultOrErrors: result });
    onMatchEnded({ matchId: context.matchId, outcome: 'already_exists', resultOrErrors: { matchId: context.matchId } });

    expect(game.matchFinished).toBe(true);
    expect(game.finalResult).toEqual(result);
    expect(game.resultMessage).toBe('Partida finalizada correctamente.');
  });
});

describe('GameModule (plantilla)', () => {
  beforeEach(() => {
    resetClient();
    jest.spyOn(console, 'log').mockImplementation(() => undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('el componente trae su plantilla: el Shell la dibuja aunque aún no haya contexto', async () => {
    const { appHost } = await createFixture(
      '<typing-game-module></typing-game-module>',
      {},
      [GameModule],
    ).started;

    expect(appHost.textContent).toContain('Competitive Typing Arena');
    expect(appHost.textContent).toContain('Esperando información de la partida');
  });

  it('el área de escritura no trae texto inicial (la sangría del HTML desalinearía todo lo escrito)', () => {
    expect(template).toMatch(/<textarea[^>]*><\/textarea>/);
  });
});

describe('GameModule (autenticación y URL de la API)', () => {
  const realFetch = globalThis.fetch;
  let fetchMock: jest.Mock;

  beforeEach(() => {
    resetClient();
    jest.spyOn(console, 'log').mockImplementation(() => undefined);
    fetchMock = jest.fn().mockResolvedValue({ ok: true, json: async () => [] });
    globalThis.fetch = fetchMock as unknown as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = realFetch;
    jest.restoreAllMocks();
  });

  function lastHubClientArgs(): [string, { accessTokenFactory?: () => Promise<string>; devUser?: unknown }] {
    const calls = (SignalRClient as unknown as jest.Mock).mock.calls;
    return calls[calls.length - 1];
  }

  it('sin token del Shell usa la identidad de desarrollo en el hub y en la API', async () => {
    const game = new GameModule();
    await game.initialize(context);
    await game.loadStats();

    const [hubUrl, auth] = lastHubClientArgs();
    expect(hubUrl).toBe('http://localhost:5015/hubs/typing');
    expect(auth).toEqual({ devUser: { id: 'user-ana', displayName: 'Ana' } });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://localhost:5015/api/games/typing/players/user-ana/stats');
    expect(init.headers).toEqual({ 'X-Dev-User': 'user-ana', 'X-Dev-Name': 'Ana' });
  });

  it('con getAccessToken del Shell usa el token de Auth0 en el hub y en la API', async () => {
    const withToken = { ...context, getAccessToken: async () => 'token-de-ana' };
    const game = new GameModule();
    await game.initialize(withToken);
    await game.loadHistory();

    const [, auth] = lastHubClientArgs();
    expect(auth.devUser).toBeUndefined();
    await expect(auth.accessTokenFactory?.()).resolves.toBe('token-de-ana');

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers).toEqual({ Authorization: 'Bearer token-de-ana' });
  });

  it('codifica la identidad de desarrollo: los encabezados HTTP no admiten tildes', async () => {
    const game = new GameModule();
    await game.initialize({ ...context, currentUser: { id: 'user-jose', displayName: 'José Núñez' } });
    await game.loadHistory();

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers['X-Dev-Name']).toBe('Jos%C3%A9%20N%C3%BA%C3%B1ez');
  });
});

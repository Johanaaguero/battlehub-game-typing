import { GameModule } from '../src/game-module';
import {
  GAME_FINISHED_EVENT,
  type GameContext,
  type GameFinishedDetail,
  type GameLaunchContext,
  type PracticeContext,
} from '../src/game-contracts';

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

const currentUser = { id: 'user-ana', displayName: 'Ana' };

function matchContext(): GameContext {
  return { matchId: 'match-001', gameType: 'typing', currentUser, events: new EventTarget() };
}

function practiceContext(): PracticeContext {
  return { mode: 'practice', gameType: 'typing', currentUser, events: new EventTarget() };
}

const result = {
  matchId: 'match-001',
  gameType: 'typing',
  players: [{ userId: 'user-ana', displayName: 'Ana', score: 600 }],
  startedAt: '2026-10-03T20:00:00Z',
  finishedAt: '2026-10-03T20:01:00Z',
  winnerUserId: 'user-ana',
  metadata: {},
};

beforeEach(() => {
  jest.clearAllMocks();
  jest.useFakeTimers();
  jest.spyOn(console, 'log').mockImplementation(() => undefined);
  mockClient.state = 'Connected';
  mockClient.connect.mockResolvedValue(undefined);
  mockClient.joinMatch.mockResolvedValue(true);
  mockClient.leaveMatch.mockResolvedValue(true);
  mockClient.sendPlayerUpdate.mockResolvedValue(true);
  mockClient.endMatch.mockResolvedValue(undefined);
  mockClient.disconnect.mockResolvedValue(undefined);
});

afterEach(() => {
  jest.useRealTimers();
  jest.restoreAllMocks();
});

describe('contrato del Shell: contexto de lanzamiento', () => {
  it.each<[string, unknown, string]>([
    ['otro gameType', { ...matchContext(), gameType: 'memory' }, 'gameType debe ser typing'],
    ['sin usuario', { ...matchContext(), currentUser: undefined }, 'currentUser.id es requerido'],
    ['sin canal de eventos', { ...matchContext(), events: undefined }, 'events es requerido'],
    ['práctica con matchId', { ...practiceContext(), matchId: 'match-001' }, 'Una práctica no lleva matchId'],
  ])('rechaza el contexto: %s', async (_caso, invalid, message) => {
    const game = new GameModule();

    await expect(game.initialize(invalid as GameLaunchContext)).rejects.toThrow(message);
  });

  it('sin mode exige matchId: no cae a la práctica ni inventa una sala', async () => {
    const game = new GameModule();
    const { gameType, events } = matchContext();

    await expect(game.initialize({ gameType, currentUser, events } as GameLaunchContext))
      .rejects.toThrow('matchId es requerido para una partida normal');

    expect(game.isPracticeMode).toBe(false);
    expect(game.context).toBeNull();
  });

  it('una partida normal con matchId no es práctica', async () => {
    const game = new GameModule();

    await game.initialize(matchContext());

    expect(game.isPracticeMode).toBe(false);
  });
});

describe('contrato del Shell: práctica sin conexiones ni registros', () => {
  const realFetch = globalThis.fetch;
  let fetchMock: jest.Mock;

  beforeEach(() => {
    fetchMock = jest.fn();
    globalThis.fetch = fetchMock as unknown as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = realFetch;
  });

  it('juega hasta el final sin SignalR, sin llamadas a la API y sin avisar el fin', async () => {
    const context = practiceContext();
    const finished = jest.fn();
    context.events.addEventListener(GAME_FINISHED_EVENT, finished);
    const game = new GameModule();

    await game.initialize(context);
    await game.start();
    await jest.advanceTimersByTimeAsync(2_000);
    game.typedText = game.targetText;
    game.handleTyping();

    expect(game.matchFinished).toBe(true);
    expect(game.progress).toBe(100);
    expect(mockClient.onMatchEnded).not.toHaveBeenCalled();
    expect(mockClient.connect).not.toHaveBeenCalled();
    expect(mockClient.joinMatch).not.toHaveBeenCalled();
    expect(mockClient.sendPlayerUpdate).not.toHaveBeenCalled();
    expect(mockClient.endMatch).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
    expect(finished).not.toHaveBeenCalled();
  });

  it('no consulta historial ni estadísticas del servidor', async () => {
    const game = new GameModule();
    await game.initialize(practiceContext());

    await game.showHistory();
    await game.showStats();
    await game.refreshHistory();
    await game.refreshStats();

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('pausa, reanuda y reinicia sin duplicar temporizadores', async () => {
    const game = new GameModule();
    await game.initialize(practiceContext());
    await game.start();

    await game.pause();
    expect(jest.getTimerCount()).toBe(0);

    await game.start();
    await game.start();
    expect(jest.getTimerCount()).toBe(1);
    await jest.advanceTimersByTimeAsync(1_000);
    expect(game.timeRemaining).toBe(59);

    game.typedText = game.targetText;
    game.handleTyping();
    expect(jest.getTimerCount()).toBe(0);

    game.restartPractice();
    expect(jest.getTimerCount()).toBe(1);
    expect(game.canType).toBe(true);

    await game.dispose();
    expect(jest.getTimerCount()).toBe(0);
  });
});

describe('contrato del Shell: fin de una partida normal', () => {
  async function startMatch(context: GameContext): Promise<{
    game: GameModule;
    onMatchEnded: (notification: unknown) => void;
  }> {
    const game = new GameModule();
    await game.initialize(context);
    await game.start();
    return { game, onMatchEnded: mockClient.onMatchEnded.mock.calls[0][0] };
  }

  it('avisa una vez por context.events, con solo matchId y gameType, y no por window', async () => {
    const context = matchContext();
    const details: GameFinishedDetail[] = [];
    const onWindow = jest.fn();
    context.events.addEventListener(GAME_FINISHED_EVENT, (event) => {
      details.push((event as CustomEvent<GameFinishedDetail>).detail);
    });
    window.addEventListener(GAME_FINISHED_EVENT, onWindow);
    const { onMatchEnded } = await startMatch(context);

    onMatchEnded({ matchId: 'match-001', outcome: 'created', resultOrErrors: result });
    onMatchEnded({ matchId: 'match-001', outcome: 'created', resultOrErrors: result });
    onMatchEnded({ matchId: 'match-001', outcome: 'already_exists', resultOrErrors: { matchId: 'match-001' } });

    window.removeEventListener(GAME_FINISHED_EVENT, onWindow);
    expect(details).toEqual([{ matchId: 'match-001', gameType: 'typing' }]);
    expect(onWindow).not.toHaveBeenCalled();
  });

  it('un aviso tardío de otra partida no termina la actual', async () => {
    const context = matchContext();
    const finished = jest.fn();
    context.events.addEventListener(GAME_FINISHED_EVENT, finished);
    const { game, onMatchEnded } = await startMatch(context);

    onMatchEnded({ matchId: 'otra-partida', outcome: 'created', resultOrErrors: { ...result, matchId: 'otra-partida' } });

    expect(game.matchFinished).toBe(false);
    expect(game.canType).toBe(true);
    expect(finished).not.toHaveBeenCalled();
  });

  it('salir antes de terminar no avisa el fin, ni tampoco un aviso que llegue después', async () => {
    const context = matchContext();
    const finished = jest.fn();
    context.events.addEventListener(GAME_FINISHED_EVENT, finished);
    const { game, onMatchEnded } = await startMatch(context);

    await game.dispose();
    onMatchEnded({ matchId: 'match-001', outcome: 'created', resultOrErrors: result });

    expect(mockClient.endMatch).not.toHaveBeenCalled();
    expect(mockClient.disconnect).toHaveBeenCalledTimes(1);
    expect(finished).not.toHaveBeenCalled();
  });
});

describe('contrato del Shell: ciclo de vida de una partida normal', () => {
  it('reanudar no vuelve a conectar ni a unirse, y el tiempo no corre doble', async () => {
    const game = new GameModule();
    await game.initialize(matchContext());
    await game.start();

    await game.pause();
    await game.start();
    await game.start();

    expect(mockClient.connect).toHaveBeenCalledTimes(1);
    expect(mockClient.joinMatch).toHaveBeenCalledTimes(1);
    expect(jest.getTimerCount()).toBe(1);
    await jest.advanceTimersByTimeAsync(1_000);
    expect(game.timeRemaining).toBe(59);
  });

  it('al reanudar vuelve a entrar si la conexión se perdió', async () => {
    const game = new GameModule();
    await game.initialize(matchContext());
    await game.start();
    await game.pause();

    mockClient.state = 'Disconnected';
    await game.start();

    expect(mockClient.connect).toHaveBeenCalledTimes(2);
    expect(mockClient.joinMatch).toHaveBeenCalledTimes(2);
  });

  it('dispose() durante el inicio no deja un temporizador corriendo', async () => {
    let resolveJoin: (joined: boolean) => void = () => undefined;
    mockClient.joinMatch.mockReturnValue(new Promise<boolean>((resolve) => {
      resolveJoin = resolve;
    }));
    const game = new GameModule();
    await game.initialize(matchContext());

    const starting = game.start();
    await jest.advanceTimersByTimeAsync(0);
    await game.dispose();
    resolveJoin(true);
    await starting;

    expect(jest.getTimerCount()).toBe(0);
    expect(game.canType).toBe(false);
  });

  it('dispose() detiene el tiempo y cierra la conexión propia', async () => {
    const game = new GameModule();
    await game.initialize(matchContext());
    await game.start();

    await game.dispose();

    expect(jest.getTimerCount()).toBe(0);
    expect(mockClient.leaveMatch).toHaveBeenCalledTimes(1);
    expect(mockClient.disconnect).toHaveBeenCalledTimes(1);
    expect(game.context).toBeNull();
  });
});

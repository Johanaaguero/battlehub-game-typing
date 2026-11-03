import { GameModule } from '../src/game-module';
import { GAME_FINISHED_EVENT, type GameContext, type GameFinishedDetail } from '../src/game-contracts';
import { mockClient, registeredHandler, resetClient } from './helpers/signalr-mock';

jest.mock('../src/signalr-client', () => ({
  SignalRClient: jest.fn(
    () => jest.requireActual<typeof import('./helpers/signalr-mock')>('./helpers/signalr-mock').mockClient,
  ),
}));

interface PlayerNotification { userId: string }
interface UpdateNotification { userId: string; score: number | null; wpm: number | null; accuracy: number | null }
interface MatchEndedNotification { matchId: string; outcome: string; resultOrErrors: unknown }
interface FailureNotification { matchId: string; reason: string }

const RESULT = {
  matchId: 'match-001',
  gameType: 'typing',
  players: [
    { userId: 'user-ana', displayName: 'Ana', score: 600 },
    { userId: 'user-luis', displayName: 'Luis', score: 360 },
  ],
  startedAt: '2026-10-03T20:00:00Z',
  finishedAt: '2026-10-03T20:01:00Z',
  winnerUserId: 'user-ana',
  metadata: {},
};

function matchContext(): GameContext {
  return {
    matchId: 'match-001',
    gameType: 'typing',
    currentUser: { id: 'user-ana', displayName: 'Ana' },
    events: new EventTarget(),
  };
}

async function startMatch(context: GameContext = matchContext()): Promise<{ game: GameModule; context: GameContext }> {
  const game = new GameModule();
  await game.initialize(context);
  await game.start();
  return { game, context };
}

beforeEach(() => {
  resetClient();
  jest.useFakeTimers();
  jest.spyOn(console, 'log').mockImplementation(() => undefined);
  jest.spyOn(console, 'warn').mockImplementation(() => undefined);
  jest.spyOn(console, 'error').mockImplementation(() => undefined);
});

afterEach(() => {
  jest.useRealTimers();
  jest.restoreAllMocks();
});

describe('jugadores de la partida (notificaciones del hub)', () => {
  it('un jugador que entra se agrega a la lista, una sola vez', async () => {
    const { game } = await startMatch();
    const playerJoined = registeredHandler<PlayerNotification>(mockClient.onPlayerJoined);

    playerJoined({ userId: 'user-luis' });
    playerJoined({ userId: 'user-luis' });

    expect(game.players.map((player) => player.userId)).toEqual(['user-ana', 'user-luis']);
    expect(game.players[1]).toEqual({
      userId: 'user-luis',
      displayName: 'user-luis',
      score: null,
      wpm: 0,
      accuracy: null,
    });
  });

  it('un jugador que sale se quita de la lista', async () => {
    const { game } = await startMatch();
    registeredHandler<PlayerNotification>(mockClient.onPlayerJoined)({ userId: 'user-luis' });

    registeredHandler<PlayerNotification>(mockClient.onPlayerLeft)({ userId: 'user-luis' });

    expect(game.players.map((player) => player.userId)).toEqual(['user-ana']);
  });

  it('actualiza las métricas de un jugador que ya está en la lista', async () => {
    const { game } = await startMatch();

    registeredHandler<UpdateNotification>(mockClient.onPlayerUpdate)({
      userId: 'user-ana',
      score: 540,
      wpm: 54,
      accuracy: 100,
    });

    expect(game.players).toHaveLength(1);
    expect(game.players[0]).toMatchObject({ userId: 'user-ana', score: 540, wpm: 54, accuracy: 100 });
  });

  it('agrega a un jugador desconocido si llegan sus métricas primero', async () => {
    const { game } = await startMatch();

    registeredHandler<UpdateNotification>(mockClient.onPlayerUpdate)({
      userId: 'user-luis',
      score: 300,
      wpm: 30,
      accuracy: 90,
    });

    expect(game.players.find((player) => player.userId === 'user-luis')).toEqual({
      userId: 'user-luis',
      displayName: 'user-luis',
      score: 300,
      wpm: 30,
      accuracy: 90,
    });
  });

  it('registra los avisos de que no pudo unirse o actualizar, sin romper el juego', async () => {
    const { game } = await startMatch();
    const joinFailed: FailureNotification = { matchId: 'match-001', reason: 'join_rejected' };
    const updateFailed: FailureNotification = { matchId: 'match-001', reason: 'not_joined' };

    registeredHandler<FailureNotification>(mockClient.onJoinFailed)(joinFailed);
    registeredHandler<FailureNotification>(mockClient.onUpdateFailed)(updateFailed);

    expect(console.error).toHaveBeenCalledWith('No se pudo unir a la partida:', joinFailed);
    expect(console.error).toHaveBeenCalledWith('No se pudo actualizar al jugador:', updateFailed);
    expect(game.canType).toBe(true);
  });
});

describe('resultado de la partida (matchEnded)', () => {
  let finished: GameFinishedDetail[];

  async function startAndListen(): Promise<{ game: GameModule; matchEnded: (n: MatchEndedNotification) => void }> {
    const { game, context } = await startMatch();
    finished = [];
    context.events.addEventListener(GAME_FINISHED_EVENT, (event) => {
      finished.push((event as CustomEvent<GameFinishedDetail>).detail);
    });
    return { game, matchEnded: registeredHandler<MatchEndedNotification>(mockClient.onMatchEnded) };
  }

  it('created: muestra el resultado, completa la lista de jugadores y detiene el reloj', async () => {
    const { game, matchEnded } = await startAndListen();
    game.historyLoaded = true;
    game.statsLoaded = true;

    matchEnded({ matchId: 'match-001', outcome: 'created', resultOrErrors: RESULT });

    expect(game.matchFinished).toBe(true);
    expect(game.matchOutcome).toBe('created');
    expect(game.finalResult).toEqual(RESULT);
    expect(game.resultMessage).toBe('Partida finalizada correctamente.');
    expect(game.canType).toBe(false);
    expect(jest.getTimerCount()).toBe(0);
    expect(game.players.find((player) => player.userId === 'user-ana')?.score).toBe(600);
    expect(game.players.find((player) => player.userId === 'user-luis')).toEqual({
      userId: 'user-luis',
      displayName: 'Luis',
      score: 360,
      wpm: null,
      accuracy: null,
    });
    expect(game.historyLoaded).toBe(false);
    expect(game.statsLoaded).toBe(false);
    expect(finished).toEqual([{ matchId: 'match-001', gameType: 'typing' }]);
  });

  it('created con un resultado que no se puede leer: avisa, pero la partida igual terminó', async () => {
    const { game, matchEnded } = await startAndListen();

    matchEnded({ matchId: 'match-001', outcome: 'created', resultOrErrors: { inesperado: true } });

    expect(game.finalResult).toBeNull();
    expect(game.resultMessage).toBe('La partida terminó, pero no fue posible leer el resultado.');
    expect(finished).toHaveLength(1);
  });

  it('already_exists sin resultado previo: informa que ya estaba registrado y avisa al Shell', async () => {
    const { game, matchEnded } = await startAndListen();

    matchEnded({ matchId: 'match-001', outcome: 'already_exists', resultOrErrors: { matchId: 'match-001' } });

    expect(game.resultMessage).toBe('El resultado de esta partida ya había sido registrado.');
    expect(game.matchFinished).toBe(true);
    expect(finished).toHaveLength(1);
  });

  it('invalid: informa los datos inválidos, deja el detalle en la consola y no avisa al Shell', async () => {
    const { game, matchEnded } = await startAndListen();
    const errors = { players: ['Debe incluir al menos un jugador.'] };

    matchEnded({ matchId: 'match-001', outcome: 'invalid', resultOrErrors: errors });

    expect(game.resultMessage).toBe('No fue posible guardar el resultado porque contiene datos inválidos.');
    expect(console.error).toHaveBeenCalledWith('Errores de validación:', errors);
    expect(finished).toEqual([]);
  });

  it('error: informa que falló el cierre y no avisa al Shell', async () => {
    const { game, matchEnded } = await startAndListen();

    matchEnded({ matchId: 'match-001', outcome: 'error', resultOrErrors: { message: 'unexpected_outcome' } });

    expect(game.resultMessage).toBe('Ocurrió un error al finalizar la partida.');
    expect(console.error).toHaveBeenCalledWith('Error al finalizar la partida:', { message: 'unexpected_outcome' });
    expect(finished).toEqual([]);
  });

  it('un resultado que el juego no reconoce se muestra como finalizada, se registra y no avisa al Shell', async () => {
    const { game, matchEnded } = await startAndListen();

    matchEnded({ matchId: 'match-001', outcome: 'raro', resultOrErrors: { x: 1 } });

    expect(game.resultMessage).toBe('La partida ha finalizado.');
    expect(console.warn).toHaveBeenCalledWith('Resultado no reconocido:', 'raro', { x: 1 });
    expect(finished).toEqual([]);
  });
});

describe('ganador', () => {
  it('sin resultado todavía no hay ganador', async () => {
    const { game } = await startMatch();

    expect(game.winnerName).toBe('');
    expect(game.isWinner('user-ana')).toBe(false);
  });

  it('con el resultado, el ganador es el que indica el servidor', async () => {
    const { game } = await startMatch();

    registeredHandler<MatchEndedNotification>(mockClient.onMatchEnded)({
      matchId: 'match-001',
      outcome: 'created',
      resultOrErrors: RESULT,
    });

    expect(game.winnerName).toBe('Ana');
    expect(game.isWinner('user-ana')).toBe(true);
    expect(game.isWinner('user-luis')).toBe(false);
  });

  it('una partida sin ganador no muestra nombre', async () => {
    const { game } = await startMatch();

    game.finalResult = { ...RESULT, winnerUserId: null, metadata: {} };

    expect(game.winnerName).toBe('');
  });

  it('si el ganador no figura entre los jugadores, se muestra su id', async () => {
    const { game } = await startMatch();

    game.finalResult = { ...RESULT, winnerUserId: 'user-fantasma', metadata: {} };

    expect(game.winnerName).toBe('user-fantasma');
  });
});

describe('salir y llamadas fuera de una partida', () => {
  it('dispose registra el error si no puede avisar que sale, y de todos modos cierra la conexión', async () => {
    const { game } = await startMatch();
    const failure = new Error('red caída');
    mockClient.leaveMatch.mockRejectedValue(failure);

    await expect(game.dispose()).resolves.toBeUndefined();

    expect(console.error).toHaveBeenCalledWith('Error al salir de la partida:', failure);
    expect(mockClient.disconnect).toHaveBeenCalledTimes(1);
  });

  it('dispose no avisa que sale si la conexión ya estaba cerrada', async () => {
    const { game } = await startMatch();
    mockClient.state = 'Disconnected';

    await game.dispose();

    expect(mockClient.leaveMatch).not.toHaveBeenCalled();
    expect(mockClient.disconnect).toHaveBeenCalledTimes(1);
  });

  it('start() antes de initialize() falla con un mensaje claro', async () => {
    const game = new GameModule();

    await expect(game.start()).rejects.toThrow('El juego debe inicializarse antes de comenzar');
  });

  it('sendPlayerUpdate arma la actualización con el jugador y la partida del contexto', async () => {
    const { game } = await startMatch();

    await expect(game.sendPlayerUpdate(null, 50, 98)).resolves.toBe(true);

    expect(mockClient.sendPlayerUpdate).toHaveBeenCalledWith({
      matchId: 'match-001',
      currentUser: 'user-ana',
      score: null,
      wpm: 50,
      accuracy: 98,
      timestamp: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/),
    });
  });

  it('endMatch pide terminar la partida con los datos recibidos y detiene el reloj', async () => {
    const { game } = await startMatch();

    await game.endMatch(null, null, null, null, null);

    expect(mockClient.endMatch).toHaveBeenCalledWith({
      matchId: 'match-001',
      currentUser: 'user-ana',
      players: null,
      startedAt: null,
      finishedAt: null,
      winnerUserId: null,
      metadata: null,
    });
    expect(jest.getTimerCount()).toBe(0);
  });

  it('sin una partida integrada no se envían métricas ni se pide terminar', async () => {
    const game = new GameModule();

    await expect(game.sendPlayerUpdate(null, 50, 98)).rejects.toThrow('requieren una partida integrada');
    await expect(game.endMatch(null, null, null, null, null)).rejects.toThrow('requiere una partida integrada');
    expect(mockClient.sendPlayerUpdate).not.toHaveBeenCalled();
    expect(mockClient.endMatch).not.toHaveBeenCalled();
  });

  it('en práctica tampoco se envían métricas ni se pide terminar', async () => {
    const game = new GameModule();
    await game.initialize({
      mode: 'practice',
      gameType: 'typing',
      currentUser: { id: 'user-ana', displayName: 'Ana' },
      events: new EventTarget(),
    });

    await expect(game.sendPlayerUpdate(null, 50, 98)).rejects.toThrow('requieren una partida integrada');
    await expect(game.endMatch(null, null, null, null, null)).rejects.toThrow('requiere una partida integrada');
  });
});

describe('startPractice', () => {
  it('no convierte en práctica una partida normal', async () => {
    const { game } = await startMatch();

    game.startPractice();

    expect(game.isPracticeMode).toBe(false);
    expect(game.context?.matchId).toBe('match-001');
  });

  it('no reinicia una práctica que ya está en curso', () => {
    const game = new GameModule();
    game.startPractice();
    jest.advanceTimersByTime(5_000);

    game.startPractice();

    expect(game.timeRemaining).toBe(55);
    expect(jest.getTimerCount()).toBe(1);
  });
});

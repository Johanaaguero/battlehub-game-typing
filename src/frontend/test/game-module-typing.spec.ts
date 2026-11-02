import { GameModule } from '../src/game-module';
import type { GameContext } from '../src/game-contracts';
import { mockClient, registeredHandler, resetClient } from './helpers/signalr-mock';

jest.mock('../src/signalr-client', () => ({
  SignalRClient: jest.fn(
    () => jest.requireActual<typeof import('./helpers/signalr-mock')>('./helpers/signalr-mock').mockClient,
  ),
}));

function matchContext(): GameContext {
  return {
    matchId: 'match-001',
    gameType: 'typing',
    currentUser: { id: 'user-ana', displayName: 'Ana' },
    events: new EventTarget(),
  };
}

async function startMatch(): Promise<GameModule> {
  const game = new GameModule();
  await game.initialize(matchContext());
  await game.start();
  return game;
}

beforeEach(() => {
  resetClient();
  jest.useFakeTimers();
  jest.spyOn(console, 'log').mockImplementation(() => undefined);
  jest.spyOn(console, 'error').mockImplementation(() => undefined);
});

afterEach(() => {
  jest.useRealTimers();
  jest.restoreAllMocks();
});

describe('métricas al escribir', () => {
  it('cuenta aciertos y errores y calcula la precisión', async () => {
    const game = await startMatch();
    const typed = `${game.targetText.slice(0, 8)}x${game.targetText.slice(9, 10)}`;
    game.typedText = typed;

    game.handleTyping();

    expect(game.correctCharacters).toBe(9);
    expect(game.incorrectCharacters).toBe(1);
    expect(game.accuracy).toBe(90);
    expect(game.progress).toBe(Math.round((10 / game.targetText.length) * 100));
  });

  it('los caracteres de más cuentan como errores y el avance no pasa de 100 %', async () => {
    const game = await startMatch();
    game.typedText = `${game.targetText}xyz`;

    game.handleTyping();

    expect(game.correctCharacters).toBe(game.targetText.length);
    expect(game.incorrectCharacters).toBe(3);
    expect(game.progress).toBe(100);
    expect(game.completed).toBe(false);
    expect(game.canType).toBe(true);
  });

  it('sin nada escrito la precisión es 100 y no hay velocidad', async () => {
    const game = await startMatch();
    await jest.advanceTimersByTimeAsync(10_000);
    game.typedText = '';

    game.handleTyping();

    expect(game.accuracy).toBe(100);
    expect(game.wpm).toBe(0);
    expect(game.progress).toBe(0);
  });

  it('antes de que pase el primer segundo no hay velocidad', async () => {
    const game = await startMatch();
    game.typedText = game.targetText.slice(0, 5);

    game.handleTyping();

    expect(game.wpm).toBe(0);
  });

  it('la velocidad sale del tiempo transcurrido: cada 5 aciertos son una palabra', async () => {
    const game = await startMatch();
    await jest.advanceTimersByTimeAsync(30_000);
    game.typedText = game.targetText.slice(0, 25);

    game.handleTyping();

    // 25 aciertos = 5 palabras en medio minuto = 10 palabras por minuto.
    expect(game.wpm).toBe(10);
  });

  it('cada segundo recalcula la velocidad si todavía no terminó el texto', async () => {
    const game = await startMatch();
    game.typedText = game.targetText.slice(0, 25);
    game.handleTyping();

    await jest.advanceTimersByTimeAsync(30_000);

    expect(game.wpm).toBe(10);
    expect(game.players[0].wpm).toBe(10);
  });

  it('sin texto objetivo reinicia las métricas en lugar de dividir por cero', async () => {
    const game = await startMatch();
    game.targetText = '';
    game.typedText = 'abc';
    game.progress = 50;
    game.wpm = 30;

    game.handleTyping();

    expect(game.progress).toBe(0);
    expect(game.correctCharacters).toBe(0);
    expect(game.incorrectCharacters).toBe(0);
    expect(game.accuracy).toBe(100);
    expect(game.wpm).toBe(0);
  });

  it('si el jugador ya no figura en la lista, escribir no falla', async () => {
    const game = await startMatch();
    registeredHandler<{ userId: string }>(mockClient.onPlayerLeft)({ userId: 'user-ana' });
    game.typedText = game.targetText.slice(0, 5);

    expect(() => game.handleTyping()).not.toThrow();
    expect(game.correctCharacters).toBe(5);
  });
});

describe('envío de métricas al hub', () => {
  it('envía la velocidad y la precisión, y deja el puntaje en null para que lo calcule el servidor', async () => {
    const game = await startMatch();
    await jest.advanceTimersByTimeAsync(10_000);
    game.typedText = game.targetText.slice(0, 10);

    game.handleTyping();
    await jest.advanceTimersByTimeAsync(0);

    expect(mockClient.sendPlayerUpdate).toHaveBeenCalledWith(
      expect.objectContaining({
        matchId: 'match-001',
        currentUser: 'user-ana',
        score: null,
        wpm: game.wpm,
        accuracy: game.accuracy,
      }),
    );
  });

  it('no envía nada si la conexión no está abierta', async () => {
    const game = await startMatch();
    mockClient.state = 'Reconnecting';
    game.typedText = game.targetText.slice(0, 5);

    game.handleTyping();
    await jest.advanceTimersByTimeAsync(0);

    expect(mockClient.sendPlayerUpdate).not.toHaveBeenCalled();
  });

  it('un fallo al enviar las métricas se registra, pero el jugador puede seguir escribiendo', async () => {
    const game = await startMatch();
    const failure = new Error('hub caído');
    mockClient.sendPlayerUpdate.mockRejectedValue(failure);
    game.typedText = game.targetText.slice(0, 5);

    game.handleTyping();
    await jest.advanceTimersByTimeAsync(0);

    expect(console.error).toHaveBeenCalledWith('No fue posible enviar las métricas del jugador:', failure);
    expect(game.canType).toBe(true);
  });
});

describe('fin del tiempo', () => {
  it('si no se puede pedir el fin de la partida, avisa al jugador y no lo reintenta', async () => {
    const game = await startMatch();
    const failure = new Error('sin conexión');
    mockClient.endMatch.mockRejectedValue(failure);

    await jest.advanceTimersByTimeAsync(60_000);
    await jest.advanceTimersByTimeAsync(10_000);

    expect(game.isTimeUp).toBe(true);
    expect(game.matchFinished).toBe(false);
    expect(game.resultMessage).toContain('No fue posible finalizar la partida');
    expect(console.error).toHaveBeenCalledWith('Error al finalizar la partida:', failure);
    expect(mockClient.endMatch).toHaveBeenCalledTimes(1);
  });

  it('reanudar después de acabarse el tiempo no vuelve a arrancar el reloj', async () => {
    const game = await startMatch();
    await jest.advanceTimersByTimeAsync(60_000);

    await game.start();
    await jest.advanceTimersByTimeAsync(5_000);

    expect(game.timeRemaining).toBe(0);
    expect(jest.getTimerCount()).toBe(0);
    expect(game.canType).toBe(false);
  });

  it('en la práctica, al acabarse el tiempo termina localmente sin pedirle nada al hub', async () => {
    const game = new GameModule();
    await game.initialize({
      mode: 'practice',
      gameType: 'typing',
      currentUser: { id: 'user-ana', displayName: 'Ana' },
      events: new EventTarget(),
    });
    await game.start();

    await jest.advanceTimersByTimeAsync(60_000);

    expect(game.matchFinished).toBe(true);
    expect(game.matchOutcome).toBe('PRÁCTICA LOCAL');
    expect(mockClient.endMatch).not.toHaveBeenCalled();
    expect(mockClient.sendPlayerUpdate).not.toHaveBeenCalled();
  });
});

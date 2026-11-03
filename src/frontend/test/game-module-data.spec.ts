import { GameModule } from '../src/game-module';
import { RESULTS_API_URL } from '../src/config';
import type { GameContext } from '../src/game-contracts';
import { resetClient } from './helpers/signalr-mock';

jest.mock('../src/signalr-client', () => ({
  SignalRClient: jest.fn(
    () => jest.requireActual<typeof import('./helpers/signalr-mock')>('./helpers/signalr-mock').mockClient,
  ),
}));

const HISTORY = [
  {
    matchId: 'match-001',
    startedAt: '2026-10-03T20:00:00Z',
    finishedAt: '2026-10-03T20:01:00Z',
    score: 600,
    wpm: 60,
    accuracy: 100,
    position: 1,
    playersCount: 2,
    won: true,
  },
];

const STATS = {
  userId: 'auth0|abc',
  gamesPlayed: 3,
  wins: 2,
  winRate: 0.6667,
  averageScore: 450,
  bestScore: 600,
  averageWpm: 45,
  bestWpm: 60,
  averageAccuracy: 95,
  lastPlayedAt: '2026-10-03T20:01:00Z',
};

function matchContext(): GameContext {
  return {
    matchId: 'match-001',
    gameType: 'typing',
    currentUser: { id: 'auth0|abc', displayName: 'Ana' },
    events: new EventTarget(),
  };
}

function okJson(body: unknown): Response {
  return { ok: true, json: async () => body } as unknown as Response;
}

function httpError(status: number): Response {
  return { ok: false, status } as unknown as Response;
}

describe('historial y estadísticas del jugador', () => {
  const realFetch = globalThis.fetch;
  let fetchMock: jest.Mock;
  let game: GameModule;

  beforeEach(async () => {
    resetClient();
    jest.spyOn(console, 'log').mockImplementation(() => undefined);
    jest.spyOn(console, 'error').mockImplementation(() => undefined);
    fetchMock = jest.fn();
    globalThis.fetch = fetchMock as unknown as typeof fetch;
    game = new GameModule();
    await game.initialize(matchContext());
  });

  afterEach(() => {
    globalThis.fetch = realFetch;
    jest.restoreAllMocks();
  });

  describe('historial', () => {
    it('lo pide a la API con el id del jugador codificado, con límite y desde el inicio', async () => {
      fetchMock.mockResolvedValue(okJson(HISTORY));

      await game.loadHistory();

      expect(fetchMock).toHaveBeenCalledWith(`${RESULTS_API_URL}/players/auth0%7Cabc/history?limit=50&offset=0`);
      expect(game.history).toEqual(HISTORY);
      expect(game.historyLoaded).toBe(true);
      expect(game.historyError).toBe('');
      expect(game.historyLoading).toBe(false);
    });

    it('si la API responde con error, muestra el aviso y deja el historial vacío', async () => {
      fetchMock.mockResolvedValue(httpError(500));

      await game.loadHistory();

      expect(game.history).toEqual([]);
      expect(game.historyLoaded).toBe(false);
      expect(game.historyError).toContain('No fue posible cargar el historial');
      expect(game.historyLoading).toBe(false);
      expect(console.error).toHaveBeenCalledWith('Error cargando historial:', new Error('HTTP 500'));
    });

    it('si la API no responde, muestra el mismo aviso', async () => {
      fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

      await game.loadHistory();

      expect(game.historyError).toContain('Verificá que el backend esté disponible');
      expect(game.historyLoaded).toBe(false);
    });

    it('showHistory abre la sección y lo carga solo la primera vez', async () => {
      fetchMock.mockResolvedValue(okJson(HISTORY));

      await game.showHistory();
      await game.showHistory();

      expect(game.activeSection).toBe('history');
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it('refreshHistory vuelve a pedirlo aunque ya estuviera cargado', async () => {
      fetchMock.mockResolvedValue(okJson(HISTORY));
      await game.showHistory();

      await game.refreshHistory();

      expect(fetchMock).toHaveBeenCalledTimes(2);
      expect(game.historyLoaded).toBe(true);
    });

    it('no pide nada si el juego todavía no recibió su contexto', async () => {
      await new GameModule().loadHistory();

      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('estadísticas', () => {
    it('las pide a la API con el id del jugador codificado', async () => {
      fetchMock.mockResolvedValue(okJson(STATS));

      await game.loadStats();

      expect(fetchMock).toHaveBeenCalledWith(`${RESULTS_API_URL}/players/auth0%7Cabc/stats`);
      expect(game.stats).toEqual(STATS);
      expect(game.statsLoaded).toBe(true);
      expect(game.statsError).toBe('');
      expect(game.statsLoading).toBe(false);
    });

    it('si la API responde con error, muestra el aviso y no deja estadísticas', async () => {
      fetchMock.mockResolvedValue(httpError(503));

      await game.loadStats();

      expect(game.stats).toBeNull();
      expect(game.statsLoaded).toBe(false);
      expect(game.statsError).toContain('No fue posible cargar las estadísticas');
      expect(console.error).toHaveBeenCalledWith('Error cargando estadísticas:', new Error('HTTP 503'));
    });

    it('si la API no responde, muestra el mismo aviso', async () => {
      fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

      await game.loadStats();

      expect(game.statsError).toContain('Verificá que el backend esté disponible');
      expect(game.stats).toBeNull();
    });

    it('showStats abre la sección y las carga solo la primera vez', async () => {
      fetchMock.mockResolvedValue(okJson(STATS));

      await game.showStats();
      await game.showStats();

      expect(game.activeSection).toBe('stats');
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it('refreshStats vuelve a pedirlas aunque ya estuvieran cargadas', async () => {
      fetchMock.mockResolvedValue(okJson(STATS));
      await game.showStats();

      await game.refreshStats();

      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('no pide nada si el juego todavía no recibió su contexto', async () => {
      await new GameModule().loadStats();

      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('winRatePercentage convierte la fracción en porcentaje con un decimal', async () => {
      expect(game.winRatePercentage).toBe(0);

      game.stats = STATS;

      expect(game.winRatePercentage).toBe(66.7);
    });
  });

  it('showGame vuelve a la pantalla del juego', async () => {
    fetchMock.mockResolvedValue(okJson(HISTORY));
    await game.showHistory();

    game.showGame();

    expect(game.activeSection).toBe('game');
  });

  describe('formato', () => {
    it('formatDate muestra la fecha en español de Costa Rica', () => {
      expect(game.formatDate('2026-10-03T20:01:00Z')).toMatch(/2026/);
    });

    it.each([null, '', 'no es una fecha'])('formatDate devuelve «Sin datos» para %p', (value) => {
      expect(game.formatDate(value)).toBe('Sin datos');
    });

    it('formatMetric usa un decimal y un guion largo cuando no hay dato', () => {
      expect(game.formatMetric(62.44)).toBe('62.4');
      expect(game.formatMetric(0)).toBe('0.0');
      expect(game.formatMetric(null)).toBe('—');
    });
  });
});

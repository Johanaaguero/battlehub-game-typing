// Cliente SignalR simulado: las pruebas del juego no abren conexiones reales.
// Se usa dentro de jest.mock('../src/signalr-client', ...) con jest.requireActual para compartir la misma instancia.
export const mockClient = {
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

/** Limpia las llamadas y deja al cliente conectado y respondiendo bien. */
export function resetClient(): void {
  jest.clearAllMocks();
  mockClient.state = 'Connected';
  mockClient.connect.mockResolvedValue(undefined);
  mockClient.joinMatch.mockResolvedValue(true);
  mockClient.leaveMatch.mockResolvedValue(true);
  mockClient.sendPlayerUpdate.mockResolvedValue(true);
  mockClient.endMatch.mockResolvedValue(undefined);
  mockClient.disconnect.mockResolvedValue(undefined);
}

/** El callback que el juego registró en una de las suscripciones del cliente (onPlayerJoined, onMatchEnded…). */
export function registeredHandler<T>(register: jest.Mock): (notification: T) => void {
  return register.mock.calls[0][0] as (notification: T) => void;
}

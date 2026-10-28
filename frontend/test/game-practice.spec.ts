
import { GameModule } from '../src/game-module';
import { GAME_FINISHED_EVENT } from '../src/game-contracts';
import type { SignalRClient } from '../src/signalr-client';

describe('práctica individual de Typing Battle', () => {
    let game: GameModule;

    beforeEach(() => {
        jest.useFakeTimers();
        game = new GameModule();
    });

    afterEach(async () => {
        await game.dispose();
        jest.useRealTimers();
    });

    it('inicia sin un matchId real ni conexión a SignalR', () => {
        const signalR = (game as unknown as { signalR: SignalRClient }).signalR;
        const connectSpy = jest.spyOn(signalR, 'connect');
        const joinMatchSpy = jest.spyOn(signalR, 'joinMatch');

        game.startPractice();

        expect(connectSpy).not.toHaveBeenCalled();
        expect(joinMatchSpy).not.toHaveBeenCalled();
        expect(game.isPracticeMode).toBe(true);
        expect(game.context).not.toBeNull();
        expect(game.context?.matchId).toBeUndefined();
        expect(game.canType).toBe(true);
        expect(game.timeRemaining).toBe(60);
        expect(game.players).toHaveLength(1);
    });

    it('calcula las métricas mientras se escribe en modo local', () => {
        game.startPractice();
        game.typedText = game.targetText.slice(0, 10);

        game.handleTyping();

        expect(game.correctCharacters).toBe(10);
        expect(game.incorrectCharacters).toBe(0);
        expect(game.progress).toBeGreaterThan(0);
        expect(game.accuracy).toBe(100);
    });

    it('finaliza localmente cuando se completa el texto', () => {
        game.startPractice();
        game.typedText = game.targetText;

        game.handleTyping();

        expect(game.matchFinished).toBe(true);
        expect(game.isTimeUp).toBe(true);
        expect(game.matchOutcome).toBe('PRÁCTICA LOCAL');
        expect(game.canType).toBe(false);
        expect(game.resultMessage).toContain('no se enviaron al servidor');
    });

    it('no avisa al Shell al terminar la práctica: no es una partida de Matchmaking', () => {
        const listener = jest.fn();
        window.addEventListener(GAME_FINISHED_EVENT, listener);
        game.startPractice();
        game.typedText = game.targetText;

        game.handleTyping();

        window.removeEventListener(GAME_FINISHED_EVENT, listener);
        expect(game.matchFinished).toBe(true);
        expect(listener).not.toHaveBeenCalled();
    });

    it('finaliza localmente cuando se agota el tiempo', () => {
        game.startPractice();

        jest.advanceTimersByTime(60_000);

        expect(game.timeRemaining).toBe(0);
        expect(game.matchFinished).toBe(true);
        expect(game.matchOutcome).toBe('PRÁCTICA LOCAL');
    });

    it('permite volver a jugar y reinicia las métricas', () => {
        game.startPractice();
        game.typedText = game.targetText;
        game.handleTyping();

        game.restartPractice();

        expect(game.isPracticeMode).toBe(true);
        expect(game.matchFinished).toBe(false);
        expect(game.typedText).toBe('');
        expect(game.progress).toBe(0);
        expect(game.correctCharacters).toBe(0);
        expect(game.incorrectCharacters).toBe(0);
        expect(game.accuracy).toBe(100);
        expect(game.timeRemaining).toBe(60);
        expect(game.canType).toBe(true);
    });

    it('conserva el flujo integrado: conecta SignalR y se une al match del Shell', async () => {
        await game.initialize({
            matchId: 'match-integrado-123',
            gameType: 'typing',
            currentUser: {
                id: 'user-shell-123',
                displayName: 'Jugador Shell',
            },
        });

        const signalR = (game as unknown as { signalR: SignalRClient }).signalR;
        const connectSpy = jest.spyOn(signalR, 'connect').mockResolvedValue(undefined);
        const joinMatchSpy = jest.spyOn(signalR, 'joinMatch').mockResolvedValue(true);
        jest.spyOn(signalR, 'disconnect').mockResolvedValue(undefined);

        await game.start();

        expect(game.isPracticeMode).toBe(false);
        expect(connectSpy).toHaveBeenCalledTimes(1);
        expect(joinMatchSpy).toHaveBeenCalledWith({
            matchId: 'match-integrado-123',
            currentUser: 'user-shell-123',
            displayName: 'Jugador Shell',
        });

        await game.pause();
    });
});

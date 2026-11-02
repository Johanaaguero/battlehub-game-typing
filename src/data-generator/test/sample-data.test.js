import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { SAMPLE_PLAYERS, createRng, generateMatches, matchIdFor, toIsoUtc } from '../src/sample-data.js';

const NOW = new Date('2026-11-02T12:00:00Z');
const ISO_UTC = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;

describe('partidas de ejemplo', () => {
    it('genera la cantidad pedida con ids fijos y sin repetir', () => {
        const matches = generateMatches({ count: 30, now: NOW });

        assert.equal(matches.length, 30);
        assert.equal(matches[0].matchId, 'demo-0001');
        assert.equal(matches[29].matchId, 'demo-0030');
        assert.equal(new Set(matches.map((m) => m.matchId)).size, 30);
        assert.equal(matchIdFor(7), 'demo-0007');
    });

    it('cumple las reglas de validación de POST /results', () => {
        for (const match of generateMatches({ count: 60, now: NOW })) {
            assert.equal(match.gameType, 'typing');
            assert.ok(match.players.length >= 2 && match.players.length <= 4, `${match.matchId}: de 2 a 4 jugadores`);
            assert.equal(new Set(match.players.map((p) => p.userId)).size, match.players.length, 'sin jugadores repetidos');

            for (const player of match.players) {
                assert.ok(player.displayName.length > 0);
                assert.ok(Number.isInteger(player.score) && player.score >= 0);
            }

            assert.match(match.startedAt, ISO_UTC);
            assert.match(match.finishedAt, ISO_UTC);
            assert.ok(new Date(match.finishedAt) >= new Date(match.startedAt), 'finishedAt no es anterior a startedAt');
            assert.ok(new Date(match.finishedAt) <= NOW, 'la partida ya terminó');
            assert.ok(JSON.stringify(match.metadata).length <= 16 * 1024);
        }
    });

    it('el orden de players es el ranking y el ganador es el primero', () => {
        for (const match of generateMatches({ count: 40, now: NOW })) {
            const scores = match.players.map((p) => p.score);

            assert.deepEqual(scores, [...scores].sort((a, b) => b - a));
            assert.equal(match.winnerUserId, match.players[0].userId);
        }
    });

    it('trae wpm y precisión de cada jugador para el historial y las estadísticas', () => {
        for (const match of generateMatches({ count: 20, now: NOW })) {
            assert.deepEqual(
                match.metadata.players.map((p) => p.userId),
                match.players.map((p) => p.userId),
            );

            for (const stats of match.metadata.players) {
                assert.ok(stats.wpm > 0 && stats.accuracy > 0 && stats.accuracy <= 100);
            }
        }
    });

    it('calcula el puntaje con la fórmula del hub: wpm × precisión × 10', () => {
        for (const match of generateMatches({ count: 20, now: NOW })) {
            for (const player of match.players) {
                const stats = match.metadata.players.find((p) => p.userId === player.userId);

                assert.equal(player.score, Math.round((stats.wpm * stats.accuracy) / 100 * 10));
            }
        }
    });

    it('con los mismos datos de entrada genera siempre las mismas partidas', () => {
        assert.deepEqual(generateMatches({ count: 30, now: NOW }), generateMatches({ count: 30, now: NOW }));
    });

    it('las primeras partidas no cambian al pedir más', () => {
        const treinta = generateMatches({ count: 30, now: NOW });
        const cien = generateMatches({ count: 100, now: NOW });

        assert.deepEqual(cien.slice(0, 30), treinta);
    });

    it('reparte las partidas en las últimas semanas, de la más reciente a la más antigua', () => {
        const matches = generateMatches({ count: 30, now: NOW });
        const days = (match) => (NOW - new Date(match.startedAt)) / 86_400_000;

        assert.ok(days(matches[0]) < 2, 'la primera es de hace menos de 2 días');
        assert.ok(days(matches[29]) > 14 && days(matches[29]) < 28, 'la última es de hace 2 a 4 semanas');
        assert.ok(matches.every((m, i) => i === 0 || new Date(m.startedAt) < new Date(matches[i - 1].startedAt)));
    });

    it('usa los 8 jugadores de ejemplo', () => {
        const used = new Set(generateMatches({ count: 30, now: NOW }).flatMap((m) => m.players.map((p) => p.userId)));

        assert.equal(SAMPLE_PLAYERS.length, 8);
        assert.deepEqual([...used].sort(), SAMPLE_PLAYERS.map((p) => p.userId).sort());
    });

    it('incluye a los jugadores extra, como los usuarios reales de la demostración', () => {
        const extra = [{ userId: 'auth0|64f0c1', displayName: 'Usuario Demo' }];
        const matches = generateMatches({ count: 30, now: NOW, extraPlayers: extra });
        const withExtra = matches.filter((m) => m.players.some((p) => p.userId === 'auth0|64f0c1'));

        assert.ok(withExtra.length > 0, 'aparece en alguna partida');
        assert.equal(withExtra[0].players.find((p) => p.userId === 'auth0|64f0c1').displayName, 'Usuario Demo');
    });

    it('ignora un jugador extra que repite el id de uno de ejemplo', () => {
        const extra = [{ userId: 'demo-ana', displayName: 'Otra Ana' }];

        assert.deepEqual(
            generateMatches({ count: 10, now: NOW, extraPlayers: extra }),
            generateMatches({ count: 10, now: NOW }),
        );
    });
});

describe('utilidades', () => {
    it('toIsoUtc escribe UTC con Z y sin milisegundos', () => {
        assert.equal(toIsoUtc(new Date('2026-11-02T12:00:00.789Z')), '2026-11-02T12:00:00Z');
    });

    it('createRng repite la secuencia con la misma semilla', () => {
        const a = createRng(42);
        const b = createRng(42);

        assert.deepEqual([a(), a(), a()], [b(), b(), b()]);
        assert.notEqual(createRng(1)(), createRng(2)());
    });
});

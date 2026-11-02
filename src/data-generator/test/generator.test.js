import assert from 'node:assert/strict';
import http from 'node:http';
import { afterEach, describe, it } from 'node:test';

import { run, waitForApi } from '../src/generator.js';

const NOW = new Date('2026-11-02T12:00:00Z');
const silent = { log: () => undefined, error: () => undefined, now: NOW, pollMs: 10 };

const servers = [];

afterEach(async () => {
    await Promise.all(servers.splice(0).map((server) => new Promise((resolve) => server.close(resolve))));
});

/** API falsa: /health responde 503 las primeras `unhealthyCalls` veces y POST /results imita 201, 409 y rechazos. */
async function startFakeApi({ unhealthyCalls = 0, rejectWith = null } = {}) {
    const saved = new Map();
    let healthCalls = 0;

    const server = http.createServer((request, response) => {
        if (request.method === 'GET' && request.url === '/health') {
            healthCalls++;
            response.statusCode = healthCalls > unhealthyCalls ? 200 : 503;
            response.end();
            return;
        }

        if (request.method === 'POST' && request.url === '/api/games/typing/results') {
            let body = '';
            request.on('data', (chunk) => (body += chunk));
            request.on('end', () => {
                const match = JSON.parse(body);

                if (rejectWith) {
                    response.statusCode = rejectWith;
                    response.end('{"errors":{"matchId":["inválido"]}}');
                } else if (saved.has(match.matchId)) {
                    response.statusCode = 409;
                    response.end();
                } else {
                    saved.set(match.matchId, match);
                    response.statusCode = 201;
                    response.end('{}');
                }
            });
            return;
        }

        response.statusCode = 404;
        response.end();
    });

    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    servers.push(server);

    return { saved, url: `http://127.0.0.1:${server.address().port}`, healthCalls: () => healthCalls };
}

function envFor(url, overrides = {}) {
    return { DATA_API_URL: url, DATA_MATCHES: '5', DATA_WAIT_SECONDS: '5', DATA_EXTRA_PLAYERS: '', ...overrides };
}

describe('generador de datos', () => {
    it('espera a que la API responda y registra todas las partidas', async () => {
        const api = await startFakeApi({ unhealthyCalls: 2 });

        const exitCode = await run(envFor(api.url), silent);

        assert.equal(exitCode, 0);
        assert.equal(api.healthCalls(), 3, 'reintentó /health hasta que respondió 200');
        assert.deepEqual([...api.saved.keys()], ['demo-0001', 'demo-0002', 'demo-0003', 'demo-0004', 'demo-0005']);
    });

    it('volver a correrlo no duplica datos: la API responde 409 y se ignora', async () => {
        const api = await startFakeApi();
        const messages = [];

        assert.equal(await run(envFor(api.url), silent), 0);
        assert.equal(await run(envFor(api.url), { ...silent, log: (text) => messages.push(text) }), 0);

        assert.equal(api.saved.size, 5);
        assert.match(messages.at(-1), /0 partidas nuevas, 5 ya existían, 0 con error/);
    });

    it('envía partidas que la API acepta: sin repetir ids y con el ganador primero', async () => {
        const api = await startFakeApi();

        await run(envFor(api.url, { DATA_MATCHES: '10' }), silent);

        for (const match of api.saved.values()) {
            assert.equal(match.gameType, 'typing');
            assert.equal(match.winnerUserId, match.players[0].userId);
        }
    });

    it('incluye a los jugadores extra de la configuración', async () => {
        const api = await startFakeApi();

        await run(envFor(api.url, { DATA_MATCHES: '30', DATA_EXTRA_PLAYERS: 'auth0|1=Usuario Demo' }), silent);

        const ids = new Set([...api.saved.values()].flatMap((match) => match.players.map((p) => p.userId)));
        assert.ok(ids.has('auth0|1'));
    });

    it('termina con error si la API rechaza las partidas', async () => {
        const api = await startFakeApi({ rejectWith: 400 });
        const errors = [];

        const exitCode = await run(envFor(api.url), { ...silent, error: (text) => errors.push(text) });

        assert.equal(exitCode, 1);
        assert.equal(errors.length, 5);
        assert.match(errors[0], /demo-0001: la API respondió 400/);
    });

    it('termina con error si la API nunca responde', async () => {
        const api = await startFakeApi({ unhealthyCalls: Infinity });
        const errors = [];

        const exitCode = await run(envFor(api.url, { DATA_WAIT_SECONDS: '1' }), { ...silent, error: (text) => errors.push(text) });

        assert.equal(exitCode, 1);
        assert.equal(api.saved.size, 0);
        assert.match(errors[0], /La API no respondió en 1 s/);
    });

    it('termina con error si la API no está levantada', async () => {
        const api = await startFakeApi();
        const url = api.url;
        await new Promise((resolve) => servers.pop().close(resolve));

        assert.equal(await run(envFor(url, { DATA_WAIT_SECONDS: '1' }), silent), 1);
    });

    it('con la configuración inválida no llama a la API', async () => {
        let calls = 0;
        const errors = [];

        const exitCode = await run(
            { DATA_API_URL: 'http://api:8080' },
            { ...silent, error: (text) => errors.push(text), fetchFn: async () => { calls++; return { ok: true }; } },
        );

        assert.equal(exitCode, 1);
        assert.equal(calls, 0);
        assert.match(errors[0], /Configuración inválida/);
        assert.match(errors[0], /DATA_MATCHES/);
    });

    it('cuenta como error una partida que no se pudo enviar por la red', async () => {
        const errors = [];
        let posts = 0;
        const fetchFn = async (url) => {
            if (url.endsWith('/health')) {
                return { ok: true };
            }

            posts++;
            throw new Error('conexión rechazada');
        };

        const exitCode = await run(envFor('http://api:8080', { DATA_MATCHES: '2' }), { ...silent, fetchFn, error: (text) => errors.push(text) });

        assert.equal(exitCode, 1);
        assert.equal(posts, 2);
        assert.match(errors[0], /no se pudo enviar \(conexión rechazada\)/);
    });
});

describe('waitForApi', () => {
    it('devuelve true en cuanto /health responde 200', async () => {
        const api = await startFakeApi();

        assert.equal(await waitForApi(api.url, 5, { log: () => undefined, pollMs: 10 }), true);
        assert.equal(api.healthCalls(), 1);
    });
});

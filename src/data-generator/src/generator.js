import { ConfigError, readConfig } from './config.js';
import { generateMatches } from './sample-data.js';

const REQUEST_TIMEOUT_MS = 10_000;
const POLL_INTERVAL_MS = 2_000;

/**
 * Espera a que la API responda /health con 200. La imagen de la API no trae curl, así que la espera la hace el
 * generador. Devuelve false si se acaba el tiempo.
 */
export async function waitForApi(apiUrl, waitSeconds, { fetchFn = fetch, sleep = defaultSleep, pollMs = POLL_INTERVAL_MS, log = console.log } = {}) {
    const deadline = Date.now() + waitSeconds * 1000;
    log(`Esperando a la API en ${apiUrl} (máximo ${waitSeconds} s)...`);

    for (;;) {
        try {
            const response = await fetchFn(`${apiUrl}/health`, { signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
            if (response.ok) {
                log('API lista.');
                return true;
            }
        } catch {
            // La API todavía no acepta conexiones: se reintenta.
        }

        if (Date.now() + pollMs > deadline) {
            return false;
        }

        await sleep(pollMs);
    }
}

/** Registra una partida. 201 es nueva; 409 ya existía (no se duplica); cualquier otra cosa es un error. */
async function postMatch(apiUrl, match, fetchFn, error) {
    try {
        const response = await fetchFn(`${apiUrl}/api/games/typing/results`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(match),
            signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        });

        if (response.status === 201) {
            return 'created';
        }

        if (response.status === 409) {
            return 'exists';
        }

        error(`  ${match.matchId}: la API respondió ${response.status} ${(await response.text()).slice(0, 300)}`);
    } catch (cause) {
        error(`  ${match.matchId}: no se pudo enviar (${cause.message})`);
    }

    return 'failed';
}

/** Genera y registra las partidas de ejemplo. Devuelve el código de salida del proceso: 0 si todo salió bien. */
export async function run(env, { fetchFn = fetch, now = new Date(), log = console.log, error = console.error, sleep, pollMs } = {}) {
    let config;

    try {
        config = readConfig(env);
    } catch (cause) {
        if (!(cause instanceof ConfigError)) {
            throw cause;
        }

        error(`Configuración inválida:\n${cause.message}`);
        return 1;
    }

    const ready = await waitForApi(config.apiUrl, config.waitSeconds, { fetchFn, sleep, pollMs, log });
    if (!ready) {
        error(`La API no respondió en ${config.waitSeconds} s: no se generaron datos.`);
        return 1;
    }

    const matches = generateMatches({ count: config.matches, now, extraPlayers: config.extraPlayers });
    const counts = { created: 0, exists: 0, failed: 0 };

    for (const match of matches) {
        counts[await postMatch(config.apiUrl, match, fetchFn, error)]++;
    }

    log(`Datos de ejemplo: ${counts.created} partidas nuevas, ${counts.exists} ya existían, ${counts.failed} con error.`);

    return counts.failed === 0 ? 0 : 1;
}

function defaultSleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

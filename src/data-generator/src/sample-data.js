// Partidas de ejemplo con la forma de POST /api/games/typing/results (docs/api-resultados.md). Es lógica pura:
// no toca la red ni el reloj, y con los mismos datos de entrada produce siempre las mismas partidas.

const GAME_TYPE = 'typing';
const TIME_LIMIT_SECONDS = 60;
const MIN_PLAYERS = 2;
const MAX_PLAYERS = 4;
const HOURS_BETWEEN_MATCHES = 17;

/** Jugadores de ejemplo, con la velocidad (wpm) alrededor de la cual escriben. */
export const SAMPLE_PLAYERS = [
    { userId: 'demo-ana', displayName: 'Ana Mora', skill: 78 },
    { userId: 'demo-luis', displayName: 'Luis Vargas', skill: 64 },
    { userId: 'demo-sofia', displayName: 'Sofía Rojas', skill: 71 },
    { userId: 'demo-carlos', displayName: 'Carlos Jiménez', skill: 52 },
    { userId: 'demo-maria', displayName: 'María Solano', skill: 58 },
    { userId: 'demo-diego', displayName: 'Diego Araya', skill: 45 },
    { userId: 'demo-valeria', displayName: 'Valeria Quesada', skill: 83 },
    { userId: 'demo-mateo', displayName: 'Mateo Cordero', skill: 49 },
];

/** Generador pseudoaleatorio con semilla (mulberry32): el mismo número da la misma secuencia. */
export function createRng(seed) {
    let state = seed >>> 0;

    return () => {
        state = (state + 0x6d2b79f5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

/** Id fijo de la partida n (demo-0001, demo-0002…): por eso volver a generar no duplica nada. */
export function matchIdFor(index) {
    return `demo-${String(index).padStart(4, '0')}`;
}

/** Fecha en UTC, ISO-8601 con sufijo Z y sin milisegundos. */
export function toIsoUtc(date) {
    return `${date.toISOString().slice(0, 19)}Z`;
}

/**
 * Genera `count` partidas de 2 a 4 jugadores, de la más reciente (demo-0001) a la más antigua, con una cada 17 horas
 * aproximadamente. Las primeras N partidas son las mismas sin importar cuántas se pidan.
 */
export function generateMatches({ count, now, extraPlayers = [] }) {
    const roster = buildRoster(extraPlayers);

    return Array.from({ length: count }, (_, position) => buildMatch(position + 1, roster, now));
}

function buildRoster(extraPlayers) {
    const roster = [...SAMPLE_PLAYERS];

    for (const player of extraPlayers) {
        if (!roster.some((existing) => existing.userId === player.userId)) {
            roster.push({ ...player, skill: 45 + (hash(player.userId) % 35) });
        }
    }

    return roster;
}

function buildMatch(index, roster, now) {
    const rng = createRng(index * 7919 + 13);

    const playersInMatch = Math.min(roster.length, MIN_PLAYERS + Math.floor(rng() * (MAX_PLAYERS - MIN_PLAYERS + 1)));
    const chosen = shuffle(roster, rng).slice(0, playersInMatch);

    // El puntaje sale de la misma fórmula que usa el hub: wpm × precisión × 10.
    const ranking = chosen
        .map((player) => {
            const wpm = round1(clamp(player.skill + (rng() - 0.5) * 16, 15, 120));
            const accuracy = round1(clamp(88 + rng() * 11.5, 80, 99.9));
            const finished = rng() < 0.6;
            const progress = finished ? 100 : round1(70 + rng() * 29);

            return {
                userId: player.userId,
                displayName: player.displayName,
                wpm,
                accuracy,
                progress,
                finished,
                score: Math.round((wpm * accuracy) / 100 * 10),
            };
        })
        .sort((a, b) => b.score - a.score || a.userId.localeCompare(b.userId));

    const hoursAgo = index * HOURS_BETWEEN_MATCHES + rng() * 5;
    const startedAt = new Date(Math.floor((now.getTime() - hoursAgo * 3_600_000) / 1000) * 1000);
    const finishedAt = new Date(startedAt.getTime() + (TIME_LIMIT_SECONDS + Math.floor(rng() * 4)) * 1000);
    const textNumber = 1 + Math.floor(rng() * 10);

    return {
        matchId: matchIdFor(index),
        gameType: GAME_TYPE,
        players: ranking.map(({ userId, displayName, score }) => ({ userId, displayName, score })),
        startedAt: toIsoUtc(startedAt),
        finishedAt: toIsoUtc(finishedAt),
        winnerUserId: ranking[0].userId,
        metadata: {
            source: 'data-generator',
            textId: `t-${String(textNumber).padStart(2, '0')}`,
            timeLimitSeconds: TIME_LIMIT_SECONDS,
            players: ranking.map(({ userId, wpm, accuracy, progress, finished }) => ({
                userId,
                wpm,
                accuracy,
                progress,
                finished,
            })),
        },
    };
}

function shuffle(items, rng) {
    const copy = [...items];

    for (let i = copy.length - 1; i > 0; i--) {
        const j = Math.floor(rng() * (i + 1));
        [copy[i], copy[j]] = [copy[j], copy[i]];
    }

    return copy;
}

function hash(text) {
    let value = 0;

    for (const char of text) {
        value = (value * 31 + char.charCodeAt(0)) >>> 0;
    }

    return value;
}

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

function round1(value) {
    return Math.round(value * 10) / 10;
}

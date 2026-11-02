// Configuración del generador. Todo llega por variables de entorno (R-09): el código no trae valores por defecto.

const MAX_MATCHES = 500;

export class ConfigError extends Error {}

/** Valida las variables de entorno. Si alguna falta o es inválida, lanza un ConfigError con todos los problemas. */
export function readConfig(env) {
    const errors = [];

    const apiUrl = (env.DATA_API_URL ?? '').trim().replace(/\/+$/, '');
    if (!apiUrl) {
        errors.push('DATA_API_URL es obligatoria: URL base de la API, por ejemplo http://api:8080');
    }

    const matches = toInteger(env.DATA_MATCHES);
    if (matches === null || matches < 1 || matches > MAX_MATCHES) {
        errors.push(`DATA_MATCHES debe ser un entero entre 1 y ${MAX_MATCHES}`);
    }

    const waitSeconds = toInteger(env.DATA_WAIT_SECONDS);
    if (waitSeconds === null || waitSeconds < 1) {
        errors.push('DATA_WAIT_SECONDS debe ser un entero mayor o igual a 1');
    }

    const extraPlayers = parseExtraPlayers(env.DATA_EXTRA_PLAYERS, errors);

    if (errors.length > 0) {
        throw new ConfigError(errors.join('\n'));
    }

    return { apiUrl, matches, waitSeconds, extraPlayers };
}

function toInteger(value) {
    const text = (value ?? '').trim();
    return /^\d+$/.test(text) ? Number(text) : null;
}

/**
 * "id=Nombre;otro-id=Otro Nombre" → [{ userId, displayName }]. Los ids de Auth0 traen "|", por eso se separa
 * en el primer "=". Sirve para que usuarios reales de la demostración tengan historial.
 */
export function parseExtraPlayers(text, errors = []) {
    const players = [];

    for (const entry of (text ?? '').split(';').map((item) => item.trim()).filter(Boolean)) {
        const separator = entry.indexOf('=');
        const userId = separator > 0 ? entry.slice(0, separator).trim() : '';
        const displayName = separator > 0 ? entry.slice(separator + 1).trim() : '';

        if (!userId || !displayName) {
            errors.push(`DATA_EXTRA_PLAYERS: "${entry}" no tiene el formato id=Nombre`);
            continue;
        }

        players.push({ userId, displayName });
    }

    return players;
}

/**
 * URL base de la API y del hub de Typing Battle.
 *
 * Se fija al compilar con la variable de entorno TYPING_API_URL
 * (webpack la inyecta como __TYPING_API_URL__). Es obligatoria: no hay
 * un valor por defecto, y sin ella este módulo falla al cargarse.
 *
 *   TYPING_API_URL=https://typing.mi-dominio.com npm run build
 */
declare const __TYPING_API_URL__: string | undefined;

const configured =
    typeof __TYPING_API_URL__ === 'string'
        ? __TYPING_API_URL__.trim()
        : '';

if (!configured) {
    throw new Error(
        'Falta la variable de entorno TYPING_API_URL: la URL de la API de Typing se fija al compilar el frontend.'
    );
}

export const API_BASE_URL = configured.replace(/\/+$/, '');

export const RESULTS_API_URL = `${API_BASE_URL}/api/games/typing`;

export const TYPING_HUB_URL = `${API_BASE_URL}/hubs/typing`;

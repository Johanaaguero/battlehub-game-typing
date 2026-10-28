/**
 * URL base de la API y del hub de Typing Battle.
 *
 * Se fija al compilar con la variable de entorno TYPING_API_URL
 * (webpack la inyecta como __TYPING_API_URL__). Sin ella se usa
 * la API local: http://localhost:5015.
 *
 *   TYPING_API_URL=https://typing.mi-dominio.com npm run build
 */
declare const __TYPING_API_URL__: string | undefined;

const DEFAULT_API_URL = 'http://localhost:5015';

const configured =
    typeof __TYPING_API_URL__ === 'string'
        ? __TYPING_API_URL__.trim()
        : '';

export const API_BASE_URL = (configured || DEFAULT_API_URL).replace(/\/+$/, '');

export const RESULTS_API_URL = `${API_BASE_URL}/api/games/typing`;

export const TYPING_HUB_URL = `${API_BASE_URL}/hubs/typing`;

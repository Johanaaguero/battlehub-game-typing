import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { ConfigError, parseExtraPlayers, readConfig } from '../src/config.js';

const VALID = {
    DATA_API_URL: 'http://api:8080',
    DATA_MATCHES: '30',
    DATA_WAIT_SECONDS: '60',
    DATA_EXTRA_PLAYERS: '',
};

describe('configuración del generador', () => {
    it('lee las variables de entorno', () => {
        assert.deepEqual(readConfig(VALID), {
            apiUrl: 'http://api:8080',
            matches: 30,
            waitSeconds: 60,
            extraPlayers: [],
        });
    });

    it('quita la barra final de la URL de la API', () => {
        assert.equal(readConfig({ ...VALID, DATA_API_URL: 'http://api:8080//' }).apiUrl, 'http://api:8080');
    });

    it('no tiene valores por defecto: sin variables, informa todas las que faltan', () => {
        assert.throws(() => readConfig({}), (error) => {
            assert.ok(error instanceof ConfigError);
            assert.match(error.message, /DATA_API_URL/);
            assert.match(error.message, /DATA_MATCHES/);
            assert.match(error.message, /DATA_WAIT_SECONDS/);
            return true;
        });
    });

    for (const [name, value] of [
        ['DATA_MATCHES', '0'],
        ['DATA_MATCHES', '501'],
        ['DATA_MATCHES', 'muchas'],
        ['DATA_MATCHES', '-3'],
        ['DATA_WAIT_SECONDS', '0'],
        ['DATA_WAIT_SECONDS', '1.5'],
    ]) {
        it(`rechaza ${name}=${value}`, () => {
            assert.throws(() => readConfig({ ...VALID, [name]: value }), new RegExp(name));
        });
    }

    it('lee los jugadores extra', () => {
        const config = readConfig({ ...VALID, DATA_EXTRA_PLAYERS: 'auth0|1=Ana Demo; auth0|2=Luis Demo' });

        assert.deepEqual(config.extraPlayers, [
            { userId: 'auth0|1', displayName: 'Ana Demo' },
            { userId: 'auth0|2', displayName: 'Luis Demo' },
        ]);
    });

    it('rechaza un jugador extra sin el formato id=Nombre', () => {
        assert.throws(() => readConfig({ ...VALID, DATA_EXTRA_PLAYERS: 'sin-nombre' }), /DATA_EXTRA_PLAYERS/);
        assert.throws(() => readConfig({ ...VALID, DATA_EXTRA_PLAYERS: '=Sin id' }), /DATA_EXTRA_PLAYERS/);
    });
});

describe('parseExtraPlayers', () => {
    it('separa en el primer "=" y respeta el resto del nombre', () => {
        assert.deepEqual(parseExtraPlayers('id=a=b'), [{ userId: 'id', displayName: 'a=b' }]);
    });

    it('acepta un texto vacío o ausente', () => {
        assert.deepEqual(parseExtraPlayers(''), []);
        assert.deepEqual(parseExtraPlayers(undefined), []);
        assert.deepEqual(parseExtraPlayers(' ; ; '), []);
    });
});

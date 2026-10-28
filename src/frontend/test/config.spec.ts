type ConfigScope = { __TYPING_API_URL__?: string };

describe('config (URL de la API, sin valor por defecto)', () => {
  const scope = globalThis as ConfigScope;
  const original = scope.__TYPING_API_URL__;

  afterEach(() => {
    scope.__TYPING_API_URL__ = original;
    jest.resetModules();
  });

  it('arma las URLs de la API y del hub con TYPING_API_URL, sin la barra final', async () => {
    scope.__TYPING_API_URL__ = 'https://typing.test/';
    jest.resetModules();

    const config = await import('../src/config');

    expect(config.API_BASE_URL).toBe('https://typing.test');
    expect(config.RESULTS_API_URL).toBe('https://typing.test/api/games/typing');
    expect(config.TYPING_HUB_URL).toBe('https://typing.test/hubs/typing');
  });

  it.each(['', '   ', undefined])('falla al cargarse si falta TYPING_API_URL (%p)', async (value) => {
    scope.__TYPING_API_URL__ = value;
    jest.resetModules();

    await expect(import('../src/config')).rejects.toThrow('TYPING_API_URL');
  });
});

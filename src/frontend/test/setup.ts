import { BrowserPlatform } from '@aurelia/platform-browser';
import { setPlatform, onFixtureCreated, type IFixture } from '@aurelia/testing';

// URL de la API para las pruebas: en el build real la inyecta webpack desde TYPING_API_URL (src/config.ts).
// Aquí es un valor ficticio; las pruebas arman sus URLs a partir de src/config.ts.
(globalThis as { __TYPING_API_URL__?: string }).__TYPING_API_URL__ = 'http://typing-api.test';

// Sets up the Aurelia environment for testing
function bootstrapTextEnv() {
  const platform = new BrowserPlatform(window);
  setPlatform(platform);
  BrowserPlatform.set(globalThis, platform);
}

const fixtures: IFixture<object>[] = [];
beforeAll(() => {
  bootstrapTextEnv();
  onFixtureCreated(fixture => {
    fixtures.push(fixture);
  });
});

afterEach(() => {
  fixtures.forEach(async f => {
    try {
      await f.stop(true);
    } catch {
      // ignore
    }
  });
  fixtures.length = 0;
});

import { createFixture } from '@aurelia/testing';
import { MyApp } from '../src/my-app';

describe('my-app (playground)', () => {
  async function renderPlayground(): Promise<HTMLElement> {
    const { appHost } = await createFixture('<my-app></my-app>', {}, [MyApp]).started;
    return appHost;
  }

  it('muestra el juego de Typing y nada más: sin texto de la plantilla de Aurelia', async () => {
    const appHost = await renderPlayground();

    expect(appHost.querySelector('typing-game-module')).not.toBeNull();
    expect(appHost.textContent).not.toContain('Hello World');
    expect(appHost.querySelector('.message')).toBeNull();
  });

  it('se puede jugar sin Shell: ofrece iniciar la práctica local', async () => {
    const appHost = await renderPlayground();

    expect(appHost.textContent).toContain('INICIAR PRÁCTICA');
  });
});

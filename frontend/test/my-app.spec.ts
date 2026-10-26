
import { MyApp } from '../src/my-app';
import { createFixture } from '@aurelia/testing';

describe('my-app', () => {
  it('should render message', async () => {
    const { appHost } = await createFixture(
      '<my-app></my-app>',
      {},
      [MyApp],
    ).started;

    const message = appHost.querySelector('.message');

    expect(message?.textContent?.trim()).toBe('Hello World!');
  });
});

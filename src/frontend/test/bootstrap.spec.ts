// Aurelia se reemplaza: lo que importa es qué aplicación arranca el playground (R-34), no el framework.
const mockStart = jest.fn();
const mockApp = jest.fn<{ start: jest.Mock }, [unknown]>(() => ({ start: mockStart }));

// Solo se reemplaza Aurelia.app(): la plantilla de MyApp carga el módulo del juego, que necesita el resto del framework.
jest.mock('aurelia', () => ({
    ...jest.requireActual('aurelia'),
    __esModule: true,
    default: { app: mockApp },
}));

describe('arranque del playground', () => {
    beforeEach(() => {
        jest.resetModules();
        jest.clearAllMocks();
    });

    it('bootstrap arranca Aurelia con MyApp', async () => {
        const { MyApp } = await import('../src/my-app');

        await import('../src/bootstrap');

        expect(mockApp).toHaveBeenCalledTimes(1);
        expect(mockApp).toHaveBeenCalledWith(MyApp);
        expect(mockStart).toHaveBeenCalledTimes(1);
    });

    it('main carga bootstrap con un import dinámico, el límite asíncrono que Module Federation necesita', async () => {
        await import('../src/main');
        await new Promise((resolve) => setTimeout(resolve, 0));

        expect(mockApp).toHaveBeenCalledTimes(1);
        expect(mockStart).toHaveBeenCalledTimes(1);
    });
});

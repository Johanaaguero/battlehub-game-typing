import { TYPING_TEXTS, randomTypingText, textForMatch } from '../src/typing-texts';

describe('textos de la partida', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('hay al menos 6 textos, todos distintos y de longitud parecida', () => {
    expect(TYPING_TEXTS.length).toBeGreaterThanOrEqual(6);
    expect(new Set(TYPING_TEXTS).size).toBe(TYPING_TEXTS.length);
    TYPING_TEXTS.forEach((text) => {
      expect(text.length).toBeGreaterThanOrEqual(100);
      expect(text.length).toBeLessThanOrEqual(150);
      expect(text).toBe(text.trim());
    });
  });

  it('la misma partida siempre da el mismo texto, y las partidas se reparten entre varios textos', () => {
    expect(textForMatch('match-001')).toBe(textForMatch('match-001'));

    const usados = new Set(Array.from({ length: 80 }, (_, i) => textForMatch(`match-${i}`)));

    expect(usados.size).toBeGreaterThanOrEqual(5);
    usados.forEach((text) => expect(TYPING_TEXTS).toContain(text));
  });

  it('el texto al azar nunca repite el anterior, sea cual sea el sorteo', () => {
    TYPING_TEXTS.forEach((anterior) => {
      [0, 0.25, 0.5, 0.75, 0.999999].forEach((sorteo) => {
        jest.spyOn(Math, 'random').mockReturnValue(sorteo);

        const nuevo = randomTypingText(anterior);

        expect(TYPING_TEXTS).toContain(nuevo);
        expect(nuevo).not.toBe(anterior);
      });
    });
  });

  it('sin texto anterior elige cualquiera de la lista', () => {
    jest.spyOn(Math, 'random').mockReturnValue(0);

    expect(randomTypingText()).toBe(TYPING_TEXTS[0]);
  });

  it('con sorteos reales usa varios textos distintos', () => {
    const vistos = new Set<string>();
    let texto = TYPING_TEXTS[0];

    for (let i = 0; i < 200; i++) {
      texto = randomTypingText(texto);
      vistos.add(texto);
    }

    expect(vistos.size).toBe(TYPING_TEXTS.length);
  });
});

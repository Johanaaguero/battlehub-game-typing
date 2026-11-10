/**
 * Textos que se escriben en una partida. Todos son distintos y de longitud parecida (unos 120 caracteres),
 * para que quepan en el área del juego y se puedan terminar en los 60 segundos.
 */
export const TYPING_TEXTS: readonly string[] = [
    'La tecnología permite conectar personas, compartir información y crear soluciones para los desafíos del mundo moderno.',
    'Programar es el arte de dividir un problema grande en pasos pequeños y ordenados que una computadora pueda seguir sin equivocarse.',
    'Cada error es una oportunidad para aprender, y cada línea de código bien escrita acerca el proyecto un poco más a su meta.',
    'Escribir rápido no sirve de nada si no se escribe bien: la precisión y la constancia marcan la diferencia en cada partida.',
    'Un buen equipo comparte ideas, revisa el trabajo de los demás y celebra los avances, por pequeños que parezcan al principio.',
    'La paciencia y la práctica diaria transforman a cualquier principiante en una persona capaz de dominar el teclado con soltura.',
    'Las pruebas automáticas vigilan el código en todo momento y avisan a tiempo cuando un cambio rompe algo que antes funcionaba.',
    'En Costa Rica, el café de la mañana, el sol de la tarde y una buena conversación hacen que cualquier jornada de estudio sea mejor.',
];

/**
 * Texto de una partida de sala. Se calcula solo con el id de la partida, así todos los jugadores de la misma
 * sala escriben el mismo texto sin que el servidor tenga que repartirlo, y cada partida nueva cambia de texto.
 */
export function textForMatch(matchId: string): string {
    let hash = 5381;

    for (let i = 0; i < matchId.length; i++) {
        hash = (hash * 33 + matchId.charCodeAt(i)) % 2_147_483_647;
    }

    return TYPING_TEXTS[hash % TYPING_TEXTS.length];
}

/**
 * Texto al azar para la práctica. Nunca repite el anterior, para que «Volver a jugar» siempre cambie de texto.
 */
export function randomTypingText(previous?: string): string {
    const options = TYPING_TEXTS.filter((text) => text !== previous);

    return options[Math.floor(Math.random() * options.length)];
}

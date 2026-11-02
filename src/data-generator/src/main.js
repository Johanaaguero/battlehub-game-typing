import { run } from './generator.js';

// El código de salida es el del generador: 0 si registró (o ya tenía) todas las partidas.
process.exitCode = await run(process.env);

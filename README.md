# BattleHub - Typing Battle

Microservicio del Equipo 4 responsable del juego **Typing Battle** dentro de la plataforma BattleHub. Implementa los contratos de [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), la fuente de verdad del proyecto.

## Responsabilidades

Este servicio será responsable de:

- Gestionar la lógica de las partidas de Typing Battle.
- Proporcionar comunicación en tiempo real mediante SignalR.
- Mantener persistencia propia de los resultados de las partidas.
- Exponer una API REST para resultados, historial y estadísticas.
- Integrarse con Matchmaking a través del Shell: el juego avisa el fin de la partida al Shell y este lo notifica a Matchmaking.
- Proporcionar el microfrontend correspondiente al juego.
- Incluir pruebas unitarias y de integración.
- Generar datos de ejemplo al levantar el compose, con un proceso aparte (R-28).

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET 10 (API REST; hub SignalR en `/hubs/typing`) |
| Base de datos | MySQL 8 con Entity Framework Core y migraciones ([ADR-005](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/adrs/ADR-005-motor-bd-juego-typing.md)) |
| Microfrontend | Aurelia con Module Federation |
| Datos de ejemplo | Node 22 sin dependencias, como servicio aparte del compose |
| CI | GitHub Actions |

## Estructura

```text
battlehub-game-typing/
├── .github/workflows/ci.yml           → pipeline de CI (build, pruebas unitarias y de integración con MySQL)
├── docs/
│   ├── api-resultados.md              → referencia de la API REST de resultados
│   ├── api-typing-hub.md              → contrato del hub SignalR /hubs/typing
│   ├── despliegue.md                  → servicios, dependencias y estrategia de despliegue
│   ├── docker.md                      → guía para construir, levantar y comprobar los contenedores
│   └── seguridad.md                   → auditoría de secretos y reglas para manejarlos
├── src/
│   ├── backend/
│   │   └── TypingBattle.Api/
│   │       ├── Common/                → utilidades (fechas UTC)
│   │       ├── Persistence/           → DbContext MySQL, entidades y Migrations/
│   │       └── Results/               → validación, servicio y endpoints de resultados, historial y estadísticas
│   ├── data-generator/                → generador de datos de ejemplo (Node 22): registra partidas con POST /results, con su Dockerfile y pruebas
│   └── frontend/                      → microfrontend de Typing (Aurelia 2 y Module Federation), con su Dockerfile y nginx.conf
├── tests/
│   ├── TypingBattle.UnitTests/        → pruebas unitarias (Category=Unit)
│   └── TypingBattle.IntegrationTests/ → pruebas de integración contra MySQL real (Category=Integration)
├── docker-compose.yml                 → MySQL, API, microfrontend y datos de ejemplo en local (configuración en .env, plantilla .env.example)
├── dotnet-tools.json                  → herramienta local dotnet-ef
├── .gitignore
├── README.md
└── TypingBattle.slnx
```

## Backend: cómo correrlo localmente

Requisitos: el [SDK de .NET 10](https://dotnet.microsoft.com/download) y un MySQL 8. La forma más simple de tener MySQL es Docker, con el `.env` ya creado (ver [Variables de `.env`](#variables-de-env)):

```bash
docker compose up -d mysql
```

Eso levanta MySQL 8.4 en el puerto `MYSQL_PORT`, con la base, el usuario y la contraseña de tu `.env`. Si prefieren un MySQL instalado en su máquina, creen esa base y ese usuario.

El código y los `appsettings` no traen cadena de conexión ni orígenes de CORS: hay que definirlos como variables de entorno (ver [Configuración](#configuración-variables-de-entorno)). En PowerShell, con el MySQL de arriba:

```powershell
$env:ConnectionStrings__Typing = "Server=localhost;Port=3306;Database=<MYSQL_DATABASE>;User ID=<MYSQL_USER>;Password=<MYSQL_PASSWORD>"
$env:Cors__AllowedOrigins__0 = "<origen del Shell>"
```

```bash
dotnet run --project src/backend/TypingBattle.Api
```

La API queda en `http://localhost:5015`. En Development aplica las migraciones pendientes al arrancar (`Database:MigrateOnStartup`). Comprobación de salud en `/health` y documento OpenAPI en `/openapi/v1.json`. Los orígenes que el navegador puede usar para llamar a la API se configuran en `Cors:AllowedOrigins`.

La API no pide autenticación (ver [Identidad del jugador](#identidad-del-jugador)), así que se puede llamar directamente:

```bash
curl http://localhost:5015/api/games/typing/players/user-001/stats
```

La API completa, con ejemplos, está en [`docs/api-resultados.md`](docs/api-resultados.md).

### Identidad del jugador

El juego no tiene contacto con Auth0 (R-30). El Shell inicia la sesión del usuario, decide si se muestra el juego y le entrega al microfrontend el `currentUser` (`id` y `displayName`) en el contexto. Esa decisión tiene estas consecuencias:

- La API y el hub no piden credenciales: no hay tokens, encabezados de identidad ni permisos.
- El jugador es el `currentUser` que el microfrontend envía en cada petición del hub. El hub solo exige que venga informado.
- Solo los jugadores que se unieron a una partida pueden enviar métricas o terminarla.
- Por la misma razón, nada en la API impide que alguien con acceso a la red se haga pasar por otro jugador o registre un resultado con `POST /results`. Se acepta porque la API solo se publica detrás del Shell y dentro de la red del compose; si algún día se expusiera a internet, habría que protegerla en el borde (un proxy o una puerta de enlace) y no en el juego.
- **Pendiente:** el Equipo 3 pide acordar cómo se protege la API y el hub antes de habilitar partidas públicas, porque `currentUser` no acredita identidad ante el servidor. La práctica no depende de esto: no usa la API ni el hub.

### Configuración (variables de entorno)

No hay valores por defecto en el código, en los `appsettings` ni en `docker-compose.yml`: todo llega por variables de entorno. Para Docker Compose salen del `.env` de cada uno, cuya plantilla es `.env.example` (ver [Variables de `.env`](#variables-de-env)).

| Variable de entorno | Para qué |
|---|---|
| `ConnectionStrings__Typing` | Cadena de conexión a MySQL. Obligatoria: la API y `dotnet ef` no arrancan sin ella (en Docker la arma el compose) |
| `Database__MigrateOnStartup` | `true` para aplicar las migraciones al arrancar (por defecto `false`) |
| `Cors__AllowedOrigins__0`, `__1`… | Orígenes del Shell y del microfrontend que pueden llamar a la API. Sin ellos, el navegador bloquea las llamadas desde otro origen |
| `TYPING_TEST_MYSQL` | Servidor MySQL de las pruebas de integración, sin `Database` (ver [Pruebas](#pruebas)). Obligatoria para correrlas |
| `TYPING_API_URL` | URL de la API, fija al compilar el frontend. Obligatoria: `npm start`, `npm run build` y el build de Docker fallan sin ella |

### Migraciones

El esquema solo cambia con migraciones de EF Core. La herramienta `dotnet-ef` está fijada en `dotnet-tools.json`:

```bash
dotnet tool restore
```

Todos los comandos `dotnet ef` necesitan `ConnectionStrings__Typing` (ver [Configuración](#configuración-variables-de-entorno)). Crear una migración no se conecta a MySQL, así que basta una cadena sin credenciales; aplicarlas sí se conecta.

Crear una migración después de cambiar las entidades o el `TypingDbContext`:

```bash
dotnet ef migrations add NombreDelCambio --project src/backend/TypingBattle.Api --output-dir Persistence/Migrations
```

Aplicarlas a la base de `ConnectionStrings__Typing`:

```bash
dotnet ef database update --project src/backend/TypingBattle.Api
```

El CI falla si el modelo cambió y falta la migración (`dotnet ef migrations has-pending-model-changes`).

### Pruebas

Las mismas que corre el CI:

```bash
dotnet test --filter "Category=Unit"
```

```bash
dotnet test --filter "Category=Integration"
```

Las de integración levantan la API completa contra MySQL real: cada clase crea su propia base `typing_test_…`, le aplica las migraciones y la borra al terminar. Necesitan la variable `TYPING_TEST_MYSQL`, con una cadena sin `Database` y un usuario que pueda crear y borrar bases. Con el MySQL de docker compose (`docker compose up -d mysql`), en PowerShell:

```powershell
$env:TYPING_TEST_MYSQL = "Server=localhost;Port=3306;User ID=root;Password=<MYSQL_ROOT_PASSWORD>;SslMode=Disabled"
```

`SslMode=Disabled` evita un error intermitente del conector MySql.Data al negociar TLS en varias conexiones a la vez ([bug #111759](https://bugs.mysql.com/bug.php?id=111759)). Para un MySQL local de pruebas no hace falta TLS.

### Uso desde el hub

El hub `/hubs/typing` corre en el mismo proceso y guarda el resultado al terminar la partida inyectando `IResultsService` (ver [`docs/api-resultados.md`](docs/api-resultados.md#desde-el-backend-del-hub-sin-http)).

## Frontend: cómo correrlo localmente

Requisitos: Node 22 LTS (`src/frontend/.nvmrc`, ADR-003). Desde `src/frontend/`:

```bash
npm ci
```

```bash
npm start
```

El remote queda en `http://localhost:4001/remoteEntry.js` para que el Shell lo cargue. Las mismas verificaciones que corre el CI:

```bash
npm test
```

```bash
npm run build
```

`npm test` corre primero el lint (ESLint y Stylelint) y luego las pruebas de Jest.

- **URL de la API.** Se fija al compilar con la variable `TYPING_API_URL`, que es obligatoria: sin ella fallan `npm start`, `npm run build` y el build de Docker. Por ejemplo, `TYPING_API_URL=https://typing.mi-dominio.com npm run build`.
- **Identidad.** El juego no usa tokens ni habla con Auth0: manda el `currentUser` que le entrega el Shell en el contexto (ver [Identidad del jugador](#identidad-del-jugador)).
- **Estilos.** Todas las clases llevan el prefijo `typing-game` y las variables CSS se definen en la raíz del juego (`.typing-game`), así que no alteran el layout del Shell (ADR-003 §8).
- **Altura.** La raíz del juego toma la altura que le asigna el Shell. A 1366x768 le quedan unos 495 px en la práctica y 550 px en una partida, y entra sin scroll de página ni del juego (R-33). El historial, las estadísticas y las clasificaciones muy largas hacen scroll dentro de su propio panel.

## Todo con Docker Compose

Para levantar MySQL, la API, el microfrontend y los datos de ejemplo solo con Docker, sin instalar .NET ni Node. La primera vez, crear el `.env` desde la plantilla (en PowerShell, `Copy-Item .env.example .env`):

```bash
cp .env.example .env
```

```bash
docker compose up -d --build
```

| Servicio | Contenedor | URL local |
|---|---|---|
| `mysql` | `typing-battle-mysql` | `localhost:3306` |
| `api` | `typing-battle-api` | `http://localhost:5015` (`/health`, `/api/games/typing`, `/hubs/typing`) |
| `frontend` | `typing-battle-frontend` | `http://localhost:4001/remoteEntry.js` |
| `data-generator` | `typing-battle-data-generator` | Sin puerto: registra las partidas de ejemplo y termina (`Exited (0)`) |

- **Mismos puertos que `dotnet run` y `npm start`:** el Shell carga el juego sin cambiar nada, pero no se pueden correr a la vez que esos comandos.
- **La API corre en Production:** aplica las migraciones al arrancar. No pide autenticación: el jugador es el `currentUser` que le envía el juego.
- **Matchmaking:** la API no lo llama (R-46). El fin de la partida lo avisa el Shell.
- `docker compose up -d mysql` sigue levantando solo MySQL, para usar `dotnet run` o correr las pruebas.
- **Datos de ejemplo:** al terminar de arrancar la API, el servicio `data-generator` registra partidas de ejemplo (ver [Datos de ejemplo](#datos-de-ejemplo)).

```bash
docker compose logs -f api
```

```bash
docker compose down
```

La guía completa, con las comprobaciones, cómo actualizar, cómo borrar los datos y los problemas comunes, está en [`docs/docker.md`](docs/docker.md).

### Variables de `.env`

Docker Compose lee la configuración de un archivo `.env` en la raíz del repo, que no se versiona. Es obligatorio: `docker-compose.yml` no tiene valores por defecto y, si falta una variable, se detiene con un mensaje como `required variable MYSQL_PASSWORD is missing a value`. La plantilla es `.env.example` y sus valores sirven para desarrollo local:

```bash
cp .env.example .env
```

| Variable | Valor en `.env.example` | Para qué |
|---|---|---|
| `MYSQL_ROOT_PASSWORD` | `typing_root` | Contraseña de `root`. La usan las pruebas de integración |
| `MYSQL_DATABASE` | `typing_battle` | Base de datos de la API |
| `MYSQL_USER` | `typing` | Usuario con el que se conecta la API |
| `MYSQL_PASSWORD` | `typing_dev` | Contraseña de ese usuario |
| `MYSQL_PORT` | `3306` | Puerto de MySQL en tu máquina |
| `API_PORT` | `5015` | Puerto de la API en tu máquina |
| `CORS_SHELL_ORIGIN`, `CORS_FRONTEND_ORIGIN` | `http://localhost:4000` y `http://localhost:4001` | Orígenes del navegador que pueden llamar a la API |
| `FRONTEND_PORT` | `4001` | Puerto del microfrontend en tu máquina; el Shell lo busca en `http://localhost:4001/remoteEntry.js` |
| `TYPING_API_URL` | `http://localhost:5015` | URL de la API que usa el frontend, con el puerto de `API_PORT`. Queda fija al construir la imagen: al cambiarla, `docker compose up -d --build frontend` |
| `DATA_MATCHES` | `30` | Cuántas partidas de ejemplo registra el generador (de 1 a 500) |
| `DATA_WAIT_SECONDS` | `60` | Segundos que el generador espera a que la API responda antes de rendirse |
| `DATA_EXTRA_PLAYERS` | vacío | Usuarios reales de la demostración que también tendrán historial, como `id=Nombre` separados por `;`. Puede quedar vacía, pero la línea debe existir |

Si ya tenías un `.env` de antes, agrégale las tres variables `DATA_` de `.env.example`: sin ellas, `docker compose` se detiene y dice cuál falta.

Al cambiar las variables de MySQL:

- MySQL las aplica una sola vez, al crear el volumen de datos. Para que tomen efecto hay que borrar los datos con `docker compose down -v` y volver a levantarlo.
- Si cambian `MYSQL_ROOT_PASSWORD`, las pruebas de integración necesitan `TYPING_TEST_MYSQL` con la contraseña nueva (ver [Pruebas](#pruebas)).
- `dotnet run` y `dotnet ef` no leen `.env`: necesitan la variable `ConnectionStrings__Typing` con los valores de `MYSQL_DATABASE`, `MYSQL_USER` y `MYSQL_PASSWORD`.

### Datos de ejemplo

Con la base vacía, el historial y las estadísticas no muestran nada. Por eso el compose incluye el servicio `data-generator` (R-28), un proceso aparte que **no usa el código de la API, ni sus migraciones, ni un seed**: espera a que la API responda `/health` y registra partidas de ejemplo con el mismo `POST /api/games/typing/results` que usa cualquiera. Su código está en `src/data-generator/`.

- **Qué crea:** `DATA_MATCHES` partidas (30 por defecto) de 2 a 4 jugadores, con ids fijos `demo-0001`, `demo-0002`…, repartidas en las últimas semanas (una cada 17 horas aproximadamente), con wpm y precisión por jugador. Los jugadores son 8: `demo-ana`, `demo-luis`, `demo-sofia`, `demo-carlos`, `demo-maria`, `demo-diego`, `demo-valeria` y `demo-mateo`. Los datos son siempre los mismos, salvo las fechas.
- **No duplica:** como los ids son fijos, si una partida ya existe la API responde `409` y el generador la ignora. Volver a correr `docker compose up -d` no agrega nada.
- **Cuándo termina:** al registrar las partidas, con código 0. Queda como `Exited (0)` en `docker compose ps`; es lo esperado.
- **Usuarios reales:** si el Equipo 1 comparte los ids de los usuarios de la demostración, ponlos en `DATA_EXTRA_PLAYERS` (por ejemplo `auth0|64f0c1=Ana Demo;auth0|7a91b2=Luis Demo`) para que su historial muestre datos.

Para comprobar los datos:

```bash
docker compose logs data-generator
```

```bash
curl "http://localhost:5015/api/games/typing/players/demo-ana/history?limit=3"
```

```bash
curl http://localhost:5015/api/games/typing/players/demo-ana/stats
```

Para empezar de cero, borrando también los datos de MySQL (la próxima vez que se levante, se vuelven a generar):

```bash
docker compose down -v
```

También se puede correr sin Docker contra una API ya levantada, con Node 22 y desde `src/data-generator/`. En PowerShell:

```powershell
$env:DATA_API_URL = "http://localhost:5015"; $env:DATA_MATCHES = "30"; $env:DATA_WAIT_SECONDS = "60"; $env:DATA_EXTRA_PLAYERS = ""
```

```bash
npm start
```

Las pruebas del generador (`npm test`) no necesitan la API: usan una falsa.

## Integración con los otros equipos

Puertos locales del proyecto:

| Servicio | Equipo | URL local |
|---|---|---|
| Shell | 3 | `http://localhost:4000` |
| Microfrontend de Typing (remote `typingGame`) | 4 | `http://localhost:4001/remoteEntry.js` |
| API y hub de Typing | 4 | `http://localhost:5015` (`/api/games/typing`, `/hubs/typing`) |
| Matchmaking | 2 | `http://localhost:5211` |
| Profile | 1 | `http://localhost:5220` |

**Shell (Equipo 3).** El microfrontend sigue ADR-003: remote `typingGame`, módulo `./GameModule`, puerto 4001 y el mismo `mf-shared.js` que el Shell. Para que el Shell lo cargue, su `config/remotes.local.json` necesita esta entrada:

```json
"typing": { "scope": "typingGame", "url": "http://localhost:4001/remoteEntry.js", "module": "./GameModule" }
```

Para probarlo juntos: MySQL (`docker compose up -d mysql`), la API (`dotnet run --project src/backend/TypingBattle.Api`), el remote (`npm start` en `src/frontend/`) y el Shell (`npm start` en su repo). Las tres primeras también se pueden levantar juntas con `docker compose up -d --build` (ver [Todo con Docker Compose](#todo-con-docker-compose)). La API ya permite los orígenes `http://localhost:4000` y `http://localhost:4001`.

El contrato está en el README y en `src/games/game-contracts.ts`, `game-events.ts` y `game-host.ts` del [PR #6 del Shell](https://github.com/KeynerMC/battlehub-shell/pull/6) (referencia `83e5401`). Typing ya lo implementa, así que necesita esa versión del Shell: la de su `main` todavía no entrega `events` ni `mode`, y `initialize()` rechaza ese contexto.

#### Contexto que entrega el Shell

| Campo | Partida normal | Práctica |
|---|---|---|
| `gameType` | `typing` | `typing` |
| `currentUser` | `{ id, displayName }` | `{ id, displayName }` |
| `events` | `EventTarget` propio de la partida | `EventTarget` propio de la práctica |
| `matchId` | Id real de la sala | No existe |
| `mode` | No existe | `practice` |

`initialize()` rechaza un contexto con otro `gameType`, sin usuario, sin `events`, sin `mode` ni `matchId`, o con `mode: 'practice'` y `matchId`. El juego nunca inventa un `matchId` ni recibe tokens.

- **Práctica (R-44):** no conecta con SignalR ni llama a la API, no guarda nada y no emite el fin de partida. Permite escribir, ver el resultado local, volver a jugar, pausar y reanudar.
- **Playground (R-34):** sin Shell, el botón «Iniciar práctica» de la página del juego arma el mismo contexto de práctica con un jugador local.
- **Ciclo de vida:** el Shell llama a `initialize()` y luego a `start()`; el juego no escucha `battlehub:game-start`. `start()` también reanuda: no vuelve a conectar ni a unirse si la conexión sigue abierta, ni duplica el temporizador. `dispose()` detiene el temporizador y cierra la conexión.

**Matchmaking (Equipo 2).** Typing no llama a Matchmaking (R-46). Al terminar la partida, el juego avisa al Shell con un evento y el Shell notifica a Matchmaking; la API solo guarda el resultado en MySQL.

#### Evento de fin de partida

El microfrontend emite en `context.events` un `CustomEvent` llamado `battlehub:game-finished` cuando el servidor cierra una partida normal (el contrato está en `src/frontend/src/game-contracts.ts`). El Shell lo escucha y notifica a Matchmaking:

```ts
context.events.dispatchEvent(new CustomEvent('battlehub:game-finished', {
    detail: { matchId: context.matchId, gameType: context.gameType },
}));
```

- **Cuándo se emite:** una sola vez por partida, cuando el hub responde `created` o `already_exists`. No se emite en la práctica, ni al salir del juego antes de terminar, ni si el resultado fue inválido o hubo un error.
- **Qué lleva:** solo `matchId` y `gameType`. El ganador y los puntajes se quedan en la API de Typing; compartirlos con el Shell requiere acordarlo antes con el Equipo 3.
- **Una vez por jugador:** cada jugador tiene su propio navegador y su propio Shell, así que cada uno recibe el evento de la misma partida. El Shell (o Matchmaking) debe tolerar el aviso repetido de un mismo `matchId`.
- **Quién emite:** el juego solo emite el evento. Cómo se autentica el Shell con Matchmaking al notificar queda fuera de Typing.

**Profile y Auth0 (Equipo 1).** Typing no se conecta con Auth0 ni con Profile (R-30). El Shell inicia la sesión con Auth0, consulta al Profile Service qué juegos tiene habilitados el usuario y solo muestra Typing si corresponde; después le pasa el `currentUser` al microfrontend.

## Flujo de trabajo

- `main` está protegida: sin push directo; todo entra por Pull Request con CI en verde y al menos 1 aprobación.
- Commits y títulos de PR en formato semántico: `<tipo>(<alcance opcional>): <descripción en imperativo>`. Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`, `perf`.
- Las pruebas de .NET se etiquetan `[Trait("Category", "Unit")]` o `[Trait("Category", "Integration")]`, porque el CI las filtra por categoría.
- Todas las fechas y timestamps van en UTC, formato ISO-8601 con sufijo `Z`.
- Nada de secretos en el repo: usar variables de entorno.

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

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET 10 (API REST; hub SignalR en `/hubs/typing`) |
| Base de datos | MySQL 8 con Entity Framework Core y migraciones ([ADR-005](https://github.com/javiercoulon-public/battlehub-contracts/blob/main/adrs/ADR-005-motor-bd-juego-typing.md)) |
| Microfrontend | Aurelia con Module Federation |
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
│   └── backend/
│       └── TypingBattle.Api/
│           ├── Common/                → utilidades (fechas UTC)
│           ├── Persistence/           → DbContext MySQL, entidades y Migrations/
│           └── Results/               → validación, servicio y endpoints de resultados, historial y estadísticas
├── tests/
│   ├── TypingBattle.UnitTests/        → pruebas unitarias (Category=Unit)
│   └── TypingBattle.IntegrationTests/ → pruebas de integración contra MySQL real (Category=Integration)
├── docker-compose.yml                 → MySQL, API y microfrontend en local (configuración en .env, plantilla .env.example)
├── dotnet-tools.json                  → herramienta local dotnet-ef
├── .gitignore
├── README.md
└── TypingBattle.slnx
```

## Backend: cómo correrlo localmente

Requisitos: el [SDK de .NET 10](https://dotnet.microsoft.com/download) y un MySQL 8. La forma más simple de tener MySQL es Docker:

```bash
docker compose up -d mysql
```

Eso levanta MySQL 8.4 en `localhost:3306` con la base `typing_battle` y el usuario `typing` / `typing_dev` (credenciales solo de desarrollo, ya configuradas en `appsettings.Development.json`). Para cambiarlas, ver [Variables de `.env`](#variables-de-env). Si prefieren un MySQL instalado en su máquina, creen esa base y ese usuario, o cambien `ConnectionStrings:Typing`.

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

### Configuración fuera de Development

| Variable de entorno | Para qué |
|---|---|
| `ConnectionStrings__Typing` | Cadena de conexión a MySQL (obligatoria: la API no arranca sin ella) |
| `Database__MigrateOnStartup` | `true` para aplicar las migraciones al arrancar (por defecto `false`) |
| `Cors__AllowedOrigins__0`, `__1`… | Orígenes del Shell / microfrontend |

### Migraciones

El esquema solo cambia con migraciones de EF Core. La herramienta `dotnet-ef` está fijada en `dotnet-tools.json`:

```bash
dotnet tool restore
```

Crear una migración después de cambiar las entidades o el `TypingDbContext`:

```bash
dotnet ef migrations add NombreDelCambio --project src/backend/TypingBattle.Api --output-dir Persistence/Migrations
```

Aplicarlas a la base configurada (toma `ConnectionStrings__Typing` si existe; si no, el MySQL de docker-compose):

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

Las de integración levantan la API completa contra MySQL real: cada clase crea su propia base `typing_test_…`, le aplica las migraciones y la borra al terminar. Usan el MySQL de docker-compose; para otro servidor, configuren `TYPING_TEST_MYSQL` con una cadena sin `Database` y un usuario que pueda crear y borrar bases (por ejemplo `Server=localhost;Port=3306;User ID=root;Password=...`).

### Uso desde el hub

El hub `/hubs/typing` corre en el mismo proceso y guarda el resultado al terminar la partida inyectando `IResultsService` (ver [`docs/api-resultados.md`](docs/api-resultados.md#desde-el-backend-del-hub-sin-http)).

## Frontend: cómo correrlo localmente

Requisitos: Node 24 LTS (`frontend/.nvmrc`, ADR-003). Desde `frontend/`:

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

- **URL de la API.** Se fija al compilar con `TYPING_API_URL` (por defecto `http://localhost:5015`), por ejemplo `TYPING_API_URL=https://typing.mi-dominio.com npm run build`.
- **Identidad.** El juego no usa tokens ni habla con Auth0: manda el `currentUser` que le entrega el Shell en el contexto (ver [Identidad del jugador](#identidad-del-jugador)).
- **Estilos.** Todas las clases llevan el prefijo `typing-game` y las variables CSS se definen en la raíz del juego (`.typing-game`), así que no alteran el layout del Shell (ADR-003 §8).

## Todo con Docker Compose

Para levantar MySQL, la API y el microfrontend solo con Docker, sin instalar .NET ni Node:

```bash
docker compose up -d --build
```

| Servicio | Contenedor | URL local |
|---|---|---|
| `mysql` | `typing-battle-mysql` | `localhost:3306` |
| `api` | `typing-battle-api` | `http://localhost:5015` (`/health`, `/api/games/typing`, `/hubs/typing`) |
| `frontend` | `typing-battle-frontend` | `http://localhost:4001/remoteEntry.js` |

- **Mismos puertos que `dotnet run` y `npm start`:** el Shell carga el juego sin cambiar nada, pero no se pueden correr a la vez que esos comandos.
- **La API corre en Production:** aplica las migraciones al arrancar. No pide autenticación: el jugador es el `currentUser` que le envía el juego.
- **Matchmaking:** la API no lo llama (R-46). El fin de la partida lo avisa el Shell.
- `docker compose up -d mysql` sigue levantando solo MySQL, para usar `dotnet run` o correr las pruebas.

```bash
docker compose logs -f api
```

```bash
docker compose down
```

La guía completa, con las comprobaciones, cómo actualizar, cómo borrar los datos y los problemas comunes, está en [`docs/docker.md`](docs/docker.md).

### Variables de `.env`

Docker Compose lee la configuración de un archivo `.env` en la raíz del repo, que no se versiona. Una variable que falta o está vacía toma el valor de desarrollo, así que `.env` solo hace falta para cambiar algún valor o para poner un secreto. La plantilla es `.env.example`:

```bash
cp .env.example .env
```

| Variable | Valor de desarrollo | Para qué |
|---|---|---|
| `MYSQL_ROOT_PASSWORD` | `typing_root` | Contraseña de `root`. La usan las pruebas de integración |
| `MYSQL_DATABASE` | `typing_battle` | Base de datos de la API |
| `MYSQL_USER` | `typing` | Usuario con el que se conecta la API |
| `MYSQL_PASSWORD` | `typing_dev` | Contraseña de ese usuario |
| `CORS_SHELL_ORIGIN`, `CORS_FRONTEND_ORIGIN` | `http://localhost:4000` y `http://localhost:4001` | Orígenes del navegador que pueden llamar a la API |
| `TYPING_API_URL` | `http://localhost:5015` | URL de la API que usa el frontend. Queda fija al construir la imagen: al cambiarla, `docker compose up -d --build frontend` |

Al cambiar las variables de MySQL:

- MySQL las aplica una sola vez, al crear el volumen de datos. Para que tomen efecto hay que borrar los datos con `docker compose down -v` y volver a levantarlo.
- Si cambian `MYSQL_ROOT_PASSWORD`, las pruebas de integración necesitan `TYPING_TEST_MYSQL` con la contraseña nueva (ver [Pruebas](#pruebas)).
- Si cambian `MYSQL_DATABASE`, `MYSQL_USER` o `MYSQL_PASSWORD`, `dotnet run` necesita la variable `ConnectionStrings__Typing` con los valores nuevos, porque `appsettings.Development.json` trae los de desarrollo.

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

Para probarlo juntos: MySQL (`docker compose up -d mysql`), la API (`dotnet run --project src/backend/TypingBattle.Api`), el remote (`npm start` en `frontend/`) y el Shell (`npm start` en su repo). Las tres primeras también se pueden levantar juntas con `docker compose up -d --build` (ver [Todo con Docker Compose](#todo-con-docker-compose)). La API ya permite los orígenes `http://localhost:4000` y `http://localhost:4001`.

**Matchmaking (Equipo 2).** Typing no llama a Matchmaking (R-46). Al terminar la partida, el juego avisa al Shell con un evento y el Shell notifica a Matchmaking; la API solo guarda el resultado en MySQL.

#### Evento de fin de partida

El microfrontend emite en `window` un `CustomEvent` llamado `battlehub:game-finished` cuando el servidor cierra una partida real (el contrato está en `frontend/src/game-contracts.ts`). El Shell lo escucha y notifica a Matchmaking:

```ts
window.addEventListener('battlehub:game-finished', (event) => {
    const { matchId, gameType, winnerUserId, players, finishedAt } = (event as CustomEvent).detail;
    // notificar a Matchmaking el fin de la partida `matchId`
});
```

| Campo de `detail` | Contenido |
|---|---|
| `matchId`, `gameType` | Los del contexto que el Shell entregó al juego |
| `winnerUserId` | Ganador de la partida (`null` si este cliente no recibió el resultado) |
| `players` | `{ userId, displayName, score, position }` en orden de ranking; vacío si este cliente no recibió el resultado |
| `finishedAt` | Fin de la partida en UTC (ISO-8601 con `Z`); `null` si no recibió el resultado |

- **Cuándo se emite:** una sola vez por partida, cuando el hub responde `created` o `already_exists`. No se emite en el modo práctica ni si el resultado fue inválido o hubo un error.
- **Una vez por jugador:** cada jugador tiene su propio navegador y su propio Shell, así que cada uno recibe el evento de la misma partida. El Shell (o Matchmaking) debe tolerar el aviso repetido de un mismo `matchId`.
- **Quién emite:** el juego solo emite el evento. Cómo se autentica el Shell con Matchmaking al notificar queda fuera de Typing.

**Profile y Auth0 (Equipo 1).** Typing no se conecta con Auth0 ni con Profile (R-30). El Shell inicia la sesión con Auth0, consulta al Profile Service qué juegos tiene habilitados el usuario y solo muestra Typing si corresponde; después le pasa el `currentUser` al microfrontend.

## Flujo de trabajo

- `main` está protegida: sin push directo; todo entra por Pull Request con CI en verde y al menos 1 aprobación.
- Commits y títulos de PR en formato semántico: `<tipo>(<alcance opcional>): <descripción en imperativo>`. Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`, `perf`.
- Las pruebas de .NET se etiquetan `[Trait("Category", "Unit")]` o `[Trait("Category", "Integration")]`, porque el CI las filtra por categoría.
- Todas las fechas y timestamps van en UTC, formato ISO-8601 con sufijo `Z`.
- Nada de secretos en el repo: usar variables de entorno.

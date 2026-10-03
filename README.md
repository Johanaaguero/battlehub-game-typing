# BattleHub - Typing Battle

Microservicio del Equipo 4 responsable del juego **Typing Battle** dentro de la plataforma BattleHub. Implementa los contratos de [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), la fuente de verdad del proyecto.

## Responsabilidades

Este servicio será responsable de:

- Gestionar la lógica de las partidas de Typing Battle.
- Proporcionar comunicación en tiempo real mediante SignalR.
- Mantener persistencia propia de los resultados de las partidas.
- Exponer una API REST para resultados, historial y estadísticas.
- Integrarse con el sistema de Matchmaking de BattleHub.
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
│   └── api-resultados.md              → referencia de la API REST de resultados
├── src/
│   └── backend/
│       └── TypingBattle.Api/
│           ├── Common/                → utilidades (fechas UTC)
│           ├── Persistence/           → DbContext MySQL, entidades y Migrations/
│           └── Results/               → validación, servicio y endpoints de resultados, historial y estadísticas
├── tests/
│   ├── TypingBattle.UnitTests/        → pruebas unitarias (Category=Unit)
│   └── TypingBattle.IntegrationTests/ → pruebas de integración contra MySQL real (Category=Integration)
├── docker-compose.yml                 → MySQL local para desarrollo y pruebas
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

Eso levanta MySQL 8.4 en `localhost:3306` con la base `typing_battle` y el usuario `typing` / `typing_dev` (credenciales solo de desarrollo, ya configuradas en `appsettings.Development.json`). Si prefieren un MySQL instalado en su máquina, creen esa base y ese usuario, o cambien `ConnectionStrings:Typing`.

```bash
dotnet run --project src/backend/TypingBattle.Api
```

La API queda en `http://localhost:5015`. En Development aplica las migraciones pendientes al arrancar (`Database:MigrateOnStartup`). Comprobación de salud en `/health` y documento OpenAPI en `/openapi/v1.json`. Los orígenes que el navegador puede usar para llamar a la API se configuran en `Cors:AllowedOrigins`.

```bash
curl http://localhost:5015/api/games/typing/players/user-001/stats
```

La API completa, con ejemplos, está en [`docs/api-resultados.md`](docs/api-resultados.md).

### Configuración fuera de Development

| Variable de entorno | Para qué |
|---|---|
| `ConnectionStrings__Typing` | Cadena de conexión a MySQL (obligatoria: la API no arranca sin ella) |
| `Database__MigrateOnStartup` | `true` para aplicar las migraciones al arrancar (por defecto `false`) |
| `Cors__AllowedOrigins__0`, `__1`… | Orígenes del Shell / microfrontend |
| `Matchmaking__BaseUrl` | URL del Matchmaking Service para avisar el fin de partida (ADR-004). Vacía: no se avisa |
| `Matchmaking__Auth0__Domain`, `__ClientId`, `__ClientSecret`, `__Audience` | Cliente M2M de Auth0 con el permiso `matches.finish` (secretos: nunca en el repo) |

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

Para probarlo juntos: MySQL (`docker compose up -d mysql`), la API (`dotnet run --project src/backend/TypingBattle.Api`), el remote (`npm start` en `frontend/`) y el Shell (`npm start` en su repo). La API ya permite los orígenes `http://localhost:4000` y `http://localhost:4001`.

**Matchmaking (Equipo 2).** Al guardar el resultado, el backend avisa el fin de la partida con `POST /api/matches/{matchId}/finish` (ADR-004). Queda inactivo hasta configurar `Matchmaking__BaseUrl`; el detalle está en [`docs/api-typing-hub.md`](docs/api-typing-hub.md).

**Profile (Equipo 1).** El Shell consulta los juegos habilitados (`typing` requiere `games.typing.play`). Todavía no hay autenticación en la API ni en el hub de Typing: depende de que se acuerde cómo validan los juegos los tokens de Auth0 (ADR-007 deja fuera a los juegos).

## Flujo de trabajo

- `main` está protegida: sin push directo; todo entra por Pull Request con CI en verde y al menos 1 aprobación.
- Commits y títulos de PR en formato semántico: `<tipo>(<alcance opcional>): <descripción en imperativo>`. Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`, `perf`.
- Las pruebas de .NET se etiquetan `[Trait("Category", "Unit")]` o `[Trait("Category", "Integration")]`, porque el CI las filtra por categoría.
- Todas las fechas y timestamps van en UTC, formato ISO-8601 con sufijo `Z`.
- Nada de secretos en el repo: usar variables de entorno.

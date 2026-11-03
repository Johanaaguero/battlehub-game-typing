# BattleHub - Typing Battle

Microservicio del Equipo 4 responsable del juego **Typing Battle** dentro de la plataforma BattleHub. Sigue los contratos de [`battlehub-contracts`](https://github.com/javiercoulon-public/battlehub-contracts), la fuente de verdad del proyecto.

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
| Base de datos | MySQL 8.4 con Entity Framework Core y migraciones (ver [Base de datos](#base-de-datos)) |
| Microfrontend | Aurelia 2 con Module Federation (Webpack 5) y Node 22 |
| Datos de ejemplo | Node 22 sin dependencias, como servicio aparte del compose |
| Contenedores | Docker Compose |
| CI | GitHub Actions |

## Estructura

```text
battlehub-game-typing/
├── .github/
│   ├── actions/coverage-comment/      → acción que publica el comentario de cobertura en el PR
│   └── workflows/                     → CI - Backend, CI - Frontend, CI - Generador de datos y Build y pruebas unitarias
├── docs/
│   ├── api-resultados.md              → referencia de la API REST de resultados
│   ├── api-typing-hub.md              → contrato del hub SignalR /hubs/typing
│   ├── despliegue.md                  → propuesta de hosting para publicar Typing (sin confirmar; el despliegue no se evalúa)
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
│   ├── TypingBattle.UnitTests/        → pruebas unitarias con Moq, sin base de datos ni red (Category=Unit); sus carpetas (Common, Hubs, Persistence, Results) reflejan las de TypingBattle.Api
│   └── TypingBattle.IntegrationTests/ → pruebas de integración contra MySQL real (Category=Integration); sus carpetas (Hubs, Results) reflejan las de TypingBattle.Api
├── .env.example                       → plantilla de la configuración del compose
├── docker-compose.yml                 → MySQL, API, microfrontend y datos de ejemplo en local (configuración en .env)
├── dotnet-tools.json                  → herramienta local dotnet-ef
├── .gitignore
├── README.md
└── TypingBattle.slnx
```

## Cómo correrlo

Solo hace falta **Docker Desktop** abierto (en la barra de tareas debe decir *Engine running*) y los puertos 3306, 5015 y 4001 libres. No hay que instalar .NET ni Node. Todos los comandos se corren desde la raíz del repo.

La primera vez, crear el `.env` desde la plantilla. No se versiona y sus valores sirven para desarrollo local (en PowerShell, `Copy-Item .env.example .env`):

```bash
cp .env.example .env
```

Construir y levantar todo:

```bash
docker compose up -d --build
```

La primera vez descarga las imágenes base (.NET, Node, nginx y MySQL) y tarda unos minutos.

| Servicio | Contenedor | URL local |
|---|---|---|
| `mysql` | `typing-battle-mysql` | `localhost:3306` |
| `api` | `typing-battle-api` | `http://localhost:5015` (`/health`, `/api/games/typing`, `/hubs/typing`) |
| `frontend` | `typing-battle-frontend` | `http://localhost:4001/remoteEntry.js` |
| `data-generator` | `typing-battle-data-generator` | Sin puerto: registra las partidas de ejemplo y termina (`Exited (0)`) |

Comprobar que funciona:

```bash
docker compose ps -a
```

`typing-battle-mysql` debe aparecer como `(healthy)`, la API y el frontend como `Up`, y `typing-battle-data-generator` como `Exited (0)`: terminó bien y no se reinicia.

```bash
curl http://localhost:5015/health
```

Debe responder `Healthy`: la comprobación incluye la conexión a MySQL. Para ver los logs de un servicio (`api`, `mysql`, `frontend` o `data-generator`):

```bash
docker compose logs -f api
```

Detener todo:

```bash
docker compose down
```

- **Borrar también los datos:** `docker compose down -v` elimina el volumen de MySQL. La próxima vez que se levante, la base se crea de nuevo y se vuelven a generar los datos de ejemplo.
- **Si falta una variable** en el `.env`, Compose se detiene y dice cuál (ver [Variables de `.env`](#variables-de-env)).
- **La API aplica las migraciones al arrancar.** No pide autenticación: el jugador es el `currentUser` que le envía el juego (ver [Identidad del jugador](#identidad-del-jugador)).
- La guía completa, con las comprobaciones, cómo actualizar y los problemas comunes, está en [`docs/docker.md`](docs/docker.md).

### Datos de ejemplo

Con la base vacía, el historial y las estadísticas no muestran nada. Por eso el compose incluye el servicio `data-generator` (R-28), un proceso aparte que **no usa el código de la API, ni sus migraciones, ni un seed**: espera a que la API responda `/health` y registra partidas de ejemplo con el mismo `POST /api/games/typing/results` que usa cualquiera. Su código está en `src/data-generator/`.

- **Qué crea:** `DATA_MATCHES` partidas (30 por defecto) de 2 a 4 jugadores, con ids fijos `demo-0001`, `demo-0002`…, repartidas en las últimas semanas (una cada 17 horas aproximadamente), con wpm y precisión por jugador. Los jugadores son 8: `demo-ana`, `demo-luis`, `demo-sofia`, `demo-carlos`, `demo-maria`, `demo-diego`, `demo-valeria` y `demo-mateo`. Los datos son siempre los mismos, salvo las fechas.
- **No duplica:** como los ids son fijos, si una partida ya existe la API responde `409` y el generador la ignora. Volver a correr `docker compose up -d` no agrega nada.
- **Cuándo termina:** al registrar las partidas, con código 0. Queda como `Exited (0)` en `docker compose ps -a`; es lo esperado.
- **Usuarios reales:** si el Equipo 1 comparte los ids de los usuarios de la demostración, ponlos en `DATA_EXTRA_PLAYERS` (por ejemplo `auth0|64f0c1=Ana Demo;auth0|7a91b2=Luis Demo`) para que su historial muestre datos.

Para comprobarlos:

```bash
docker compose logs data-generator
```

```bash
curl "http://localhost:5015/api/games/typing/players/demo-ana/history?limit=3"
```

## Ejemplos de la API con curl

La API REST cuelga de `http://localhost:5015/api/games/typing` y no pide autenticación. Los ejemplos funcionan con el proyecto levantado (ver [Cómo correrlo](#cómo-correrlo)); en la referencia completa, [`docs/api-resultados.md`](docs/api-resultados.md), están todas las reglas de validación.

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/health` | Estado de la API; incluye la conexión a MySQL |
| POST | `/api/games/typing/results` | Registra el resultado de una partida finalizada |
| GET | `/api/games/typing/results/{matchId}` | Resultado detallado de una partida |
| GET | `/api/games/typing/players/{userId}/history` | Historial del jugador (`?limit=1..200`, por defecto 50, y `?offset=`) |
| GET | `/api/games/typing/players/{userId}/stats` | Estadísticas agregadas del jugador |

El hub SignalR `/hubs/typing` no se llama con curl: su contrato está en [`docs/api-typing-hub.md`](docs/api-typing-hub.md).

**Estado de la API**

```bash
curl http://localhost:5015/health
```

**Registrar el resultado de una partida.** El orden de `players` es el ranking final (el primero es el ganador) y las fechas van en UTC con `Z`:

```bash
curl -i -X POST http://localhost:5015/api/games/typing/results \
  -H "Content-Type: application/json" \
  -d '{
    "matchId": "match-001",
    "gameType": "typing",
    "players": [
      { "userId": "user-001", "displayName": "Ana", "score": 850 },
      { "userId": "user-002", "displayName": "Luis", "score": 620 }
    ],
    "startedAt": "2026-09-02T20:00:00Z",
    "finishedAt": "2026-09-02T20:00:47Z",
    "winnerUserId": "user-001",
    "metadata": {
      "textId": "t-07",
      "players": [
        { "userId": "user-001", "wpm": 62.4, "accuracy": 96.1 },
        { "userId": "user-002", "wpm": 44.0, "accuracy": 91.5 }
      ]
    }
  }'
```

Responde `201 Created` con el resultado guardado y el encabezado `Location`. Si se repite el mismo `matchId`, responde `409 Conflict`: solo se registra un resultado por partida. Si el cuerpo no es válido (por ejemplo, otro `gameType`), responde `400` con el detalle por campo:

```bash
curl -i -X POST http://localhost:5015/api/games/typing/results \
  -H "Content-Type: application/json" \
  -d '{ "matchId": "match-002", "gameType": "chess" }'
```

En PowerShell, `curl` es otro comando y las comillas del JSON se rompen. Guardar el cuerpo en un archivo, por ejemplo `resultado.json`, y usar `curl.exe`:

```powershell
curl.exe -i -X POST http://localhost:5015/api/games/typing/results -H "Content-Type: application/json" --data-binary "@resultado.json"
```

**Consultar el resultado de una partida**

```bash
curl http://localhost:5015/api/games/typing/results/match-001
```

Responde `200` con la misma forma del `POST`, o `404` si no existe.

**Historial de un jugador**, de la partida más reciente a la más antigua, con `limit` y `offset`:

```bash
curl "http://localhost:5015/api/games/typing/players/user-001/history?limit=10&offset=0"
```

Los ids de jugador distinguen mayúsculas y minúsculas y pueden traer caracteres especiales, como `auth0|64f0c1`: en la URL hay que codificarlos (`|` es `%7C`).

```bash
curl "http://localhost:5015/api/games/typing/players/auth0%7C64f0c1/history"
```

Un jugador sin partidas devuelve `[]`.

**Estadísticas de un jugador**

```bash
curl http://localhost:5015/api/games/typing/players/user-001/stats
```

Responde partidas jugadas, victorias, promedios y mejores marcas. Un jugador sin partidas devuelve ceros y `null` en los promedios, no un `404`.

## Puertos

| Servicio | Equipo | Puerto | URL local |
|---|---|---|---|
| MySQL de Typing | 4 | 3306 | `localhost:3306` |
| API y hub de Typing | 4 | 5015 | `http://localhost:5015` (`/api/games/typing`, `/hubs/typing`) |
| Microfrontend de Typing (remote `typingGame`) | 4 | 4001 | `http://localhost:4001/remoteEntry.js` |
| Shell | 3 | 4000 | `http://localhost:4000` |
| Matchmaking | 2 | 5211 | `http://localhost:5211` |
| Profile | 1 | 5220 | `http://localhost:5220` |

Los puertos de Typing se cambian en el `.env` (`MYSQL_PORT`, `API_PORT` y `FRONTEND_PORT`). El Shell busca el remote en el 4001, así que conviene no cambiar ese.

## Base de datos

Typing usa **MySQL 8.4** con **Entity Framework Core** (proveedor oficial `MySql.EntityFrameworkCore`). El esquema se crea y evoluciona **solo con migraciones**, que están en `src/backend/TypingBattle.Api/Persistence/Migrations`: una por tabla, `CreateGameResults` y `CreatePlayerResults` (R-14). Ninguna clave primaria es `varchar` (R-13).

**Por qué MySQL.** Varios jugadores guardan resultados al mismo tiempo, así que se necesita un motor que maneje accesos concurrentes y transacciones para mantener la integridad de los datos, con buena integración con .NET y EF Core. Los datos de partidas y jugadores encajan bien en un modelo relacional. Se descartaron:

| Alternativa | Por qué no |
|---|---|
| SQLite | Tiene limitaciones de concurrencia de escritura cuando varios usuarios guardan resultados a la vez |
| PostgreSQL | Es una alternativa válida, pero el equipo decidió usar MySQL para las necesidades del proyecto |
| MongoDB | Es flexible, pero los datos de partidas y jugadores se manejan bien con un modelo relacional |

**Qué guarda**

| Tabla | Entidad (EF Core) | Contenido |
|---|---|---|
| `game_results` | `GameResultEntity` | Una fila por partida: `Id` (GUID, clave), `MatchId` (único), `GameType`, `StartedAt`, `FinishedAt`, `WinnerUserId`, `PlayersCount`, `MetadataJson` (`json`) y `CreatedAt` |
| `player_results` | `PlayerResultEntity` | Una fila por jugador y partida (único `MatchId` + `UserId`), con `Score`, `Position`, `Wpm` y `Accuracy` copiados del `metadata` para consultar en SQL. Índice por `UserId` |

Las fechas son `datetime(6)` guardadas en UTC. Los ids (`MatchId`, `UserId`, `WinnerUserId`) usan la intercalación `utf8mb4_bin`, que distingue mayúsculas y minúsculas. Solo la API se conecta a MySQL: la base no se expone al navegador.

**Qué no se guarda.** Las partidas en curso (quién entró y su puntaje parcial) viven en memoria, en `TypingMatchStore`. Es estado de juego y se pierde si la API se reinicia; solo el resultado final llega a MySQL.

## Variables de entorno

No hay valores por defecto en el código, en los `appsettings` ni en `docker-compose.yml`: todo llega por variables de entorno (R-09). Ninguna credencial se versiona: el `.env` está en `.gitignore`.

### Variables de `.env`

Docker Compose lee la configuración de un archivo `.env` en la raíz del repo. Es obligatorio: `docker-compose.yml` no tiene valores por defecto y, si falta una variable, se detiene con un mensaje como `required variable MYSQL_PASSWORD is missing a value`. La plantilla es `.env.example` y sus valores sirven para desarrollo local:

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

### Configuración (variables de entorno)

Son las variables de la API y de las herramientas cuando se corren fuera de Docker. Con Docker Compose, el propio compose las arma a partir del `.env`.

| Variable de entorno | Para qué |
|---|---|
| `ConnectionStrings__Typing` | Cadena de conexión a MySQL. Obligatoria: la API y `dotnet ef` no arrancan sin ella (en Docker la arma el compose) |
| `Database__MigrateOnStartup` | `true` para aplicar las migraciones al arrancar (por defecto `false`) |
| `Cors__AllowedOrigins__0`, `__1`… | Orígenes del Shell y del microfrontend que pueden llamar a la API. Sin ellos, el navegador bloquea las llamadas desde otro origen |
| `TYPING_TEST_MYSQL` | Servidor MySQL de las pruebas de integración, sin `Database` (ver [Pruebas](#pruebas)). Obligatoria para correrlas |
| `TYPING_API_URL` | URL de la API, fija al compilar el frontend. Obligatoria: `npm start`, `npm run build` y el build de Docker fallan sin ella |
| `DATA_API_URL`, `DATA_MATCHES`, `DATA_WAIT_SECONDS`, `DATA_EXTRA_PLAYERS` | Las del generador de datos. En el compose, `DATA_API_URL` es la API por la red interna (`http://api:8080`) |

## Decisiones técnicas

- **Microfrontend con Module Federation.** El juego es un *remote* de Webpack 5 en Aurelia 2 (`2.0.0-rc.2`, versión exacta): nombre `typingGame`, módulo `./GameModule`, puerto 4001 y punto de entrada `remoteEntry.js`. El Shell lo descarga en tiempo de ejecución, así que cambiar un juego no obliga a recompilar el Shell y cada juego se despliega por su cuenta. Aurelia se comparte como instancia única con el mismo `mf-shared.js` que el Shell y los otros juegos: un desfase de versión falla de inmediato en lugar de fallar en silencio. Se descartaron los `iframe` (dificultan el contrato visual y el ciclo de vida), Vite (la federación depende de plugins de terceros) y los juegos como paquetes del Shell (se pierde el despliegue independiente). El servidor del remote responde con CORS abierto (`Access-Control-Allow-Origin: *`), usa `publicPath: 'auto'` para que los chunks se pidan a su servidor, y las clases CSS llevan el prefijo `typing-game` para no alterar el layout del Shell.
- **MySQL con EF Core y solo migraciones.** Ver [Base de datos](#base-de-datos).
- **El juego no se autentica (R-30).** El Shell inicia la sesión con Auth0 y le pasa al juego el `currentUser`. Ver [Identidad del jugador](#identidad-del-jugador).
- **El fin de la partida se avisa al Shell por un evento (R-46).** El juego no llama a Matchmaking: emite `battlehub:game-finished` y el Shell notifica. Ver [Evento de fin de partida](#evento-de-fin-de-partida).
- **Una sola instancia de la API, con las partidas en curso en memoria (R-12).** El estado de una partida mientras se juega (quién entró, su puntaje y su temporizador) vive en memoria, en `TypingMatchStore`. Es estado de juego, no persistencia: lo que se guarda en MySQL son los resultados, al terminar. Con dos instancias, los jugadores de una misma partida podrían quedar en procesos distintos, y si la API se reinicia se pierden las partidas en curso, pero no los resultados ya guardados. Correr varias instancias requeriría un *backplane* de SignalR y guardar el estado fuera del proceso, y queda fuera del alcance del proyecto.
- **El servidor decide el resultado.** Cada cliente pide terminar al acabarse su tiempo y el servidor arma el ranking, el ganador y los puntajes con la fórmula `wpm × precisión × 10`; el navegador no decide ganadores. El primero que termina guarda el resultado y todos lo reciben. Ver [`docs/api-typing-hub.md`](docs/api-typing-hub.md).
- **Práctica y playground sin servicios (R-44, R-34).** La práctica integrada con el Shell no conecta con el hub, no llama a la API y no guarda nada. El playground (la página del juego sola, sin Shell) arma el mismo contexto con un jugador local.
- **Configuración solo por variables de entorno (R-09, R-32).** Sin valores por defecto en el código ni en el compose; la plantilla documentada es `.env.example`.
- **Datos de ejemplo en un proceso aparte (R-28).** Un servicio del compose que usa la API pública, para que no dependa del código de la API, de sus migraciones ni de un seed.
- **Fechas en UTC**, en formato ISO-8601 con sufijo `Z`, en la API, la base y los eventos.
- **Node 22 y .NET 10 (R-42).** Fijados en `.nvmrc`, en `engines` y en el CI.

### Si la API se publica fuera de la máquina local

Hoy el proyecto solo corre en local y el despliegue no se evalúa; las opciones de hosting estudiadas están en [`docs/despliegue.md`](docs/despliegue.md). Si algún día se publica, el código impone estas condiciones:

- **WebSockets.** El hosting de la API debe permitirlos. SignalR puede usar Server-Sent Events o long polling si no los hay, pero con más latencia, y en un juego de velocidad se nota.
- **HTTPS.** Si el Shell se publica con HTTPS, el microfrontend y la API también deben usarlo: el navegador bloquea las llamadas HTTP desde una página HTTPS.
- **CORS.** El origen público del Shell va en `Cors__AllowedOrigins` de la API.
- **La URL de la API se fija al compilar el frontend** (`TYPING_API_URL`): si cambia, hay que volver a compilar el remote. Ejemplo: `docker build --build-arg TYPING_API_URL=https://typing.mi-dominio.com -t typing-battle-frontend src/frontend`.
- **Secretos.** `ConnectionStrings__Typing` se configura solo como variable de entorno del hosting: nunca va en el repo ni dentro de una imagen.
- **Salud.** `GET /health` comprueba también la conexión a MySQL, y el hosting puede usarlo para saber si la API está sana.

### Identidad del jugador

El juego no tiene contacto con Auth0 (R-30). El Shell inicia la sesión del usuario, decide si se muestra el juego y le entrega al microfrontend el `currentUser` (`id` y `displayName`) en el contexto. Esa decisión tiene estas consecuencias:

- La API y el hub no piden credenciales: no hay tokens, encabezados de identidad ni permisos.
- El jugador es el `currentUser` que el microfrontend envía en cada petición del hub. El hub solo exige que venga informado.
- Solo los jugadores que se unieron a una partida pueden enviar métricas o terminarla.
- Por la misma razón, nada en la API impide que alguien con acceso a la red se haga pasar por otro jugador o registre un resultado con `POST /results`. Se acepta porque la API solo se publica detrás del Shell y dentro de la red del compose; si algún día se expusiera a internet, habría que protegerla en el borde (un proxy o una puerta de enlace) y no en el juego.
- **Pendiente:** el Equipo 3 pide acordar cómo se protege la API y el hub antes de habilitar partidas públicas, porque `currentUser` no acredita identidad ante el servidor. La práctica no depende de esto: no usa la API ni el hub.

## Integración con los otros equipos

Los puertos de todos los servicios están en [Puertos](#puertos).

**Shell (Equipo 3).** El microfrontend sigue el contrato de integración de microfrontends con Module Federation: remote `typingGame`, módulo `./GameModule`, puerto 4001 y el mismo `mf-shared.js` que el Shell. Para que el Shell lo cargue, su `config/remotes.local.json` necesita esta entrada:

```json
"typing": { "scope": "typingGame", "url": "http://localhost:4001/remoteEntry.js", "module": "./GameModule" }
```

Para probarlo juntos: levantar Typing con `docker compose up -d --build` (ver [Cómo correrlo](#cómo-correrlo)) y el Shell con `npm start` en su repo. La API ya permite los orígenes `http://localhost:4000` y `http://localhost:4001`.

El contrato está en el README y en `src/games/game-contracts.ts`, `game-events.ts` y `game-host.ts` del Shell, que ya mezcló su [PR #6](https://github.com/KeynerMC/battlehub-shell/pull/6) en `main` (commit `478f79c`). Typing implementa ese contrato: con una versión anterior del Shell, `initialize()` rechaza el contexto porque no trae `events` ni `mode`.

#### Contexto que entrega el Shell

| Campo | Partida normal | Práctica |
|---|---|---|
| `gameType` | `typing` | `typing` |
| `currentUser` | `{ id, displayName }` | `{ id, displayName }` |
| `events` | `EventTarget` propio de la partida | `EventTarget` propio de la práctica |
| `matchId` | Id real de la sala | No existe |
| `mode` | No existe | `practice` |
| `participants` | Foto de los integrantes de la sala al iniciar (opcional; Typing no la usa) | No existe |

`initialize()` rechaza un contexto con otro `gameType`, sin usuario, sin `events`, sin `mode` ni `matchId`, o con `mode: 'practice'` y `matchId`. El juego nunca inventa un `matchId` ni recibe tokens.

- **Ciclo de vida:** el Shell llama a `initialize()` y luego a `start()`; el juego no escucha `battlehub:game-start`. `start()` también reanuda: no vuelve a conectar ni a unirse si la conexión sigue abierta, ni duplica el temporizador. `dispose()` detiene el temporizador y cierra la conexión.

#### Modo práctica (R-44)

El modo práctica está integrado con el Shell y permite jugar solo, sin crear una sala de Matchmaking ni registrar resultados.

El Shell lo abre desde el botón **Practicar** del catálogo (ruta `/practice/typing`), solo para los usuarios que tienen el permiso `games.typing.play` según el Profile Service. Le entrega al juego un contexto con `mode: "practice"` y sin `matchId`.

En este modo el juego corre completo en el navegador: no se conecta al hub, no llama a la API, no guarda la partida y no emite `battlehub:game-finished`, porque no hay sala que cerrar. Desde el Shell se puede pausar, reanudar y reiniciar.

#### Modo playground (R-34)

El juego se puede jugar por sí solo, sin Shell, sin Auth0 y sin ningún otro servicio.

1. Tener el `.env` creado (`cp .env.example .env`; Compose lo lee completo aunque se levante un solo servicio) y levantar solo el microfrontend:

   ```bash
   docker compose up -d --build frontend
   ```

   Si ya está corriendo el entorno completo con `docker compose up`, el playground también está disponible.

2. Abrir `http://localhost:4001` en el navegador (el puerto de `FRONTEND_PORT`).
3. Pulsar **Iniciar práctica**.

La partida de un jugador local corre completa en el navegador: no llama a la API, no se conecta al hub y no guarda resultados. Al terminar muestra el resultado y permite jugar de nuevo con **Volver a jugar**. Arma el mismo contexto de práctica que entrega el Shell.

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

## Desarrollo (opcional)

Para correr el proyecto basta `docker compose up`. Esta sección es para quien quiera cambiar el código y necesita .NET 10 o Node 22 en su máquina.

### Backend

Requisitos: el [SDK de .NET 10](https://dotnet.microsoft.com/download) y un MySQL 8. La forma más simple de tener MySQL es Docker, con el `.env` ya creado (ver [Variables de `.env`](#variables-de-env)):

```bash
docker compose up -d mysql
```

Eso levanta solo MySQL 8.4 en el puerto `MYSQL_PORT`, con la base, el usuario y la contraseña de tu `.env`. Si prefieren un MySQL instalado en su máquina, creen esa base y ese usuario.

El código y los `appsettings` no traen cadena de conexión ni orígenes de CORS: hay que definirlos como variables de entorno (ver [Configuración](#configuración-variables-de-entorno)). En PowerShell, con el MySQL de arriba:

```powershell
$env:ConnectionStrings__Typing = "Server=localhost;Port=3306;Database=<MYSQL_DATABASE>;User ID=<MYSQL_USER>;Password=<MYSQL_PASSWORD>"
$env:Cors__AllowedOrigins__0 = "<origen del Shell>"
```

```bash
dotnet run --project src/backend/TypingBattle.Api
```

La API queda en `http://localhost:5015`, el mismo puerto que usa el compose: no se pueden correr a la vez. En Development aplica las migraciones pendientes al arrancar (`Database:MigrateOnStartup`). Comprobación de salud en `/health` y documento OpenAPI en `/openapi/v1.json`.

El hub `/hubs/typing` corre en el mismo proceso y guarda el resultado al terminar la partida inyectando `IResultsService` (ver [`docs/api-resultados.md`](docs/api-resultados.md#desde-el-backend-del-hub-sin-http)).

#### Migraciones

El esquema solo cambia con migraciones de EF Core, y cada migración crea o cambia una sola tabla. El historial de migraciones se actualizó en la Etapa 2: se reemplazó la migración que tenía las dos tablas y una clave `varchar` por dos migraciones, una por tabla; el esquema resultante es el mismo. **Una base creada con el historial anterior no sirve**: la API se detiene al arrancar con `Table 'game_results' already exists`. Hay que borrar el volumen y volver a levantar: `docker compose down -v` y luego `docker compose up -d --build`. La herramienta `dotnet-ef` está fijada en `dotnet-tools.json`:

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

#### Pruebas

Dos proyectos separados (R-15), cuyas carpetas reflejan las de `src/backend/TypingBattle.Api` (R-16): `Hubs/TypingHub.cs` se prueba en `Hubs/TypingHubTests.cs`. Las mismas que corre el CI:

```bash
dotnet test --filter "Category=Unit"
```

Las unitarias no tocan MySQL, red ni archivos (R-17): lo que sale del proceso se simula con [Moq](https://github.com/devlooped/moq). Al hub se le simulan los clientes de SignalR, los grupos y el contexto de la conexión; al servicio de partidas, el servicio de resultados y el reloj; y a los endpoints, el servicio de resultados. La lógica de `ResultsService` con la base de datos se prueba solo en integración, con MySQL real.

```bash
dotnet test --filter "Category=Integration"
```

Las de integración levantan la API completa contra MySQL real: cada clase crea su propia base `typing_test_…`, le aplica las migraciones y la borra al terminar. Necesitan la variable `TYPING_TEST_MYSQL`, con una cadena sin `Database` y un usuario que pueda crear y borrar bases. Con el MySQL de docker compose (`docker compose up -d mysql`), en PowerShell:

```powershell
$env:TYPING_TEST_MYSQL = "Server=localhost;Port=3306;User ID=root;Password=<MYSQL_ROOT_PASSWORD>;SslMode=Disabled"
```

`SslMode=Disabled` evita un error intermitente del conector MySql.Data al negociar TLS en varias conexiones a la vez ([bug #111759](https://bugs.mysql.com/bug.php?id=111759)). Para un MySQL local de pruebas no hace falta TLS.

### Frontend

Requisitos: Node 22 LTS (`src/frontend/.nvmrc`). Desde `src/frontend/`:

```bash
npm ci
```

```bash
npm start
```

El remote queda en `http://localhost:4001/remoteEntry.js` para que el Shell lo cargue, en el mismo puerto que usa el compose. Las mismas verificaciones que corre el CI:

```bash
npm test
```

```bash
npm run build
```

`npm test` corre primero el lint (ESLint y Stylelint) y luego las pruebas de Jest, con el reporte de cobertura (`coverage/`: `lcov.info` y `coverage-summary.json`). Si la cobertura de sentencias, ramas, funciones o líneas baja del 85 %, Jest falla (R-19, R-22): el umbral está en `coverageThreshold`, dentro de la configuración de Jest de `package.json`.

- **URL de la API.** Se fija al compilar con la variable `TYPING_API_URL`, que es obligatoria: sin ella fallan `npm start`, `npm run build` y el build de Docker. Por ejemplo, `TYPING_API_URL=https://typing.mi-dominio.com npm run build`.
- **Identidad.** El juego no usa tokens ni habla con Auth0: manda el `currentUser` que le entrega el Shell en el contexto (ver [Identidad del jugador](#identidad-del-jugador)).
- **Estilos.** Todas las clases llevan el prefijo `typing-game` y las variables CSS se definen en la raíz del juego (`.typing-game`), así que no alteran el layout del Shell.
- **Altura.** La raíz del juego toma la altura que le asigna el Shell. A 1366x768 le quedan unos 495 px en la práctica y 550 px en una partida, y entra sin scroll de página ni del juego (R-33). El historial, las estadísticas y las clasificaciones muy largas hacen scroll dentro de su propio panel.

### Generador de datos

Con Node 22, desde `src/data-generator/`, y contra una API ya levantada. En PowerShell:

```powershell
$env:DATA_API_URL = "http://localhost:5015"; $env:DATA_MATCHES = "30"; $env:DATA_WAIT_SECONDS = "60"; $env:DATA_EXTRA_PLAYERS = ""
```

```bash
npm start
```

Las pruebas (`npm test`) no necesitan la API: usan una falsa.

## Integración continua (CI)

El CI corre en GitHub Actions, con cuatro workflows en `.github/workflows/`. Los tres CI por componente corren **solo si cambia su parte** (filtros `paths`, R-24); «Build y pruebas unitarias» corre siempre.

| Workflow | Cuándo corre | Qué hace |
|---|---|---|
| **CI - Backend** | Cambios en `src/backend/`, `tests/`, `TypingBattle.slnx`, `dotnet-tools.json`, `.editorconfig`, `.dockerignore`, `docker-compose.yml` o `.env.example` | Restore, build sin advertencias, lint (`dotnet format`), migraciones al día, pruebas unitarias y de integración con MySQL 8.4, cobertura, comentario en el PR y `docker build` de la API |
| **CI - Frontend** | Cambios en `src/frontend/` | Install, lint (ESLint y Stylelint), build, pruebas unitarias con cobertura, comentario en el PR y `docker build` del frontend |
| **CI - Generador de datos** | Cambios en `src/data-generator/` | Pruebas unitarias con cobertura y `docker build` del generador |
| **Build y pruebas unitarias** | Siempre: cada PR hacia `main` y cada push a `main` | Solo build y pruebas unitarias de las tres partes, sin MySQL ni Docker: es el aviso rápido (R-21) |

- **Ejecución manual (R-54):** los cuatro workflows tienen `workflow_dispatch`. En la pestaña **Actions**, elegir el workflow y pulsar **Run workflow**, sin hacer un commit.
- **Cobertura mínima del 85 % (R-19, R-22):**
  - Backend: líneas, con las pruebas unitarias y las de integración juntas, sin contar las migraciones ni el código generado. Las une ReportGenerator (herramienta fijada en `dotnet-tools.json`).
  - Frontend: Jest exige 85 % en sentencias, ramas, funciones y líneas (`coverageThreshold` en `src/frontend/package.json`), sin excluir archivos de producción.
  - Generador de datos: el ejecutor de pruebas de Node exige 85 % en líneas, ramas y funciones.
  - En cada PR, CI - Backend y CI - Frontend publican un comentario con los porcentajes, que se actualiza en cada push, y fallan si no llegan al mínimo. Los informes completos quedan como artefactos de la ejecución.
- **Merge bloqueado si falla el CI (R-25):** el ruleset de `main` exige los tres checks de «Build y pruebas unitarias» (`Build y unitarias - Backend`, `Build y unitarias - Frontend` y `Build y unitarias - Generador de datos`). Ese workflow corre siempre, así que un PR que solo cambia documentación no se queda esperando. Los CI por componente no son obligatorios porque, con un filtro `paths`, un check obligatorio que no se ejecuta deja el PR esperando; igual aparecen en el PR y hay que revisarlos antes de mezclar.
- **Versiones (R-42, R-51):** Node 22 sale del `.nvmrc` de cada proyecto y .NET 10 de `dotnet-version: 10.0.x`.
- Los comandos son los mismos que se corren en local: ver [Desarrollo (opcional)](#desarrollo-opcional) y [`docs/docker.md`](docs/docker.md).

## Flujo de trabajo

- `main` está protegida: sin push directo; todo entra por Pull Request con los checks de «Build y pruebas unitarias» en verde y al menos 1 aprobación.
- Commits y títulos de PR en formato semántico: `<tipo>(<alcance opcional>): <descripción en imperativo>`. Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`, `perf`.
- Las pruebas de .NET se etiquetan `[Trait("Category", "Unit")]` o `[Trait("Category", "Integration")]`, porque el CI las filtra por categoría.
- Todas las fechas y timestamps van en UTC, formato ISO-8601 con sufijo `Z`.
- Nada de secretos en el repo: usar variables de entorno.

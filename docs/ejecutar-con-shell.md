# Ejecutar Typing con el Shell

Guía paso a paso para correr Typing Battle dentro del Shell del Equipo 3. Tiene dos partes:

| Parte | Qué prueba | Qué necesita | Estado |
|---|---|---|---|
| **A. Práctica en modo de prueba** | Que el Shell cargue Typing y se pueda jugar la práctica | Solo Typing y el Shell | Probada el 3 de noviembre con el `main` del Shell (`478f79c`) |
| **B. Con login real y dos jugadores** | La práctica con permisos reales y una partida de sala completa | Auth0, Profile, Matchmaking y MongoDB de los equipos 1 y 2 | **Pendiente**: es la prueba conjunta (R-41 y R-49) |

La parte A no sustituye a la B: el modo de prueba del Shell no verifica permisos reales ni salas.

## 0. Requisitos

- Docker Desktop abierto, Git y **Node 22** (el Shell exige `>=22 <23`).
- Puertos libres: 4000 (Shell), 4001, 5015 y 3306 (Typing). Para la parte B también 5220 (Profile), 5211 (Matchmaking) y 27017 (MongoDB). Los puertos de todos los servicios están en el [README](../README.md#puertos).
- El Shell debe incluir el [PR #6](https://github.com/KeynerMC/battlehub-shell/pull/6) (ya está en su `main`). Con una versión anterior, Typing rechaza el contexto: no trae `events` ni `mode`.

## 1. Levantar Typing

Desde la raíz de este repositorio:

```powershell
Copy-Item .env.example .env        # solo la primera vez
docker compose up -d --build
docker compose ps -a
```

Debe verse `mysql` en `healthy`, `api` y `frontend` en `Up`, y `data-generator` en `Exited (0)`. Comprobación rápida:

```powershell
curl.exe http://localhost:5015/health
curl.exe -I http://localhost:4001/remoteEntry.js
```

La segunda debe responder `200`. Dos valores del `.env` importan para el Shell:

| Variable | Valor local | Para qué |
|---|---|---|
| `TYPING_API_URL` | `http://localhost:5015` | La URL de la API tal como la usa el navegador. Se fija al construir el frontend |
| `CORS_SHELL_ORIGIN` | `http://localhost:4000` | El origen del Shell, para que la API acepte sus llamadas |

Si la base se creó antes del PR #43, la API se detiene con `Table 'game_results' already exists`: corre `docker compose down -v` y vuelve a levantar (borra los datos locales). Más problemas en [Docker](docker.md).

## 2. Levantar el Shell

En otra carpeta y otra terminal:

```powershell
git clone https://github.com/KeynerMC/battlehub-shell.git
Set-Location battlehub-shell
npm.cmd ci
Copy-Item .env.example .env.development
npm.cmd start
```

El Shell queda en `http://localhost:4000`. No hace falta cambiar nada para que encuentre a Typing: su `config/remotes.local.json` ya trae esta entrada, que coincide con la nuestra.

```json
"typing": { "scope": "typingGame", "url": "http://localhost:4001/remoteEntry.js", "module": "./GameModule" }
```

Si cambias el `.env.development`, reinicia `npm.cmd start`. No corras a la vez `poc/shell`: usa el mismo puerto 4000.

## Parte A. Práctica en modo de prueba

Con el `.env.development` tal como viene (`BATTLEHUB_AUTH_MODE=mock`) no hace falta Auth0 ni Profile. Pero ese modo **no concede el permiso de Typing**, y el catálogo lo marca como «Integración pendiente». Para probar la práctica hay que darle el permiso **en tu copia local** del Shell. Es un cambio solo para probar: no se sube al repositorio del Shell.

En `src/profile/mock-profile-service.ts` del Shell:

```diff
-  public async getPermissions(): Promise<string[]> { await this.getProfile(); return []; }
+  public async getPermissions(): Promise<string[]> { await this.getProfile(); return ['games.typing.play']; }
...
-      { gameType: 'typing', name: 'Typing Battle', description: 'Juego de escritura del Grupo 2.' },
+      { gameType: 'typing', name: 'Typing Battle', description: 'Juego de escritura del Grupo 2.', requiredPermission: 'games.typing.play' },
```

1. Abre `http://localhost:4000/login`, escribe un nombre y pulsa **Entrar en modo prueba**. La sesión se pierde al recargar la página: si recargas, entra otra vez.
2. En el modo de prueba el catálogo no muestra el botón **Practicar** (solo existe con login real). Abre la ruta sin recargar: en la consola del navegador (F12) ejecuta

   ```js
   history.pushState({}, '', '/practice/typing'); dispatchEvent(new PopStateEvent('popstate'));
   ```

3. Debe aparecer «PRÁCTICA LOCAL» con tu nombre como jugador y el tiempo corriendo. Escribe el texto hasta terminar.
4. Comprueba:
   - Al terminar dice «Tus resultados no se enviaron al servidor».
   - **Iniciar practica**, **Pausar** y **Reanudar** (botones del Shell) funcionan: al pausar el tiempo se congela.
   - **Volver a juegos** cierra el juego y vuelve al catálogo.
   - En la pestaña Network (F12) no hay peticiones al puerto 5015 ni WebSockets: la práctica no usa la API ni el hub.
   - Con la ventana en 1366 × 768 (F12, modo dispositivo) no hay scroll.

## Parte B. Con login real y dos jugadores

Esta parte necesita a los equipos 1 y 2 y **un tenant real de Auth0** (administrado por el Equipo 1). Cada proyecto se levanta con su propio `docker compose`, en su carpeta. Los puertos no chocan: Profile no publica su MySQL, así que no usa el 3306 de Typing.

| Servicio | Equipo | Carpeta del repo | Puerto |
|---|---|---|---|
| Typing (frontend, API, MySQL) | 4 | raíz de este repositorio | 4001, 5015, 3306 |
| Matchmaking (API y MongoDB) | 2 | `battlehub-matchmaking` | 5211, 27017 |
| Profile Service (API y MySQL) | 1 | `battlehub-profile-service` | 5220 |
| Shell | 3 | `battlehub-shell` | 4000 |

**Lo que hay que pedirle al Equipo 1** (sin esto no hay forma de probar con login real):

| Dato | Para qué | ¿Es secreto? |
|---|---|---|
| `AUTH0_DOMAIN` (dominio del tenant, sin `https://`) | Shell, Profile y Matchmaking | No |
| `AUTH0_CLIENT_ID` de la aplicación SPA del Shell | Shell | No (es público) |
| `AUTH0_AUDIENCE` (por defecto `https://api.battlehub.local/profile`) | Shell, Profile y Matchmaking | No |
| Identifier de la API de cada juego (`AUTH0_API_TYPING`, `_TRIVIA`, `_MEMORY`) | Profile | No |
| `AUTH0_MGMT_CLIENT_ID` y `AUTH0_MGMT_CLIENT_SECRET` (aplicación M2M) | Profile | **Sí**: solo en su `.env`, nunca en capturas, chats ni commits |
| Los 3 usuarios de demostración y sus contraseñas | Iniciar sesión | **Sí** |

Los usuarios de demostración del Equipo 1: el usuario 1 no tiene juegos, el 2 solo Typing y el 3 los tres. Los usuarios 2 y 3 tienen `matches.create` y pueden crear salas. Sin las credenciales M2M, Profile arranca pero `/me/access`, `/me/permissions` y `/me/games` responden `503` y el catálogo queda vacío.

**Orden de arranque** (cada uno en su carpeta; la primera vez que se construye tarda unos minutos):

1. **Typing:** `docker compose up -d --build` (ver el paso 1 de esta guía).
2. **Matchmaking:** `Copy-Item .env.example .env`, completar `AUTH0_DOMAIN` y `docker compose up --build`. Comprueba `http://localhost:5211/health`.
3. **Profile:** `Copy-Item .env.example .env`, completar contraseñas locales de MySQL (las que quieras), `AUTH0_DOMAIN`, los tres `AUTH0_API_*` y las credenciales M2M, y `docker compose up --build`. Comprueba `http://localhost:5220/health`.
4. **Shell:** el `.env.development` del siguiente punto y `npm.cmd start`.

Si cambias las contraseñas de MySQL de Profile después de haberlo levantado una vez, MySQL conserva las anteriores: corre `docker compose down -v` en esa carpeta antes de volver a levantarlo. Con los valores de relleno, los tres proyectos arrancan y responden `200` en `/health`, y piden autenticación (`401`) sin token; con un dominio falso no pueden validar sesiones reales.

1. En el `.env.development` del Shell:

   ```text
   BATTLEHUB_AUTH_MODE=auth0
   AUTH0_DOMAIN=<dominio del tenant, sin https://>
   AUTH0_CLIENT_ID=<Client ID público de la SPA del Shell>
   AUTH0_AUDIENCE=<identifier de la API acordada con el Equipo 1>
   PROFILE_SERVICE_URL=http://localhost:5220
   MATCHMAKING_SERVICE_URL=http://localhost:5211
   ```

   Son valores públicos del frontend. **Nunca** pongas ahí un Client Secret ni tokens. Reinicia `npm.cmd start`.

2. **Práctica con login real.** Entra con el primer usuario, abre **Juegos** y en la tarjeta de Typing Battle pulsa **Practicar**. Repite las comprobaciones de la parte A (juego completo, pausa, reanudar, reiniciar, salir, sin llamadas a la API, sin scroll a 1366 × 768).
3. **Acceso denegado.** Con el usuario sin `games.typing.play`, Typing no debe aparecer en su catálogo, porque el Shell lo filtra según los permisos. Si abres `/practice/typing` a mano, el Shell lo devuelve al catálogo.
4. **Partida de sala con dos jugadores.** Usa dos navegadores distintos (o uno normal y uno de incógnito), cada uno con su usuario:
   1. El usuario con `matches.create` abre **Salas**, crea una sala de Typing.
   2. El segundo usuario se une. Ambos deben ver a los dos participantes.
   3. El anfitrión inicia la partida. El Shell carga Typing y le entrega el `matchId`.
   4. Juegan los 60 segundos. Al terminar aparece el resultado final, calculado por el servidor.
   5. Cada navegador avisa el fin al Shell con el evento `battlehub:game-finished`; el Shell notifica a Matchmaking y la sala pasa a `Finished`. Que lleguen dos avisos del mismo `matchId` es esperado: el servidor de Typing guarda solo el primer resultado y al segundo le responde `already_exists`.
   6. Cierra el juego desde el Shell y comprueba que la sala queda finalizada.
5. **Comprobar el resultado guardado.** Con el `matchId` de la partida y el id del jugador (el `sub` de Auth0, por ejemplo `auth0|64f0c1`; el `|` se escribe `%7C` en la URL):

   ```powershell
   curl.exe -i http://localhost:5015/api/games/typing/results/<matchId>
   curl.exe -i "http://localhost:5015/api/games/typing/players/auth0%7C64f0c1/history?limit=3"
   ```

6. **Evidencia.** Captura cada paso con su pie de figura. **No** muestres tokens ni la cabecera `Authorization` de la pestaña Network, ni valores del `.env`.

## Problemas frecuentes

| Qué pasa | Causa probable | Qué hacer |
|---|---|---|
| Typing aparece como «Integración pendiente» y no hay botón **Practicar** | Estás en modo `mock` (ese botón es solo para `auth0`) | Usa la ruta de la parte A, o cambia a login real |
| El Shell muestra que no pudo descargar `http://localhost:4001/remoteEntry.js` | El frontend de Typing no está corriendo | `docker compose up -d frontend` y revisa `docker compose ps -a` |
| `initialize()` rechaza el contexto (`events es requerido`, `matchId es requerido…`) | El Shell es anterior al PR #6 | Actualiza el Shell a su `main` actual |
| Error de CORS en la consola al llamar a la API | `CORS_SHELL_ORIGIN` no coincide con el origen del Shell | Corrige el `.env` y corre `docker compose up -d api` |
| La API se detiene con `Table 'game_results' already exists` | Base creada con el historial de migraciones anterior | `docker compose down -v` y `docker compose up -d --build` |
| Error de versiones de Aurelia al cargar el remote | Typing y el Shell deben usar exactamente `2.0.0-rc.2` | Revisa `package.json` y `mf-shared.js` de ambos |
| El puerto 4000 está ocupado | Hay otro Shell o `poc/shell` corriendo | Detén el otro proceso |
| Se pierde la sesión al recargar (modo de prueba) | Es lo esperado en el modo mock | Entra de nuevo |

## Apagar todo

```powershell
docker compose down        # en este repositorio; con -v también borra los datos de MySQL
```

Y detén `npm.cmd start` en la terminal del Shell (Ctrl+C).

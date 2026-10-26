# Despliegue de Typing Battle

Qué hay que desplegar para que Typing Battle funcione fuera de las máquinas del equipo, de qué servicios depende y qué condiciones impone el código al hosting.

## Servicios propios (Equipo 4)

| Servicio | Tecnología | Qué expone | Puerto local | Necesita |
|---|---|---|---|---|
| API y hub de Typing | .NET 10 (`TypingBattle.Api`) | REST `/api/games/typing`, hub SignalR `/hubs/typing` y `GET /health` | 5015 | MySQL, el tenant de Auth0 y, para avisar el fin de partida, Matchmaking |
| Microfrontend (remote `typingGame`) | Aurelia 2 y Webpack Module Federation | Archivos estáticos: `remoteEntry.js` y sus chunks | 4001 | La URL pública de la API, que se fija al compilar (`TYPING_API_URL`) |
| Base de datos | MySQL 8.4, esquema `typing_battle` | Tablas `game_results` y `player_results` (migraciones de EF Core) | 3306 | Almacenamiento persistente y respaldos |

Solo la API se conecta a MySQL: la base de datos no se expone al navegador.

## Dependencias externas

| Servicio | Equipo | Relación con Typing | Qué hay que coordinar para desplegar |
|---|---|---|---|
| Shell | 3 | Carga el remote `typingGame` (ADR-003) y le entrega el contexto del juego: usuario, sala y `getAccessToken` | Registrar la URL pública de `remoteEntry.js` en su configuración de remotes. Su origen público va en `Cors__AllowedOrigins` de la API |
| Matchmaking | 2 | Crea las salas. Al terminar la partida, la API le avisa con `POST /api/matches/{matchId}/finish` y un token M2M (ADR-004) | Su URL pública, que va en `Matchmaking__BaseUrl` |
| Profile | 1 | El Shell lo consulta para saber qué juegos tiene habilitados el usuario (`typing` requiere `games.typing.play`). Typing no lo llama directamente | Nada específico de Typing |
| Auth0 | 1 | Emite el JWT del usuario, que valida la API, y el token M2M con el que la API avisa a Matchmaking | Registrar las URLs públicas del Shell (callbacks, logout y orígenes web), el audience de la API de Typing y el cliente M2M con el permiso `matches.finish` |

## Cómo se comunican

1. El usuario abre el Shell e inicia sesión con Auth0.
2. El Shell descarga `remoteEntry.js` desde el hosting del microfrontend y monta el juego.
3. El juego llama a la API por REST y se conecta al hub por SignalR, con el JWT del usuario. Estas llamadas salen del navegador desde el origen del Shell, por eso la API debe permitirlo en CORS.
4. Al terminar la partida, la API guarda el resultado en MySQL con EF Core.
5. La API pide un token M2M a Auth0 y avisa el fin de la partida a Matchmaking.

## Condiciones que impone el código

- **Una sola instancia de la API.** Las partidas en curso viven en memoria (`TypingMatchStore`), igual que los avisos pendientes a Matchmaking (`MatchFinishedQueue`). Con dos instancias, los jugadores de una misma sala podrían quedar conectados a procesos distintos. Si la API se reinicia, se pierden las partidas en curso, pero no los resultados ya guardados. Correr varias instancias requeriría un backplane de SignalR y guardar el estado fuera del proceso; queda fuera del alcance del proyecto.
- **WebSockets.** El hosting de la API debe permitir WebSockets. SignalR puede usar Server-Sent Events o long polling si no los hay, pero con más latencia, y en un juego de velocidad se nota.
- **HTTPS.** Si el Shell se publica con HTTPS, el microfrontend y la API también deben usarlo: el navegador bloquea las llamadas HTTP desde una página HTTPS (contenido mixto).
- **La URL de la API se fija al compilar el frontend.** `TYPING_API_URL` se inyecta en el build (`DefinePlugin` de Webpack). Si cambia la URL de la API, hay que volver a compilar y publicar el remote.
- **Migraciones.** Con `Database__MigrateOnStartup=true`, la API aplica las migraciones pendientes al arrancar. Con una sola instancia no hay riesgo de que dos procesos migren a la vez.
- **Secretos.** `ConnectionStrings__Typing` y `Matchmaking__Auth0__ClientSecret` se configuran solo como variables de entorno del hosting. Nunca van en el repo ni dentro de una imagen de Docker.
- **Health check.** `GET /health` comprueba también la conexión a MySQL. El hosting puede usarlo para saber si la API está sana.

## Configuración por entorno

Las variables de la API están en el README, en la sección «Configuración fuera de Development». El frontend solo necesita `TYPING_API_URL` al compilar.

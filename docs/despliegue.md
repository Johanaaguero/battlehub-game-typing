# Despliegue de Typing Battle

Qué hay que desplegar para que Typing Battle funcione fuera de las máquinas del equipo, de qué servicios depende y qué condiciones impone el código al hosting.

## Servicios propios (Equipo 4)

| Servicio | Tecnología | Qué expone | Puerto local | Necesita |
|---|---|---|---|---|
| API y hub de Typing | .NET 10 (`TypingBattle.Api`) | REST `/api/games/typing`, hub SignalR `/hubs/typing` y `GET /health` | 5015 | MySQL y, para avisar el fin de partida, Matchmaking |
| Microfrontend (remote `typingGame`) | Aurelia 2 y Webpack Module Federation | Archivos estáticos: `remoteEntry.js` y sus chunks | 4001 | La URL pública de la API, que se fija al compilar (`TYPING_API_URL`) |
| Base de datos | MySQL 8.4, esquema `typing_battle` | Tablas `game_results` y `player_results` (migraciones de EF Core) | 3306 | Almacenamiento persistente y respaldos |

Solo la API se conecta a MySQL: la base de datos no se expone al navegador.

## Dependencias externas

| Servicio | Equipo | Relación con Typing | Qué hay que coordinar para desplegar |
|---|---|---|---|
| Shell | 3 | Carga el remote `typingGame` (ADR-003) y le entrega el contexto del juego: usuario y sala | Registrar la URL pública de `remoteEntry.js` en su configuración de remotes. Su origen público va en `Cors__AllowedOrigins` de la API |
| Matchmaking | 2 | Crea las salas. Al terminar la partida, la API le avisa con `POST /api/matches/{matchId}/finish` y un token M2M (ADR-004) | Su URL pública, que va en `Matchmaking__BaseUrl` |
| Profile | 1 | El Shell lo consulta para saber qué juegos tiene habilitados el usuario. Typing no lo llama directamente | Nada específico de Typing |
| Auth0 | 1 | Solo el Shell inicia sesión con Auth0: Typing no valida tokens de usuario (R-30). Emite el token M2M con el que la API avisa a Matchmaking | Registrar las URLs públicas del Shell (callbacks, logout y orígenes web) y el cliente M2M con el permiso `matches.finish` |

## Cómo se comunican

1. El usuario abre el Shell e inicia sesión con Auth0.
2. El Shell descarga `remoteEntry.js` desde el hosting del microfrontend y monta el juego.
3. El juego llama a la API por REST y se conecta al hub por SignalR, enviando el `currentUser` que le dio el Shell. Estas llamadas salen del navegador desde el origen del Shell, por eso la API debe permitirlo en CORS.
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

## ¿El microfrontend necesita su propia imagen?

El remote compila a archivos estáticos (`remoteEntry.js`, los chunks e `index.html`), así que no necesita un servidor de aplicación. La imagen de `frontend/Dockerfile` se usa en dos casos:

- **En local, con Docker Compose:** levanta el remote junto con la API y MySQL sin instalar Node, en el mismo puerto 4001 que espera el Shell.
- **En un hosting de contenedores:** cuando el frontend se despliega en el mismo lugar que la API, como en la opción C.

En un hosting estático, que es la opción propuesta, la imagen no se usa: se publica la carpeta `dist/` que genera `npm run build`. En los dos casos, `TYPING_API_URL` se fija al compilar, así que hace falta un build o una imagen por cada URL de la API:

```bash
docker build --build-arg TYPING_API_URL=https://typing.mi-dominio.com -t typing-battle-frontend frontend
```

La imagen sirve los archivos con nginx sin root (`frontend/nginx.conf`) y aplica estas reglas, que también hay que revisar al elegir un hosting estático:

| Regla | Por qué |
|---|---|
| `Access-Control-Allow-Origin: *` | El Shell descarga `remoteEntry.js` y los chunks desde otro origen. Son archivos públicos y no usan cookies |
| `remoteEntry.js` e `index.html` con `Cache-Control: no-cache` | No llevan hash en el nombre: así el Shell recibe la versión nueva después de cada despliegue |
| Chunks con hash en caché por un año (`immutable`) | Su nombre cambia cuando cambia el contenido |
| Un archivo que no existe devuelve 404 | Si falta un chunk, el error es claro, en lugar de recibir `index.html` como si fuera JavaScript |

Netlify, por ejemplo, permite configurar estos encabezados con un archivo `_headers`; GitHub Pages no permite encabezados propios.

## Opciones de hosting

Las condiciones se revisaron el 26 de octubre de 2026 en las páginas oficiales (ver [Fuentes](#fuentes)). Cambian con frecuencia, así que hay que confirmarlas antes de crear las cuentas.

Criterios: costo para el equipo, soporte de WebSockets, MySQL administrado, HTTPS incluido, si el servicio se apaga cuando no hay uso y si acepta la imagen de Docker de la API.

| | A. Render + Aiven + GitHub Pages | B. Azure for Students | C. Una VM con Docker Compose |
|---|---|---|---|
| API | Render, plan gratuito, con la imagen de Docker | App Service Linux (plan B1) con la imagen de Docker | Contenedor `api` del compose |
| MySQL | Aiven, plan gratuito | Azure Database for MySQL (Flexible Server) | Contenedor `mysql` del compose |
| Frontend | GitHub Pages o Netlify | GitHub Pages o Azure Static Web Apps | Contenedor `frontend` del compose |
| Costo | USD 0 | Se descuenta del crédito de USD 100 por 12 meses, sin tarjeta | Horas de VM pequeña incluidas en Azure for Students |
| WebSockets | Sí | Sí. En el plan gratuito F1, máximo 5 conexiones | Sí |
| HTTPS | Incluido | Incluido (`*.azurewebsites.net`) | Lo configura el equipo: dominio y certificado |
| ¿Se apaga sin uso? | Sí. La API se duerme tras 15 minutos sin peticiones HTTP ni mensajes WebSocket y tarda cerca de 1 minuto en despertar | No (plan B1) | No |
| Límites | API: 512 MB de RAM y 0,1 CPU. MySQL: 1 GB de disco, una base gratuita por organización; Aiven puede apagarla si no se usa, avisando antes | El crédito se agota según el consumo mensual de App Service y MySQL | 1 GB de RAM para MySQL, la API y nginx juntos |
| Trabajo de administración | Bajo | Bajo | Alto: servidor, actualizaciones, certificados y respaldos |

Consecuencias para Typing:

- **A.** Cuando la API se duerme, se pierden las partidas en curso. Eso solo pasa después de 15 minutos sin tráfico, así que no afecta una partida activa. Antes de una demostración hay que abrir `/health` para despertar la API.
- **B.** El plan gratuito F1 no sirve para el juego, porque con 5 conexiones WebSocket solo caben unas pocas partidas a la vez. Con B1, el crédito alcanza solo algunos meses, así que hay que calcular el costo en la calculadora de precios de Azure antes de crear los recursos. El crédito es personal, de la cuenta de un estudiante.
- **C.** Reproduce exactamente el entorno local, pero agrega trabajo de operación que no forma parte del curso.

## Decisión

**Estado: propuesta, pendiente de confirmar con el Tech Lead y con los equipos 1, 2 y 3.** Si el Tech Lead define un hosting común para todos los equipos, esa decisión reemplaza esta propuesta.

- **Propuesta: opción A.** No cuesta nada, no depende del crédito de una sola persona y usa directamente la imagen de Docker de la API. Su limitación, que la API se duerme, se resuelve despertándola antes de usarla.
- **Alternativa: opción B**, si se necesita que la API esté siempre disponible, por ejemplo para que el profesor pruebe en cualquier momento.

Dónde corre cada servicio:

| Servicio | Equipo | Hosting | URL pública | Estado |
|---|---|---|---|---|
| API y hub de Typing | 4 | Render (propuesta) | Por definir | Pendiente |
| Microfrontend de Typing | 4 | GitHub Pages (propuesta) | Por definir | Pendiente |
| MySQL de Typing | 4 | Aiven (propuesta) | No aplica: solo la API se conecta | Pendiente |
| Shell | 3 | Por confirmar con el Equipo 3 | Por definir | Pendiente |
| Matchmaking | 2 | Por confirmar con el Equipo 2 | Por definir | Pendiente |
| Profile | 1 | Por confirmar con el Equipo 1 | Por definir | Pendiente |
| Auth0 | 1 | Tenant actual del Equipo 1 | Ya existe | Falta registrar las URLs públicas del Shell |

Las URLs públicas se completan cuando se haga el despliegue.

## Fuentes

- Render: [Deploy for Free](https://render.com/docs/free) y [servicios gratuitos activos con mensajes WebSocket](https://render.com/changelog/free-web-services-now-remain-active-while-receiving-websocket-messages)
- Aiven: [MySQL free tier](https://aiven.io/docs/products/mysql/concepts/mysql-free-tier)
- Microsoft: [Azure for Students](https://azure.microsoft.com/en-us/free/students), [Azure Database for MySQL con una cuenta gratuita](https://learn.microsoft.com/azure/mysql/flexible-server/how-to-deploy-on-azure-free-account) y [WebSockets en planes Free de App Service](https://learn.microsoft.com/en-us/answers/questions/1358483/cant-connect-to-free-webapp-on-azure-through-webso)

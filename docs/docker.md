# Typing Battle con Docker

Cómo construir, levantar y comprobar MySQL, la API, el microfrontend de Typing y el generador de datos de ejemplo con Docker Compose, sin instalar .NET ni Node.

**Requisitos:** Docker Desktop abierto (en la barra de tareas debe decir *Engine running*) y los puertos 3306, 5015 y 4001 libres. Todos los comandos se corren desde la raíz del repo.

| Servicio | Contenedor | URL local | Imagen |
|---|---|---|---|
| `mysql` | `typing-battle-mysql` | `localhost:3306` | `mysql:8.4` |
| `api` | `typing-battle-api` | `http://localhost:5015` | `src/backend/TypingBattle.Api/Dockerfile` |
| `frontend` | `typing-battle-frontend` | `http://localhost:4001/remoteEntry.js` | `src/frontend/Dockerfile` |
| `data-generator` | `typing-battle-data-generator` | Sin puerto: termina al registrar los datos | `src/data-generator/Dockerfile` |

## 1. Preparar `.env`

El compose no tiene valores por defecto: sin `.env` se detiene y dice qué variable falta. La primera vez, copiar la plantilla; sus valores sirven para desarrollo local:

```powershell
Copy-Item .env.example .env
```

`.env` no se sube a git. Qué hace cada variable: README, [Variables de `.env`](../README.md#variables-de-env).

## 2. Construir y levantar

```powershell
docker compose up -d --build
```

La primera vez descarga las imágenes base (.NET, Node, nginx y MySQL) y las dependencias, así que tarda unos minutos. Después usa la caché y tarda segundos.

Compose arranca MySQL y espera a que esté *healthy* antes de iniciar la API, porque la API aplica las migraciones al arrancar. El frontend no depende de los otros servicios: son archivos estáticos. El generador de datos arranca después de la API, espera a que responda `/health` y registra las partidas de ejemplo.

## 3. Comprobar que funciona

```powershell
docker compose ps
```

`typing-battle-mysql` debe aparecer como `(healthy)`, la API y el frontend como `Up`, y `typing-battle-data-generator` como `Exited (0)`: terminó bien y no se reinicia. `docker compose ps -a` muestra también los contenedores que ya terminaron.

```powershell
curl.exe -i http://localhost:5015/health
```

Debe responder `200 OK` y `Healthy`. La comprobación incluye la conexión a MySQL.

```powershell
curl.exe -I http://localhost:4001/remoteEntry.js
```

Debe responder `200 OK`, con `Access-Control-Allow-Origin: *` y `Cache-Control: no-cache`.

En PowerShell, `curl` es otro comando; por eso se escribe `curl.exe`.

### Datos de ejemplo

```powershell
docker compose logs data-generator
```

Debe terminar con `Datos de ejemplo: 30 partidas nuevas, 0 ya existían, 0 con error.` (la primera vez; después, `0 partidas nuevas, 30 ya existían`).

```powershell
curl.exe "http://localhost:5015/api/games/typing/players/demo-ana/history?limit=3"
```

Debe devolver partidas. Qué crea y cómo configurarlo: README, [Datos de ejemplo](../README.md#datos-de-ejemplo).

### Jugar una partida

1. Levantar los servicios de los otros equipos, cada uno como indica el README de su repo: el Shell, Profile y Matchmaking. Los puertos están en el README de este repo, en «Integración con los otros equipos».
2. Abrir `http://localhost:4000`, iniciar sesión y jugar una partida de Typing hasta ver los resultados.
3. Confirmar que el resultado quedó guardado:

```powershell
docker exec typing-battle-mysql sh -c 'mysql -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" "$MYSQL_DATABASE" -e "SELECT MatchId, WinnerUserId, FinishedAt FROM game_results ORDER BY CreatedAt DESC LIMIT 3;"'
```

El comando usa el usuario y la contraseña que el contenedor recibió de `.env`, así que no hace falta escribirlos. El aviso `Using a password on the command line interface can be insecure` es normal.

## 4. Ver los logs

```powershell
docker compose logs -f api
```

`-f` sigue mostrando los logs nuevos; se sale con `Ctrl+C`. Para otro servicio, cambiar `api` por `mysql` o `frontend`.

Al arrancar, la API muestra dos avisos de *DataProtection* sobre las claves dentro del contenedor. No afectan a la API, que no usa cookies ni datos protegidos.

## 5. Actualizar después de cambiar el código

```powershell
docker compose up -d --build
```

Vuelve a construir solo lo que cambió y recrea esos contenedores. Para un solo servicio: `docker compose up -d --build api`.

Si cambia `TYPING_API_URL`, hay que reconstruir el frontend, porque la URL queda fija al compilar: `docker compose up -d --build frontend`.

## 6. Detener y borrar

| Comando | Qué hace |
|---|---|
| `docker compose stop` | Detiene los contenedores sin borrarlos |
| `docker compose down` | Borra los contenedores. **Los datos de MySQL se conservan** en el volumen `typing-mysql-data` |
| `docker compose down -v` | Borra los contenedores **y los datos de MySQL**: se pierden los resultados guardados, pero los datos de ejemplo se vuelven a generar al levantar de nuevo. Hace falta, por ejemplo, después de cambiar las contraseñas de MySQL en `.env` |
| `docker image rm typing-battle-api:local typing-battle-frontend:local typing-battle-data-generator:local` | Borra las imágenes construidas, para liberar espacio |

## 7. Construir las imágenes sin Compose

Son los mismos comandos que corre el CI (los jobs «Docker» de los workflows CI - Backend, CI - Frontend y CI - Generador de datos):

```powershell
docker build -f src/backend/TypingBattle.Api/Dockerfile -t typing-battle-api .
```

```powershell
docker build --build-arg TYPING_API_URL=https://typing.mi-dominio.com -t typing-battle-frontend src/frontend
```

La imagen de la API no lleva configuración: al ejecutarla hay que pasar al menos `ConnectionStrings__Typing` (lista completa en el README, en «Configuración (variables de entorno)»). Las dos imágenes corren sin root: la API como el usuario `app` y nginx como `nginx`.

## Problemas comunes

| Síntoma | Causa | Solución |
|---|---|---|
| `failed to connect to the docker API` | Docker Desktop no está abierto | Abrirlo y esperar a que diga *Engine running* |
| `required variable … is missing a value` | No existe el `.env` o le falta una variable: el compose no tiene valores por defecto | `Copy-Item .env.example .env`, o agregar al `.env` la variable que nombra el error |
| `port is already allocated` o `bind: address already in use` | `dotnet run`, `npm start` u otro MySQL ya usan el puerto 5015, 4001 o 3306 | Detenerlos. Para ver qué usa un puerto: `Get-NetTCPConnection -LocalPort 5015` |
| `typing-battle-api` no aparece en `docker compose ps` | La API se detuvo al arrancar | Ver el error con `docker compose ps -a` y `docker compose logs api` |
| `typing-battle-data-generator` aparece como `Exited (1)` | La API no respondió a tiempo o rechazó las partidas | Ver el motivo con `docker compose logs data-generator`; si la API tardó, subir `DATA_WAIT_SECONDS` en `.env` y `docker compose up -d` |
| El historial de `demo-ana` está vacío | El generador no terminó o falló | `docker compose logs data-generator`. Al terminar sin errores, `docker compose up -d` lo vuelve a correr sin duplicar datos |
| La API se detiene con `Access denied` de MySQL después de cambiar `.env` | MySQL aplica las contraseñas solo al crear el volumen | `docker compose down -v` (borra los datos) y volver a levantar |
| La API se detiene con `Table 'game_results' already exists` | La base se creó con el historial de migraciones anterior, que se rehízo (una migración por tabla) | `docker compose down -v` (borra los datos, que se vuelven a generar) y `docker compose up -d --build` |
| El navegador bloquea las llamadas del juego a la API (error de CORS en la consola) | El origen del Shell no está permitido en la API | En `.env`: `CORS_SHELL_ORIGIN` con el origen del Shell; luego `docker compose up -d api` |
| El juego llama a una URL de la API equivocada | `TYPING_API_URL` quedó fija al construir la imagen | `docker compose up -d --build frontend` |

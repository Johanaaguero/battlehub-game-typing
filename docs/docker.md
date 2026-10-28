# Typing Battle con Docker

Cómo construir, levantar y comprobar MySQL, la API y el microfrontend de Typing con Docker Compose, sin instalar .NET ni Node.

**Requisitos:** Docker Desktop abierto (en la barra de tareas debe decir *Engine running*) y los puertos 3306, 5015 y 4001 libres. Todos los comandos se corren desde la raíz del repo.

| Servicio | Contenedor | URL local | Imagen |
|---|---|---|---|
| `mysql` | `typing-battle-mysql` | `localhost:3306` | `mysql:8.4` |
| `api` | `typing-battle-api` | `http://localhost:5015` | `src/backend/TypingBattle.Api/Dockerfile` |
| `frontend` | `typing-battle-frontend` | `http://localhost:4001/remoteEntry.js` | `src/frontend/Dockerfile` |

## 1. Preparar `.env` (opcional)

Sin `.env`, el compose usa los valores de desarrollo y todo funciona. Solo hace falta para cambiar algún valor como las contraseñas de MySQL:

```powershell
Copy-Item .env.example .env
```

`.env` no se sube a git. Qué hace cada variable: README, [Variables de `.env`](../README.md#variables-de-env).

## 2. Construir y levantar

```powershell
docker compose up -d --build
```

La primera vez descarga las imágenes base (.NET, Node, nginx y MySQL) y las dependencias, así que tarda unos minutos. Después usa la caché y tarda segundos.

Compose arranca MySQL y espera a que esté *healthy* antes de iniciar la API, porque la API aplica las migraciones al arrancar. El frontend no depende de los otros servicios: son archivos estáticos.

## 3. Comprobar que funciona

```powershell
docker compose ps
```

`typing-battle-mysql` debe aparecer como `(healthy)` y los otros dos como `Up`.

```powershell
curl.exe -i http://localhost:5015/health
```

Debe responder `200 OK` y `Healthy`. La comprobación incluye la conexión a MySQL.

```powershell
curl.exe -I http://localhost:4001/remoteEntry.js
```

Debe responder `200 OK`, con `Access-Control-Allow-Origin: *` y `Cache-Control: no-cache`.

En PowerShell, `curl` es otro comando; por eso se escribe `curl.exe`.

### Jugar una partida

1. Levantar los servicios de los otros equipos, cada uno como indica el README de su repo: el Shell, Profile y Matchmaking. Los puertos están en el README de este repo, en «Integración con los otros equipos».
2. Abrir `http://localhost:4000`, iniciar sesión y jugar una partida de Typing hasta ver los resultados.
3. Confirmar que el resultado quedó guardado:

```powershell
docker exec typing-battle-mysql mysql -utyping -ptyping_dev typing_battle -e "SELECT MatchId, WinnerUserId, FinishedAt FROM game_results ORDER BY CreatedAt DESC LIMIT 3;"
```

El aviso `Using a password on the command line interface can be insecure` es normal: es la contraseña de desarrollo.

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
| `docker compose down -v` | Borra los contenedores **y los datos de MySQL**: se pierden los resultados guardados. Hace falta, por ejemplo, después de cambiar las contraseñas de MySQL en `.env` |
| `docker image rm typing-battle-api:local typing-battle-frontend:local` | Borra las imágenes construidas, para liberar espacio |

## 7. Construir las imágenes sin Compose

Son los mismos comandos que corre el CI (job «Docker (imágenes)»):

```powershell
docker build -f src/backend/TypingBattle.Api/Dockerfile -t typing-battle-api .
```

```powershell
docker build --build-arg TYPING_API_URL=https://typing.mi-dominio.com -t typing-battle-frontend src/frontend
```

La imagen de la API no lleva configuración: al ejecutarla hay que pasar al menos `ConnectionStrings__Typing` (lista completa en el README, en «Configuración fuera de Development»). Las dos imágenes corren sin root: la API como el usuario `app` y nginx como `nginx`.

## Problemas comunes

| Síntoma | Causa | Solución |
|---|---|---|
| `failed to connect to the docker API` | Docker Desktop no está abierto | Abrirlo y esperar a que diga *Engine running* |
| `port is already allocated` o `bind: address already in use` | `dotnet run`, `npm start`, `Start-Integration.ps1` u otro MySQL ya usan el puerto 5015, 4001 o 3306 | Detenerlos. Para ver qué usa un puerto: `Get-NetTCPConnection -LocalPort 5015` |
| `typing-battle-api` no aparece en `docker compose ps` | La API se detuvo al arrancar | Ver el error con `docker compose ps -a` y `docker compose logs api` |
| La API se detiene con `Access denied` de MySQL después de cambiar `.env` | MySQL aplica las contraseñas solo al crear el volumen | `docker compose down -v` (borra los datos) y volver a levantar |
| El navegador bloquea las llamadas del juego a la API (error de CORS en la consola) | El origen del Shell no está permitido en la API | En `.env`: `CORS_SHELL_ORIGIN` con el origen del Shell; luego `docker compose up -d api` |
| El juego llama a una URL de la API equivocada | `TYPING_API_URL` quedó fija al construir la imagen | `docker compose up -d --build frontend` |

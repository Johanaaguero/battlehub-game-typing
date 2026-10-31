# Seguridad: secretos y configuración

Resultado de la auditoría de secretos del repo y reglas para que ningún secreto llegue a git ni a las imágenes de Docker.

## Auditoría de secretos (26 de octubre de 2026)

**Resultado: no hay secretos reales en el historial del repo.**

| | |
|---|---|
| Herramienta | gitleaks v8.30.1 (imagen `ghcr.io/gitleaks/gitleaks:v8.30.1`), con sus reglas por defecto |
| Alcance | Todas las ramas y referencias locales (`--log-opts="--all"`): 27 commits, sin contar los merges |
| Hallazgos | 2, los dos falsos positivos |
| Resultado con `.gitleaksignore` | `no leaks found` |

| Commit | Archivo | Regla | Qué es | Decisión |
|---|---|---|---|---|
| `e7ff544` (en `main`) | `scripts/Start-Integration.ps1` (ya eliminado), línea 30 | `generic-api-key` | Client ID del cliente M2M de Auth0 | Falso positivo, agregado a `.gitleaksignore` |
| `cb83cd0` (stash local de GitHub Desktop, no está en GitHub) | `scripts/Start-Integration.ps1` (ya eliminado), línea 30 | `generic-api-key` | El mismo Client ID | Falso positivo, agregado a `.gitleaksignore` |

El Client ID no es un secreto: es un identificador público. Auth0 solo entrega un token M2M si se presenta junto con el Client Secret, y el Client Secret no está en el repo: el script `Start-Integration.ps1`, ya eliminado, lo pedía con `Read-Host -AsSecureString` al ejecutarse. La API ya no usa ese cliente M2M (R-30, R-46).

### Revisión manual complementaria

Además de gitleaks, se revisó el historial completo con git:

- Ningún `ClientSecret` tiene un valor real. El único con valor es `secreto-de-prueba`, en una prueba unitaria (`MatchmakingClientTests`, eliminada junto con el cliente de Matchmaking).
- No hay claves privadas, JWT ni tokens de servicios (GitHub, AWS u otros).
- Nunca se versionaron archivos `.env`, `*.pem`, `*.pfx`, `*.key` ni `appsettings.Local.json`.
- Hay valores de configuración públicos que no son secretos: el dominio del tenant de Auth0, los audiences y el Client ID del cliente M2M (la API ya no los usa).

### Contraseñas de desarrollo (intencionales)

| Valor | Dónde aparece | Para qué |
|---|---|---|
| `typing_dev` | `.env.example` (valor de ejemplo) y README | Usuario `typing` del MySQL local |
| `typing_root` | `.env.example` (valor de ejemplo) y README | Usuario `root` del MySQL local |
| `ci_root` | `.github/workflows/ci.yml` | Usuario `root` del MySQL temporal del CI |

Solo sirven para bases de datos locales o del CI, que no se exponen fuera de la máquina. Ningún despliegue las usa: ahí la conexión llega por `ConnectionStrings__Typing`.

En el código no queda ninguna (R-09): ni en los `appsettings`, ni en las herramientas de EF, ni en las pruebas, ni en el frontend, ni en `docker-compose.yml`. Todo se lee de variables de entorno.

> **Decisión del profesor (28 de octubre de 2026): opción B.** `docker-compose.yml` no tiene valores por defecto: exige cada variable con `${VARIABLE:?falta en .env}` y se detiene si falta alguna. Antes de la primera vez hay que crear el `.env` con `cp .env.example .env`. `.env.example` es la plantilla documentada (R-32) y trae valores de ejemplo para desarrollo local.

## Reglas para no filtrar secretos

- **Variables de entorno.** El único secreto de la API, `ConnectionStrings__Typing`, solo se configura como variable de entorno; la lista completa está en el README, en «Configuración (variables de entorno)».
- **`.gitignore`.** Ignora `.env` y `.env.*` (salvo `.env.example`), `appsettings.Local.json`, claves y certificados (`*.pem`, `*.pfx`, `*.key`, `*.p12`), `docker-compose.override.yml` y los reportes de gitleaks.
- **`.dockerignore` de la raíz** (imagen de la API). Solo entran el código de `TypingBattle.Api`, sus `appsettings` y la solución. Quedan fuera los secretos, `.git`, el CI, `bin`/`obj`, `src/frontend`, `tests` y `docs`.
- **`src/frontend/.dockerignore`** (imagen del microfrontend). Solo entran `src`, la configuración de Webpack y TypeScript y los `package*.json`. Quedan fuera `node_modules`, `dist`, las pruebas, `.env` y `.npmrc`.
- **`appsettings.Development.json`** entra en la imagen de la API, pero no tiene secretos y solo se carga con `ASPNETCORE_ENVIRONMENT=Development`.

## Cómo repetir la auditoría

Con Docker Desktop en marcha, desde la raíz del repo (PowerShell):

```powershell
docker run --rm -v "${PWD}:/repo:ro" ghcr.io/gitleaks/gitleaks:v8.30.1 git /repo --log-opts="--all" --redact -v
```

`--redact` oculta los valores encontrados y `:ro` monta el repo en solo lectura. El resultado esperado es `no leaks found`. Si aparece un hallazgo nuevo:

1. Revisar si es un secreto real.
2. Si es un falso positivo, agregar su `Fingerprint` a `.gitleaksignore`, con un comentario que explique por qué.
3. Si es un secreto real, seguir la sección siguiente.

## Si se filtra un secreto

1. **Rotarlo de inmediato en su origen.** Para una contraseña de MySQL, cambiarla en el servidor. Si fuera un secreto de otro equipo (por ejemplo, de Auth0), avisarle al Equipo 1, que administra el tenant.
2. Actualizar el valor nuevo en las variables de entorno del hosting.
3. Quitar el secreto del código en un commit nuevo. Borrarlo del historial no basta: si ya se subió a GitHub, hay que darlo por comprometido aunque se reescriba el historial.
4. Avisar al Tech Lead.

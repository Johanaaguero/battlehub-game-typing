# Imagen del microfrontend de Typing Battle (remote typingGame). Se construye desde la raíz del repo:
#   docker build --build-arg TYPING_API_URL=https://typing.mi-dominio.com -t typing-battle-frontend src/frontend
# La URL de la API (TYPING_API_URL) es obligatoria y queda fija al compilar: sin ella el build falla.
# Con docker compose la pasa el servicio frontend.

# Etapa 1: compilar con Node.
FROM node:22-slim AS build
WORKDIR /app

# Primero solo package*.json: mientras no cambien las dependencias, Docker reutiliza npm ci desde la caché.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# Va después de npm ci para que cambiar la URL no obligue a reinstalar las dependencias.
# Docker la expone como variable de entorno durante el build, que es donde la lee Webpack.
ARG TYPING_API_URL
RUN npm run build

# Etapa 2: solo los archivos estáticos, servidos por nginx; sin Node ni el código fuente.
# nginx-unprivileged corre sin root y escucha en el puerto 8080.
FROM nginxinc/nginx-unprivileged:1.30-alpine AS final
# CORS para el Shell, caché según el tipo de archivo y versión de nginx oculta (ver nginx.conf).
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080

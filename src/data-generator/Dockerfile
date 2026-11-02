# Imagen del generador de datos de ejemplo (R-28). Se construye con su propio contexto:
#   docker build -t typing-battle-data-generator src/data-generator
# No lleva configuración: la URL de la API y la cantidad de partidas llegan por variables de entorno al
# ejecutar el contenedor (lista en el README, en «Datos de ejemplo»). No tiene dependencias: no hay npm install.
FROM node:22-slim
WORKDIR /app

COPY package.json ./
COPY src ./src

# Usuario sin privilegios que trae la imagen base: el generador solo necesita salir a la red.
USER node
CMD ["node", "src/main.js"]

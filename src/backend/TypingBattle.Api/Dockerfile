# Imagen de la API de Typing Battle. Se construye desde la raíz del repo:
#   docker build -f src/backend/TypingBattle.Api/Dockerfile -t typing-battle-api .
# La configuración (MySQL, Auth0, CORS y Matchmaking) llega por variables de entorno al ejecutar el
# contenedor: la imagen no lleva credenciales. La lista de variables está en el README.

# Etapa 1: compilar y publicar con el SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo el .csproj: mientras no cambien las dependencias, Docker reutiliza el restore desde la caché.
COPY src/backend/TypingBattle.Api/TypingBattle.Api.csproj src/backend/TypingBattle.Api/
RUN dotnet restore src/backend/TypingBattle.Api/TypingBattle.Api.csproj

COPY src/backend/TypingBattle.Api/ src/backend/TypingBattle.Api/
RUN dotnet publish src/backend/TypingBattle.Api/TypingBattle.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# Etapa 2: solo el runtime de ASP.NET Core, sin el SDK ni el código fuente.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# La imagen base escucha en el puerto 8080 (ASPNETCORE_HTTP_PORTS).
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TypingBattle.Api.dll"]

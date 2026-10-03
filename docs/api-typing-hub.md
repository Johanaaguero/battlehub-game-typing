# API del Hub SignalR: /hubs/typing

Este documento describe el contrato mínimo del hub SignalR para Typing Battle. Sigue las decisiones del profesor: Typing recibe matchId y currentUser; matchmaking es dueño de la sala; al finalizar la partida el backend persiste el resultado usando IResultsService y notifica el fin de la partida.

NOTA: Los tipos de persistencia y los DTOs de resultados ya existen en Results/ResultsDtos.cs (SaveResultRequest, PlayerResultRequest, etc.). No duplicar esos tipos: el Hub debe reutilizarlos cuando proceda.

Mensajes y flujo

1) JoinMatch
   - Método invocado por el cliente cuando entra a una sala de Typing.
   - Payload: { matchId: string, currentUser: string }
   - Efecto en servidor: añadir la conexión al grupo SignalR con nombre igual a matchId.
   - Si la partida todavía no existe en memoria, la crea el primer jugador que entra y el servidor fija su inicio (startedAt). La sala la administra Matchmaking; el Shell solo carga el juego después de MatchStarted.
   - No se puede entrar a una partida ya terminada: responde false y envía joinFailed { matchId, reason: "join_rejected" }.
   - Respuestas: confirmación al cliente; broadcast al grupo de la lista de jugadores si procede.

2) LeaveMatch
   - Payload: { matchId: string, currentUser: string }
   - Efecto: eliminar la conexión del grupo.

3) PlayerUpdate (estado intermedio)
   - Uso: enviar actualizaciones de estado/score del jugador durante la partida.
   - Payload sugerido: { matchId: string, currentUser: string, score: int?, wpm: double?, accuracy: double?, timestamp: string }
   - El servidor puede validar/filtrar y retransmitir al grupo para sincronizar vistas.
   - Si score llega null, el servidor lo calcula con TypingScore: round(wpm × accuracy / 100 × 10), es decir, 60 ppm al 100 % = 600 puntos. playerUpdate retransmite ese puntaje del servidor.

4) EndMatch / RequestEndMatch
   - El flujo final puede ser server-authoritative: el cliente solicita el fin y el servidor valida y decide.
   - Payload (cuando el cliente provee el resumen final):
	 {
	   matchId: string,
	   currentUser: string,
	   players: [{ userId: string, displayName?: string, score: int }],
	   startedAt: string (ISO-8601 Z),
	   finishedAt: string (ISO-8601 Z),
	   winnerUserId?: string,
	   metadata?: object  // libre, pero debe contener metadata.players[] si se envían wpm/accuracy
	 }

   - El servidor construirá un SaveResultRequest reutilizando PlayerResultRequest y el objeto metadata.
   - El servidor llamará a IResultsService.SaveAsync(request).
   - Resultado: el servidor notificará al grupo del match sobre el fin de la partida y el outcome (éxito, ya existe, errores de validación).
   - Cuándo se invoca: cada cliente llama a EndMatch al acabarse su temporizador, que corre desde start() y no desde la primera tecla, así que todos terminan a la vez. El cliente envía players, startedAt, finishedAt y winnerUserId en null: los jugadores y sus puntajes salen del estado del servidor, el inicio es la entrada del primer jugador, el fin es el momento del pedido y el ganador es el de mayor puntaje.
   - El primero que la termina guarda el resultado y todos reciben matchEnded "created". Los que piden después reciben "already_exists" solo ellos (Clients.Caller), para no pisar el resultado que ya ven los demás.
   - Quien completa el texto antes de tiempo deja de escribir y sus métricas quedan congeladas hasta el final de la partida.

Metadata para Typing
 - Para que TypingMetadata.ExtractPlayerStats funcione, incluir en metadata un arreglo `players` con objetos que al menos contengan { userId: string, wpm?: number, accuracy?: number }.
 - Ejemplo mínimo de metadata:
   {
	 "textId": "t-07",
	 "players": [ { "userId": "user-001", "wpm": 62.4, "accuracy": 96.1 }, ... ]
   }

Notificación a Matchmaking
 - Requisito: al finalizar la partida, además de persistir el resultado, Typing debe notificar a Matchmaking que la partida terminó.
 - Mecanismo: callback HTTP de ADR-004 (propuesto por el Equipo 2): POST {Matchmaking:BaseUrl}/api/matches/{matchId}/finish, sin cuerpo (los resultados son solo de Typing), con un token M2M de Auth0 (client credentials) que debe traer el permiso matches.finish.
 - Implementación: MatchFinishedQueue (IMatchFinishedNotifier) encola solo las partidas recién guardadas ("created") y vuelve de inmediato, así el hub anuncia el resultado sin esperar a Matchmaking. MatchFinishedWorker envía el aviso con MatchmakingClient, que reintenta errores temporales (red, 408, 429, 5xx) y no reintenta rechazos (401, 403, 404, 409).
 - Configuración: sección Matchmaking de appsettings.json. Con BaseUrl vacía no se llama a nadie y solo queda en el log. Las credenciales M2M (Matchmaking:Auth0:Domain, ClientId, ClientSecret y Audience) van en variables de entorno o user-secrets; sin ellas el aviso viaja sin token.
 - Estado: el Matchmaking Service todavía no publica /finish (su rama feat/matchmaking-api-integration tiene la API REST, pero sin ese endpoint). Cuando exista, basta con configurar BaseUrl y las credenciales.

Seguridad y validación
 - El hub exige un usuario autenticado (política TypingPlay): un JWT de Auth0, que desde el navegador viaja en ?access_token=, o en desarrollo la identidad de ?dev_user= (solo con Auth:Mode=Development). Sin usuario, la conexión responde 401.
 - El jugador es siempre el del token (claim sub). Si el currentUser que envía el cliente no coincide, JoinMatch responde false con joinFailed { reason: "user_mismatch" }; SendPlayerUpdate responde con updateFailed por el mismo motivo.
 - Solo un jugador que entró a la partida puede enviar métricas (si no, updateFailed { reason: "not_joined" }) o terminarla (si no, matchEnded "invalid" solo para quien lo pidió).
 - Reusar ResultValidator para validar los datos antes de persistir.
 - Limitar tamaño de metadata usando ResultValidator.MaxMetadataChars y número de jugadores con ResultValidator.MaxPlayers.

Errores y outcomes
 - Si IResultsService.SaveAsync devuelve AlreadyExists: notificar al cliente que el resultado ya estaba registrado.
 - Si devuelve Invalid: reenviar errores de validación al solicitante.
 - Si Created: enviar el GameResultDto al grupo/solicitante.

Ejemplo de mensajes del servidor al grupo
 - "playerJoined": { userId }
 - "playerLeft": { userId }
 - "playerUpdate": { userId, score, wpm, accuracy }
 - "matchEnded": { matchId, outcome: "created" | "already_exists" | "invalid", result?: GameResultDto, errors?: {...} }

Notas finales
 - No duplicar los DTOs persistentes: cuando sea necesario construir una petición de persistencia, usar SaveResultRequest y PlayerResultRequest definidos en Results/ResultsDtos.cs.
 - Este documento es la base para las implementaciones y pruebas. Las decisiones de integración con Matchmaking y el método de notificación quedan marcadas como "decisión del equipo".

# API del Hub SignalR: /hubs/typing

Este documento describe el contrato mínimo del hub SignalR para Typing Battle. Sigue las decisiones del profesor: Typing recibe matchId y currentUser; matchmaking es dueño de la sala; al finalizar la partida el backend persiste el resultado usando IResultsService y notifica el fin de la partida.

NOTA: Los tipos de persistencia y los DTOs de resultados ya existen en Results/ResultsDtos.cs (SaveResultRequest, PlayerResultRequest, etc.). No duplicar esos tipos: el Hub debe reutilizarlos cuando proceda.

Mensajes y flujo

1) JoinMatch
   - Método invocado por el cliente cuando entra a una sala de Typing.
   - Payload: { matchId: string, currentUser: string }
   - Efecto en servidor: añadir la conexión al grupo SignalR con nombre igual a matchId.
   - Respuestas: confirmación al cliente; broadcast al grupo de la lista de jugadores si procede.

2) LeaveMatch
   - Payload: { matchId: string, currentUser: string }
   - Efecto: eliminar la conexión del grupo.

3) PlayerUpdate (estado intermedio)
   - Uso: enviar actualizaciones de estado/score del jugador durante la partida.
   - Payload sugerido: { matchId: string, currentUser: string, score: int?, wpm: double?, accuracy: double?, timestamp: string }
   - El servidor puede validar/filtrar y retransmitir al grupo para sincronizar vistas.

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

Metadata para Typing
 - Para que TypingMetadata.ExtractPlayerStats funcione, incluir en metadata un arreglo `players` con objetos que al menos contengan { userId: string, wpm?: number, accuracy?: number }.
 - Ejemplo mínimo de metadata:
   {
	 "textId": "t-07",
	 "players": [ { "userId": "user-001", "wpm": 62.4, "accuracy": 96.1 }, ... ]
   }

Notificación a Matchmaking
 - Requisito: al finalizar la partida, además de persistir el resultado, Typing debe notificar a Matchmaking que la partida terminó.
 - Mecanismo de notificación: decisión del equipo (no definido por el profesor). Opciones posibles:
   - Callback HTTP configurable a una URL de matchmaking.
   - Publicación en un bus de mensajes (RabbitMQ, Kafka, etc.).
   - Emisión de un evento SignalR a un hub de matchmaking (si existe).
 - Implementación: TypingMatchService debe exponer un hook configurable (IOptions o delegate) para notificar a Matchmaking. La elección concreta se decidirá e implementará como "decisión del equipo".

Seguridad y validación
 - Preferir Context.UserIdentifier cuando haya autenticación; si no, validar currentUser recibido en cada llamada (decisión del equipo sobre tolerancia a suplantación).
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

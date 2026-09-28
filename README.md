

# BattleHub - Typing Battle

Microservicio del Equipo 4 responsable del juego **Typing Battle** dentro de la plataforma BattleHub.

## Responsabilidades

Este servicio será responsable de:

- Gestionar la lógica de las partidas de Typing Battle.
- Proporcionar comunicación en tiempo real mediante SignalR.
- Mantener persistencia propia de los resultados de las partidas.
- Exponer una API REST para resultados, historial y estadísticas.
- Integrarse con el sistema de Matchmaking de BattleHub.
- Proporcionar el microfrontend correspondiente al juego.
- Incluir pruebas unitarias y de integración.

## Estructura inicial

```text
battlehub-game-typing/
├── .github/
│   └── workflows/
├── docs/
├── src/
│   └── backend/
│       └── TypingBattle.Api/
├── tests/
│   ├── TypingBattle.UnitTests/
│   └── TypingBattle.IntegrationTests/
├── .gitignore
├── README.md
└── TypingBattle.slnx
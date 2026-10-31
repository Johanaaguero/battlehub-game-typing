using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace TypingBattle.IntegrationTests;

/// <summary>Conexión al hub de la API de pruebas. El juego no se autentica: el jugador viaja en cada petición.</summary>
public static class TestHub
{
    /// <summary>Conexión al hub sobre el servidor de pruebas (sin iniciar).</summary>
    public static HubConnection CreateHubConnection(this WebApplicationFactory<Program> app) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(app.Server.BaseAddress, "/hubs/typing"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();
}

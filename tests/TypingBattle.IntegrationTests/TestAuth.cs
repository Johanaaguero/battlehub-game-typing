using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Identidad para las pruebas con Auth:Mode=Development (TypingApiFactory lo activa): encabezados en la API REST y
/// parámetros de la URL en el hub, igual que hace el microfrontend con el Shell simulado.
/// </summary>
public static class TestAuth
{
    public const string ResultsWritePermission = "games.typing.results.write";

    /// <summary>Cliente HTTP autenticado como <paramref name="userId"/>, con los permisos indicados.</summary>
    public static HttpClient CreateAuthenticatedClient(
        this WebApplicationFactory<Program> app, string userId = "tester", params string[] permissions)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", userId);
        if (permissions.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Dev-Permissions", string.Join(',', permissions));
        }

        return client;
    }

    /// <summary>Conexión al hub autenticada como <paramref name="userId"/> (sin iniciar).</summary>
    public static HubConnection CreateHubConnection(this WebApplicationFactory<Program> app, string userId)
    {
        var url = new Uri(app.Server.BaseAddress, $"/hubs/typing?dev_user={Uri.EscapeDataString(userId)}");
        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();
    }
}

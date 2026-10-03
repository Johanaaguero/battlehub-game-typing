using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Matchmaking;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Al guardar el resultado de una partida, el servicio avisa a Matchmaking con POST /api/matches/{matchId}/finish
/// (ADR-004). Matchmaking se reemplaza por un servidor falso que solo registra las llamadas.
/// </summary>
[Trait("Category", "Integration")]
public class MatchmakingNotificationTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    private sealed class FakeMatchmaking : HttpMessageHandler
    {
        public ConcurrentQueue<(HttpMethod Method, string Path)> Calls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Enqueue((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    [Fact]
    public async Task AlTerminarLaPartida_SeAvisaAMatchmaking_UnaSolaVez()
    {
        var matchmaking = new FakeMatchmaking();
        await using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Matchmaking:BaseUrl", "http://matchmaking.test");
            builder.ConfigureTestServices(services => services
                .AddHttpClient(MatchmakingClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => matchmaking));
        });
        var http = app.CreateClient();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(http.BaseAddress!, "/hubs/typing"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();

        var matchId = $"match-{Guid.NewGuid():N}";
        Assert.True(await connection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, "ana", "Ana")));
        Assert.True(await connection.InvokeAsync<bool>("SendPlayerUpdate", new PlayerUpdateDto(matchId, "ana", null, 50.0, 100.0, DateTime.UtcNow)));

        var end = new EndMatchRequest(matchId, "ana", null, null, null, null, null);
        await connection.InvokeAsync("EndMatch", end);
        await connection.InvokeAsync("EndMatch", end); // segundo pedido: "already_exists", no se vuelve a avisar

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (matchmaking.Calls.IsEmpty)
        {
            await Task.Delay(20, timeout.Token);
        }
        await Task.Delay(300);

        var call = Assert.Single(matchmaking.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal($"/api/matches/{matchId}/finish", call.Path);
    }
}

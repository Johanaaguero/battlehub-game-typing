using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TypingBattle.Api.Hubs;

namespace TypingBattle.IntegrationTests;

[Trait("Category", "Integration")]
public class TypingHubIntegrationTests : IClassFixture<TypingApiFactory>
{
    private readonly TypingApiFactory _factory;
    private readonly HttpClient _client;

    public TypingHubIntegrationTests(TypingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient("espectador");
    }

    [Fact]
    public async Task HubFlow_Join_Update_End_PersistsResult()
    {
        var matchId = Guid.NewGuid().ToString("N");
        var userA = "uA" + Guid.NewGuid().ToString("N");
        var userB = "uB" + Guid.NewGuid().ToString("N");

        // Ensure match exists in the shared store (matchmaking responsibility)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<TypingMatchStore>();
            store.GetOrCreate(matchId);
        }

        // Cada jugador usa su propia conexión autenticada: el hub no deja actuar en nombre de otro usuario.
        await using var connectionA = _factory.CreateHubConnection(userA);
        await using var connectionB = _factory.CreateHubConnection(userB);

        var joinedNotifications = new List<string>();
        var updates = new List<(string user, int? score)>();
        var ended = new List<MatchEndedNotification>();

        connectionA.On<PlayerJoinedNotification>("playerJoined", n => joinedNotifications.Add(n.UserId));
        connectionA.On<PlayerUpdateNotification>("playerUpdate", u => updates.Add((u.UserId, u.Score)));
        connectionA.On<MatchEndedNotification>("matchEnded", m => ended.Add(m));

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        // Join both players
        var r1 = await connectionA.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, userA, "Player A"));
        Assert.True(r1);
        var r2 = await connectionB.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, userB, "Player B"));
        Assert.True(r2);

        // Wait briefly to receive broadcasts
        await Task.Delay(100);
        Assert.Contains(userA, joinedNotifications);
        Assert.Contains(userB, joinedNotifications);

        // Send updates
        var updateA = new PlayerUpdateDto(matchId, userA, 100, 50.0, 95.0, DateTime.UtcNow);
        await connectionA.InvokeAsync<bool>("SendPlayerUpdate", updateA);
        var updateB = new PlayerUpdateDto(matchId, userB, 200, 60.0, 98.0, DateTime.UtcNow);
        await connectionB.InvokeAsync<bool>("SendPlayerUpdate", updateB);

        await Task.Delay(100);
        Assert.Contains(updates, u => u.user == userA && u.score == 100);
        Assert.Contains(updates, u => u.user == userB && u.score == 200);

        // End match
        var endReq = new EndMatchRequest(matchId, userA, null, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow, userB, null);
        await connectionA.InvokeAsync("EndMatch", endReq);

        // Wait for matchEnded notification
        await Task.Delay(200);
        Assert.NotEmpty(ended);

        // Verify result persisted via REST API
        var get = await _client.GetAsync($"/api/games/typing/results/{matchId}");
        Assert.Equal(System.Net.HttpStatusCode.OK, get.StatusCode);

        var json = JsonNode.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal(matchId, (string?)json!["matchId"]);
        Assert.Equal("typing", (string?)json!["gameType"]);

        await connectionA.StopAsync();
        await connectionB.StopAsync();
    }
}

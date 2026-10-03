using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TypingBattle.Api.Hubs;

namespace TypingBattle.Api.Matchmaking;

public static class MatchmakingServiceCollectionExtensions
{
    /// <summary>Registra el aviso de fin de partida a Matchmaking (ADR-004). Sin Matchmaking:BaseUrl queda inactivo.</summary>
    public static IServiceCollection AddMatchmakingIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MatchmakingOptions>(configuration.GetSection(MatchmakingOptions.SectionName));

        services.AddHttpClient(MatchmakingClient.HttpClientName, ConfigureTimeout);
        services.AddHttpClient(Auth0MachineTokenProvider.HttpClientName, ConfigureTimeout);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IMachineTokenProvider, Auth0MachineTokenProvider>();
        services.AddSingleton<MatchmakingClient>();
        services.AddSingleton<MatchFinishedQueue>();
        services.AddSingleton<IMatchFinishedNotifier>(sp => sp.GetRequiredService<MatchFinishedQueue>());
        services.AddHostedService<MatchFinishedWorker>();
        return services;
    }

    private static void ConfigureTimeout(IServiceProvider services, HttpClient client)
    {
        var seconds = services.GetRequiredService<IOptions<MatchmakingOptions>>().Value.TimeoutSeconds;
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 60));
    }
}

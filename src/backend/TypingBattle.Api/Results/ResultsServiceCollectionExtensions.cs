namespace TypingBattle.Api.Results;

public static class ResultsServiceCollectionExtensions
{
    /// <summary>Registra el servicio de resultados. Requiere la persistencia (<c>AddTypingPersistence</c>).</summary>
    public static IServiceCollection AddTypingResults(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IResultsService, ResultsService>();
        return services;
    }
}

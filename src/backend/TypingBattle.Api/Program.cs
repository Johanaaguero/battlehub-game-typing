using TypingBattle.Api.Auth;
using TypingBattle.Api.Persistence;
using TypingBattle.Api.Results;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Matchmaking;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
// JWT de Auth0 (Auth:Mode=Auth0) o identidad de desarrollo (Auth:Mode=Development, solo Development y Testing).
builder.Services.AddTypingAuth(builder.Configuration, builder.Environment);
builder.Services.AddTypingPersistence(builder.Configuration);
builder.Services.AddTypingResults();
// SignalR and Typing services
builder.Services.AddSignalR();
// Shared in-memory store for matches (singleton)
builder.Services.AddSingleton<TypingMatchStore>();
// Facade service for match logic (scoped to allow injecting IResultsService which is scoped)
builder.Services.AddScoped<TypingMatchService>();
// Aviso de fin de partida a Matchmaking (ADR-004). Inactivo mientras Matchmaking:BaseUrl esté vacío.
builder.Services.AddMatchmakingIntegration(builder.Configuration);

// El microfrontend corre en otro origen (el Shell), así que el navegador exige CORS.
// Los orígenes permitidos salen de la configuración: Cors:AllowedOrigins.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

var app = builder.Build();

await app.MigrateTypingDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
// CORS antes de la autenticación: el preflight (OPTIONS) se responde sin token.
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHub<TypingHub>("/hubs/typing").RequireAuthorization(TypingPolicies.Play);
app.MapResultsEndpoints();

app.Run();

// Permite que los proyectos de pruebas usen WebApplicationFactory<Program>.
public partial class Program;

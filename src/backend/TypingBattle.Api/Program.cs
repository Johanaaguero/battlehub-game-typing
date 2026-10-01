using TypingBattle.Api.Persistence;
using TypingBattle.Api.Results;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddTypingPersistence(builder.Configuration);
builder.Services.AddTypingResults();

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
app.UseCors();

app.MapHealthChecks("/health");
app.MapResultsEndpoints();

app.Run();

// Permite que los proyectos de pruebas usen WebApplicationFactory<Program>.
public partial class Program;

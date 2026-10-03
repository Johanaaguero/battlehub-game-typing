using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TypingBattle.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>Ruta del hub: solo ahí se acepta el token en la URL.</summary>
    public const string HubsPathPrefix = "/hubs";

    /// <summary>
    /// Registra la autenticación según <c>Auth:Mode</c> y las políticas de <see cref="TypingPolicies"/>. Falla al
    /// arrancar, con un mensaje claro, si la configuración está incompleta o si se intenta el modo Development fuera
    /// de los entornos Development y Testing.
    /// </summary>
    public static IServiceCollection AddTypingAuth(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        if (string.Equals(options.Mode, AuthOptions.DevelopmentMode, StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    $"Auth:Mode={AuthOptions.DevelopmentMode} solo se permite en los entornos Development y Testing " +
                    $"(entorno actual: {environment.EnvironmentName}). Use Auth:Mode={AuthOptions.Auth0Mode}.");
            }

            services
                .AddAuthentication(DevelopmentAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.SchemeName, _ => { });
        }
        else if (string.Equals(options.Mode, AuthOptions.Auth0Mode, StringComparison.OrdinalIgnoreCase))
        {
            var domain = NormalizeDomain(options.Domain);
            if (domain.Length == 0 || string.IsNullOrWhiteSpace(options.Audience))
            {
                throw new InvalidOperationException(
                    "Con Auth:Mode=Auth0 hay que configurar Auth:Domain (por ejemplo mi-tenant.us.auth0.com) y " +
                    "Auth:Audience (el identificador de la API en Auth0). Para desarrollo local sin Auth0 use " +
                    "Auth__Mode=Development.");
            }

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = $"https://{domain}/";
                    jwt.Audience = options.Audience.Trim();

                    // Sin transformar los nombres de los claims: "sub" sigue siendo "sub".
                    jwt.MapInboundClaims = false;

                    // Un WebSocket del navegador no puede enviar el encabezado Authorization: SignalR manda el token
                    // en la URL (?access_token=...). Solo se acepta ahí para el hub, como hace el lobby de Matchmaking.
                    jwt.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var token = context.Request.Query["access_token"].ToString();
                            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(HubsPathPrefix))
                            {
                                context.Token = token;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }
        else
        {
            throw new InvalidOperationException(
                $"Auth:Mode '{options.Mode}' no es válido. Use '{AuthOptions.Auth0Mode}' o '{AuthOptions.DevelopmentMode}'.");
        }

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(TypingPolicies.Play, policy =>
            {
                policy.RequireAuthenticatedUser();
                var permission = options.RequiredPermission.Trim();
                if (permission.Length > 0)
                {
                    policy.RequireAssertion(context => PermissionCheck.HasPermission(context.User, permission));
                }
            });

            authorization.AddPolicy(TypingPolicies.ResultsWriter, policy =>
            {
                policy.RequireAuthenticatedUser();
                var permission = options.ResultsWritePermission.Trim();
                policy.RequireAssertion(context => permission.Length > 0 && PermissionCheck.HasPermission(context.User, permission));
            });
        });

        return services;
    }

    /// <summary>Acepta el dominio con o sin esquema y barra final: <c>https://mi-tenant.us.auth0.com/</c> → <c>mi-tenant.us.auth0.com</c>.</summary>
    public static string NormalizeDomain(string domain)
    {
        var value = domain.Trim();
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = value["https://".Length..];
        }

        return value.TrimEnd('/');
    }
}

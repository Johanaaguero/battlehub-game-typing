using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TypingBattle.Api.Auth;

namespace TypingBattle.UnitTests.Auth;

[Trait("Category", "Unit")]
public class AuthConfigurationTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "TypingBattle.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static void AddAuth(string environment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        new ServiceCollection().AddTypingAuth(configuration, new FakeEnvironment(environment));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void ModoDevelopment_SePermiteEnDesarrolloYPruebas(string environment)
    {
        AddAuth(environment, ("Auth:Mode", "Development"));
    }

    [Fact]
    public void ModoDevelopment_EnProduccion_NoArranca()
    {
        var error = Assert.Throws<InvalidOperationException>(() => AddAuth("Production", ("Auth:Mode", "Development")));
        Assert.Contains("Production", error.Message);
    }

    [Fact]
    public void ModoAuth0_SinDominioOAudience_NoArranca()
    {
        Assert.Throws<InvalidOperationException>(() => AddAuth("Production", ("Auth:Mode", "Auth0"), ("Auth:Audience", "https://api")));
        Assert.Throws<InvalidOperationException>(() => AddAuth("Production", ("Auth:Mode", "Auth0"), ("Auth:Domain", "tenant.auth0.com")));
    }

    [Fact]
    public void ModoAuth0_PorDefecto_SinConfiguracion_NoArranca()
    {
        Assert.Throws<InvalidOperationException>(() => AddAuth("Production"));
    }

    [Fact]
    public void ModoAuth0_Completo_Arranca()
    {
        AddAuth("Production", ("Auth:Mode", "Auth0"), ("Auth:Domain", "tenant.us.auth0.com"), ("Auth:Audience", "https://api"));
    }

    [Fact]
    public void ModoDesconocido_NoArranca()
    {
        Assert.Throws<InvalidOperationException>(() => AddAuth("Development", ("Auth:Mode", "Basic")));
    }

    [Theory]
    [InlineData("tenant.us.auth0.com", "tenant.us.auth0.com")]
    [InlineData("https://tenant.us.auth0.com/", "tenant.us.auth0.com")]
    [InlineData("  tenant.us.auth0.com/ ", "tenant.us.auth0.com")]
    public void NormalizeDomain_QuitaEsquemaYBarraFinal(string domain, string expected)
    {
        Assert.Equal(expected, AuthServiceCollectionExtensions.NormalizeDomain(domain));
    }

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Permiso_SeLeeDelClaimPermissionsODelScope()
    {
        Assert.True(PermissionCheck.HasPermission(User(("permissions", "games.typing.play")), "games.typing.play"));
        Assert.True(PermissionCheck.HasPermission(User(("scope", "openid games.typing.results.write")), "games.typing.results.write"));
    }

    [Fact]
    public void Permiso_ExigeCoincidenciaExacta()
    {
        Assert.False(PermissionCheck.HasPermission(User(("permissions", "games.typing.play.admin")), "games.typing.play"));
        Assert.False(PermissionCheck.HasPermission(User(("permissions", "GAMES.TYPING.PLAY")), "games.typing.play"));
        Assert.False(PermissionCheck.HasPermission(User(("name", "games.typing.play")), "games.typing.play"));
    }

    [Fact]
    public void Identidad_UsaElSubYElNombreComoRespaldo()
    {
        var user = User(("sub", "auth0|ana"), ("name", "Ana"));

        Assert.Equal("auth0|ana", user.GetUserId());
        Assert.Equal("Ana", user.GetDisplayName());
        Assert.Equal("auth0|luis", User(("sub", "auth0|luis")).GetDisplayName());
        Assert.Null(User().GetUserId());
    }
}

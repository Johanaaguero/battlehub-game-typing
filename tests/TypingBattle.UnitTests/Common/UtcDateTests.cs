using TypingBattle.Api.Common;

namespace TypingBattle.UnitTests.Common;

[Trait("Category", "Unit")]
public class UtcDateTests
{
    [Fact]
    public void Utc_SeDevuelveIgual()
    {
        var value = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);

        Assert.True(UtcDate.TryNormalize(value, out var utc));
        Assert.Equal(value, utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void Local_SeConvierteAUtc()
    {
        var local = new DateTime(2026, 9, 2, 14, 0, 0, DateTimeKind.Local);

        Assert.True(UtcDate.TryNormalize(local, out var utc));
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(local.ToUniversalTime(), utc);
    }

    [Fact]
    public void SinZona_SeRechaza()
    {
        var unspecified = new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Unspecified);

        Assert.False(UtcDate.TryNormalize(unspecified, out _));
    }
}

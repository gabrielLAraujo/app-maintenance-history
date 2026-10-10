using Garage.Domain.Common;
using Garage.Domain.Sharing;

namespace Garage.Domain.Tests.Sharing;

public class ShareLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expiry = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid VehicleId = Guid.Parse("0197b3a0-0000-7000-8000-000000000002");
    private const string Token = "k3Zp9vQ2mXr8TnYb4LwA7dCe1HsJ6uGf0oIq5PxVhNE";

    private static ShareLink Create(DateTimeOffset? expiresAt = null, string? label = "Para o comprador") =>
        ShareLink.Create(VehicleId, Token, label, includeCosts: true, includePlate: false, expiresAt, Now);

    [Fact]
    public void Create_populates_fields_and_generates_a_version7_id()
    {
        var link = Create(Expiry);

        Assert.Equal(7, link.Id.Version);
        Assert.Equal(VehicleId, link.VehicleId);
        Assert.Equal(Token, link.Token);
        Assert.Equal("Para o comprador", link.Label);
        Assert.True(link.IncludeCosts);
        Assert.False(link.IncludePlate);
        Assert.Equal(Expiry, link.ExpiresAt);
        Assert.Null(link.RevokedAt);
        Assert.Equal(Now, link.CreatedAt);
    }

    [Fact]
    public void Create_accepts_missing_label_and_expiry()
    {
        var link = Create(expiresAt: null, label: null);

        Assert.Null(link.Label);
        Assert.Null(link.ExpiresAt);
    }

    [Fact]
    public void Link_without_expiry_is_valid()
    {
        var link = Create();

        Assert.True(link.IsValidAt(Now));
        Assert.True(link.IsValidAt(Now.AddYears(50)));
    }

    [Fact]
    public void Link_is_valid_before_expiry()
    {
        var link = Create(Expiry);

        Assert.True(link.IsValidAt(Expiry.AddTicks(-1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(86_400)]
    public void Link_is_invalid_at_or_after_expiry(int secondsAfterExpiry)
    {
        var link = Create(Expiry);

        Assert.False(link.IsValidAt(Expiry.AddSeconds(secondsAfterExpiry)));
    }

    [Fact]
    public void Revoked_link_is_invalid_and_revoke_is_idempotent()
    {
        var link = Create(Expiry);
        var firstRevocation = Now.AddDays(1);

        link.Revoke(firstRevocation);

        Assert.Equal(firstRevocation, link.RevokedAt);
        Assert.False(link.IsValidAt(firstRevocation));
        Assert.False(link.IsValidAt(Now.AddDays(2)));

        link.Revoke(Now.AddDays(3));

        Assert.Equal(firstRevocation, link.RevokedAt);
        Assert.False(link.IsValidAt(Now.AddDays(3)));
    }

    [Fact]
    public void Revoked_link_without_expiry_is_invalid()
    {
        var link = Create();

        link.Revoke(Now);

        Assert.False(link.IsValidAt(Now));
        Assert.False(link.IsValidAt(Now.AddYears(1)));
    }
}

public class ShareLinkErrorsTests
{
    [Fact]
    public void NotFound_is_a_not_found_error_in_pt_BR()
    {
        var error = ShareLinkErrors.NotFound;

        Assert.Equal("share_link.not_found", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Link não encontrado ou expirado.", error.Message);
    }
}

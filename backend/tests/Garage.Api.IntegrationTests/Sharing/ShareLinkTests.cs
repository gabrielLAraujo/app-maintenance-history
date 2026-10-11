using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Garage.Api.IntegrationTests.Sharing;

[Collection("api")]
public sealed class ShareLinkTests(GarageApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_returns_43_char_token_and_path()
    {
        using var client = await UserAsync();
        var vehicleId = await client.CreateVehicleAsync();
        // Com o offset de São Paulo, como o front manda: vale o mesmo instante.
        var expiresAt = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.FromHours(-3));

        using var response = await client.PostAsJsonAsync(
            LinksUrl(vehicleId),
            new { label = "  Comprador  ", includeCosts = true, includePlate = false, expiresAt },
            TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var token = Token(link);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.Equal($"/p/{token}", link.GetProperty("path").GetString());
        Assert.Equal($"{LinksUrl(vehicleId)}/{Id(link)}", response.Headers.Location?.OriginalString);
        Assert.Equal("Comprador", link.GetProperty("label").GetString());
        Assert.True(link.GetProperty("includeCosts").GetBoolean());
        Assert.False(link.GetProperty("includePlate").GetBoolean());
        Assert.Equal(expiresAt, link.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, link.GetProperty("revokedAt").ValueKind);
        Assert.Equal(Now, link.GetProperty("createdAt").GetDateTimeOffset());
        Assert.True(link.GetProperty("isActive").GetBoolean());

        // Sem os campos opcionais: sem rótulo, sem custos, sem placa e sem validade. Cada link tem o seu token.
        var minimal = await client.CreateShareLinkAsync(vehicleId);

        Assert.Equal(JsonValueKind.Null, minimal.GetProperty("label").ValueKind);
        Assert.False(minimal.GetProperty("includeCosts").GetBoolean());
        Assert.False(minimal.GetProperty("includePlate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, minimal.GetProperty("expiresAt").ValueKind);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", Token(minimal));
        Assert.NotEqual(token, Token(minimal));
    }

    [Fact]
    public async Task Revoke_marks_link_inactive()
    {
        var clock = new FakeTimeProvider(Now);
        using var timed = factory.WithClock(clock);
        using var client = await timed.CreateUserClientAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var revoked = await client.CreateShareLinkAsync(vehicleId, new { label = "Revogado" });
        clock.Advance(TimeSpan.FromMinutes(1));
        var kept = await client.CreateShareLinkAsync(vehicleId, new { label = "Fica" });
        clock.Advance(TimeSpan.FromMinutes(1));
        var revokedAt = clock.GetUtcNow();

        using var response = await client.DeleteAsync($"{LinksUrl(vehicleId)}/{Id(revoked)}");
        clock.Advance(TimeSpan.FromMinutes(1));
        // S2: revogar é irreversível; revogar de novo não falha e mantém o primeiro instante.
        using var again = await client.DeleteAsync($"{LinksUrl(vehicleId)}/{Id(revoked)}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        // Os mais novos primeiro; os revogados continuam na lista, inativos.
        var links = (await client.GetShareLinksAsync(vehicleId)).EnumerateArray().ToList();
        Assert.Equal(new[] { Id(kept), Id(revoked) }, links.Select(Id));
        Assert.True(links[0].GetProperty("isActive").GetBoolean());
        Assert.Equal(JsonValueKind.Null, links[0].GetProperty("revokedAt").ValueKind);
        Assert.False(links[1].GetProperty("isActive").GetBoolean());
        Assert.Equal(revokedAt, links[1].GetProperty("revokedAt").GetDateTimeOffset());
        Assert.Equal(Token(revoked), Token(links[1]));
        Assert.Equal($"/p/{Token(revoked)}", links[1].GetProperty("path").GetString());
        Assert.Equal("Revogado", links[1].GetProperty("label").GetString());
    }

    [Fact]
    public async Task Revoke_of_unknown_link_or_link_of_another_vehicle_returns_404()
    {
        using var client = await UserAsync();
        var vehicleA = await client.CreateVehicleAsync();
        var vehicleB = await client.CreateVehicleAsync();
        var link = await client.CreateShareLinkAsync(vehicleA);

        // Os links são sempre buscados por veículo e id: o link de A não é alcançável pelo veículo B.
        using var throughB = await client.DeleteAsync($"{LinksUrl(vehicleB)}/{Id(link)}");
        using var unknown = await client.DeleteAsync($"{LinksUrl(vehicleA)}/{Guid.NewGuid()}");

        await throughB.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");
        Assert.True(Assert.Single((await client.GetShareLinksAsync(vehicleA)).EnumerateArray()).GetProperty("isActive").GetBoolean());
        Assert.Equal(0, (await client.GetShareLinksAsync(vehicleB)).GetArrayLength());
    }

    [Fact]
    public async Task Expires_at_in_past_returns_400()
    {
        using var client = await UserAsync();
        var vehicleId = await client.CreateVehicleAsync();

        // A validade precisa ser futura: o instante atual também é recusado.
        foreach (var expiresAt in new[] { Now.AddDays(-1), Now })
        {
            using var response = await client.PostAsJsonAsync(LinksUrl(vehicleId), new { expiresAt }, TestJson.Options);

            var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
            Assert.Equal(new[] { "expiresAt" }, FieldNames(errors));
        }

        Assert.Equal(0, (await client.GetShareLinksAsync(vehicleId)).GetArrayLength());
        var future = await client.CreateShareLinkAsync(vehicleId, new { expiresAt = Now.AddSeconds(1) });
        Assert.Equal(Now.AddSeconds(1), future.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Label_longer_than_60_returns_400()
    {
        using var client = await UserAsync();
        var vehicleId = await client.CreateVehicleAsync();

        using var response = await client.PostAsJsonAsync(
            LinksUrl(vehicleId), new { label = new string('a', 61) }, TestJson.Options);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal(new[] { "label" }, FieldNames(errors));
        Assert.Equal(0, (await client.GetShareLinksAsync(vehicleId)).GetArrayLength());

        var limit = await client.CreateShareLinkAsync(vehicleId, new { label = new string('a', 60) });
        Assert.Equal(new string('a', 60), limit.GetProperty("label").GetString());
        // Rótulo em branco vira "sem rótulo".
        var blank = await client.CreateShareLinkAsync(vehicleId, new { label = "   " });
        Assert.Equal(JsonValueKind.Null, blank.GetProperty("label").ValueKind);
    }

    [Fact]
    public async Task Deleting_the_vehicle_deletes_its_links()
    {
        using var client = await UserAsync();
        var vehicleId = await client.CreateVehicleAsync();
        var token = Token(await client.CreateShareLinkAsync(vehicleId));
        Assert.True(await AnyLinkAsync(vehicleId));

        using var deleted = await client.DeleteAsync($"/api/vehicles/{vehicleId}");
        using var visitor = factory.CreateClient();
        using var view = await visitor.GetAsync($"/api/public/{token}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await AnyLinkAsync(vehicleId));
        await view.AssertProblemAsync(HttpStatusCode.NotFound, "share_link.not_found");
    }

    [Fact]
    public async Task Other_user_gets_404()
    {
        using var owner = await UserAsync();
        using var other = await factory.CreateUserClientAsync();
        using var anonymous = await factory.CreateAnonymousClientAsync();
        var vehicleId = await owner.CreateVehicleAsync();
        var link = await owner.CreateShareLinkAsync(vehicleId);

        using var list = await other.GetAsync(LinksUrl(vehicleId));
        using var create = await other.PostAsJsonAsync(LinksUrl(vehicleId), new { }, TestJson.Options);
        using var revoke = await other.DeleteAsync($"{LinksUrl(vehicleId)}/{Id(link)}");
        using var anonymousList = await anonymous.GetAsync(LinksUrl(vehicleId));

        await list.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await create.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        await revoke.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
        var only = Assert.Single((await owner.GetShareLinksAsync(vehicleId)).EnumerateArray());
        Assert.Equal(Id(link), Id(only));
        Assert.True(only.GetProperty("isActive").GetBoolean());
    }

    private async Task<HttpClient> UserAsync()
    {
        factory.Clock.SetUtcNow(Now);
        return await factory.CreateUserClientAsync();
    }

    private async Task<bool> AnyLinkAsync(Guid vehicleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IReadDbContext>()
            .Query<ShareLink>()
            .AnyAsync(link => link.VehicleId == vehicleId);
    }

    private static string LinksUrl(Guid vehicleId) => $"/api/vehicles/{vehicleId}/share-links";

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();

    private static string Token(JsonElement link) => link.GetProperty("token").GetString()!;

    private static string[] FieldNames(JsonElement errors) =>
        errors.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}

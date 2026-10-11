using Garage.Api.Endpoints;

namespace Garage.Api.Auth;

public sealed record AuthConfigResponse(bool RegistrationEnabled);

internal sealed class AuthConfigEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder api)
    {
        var config = new AuthConfigResponse(api.IsRegistrationEnabled());

        api.MapGet("/auth/config", () => TypedResults.Ok(config))
            .AllowAnonymous()
            .WithName("GetAuthConfig")
            .WithTags("Auth")
            .Produces<AuthConfigResponse>(StatusCodes.Status200OK);
    }
}

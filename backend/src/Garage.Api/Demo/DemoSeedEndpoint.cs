using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Demo;
using Garage.Application.Demo.SeedDemoData;

namespace Garage.Api.Demo;

internal sealed class DemoSeedEndpoint : IEndpoint
{
    private const string EnabledSetting = "Demo:Enabled";

    public void Map(RouteGroupBuilder api)
    {
        if (!IsEnabled(api))
            return;

        api.MapPost("/demo/seed", HandleAsync)
            .WithName("SeedDemoData")
            .WithTags("Demo")
            .Produces<DemoSeedResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary><c>Demo:Enabled</c> (padrão false), lido ao mapear as rotas: desligado, a rota nem existe (404).</summary>
    private static bool IsEnabled(IEndpointRouteBuilder routes) =>
        routes.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>(EnabledSetting);

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new SeedDemoDataCommand(), ct);

        return result.ToHttpResult(seeded => TypedResults.Created("/api/vehicles", seeded));
    }
}

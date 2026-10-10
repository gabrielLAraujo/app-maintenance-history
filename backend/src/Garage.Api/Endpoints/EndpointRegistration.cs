namespace Garage.Api.Endpoints;

public static class EndpointRegistration
{
    /// <summary>Instancia e mapeia todo <see cref="IEndpoint"/> concreto do assembly da API, em ordem estável.</summary>
    public static void MapEndpoints(this WebApplication app, RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(api);

        var endpointTypes = typeof(EndpointRegistration).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                           && typeof(IEndpoint).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var type in endpointTypes)
            ((IEndpoint)Activator.CreateInstance(type, nonPublic: true)!).Map(api);
    }
}

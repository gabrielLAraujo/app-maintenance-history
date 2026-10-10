namespace Garage.Api.Endpoints;

public interface IEndpoint
{
    /// <param name="api">Grupo <c>/api</c>, que exige autenticação por padrão.</param>
    void Map(RouteGroupBuilder api);
}

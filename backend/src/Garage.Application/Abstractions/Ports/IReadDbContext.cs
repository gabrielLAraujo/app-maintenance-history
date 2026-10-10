namespace Garage.Application.Abstractions.Ports;

public interface IReadDbContext
{
    /// <summary>Consulta somente leitura (sempre <c>AsNoTracking</c>).</summary>
    IQueryable<T> Query<T>() where T : class;
}

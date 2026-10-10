namespace Garage.Application.Abstractions.Ports;

public interface ICurrentUser
{
    /// <summary>Id do usuário autenticado; lança <see cref="InvalidOperationException"/> se a requisição for anônima.</summary>
    Guid UserId { get; }
}

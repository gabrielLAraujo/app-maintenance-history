namespace Garage.Application.Abstractions.Ports;

public interface ICurrentUser
{
    /// <summary>Id do usuário autenticado; lança <see cref="InvalidOperationException"/> se a requisição for anônima.</summary>
    Guid UserId { get; }

    /// <summary>Se a sessão é de uma conta demo (claim <c>garage:demo</c>); falso na requisição anônima.</summary>
    bool IsDemo { get; }
}

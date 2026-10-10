namespace Garage.Application.Abstractions.Ports;

public interface IAppClock
{
    /// <summary>Instante atual em UTC.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Data de hoje no fuso da aplicação (<c>App:TimeZone</c>).</summary>
    DateOnly Today { get; }

    /// <summary>Ano de <see cref="Today"/>.</summary>
    int CurrentYear { get; }
}

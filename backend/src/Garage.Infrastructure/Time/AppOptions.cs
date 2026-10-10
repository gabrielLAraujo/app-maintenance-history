namespace Garage.Infrastructure.Time;

internal sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Fuso (id IANA) que define o "hoje" da aplicação.</summary>
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

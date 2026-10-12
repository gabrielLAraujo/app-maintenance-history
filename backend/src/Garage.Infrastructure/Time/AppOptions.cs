namespace Garage.Infrastructure.Time;

internal sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Fuso (id IANA) que define o "hoje" da aplicação. Validado ao subir (<see cref="IsKnownTimeZone"/>).</summary>
    public string TimeZone { get; set; } = "America/Sao_Paulo";

    /// <summary>O fuso existe no sistema (no Linux, vem do tzdata da imagem).</summary>
    public static bool IsKnownTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}

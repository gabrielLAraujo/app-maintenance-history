namespace Garage.Infrastructure.Demo;

/// <summary>Demo por visitante (spec 3.4). Desligada por padrão; produção liga com <c>Demo:Visitors:Enabled=true</c>.</summary>
public sealed class DemoVisitorOptions
{
    public const string SectionName = "Demo:Visitors";

    public bool Enabled { get; set; }

    /// <summary>Validade da conta e da sessão, em horas.</summary>
    public int TtlHours { get; set; } = 24;

    /// <summary>Teto de contas demo válidas ao mesmo tempo; atingido, <c>POST /auth/demo</c> responde 503.</summary>
    public int MaxActive { get; set; } = 100;

    public int MaxVehicles { get; set; } = 5;

    /// <summary>Intervalo da limpeza das contas vencidas; 0 desliga a limpeza automática.</summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}

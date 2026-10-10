using Garage.Domain.Common;

namespace Garage.Domain.Records;

public sealed class Part : Entity
{
    // Construtor sem parâmetros apenas para a materialização do EF Core; os inicializadores
    // `null!` satisfazem a análise de nulos e são sempre sobrescritos na materialização.
    private Part()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Brand { get; private set; }

    public string? PartNumber { get; private set; }

    public decimal Quantity { get; private set; }

    public PartUnit Unit { get; private set; }

    public Money? UnitPrice { get; private set; }

    /// <summary>Preço unitário × quantidade; zero quando a peça não tem preço informado.</summary>
    public Money LineTotal => UnitPrice?.Times(Quantity) ?? Money.Zero;

    internal static Part From(PartData data) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = data.Name,
        Brand = data.Brand,
        PartNumber = data.PartNumber,
        Quantity = data.Quantity,
        Unit = data.Unit,
        UnitPrice = data.UnitPrice
    };
}

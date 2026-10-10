namespace Garage.Domain.Vehicles;

public readonly record struct VehicleYear(int Manufacture, int Model)
{
    public static bool IsValid(int manufacture, int model, int currentYear) =>
        manufacture >= 1900
        && manufacture <= currentYear + 1
        && (model == manufacture || model == manufacture + 1);

    public static VehicleYear From(int manufacture, int model, int currentYear)
    {
        if (!IsValid(manufacture, model, currentYear))
            throw new ArgumentException("Ano de fabricação/modelo inválido.");

        return new VehicleYear(manufacture, model);
    }
}

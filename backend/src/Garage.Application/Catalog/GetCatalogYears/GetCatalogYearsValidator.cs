using FluentValidation;

namespace Garage.Application.Catalog.GetCatalogYears;

internal sealed class GetCatalogYearsValidator : AbstractValidator<GetCatalogYearsQuery>
{
    public GetCatalogYearsValidator()
    {
        RuleFor(x => x.MakeId).MustBeCatalogId("Código de marca inválido.");
        RuleFor(x => x.ModelId).MustBeCatalogId("Código de modelo inválido.");
    }
}

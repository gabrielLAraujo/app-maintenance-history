using FluentValidation;

namespace Garage.Application.Catalog.GetCatalogDetails;

internal sealed class GetCatalogDetailsValidator : AbstractValidator<GetCatalogDetailsQuery>
{
    public GetCatalogDetailsValidator()
    {
        RuleFor(x => x.MakeId).MustBeCatalogId("Código de marca inválido.");
        RuleFor(x => x.ModelId).MustBeCatalogId("Código de modelo inválido.");
        RuleFor(x => x.YearId).MustBeCatalogId("Código de ano inválido.");
    }
}

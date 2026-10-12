using FluentValidation;

namespace Garage.Application.Catalog.GetCatalogModels;

internal sealed class GetCatalogModelsValidator : AbstractValidator<GetCatalogModelsQuery>
{
    public GetCatalogModelsValidator() =>
        RuleFor(x => x.MakeId).MustBeCatalogId("Código de marca inválido.");
}

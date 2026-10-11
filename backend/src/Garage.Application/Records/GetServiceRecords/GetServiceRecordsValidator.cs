using FluentValidation;

namespace Garage.Application.Records.GetServiceRecords;

internal sealed class GetServiceRecordsValidator : AbstractValidator<GetServiceRecordsQuery>
{
    public GetServiceRecordsValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de registro inválido.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Categoria inválida.");

        RuleFor(x => x.Cursor)
            .Must(cursor => RecordCursor.TryDecode(cursor, out _)).WithMessage("Cursor inválido.")
            .When(x => !string.IsNullOrEmpty(x.Cursor));

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetServiceRecordsQuery.MaxPageSize)
            .WithMessage("O tamanho da página deve estar entre 1 e {To}.");
    }
}

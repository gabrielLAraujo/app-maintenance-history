using FluentValidation;

namespace Garage.Application.Records.UpdateServiceRecord;

internal sealed class UpdateServiceRecordValidator : AbstractValidator<UpdateServiceRecordCommand>
{
    public UpdateServiceRecordValidator(IValidator<RecordInput> record) =>
        // O corpo HTTP é plano: sem prefixo "record", as chaves de erro batem com os campos do formulário.
        RuleFor(x => x.Record).SetValidator(record).OverridePropertyName(string.Empty);
}

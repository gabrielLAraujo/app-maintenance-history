using Garage.Domain.Common;

namespace Garage.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error Sample = new("x", "y", ErrorType.DomainRule);

    [Fact]
    public void Result_value_of_failure_throws() =>
        Assert.Throws<InvalidOperationException>(() => Result<int>.Failure(new Error("x", "y", ErrorType.DomainRule)).Value);

    [Fact]
    public void Validation_error_carries_field_errors()
    {
        var e = Error.Validation(new Dictionary<string, string[]> { ["make"] = ["Obrigatório."] });
        Assert.Equal("validation.failed", e.Code);
        Assert.Equal(ErrorType.Validation, e.Type);
        Assert.NotNull(e.Metadata!["errors"]);
    }

    [Fact]
    public void Validation_error_has_ptbr_message_and_keeps_the_given_dictionary()
    {
        IReadOnlyDictionary<string, string[]> fieldErrors = new Dictionary<string, string[]> { ["make"] = ["Obrigatório."] };

        var e = Error.Validation(fieldErrors);

        Assert.Equal("Dados inválidos.", e.Message);
        Assert.Same(fieldErrors, e.Metadata!["errors"]);
    }

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        var result = Result.Failure(Sample);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(Sample, result.Error);
    }

    [Fact]
    public void Generic_success_exposes_value_and_no_error()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Generic_failure_carries_the_error()
    {
        var result = Result<int>.Failure(Sample);

        Assert.True(result.IsFailure);
        Assert.Same(Sample, result.Error);
    }

    [Fact]
    public void Value_converts_implicitly_to_successful_result()
    {
        Result<int> result = 7;

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void Error_converts_implicitly_to_failed_result()
    {
        Result plain = Sample;
        Result<int> generic = Sample;

        Assert.True(plain.IsFailure);
        Assert.Same(Sample, plain.Error);
        Assert.True(generic.IsFailure);
        Assert.Same(Sample, generic.Error);
    }

    [Fact]
    public void Interface_typed_result_is_created_through_Success()
    {
        IReadOnlyList<int> list = [1, 2, 3];

        var result = Result<IReadOnlyList<int>>.Success(list);

        Assert.True(result.IsSuccess);
        Assert.Same(list, result.Value);
    }

    [Fact]
    public void Unit_values_are_equal() => Assert.Equal(default, Unit.Value);
}

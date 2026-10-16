using Garage.Domain.Common;

namespace Garage.Api.Http;

public static class ResultHttpExtensions
{
    private const string FieldErrorsKey = "errors";

    /// <summary>Sucesso vira a resposta montada por <paramref name="onSuccess"/>; falha vira ProblemDetails (<see cref="ToProblem"/>).</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();
    }

    /// <summary>Sucesso vira 204 sem corpo; falha vira ProblemDetails.</summary>
    public static IResult ToNoContent(this Result<Unit> result) => result.ToHttpResult(_ => TypedResults.NoContent());

    /// <summary>
    /// Converte o erro em ProblemDetails com status pelo <see cref="ErrorType"/>, a mensagem em <c>detail</c> e as
    /// extensões <c>code</c> e metadados (valores nulos inclusive). <c>traceId</c> é incluído ao escrever a resposta.
    /// Validation com <c>Metadata["errors"]</c> vira ValidationProblem com <c>errors</c> por campo.
    /// </summary>
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var fieldErrors = error.Type == ErrorType.Validation
            ? error.Metadata?.GetValueOrDefault(FieldErrorsKey) as IReadOnlyDictionary<string, string[]>
            : null;

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code };
        foreach (var (key, value) in error.Metadata ?? new Dictionary<string, object?>())
        {
            if (fieldErrors is null || key != FieldErrorsKey)
                extensions.TryAdd(key, value);
        }

        if (fieldErrors is not null)
            return TypedResults.ValidationProblem(fieldErrors, detail: error.Message, extensions: extensions);

        return TypedResults.Problem(detail: error.Message, statusCode: StatusCodeFor(error.Type), extensions: extensions);
    }

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        ErrorType.UnsupportedMedia => StatusCodes.Status415UnsupportedMediaType,
        ErrorType.DomainRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de erro sem status HTTP mapeado.")
    };
}

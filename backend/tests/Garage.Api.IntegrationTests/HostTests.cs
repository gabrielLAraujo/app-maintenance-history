using System.Net;
using System.Text.Json;
using Garage.Api.Http;
using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace Garage.Api.IntegrationTests;

[Collection("api")]
public sealed class HostTests(GarageApiFactory factory)
{
    [Fact]
    public async Task Health_returns_200_without_authentication()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ready_returns_200_when_database_is_up()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("App:TimeZone", "Fuso/Inexistente")]
    [InlineData("App:TimeZone", "")]
    [InlineData("Storage:RootPath", "")]
    [InlineData("Storage:RootPath", "   ")]
    public void Invalid_configuration_stops_the_host_at_startup(string key, string value)
    {
        using var misconfigured = factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        // Falha ao subir, não na primeira requisição que precisar do valor.
        var exception = Record.Exception(() => misconfigured.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains(key, validation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exception_handler_hides_details()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IExceptionHandler>().OfType<GlobalExceptionHandler>().Single();
        var context = NewHttpContext(scope.ServiceProvider);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("segredo"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain("segredo", body);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"traceId\"", body);
    }

    [Fact]
    public async Task Exception_handler_ignores_requests_aborted_by_the_client()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var logger = new RecordingLogger();
        var handler = new GlobalExceptionHandler(scope.ServiceProvider.GetRequiredService<IProblemDetailsService>(), logger);
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = NewHttpContext(scope.ServiceProvider);
        context.RequestAborted = aborted.Token;

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        // O cliente desistiu: não é falha do servidor, e não há para quem escrever o corpo.
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.DoesNotContain(logger.Levels, level => level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Exception_handler_still_reports_a_cancellation_the_client_did_not_ask_for()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var logger = new RecordingLogger();
        var handler = new GlobalExceptionHandler(scope.ServiceProvider.GetRequiredService<IProblemDetailsService>(), logger);
        var context = NewHttpContext(scope.ServiceProvider);

        // Um timeout interno também é OperationCanceledException, mas com o cliente ainda esperando: é erro do servidor.
        var handled = await handler.TryHandleAsync(context, new OperationCanceledException("timeout"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains(LogLevel.Error, logger.Levels);
    }

    [Theory]
    [InlineData(true, true, 200, LogEventLevel.Information)]
    [InlineData(true, false, 200, LogEventLevel.Error)]
    [InlineData(false, false, 500, LogEventLevel.Error)]
    [InlineData(false, false, 503, LogEventLevel.Error)]
    [InlineData(false, false, 404, LogEventLevel.Information)]
    [InlineData(false, true, 200, LogEventLevel.Information)]
    public async Task Request_log_level_does_not_treat_client_aborts_as_errors(
        bool cancellationException, bool clientAborted, int status, LogEventLevel expected)
    {
        using var aborted = new CancellationTokenSource();
        if (clientAborted)
            await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token, Response = { StatusCode = status } };
        var exception = cancellationException ? new OperationCanceledException() : null;

        Assert.Equal(expected, RequestLoggingSetup.GetLevel(context, 12.5, exception));
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.PayloadTooLarge, 413)]
    [InlineData(ErrorType.UnsupportedMedia, 415)]
    [InlineData(ErrorType.DomainRule, 422)]
    [InlineData(ErrorType.TooManyRequests, 429)]
    [InlineData(ErrorType.Unavailable, 503)]
    public async Task ToProblem_maps_error_type_to_status_with_code_and_trace_id(ErrorType type, int status)
    {
        var (statusCode, problem) = await ExecuteAsync(new Error("sample.code", "Mensagem de teste.", type));

        Assert.Equal(status, statusCode);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal("sample.code", problem.GetProperty("code").GetString());
        Assert.Equal("Mensagem de teste.", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task ToProblem_writes_validation_errors_by_field()
    {
        var error = Error.Validation(new Dictionary<string, string[]> { ["parts[0].name"] = ["Informe o nome da peça."] });

        var (statusCode, problem) = await ExecuteAsync(error);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.Equal("Informe o nome da peça.", problem.GetProperty("errors").GetProperty("parts[0].name")[0].GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task ToProblem_writes_metadata_including_null_values()
    {
        var (statusCode, problem) = await ExecuteAsync(ServiceRecordErrors.OdometerOutOfOrder(120_000, null));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, statusCode);
        Assert.Equal("record.odometer_out_of_order", problem.GetProperty("code").GetString());
        Assert.Equal(120_000, problem.GetProperty("maxKmBefore").GetInt32());
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("minKmAfter").ValueKind);
    }

    private async Task<(int StatusCode, JsonElement Problem)> ExecuteAsync(Error error)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = NewHttpContext(scope.ServiceProvider);

        await error.ToProblem().ExecuteAsync(context);

        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        var problem = JsonSerializer.Deserialize<JsonElement>(await ReadBodyAsync(context), TestJson.Options);
        return (context.Response.StatusCode, problem);
    }

    private static DefaultHttpContext NewHttpContext(IServiceProvider services) =>
        new() { RequestServices = services, Response = { Body = new MemoryStream() } };

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Guarda o nível de cada entrada de log do <see cref="GlobalExceptionHandler"/>.</summary>
    private sealed class RecordingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }
}

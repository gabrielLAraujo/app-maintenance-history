using FluentValidation;
using Garage.Application;
using Garage.Application.Abstractions;
using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Garage.Api.IntegrationTests;

// Pipeline real do AddApplication (Sender + decorators) com handlers de teste e um IUnitOfWork que registra as chamadas.
[Collection("api")]
public sealed class PipelineTests : IDisposable
{
    private readonly List<string> _log = [];
    private readonly RecordingUnitOfWork _unitOfWork;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public PipelineTests()
    {
        _unitOfWork = new RecordingUnitOfWork(_log);

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(new RecordingLoggerProvider(_log)));
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton(_log);
        services.AddScoped<ICommandHandler<RenameParts, int>, RenamePartsHandler>();
        services.AddScoped<IValidator<RenameParts>, RenamePartsValidator>();
        services.AddScoped<IQueryHandler<FindPart, string>, FindPartHandler>();
        services.AddScoped<IValidator<FindPart>, FindPartValidator>();
        services.AddApplication();

        // Sem ValidateOnBuild: o AddApplication também registra os handlers reais, cujas portas (repositórios, relógio...)
        // vêm da Infrastructure. O grafo completo é validado ao subir a API (GarageApiFactory).
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    private ISender Sender => _scope.ServiceProvider.GetRequiredService<ISender>();

    [Fact]
    public async Task Successful_command_is_saved_inside_logging()
    {
        var result = await Sender.Send(new RenameParts([new PartInput("Filtro de óleo")]));

        Assert.Equal(1, result.Value);
        Assert.Equal(new[] { "handle", "save" }, _log.Take(2));
        Assert.StartsWith("log: RenameParts succeeded", Assert.Single(_log.Skip(2)));
    }

    [Fact]
    public async Task Invalid_command_returns_camel_case_field_errors_before_the_transaction()
    {
        var result = await Sender.Send(new RenameParts([new PartInput("")]));

        Assert.Equal("validation.failed", result.Error!.Code);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(result.Error.Metadata!["errors"]);
        Assert.Equal(new[] { "Informe o nome." }, errors["parts[0].name"]);
        Assert.StartsWith("log: RenameParts failed with validation.failed", Assert.Single(_log));
    }

    [Fact]
    public async Task Failed_command_is_discarded()
    {
        var result = await Sender.Send(new RenameParts([new PartInput(RenamePartsHandler.Rejected)]));

        Assert.Equal("part.rejected", result.Error!.Code);
        Assert.Equal(new[] { "handle", "discard" }, _log.Take(2));
    }

    [Fact]
    public async Task Save_failure_is_returned_instead_of_the_handler_result()
    {
        _unitOfWork.SaveResult = CommonErrors.ConcurrencyConflict;

        var result = await Sender.Send(new RenameParts([new PartInput("Filtro de ar")]));

        Assert.Same(CommonErrors.ConcurrencyConflict, result.Error);
        Assert.Equal(new[] { "handle", "save" }, _log.Take(2));
    }

    [Fact]
    public async Task Handler_exception_discards_and_propagates()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sender.Send(new RenameParts([new PartInput(RenamePartsHandler.Explodes)])));

        Assert.Equal(new[] { "handle", "discard" }, _log);
    }

    [Fact]
    public async Task Query_is_validated_and_never_touches_the_unit_of_work()
    {
        var invalid = await Sender.Send(new FindPart(""));
        var valid = await Sender.Send(new FindPart("Vela"));

        Assert.Equal("validation.failed", invalid.Error!.Code);
        Assert.Equal("Vela encontrada", valid.Value);
        Assert.DoesNotContain(_log, entry => entry is "save" or "discard");
        Assert.Equal(2, _log.Count(entry => entry.StartsWith("log: FindPart", StringComparison.Ordinal)));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    public sealed record PartInput(string Name);

    public sealed record RenameParts(IReadOnlyList<PartInput> Parts) : ICommand<int>;

    public sealed record FindPart(string Name) : IQuery<string>;

    private sealed class RenamePartsValidator : AbstractValidator<RenameParts>
    {
        public RenamePartsValidator() =>
            RuleForEach(command => command.Parts).ChildRules(part =>
                part.RuleFor(p => p.Name).NotEmpty().WithMessage("Informe o nome."));
    }

    private sealed class FindPartValidator : AbstractValidator<FindPart>
    {
        public FindPartValidator() => RuleFor(query => query.Name).NotEmpty();
    }

    private sealed class RenamePartsHandler(List<string> log) : ICommandHandler<RenameParts, int>
    {
        public const string Rejected = "rejeitada";
        public const string Explodes = "explode";

        public Task<Result<int>> Handle(RenameParts command, CancellationToken ct)
        {
            log.Add("handle");
            return command.Parts[0].Name switch
            {
                Rejected => Task.FromResult<Result<int>>(new Error("part.rejected", "Peça rejeitada.", ErrorType.DomainRule)),
                Explodes => throw new InvalidOperationException("falha inesperada"),
                _ => Task.FromResult<Result<int>>(command.Parts.Count)
            };
        }
    }

    private sealed class FindPartHandler : IQueryHandler<FindPart, string>
    {
        public Task<Result<string>> Handle(FindPart query, CancellationToken ct) =>
            Task.FromResult<Result<string>>($"{query.Name} encontrada");
    }

    private sealed class RecordingUnitOfWork(List<string> log) : IUnitOfWork
    {
        public Result SaveResult { get; set; } = Result.Success();

        public Task<Result> SaveChangesAsync(CancellationToken ct)
        {
            log.Add("save");
            return Task.FromResult(SaveResult);
        }

        public Task DiscardAsync(CancellationToken ct)
        {
            log.Add("discard");
            return Task.CompletedTask;
        }

        public void AfterCommit(Func<CancellationToken, Task> action) => throw new NotSupportedException();

        public void OnRollback(Func<CancellationToken, Task> action) => throw new NotSupportedException();
    }

    private sealed class RecordingLoggerProvider(List<string> log) : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => log.Add("log: " + formatter(state, exception));

        public void Dispose()
        {
        }
    }
}

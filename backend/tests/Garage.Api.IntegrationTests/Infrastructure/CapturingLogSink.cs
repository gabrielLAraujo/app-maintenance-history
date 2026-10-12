using System.Collections.Concurrent;
using System.Text;
using Serilog.Core;
using Serilog.Events;

namespace Garage.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Sink do Serilog que guarda os eventos em memória. Registrado como <see cref="ILogEventSink"/> no contêiner de um host de
/// teste, o <c>ReadFrom.Services</c> do <c>Program</c> o liga ao logger daquele host.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>
    /// Espera (até 5 s) os eventos satisfazerem <paramref name="condition"/>: o log de fim de requisição é gravado depois de
    /// a resposta sair. Devolve se a condição foi atingida, para o teste afirmar com a própria mensagem.
    /// </summary>
    public async Task<bool> WaitUntilAsync(Func<IReadOnlyList<LogEvent>, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition(Events))
        {
            if (DateTime.UtcNow > deadline)
                return false;

            await Task.Delay(20);
        }

        return true;
    }

    /// <summary>Tudo o que um evento leva para o log: o template, a mensagem renderizada, cada propriedade e a exceção.</summary>
    public static string Describe(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var text = new StringBuilder()
            .AppendLine(logEvent.MessageTemplate.Text)
            .AppendLine(logEvent.RenderMessage());
        foreach (var (name, value) in logEvent.Properties)
            text.Append(name).Append('=').Append(value).AppendLine();

        return text.Append(logEvent.Exception).ToString();
    }

    /// <summary>A propriedade escalar <paramref name="name"/> do evento, como texto (nula se não existir).</summary>
    public static string? Scalar(LogEvent logEvent, string name)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        return logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;
    }
}

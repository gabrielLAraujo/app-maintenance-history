using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Garage.Api.IntegrationTests.Infrastructure;

/// <summary>
/// O TestServer não aplica limite ao tamanho do corpo da requisição (não oferece <see cref="IHttpMaxRequestBodySizeFeature"/>),
/// e o <c>RequestSizeLimit</c> dos endpoints seria ignorado nos testes. Este filtro reproduz o Kestrel: limite padrão de
/// 30.000.000 bytes, trocado pelo roteamento quando o endpoint declara o seu, e <see cref="BadHttpRequestException"/> 413
/// ao ler um corpo maior que o limite — logo na primeira leitura se o Content-Length já passa dele, ou no meio da leitura
/// quando o corpo vem em partes (sem Content-Length).
/// </summary>
internal sealed class KestrelRequestBodyLimit : IStartupFilter
{
    private const long KestrelDefaultMaxRequestBodySize = 30_000_000;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            var body = new LimitedBody(context.Request.Body, context.Request.ContentLength);
            context.Features.Set<IHttpMaxRequestBodySizeFeature>(body);
            context.Request.Body = body;
            await nextMiddleware(context);
        });
        next(app);
    };

    private sealed class LimitedBody(Stream inner, long? contentLength) : Stream, IHttpMaxRequestBodySizeFeature
    {
        private bool _started;
        private long _read;

        // Como no Kestrel, o limite só pode mudar antes de a leitura começar.
        public bool IsReadOnly => _started;

        public long? MaxRequestBodySize { get; set; } = KestrelDefaultMaxRequestBodySize;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Starting();
            return Counted(inner.Read(buffer, offset, count));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Starting();
            return Counted(await inner.ReadAsync(buffer, cancellationToken));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void Starting()
        {
            _started = true;
            ThrowIfOverLimit(contentLength ?? 0);
        }

        private int Counted(int read)
        {
            _read += read;
            ThrowIfOverLimit(_read);
            return read;
        }

        private void ThrowIfOverLimit(long bytes)
        {
            if (MaxRequestBodySize is { } max && bytes > max)
                throw new BadHttpRequestException(
                    $"Request body too large. The max request body size is {max} bytes.", StatusCodes.Status413PayloadTooLarge);
        }
    }
}

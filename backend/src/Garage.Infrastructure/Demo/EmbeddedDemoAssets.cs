using System.Reflection;
using Garage.Application.Abstractions.Ports;

namespace Garage.Infrastructure.Demo;

/// <summary>
/// Lê os arquivos de <c>Demo/Assets</c>, embutidos no assembly (o <c>.csproj</c> dá a cada um o nome lógico
/// <c>Garage.Infrastructure.Demo.Assets.{arquivo}</c>), então vão junto com a API na publicação e na imagem Docker.
/// </summary>
internal sealed class EmbeddedDemoAssets : IDemoAssets
{
    private const string ResourcePrefix = "Garage.Infrastructure.Demo.Assets.";

    public Stream Open(string name) =>
        typeof(EmbeddedDemoAssets).Assembly.GetManifestResourceStream(ResourcePrefix + name)
        ?? throw new FileNotFoundException($"Não há arquivo de exemplo embutido chamado \"{name}\".", name);
}

namespace Garage.Application.Abstractions.Ports;

/// <summary>
/// Os arquivos que acompanham a aplicação e que os dados de exemplo anexam aos registros (fotos e a nota de exemplo).
/// Os nomes são os do catálogo (<c>civic-correia-dentada.jpg</c>), nunca um caminho.
/// </summary>
public interface IDemoAssets
{
    /// <summary>Abre o arquivo para leitura. Quem chama descarta o stream.</summary>
    /// <exception cref="FileNotFoundException">Não há arquivo com esse nome: é um erro do catálogo, não do usuário.</exception>
    Stream Open(string name);
}

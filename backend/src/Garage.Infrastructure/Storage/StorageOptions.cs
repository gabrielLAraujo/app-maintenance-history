namespace Garage.Infrastructure.Storage;

internal sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Pasta raiz dos arquivos dos anexos (em produção, um volume persistente). Obrigatória: vazia, a API não sobe
    /// (validação no início, ver <c>DependencyInjection</c>).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}

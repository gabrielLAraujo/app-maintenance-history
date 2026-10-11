namespace Garage.Infrastructure.Storage;

internal sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Pasta raiz dos arquivos dos anexos (em produção, um volume persistente). Sem ela, gravar ou ler um anexo falha;
    /// o resto da API continua funcionando.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}

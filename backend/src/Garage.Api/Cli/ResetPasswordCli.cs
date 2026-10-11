using Garage.Infrastructure.Identity;

namespace Garage.Api.Cli;

/// <summary>
/// <c>dotnet Garage.Api.dll reset-password &lt;email&gt;</c>: gera uma senha temporária e a imprime (não há e-mail no MVP).
/// </summary>
internal static class ResetPasswordCli
{
    /// <returns>0 quando a senha foi redefinida; 1 quando o usuário não existe.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string email, TextWriter output)
    {
        await using var scope = services.CreateAsyncScope();
        var password = await scope.ServiceProvider.GetRequiredService<PasswordResetService>()
            .ResetAsync(email, CancellationToken.None);

        if (password is null)
        {
            await output.WriteLineAsync("Usuário não encontrado.");
            return 1;
        }

        await output.WriteLineAsync($"Senha temporária: {password}");
        return 0;
    }
}

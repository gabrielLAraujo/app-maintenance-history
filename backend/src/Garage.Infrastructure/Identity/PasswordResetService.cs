using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace Garage.Infrastructure.Identity;

/// <summary>Redefine a senha de um usuário para uma temporária aleatória e destrava a conta (comando administrativo).</summary>
public sealed class PasswordResetService(UserManager<AppUser> userManager)
{
    // 12 bytes viram exatamente 16 caracteres base64url, sem preenchimento.
    private const int TemporaryPasswordBytes = 12;

    /// <returns>A senha temporária, ou <c>null</c> se não existe usuário com esse e-mail.</returns>
    public async Task<string?> ResetAsync(string email, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return null;

        var password = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TemporaryPasswordBytes));
        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        Require(await userManager.ResetPasswordAsync(user, token, password), "redefinir a senha");
        Require(await userManager.SetLockoutEndDateAsync(user, null), "destravar a conta");
        Require(await userManager.ResetAccessFailedCountAsync(user), "zerar as tentativas erradas");

        return password;
    }

    private static void Require(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Não foi possível {action}: {string.Join("; ", result.Errors.Select(error => error.Code))}.");
    }
}

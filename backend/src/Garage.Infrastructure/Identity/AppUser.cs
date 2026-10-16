using Microsoft.AspNetCore.Identity;

namespace Garage.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>Conta de visitante da demo: sem senha, criada por <c>POST /auth/demo</c> e apagada depois de vencer.</summary>
    public bool IsDemo { get; set; }

    /// <summary>Quando a conta demo vence (a sessão expira junto); nulo nas contas reais.</summary>
    public DateTimeOffset? DemoExpiresAt { get; set; }
}

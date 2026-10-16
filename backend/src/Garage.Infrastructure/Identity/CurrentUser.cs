using System.Security.Claims;
using Garage.Application.Abstractions.Ports;
using Microsoft.AspNetCore.Http;

namespace Garage.Infrastructure.Identity;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException("Não há usuário autenticado nesta requisição.");
        }
    }

    public bool IsDemo
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true && user.HasClaim(GarageClaims.Demo, "true");
        }
    }
}

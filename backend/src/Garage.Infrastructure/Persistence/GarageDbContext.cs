using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Garage.Infrastructure.Persistence;

public sealed class GarageDbContext(DbContextOptions<GarageDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IReadDbContext
{
    public IQueryable<T> Query<T>() where T : class => Set<T>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        MapIdentityNamesToSnakeCase(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(GarageDbContext).Assembly);
    }

    // O Identity fixa nomes PascalCase (AspNetUsers, EmailIndex...), que a convenção snake_case não reescreve.
    private static void MapIdentityNamesToSnakeCase(ModelBuilder builder)
    {
        builder.Entity<AppUser>(user =>
        {
            user.ToTable("asp_net_users");
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_asp_net_users_normalized_user_name");
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_asp_net_users_normalized_email");
            // A limpeza busca as contas demo vencidas: índice só sobre elas.
            user.HasIndex(u => u.DemoExpiresAt).HasDatabaseName("ix_asp_net_users_demo_expires_at").HasFilter("is_demo");
        });
        builder.Entity<IdentityRole<Guid>>(role =>
        {
            role.ToTable("asp_net_roles");
            role.HasIndex(r => r.NormalizedName).HasDatabaseName("ix_asp_net_roles_normalized_name");
        });
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims");
    }
}

using Garage.Api.IntegrationTests.Infrastructure;
using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Api.IntegrationTests;

[Collection("api")]
public sealed class UnitOfWorkTests(GarageApiFactory factory)
{
    [Fact]
    public async Task Save_commits_then_runs_after_commit_actions_and_only_logs_their_failures()
    {
        var userName = NewUserName();
        List<string> ran = [];

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            scope.ServiceProvider.GetRequiredService<GarageDbContext>().Users.Add(NewUser(userName));
            unitOfWork.AfterCommit(_ => throw new InvalidOperationException("falha depois do commit"));
            unitOfWork.AfterCommit(_ => Record(ran, "after-commit"));
            unitOfWork.OnRollback(_ => Record(ran, "rollback"));

            var result = await unitOfWork.SaveChangesAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        Assert.Equal(new[] { "after-commit" }, ran);
        Assert.True(await UserExistsAsync(userName));
    }

    [Fact]
    public async Task Discard_runs_rollback_actions_and_forgets_pending_changes()
    {
        var userName = NewUserName();
        List<string> ran = [];

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<GarageDbContext>();
            db.Users.Add(NewUser(userName));
            unitOfWork.AfterCommit(_ => Record(ran, "after-commit"));
            unitOfWork.OnRollback(_ => Record(ran, "rollback"));

            await unitOfWork.DiscardAsync(CancellationToken.None);
            var result = await unitOfWork.SaveChangesAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(db.ChangeTracker.Entries());
        }

        Assert.Equal(new[] { "rollback" }, ran);
        Assert.False(await UserExistsAsync(userName));
    }

    private async Task<bool> UserExistsAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IReadDbContext>()
            .Query<AppUser>()
            .AnyAsync(user => user.UserName == userName);
    }

    private static AppUser NewUser(string userName) => new()
    {
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static string NewUserName() => $"uow-{Guid.NewGuid():N}";

    private static Task Record(List<string> ran, string entry)
    {
        ran.Add(entry);
        return Task.CompletedTask;
    }
}

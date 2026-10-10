using System.Reflection;
using NetArchTest.Rules;

namespace Garage.ArchitectureTests;

public class LayerDependencyTests
{
    [Fact]
    public void Domain_depends_on_nothing() => AssertNoDependency(
        typeof(Garage.Domain.AssemblyMarker).Assembly,
        "Garage.Application", "Garage.Infrastructure", "Garage.Api", "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Application_does_not_depend_on_outer_layers() => AssertNoDependency(
        typeof(Garage.Application.AssemblyMarker).Assembly,
        "Garage.Infrastructure", "Garage.Api", "Npgsql");

    [Fact]
    public void Infrastructure_does_not_depend_on_api() => AssertNoDependency(
        typeof(Garage.Infrastructure.AssemblyMarker).Assembly,
        "Garage.Api");

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}

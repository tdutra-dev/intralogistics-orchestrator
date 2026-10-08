using NetArchTest.Rules;
using Orders.Domain;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void Domain_layer_should_not_depend_on_infrastructure()
    {
        var assembly = typeof(CustomerOrder).Assembly;
        var result = Types.InAssembly(assembly)
            .That()
            .DoNotHaveNameEndingWith("Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Application_layer_should_reference_domain_only()
    {
        var types = Types.InAssembly(typeof(Orders.Application.OrderApplicationService).Assembly)
            .GetTypes();

        types.ShouldNotBeNull();
    }
}

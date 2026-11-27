using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace DevAgent.ArchitectureTests;

/// <summary>
/// Enforces the DDD/Clean Architecture dependency direction: Domain ← Application ← Infrastructure ← Api/Worker.
/// Violations fail CI so the boundaries cannot erode over time.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(DevAgent.Domain.Tasks.AgentTask).Assembly;
    private static readonly Assembly Application = typeof(DevAgent.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(DevAgent.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_is_independent_of_frameworks_and_outer_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevAgent.Application",
                "DevAgent.Infrastructure",
                "DevAgent.Api",
                "DevAgent.Worker",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "Npgsql",
                "Anthropic",
                "System.Net.Http")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_vendor_sdks()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevAgent.Infrastructure",
                "DevAgent.Api",
                "DevAgent.Worker",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql",
                "Anthropic",
                "Serilog",
                "OpenTelemetry")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_hosts()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("DevAgent.Api", "DevAgent.Worker", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Ai_vendor_sdk_is_confined_to_ai_adapter_namespace()
    {
        var result = Types.InAssembly(Infrastructure)
            .That()
            .DoNotResideInNamespace("DevAgent.Infrastructure.Ai")
            .ShouldNot()
            .HaveDependencyOn("Anthropic")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Feature_modules_and_the_api_do_not_depend_on_the_agent_runtime()
    {
        var features = Types.InAssembly(Application)
            .That()
            .ResideInNamespace("DevAgent.Application.Features")
            .ShouldNot()
            .HaveDependencyOn("DevAgent.Application.AgentRuntime")
            .GetResult();
        features.IsSuccessful.ShouldBeTrue(Describe(features));

        var api = Types.InAssembly(Assembly.Load("DevAgent.Api"))
            .ShouldNot()
            .HaveDependencyOn("DevAgent.Application.AgentRuntime")
            .GetResult();
        api.IsSuccessful.ShouldBeTrue(Describe(api));
    }

    [Fact]
    public void Repository_contracts_live_next_to_their_aggregate_in_the_domain()
    {
        var misplaced = Application.GetTypes()
            .Concat(Infrastructure.GetTypes())
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToArray();

        misplaced.ShouldBeEmpty("Repository interfaces belong to their aggregate namespace in DevAgent.Domain.");

        var domainContracts = Domain.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToArray();
        domainContracts.ShouldNotBeEmpty();
        domainContracts
            .Where(t => t.Namespace?.EndsWith(".Repositories", StringComparison.Ordinal) != true)
            .Select(t => t.FullName)
            .ShouldBeEmpty("Each module keeps its repository contracts in a Repositories folder (DevAgent.Domain.<Module>.Repositories).");
    }

    [Fact]
    public void Aggregate_roots_have_no_public_setters()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("DevAgent.Domain", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToArray();

        offenders.ShouldBeEmpty("Domain state may only change through business methods.");
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful ? string.Empty : "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}

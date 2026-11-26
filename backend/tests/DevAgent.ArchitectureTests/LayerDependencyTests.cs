using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace DevAgent.ArchitectureTests;

/// <summary>
/// Giữ đúng hướng phụ thuộc DDD/Clean Architecture: Domain ← Application ← Infrastructure ← Api/Worker.
/// Vi phạm làm CI đỏ, nên ranh giới không bị xói mòn dần theo thời gian.
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
    public void Aggregate_roots_have_no_public_setters()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("DevAgent.Domain", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToArray();

        offenders.ShouldBeEmpty("Trạng thái domain chỉ được thay đổi qua phương thức nghiệp vụ.");
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful ? string.Empty : "Vi phạm: " + string.Join(", ", result.FailingTypeNames ?? []);
}

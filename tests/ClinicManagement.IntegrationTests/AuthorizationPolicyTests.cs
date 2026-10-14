using System.Reflection;
using ClinicManagement.Api.Authorization;
using ClinicManagement.Infrastructure.Seeding;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ClinicManagement.IntegrationTests;

/// <summary>
/// Every endpoint must state who may call it: an endpoint is only anonymous when it says so
/// explicitly, otherwise it must name a registered policy. This is a pure reflection check and
/// does not need a database, so it lives outside the Docker-bound collection.
/// </summary>
public sealed class AuthorizationPolicyReflectionTests
{
    private static readonly string[] RegisteredPolicies =
    [
        Policies.AuthenticatedUser,
        Policies.DoctorOnly,
        Policies.SecretaryOnly,
        Policies.PatientOnly,
        Policies.ClinicalStaff
    ];

    [Fact]
    public void EveryEndpoint_DeclaresItsAuthorizationExplicitly()
    {
        var scanned = 0;
        var offenders = new List<string>();

        foreach (var controller in ControllersInApiAssembly())
        {
            var classAllowsAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var classPolicy = controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy;

            foreach (var action in ActionMethods(controller))
            {
                scanned++;
                var allowsAnonymous = classAllowsAnonymous || action.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
                var policy = action.GetCustomAttribute<AuthorizeAttribute>()?.Policy ?? classPolicy;

                if (!allowsAnonymous && string.IsNullOrWhiteSpace(policy))
                {
                    offenders.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        Assert.True(scanned > 0, "No API endpoints were discovered; the reflection assumptions are stale.");
        Assert.True(
            offenders.Count == 0,
            "These endpoints neither opt out of authorization nor name a policy: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryPolicyNamedByAnEndpoint_IsRegistered()
    {
        var known = new HashSet<string>(RegisteredPolicies, StringComparer.Ordinal);
        var unknown = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controller in ControllersInApiAssembly())
        {
            var policies = controller
                .GetCustomAttributes<AuthorizeAttribute>()
                .Concat(ActionMethods(controller).SelectMany(action => action.GetCustomAttributes<AuthorizeAttribute>()))
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy));

            foreach (var policy in policies)
            {
                if (!known.Contains(policy!))
                {
                    unknown.Add(policy!);
                }
            }
        }

        Assert.Empty(unknown);
    }

    private static IEnumerable<Type> ControllersInApiAssembly() =>
        typeof(Program).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type));

    private static IEnumerable<MethodInfo> ActionMethods(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && !method.Name.StartsWith('<'));
}

/// <summary>
/// Endpoint behaviour checks that exercise the real (containerised) PostgreSQL database.
/// </summary>
[Collection(ClinicApiCollection.Name)]
public sealed class AuthorizationPolicyTests
{
    private readonly ClinicApiFactory _factory;

    public AuthorizationPolicyTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task ProfileEndpoint_RejectsAnonymousCallers()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task ProfileEndpoint_AcceptsASignedInDemoDoctor()
    {
        using var scope = _factory.CreateScope();
        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

        var login = await _factory.CreateClient().LoginAsync(
            DemoAccounts.DoctorEmail,
            seedOptions.DoctorPassword!);

        var client = _factory.CreateClientWithToken(login.Tokens!.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        var body = await response.Content.ReadAsStringAsync();
        var headers = string.Join("; ", response.Headers.Select(header => $"{header.Key}={string.Join("|", header.Value)}"));
        Assert.True(
            response.IsSuccessStatusCode,
            $"status={(int)response.StatusCode} headers=[{headers}] body=[{body}]");
    }
}
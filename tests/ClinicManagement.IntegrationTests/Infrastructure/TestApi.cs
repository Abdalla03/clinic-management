using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicManagement.Application.Auth.Contracts;
using ClinicManagement.Application.Auth.Totp;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests.Infrastructure;

public static class TestApi
{
    public const string Password = "Str0ng!Passw0rd#2026";

    public static HttpClient CreateClientWithToken(this ClinicApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<ApplicationUser> CreateUserAsync(
        this ClinicApiFactory factory,
        string email,
        string password,
        string role,
        string fullName = "Dr. Test Doctor")
    {
        using var scope = factory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await factory.DeleteUserAsync(email);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
        };

        var created = await userManager.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));

        var roleAssigned = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleAssigned.Succeeded, string.Join("; ", roleAssigned.Errors.Select(error => error.Description)));

        return user;
    }

    public static async Task DeleteUserAsync(this ClinicApiFactory factory, string email)
    {
        using var scope = factory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return;
        }

        var refreshTokens = dbContext.RefreshTokens.Where(token => token.ApplicationUserId == user.Id);
        dbContext.RefreshTokens.RemoveRange(refreshTokens);
        await dbContext.SaveChangesAsync();

        await userManager.DeleteAsync(user);
    }

    public static async Task<LoginResult> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Login failed with {(int)response.StatusCode}: {body}");

        return (await response.Content.ReadFromJsonAsync<LoginResult>())!;
    }

    public static async Task<MfaEnrollment> EnrollMfaAsync(this ClinicApiFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.LoginAsync(email, password);

        var authorized = factory.CreateClientWithToken(login.Tokens!.AccessToken);
        var enrollment = await authorized.PostAsync("/api/auth/mfa/enroll", content: null);
        enrollment.EnsureSuccessStatusCode();

        var result = (await enrollment.Content.ReadFromJsonAsync<MfaEnrollment>())!;

        var code = TotpGenerator.ComputeCodeAt(result.Base32Secret, DateTime.UtcNow);
        var confirm = await authorized.PostAsJsonAsync("/api/auth/mfa/confirm", new MfaCodeRequest(code));
        var body = await confirm.Content.ReadAsStringAsync();
        Assert.True(confirm.IsSuccessStatusCode, $"MFA confirmation failed with {(int)confirm.StatusCode}: {body}");

        return result;
    }

    public static async Task<TokenResponse> LoginAndCompleteMfaAsync(this ClinicApiFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var pending = await client.LoginAsync(email, password);

        Assert.True(pending.MfaRequired, "Expected the login to require a second factor.");
        Assert.NotNull(pending.MfaPendingToken);

        // Confirmation and any earlier verification consume the current TOTP step. Clear the replay
        // marker so this helper can exercise the flow repeatedly without waiting for the next window.
        await factory.ResetTotpStepAsync(email);

        var secret = await factory.ReadTotpSecretAsync(email);
        var code = TotpGenerator.ComputeCodeAt(secret, DateTime.UtcNow);

        var response = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerifyRequest(pending.MfaPendingToken!, code));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"MFA verification failed with {(int)response.StatusCode}: {body}");

        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public static async Task ResetTotpStepAsync(this ClinicApiFactory factory, string email)
    {
        using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var user = await dbContext.Users.SingleAsync(candidate => candidate.Email == email);
        user.LastUsedTotpStep = null;
        await dbContext.SaveChangesAsync();
    }

    public static async Task<string> ReadTotpSecretAsync(this ClinicApiFactory factory, string email)
    {
        using var scope = factory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var protector = scope.ServiceProvider.GetRequiredService<ClinicManagement.Application.Abstractions.ISecretProtector>();

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(string.IsNullOrWhiteSpace(user!.TotpSecretCiphertext));

        return protector.Unprotect(
            user.TotpSecretCiphertext!,
            ClinicManagement.Infrastructure.Security.AesGcmSecretProtector.TotpSecretPurpose);
    }

    public static async Task RotateSecurityStampAsync(this ClinicApiFactory factory, string email)
    {
        using var scope = factory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var result = await userManager.UpdateSecurityStampAsync(user!);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
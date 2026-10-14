using System.Net;
using System.Net.Http.Json;
using ClinicManagement.Application.Auth.Contracts;
using ClinicManagement.Domain.Users;
using ClinicManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class RefreshSessionTests
{
    private readonly ClinicApiFactory _factory;

    public RefreshSessionTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task Refresh_AfterMfaCompletion_SucceedsAndRotatesTheToken()
    {
        const string email = "refresh-mfa@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);
        await _factory.EnrollMfaAsync(email, TestApi.Password);

        var client = _factory.CreateClient();

        try
        {
            var session = await _factory.LoginAndCompleteMfaAsync(email, TestApi.Password);

            var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken));
            var rotated = await refresh.Content.ReadFromJsonAsync<TokenResponse>();

            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            Assert.NotNull(rotated);
            Assert.NotEqual(session.RefreshToken, rotated!.RefreshToken);
            Assert.Contains(RoleNames.Doctor, rotated.Roles);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }

    [RequiresDockerFact]
    public async Task Refresh_WithAReusedToken_RevokesEverySessionForThatAccount()
    {
        const string email = "refresh-reuse@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);
        await _factory.EnrollMfaAsync(email, TestApi.Password);

        var client = _factory.CreateClient();

        try
        {
            var session = await _factory.LoginAndCompleteMfaAsync(email, TestApi.Password);

            var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken));
            var rotated = await firstRefresh.Content.ReadFromJsonAsync<TokenResponse>();
            Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

            var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken));
            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

            var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated!.RefreshToken));
            Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }

    [RequiresDockerFact]
    public async Task Refresh_AfterTheSecurityStampChanges_IsRejected()
    {
        const string email = "refresh-stamp@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);
        await _factory.EnrollMfaAsync(email, TestApi.Password);

        var client = _factory.CreateClient();

        try
        {
            var session = await _factory.LoginAndCompleteMfaAsync(email, TestApi.Password);

            await _factory.RotateSecurityStampAsync(email);

            var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken));

            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }

    [RequiresDockerFact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        const string email = "logout@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Patient);

        var client = _factory.CreateClient();

        try
        {
            var login = await client.LoginAsync(email, TestApi.Password);
            Assert.NotNull(login.Tokens);

            var logout = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(login.Tokens!.RefreshToken));
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

            var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.Tokens.RefreshToken));
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }
}
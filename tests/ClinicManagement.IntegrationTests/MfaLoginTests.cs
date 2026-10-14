using System.Net;
using System.Net.Http.Json;
using ClinicManagement.Application.Auth.Contracts;
using ClinicManagement.Application.Auth.Totp;
using ClinicManagement.Domain.Users;
using ClinicManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class MfaLoginTests
{
    private readonly ClinicApiFactory _factory;

    public MfaLoginTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task DoctorWithMfa_MustCompleteTheSecondFactorBeforeReceivingAFullToken()
    {
        const string email = "mfa-doctor@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);

        var client = _factory.CreateClient();

        try
        {
            var beforeEnrollment = await client.LoginAsync(email, TestApi.Password);
            Assert.False(beforeEnrollment.MfaRequired, "The first login happens before MFA is enrolled.");
            Assert.NotNull(beforeEnrollment.Tokens);

            await _factory.EnrollMfaAsync(email, TestApi.Password);
            var secret = await _factory.ReadTotpSecretAsync(email);
            await _factory.ResetTotpStepAsync(email);
            var code = TotpGenerator.ComputeCodeAt(secret, DateTime.UtcNow);

            var pending = await client.LoginAsync(email, TestApi.Password);
            Assert.True(pending.MfaRequired);
            Assert.NotNull(pending.MfaPendingToken);
            Assert.Null(pending.Tokens);

            var wrongCode = await client.PostAsJsonAsync(
                "/api/auth/mfa/verify",
                new MfaVerifyRequest(pending.MfaPendingToken!, "000000"));
            Assert.Equal(HttpStatusCode.Unauthorized, wrongCode.StatusCode);

            var verified = await client.PostAsJsonAsync(
                "/api/auth/mfa/verify",
                new MfaVerifyRequest(pending.MfaPendingToken!, code));
            var tokens = await verified.Content.ReadFromJsonAsync<TokenResponse>();

            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            Assert.NotNull(tokens);
            Assert.Contains(RoleNames.Doctor, tokens!.Roles);

            var me = await _factory.CreateClientWithToken(tokens.AccessToken).GetAsync("/api/auth/me");
            var currentUser = await me.Content.ReadFromJsonAsync<CurrentUserResponse>();

            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Equal(email, currentUser!.Email);
            Assert.True(currentUser.MfaEnabled);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }

    [RequiresDockerFact]
    public async Task MfaPendingToken_CannotAccessProtectedEndpoints()
    {
        const string email = "mfa-pending@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);
        await _factory.EnrollMfaAsync(email, TestApi.Password);

        var client = _factory.CreateClient();

        try
        {
            var pending = await client.LoginAsync(email, TestApi.Password);
            Assert.True(pending.MfaRequired);
            Assert.NotNull(pending.MfaPendingToken);

            var response = await _factory
                .CreateClientWithToken(pending.MfaPendingToken!)
                .GetAsync("/api/auth/me");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }

    [RequiresDockerFact]
    public async Task Login_WithAWrongPassword_IsIndistinguishableFromAnUnknownUser()
    {
        const string email = "wrong-password@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);

        var client = _factory.CreateClient();

        try
        {
            var wrongPassword = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "Wr0ng!Passw0rd#2026"));
            var unknownUser = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("nobody@clinic.test", "Wr0ng!Passw0rd#2026"));

            var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
            var unknownUserBody = await unknownUser.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
            Assert.Equal(wrongPasswordBody, unknownUserBody);
        }
        finally
        {
            await _factory.DeleteUserAsync(email);
        }
    }
}
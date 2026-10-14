using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.Infrastructure.Seeding;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests;

/// <summary>
/// Guards the demo logins the README publishes: they must actually work, they must carry the right
/// profiles, and re-running the seeder must not duplicate or reset anything.
/// </summary>
[Collection(ClinicApiCollection.Name)]
public sealed class DemoSeedTests
{
    private static readonly (string Email, string Password)[] DocumentedCredentials =
    [
        (DemoAccounts.DoctorEmail, "Doctor!Clinic2026"),
        (DemoAccounts.SecretaryEmail, "Secretary!Clinic2026"),
        (DemoAccounts.PatientEmail, "Patient!Clinic2026")
    ];

    private readonly ClinicApiFactory _factory;

    public DemoSeedTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task DocumentedDemoCredentials_SignIn()
    {
        foreach (var (email, password) in DocumentedCredentials)
        {
            var client = _factory.CreateClient();
            var login = await client.LoginAsync(email, password);

            Assert.False(login.MfaRequired, $"{email} must be able to sign in before MFA is enrolled.");
            Assert.NotNull(login.Tokens);
            Assert.Contains(
                login.Tokens!.Roles,
                role => role is RoleNames.Doctor or RoleNames.Secretary or RoleNames.Patient);
        }
    }

    [RequiresDockerFact]
    public async Task SeededStaffAndPatients_AreLinkedToTheirClinicalProfiles()
    {
        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var doctorUser = await context.Users.SingleAsync(user => user.Email == DemoAccounts.DoctorEmail);
        var doctor = await context.Doctors.SingleAsync(entity => entity.ApplicationUserId == doctorUser.Id);
        Assert.Equal(DemoAccounts.DoctorSpecialty, doctor.Specialty);
        Assert.Equal(DemoAccounts.DoctorLicenseNumber, doctor.LicenseNumber);
        Assert.True(doctor.IsAcceptingAppointments);

        var patientUser = await context.Users.SingleAsync(user => user.Email == DemoAccounts.PatientEmail);
        var patient = await context.Patients.SingleAsync(entity => entity.ApplicationUserId == patientUser.Id);
        Assert.Equal(DemoAccounts.PatientMedicalRecordNumber, patient.MedicalRecordNumber);
        Assert.True(patient.IsActive);

        var secretaryUser = await context.Users.SingleAsync(user => user.Email == DemoAccounts.SecretaryEmail);
        Assert.False(await context.Doctors.AnyAsync(entity => entity.ApplicationUserId == secretaryUser.Id));
        Assert.False(await context.Patients.AnyAsync(entity => entity.ApplicationUserId == secretaryUser.Id));
    }

    [RequiresDockerFact]
    public async Task Seeding_IsIdempotent()
    {
        var before = await CountDemoUsersAsync();

        await DatabaseInitializer.InitializeAsync(
            _factory.Services,
            seedDemoAccounts: true);

        var after = await CountDemoUsersAsync();
        Assert.Equal(before, after);

        async Task<int> CountDemoUsersAsync()
        {
            using var scope = _factory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var emails = DocumentedCredentials.Select(credential => credential.Email).ToArray();
            return await context.Users.CountAsync(user => emails.Contains(user.Email));
        }
    }

    [RequiresDockerFact]
    public async Task SeededProfiles_AreNotDuplicatedWhenTheSeederRunsAgain()
    {
        await DatabaseInitializer.InitializeAsync(
            _factory.Services,
            seedDemoAccounts: true);

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        Assert.Equal(1, await context.Users.CountAsync(user => user.Email == DemoAccounts.DoctorEmail));
        Assert.Equal(1, await context.Users.CountAsync(user => user.Email == DemoAccounts.PatientEmail));

        var patientUser = await context.Users.SingleAsync(user => user.Email == DemoAccounts.PatientEmail);
        Assert.Equal(
            1,
            await context.Patients.CountAsync(patient =>
                patient.ApplicationUserId == patientUser.Id ||
                patient.MedicalRecordNumber == DemoAccounts.PatientMedicalRecordNumber));
    }

}

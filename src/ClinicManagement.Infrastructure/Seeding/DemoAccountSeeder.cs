using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicManagement.Infrastructure.Seeding;

/// <summary>
/// Creates the three demo logins the README documents. It is idempotent: running it against a
/// database that already holds the accounts leaves them (and any edits made to them) alone, it only
/// fills in whatever is still missing.
/// </summary>
public sealed class DemoAccountSeeder(
    UserManager<ApplicationUser> userManager,
    ClinicDbContext dbContext,
    IOptions<SeedOptions> seedOptions,
    ILogger<DemoAccountSeeder> logger)
{
    private readonly SeedOptions _seedOptions = seedOptions.Value;

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var seeded = 0;

        seeded += await SeedUserAsync(
            DemoAccounts.DoctorEmail,
            DemoAccounts.DoctorFullName,
            RoleNames.Doctor,
            now,
            cancellationToken);

        seeded += await SeedUserAsync(
            DemoAccounts.SecretaryEmail,
            DemoAccounts.SecretaryFullName,
            RoleNames.Secretary,
            now,
            cancellationToken);

        seeded += await SeedUserAsync(
            DemoAccounts.PatientEmail,
            DemoAccounts.PatientFullName,
            RoleNames.Patient,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return seeded;
    }

    private async Task<int> SeedUserAsync(
        string email,
        string fullName,
        string role,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var password = _seedOptions.ResolvePassword(email);
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No Seed:{Section} password configured for {Email}; the demo login will not be created.",
                SeedOptions.SectionName,
                email);
            return 0;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true,
                CreatedAtUtc = nowUtc
            };

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError(
                    "Could not seed the demo account {Email}: {Errors}",
                    email,
                    string.Join("; ", created.Errors.Select(error => error.Description)));
                return 0;
            }

            logger.LogInformation("Seeded demo account {Email} with role {Role}.", email, role);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                logger.LogError(
                    "Could not assign {Role} to {Email}: {Errors}",
                    role,
                    email,
                    string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        await EnsureProfileAsync(user, role, nowUtc, cancellationToken);
        return 1;
    }

    private async Task EnsureProfileAsync(
        ApplicationUser user,
        string role,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (role == RoleNames.Doctor)
        {
            var hasProfile = await dbContext.Doctors
                .AnyAsync(doctor => doctor.ApplicationUserId == user.Id, cancellationToken);

            if (!hasProfile)
            {
                dbContext.Doctors.Add(Doctor.Create(
                    user.Id,
                    DemoAccounts.DoctorSpecialty,
                    DemoAccounts.DoctorLicenseNumber,
                    nowUtc));
            }

            return;
        }

        if (role != RoleNames.Patient)
        {
            return;
        }

        var hasPatientRecord = await dbContext.Patients
            .AnyAsync(patient =>
                patient.ApplicationUserId == user.Id ||
                patient.MedicalRecordNumber == DemoAccounts.PatientMedicalRecordNumber,
                cancellationToken);

        if (hasPatientRecord)
        {
            return;
        }

        var patient = Patient.Create(
            DemoAccounts.PatientMedicalRecordNumber,
            "Nour",
            "Ibrahim",
            nowUtc,
            user.Id);

        patient.UpdateContactDetails(
            nationalId: "28504170100411",
            phoneNumber: "+201000000001",
            emergencyContactName: "Omar Ibrahim",
            emergencyContactPhone: "+201000000002",
            nowUtc: nowUtc);

        patient.UpdateDemographics(
            dateOfBirth: new DateOnly(1992, 4, 17),
            gender: Gender.Female,
            bloodType: BloodType.OPositive,
            nowUtc: nowUtc);

        patient.UpdateNotes("Demo patient created by the development seeder.", nowUtc);

        dbContext.Patients.Add(patient);
    }
}

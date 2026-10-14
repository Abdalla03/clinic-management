using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicManagement.Application.Appointments;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Patients;
using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class AppointmentEndpointsTests : IDisposable
{
    private static readonly TimeOnly OpensAt = new(9, 0);

    private static readonly TimeOnly ClosesAt = new(17, 0);

    private readonly ClinicApiFactory _factory;
    private readonly DateTime _slotDay;
    private readonly string _reference = $"APT-{Guid.NewGuid().ToString("N")[..8]}";

    private readonly List<string> _emails = [];
    private readonly List<Guid> _doctorIds = [];
    private readonly List<Guid> _patientIds = [];

    private HttpClient _secretary = null!;
    private Guid _doctorId;
    private Guid _patientId;

    public AppointmentEndpointsTests(ClinicApiFactory factory)
    {
        _factory = factory;
        _slotDay = DateTime.UtcNow.Date.AddDays(14);
    }

    [RequiresDockerFact]
    public async Task AnonymousCaller_CannotBookAnAppointment()
    {
        await ArrangeAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, SlotAt(30)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task PatientCannotBookAnAppointment()
    {
        var (patientClient, _) = await ArrangeAsync();

        var response = await patientClient.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, SlotAt(30)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task SecretaryCanBookRescheduleAndCompleteAnAppointment()
    {
        await ArrangeAsync();

        var bookedAt = SlotAt(30);
        var book = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, bookedAt, 30, "Annual check-up"));

        Assert.Equal(HttpStatusCode.Created, book.StatusCode);

        var appointment = await book.Content.ReadFromJsonAsync<AppointmentDetails>();
        Assert.NotNull(appointment);
        Assert.Equal(AppointmentStatus.Booked, appointment!.Status);
        Assert.Equal(bookedAt, appointment.StartsAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(appointment.PatientName));
        Assert.False(string.IsNullOrWhiteSpace(appointment.DoctorName));

        var rescheduledAt = SlotAt(90);
        var reschedule = await _secretary.PutAsJsonAsync(
            $"/api/appointments/{appointment.Id}",
            new RescheduleAppointmentRequest(rescheduledAt, 30));

        Assert.Equal(HttpStatusCode.OK, reschedule.StatusCode);

        var rescheduled = await reschedule.Content.ReadFromJsonAsync<AppointmentDetails>();
        Assert.Equal(rescheduledAt, rescheduled!.StartsAtUtc);
        Assert.Equal(AppointmentStatus.Booked, rescheduled.Status);
        Assert.NotEqual(appointment.Version, rescheduled.Version);

        (await _secretary.PostAsync($"/api/appointments/{appointment.Id}/confirm", null))
            .EnsureSuccessStatusCode();
        (await _secretary.PostAsync($"/api/appointments/{appointment.Id}/complete", null))
            .EnsureSuccessStatusCode();

        var fetched = await _secretary.GetAsync($"/api/appointments/{appointment.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var completed = await fetched.Content.ReadFromJsonAsync<AppointmentDetails>();
        Assert.Equal(AppointmentStatus.Done, completed!.Status);
        Assert.NotNull(completed.CompletedAtUtc);
    }

    [RequiresDockerFact]
    public async Task BookingOutsideTheDoctorsWorkingHours_ReturnsItsOwnErrorCode()
    {
        await ArrangeAsync();

        var response = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, _slotDay.AddHours(19), 30));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AppointmentOutsideWorkingHours, await ProblemCodeAsync(response));
    }

    [RequiresDockerFact]
    public async Task BookingAnOverlappingSlotForTheSameDoctor_ReturnsConflict()
    {
        await ArrangeAsync();

        var bookedAt = SlotAt(30);
        var first = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, bookedAt, 30));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var secondPatientId = await CreatePatientAsync("Second");

        var second = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(secondPatientId, _doctorId, bookedAt.AddMinutes(15), 30));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.AppointmentOverlap, await ProblemCodeAsync(second));
    }

    [RequiresDockerFact]
    public async Task BookingOverlapsThePatientsOtherAppointment_EvenWithAnotherDoctor()
    {
        await ArrangeAsync();

        var bookedAt = SlotAt(30);
        var first = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, bookedAt, 30));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var otherDoctorId = await CreateDoctorAsync("Other");

        var second = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, otherDoctorId, bookedAt, 30));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [RequiresDockerFact]
    public async Task CancellingAFreeAppointment_ReleasesTheSlotForReuse()
    {
        await ArrangeAsync();

        var bookedAt = SlotAt(30);
        var first = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, bookedAt, 30));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var appointment = await first.Content.ReadFromJsonAsync<AppointmentDetails>();

        var cancel = await _secretary.PostAsJsonAsync(
            $"/api/appointments/{appointment!.Id}/cancel",
            new CancelAppointmentRequest("Patient asked to move it"));
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var secondPatientId = await CreatePatientAsync("Reuse");

        var rebook = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(secondPatientId, _doctorId, bookedAt, 30));
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);
    }

    [RequiresDockerFact]
    public async Task CompletingACancelledAppointment_ReturnsInvalidStateTransition()
    {
        await ArrangeAsync();

        var first = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, SlotAt(30), 30));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var appointment = await first.Content.ReadFromJsonAsync<AppointmentDetails>();

        (await _secretary.PostAsJsonAsync(
            $"/api/appointments/{appointment!.Id}/cancel",
            new CancelAppointmentRequest()))
            .EnsureSuccessStatusCode();

        var complete = await _secretary.PostAsync($"/api/appointments/{appointment.Id}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, await ProblemCodeAsync(complete));
    }

    [RequiresDockerFact]
    public async Task PatientSeesOnlyTheirOwnAppointments()
    {
        var (patientClient, _) = await ArrangeAsync();

        var otherPatientId = await CreatePatientAsync("Other");
        var book = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(otherPatientId, _doctorId, SlotAt(30), 30));
        Assert.Equal(HttpStatusCode.Created, book.StatusCode);

        var own = await patientClient.GetAsync("/api/appointments/me");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        var page = await own.Content.ReadFromJsonAsync<PagedResult<AppointmentSummary>>();
        Assert.NotNull(page);
        Assert.All(page!.Items, item => Assert.Equal(_patientId, item.PatientId));
        Assert.DoesNotContain(page.Items, item => item.PatientId == otherPatientId);

        var search = await patientClient.GetAsync("/api/appointments");
        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);

        var detail = await patientClient.GetAsync($"/api/appointments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
    }

    [RequiresDockerFact]
    public async Task AvailabilityExcludesTheSlotsThatAreAlreadyBooked()
    {
        await ArrangeAsync();

        var availability = await _secretary.GetAsync(
            $"/api/doctors/{_doctorId}/availability?fromUtc={_slotDay:O}&toUtc={_slotDay.AddDays(1):O}");

        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);

        var slots = await availability.Content.ReadFromJsonAsync<List<AvailabilitySlot>>();
        Assert.NotNull(slots);
        Assert.NotEmpty(slots!);
        Assert.All(slots, slot =>
        {
            Assert.Equal(DayOfWeekUtc(_slotDay), slot.StartsAtUtc.DayOfWeek);
            Assert.True(slot.StartsAtUtc >= _slotDay);
            Assert.True(slot.EndsAtUtc <= _slotDay.AddDays(1));
            Assert.Equal(30, (slot.EndsAtUtc - slot.StartsAtUtc).TotalMinutes);
        });

        var bookedAt = SlotAt(30);
        var book = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, bookedAt, 30));
        Assert.Equal(HttpStatusCode.Created, book.StatusCode);

        var afterBooking = await _secretary.GetAsync(
            $"/api/doctors/{_doctorId}/availability?fromUtc={_slotDay:O}&toUtc={_slotDay.AddDays(1):O}");
        var updated = await afterBooking.Content.ReadFromJsonAsync<List<AvailabilitySlot>>();
        Assert.NotNull(updated);

        Assert.DoesNotContain(updated!, slot => slot.StartsAtUtc == bookedAt);
        Assert.DoesNotContain(updated, slot => slot.StartsAtUtc < bookedAt.AddMinutes(30) && slot.EndsAtUtc > bookedAt);
        Assert.Equal(slots.Count - 1, updated.Count);
    }

    [RequiresDockerFact]
    public async Task AvailabilityBeyondTheAllowedWindow_ReturnsValidationFailed()
    {
        await ArrangeAsync();

        var response = await _secretary.GetAsync(
            $"/api/doctors/{_doctorId}/availability?fromUtc={_slotDay:O}&toUtc={_slotDay.AddDays(45):O}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task SecretaryCanAddAndDeactivateAWorkingHoursWindow()
    {
        await ArrangeAsync();

        var day = _slotDay.AddDays(1).DayOfWeek;
        var add = await _secretary.PostAsJsonAsync(
            $"/api/doctors/{_doctorId}/schedule",
            new UpsertDoctorScheduleRequest(day, new TimeOnly(13, 0), new TimeOnly(15, 0), 15));

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var window = await add.Content.ReadFromJsonAsync<DoctorScheduleResponse>();
        Assert.True(window!.IsActive);
        Assert.Equal(15, window.SlotMinutes);

        var duplicate = await _secretary.PostAsJsonAsync(
            $"/api/doctors/{_doctorId}/schedule",
            new UpsertDoctorScheduleRequest(day, new TimeOnly(13, 0), new TimeOnly(15, 0), 15));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var deactivate = await _secretary.DeleteAsync($"/api/doctors/{_doctorId}/schedule/{window.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var list = await _secretary.GetAsync($"/api/doctors/{_doctorId}/schedule");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var windows = await list.Content.ReadFromJsonAsync<List<DoctorScheduleResponse>>();
        Assert.DoesNotContain(windows!, item => item.Id == window.Id && item.IsActive);

        var deactivateAgain = await _secretary.DeleteAsync($"/api/doctors/{_doctorId}/schedule/{window.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deactivateAgain.StatusCode);
    }

    [RequiresDockerFact]
    public async Task SchedulingOutsideAWorkingHoursWindow_IsRejectedByBooking()
    {
        await ArrangeAsync();

        var response = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, _slotDay.AddHours(7), 30));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task BookingAnAppointmentIsAuditedAgainstTheCaller()
    {
        await ArrangeAsync();

        var book = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, SlotAt(30), 30));
        Assert.Equal(HttpStatusCode.Created, book.StatusCode);

        var appointment = await book.Content.ReadFromJsonAsync<AppointmentDetails>();

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var entries = await context.AuditLogEntries
            .Where(entry => entry.PatientId == _patientId && entry.EntityName == nameof(Appointment))
            .ToListAsync();

        var created = entries.Single(entry => entry.Action == AuditAction.Created);
        Assert.Equal(appointment!.Id.ToString(), created.EntityId);
        Assert.False(string.IsNullOrWhiteSpace(created.EntryHash));
    }

    [RequiresDockerFact]
    public async Task BookingForAnInactivePatientOrUnknownDoctor_FailsCleanly()
    {
        await ArrangeAsync();

        var unknownDoctor = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, Guid.NewGuid(), SlotAt(30)));
        Assert.Equal(HttpStatusCode.NotFound, unknownDoctor.StatusCode);

        var unknownPatient = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(Guid.NewGuid(), _doctorId, SlotAt(30)));
        Assert.Equal(HttpStatusCode.NotFound, unknownPatient.StatusCode);

        var deactivate = await _secretary.DeleteAsync($"/api/patients/{_patientId}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var inactivePatient = await _secretary.PostAsJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(_patientId, _doctorId, SlotAt(60)));
        Assert.Equal(HttpStatusCode.Conflict, inactivePatient.StatusCode);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        return document!.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }

    private DateTime SlotAt(int minutesIntoTheDay) =>
        DateTime.SpecifyKind(_slotDay.Date, DateTimeKind.Utc).AddMinutes(OpensAt.ToTimeSpan().TotalMinutes + minutesIntoTheDay);

    private static DayOfWeek DayOfWeekUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).DayOfWeek;

    private async Task<(HttpClient PatientClient, Guid PatientId)> ArrangeAsync()
    {
        _secretary = await StaffClientAsync();

        _doctorId = await CreateDoctorAsync("Primary");

        var email = $"appointment-patient-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Patient, "Salma Hassan");
        _emails.Add(email);

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);
            var patient = Patient.Create(NewMedicalRecordNumber(), "Salma", "Hassan", DateTime.UtcNow, user.Id);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            _patientId = patient.Id;
        }

        _patientIds.Add(_patientId);

        var login = await _factory.CreateClient().LoginAsync(email, TestApi.Password);
        Assert.False(login.MfaRequired);
        Assert.NotNull(login.Tokens);

        return (_factory.CreateClientWithToken(login.Tokens!.AccessToken), _patientId);
    }

    private async Task<HttpClient> StaffClientAsync()
    {
        var email = $"appointment-secretary-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Secretary, "Nour Waked");
        _emails.Add(email);

        var login = await _factory.CreateClient().LoginAsync(email, TestApi.Password);
        Assert.False(login.MfaRequired, "The secretary must be able to sign in without MFA.");
        Assert.NotNull(login.Tokens);

        return _factory.CreateClientWithToken(login.Tokens!.AccessToken);
    }

    private async Task<Guid> CreateDoctorAsync(string specialty)
    {
        var email = $"appointment-doctor-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor, $"{specialty} Doctor");
        _emails.Add(email);

        Guid doctorId;
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);

            var doctor = Doctor.Create(user.Id, specialty, $"LIC-{_reference}-{_doctorIds.Count}", DateTime.UtcNow);
            context.Doctors.Add(doctor);

            context.DoctorSchedules.Add(DoctorSchedule.Create(
                doctor.Id,
                _slotDay.DayOfWeek,
                OpensAt,
                ClosesAt,
                30,
                DateTime.UtcNow));

            await context.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        _doctorIds.Add(doctorId);
        return doctorId;
    }

    private async Task<Guid> CreatePatientAsync(string givenName)
    {
        var email = $"appointment-other-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Patient, $"{givenName} Patient");
        _emails.Add(email);

        Guid patientId;
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);

            var patient = Patient.Create(NewMedicalRecordNumber(), givenName, "Hassan", DateTime.UtcNow, user.Id);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            patientId = patient.Id;
        }

        _patientIds.Add(patientId);
        return patientId;
    }

    private string NewMedicalRecordNumber() => $"{_reference}-{_patientIds.Count + 1}";

    public void Dispose()
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

            var appointments = context.Appointments
                .Where(appointment => _doctorIds.Contains(appointment.DoctorId) ||
                                      _patientIds.Contains(appointment.PatientId));
            context.Appointments.RemoveRange(appointments);

            var schedules = context.DoctorSchedules.Where(schedule => _doctorIds.Contains(schedule.DoctorId));
            context.DoctorSchedules.RemoveRange(schedules);

            var doctors = context.Doctors.Where(doctor => _doctorIds.Contains(doctor.Id));
            context.Doctors.RemoveRange(doctors);

            var patients = context.Patients.Where(patient => _patientIds.Contains(patient.Id));
            context.Patients.RemoveRange(patients);

            context.SaveChanges();
        }

        foreach (var email in _emails)
        {
            _factory.DeleteUserAsync(email).GetAwaiter().GetResult();
        }
    }
}

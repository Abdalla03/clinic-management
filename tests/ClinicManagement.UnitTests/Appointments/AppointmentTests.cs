using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Common;
using Xunit;

namespace ClinicManagement.UnitTests.Appointments;

public sealed class AppointmentTests
{
    private static readonly Guid DoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PatientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BookedBy = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = new(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotStart = new(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Book_ClaimsTheSlotKey()
    {
        var appointment = Book();

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.NotNull(appointment.SlotKey);
        Assert.True(appointment.BlocksTheSlot);
    }

    [Fact]
    public void Confirm_KeepsTheSlotClaimed()
    {
        var appointment = Book();

        appointment.Confirm(Now);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.NotNull(appointment.SlotKey);
    }

    [Fact]
    public void Complete_ReleasesTheSlot()
    {
        var appointment = Book();
        appointment.Confirm(Now);

        appointment.Complete(Now);

        Assert.Equal(AppointmentStatus.Done, appointment.Status);
        Assert.Null(appointment.SlotKey);
        Assert.False(appointment.BlocksTheSlot);
    }

    [Fact]
    public void Cancel_ReleasesTheSlot()
    {
        var appointment = Book();

        appointment.Cancel("patient rescheduled", Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Null(appointment.SlotKey);
        Assert.Equal("patient rescheduled", appointment.CancellationReason);
    }

    [Fact]
    public void MarkNoShow_ReleasesTheSlot()
    {
        var appointment = Book();

        appointment.MarkNoShow(Now);

        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.Null(appointment.SlotKey);
    }

    [Fact]
    public void Reschedule_MovesTheSlotClaimToTheNewTime()
    {
        var appointment = Book();
        var originalKey = appointment.SlotKey;
        var newStart = SlotStart.AddHours(2);

        appointment.Reschedule(newStart, newStart.AddMinutes(30), Now);

        Assert.Equal(newStart, appointment.StartsAtUtc);
        Assert.NotEqual(originalKey, appointment.SlotKey);
        Assert.Equal(Appointment.ComputeSlotKey(DoctorId, newStart), appointment.SlotKey);
    }

    [Fact]
    public void Cancel_ThenConfirm_IsRejected()
    {
        var appointment = Book();
        appointment.Cancel(null, Now);

        var exception = Assert.Throws<DomainException>(() => appointment.Confirm(Now));

        Assert.Equal(ErrorCodes.InvalidStateTransition, exception.Code);
    }

    [Fact]
    public void Complete_AfterCompletion_IsRejected()
    {
        var appointment = Book();
        appointment.Complete(Now);

        Assert.Throws<DomainException>(() => appointment.Complete(Now));
    }

    [Fact]
    public void Book_RejectsAnEndThatIsNotAfterTheStart()
    {
        Assert.Throws<DomainException>(() => Appointment.Book(
            PatientId, DoctorId, SlotStart, SlotStart, null, BookedBy, Now));
    }

    [Fact]
    public void Book_RejectsNonUtcTimes()
    {
        var localStart = new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() => Appointment.Book(
            PatientId, DoctorId, localStart, localStart.AddMinutes(30), null, BookedBy, Now));
    }

    [Fact]
    public void ComputeSlotKey_IsDeterministicForTheSameDoctorAndStart()
    {
        Assert.Equal(
            Appointment.ComputeSlotKey(DoctorId, SlotStart),
            Appointment.ComputeSlotKey(DoctorId, SlotStart));
    }

    [Fact]
    public void ComputeSlotKey_DiffersPerDoctorAndPerStartTime()
    {
        var otherDoctor = Guid.Parse("44444444-4444-4444-4444-444444444444");

        Assert.NotEqual(
            Appointment.ComputeSlotKey(DoctorId, SlotStart),
            Appointment.ComputeSlotKey(otherDoctor, SlotStart));

        Assert.NotEqual(
            Appointment.ComputeSlotKey(DoctorId, SlotStart),
            Appointment.ComputeSlotKey(DoctorId, SlotStart.AddMinutes(30)));
    }

    [Fact]
    public void ComputeSlotKey_IsStableAcrossKinds()
    {
        Assert.Equal(
            Appointment.ComputeSlotKey(DoctorId, DateTime.SpecifyKind(SlotStart, DateTimeKind.Utc)),
            Appointment.ComputeSlotKey(DoctorId, DateTime.SpecifyKind(SlotStart, DateTimeKind.Utc)));
    }

    private static Appointment Book() =>
        Appointment.Book(PatientId, DoctorId, SlotStart, SlotStart.AddMinutes(30), "follow up", BookedBy, Now);
}
namespace ClinicManagement.Domain.Users;

public static class RoleNames
{
    public const string Doctor = "Doctor";
    public const string Secretary = "Secretary";
    public const string Patient = "Patient";

    public static IReadOnlyList<string> All { get; } = [Doctor, Secretary, Patient];
}
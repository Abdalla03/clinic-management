namespace ClinicManagement.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? FullName { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
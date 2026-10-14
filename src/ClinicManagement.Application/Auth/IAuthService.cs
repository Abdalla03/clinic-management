using ClinicManagement.Application.Auth.Contracts;
using ClinicManagement.Application.Common;

namespace ClinicManagement.Application.Auth;

public interface IAuthService
{
    Task<Result<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<TokenResponse>> VerifyMfaAsync(MfaVerifyRequest request, CancellationToken cancellationToken);

    Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<Result<MfaEnrollment>> BeginMfaEnrollmentAsync(CancellationToken cancellationToken);

    Task<Result> ConfirmMfaEnrollmentAsync(MfaCodeRequest request, CancellationToken cancellationToken);

    Task<Result> DisableMfaAsync(DisableMfaRequest request, CancellationToken cancellationToken);

    Task<Result<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);
}
using ClinicManagement.Application.Auth.Contracts;
using FluentValidation;

namespace ClinicManagement.Application.Auth.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.EmailOrUserName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}

public sealed class MfaVerifyRequestValidator : AbstractValidator<MfaVerifyRequest>
{
    public MfaVerifyRequestValidator()
    {
        RuleFor(request => request.MfaPendingToken)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(request => request.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("Code must be exactly 6 digits.");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class MfaCodeRequestValidator : AbstractValidator<MfaCodeRequest>
{
    public MfaCodeRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("Code must be exactly 6 digits.");
    }
}

public sealed class DisableMfaRequestValidator : AbstractValidator<DisableMfaRequest>
{
    public DisableMfaRequestValidator()
    {
        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}
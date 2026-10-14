using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Patients;
using FluentValidation;

namespace ClinicManagement.Application.Patients.Validation;

public sealed class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public const string MedicalRecordNumberPattern = "^[A-Za-z0-9][A-Za-z0-9._/-]*$";

    public CreatePatientRequestValidator()
    {
        RuleFor(request => request.MedicalRecordNumber)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(MedicalRecordNumberPattern)
            .WithMessage("medicalRecordNumber may only contain letters, digits and . _ - /");

        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);

        RuleFor(request => request.NationalId).MaximumLength(64).When(request => request.NationalId is not null);
        RuleFor(request => request.PhoneNumber).MaximumLength(64).When(request => request.PhoneNumber is not null);
        RuleFor(request => request.EmergencyContactPhone)
            .MaximumLength(64)
            .When(request => request.EmergencyContactPhone is not null);
        RuleFor(request => request.EmergencyContactName)
            .MaximumLength(200)
            .When(request => request.EmergencyContactName is not null);

        RuleFor(request => request.Address).MaximumLength(512).When(request => request.Address is not null);
        RuleFor(request => request.Notes).MaximumLength(1024).When(request => request.Notes is not null);

        RuleFor(request => request.DateOfBirth)
            .Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("dateOfBirth cannot be in the future.");
    }
}

public sealed class UpdatePatientRequestValidator : AbstractValidator<UpdatePatientRequest>
{
    public UpdatePatientRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);

        RuleFor(request => request.NationalId).MaximumLength(64).When(request => request.NationalId is not null);
        RuleFor(request => request.PhoneNumber).MaximumLength(64).When(request => request.PhoneNumber is not null);
        RuleFor(request => request.EmergencyContactPhone)
            .MaximumLength(64)
            .When(request => request.EmergencyContactPhone is not null);
        RuleFor(request => request.EmergencyContactName)
            .MaximumLength(200)
            .When(request => request.EmergencyContactName is not null);

        RuleFor(request => request.Address).MaximumLength(512).When(request => request.Address is not null);
        RuleFor(request => request.Notes).MaximumLength(1024).When(request => request.Notes is not null);

        RuleFor(request => request.DateOfBirth)
            .Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("dateOfBirth cannot be in the future.");
    }
}

public sealed class PatientSearchQueryValidator : AbstractValidator<PatientSearchQuery>
{
    public PatientSearchQueryValidator()
    {
        RuleFor(query => query.Query)
            .MaximumLength(200)
            .When(query => query.Query is not null);

        RuleFor(query => query.Page)
            .InclusiveBetween(1, 100_000)
            .WithMessage("page must be between 1 and 100000.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage($"pageSize must be between 1 and {PageRequest.MaxPageSize}.");
    }
}

public sealed class AddMedicalHistoryRequestValidator : AbstractValidator<AddMedicalHistoryRequest>
{
    public AddMedicalHistoryRequestValidator()
    {
        RuleFor(request => request.ConditionName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Notes)
            .MaximumLength(2048)
            .When(request => request.Notes is not null);
    }
}

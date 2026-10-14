using FluentValidation;
using FluentValidation.Results;

namespace ClinicManagement.Application.Common.Extensions;

public static class ValidatorExtensions
{
    public static async Task<Result<T>> ValidateAndBindAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(instance, cancellationToken);
        if (validationResult.IsValid)
        {
            return Result<T>.Success(instance);
        }

        return Result<T>.Failure(new Error(
            ClinicManagement.Domain.Common.ErrorCodes.ValidationFailed,
            "One or more validation errors occurred.",
            ToDictionary(validationResult)));
    }

    public static Error ToError(this ValidationResult validationResult)
    {
        return new Error(
            ClinicManagement.Domain.Common.ErrorCodes.ValidationFailed,
            "One or more validation errors occurred.",
            ToDictionary(validationResult));
    }

    private static Dictionary<string, string[]> ToDictionary(ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }
}
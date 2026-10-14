using ClinicManagement.Application.Auth;
using ClinicManagement.Application.Common;
using ClinicManagement.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Infrastructure;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this OperationResult result)
    {
        if (result.Succeeded)
        {
            return Results.NoContent();
        }

        return BuildProblem(result.Error!);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(result.Value);
        }

        return BuildProblem(result.Error!);
    }

    private static IResult BuildProblem(Error error)
    {
        var statusCode = MapStatusCode(error.Code);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = TitleFor(statusCode),
            Detail = error.Message,
            Type = $"https://clinic.local/problems/{error.Code.Replace('.', '/')}"
        };

        problem.Extensions["code"] = error.Code;

        if (error.Details is { Count: > 0 })
        {
            problem.Extensions["errors"] = error.Details;
        }

        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }

    private static int MapStatusCode(string code) => code switch
    {
        AuthErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
        AuthErrorCodes.MfaInvalidCode => StatusCodes.Status401Unauthorized,
        AuthErrorCodes.TokenInvalid => StatusCodes.Status401Unauthorized,
        AuthErrorCodes.RefreshTokenInvalid => StatusCodes.Status401Unauthorized,
        AuthErrorCodes.AccountLocked => StatusCodes.Status423Locked,
        AuthErrorCodes.AccountDisabled => StatusCodes.Status403Forbidden,
        "auth.mfa_mandatory_for_role" => StatusCodes.Status403Forbidden,
        AuthErrorCodes.MfaAlreadyEnabled => StatusCodes.Status409Conflict,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.Conflict => StatusCodes.Status409Conflict,
        ErrorCodes.AppointmentSlotTaken => StatusCodes.Status409Conflict,
        ErrorCodes.AppointmentOverlap => StatusCodes.Status409Conflict,
        ErrorCodes.InvalidStateTransition => StatusCodes.Status409Conflict,
        ErrorCodes.ConcurrencyConflict => StatusCodes.Status409Conflict,
        ErrorCodes.AppointmentOutsideWorkingHours => StatusCodes.Status400BadRequest,
        ErrorCodes.ValidationFailed => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status400BadRequest
    };

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status423Locked => "Locked",
        _ => "Request failed"
    };
}

[ApiController]
[Produces("application/json")]
public abstract class ClinicControllerBase : ControllerBase
{
}
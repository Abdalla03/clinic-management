using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClinicManagement.Api.Infrastructure;

/// <summary>
/// Attaches literal request-body examples to the main write endpoints so Swagger UI can be used
/// as a live API console. Examples reference placeholder GUIDs only; no real credentials appear.
/// </summary>
public sealed class SwaggerRequestExamplesOperationFilter : IOperationFilter
{
    private static readonly IReadOnlyDictionary<string, string> Examples = new Dictionary<string, string>
    {
        ["POST api/auth/login"] =
            """{"emailOrUserName":"doctor@clinic.local","password":"<seed password>"}""",
        ["POST api/auth/mfa/verify"] =
            """{"mfaPendingToken":"<token from login response>","code":"123456"}""",
        ["POST api/auth/refresh"] =
            """{"refreshToken":"<refresh token from login response>"}""",
        ["POST api/auth/logout"] =
            """{"refreshToken":"<refresh token from login response>"}""",
        ["POST api/auth/mfa/confirm"] =
            """{"code":"<6-digit code from your authenticator app>"}""",
        ["POST api/auth/mfa/disable"] =
            """{"password":"<your account password>"}""",

        ["POST api/patients"] =
            """{"medicalRecordNumber":"MRN-000123","firstName":"Salma","lastName":"Hassan","phoneNumber":"+201000000000","dateOfBirth":"1990-05-14","gender":2,"bloodType":3,"address":"12 Nile St, Cairo"}""",
        ["PUT api/patients/{patientId}"] =
            """{"firstName":"Salma","lastName":"Hassan","phoneNumber":"+201000000001"}""",
        ["POST api/patients/{patientId}/medical-history"] =
            """{"conditionName":"Essential hypertension","diagnosedOn":"2024-02-10","notes":"Controlled with lifestyle changes."}""",

        ["POST api/appointments"] =
            """{"patientId":"00000000-0000-0000-0000-000000000000","doctorId":"00000000-0000-0000-0000-000000000000","startsAtUtc":"2026-11-02T09:00:00Z","durationMinutes":30,"reasonForVisit":"Follow-up consultation"}""",
        ["PUT api/appointments/{appointmentId}"] =
            """{"startsAtUtc":"2026-11-03T10:00:00Z","durationMinutes":30}""",
        ["POST api/appointments/{appointmentId}/cancel"] =
            """{"reason":"Patient rescheduled by phone"}""",
        ["POST api/doctors/{doctorId}/schedule"] =
            """{"dayOfWeek":1,"startsAt":"09:00:00","endsAt":"17:00:00","slotMinutes":30}""",

        ["POST api/visits"] =
            """{"patientId":"00000000-0000-0000-0000-000000000000","doctorId":"00000000-0000-0000-0000-000000000000","visitAtUtc":"2026-11-02T09:05:00Z","chiefComplaint":"Headache for three days","notes":"Advised rest and hydration.","diagnosis":"Tension-type headache"}""",
        ["PUT api/visits/{visitId}"] =
            """{"chiefComplaint":"Headache, now resolved","notes":"Patient reports improvement.","diagnosis":"Tension headache, resolving"}""",

        ["POST api/prescriptions"] =
            """{"patientId":"00000000-0000-0000-0000-000000000000","items":[{"drugName":"Amlodipine","dose":"5mg","timesPerDay":1,"durationDays":30,"route":"Oral","instructions":"Take in the morning.","doseTimes":["08:00:00"]}],"notes":"Monitor blood pressure at home."}""",

        ["POST api/invoices"] =
            """{"patientId":"00000000-0000-0000-0000-000000000000","lines":[{"description":"Consultation","unitPrice":150,"quantity":1},{"description":"Lab work","unitPrice":40.5,"quantity":2}],"currency":"EGP"}""",
        ["POST api/invoices/{invoiceId}/payments"] =
            """{"amount":150,"method":1,"reference":"CASH-001"}""",
        ["POST api/invoices/{invoiceId}/void"] =
            """{"reason":"Duplicate invoice"}"""
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.RelativePath is not { } relativePath)
        {
            return;
        }

        var key = $"{context.ApiDescription.HttpMethod} {StripQuery(relativePath)}";

        if (!Examples.TryGetValue(key, out var exampleJson))
        {
            return;
        }

        if (operation.RequestBody?.Content is null)
        {
            return;
        }

        foreach (var mediaType in operation.RequestBody.Content.Values)
        {
            mediaType.Example = JsonNode.Parse(exampleJson);
            return;
        }
    }

    private static string StripQuery(string relativePath)
    {
        var queryIndex = relativePath.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0 ? relativePath : relativePath[..queryIndex];
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClinicManagement.Api.Infrastructure;

/// <summary>
/// Documents the RFC 7807 responses produced by <c>ClinicControllerBase.MapStatusCode</c> so that
/// the generated OpenAPI document (and any client generated from it) knows the error envelope shape.
/// </summary>
public sealed class SwaggerProblemDetailsOperationFilter : IOperationFilter
{
    private static readonly (string StatusCode, string Description)[] ProblemResponses =
    [
        ("400", "Validation failed or the request was malformed."),
        ("401", "Authentication is required or the supplied credentials are invalid."),
        ("403", "The caller is not permitted to perform this operation."),
        ("404", "The requested resource was not found."),
        ("409", "The request conflicts with the current state of the resource.")
    ];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var problemSchema = context.SchemaGenerator.GenerateSchema(
            typeof(ProblemDetails),
            context.SchemaRepository);

        operation.Responses ??= new OpenApiResponses();

        foreach (var (statusCode, description) in ProblemResponses)
        {
            operation.Responses.TryAdd(statusCode, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new OpenApiMediaType { Schema = problemSchema }
                }
            });
        }
    }
}

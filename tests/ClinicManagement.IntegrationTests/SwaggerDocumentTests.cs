using System.Net;
using System.Text.Json.Nodes;
using ClinicManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class SwaggerDocumentTests
{
    private readonly ClinicApiFactory _factory;

    public SwaggerDocumentTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task SwaggerDocumentDeclaresBearerAuthAndCarriesRequestExamples()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        var securitySchemes = document["components"]?["securitySchemes"];
        Assert.NotNull(securitySchemes);
        Assert.Equal("http", securitySchemes["Bearer"]?["type"]?.GetValue<string>());
        Assert.Equal("bearer", securitySchemes["Bearer"]?["scheme"]?.GetValue<string>());

        var securityRequirement = document["security"];
        Assert.NotNull(securityRequirement);
        Assert.NotNull(securityRequirement[0]?["Bearer"]);

        var loginExample = RequestExample(document, "post", "/api/auth/login");
        Assert.NotNull(loginExample);
        Assert.NotNull(loginExample["emailOrUserName"]);

        var paymentExample = RequestExample(document, "post", "/api/invoices/{invoiceId}/payments");
        Assert.NotNull(paymentExample);
        Assert.Equal(1, paymentExample["method"]?.GetValue<int>());

        var prescriptionExample = RequestExample(document, "post", "/api/prescriptions");
        Assert.NotNull(prescriptionExample);
        Assert.Equal("Amlodipine", prescriptionExample["items"]?[0]?["drugName"]?.GetValue<string>());
    }

    private static JsonNode? RequestExample(JsonNode document, string method, string path)
    {
        var operation = document["paths"]?[path]?[method];
        var mediaType = operation?["requestBody"]?["content"]?["application/json"];

        return mediaType?["example"] ?? mediaType?["examples"]?["example"]?["value"];
    }
}

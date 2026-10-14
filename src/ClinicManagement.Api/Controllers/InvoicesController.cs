using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Billing;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api/invoices")]
public sealed class InvoicesController(IBillingService billingService) : ClinicControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<InvoiceSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> SearchAsync(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? visitId = null,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await billingService.SearchInvoicesAsync(
            new InvoiceSearchQuery
            {
                PatientId = patientId,
                VisitId = visitId,
                Status = status,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("mine")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(PagedResult<InvoiceSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> GetOwnAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await billingService.GetOwnInvoicesAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("{invoiceId:guid}")]
    [Authorize(Policy = Policies.AuthenticatedUser)]
    [ProducesResponseType(typeof(InvoiceDetails), StatusCodes.Status200OK)]
    public async Task<IResult> GetAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await billingService.GetInvoiceAsync(invoiceId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(typeof(InvoiceDetails), StatusCodes.Status201Created)]
    public async Task<IResult> CreateAsync(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.CreateInvoiceAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/invoices/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpPost("{invoiceId:guid}/issue")]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(typeof(InvoiceDetails), StatusCodes.Status200OK)]
    public async Task<IResult> IssueAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await billingService.IssueInvoiceAsync(invoiceId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{invoiceId:guid}/payments")]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(typeof(InvoiceDetails), StatusCodes.Status200OK)]
    public async Task<IResult> RecordPaymentAsync(
        Guid invoiceId,
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.RecordPaymentAsync(invoiceId, request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{invoiceId:guid}/void")]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(typeof(InvoiceDetails), StatusCodes.Status200OK)]
    public async Task<IResult> VoidAsync(
        Guid invoiceId,
        [FromBody] VoidInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingService.VoidInvoiceAsync(invoiceId, request, cancellationToken);
        return result.ToHttpResult();
    }
}

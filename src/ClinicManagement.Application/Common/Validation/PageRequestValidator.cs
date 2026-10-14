using ClinicManagement.Application.Common.Paging;
using FluentValidation;

namespace ClinicManagement.Application.Common.Validation;

public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(request => request.Page)
            .InclusiveBetween(1, 100_000)
            .WithMessage("page must be between 1 and 100000.");

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage($"pageSize must be between 1 and {PageRequest.MaxPageSize}.");
    }
}

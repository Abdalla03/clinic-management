using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Common.Validation;
using Xunit;

namespace ClinicManagement.UnitTests.Common;

public sealed class PagingTests
{
    [Fact]
    public void PageRequest_DefaultsToTheFirstTwentyRecords()
    {
        var request = new PageRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(PageRequest.DefaultPageSize, request.PageSize);
        Assert.Equal(0, request.Offset);
    }

    [Theory]
    [InlineData(2, 25, 25)]
    [InlineData(4, 10, 30)]
    [InlineData(1, 100, 0)]
    public void Offset_SkipsWholePages(int page, int pageSize, int expectedOffset)
    {
        var request = new PageRequest { Page = page, PageSize = pageSize };

        Assert.Equal(expectedOffset, request.Offset);
    }

    [Theory]
    [InlineData(10, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(40, 20, 2)]
    [InlineData(41, 20, 3)]
    [InlineData(7, 7, 1)]
    [InlineData(8, 7, 2)]
    [InlineData(0, 20, 0)]
    public void TotalPages_RoundsUpAndTruncatesEmptyResultSets(int totalCount, int pageSize, int expectedPages)
    {
        var request = new PageRequest { Page = 1, PageSize = pageSize };
        var paged = PagedResult<string>.Create([], request, totalCount);

        Assert.Equal(expectedPages, paged.TotalPages);
    }

    [Fact]
    public void PagedResult_FlagsWhetherMorePagesExist()
    {
        var request = new PageRequest { Page = 2, PageSize = 2 };
        var paged = PagedResult<string>.Create(["a", "b"], request, 5);

        Assert.Equal(3, paged.TotalPages);
        Assert.True(paged.HasPreviousPage);
        Assert.True(paged.HasNextPage);
        Assert.Equal(2, paged.Page);
        Assert.Equal(5, paged.TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void PageRequestValidator_RejectsOutOfRangeValues(int page, int pageSize)
    {
        var result = new PageRequestValidator().Validate(new PageRequest { Page = page, PageSize = pageSize });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == (page is < 1 or > 100_000 ? nameof(PageRequest.Page) : nameof(PageRequest.PageSize)));
    }

    [Fact]
    public void PageRequestValidator_AcceptsTheDefaultRequest()
    {
        var result = new PageRequestValidator().Validate(new PageRequest());

        Assert.True(result.IsValid);
    }
}

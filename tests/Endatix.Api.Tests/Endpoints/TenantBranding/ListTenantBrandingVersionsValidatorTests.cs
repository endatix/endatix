using Endatix.Api.Endpoints.TenantBranding;
using Endatix.Core.Infrastructure.Paging;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.TenantBranding;

public class ListTenantBrandingVersionsValidatorTests
{
    private readonly ListTenantBrandingVersionsValidator _validator = new();

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, 1)]
    [InlineData(3, PagedRequestLimits.MAX_PAGE_SIZE)]
    public void Validate_PagingInRange_Passes(int? page, int? pageSize)
    {
        var result = _validator.TestValidate(new ListTenantBrandingVersionsRequest { Page = page, PageSize = pageSize });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageBelowOne_Fails(int page)
    {
        var result = _validator.TestValidate(new ListTenantBrandingVersionsRequest { Page = page });

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PagedRequestLimits.MAX_PAGE_SIZE + 1)]
    public void Validate_PageSizeOutOfRange_Fails(int pageSize)
    {
        var result = _validator.TestValidate(new ListTenantBrandingVersionsRequest { PageSize = pageSize });

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}

using Endatix.Api.Endpoints.FormTemplates;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.FormTemplates;

public class FormTemplatesListValidatorTests
{
    private readonly FormTemplatesListValidator _validator = new();

    [Fact]
    public void Validate_FilterByNullFolderId_PassesValidation()
    {
        var request = new FormTemplatesListRequest
        {
            Filter = ["folderId:null"]
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Validate_FilterByNullFolderIdWithComparisonOperator_FailsValidation()
    {
        var request = new FormTemplatesListRequest
        {
            Filter = ["folderId>null"]
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }
}

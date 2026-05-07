using Endatix.Api.Endpoints.Forms;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class FormsListValidatorTests
{
    private readonly FormsListValidator _validator = new();

    [Fact]
    public void Validate_FilterByNullFolderId_PassesValidation()
    {
        var request = new FormsListRequest
        {
            Filter = ["folderId:null"]
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Validate_FilterByNullFolderIdWithComparisonOperator_FailsValidation()
    {
        var request = new FormsListRequest
        {
            Filter = ["folderId>null"]
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }
}

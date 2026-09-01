using Endatix.Api.Endpoints.Admin.Tenants;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Admin.Tenants;

public sealed class TenantValidatorTests
{
    private const long TENANT_ID = 4242;

    private readonly CreateTenantValidator _createValidator = new();
    private readonly UpdateTenantValidator _updateValidator = new();

    [Theory]
    [InlineData("  x  ")]
    [InlineData("     ")]
    public void Create_NameShorterThanMinimumOnceTrimmed_IsRejected(string name)
    {
        var result = _createValidator.TestValidate(new CreateTenantRequest { Name = name });

        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Fact]
    public void Create_PaddedNameLongEnoughOnceTrimmed_IsAccepted()
    {
        var result = _createValidator.TestValidate(new CreateTenantRequest { Name = "  Acme  " });

        result.ShouldNotHaveValidationErrorFor(request => request.Name);
    }

    [Theory]
    [InlineData("  x  ")]
    [InlineData("     ")]
    public void Update_NameShorterThanMinimumOnceTrimmed_IsRejected(string name)
    {
        var result = _updateValidator.TestValidate(
            new UpdateTenantRequest { TenantId = TENANT_ID, Name = name });

        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Fact]
    public void Update_OmittedName_IsNotValidated()
    {
        var result = _updateValidator.TestValidate(
            new UpdateTenantRequest { TenantId = TENANT_ID, AllowSelfRegistration = true });

        result.ShouldNotHaveValidationErrorFor(request => request.Name);
    }

    [Fact]
    public void Update_NoFieldsProvided_IsRejected()
    {
        var result = _updateValidator.TestValidate(new UpdateTenantRequest { TenantId = TENANT_ID });

        result.IsValid.Should().BeFalse();
    }
}

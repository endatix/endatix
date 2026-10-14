using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.Specifications;

public sealed class FormByIdForTenantTests
{
    private const long TenantId = 1;
    private const long OtherTenantId = 2;
    private const long FormId = 100;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NegativeOrZeroTenantId_ThrowsArgumentException(long tenantId)
    {
        // Arrange
        var create = () => new FormSpecifications.ByIdForTenant(FormId, tenantId);

        // Act & Assert
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Evaluate_FormOfOtherTenant_ReturnsNothing()
    {
        // Arrange
        var spec = new FormSpecifications.ByIdForTenant(FormId, TenantId);

        // Act
        var found = spec.Evaluate([FormOf(OtherTenantId)]);

        // Assert
        found.Should().BeEmpty();
    }

    private static Form FormOf(long tenantId)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Test Form"));
        form.Id = FormId;
        return form;
    }
}

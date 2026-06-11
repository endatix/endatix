using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Features.Submitters;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class SubmitterUniqueViolationExtensionsTests
{
    [Theory]
    [InlineData("IX_Submitters_TenantId_AuthProvider_AppUserId_ExternalSubjectId")]
    [InlineData("IX_Submitters_TenantId_AuthProvider_AppUserId")]
    [InlineData("IX_Submitters_TenantId_AuthProvider_ExternalSubjectId")]
    public void IsSubmitterIdentityViolation_WithSubmitterConstraint_ReturnsTrue(string constraintName)
    {
        UniqueConstraintViolationResult violation = new(true, constraintName, null);

        violation.IsSubmitterIdentityViolation().Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(Submitter.AppUserId))]
    [InlineData(nameof(Submitter.ExternalSubjectId))]
    public void IsSubmitterIdentityViolation_WithColumnNameOnly_ReturnsFalse(string columnName)
    {
        UniqueConstraintViolationResult violation = new(true, null, columnName);

        violation.IsSubmitterIdentityViolation().Should().BeFalse();
    }
}

using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Features.Submitters;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class SubmitterUniqueViolationExtensionsTests
{
    [Theory]
    [InlineData(Submitter.UniqueConstraints.AppUserPerTenant)]
    [InlineData(Submitter.UniqueConstraints.ExternalSubjectPerTenant)]
    public void IsSubmitterIdentityViolation_WithSubmitterConstraint_ReturnsTrue(string constraintName)
    {
        UniqueConstraintViolationResult violation = new(true, constraintName, null);

        violation.IsSubmitterIdentityViolation().Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(Submitter.AppUserId))]
    [InlineData(nameof(Submitter.ExternalSubjectId))]
    public void IsSubmitterIdentityViolation_WithColumnName_ReturnsTrue(string columnName)
    {
        UniqueConstraintViolationResult violation = new(true, null, columnName);

        violation.IsSubmitterIdentityViolation().Should().BeTrue();
    }

    [Fact]
    public void IsSubmitterIdentityViolation_WithUnrelatedConstraint_ReturnsFalse()
    {
        UniqueConstraintViolationResult violation = new(true, "IX_Other_Constraint", null);

        violation.IsSubmitterIdentityViolation().Should().BeFalse();
    }
}

using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.Specifications;

public sealed class SubmissionWithDefinitionAndFormSpecTests
{
    [Fact]
    public void Constructor_Default_DoesNotLoadTheTheme()
    {
        var spec = new SubmissionWithDefinitionAndFormSpec(formId: 1, submissionId: 2);

        spec.IncludeExpressions.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_IncludeTheme_LoadsTheFormTheme()
    {
        var spec = new SubmissionWithDefinitionAndFormSpec(formId: 1, submissionId: 2, includeTheme: true);

        spec.IncludeExpressions.Should().HaveCount(3);
    }
}

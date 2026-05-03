using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Tests.Specifications;

public class SubmissionSpecificationExtensionsTests
{
    [Fact]
    public void WhereFormIdAndFilters_StatusEqualAndNotEqual_AppliesEveryStatusCriterion()
    {
        // Arrange
        var spec = new TestSubmissionSpec(10, ["status:new|read", "status!:approved"]);

        // Act
        var matchesNew = Matches(spec, CreateSubmission(10, SubmissionStatus.New));
        var matchesRead = Matches(spec, CreateSubmission(10, SubmissionStatus.Read));
        var matchesApproved = Matches(spec, CreateSubmission(10, SubmissionStatus.Approved));
        var matchesOtherForm = Matches(spec, CreateSubmission(11, SubmissionStatus.New));

        // Assert
        matchesNew.Should().BeTrue();
        matchesRead.Should().BeTrue();
        matchesApproved.Should().BeFalse();
        matchesOtherForm.Should().BeFalse();
    }

    private static bool Matches(ISpecification<Submission> spec, Submission submission)
    {
        return spec.WhereExpressions.All(where => where.FilterFunc(submission));
    }

    private static Submission CreateSubmission(long formId, SubmissionStatus status)
    {
        var formDefinition = new FormDefinition(SampleData.TENANT_ID);
        typeof(FormDefinition)
            .GetProperty(nameof(FormDefinition.FormId))!
            .SetValue(formDefinition, formId);

        var submission = Submission.Create(
            SampleData.TENANT_ID,
            "{}",
            formId,
            formDefinitionId: 1);

        typeof(Submission)
            .GetProperty(nameof(Submission.FormDefinition))!
            .SetValue(submission, formDefinition);

        submission.UpdateStatus(status);

        return submission;
    }

    private sealed class TestSubmissionSpec : Specification<Submission>
    {
        public TestSubmissionSpec(long formId, IEnumerable<string> filters)
        {
            Query.WhereFormIdAndFilters(formId, new FilterParameters(filters));
        }
    }
}

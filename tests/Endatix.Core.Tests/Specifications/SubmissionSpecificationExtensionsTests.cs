using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Submissions;

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

    [Fact]
    public void SubmissionsByFormIdSpec_CreatedAtRange_AppliesDateFilters()
    {
        // Arrange
        var spec = new SubmissionsByFormIdSpec(
            10,
            new PagingParameters(1, 10),
            new FilterParameters([
                "createdAt>:2026-01-02T00:00:00.000Z",
                "createdAt<2026-01-04T00:00:00.000Z"
            ]));

        // Act
        var matchesBefore = Matches(spec, CreateSubmission(10, SubmissionStatus.New, createdAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        var matchesInside = Matches(spec, CreateSubmission(10, SubmissionStatus.New, createdAt: new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc)));
        var matchesAfter = Matches(spec, CreateSubmission(10, SubmissionStatus.New, createdAt: new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)));

        // Assert
        matchesBefore.Should().BeFalse();
        matchesInside.Should().BeTrue();
        matchesAfter.Should().BeFalse();
    }

    [Fact]
    public void SubmissionsByFormIdCountSpec_CreatedAtRange_AppliesDateFilters()
    {
        // Arrange
        var spec = new SubmissionsByFormIdCountSpec(
            10,
            new FilterParameters([
                "createdAt>:2026-01-02T00:00:00.000Z",
                "createdAt<2026-01-04T00:00:00.000Z"
            ]));

        // Act
        var matchesBefore = Matches(spec, CreateSubmission(10, SubmissionStatus.New, createdAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        var matchesInside = Matches(spec, CreateSubmission(10, SubmissionStatus.New, createdAt: new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc)));
        var matchesAfter = Matches(spec, CreateSubmission(10, SubmissionStatus.New, createdAt: new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)));

        // Assert
        matchesBefore.Should().BeFalse();
        matchesInside.Should().BeTrue();
        matchesAfter.Should().BeFalse();
    }

    [Fact]
    public void SubmissionsByFormIdSpec_CompletedAtRange_AppliesDateFiltersAndExcludesIncompleteSubmissions()
    {
        // Arrange
        var spec = new SubmissionsByFormIdSpec(
            10,
            new PagingParameters(1, 10),
            new FilterParameters([
                "completedAt>:2026-01-02T00:00:00.000Z",
                "completedAt<2026-01-04T00:00:00.000Z"
            ]));

        // Act
        var matchesBefore = Matches(spec, CreateSubmission(10, SubmissionStatus.New, completedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        var matchesInside = Matches(spec, CreateSubmission(10, SubmissionStatus.New, completedAt: new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc)));
        var matchesIncomplete = Matches(spec, CreateSubmission(10, SubmissionStatus.New, isComplete: false));

        // Assert
        matchesBefore.Should().BeFalse();
        matchesInside.Should().BeTrue();
        matchesIncomplete.Should().BeFalse();
    }

    [Fact]
    public void SubmissionsByFormIdCountSpec_CompletedAtRange_AppliesDateFiltersAndExcludesIncompleteSubmissions()
    {
        // Arrange
        var spec = new SubmissionsByFormIdCountSpec(
            10,
            new FilterParameters([
                "completedAt>:2026-01-02T00:00:00.000Z",
                "completedAt<2026-01-04T00:00:00.000Z"
            ]));

        // Act
        var matchesBefore = Matches(spec, CreateSubmission(10, SubmissionStatus.New, completedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        var matchesInside = Matches(spec, CreateSubmission(10, SubmissionStatus.New, completedAt: new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc)));
        var matchesIncomplete = Matches(spec, CreateSubmission(10, SubmissionStatus.New, isComplete: false));

        // Assert
        matchesBefore.Should().BeFalse();
        matchesInside.Should().BeTrue();
        matchesIncomplete.Should().BeFalse();
    }

    private static bool Matches(ISpecification<Submission> spec, Submission submission)
    {
        return spec.WhereExpressions.All(where => where.FilterFunc(submission));
    }

    private static bool Matches(ISpecification<Submission, SubmissionDto> spec, Submission submission)
    {
        return spec.WhereExpressions.All(where => where.FilterFunc(submission));
    }

    private static Submission CreateSubmission(
        long formId,
        SubmissionStatus status,
        DateTime? createdAt = null,
        DateTime? completedAt = null,
        bool isComplete = true)
    {
        var formDefinition = new FormDefinition(SampleData.TENANT_ID);
        typeof(FormDefinition)
            .GetProperty(nameof(FormDefinition.FormId))!
            .SetValue(formDefinition, formId);

        var submission = Submission.Create(
            SampleData.TENANT_ID,
            "{}",
            formId,
            formDefinitionId: 1,
            options: new SubmissionCreateOptions(IsComplete: isComplete));

        typeof(Submission)
            .GetProperty(nameof(Submission.FormDefinition))!
            .SetValue(submission, formDefinition);
        typeof(Submission)
            .GetProperty(nameof(Submission.CreatedAt))!
            .SetValue(submission, createdAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        if (completedAt.HasValue)
        {
            typeof(Submission)
                .GetProperty(nameof(Submission.CompletedAt))!
                .SetValue(submission, completedAt);
        }

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

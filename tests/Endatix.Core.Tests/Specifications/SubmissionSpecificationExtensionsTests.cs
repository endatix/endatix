using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;

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
    public void WhereFormIdAndFilters_CollectionStatus_MatchesCaseInsensitivelyAndIgnoresOverLongCodes()
    {
        // Arrange
        var overLong = new string('x', CollectionStatus.CODE_MAX_LENGTH + 1);
        var equalSpec = new TestSubmissionSpec(10, [$"collectionStatus:IN_PROGRESS|{overLong}"]);
        var notEqualSpec = new TestSubmissionSpec(10, [$"collectionStatus!:{overLong}"]);
        var inProgress = CreateSubmission(10, SubmissionStatus.New, isComplete: false, startSubmission: true);
        var notStarted = CreateSubmission(10, SubmissionStatus.New, isComplete: false);
        var complete = CreateSubmission(10, SubmissionStatus.New, isComplete: true);

        // Act & Assert
        Matches(equalSpec, inProgress).Should().BeTrue();
        Matches(equalSpec, notStarted).Should().BeFalse();
        Matches(equalSpec, complete).Should().BeFalse();
        Matches(notEqualSpec, complete).Should().BeTrue();
    }

    [Fact]
    public void WhereFormIdAndFilters_CollectionStatus_SeparatesNotStartedFromInProgress()
    {
        // Arrange
        var notStartedSpec = new TestSubmissionSpec(10, ["collectionStatus:not_started"]);
        var openSpec = new TestSubmissionSpec(10, ["collectionStatus:not_started|viewed|in_progress"]);
        var excludeNotStartedSpec = new TestSubmissionSpec(10, ["collectionStatus!:not_started"]);
        var notStarted = CreateSubmission(10, SubmissionStatus.New, isComplete: false);
        var inProgress = CreateSubmission(10, SubmissionStatus.New, isComplete: false, startSubmission: true);

        // Act & Assert
        Matches(notStartedSpec, notStarted).Should().BeTrue();
        Matches(notStartedSpec, inProgress).Should().BeFalse();
        Matches(openSpec, notStarted).Should().BeTrue();
        Matches(openSpec, inProgress).Should().BeTrue();
        Matches(excludeNotStartedSpec, notStarted).Should().BeFalse();
        Matches(excludeNotStartedSpec, inProgress).Should().BeTrue();
    }

    [Fact]
    public void SubmissionsByFormIdSpec_CreatedAtRange_AppliesDateFilters()
    {
        // Arrange
        var listFilter = new SubmissionsListFilter(
            Created: new UtcDateTimeRange(
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)));
        var spec = new SubmissionsByFormIdSpec(
            10,
            new PagingParameters(1, 10),
            new FilterParameters([]),
            listFilter);

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
        var listFilter = new SubmissionsListFilter(
            Created: new UtcDateTimeRange(
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)));
        var spec = new SubmissionsByFormIdCountSpec(10, new FilterParameters([]), listFilter);

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
        var listFilter = new SubmissionsListFilter(
            Completed: new UtcDateTimeRange(
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)));
        var spec = new SubmissionsByFormIdSpec(
            10,
            new PagingParameters(1, 10),
            new FilterParameters([]),
            listFilter);

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
        var listFilter = new SubmissionsListFilter(
            Completed: new UtcDateTimeRange(
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)));
        var spec = new SubmissionsByFormIdCountSpec(10, new FilterParameters([]), listFilter);

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
        bool isComplete = true,
        bool startSubmission = false)
    {
        var formDefinition = new FormDefinition(SampleData.TENANT_ID);
        typeof(FormDefinition)
            .GetProperty(nameof(FormDefinition.FormId))!
            .SetValue(formDefinition, formId);

        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: formId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: isComplete,
            StartSubmission: startSubmission));

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

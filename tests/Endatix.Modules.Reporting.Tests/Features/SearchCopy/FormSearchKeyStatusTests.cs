using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

public sealed class FormSearchKeyStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsPending_NoPreparedRow_IsFalse()
    {
        // Arrange
        var keys = FormSearchKeys.Empty.AddUser(new SearchableColumn("q1", SubmissionGridFilterTypes.Text), Now).Value;
        FormSearchKeyStatus status = new(keys, OldestRowRevision: null);

        // Act
        var pending = status.IsPending(keys.Keys[0]);

        // Assert
        pending.Should().BeFalse();
    }

    [Fact]
    public void ToGridSearchKeys_RowOlderThanUserKey_ReportsItPendingWithTheSlots()
    {
        // Arrange
        var keys = FormSearchKeys.Empty.AddUser(new SearchableColumn("q1", SubmissionGridFilterTypes.Text), Now).Value;
        FormSearchKeyStatus status = new(keys, OldestRowRevision: 0);

        // Act
        var searchKeys = status.ToGridSearchKeys(formId: 100);

        // Assert
        searchKeys.Should().BeEquivalentTo(new SubmissionGridSearchKeys(
            100,
            [new SubmissionGridSearchKey("q1", SearchKeySources.User, SubmissionGridFilterTypes.Text, Pending: true)],
            new SubmissionGridSearchSlots(Used: 1, Max: 64, UserUsed: 1, UserMax: 14)));
    }
}

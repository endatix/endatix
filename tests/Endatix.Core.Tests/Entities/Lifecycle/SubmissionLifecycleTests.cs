using Endatix.Core.Entities;
using FluentAssertions;
using Kind = Endatix.Core.Entities.SubmissionLifecycleEventKind;

namespace Endatix.Core.Tests.Entities.Lifecycle;

/// <summary>The lifecycle rules: every cell of the phase table, create with an outcome, and host codes.</summary>
public class SubmissionLifecycleTests
{
    public static TheoryData<CollectionPhase, CollectionPhase, Kind?> PhaseTable => new()
    {
        { CollectionPhase.Pending, CollectionPhase.Pending, Kind.CollectionStatusMoved },
        { CollectionPhase.Pending, CollectionPhase.Active, Kind.Started },
        { CollectionPhase.Pending, CollectionPhase.Completed, Kind.Completed },
        { CollectionPhase.Pending, CollectionPhase.Closed, Kind.Closed },
        { CollectionPhase.Pending, CollectionPhase.Lapsed, Kind.Lapsed },
        { CollectionPhase.Active, CollectionPhase.Pending, null },
        { CollectionPhase.Active, CollectionPhase.Active, Kind.CollectionStatusMoved },
        { CollectionPhase.Active, CollectionPhase.Completed, Kind.Completed },
        { CollectionPhase.Active, CollectionPhase.Closed, Kind.Closed },
        { CollectionPhase.Active, CollectionPhase.Lapsed, Kind.Lapsed },
        { CollectionPhase.Lapsed, CollectionPhase.Pending, null },
        { CollectionPhase.Lapsed, CollectionPhase.Active, Kind.Resumed },
        { CollectionPhase.Lapsed, CollectionPhase.Completed, Kind.Completed },
        { CollectionPhase.Lapsed, CollectionPhase.Closed, Kind.Closed },
        { CollectionPhase.Lapsed, CollectionPhase.Lapsed, Kind.CollectionStatusMoved },
        { CollectionPhase.Completed, CollectionPhase.Pending, null },
        { CollectionPhase.Completed, CollectionPhase.Active, null },
        { CollectionPhase.Completed, CollectionPhase.Completed, null },
        { CollectionPhase.Completed, CollectionPhase.Closed, null },
        { CollectionPhase.Completed, CollectionPhase.Lapsed, null },
        { CollectionPhase.Closed, CollectionPhase.Pending, null },
        { CollectionPhase.Closed, CollectionPhase.Active, null },
        { CollectionPhase.Closed, CollectionPhase.Completed, null },
        { CollectionPhase.Closed, CollectionPhase.Closed, null },
        { CollectionPhase.Closed, CollectionPhase.Lapsed, null },
    };

    [Theory]
    [MemberData(nameof(PhaseTable))]
    public void Decide_EveryPhasePair_ReturnsTheTableCell(CollectionPhase from, CollectionPhase to, Kind? expected)
    {
        // Act
        var kind = SubmissionLifecycle.Decide(from, to);

        // Assert
        kind.Should().Be(expected);
    }

    [Fact]
    public void PhaseTable_CoversEveryPhasePair()
    {
        // Arrange
        var phases = Enum.GetValues<CollectionPhase>();

        // Act
        var pairs = PhaseTable.Select(row => (row.Data.Item1, row.Data.Item2)).ToHashSet();

        // Assert
        pairs.Should().HaveCount(phases.Length * phases.Length);
    }

    [Fact]
    public void Move_ToTheCurrentCode_IsNoStep()
    {
        // Arrange
        var submission = Open();

        // Act
        var step = SubmissionLifecycle.Move(submission, CollectionStatus.InProgress);

        // Assert
        step.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Move_ToAHostClosedCode_IsOneClosedAtTheNextRevision()
    {
        // Arrange
        var submission = Open();
        CollectionStatusCatalog.TryGet(HostCollectionStatuses.PanelOverquota, out var overquota);

        // Act
        var step = SubmissionLifecycle.Move(submission, overquota!);

        // Assert
        step.Status.Code.Should().Be(HostCollectionStatuses.PanelOverquota);
        step.Changes.Should().ContainSingle().Which.Should().Be(
            new LifecycleChange(Kind.Closed, submission.CollectionStatus, submission.Revision + 1));
    }

    [Fact]
    public void Move_ToAHostLapsedCode_IsOneLapsed()
    {
        // Arrange
        var submission = Open();
        CollectionStatusCatalog.TryGet(HostCollectionStatuses.InviteExpired, out var inviteExpired);

        // Act
        var step = SubmissionLifecycle.Move(submission, inviteExpired!);

        // Assert
        step.Changes.Should().ContainSingle().Which.Kind.Should().Be(Kind.Lapsed);
    }

    [Fact]
    public void Move_ToAbandoned_IsOneLapsed()
    {
        // Arrange
        var submission = Open();

        // Act
        var step = SubmissionLifecycle.Move(submission, CollectionStatus.Abandoned);

        // Assert
        step.Changes.Should().ContainSingle().Which.Kind.Should().Be(Kind.Lapsed);
    }

    [Fact]
    public void Move_OutOfComplete_Throws()
    {
        // Arrange
        var submission = Submission.Create(Args(isComplete: true));

        // Act
        var move = () => SubmissionLifecycle.Move(submission, CollectionStatus.InProgress);

        // Assert
        move.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false, false, new[] { Kind.Created })]
    [InlineData(true, false, new[] { Kind.Created, Kind.Completed })]
    [InlineData(false, true, new[] { Kind.Created, Kind.Closed })]
    public void Begin_SavedStatus_RaisesCreatedThenTheOutcome(bool isComplete, bool screenOut, Kind[] expected)
    {
        // Arrange
        var submission = Submission.Create(Args());
        var saved = (isComplete, screenOut) switch
        {
            (true, _) => CollectionStatus.Complete,
            (_, true) => CollectionStatus.ScreenOut,
            _ => CollectionStatus.InProgress,
        };

        // Act
        var step = SubmissionLifecycle.Begin(submission, CollectionStatus.InProgress, saved);

        // Assert
        step.Status.Should().Be(saved);
        step.Changes.Select(change => change.Kind).Should().Equal(expected);
        step.Changes.Select(change => change.Revision).Should()
            .Equal(Enumerable.Range(1, expected.Length).Select(offset => submission.Revision + offset));
    }

    private static SubmissionCreateArgs Args(bool isComplete = false) =>
        new(TenantId: 1, FormId: 100, FormDefinitionId: 200, JsonData: "{}", IsComplete: isComplete, StartSubmission: true);

    private static Submission Open() => Submission.Create(Args());
}

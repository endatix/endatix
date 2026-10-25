using Endatix.Core.Exceptions;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public sealed class AudienceImportTests
{
    private const string Uploading = AudienceImportStatusCodes.Uploading;
    private const string Checking = AudienceImportStatusCodes.Checking;
    private const string Checked = AudienceImportStatusCodes.Checked;
    private const string Writing = AudienceImportStatusCodes.Writing;
    private const string Completed = AudienceImportStatusCodes.Completed;
    private const string Canceled = AudienceImportStatusCodes.Canceled;
    private const string Failed = AudienceImportStatusCodes.Failed;
    private const string Expired = AudienceImportStatusCodes.Expired;

    /// <summary>Every move, from every status a test can reach, with the status it leads to; null where it is refused.</summary>
    public static TheoryData<string, string, string?> Moves() => new()
    {
        { Uploading, "BeginCheck", Checking },
        { Uploading, "Cancel", Canceled },
        { Uploading, "Expire", Expired },
        { Uploading, "Confirm", null },
        { Uploading, "Fail", null },
        { Checking, "FinishCheck", Checked },
        { Checking, "Cancel", Canceled },
        { Checking, "Fail", Failed },
        { Checking, "Expire", null },
        { Checking, "Confirm", null },
        { Checked, "Confirm", Writing },
        { Checked, "Cancel", Canceled },
        { Checked, "Expire", Expired },
        { Checked, "BeginCheck", null },
        { Writing, "Complete", Completed },
        { Writing, "Fail", Failed },
        { Writing, "Cancel", null },
        { Writing, "Expire", null },
        { Completed, "Cancel", null },
        { Completed, "Fail", null },
        { Canceled, "Confirm", null },
        { Canceled, "BeginCheck", null },
    };

    [Theory]
    [MemberData(nameof(Moves))]
    public void Move_FromEachStatus_LeadsWhereTheTableSaysOrIsRefused(string from, string move, string? to)
    {
        // Arrange
        AudienceImport import = ImportIn(from);

        // Act
        Action act = () => Apply(import, move);

        // Assert
        if (to is null)
        {
            act.Should().Throw<DomainRuleException>();
            import.Status.Should().Be(from);
        }
        else
        {
            act();
            import.Status.Should().Be(to);
        }
    }

    [Fact]
    public void Resume_StoppedWrite_WritesAgainAsANewRunFromItsCheckpoint()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);
        import.RecordWritten(new WrittenRows(ThroughRow: 501, Written: 500, Created: 500, Updated: 0, Skipped: 0, Rejected: 0));
        import.Fail("The import stopped.", resumable: true);

        // Act
        import.Resume();

        // Assert
        import.Status.Should().Be(Writing);
        import.WriteRun.Should().Be(1);
        import.WrittenThroughRow.Should().Be(501);
        import.Failure.Should().BeNull();
    }

    [Fact]
    public void Resume_FailedCheck_IsRefused()
    {
        // Arrange — a Check has no checkpoint, so resumable is ignored.
        AudienceImport import = ImportIn(Checking);
        import.Fail("The form was deleted.", resumable: true);

        // Act
        Action act = import.Resume;

        // Assert
        act.Should().Throw<DomainRuleException>();
        import.Status.Should().Be(Failed);
    }

    [Fact]
    public void Expire_StoppedWriteNobodyResumed_EndsItAndItCannotResume()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);
        import.Fail("The import stopped.", resumable: true);

        // Act
        import.Expire();

        // Assert
        import.Status.Should().Be(Expired);
        import.Resumable.Should().BeFalse();
        ((Action)import.Resume).Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void Expire_FailureThatCannotResume_IsRefused()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);
        import.Fail("The form was deleted.");

        // Act
        Action act = import.Expire;

        // Assert
        act.Should().Throw<DomainRuleException>();
        import.Status.Should().Be(Failed);
    }

    [Fact]
    public void RecordWritten_CheckpointThatDoesNotAdvance_IsRefused()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);
        import.RecordWritten(new WrittenRows(ThroughRow: 501, Written: 500, Created: 500, Updated: 0, Skipped: 0, Rejected: 0));

        // Act
        Action act = () => import.RecordWritten(new WrittenRows(501, 500, 500, 0, 0, 0));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        import.CreatedCount.Should().Be(500);
    }

    private static AudienceImport ImportIn(string status)
    {
        AudienceImport import = AudienceImport.Start(new AudienceImportStartArgs(
            1, 2, "people.csv", ["email"], "email", new Dictionary<string, string>(), IsTest: false,
            RowCount: 1_200, ChunkSize: 1_000, PerformedById: null));
        string[] path = status switch
        {
            Uploading => [],
            Checking => ["BeginCheck"],
            Checked => ["BeginCheck", "FinishCheck"],
            Writing => ["BeginCheck", "FinishCheck", "Confirm"],
            Completed => ["BeginCheck", "FinishCheck", "Confirm", "Complete"],
            Canceled => ["Cancel"],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        foreach (string move in path)
        {
            Apply(import, move);
        }

        return import;
    }

    private static void Apply(AudienceImport import, string move)
    {
        switch (move)
        {
            case "BeginCheck": import.BeginCheck(); break;
            case "FinishCheck": import.FinishCheck(); break;
            case "Confirm": import.Confirm(); break;
            case "Complete": import.Complete(); break;
            case "Cancel": import.Cancel(); break;
            case "Expire": import.Expire(); break;
            case "Fail": import.Fail("Stopped."); break;
            default: throw new ArgumentOutOfRangeException(nameof(move));
        }
    }
}

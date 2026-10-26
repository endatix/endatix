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

    [Theory]
    [InlineData(1_200, 1_000, 2)]
    [InlineData(2_000, 1_000, 2)]
    [InlineData(1, 1_000, 1)]
    public void ChunkCount_DeclaredRows_CountsAPartChunkAsOne(int rowCount, int chunkSize, int expected)
    {
        // Arrange
        AudienceImport import = AudienceImport.Start(StartArgs(rowCount, chunkSize));

        // Act
        int chunks = import.ChunkCount;

        // Assert
        chunks.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1_000)]
    [InlineData(1_200, 0)]
    public void Start_NoRowsOrNoChunkSize_IsRefused(int rowCount, int chunkSize)
    {
        // Act
        Action act = () => AudienceImport.Start(StartArgs(rowCount, chunkSize));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_StagedImport_FixesTheFileShapeAndWaitsForChunks()
    {
        // Act
        AudienceImport import = AudienceImport.Start(new AudienceImportStartArgs(
            1, 2, " people.csv ", ["Email", "City"], "Email", new Dictionary<string, string> { ["city"] = "City" },
            IsTest: true, RowCount: 10, ChunkSize: 5, PerformedById: 7));

        // Assert
        import.Status.Should().Be(Uploading);
        import.IsUnfinished.Should().BeTrue();
        import.FileName.Should().Be("people.csv");
        import.Headers.Should().Equal("Email", "City");
        import.PropertyColumns.Should().Equal(new Dictionary<string, string> { ["city"] = "City" });
        import.WrittenThroughRow.Should().Be(AudienceImport.NothingWritten);
    }

    [Fact]
    public void RestartCheck_RetriedCheck_StartsAgainAtRowOneWithTheNewMatchKey()
    {
        // Arrange
        AudienceImport import = ImportIn(Checking);
        import.RestartCheck("email");
        import.RecordChecked(new CheckedRows(Read: 1_000, New: 900, Existing: 90, Rejected: 10));

        // Act
        import.RestartCheck("externalId");

        // Assert
        import.IdentifierKind.Should().Be("externalId");
        import.CheckedRowCount.Should().Be(0);
        import.CheckNewCount.Should().Be(0);
        import.CheckExistingCount.Should().Be(0);
        import.CheckRejectedCount.Should().Be(0);
    }

    [Fact]
    public void RecordChecked_EachChunk_AddsToTheCheckCounts()
    {
        // Arrange
        AudienceImport import = ImportIn(Checking);

        // Act
        import.RecordChecked(new CheckedRows(Read: 1_000, New: 900, Existing: 90, Rejected: 10));
        import.RecordChecked(new CheckedRows(Read: 200, New: 150, Existing: 45, Rejected: 5));

        // Assert
        import.CheckedRowCount.Should().Be(1_200);
        import.CheckNewCount.Should().Be(1_050);
        import.CheckExistingCount.Should().Be(135);
        import.CheckRejectedCount.Should().Be(15);
    }

    [Theory]
    [InlineData(Uploading)]
    [InlineData(Checked)]
    public void RecordChecked_ImportNotChecking_IsRefused(string status)
    {
        // Arrange
        AudienceImport import = ImportIn(status);

        // Act
        Action act = () => import.RecordChecked(new CheckedRows(1, 1, 0, 0));

        // Assert
        act.Should().Throw<DomainRuleException>();
        import.CheckedRowCount.Should().Be(0);
    }

    [Fact]
    public void Confirm_CheckedImport_CarriesTheCheckRejectionsIntoTheWrite()
    {
        // Arrange
        AudienceImport import = ImportIn(Checking);
        import.RecordJob(42);
        import.RecordChecked(new CheckedRows(Read: 1_200, New: 1_100, Existing: 80, Rejected: 20));
        import.FinishCheck();

        // Act
        import.Confirm();

        // Assert
        import.Status.Should().Be(Writing);
        import.RejectedCount.Should().Be(20);
        import.JobId.Should().BeNull();
    }

    [Fact]
    public void RecordWritten_EachBatch_AdvancesTheCheckpointAndAddsItsCounts()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);

        // Act
        import.RecordWritten(new WrittenRows(ThroughRow: 501, Written: 498, Created: 400, Updated: 90, Skipped: 8, Rejected: 2));
        import.RecordWritten(new WrittenRows(ThroughRow: 1_001, Written: 500, Created: 500, Updated: 0, Skipped: 0, Rejected: 0));

        // Assert
        import.WrittenThroughRow.Should().Be(1_001);
        import.WrittenRowCount.Should().Be(998);
        import.CreatedCount.Should().Be(900);
        import.UpdatedCount.Should().Be(90);
        import.SkippedCount.Should().Be(8);
        import.RejectedCount.Should().Be(2);
    }

    [Fact]
    public void RecordWritten_ImportNotWriting_IsRefused()
    {
        // Arrange
        AudienceImport import = ImportIn(Checked);

        // Act
        Action act = () => import.RecordWritten(new WrittenRows(501, 500, 500, 0, 0, 0));

        // Assert
        act.Should().Throw<DomainRuleException>();
        import.WrittenThroughRow.Should().Be(AudienceImport.NothingWritten);
    }

    [Theory]
    [InlineData(Writing, "Complete")]
    [InlineData(Uploading, "Cancel")]
    [InlineData(Checked, "Expire")]
    [InlineData(Checking, "Fail")]
    public void Move_ToAnEnd_StampsWhenItFinishedAndLeavesNothingUnfinished(string from, string move)
    {
        // Arrange
        AudienceImport import = ImportIn(from);

        // Act
        Apply(import, move);

        // Assert
        import.CompletedAt.Should().NotBeNull();
        import.IsUnfinished.Should().BeFalse();
    }

    [Fact]
    public void Fail_LongReason_KeepsTheFirstFiveHundredCharacters()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);

        // Act
        import.Fail(new string('x', AudienceImport.FailureMaxLength + 50));

        // Assert
        import.Failure.Should().HaveLength(AudienceImport.FailureMaxLength);
    }

    [Fact]
    public void Resume_WriteThatFailedForGood_IsRefused()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);
        import.Fail("A column's Property was deleted.", resumable: false);

        // Act
        Action act = import.Resume;

        // Assert
        act.Should().Throw<DomainRuleException>();
        import.Status.Should().Be(Failed);
        import.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Resume_StoppedWrite_ClearsItsJobAndEndForTheNextRun()
    {
        // Arrange
        AudienceImport import = ImportIn(Writing);
        import.RecordJob(42);
        import.Fail("The import stopped.", resumable: true);

        // Act
        import.Resume();

        // Assert
        import.Resumable.Should().BeFalse();
        import.CompletedAt.Should().BeNull();
        import.JobId.Should().BeNull();
        import.IsUnfinished.Should().BeTrue();
    }

    [Fact]
    public void Constructor_OneRequestImport_IsRecordedCompleted()
    {
        // Act
        AudienceImport import = new(new AudienceImportCreateArgs(
            1, 2, " people.csv ", CreatedCount: 3, UpdatedCount: 2, SkippedCount: 1, RejectedCount: 4, PerformedById: 7));

        // Assert
        import.Status.Should().Be(Completed);
        import.IsUnfinished.Should().BeFalse();
        import.FileName.Should().Be("people.csv");
        import.CompletedAt.Should().NotBeNull();
        import.Headers.Should().BeEmpty();
        import.PropertyColumns.Should().BeEmpty();
    }

    private static AudienceImportStartArgs StartArgs(int rowCount, int chunkSize) =>
        new(1, 2, "people.csv", ["email"], "email", new Dictionary<string, string>(), IsTest: false,
            RowCount: rowCount, ChunkSize: chunkSize, PerformedById: null);

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

using System.Text;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public sealed class DraftCheckpointTests
{
    [Fact]
    public void CheckpointIdle_ExactDayWithJournalTail_AppendsOneSystemSnapshot()
    {
        // Arrange
        var draft = NewDraft();
        var edit = DateTime.UnixEpoch;
        draft.Append(Batch(2, """{"a":1}"""), DraftWrite.System(edit), 0);
        var hash = draft.ContentHash;
        var due = edit.AddDays(1);

        // Act
        draft.CheckpointIdleJournalTail(due.AddTicks(-1)).Should().BeNull();
        var checkpoint = draft.CheckpointIdleJournalTail(due);

        // Assert
        checkpoint!.Version.Should().Be(4);
        checkpoint.Payload.Should().Be("""{"a":1}""");
        checkpoint.ContentHash.Should().Be(hash);
        checkpoint.Actor.Kind.Should().Be("system");
        checkpoint.Actor.UserId.Should().BeNull();
        checkpoint.Channel.Should().Be("system");
        checkpoint.Reason.Should().Be("idle");
        checkpoint.CreatedAt.Should().Be(due);
        draft.Version.Should().Be(4);
    }

    [Fact]
    public void Append_RecordWithInvalidUnicode_CannotEnterTheTextBackedJournal()
    {
        // Arrange
        var draft = NewDraft();
        var batch = Batch(1, "{}") with { Records = ["""{"value":"\uD800"}"""] };

        // Act
        var result = draft.Append(batch, DraftWrite.System(DateTime.UtcNow), 0);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.Identifier.Should().Be("records");
        draft.Version.Should().Be(0);
    }

    [Fact]
    public void Append_UnpairedUtf16CodeUnitInCheckpoint_ReturnsValidationBeforeSeeding()
    {
        // Arrange
        var draft = NewDraft();
        var batch = Batch(201, "{}") with { Checkpoints = [new(200, "{\"title\":\"\uD800\"}")] };

        // Act
        var result = draft.Append(batch, DraftWrite.System(DateTime.UtcNow), 0);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        draft.Version.Should().Be(0);
    }

    [Fact]
    public void Append_ValidSurrogatePairAndLiteralEscape_PreservesTheCheckpointDocument()
    {
        // Arrange
        const string json = """{"\uD83D\uDE03":["\\uD800","\uD83D\uDE03"]}""";
        var batch = Batch(201, "{}") with { Checkpoints = [new(200, json)] };
        var draft = NewDraft();

        // Act
        var result = draft.Append(batch, DraftWrite.System(DateTime.UtcNow), 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(row => row.Reason == "periodic").Which.Payload.Should().Be(json);
    }

    [Theory]
    [InlineData("400,200", "{}")]
    [InlineData("200,200", "{}")]
    [InlineData("200,400,401", "{}")]
    [InlineData("199,400", "{}")]
    [InlineData("0,200,400", "{}")]
    [InlineData("200,402", "{}")]
    [InlineData("200,400", "[]")]
    [InlineData("200,400", "{")]
    [InlineData("200,400", """{"a":1,"a":2}""")]
    public void Append_InvalidInteriorCheckpoints_LeavesTheHeadAndVersionUntouched(string offsets, string lastJson)
    {
        // Arrange
        var draft = NewDraft();
        var checkpoints = offsets.Split(',').Select(value => new DraftCheckpoint(int.Parse(value), "{}")).ToArray();
        checkpoints[^1] = checkpoints[^1] with { JsonData = lastJson };
        var batch = Batch(401, """{"title":"changed"}""") with { Checkpoints = checkpoints };

        // Act
        var result = draft.Append(batch, DraftWrite.System(DateTime.UtcNow), 0);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.Identifier.Should().Be("checkpoints");
        draft.Version.Should().Be(0);
        draft.JsonData.Should().Be("{}");
        draft.ModifiedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(500_000, 200)]
    [InlineData(500_001, 100)]
    public void Append_TerminalSurveyByteLength_SelectsTheIntervalForTheEntireBatch(int byteLength, int interval)
    {
        // Arrange
        var json = "{\"s\":\"" + new string('é', 249_996) + new string('x', byteLength - 500_000) + "\"}";
        Encoding.UTF8.GetByteCount(json).Should().Be(byteLength);
        var draft = NewDraft();
        var batch = Batch(2 * interval, json) with { Checkpoints = [new(interval, """{"step":1}""")] };

        // Act
        var rows = draft.Append(batch, DraftWrite.System(DateTime.UtcNow), 0).Value;

        // Assert
        var periodic = rows.Where(row => row.Reason == "periodic").ToArray();
        periodic.Select(row => row.Version).Should().Equal(interval + 2, (2 * interval) + 3);
        periodic.Select(row => row.Payload).Should().Equal("""{"step":1}""", json);
        periodic[0].ContentHash.Should().NotBe(periodic[1].ContentHash);
        periodic[1].ContentHash.Should().Be(draft.ContentHash);
        draft.JsonData.Should().Be(json);
    }

    [Fact]
    public void Append_LargeSurveyFollowingLongSmallTail_SnapshotsTheKnownHeadBeforeTheFirstEmission()
    {
        // Arrange
        var draft = NewDraft();
        var write = DraftWrite.System(DateTime.UtcNow);
        var previous = draft.Append(Batch(150, """{"small":true}"""), write, 0).Value;
        var large = Batch(101, $$"""{"s":"{{new string('x', 500_000)}}"}""") with
        {
            Checkpoints = [new(100, """{"boundary":100}""")]
        };

        // Act
        var rows = draft.Append(large, write, previous[0].Version).Value;

        // Assert
        rows[0].Reason.Should().Be("periodic");
        rows[0].Payload.Should().Be("""{"small":true}""");
        rows[101].Payload.Should().Be("""{"boundary":100}""");
        rows[^1].Kind.Should().Be("journal");
        draft.JsonData.Should().Be(large.JsonData);
    }

    private static FormDraft NewDraft() => FormDraft.For(Form.Create(42, new FormCreateArgs(1, "Survey")), "{}");

    private static DraftJournalBatch Batch(int count, string json) =>
        new(1, "session", Enumerable.Repeat("{}", count).ToArray(), json, new string('f', 64));
}

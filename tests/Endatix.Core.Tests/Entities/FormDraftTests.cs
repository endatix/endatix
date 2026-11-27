using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public sealed class FormDraftTests
{
    [Theory]
    [InlineData(64, 200)]
    [InlineData(500_001, 100)]
    public void Append_CrossingSnapshotInterval_AddsOneSnapshotOfTheBatchHead(int titleLength, int interval)
    {
        // Arrange
        var json = $$"""{"title":"{{new string('a', titleLength)}}"}""";
        var draft = FormDraft.For(Form.Create(42, new FormCreateArgs(1, "Survey")), "{}");
        var write = DraftWrite.System(DateTime.UtcNow);
        var batch = new DraftJournalBatch(1, "session", Enumerable.Repeat("{}", interval - 1).ToArray(), json, new string('f', 64));
        var first = draft.Append(batch, write, 0);
        first.Should().ContainSingle(row => row.Kind == "snapshot" && row.Reason == "seed");
        first.Should().NotContain(row => row.Reason == "periodic");

        // Act
        var next = draft.Append(batch with { Records = ["{}"] }, write, first[0].Version);

        // Assert
        next.Should().HaveCount(2);
        next[0].ContentHash.Should().Be(batch.ContentHash);
        next[1].Reason.Should().Be("periodic");
        next[1].Payload.Should().Be(json);
        next[1].Version.Should().Be(draft.Version);
        next[1].ContentHash.Should().Be(draft.ContentHash);
    }
    [Fact]
    public void Replace_EquivalentJson_LeavesTheDraftUnsaved()
    {
        // Arrange
        var form = Form.Create(42, new FormCreateArgs(1, "Survey"));
        var draft = FormDraft.For(form, """{"title":"Survey","pages":[]}""");

        // Act
        var rows = draft.Replace("""{"pages": [], "title":"Survey"}""", DraftWrite.System(DateTime.UtcNow));

        // Assert
        rows.Should().BeEmpty();
        draft.Version.Should().Be(0);
        draft.JsonData.Should().Be("""{"title":"Survey","pages":[]}""");
    }
}

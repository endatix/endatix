using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class CollectionStatusTests
{
    [Fact]
    public void FromCode_UnknownCode_DoesNotThrow()
    {
        var status = CollectionStatus.FromCode("panel_hold");

        Assert.Equal("panel_hold", status.Code);
        Assert.Equal("panel_hold", status.Name);
    }

    [Fact]
    public void FromCode_BuiltIn_UsesCatalogLabel()
    {
        var status = CollectionStatus.FromCode("SCREEN_OUT");

        Assert.Equal(CollectionStatusCodes.ScreenOut, status.Code);
        Assert.Equal("Screened out", status.Name);
    }

    [Theory]
    [InlineData(true, CollectionStatusCodes.Complete)]
    [InlineData(false, CollectionStatusCodes.InProgress)]
    public void Create_DualWritesCollectionStatus(bool isComplete, string expected)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: isComplete));

        Assert.Equal(isComplete, submission.IsComplete);
        Assert.Equal(expected, submission.CollectionStatus.Code);
    }

    [Fact]
    public void Cancel_DoesNotMarkComplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: false));

        submission.Cancel();

        Assert.False(submission.IsComplete);
        Assert.Equal(CollectionStatusCodes.Cancelled, submission.CollectionStatus.Code);
    }
}

using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public sealed class SubmissionStatusTests
{
    [Fact]
    public void FromCode_WithCodesConst_ReturnsDistinctInstancesWithSameValue()
    {
        SubmissionStatus first = SubmissionStatus.FromCode(SubmissionStatusCodes.New);
        SubmissionStatus second = SubmissionStatus.FromCode(SubmissionStatusCodes.New);

        ReferenceEquals(first, second).Should().BeFalse();
        ReferenceEquals(first, SubmissionStatus.New).Should().BeFalse();
        first.Should().Be(SubmissionStatus.New);
        second.Should().Be(first);
        first.Code.Should().Be(SubmissionStatusCodes.New);
    }

    [Fact]
    public void FromCode_ReturnsDistinctInstancesPerCall()
    {
        SubmissionStatus first = SubmissionStatus.FromCode(SubmissionStatusCodes.Approved);
        SubmissionStatus second = SubmissionStatus.FromCode(SubmissionStatusCodes.Approved);

        ReferenceEquals(first, second).Should().BeFalse();
        ReferenceEquals(first, SubmissionStatus.Approved).Should().BeFalse();
        first.Should().Be(SubmissionStatus.Approved);
        first.Code.Should().Be(SubmissionStatusCodes.Approved);
    }

    [Fact]
    public void CreateInstance_ClonesCatalogWithoutSharingReference()
    {
        SubmissionStatus instance = SubmissionStatus.Read.CreateInstance();

        ReferenceEquals(instance, SubmissionStatus.Read).Should().BeFalse();
        instance.Should().Be(SubmissionStatus.Read);
        instance.Code.Should().Be(SubmissionStatusCodes.Read);
    }

    [Fact]
    public void Submission_Create_AssignsDistinctStatusInstances()
    {
        Submission first = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: 1,
            FormDefinitionId: 2,
            JsonData: "{}"));
        Submission second = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: 1,
            FormDefinitionId: 2,
            JsonData: "{}"));

        ReferenceEquals(first.Status, second.Status).Should().BeFalse();
        first.Status.Should().Be(SubmissionStatus.New);
        second.Status.Should().Be(SubmissionStatus.New);
        first.Status.Code.Should().Be(SubmissionStatusCodes.New);
    }

    [Theory]
    [InlineData("Processed")]
    [InlineData("Assigned")]
    [InlineData("processed")]
    [InlineData("assigned")]
    public void FromCode_WithCustomStatus_Throws(string code)
    {
        Action act = () => SubmissionStatus.FromCode(code);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("code")
            .WithMessage($"*Invalid status code: {code}*");
    }

    [Theory]
    [InlineData("Processed")]
    [InlineData("Assigned")]
    public void UpdateStatus_WithCustomStatus_CannotBeApplied(string code)
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: 1,
            FormDefinitionId: 2,
            JsonData: "{}"));

        Action act = () => submission.UpdateStatus(SubmissionStatus.FromCode(code));

        act.Should().Throw<ArgumentException>().WithParameterName("code");
        submission.Status.Should().Be(SubmissionStatus.New);
    }
}

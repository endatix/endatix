using Endatix.Core.Entities;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.Entities;

public class SubmissionConstructorTests
{
    [Fact]
    public void Constructor_NullJsonData_ThrowsArgumentNullException()
    {
        // Arrange  
        var formId = 123;
        var formDefinitionId = 456;
        string? nullJsonData = null;

        // Act
        var action = () => new Submission(SampleData.TENANT_ID, nullJsonData!, formId, formDefinitionId);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithMessage(GetErrorMessage("args.JsonData", Null));
    }

    [Fact]
    public void Constructor_EmptyJsonData_ThrowsArgumentException()
    {
        // Arrange
        var emptyJsonData = string.Empty;
        var formId = 123;
        var formDefinitionId = 456;

        // Act
        var action = () => new Submission(SampleData.TENANT_ID, emptyJsonData, formId, formDefinitionId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage("args.JsonData", Empty));
    }

    [Fact]
    public void Constructor_NegativeFormDefinitionId_ThrowsArgumentException()
    {
        // Arrange
        const long invalidFormDefinitionId = -1;
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 123
        };
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;

        // Act
        var action = () => new Submission(SampleData.TENANT_ID, jsonData, form.Id, invalidFormDefinitionId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage("args.FormDefinitionId", ZeroOrNegative));
    }

    [Fact]
    public void Create_null_args_throws()
    {
        var action = () => Submission.Create(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidInput_SetsPropertiesCorrectly()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 123
        };
        var jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 123
        };
        form.AddFormDefinition(formDefinition);

        // Act
        var submission = new Submission(SampleData.TENANT_ID, jsonData, form.Id, formDefinition.Id, isComplete: false, currentPage: 2, metadata: "Test");

        // Assert
        submission.Should().NotBeNull();
        submission.JsonData.Should().Be(jsonData);
        submission.FormDefinitionId.Should().Be(formDefinition.Id);
        submission.IsComplete.Should().BeFalse();
        submission.CurrentPage.Should().Be(2);
        submission.Metadata.Should().Be("Test");
        submission.HasStarted.Should().BeFalse();
        submission.StartedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_CompleteSubmission_SetsCompletedAt()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 123
        };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 456
        };
        form.AddFormDefinition(formDefinition);
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;

        // Act
        var submission = new Submission(SampleData.TENANT_ID, jsonData, form.Id, formDefinition.Id, isComplete: true);

        // Assert
        submission.Should().NotBeNull();
        submission.IsComplete.Should().BeTrue();
        submission.CompletedAt.Should().NotBeNull();
        submission.HasStarted.Should().BeTrue();
        submission.StartedAt.Should().Be(submission.CompletedAt);
    }

    [Fact]
    public void Create_WhenStartSubmission_StampsStartedAtForIncomplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            SampleData.TENANT_ID,
            FormId: 123,
            FormDefinitionId: 456,
            JsonData: SampleData.SUBMISSION_JSON_DATA_1,
            IsComplete: false,
            StartSubmission: true));

        submission.HasStarted.Should().BeTrue();
        submission.StartedAt.Should().NotBeNull();
        submission.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WhenStartSubmissionFalse_LeavesStartedAtNullForIncomplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            SampleData.TENANT_ID,
            FormId: 123,
            FormDefinitionId: 456,
            JsonData: SampleData.SUBMISSION_JSON_DATA_1,
            IsComplete: false,
            StartSubmission: false));

        submission.HasStarted.Should().BeFalse();
        submission.StartedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WhenSingleSubmissionGateApplies_SetsRestrictionKey()
    {
        // Act
        var submission = Submission.Create(new SubmissionCreateArgs(
            SampleData.TENANT_ID,
            FormId: 123,
            FormDefinitionId: 456,
            JsonData: SampleData.SUBMISSION_JSON_DATA_1,
            SubmitterId: 789,
            EnforceSingleSubmissionGate: true));

        // Assert
        submission.RestrictionKey.Should().Be("SingleSubmission:Form:123:Submitter:789");
        submission.SubmitterId.Should().Be(789);
        submission.SubmittedBy.Should().Be("789");
    }

    [Theory]
    [InlineData(false, false, 789L)]
    [InlineData(true, true, 789L)]
    [InlineData(true, false, null)]
    public void Create_WhenSingleSubmissionGateDoesNotApply_DoesNotSetRestrictionKey(
        bool enforceSingleSubmissionGate,
        bool isTestSubmission,
        long? submitterId)
    {
        // Act
        var submission = Submission.Create(new SubmissionCreateArgs(
            SampleData.TENANT_ID,
            FormId: 123,
            FormDefinitionId: 456,
            JsonData: SampleData.SUBMISSION_JSON_DATA_1,
            SubmitterId: submitterId,
            IsTestSubmission: isTestSubmission,
            EnforceSingleSubmissionGate: enforceSingleSubmissionGate));

        // Assert
        submission.RestrictionKey.Should().BeNull();
    }

    [Fact]
    public void Constructor_WhenSubmittedByIsNumeric_ForwardsSubmitterAndRestrictionGate()
    {
        var submission = new Submission(
            SampleData.TENANT_ID,
            SampleData.SUBMISSION_JSON_DATA_1,
            formId: 123,
            formDefinitionId: 456,
            submittedBy: "789",
            enforceSingleSubmissionGate: true);

        submission.SubmitterId.Should().Be(789);
        submission.SubmittedBy.Should().Be("789");
        submission.RestrictionKey.Should().Be("SingleSubmission:Form:123:Submitter:789");
    }

    [Fact]
    public void Constructor_WhenSubmittedByIsOpaqueString_PreservesSubmittedByMirror()
    {
        var submission = new Submission(
            SampleData.TENANT_ID,
            SampleData.SUBMISSION_JSON_DATA_1,
            formId: 123,
            formDefinitionId: 456,
            submittedBy: "user123");

        submission.SubmitterId.Should().BeNull();
        submission.SubmitterDisplayId.Should().Be("user123");
        submission.SubmittedBy.Should().Be("user123");
        submission.RestrictionKey.Should().BeNull();
    }
}

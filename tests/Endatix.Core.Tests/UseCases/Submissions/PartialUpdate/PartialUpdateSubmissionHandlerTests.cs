using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdate;

public class PartialUpdateSubmissionHandlerTests
{
    private readonly IRepository<Submission> _repository;
    private readonly IRepository<SubmissionVersion> _versions;
    private readonly PartialUpdateSubmissionHandler _handler;
    private readonly IMediator _mediator;

    public PartialUpdateSubmissionHandlerTests()
    {
        _repository = Substitute.For<IRepository<Submission>>();
        _mediator = Substitute.For<IMediator>();
        _versions = Substitute.For<IRepository<SubmissionVersion>>();
        _handler = new PartialUpdateSubmissionHandler(_repository, _versions, _mediator);
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new PartialUpdateSubmissionCommand(1, 1, null, null, null, null);
        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form submission not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesSubmission()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, "{ }", 2, 3) { Id = 1 };
        var requestMetadata = "{\"key\":\"value\"}";
        var request = new PartialUpdateSubmissionCommand(
            1, 2, true, 1, "{ \"updated\": true }", requestMetadata
        );

        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsComplete.Should().Be(request.IsComplete!.Value);
        result.Value.CurrentPage.Should().Be(request.CurrentPage!.Value);
        result.Value.Metadata.Should().Contain("\"key\":\"value\"");
        result.Value.FormId.Should().Be(request.FormId);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_JsonDataChanged_CreatesSubmissionVersion()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, "{ }", 2, 3) { Id = 1 };
        var request = new PartialUpdateSubmissionCommand(1, 2, true, 1, "{ \"updated\": true }", "metadata");

        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _versions.Received(1).AddAsync(
            Arg.Is<SubmissionVersion>(v => v.SubmissionId == submission.Id && v.JsonData == "{ }"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_JsonDataUnchanged_DoesNotCreateSubmissionVersion()
    {
        // Arrange
        var originalJson = "{ }";
        var submission = new Submission(SampleData.TENANT_ID, originalJson, 2, 3) { Id = 1 };
        var request = new PartialUpdateSubmissionCommand(1, 2, null, null, null, null);

        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _versions.DidNotReceive().AddAsync(Arg.Any<SubmissionVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoPageInCommand_KeepsExistingPage()
    {
        // Arrange
        var existingPage = 7;
        var submission = new Submission(
            tenantId: SampleData.TENANT_ID,
            jsonData: "{ }",
            formId: 2,
            formDefinitionId: 3,
            isComplete: false,
            currentPage: existingPage,
            metadata: null);

        var request = new PartialUpdateSubmissionCommand(
            SubmissionId: 1,
            FormId: 2,
            IsComplete: null,
            CurrentPage: null,
            JsonData: null,
            Metadata: null
        );
        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.CurrentPage.Should().Be(existingPage);
    }

    [Fact]
    public async Task Handle_NoPageInCommandAndSubmission_SetsDefaultPageFallbackValue()
    {
        // Arrange
        const int DEFAULT_CURRENT_PAGE = 0;
        var submission = new Submission(
            tenantId: SampleData.TENANT_ID,
            jsonData: "{ }",
            formId: 2,
            formDefinitionId: 3,
            isComplete: false,
            metadata: null);
        var request = new PartialUpdateSubmissionCommand(
            1, 2, null, null, null, null
        );
        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.CurrentPage.Should().Be(DEFAULT_CURRENT_PAGE);
    }

    [Fact]
    public async Task Handle_MetadataProvided_MergesWithExistingMetadata()
    {
        // Arrange
        var existingMetadata = "{\"test\":\"1\",\"existing\":\"value\"}";
        var submission = new Submission(
            tenantId: SampleData.TENANT_ID,
            jsonData: "{ }",
            formId: 2,
            formDefinitionId: 3,
            isComplete: false,
            metadata: existingMetadata);

        var newMetadata = "{\"language\":\"en\",\"test\":\"updated\"}";
        var request = new PartialUpdateSubmissionCommand(
            SubmissionId: 1,
            FormId: 2,
            IsComplete: null,
            CurrentPage: null,
            JsonData: null,
            Metadata: newMetadata
        );

        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();

        // Verify that metadata was merged: new fields added, updated fields overwritten, existing fields preserved
        result.Value.Metadata.Should().Contain("\"language\":\"en\"");
        result.Value.Metadata.Should().Contain("\"test\":\"updated\""); // overwritten
        result.Value.Metadata.Should().Contain("\"existing\":\"value\""); // preserved
    }

    [Fact]
    public async Task Handle_MetadataProvidedButExistingIsNull_SetsNewMetadata()
    {
        // Arrange
        var submission = new Submission(
            tenantId: SampleData.TENANT_ID,
            jsonData: "{ }",
            formId: 2,
            formDefinitionId: 3,
            isComplete: false,
            metadata: null);

        var newMetadata = "{\"language\":\"en\"}";
        var request = new PartialUpdateSubmissionCommand(
            SubmissionId: 1,
            FormId: 2,
            IsComplete: null,
            CurrentPage: null,
            JsonData: null,
            Metadata: newMetadata
        );

        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Metadata.Should().Contain("\"language\":\"en\"");
    }

    [Fact]
    public async Task Handle_NoMetadataInRequest_KeepsExistingMetadata()
    {
        // Arrange
        var existingMetadata = "{\"test\":\"1\"}";
        var submission = new Submission(
            tenantId: SampleData.TENANT_ID,
            jsonData: "{ }",
            formId: 2,
            formDefinitionId: 3,
            isComplete: false,
            metadata: existingMetadata);

        var request = new PartialUpdateSubmissionCommand(
            SubmissionId: 1,
            FormId: 2,
            IsComplete: null,
            CurrentPage: null,
            JsonData: null,
            Metadata: null
        );

        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Metadata.Should().Be(existingMetadata);
    }
}

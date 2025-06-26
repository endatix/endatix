using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.Create;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.Create;

public class CreateSubmissionHandlerTests
{
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly IFormsRepository _formsRepository;
    private readonly ISubmissionTokenService _submissionTokenService;
    private readonly IMediator _mediator;
    private readonly CreateSubmissionHandler _handler;

    public CreateSubmissionHandlerTests()
    {
        _submissionsRepository = Substitute.For<IRepository<Submission>>();
        _formsRepository = Substitute.For<IFormsRepository>();
        _submissionTokenService = Substitute.For<ISubmissionTokenService>();
        _mediator = Substitute.For<IMediator>();
        _handler = new CreateSubmissionHandler(
            _submissionsRepository,
            _formsRepository,
            _submissionTokenService,
            _mediator);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new CreateSubmissionCommand(1, "{ }", null, null, null);
        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Form?)null);
        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found. Cannot create a submission");
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesSubmission()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(formDefinition);
        form.SetActiveFormDefinition(formDefinition);
        var request = new CreateSubmissionCommand(
            FormId: 1,
            JsonData: "{ \"field\": \"value\" }",
            IsComplete: true,
            CurrentPage: 3,
            Metadata: "{ \"meta\": \"data\" }"
        );

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();

        await _submissionsRepository.Received(1).AddAsync(
            Arg.Is<Submission>(s =>
                s.FormId == request.FormId &&
                s.FormDefinitionId == formDefinition.Id &&
                s.JsonData == request.JsonData &&
                s.IsComplete == request.IsComplete &&
                s.CurrentPage == request.CurrentPage &&
                s.Metadata == request.Metadata
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_ValidRequest_GeneratesAndSetsToken()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(formDefinition);
        form.SetActiveFormDefinition(formDefinition);
        var request = new CreateSubmissionCommand(1, "{ }", null, null, null);

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();

        await _submissionTokenService.Received(1).ObtainTokenAsync(
            Arg.Any<long>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_ValidRequestAndSubmissionIsCompleted_PublishesSubmissionCreatedEvent()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(formDefinition);
        form.SetActiveFormDefinition(formDefinition);
        var request = new CreateSubmissionCommand(1, "{ }", null, null, true);

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);

        await _mediator.Received(1).Publish(
            Arg.Is<SubmissionCompletedEvent>(e =>
                e.Submission.FormId == request.FormId &&
                e.Submission.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_NullJsonData_SetsDefaultJsonData()
    {
        // Arrange
        const string DEFAULT_JSON_DATA = "{}";
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(formDefinition);
        form.SetActiveFormDefinition(formDefinition);
        var request = new CreateSubmissionCommand(
            FormId: 1,
            JsonData: null,
            IsComplete: false,
            CurrentPage: 5,
            Metadata: null
        );
        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.JsonData.Should().Be(DEFAULT_JSON_DATA);
    }

    [Fact]
    public async Task Handle_NullCurrentPage_SetsDefaultCurrentPage()
    {
        // Arrange
        const int DEFAULT_CURRENT_PAGE = 0;
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(formDefinition);
        form.SetActiveFormDefinition(formDefinition);
        var request = new CreateSubmissionCommand(
            FormId: 1,
            JsonData: "{ }",
            IsComplete: false,
            CurrentPage: null,
            Metadata: null
        );
        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.CurrentPage.Should().Be(DEFAULT_CURRENT_PAGE);
    }
}

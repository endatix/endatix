using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.GetById;

namespace Endatix.Core.Tests.UseCases.Submissions.GetById;

public class GetSubmissionByIdHandlerTests
{
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly GetByIdHandler _handler;

    public GetSubmissionByIdHandlerTests()
    {
        _submissionsRepository = Substitute.For<IRepository<Submission>>();
        _handler = new GetByIdHandler(_submissionsRepository);
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetByIdQuery(1, 1);
        _submissionsRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSubmission()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 1L;
        var submissionId = 1L;

        var form = new Form(SampleData.TENANT_ID, "Test Form", isEnabled: true);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = new Submission(SampleData.TENANT_ID, "{ }", formId, formDefinitionId) { Id = submissionId };

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        var request = new GetByIdQuery(formId, submissionId);

        _submissionsRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(submission);
    }
}

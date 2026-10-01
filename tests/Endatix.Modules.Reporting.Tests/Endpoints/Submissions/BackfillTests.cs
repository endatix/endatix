using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Endpoints.Submissions;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Tests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Submissions;

public sealed class BackfillTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly Backfill _endpoint;

    public BackfillTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<Backfill>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFormNotFound_ReturnsProblemDetails()
    {
        BackfillSubmissionsRequest request = new() { FormId = 100 };
        Result<SubmissionBackfillResult> result = Result.NotFound("Form not found.");

        _mediator.Send(Arg.Any<BackfillSubmissionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsBackfillSummary()
    {
        const long formId = 100;
        BackfillSubmissionsRequest request = new()
        {
            FormId = formId,
            BatchSize = 25,
            AfterSubmissionId = 50,
            Force = true,
        };

        SubmissionBackfillResult backfillResult = new(
            formId,
            Scanned: 25,
            Processed: 20,
            Skipped: 3,
            Failed: 2,
            HasMore: true,
            NextAfterSubmissionId: 75,
            FailedSubmissionIds: [71, 72]);

        _mediator.Send(Arg.Any<BackfillSubmissionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(backfillResult));

        var response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        var ok = response.Result as Ok<BackfillSubmissionsResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.FormId.Should().Be(formId);
        ok.Value.Processed.Should().Be(20);
        ok.Value.HasMore.Should().BeTrue();
        ok.Value.NextAfterSubmissionId.Should().Be(75);

        await _mediator.Received(1).Send(
            Arg.Is<BackfillSubmissionsCommand>(command =>
                command.FormId == formId &&
                command.TenantId == SampleData.TENANT_ID &&
                command.BatchSize == 25 &&
                command.AfterSubmissionId == 50 &&
                command.Force),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompletionScopeOmitted_SendsCompleted()
    {
        // Arrange
        BackfillSubmissionsRequest request = new() { FormId = 100 };
        _mediator.Send(Arg.Any<BackfillSubmissionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(EmptyBatch(100)));

        // Act
        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<BackfillSubmissionsCommand>(command =>
                command.Completion == SubmissionBackfillCompletion.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithIncompleteScope_SendsIncomplete()
    {
        // Arrange
        BackfillSubmissionsRequest request = new()
        {
            FormId = 100,
            CompletionScope = SubmissionBackfillCompletion.Incomplete,
        };
        _mediator.Send(Arg.Any<BackfillSubmissionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(EmptyBatch(100)));

        // Act
        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<BackfillSubmissionsCommand>(command =>
                command.Completion == SubmissionBackfillCompletion.Incomplete),
            Arg.Any<CancellationToken>());
    }

    private static SubmissionBackfillResult EmptyBatch(long formId) =>
        new(formId, Scanned: 0, Processed: 0, Skipped: 0, Failed: 0, HasMore: false, NextAfterSubmissionId: null, FailedSubmissionIds: []);
}

using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Endpoints.Jobs;
using Endatix.Modules.Jobs.Features.GetJob;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Jobs.Tests.Endpoints.Jobs;

public sealed class GetJobEndpointTests
{
    private const long JobId = 987654321098765432;

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly GetJob _endpoint;

    public GetJobEndpointTests() => _endpoint = Factory.Create<GetJob>(_mediator);

    private static JobDto Job(
        JobStatus status = JobStatus.Processing,
        JobResultDto? result = null,
        string? errorMessage = null) =>
        new(JobId, "SubmissionExport", status, 45, "Processing 4,500 of 10,000 rows", result, errorMessage);

    [Fact]
    public async Task ExecuteAsync_ExistingJob_ReturnsItsState()
    {
        // Arrange
        var job = Job();
        _mediator.Send(Arg.Any<GetJobQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(job));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new GetJobRequest { JobId = JobId }, TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result as Ok<JobResponse>;
        ok.Should().NotBeNull();
        ok!.Value!.Id.Should().Be(JobId);
        ok.Value.Status.Should().Be(JobStatus.Processing);
        ok.Value.ProgressPercentage.Should().Be(45);
        ok.Value.StatusMessage.Should().Be("Processing 4,500 of 10,000 rows");

        await _mediator.Received(1).Send(
            Arg.Is<GetJobQuery>(query => query.JobId == JobId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UnfinishedJob_OmitsDownloadMetadata()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetJobQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Job()));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new GetJobRequest { JobId = JobId }, TestContext.Current.CancellationToken);

        // Assert — a caller that polls must not be offered a download before there is one.
        var ok = response.Result as Ok<JobResponse>;
        ok!.Value!.Result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_CompletedJob_CarriesDownloadMetadata()
    {
        // Arrange
        var result = new JobResultDto("/api/jobs/987654321098765432/download", "submissions-100.csv", "text/csv");
        _mediator.Send(Arg.Any<GetJobQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Job(JobStatus.Completed, result)));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new GetJobRequest { JobId = JobId }, TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result as Ok<JobResponse>;
        ok!.Value!.Result.Should().NotBeNull();
        ok.Value.Result!.FileName.Should().Be("submissions-100.csv");
        ok.Value.Result.ContentType.Should().Be("text/csv");
    }

    [Fact]
    public async Task ExecuteAsync_FailedJob_SurfacesTheError()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetJobQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Job(JobStatus.Failed, errorMessage: "Form schema is not compiled")));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new GetJobRequest { JobId = JobId }, TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result as Ok<JobResponse>;
        ok!.Value!.Status.Should().Be(JobStatus.Failed);
        ok.Value.ErrorMessage.Should().Be("Form schema is not compiled");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownJob_Returns404()
    {
        // Arrange — also the answer for a job owned by another tenant, which the query filter makes
        // indistinguishable from one that does not exist.
        _mediator.Send(Arg.Any<GetJobQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Job with ID 1 was not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new GetJobRequest { JobId = 1 }, TestContext.Current.CancellationToken);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validator_NonPositiveJobId_Fails(long jobId)
    {
        // Arrange
        var validator = new GetJobValidator();

        // Act
        var result = await validator.ValidateAsync(
            new GetJobRequest { JobId = jobId }, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}

using System.Text.Json.Nodes;
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
        JsonNode? result = null,
        string? errorMessage = null) =>
        new(JobId, "SubmissionExport", status, 45, "Processing 4,500 of 10,000 rows", result, errorMessage);

    private async Task<JobResponse> ExecuteAsync(long jobId = JobId)
    {
        var response = await _endpoint.ExecuteAsync(
            new GetJobRequest { JobId = jobId }, TestContext.Current.CancellationToken);
        var ok = response.Result as Ok<JobResponse>;
        ok.Should().NotBeNull();
        return ok!.Value!;
    }

    private void Returns(Result<JobDto> result) =>
        _mediator.Send(Arg.Any<GetJobQuery>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task ExecuteAsync_ExistingJob_ReturnsItsState()
    {
        // Arrange
        Returns(Result.Success(Job()));

        // Act
        var job = await ExecuteAsync();

        // Assert
        job.Id.Should().Be(JobId);
        job.Status.Should().Be(JobStatus.Processing);
        job.ProgressPercentage.Should().Be(45);
        job.StatusMessage.Should().Be("Processing 4,500 of 10,000 rows");

        await _mediator.Received(1).Send(
            Arg.Is<GetJobQuery>(query => query.JobId == JobId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UnfinishedJob_ReportsNoResult()
    {
        // Arrange
        Returns(Result.Success(Job()));

        // Act
        var job = await ExecuteAsync();

        // Assert — a caller that polls must not be offered an output before there is one.
        job.Result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_CompletedExport_CarriesTheFileItProduced()
    {
        // Arrange
        var result = JsonNode.Parse(@"{""fileName"":""submissions-100.csv"",""contentType"":""text/csv""}");
        Returns(Result.Success(Job(JobStatus.Completed, result)));

        // Act
        var job = await ExecuteAsync();

        // Assert
        job.Result.Should().NotBeNull();
        job.Result!["fileName"]!.GetValue<string>().Should().Be("submissions-100.csv");
    }

    [Fact]
    public async Task ExecuteAsync_CompletedJobWithNoFileOutput_CarriesItsResultAnyway()
    {
        // Arrange — a webhook delivery reports a response code, not a file. Both are legitimate
        // outputs, which is why this endpoint imposes no shape on them.
        var result = JsonNode.Parse(@"{""statusCode"":200,""attempt"":1}");
        Returns(Result.Success(Job(JobStatus.Completed, result)));

        // Act
        var job = await ExecuteAsync();

        // Assert
        job.Result!["statusCode"]!.GetValue<int>().Should().Be(200);
    }

    [Fact]
    public async Task ExecuteAsync_FailedJob_SurfacesTheError()
    {
        // Arrange
        Returns(Result.Success(Job(JobStatus.Failed, errorMessage: "Form schema is not compiled")));

        // Act
        var job = await ExecuteAsync();

        // Assert
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("Form schema is not compiled");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownJob_Returns404()
    {
        // Arrange — also the answer for a job owned by another tenant, which the query filter makes
        // indistinguishable from one that does not exist.
        Returns(Result.NotFound("Job with ID 1 was not found."));

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

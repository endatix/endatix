using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Core.UseCases.Submissions;
using Endatix.Infrastructure.Features.Submitters;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class ListByFormIdTests
{
    private readonly IMediator _mediator;
    private readonly ListByFormId _endpoint;

    public ListByFormIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ListByFormId>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsProblemDetails()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId };
        var result = Result.NotFound("Form not found");

        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithSubmissions()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId, Page = 1, PageSize = 10 };
        var submissions = new List<SubmissionDto>
        {
            new(3, false, "{}", 1, 2, 5, DateTime.UtcNow, null, DateTime.UtcNow.AddMinutes(-5), null, "{ }", "new", null, null, null, null, false),
            new(4, false, "{}", 1, 2, 6, DateTime.UtcNow, null, DateTime.UtcNow.AddMinutes(-10), null, "{ }", "new", "7", 7, "7", null, true),
        };
        var result = Result.Success(new Paged<SubmissionDto>(
            page: 1,
            pageSize: 10,
            totalRecords: 25,
            totalPages: 3,
            items: submissions));

        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<Paged<SubmissionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Items.Count().Should().Be(2);
        okResult.Value.TotalRecords.Should().Be(25);
        okResult.Value.TotalPages.Should().Be(3);
        okResult.Value.Page.Should().Be(1);
        okResult.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new ListByFormIdRequest
        {
            FormId = 123,
            Page = 2,
            PageSize = 20,
            Filter = ["isComplete:true", "isTestSubmission:true"],
            SortBy = SubmissionListSortBy.CompletedAt,
            SortDir = SortDirection.Asc,
            CreatedFrom = "2026-01-01",
            CreatedTo = "2026-01-31",
            StartedFrom = "2026-01-02",
            CompletedTo = "2026-01-30",
        };
        var result = Result.Success(Paged<SubmissionDto>.Empty(20));
        
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListByFormIdQuery>(query =>
                query.FormId == request.FormId &&
                query.Page == request.Page &&
                query.PageSize == request.PageSize &&
                query.FilterExpressions!.SequenceEqual(request.Filter!) &&
                query.SortBy == SubmissionListSortBy.CompletedAt &&
                query.SortDescending == false &&
                query.CreatedFrom == new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
                query.CreatedTo == new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) &&
                query.StartedFrom == new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) &&
                query.CompletedTo == new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc)
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_SortDirWithoutSortBy_AppliesDirectionToDefaultSortField()
    {
        // Arrange
        var request = new ListByFormIdRequest { FormId = 123, SortDir = SortDirection.Asc };
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Paged<SubmissionDto>.Empty(10)));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListByFormIdQuery>(query =>
                query.SortBy == SubmissionListSortBy.CreatedAt &&
                query.SortDescending == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoSortParams_LeavesSortByNullForLegacyDefaultOrdering()
    {
        // Arrange
        var request = new ListByFormIdRequest { FormId = 123 };
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Paged<SubmissionDto>.Empty(10)));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListByFormIdQuery>(query => query.SortBy == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_IsTestSubmissionFilter_IsAccepted()
    {
        // Arrange
        var validator = new ListByFormIdValidator(Options.Create(new SubmitterOptions()));
        var request = new ListByFormIdRequest
        {
            FormId = 1,
            Filter = ["isTestSubmission:true"]
        };

        // Act
        var validationResult = validator.Validate(request);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_CreatedAtFilter_IsRejected()
    {
        // Arrange
        var validator = new ListByFormIdValidator(Options.Create(new SubmitterOptions()));
        var request = new ListByFormIdRequest
        {
            FormId = 1,
            Filter = ["createdAt>:2026-01-01T00:00:00.000Z"]
        };

        // Act
        var validationResult = validator.Validate(request);

        // Assert
        validationResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_CalendarDateBounds_AreAccepted()
    {
        // Arrange
        var validator = new ListByFormIdValidator(Options.Create(new SubmitterOptions()));
        var request = new ListByFormIdRequest
        {
            FormId = 1,
            CreatedFrom = "2026-01-01",
            CreatedTo = "2026-01-31",
            SortBy = SubmissionListSortBy.CreatedAt,
            SortDir = SortDirection.Desc,
        };

        // Act
        var validationResult = validator.Validate(request);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }
}

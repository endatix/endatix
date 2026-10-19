using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Modules.Reporting.Endpoints.Submissions;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Persistence;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Submissions;

public sealed class ListGridSubmissionsTests
{
    private const long FormId = 100;

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ListGridSubmissions _endpoint;

    public ListGridSubmissionsTests()
    {
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        ListSubmissionGridRows rows = new(_mediator, Substitute.For<IReportingDbContext>());
        _endpoint = Factory.Create<ListGridSubmissions>(rows, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsNotFoundProblem()
    {
        // Arrange
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<Paged<SubmissionDto>>.NotFound("Form not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(new ListGridSubmissionsRequest { FormId = FormId }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTenant_ReturnsUnauthorizedProblemAndDoesNotList()
    {
        // Arrange
        _tenantContext.TenantId.Returns(0);

        // Act
        var response = await _endpoint.ExecuteAsync(new ListGridSubmissionsRequest { FormId = FormId }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        await _mediator.DidNotReceive().Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPage_ReturnsOkWithThePagingOfTheList()
    {
        // Arrange
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<SubmissionDto>(3, 20, 60, 3, [])));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new ListGridSubmissionsRequest { FormId = FormId, Columns = "city" },
            CancellationToken.None);

        // Assert
        var ok = response.Result as Ok<Paged<SubmissionGridRow>>;
        ok.Should().NotBeNull();
        ok!.Value!.Items.Should().BeEmpty();
        ok.Value.Page.Should().Be(3);
        ok.Value.TotalRecords.Should().Be(60);
    }

    [Fact]
    public async Task ExecuteAsync_SendsTheSubmissionsListQueryOfTheRequest()
    {
        // Arrange
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<SubmissionDto>(1, 10, 0, 0, [])));
        ListGridSubmissionsRequest request = new()
        {
            FormId = FormId,
            Page = 2,
            PageSize = 10,
            Filter = ["isComplete:true"],
            SortBy = SubmissionListSortBy.ModifiedAt,
            SortDir = SortDirection.Asc,
            Columns = "city,notes",
        };

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListByFormIdQuery>(query =>
                query.FormId == FormId &&
                query.Page == 2 &&
                query.PageSize == 10 &&
                query.FilterExpressions!.Single() == "isComplete:true" &&
                query.SortBy == SubmissionListSortBy.ModifiedAt &&
                !query.SortDescending),
            Arg.Any<CancellationToken>());
    }
}

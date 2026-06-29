using Endatix.Api.Endpoints.Forms;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class ListTests
{
    private readonly IMediator _mediator;
    private readonly List _endpoint;

    public ListTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<List>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var request = new FormsListRequest { Page = -1 };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithPagedForms()
    {
        // Arrange
        var request = new FormsListRequest { Page = 1, PageSize = 10 };
        var forms = new List<FormDto>
        {
            new() { Id = "1", Name = "Form 1", SubmissionsCount = 4 },
            new() { Id = "2", Name = "Form 2", SubmissionsCount = 0 },
        };
        var paged = new Paged<FormDto>(1, 10, 2, 1, forms);
        var result = Result.Success(paged);

        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<Paged<FormModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Items.Count.Should().Be(2);
        okResult.Value.TotalRecords.Should().Be(2);
        okResult.Value.Items.First().SubmissionsCount.Should().Be(4);
        okResult.Value.Items.Last().SubmissionsCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new FormsListRequest
        {
            Page = 2,
            PageSize = 20,
            Search = "survey",
            IsEnabled = true,
            IsPublic = false,
            FolderId = 42,
            Filter = ["expression1", "expression2"],
        };
        var result = Result.Success(Paged<FormDto>.Empty(20));

        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListFormsQuery>(query =>
                query.Page == request.Page &&
                query.PageSize == request.PageSize &&
                query.Search == request.Search &&
                query.IsEnabled == request.IsEnabled &&
                query.IsPublic == request.IsPublic &&
                query.FolderId == request.FolderId &&
                query.FilterExpressions == request.Filter),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoFilter_DoesNotPassFilterToQuery()
    {
        // Arrange
        var request = new FormsListRequest
        {
            Page = 1,
            PageSize = 10,
            Filter = null,
        };
        var result = Result.Success(Paged<FormDto>.Empty(10));

        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListFormsQuery>(query =>
                query.Page == request.Page &&
                query.PageSize == request.PageSize &&
                query.FilterExpressions == null &&
                query.FolderId == null),
            Arg.Any<CancellationToken>());
    }
}

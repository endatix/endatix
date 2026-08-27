using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.List;
using Endatix.Core.UseCases.FormTemplates;

namespace Endatix.Api.Tests.Endpoints.FormTemplates;

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
        var request = new FormTemplatesListRequest { Page = 1, PageSize = 10 };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<ListFormTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithPagedFormTemplates()
    {
        // Arrange
        var request = new FormTemplatesListRequest { Page = 1, PageSize = 10 };
        var formTemplates = new List<FormTemplateDto>
        {
            new() { Id = "1", Name = "Template 1" },
            new() { Id = "2", Name = "Template 2" }
        };
        var paged = Paged<FormTemplateDto>.FromPage(1, 10, 2, formTemplates);
        var result = Result.Success(paged);

        _mediator.Send(Arg.Any<ListFormTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<Paged<FormTemplateModelWithoutJsonData>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Items.Should().HaveCount(2);
        okResult.Value.TotalRecords.Should().Be(2);
        okResult.Value.Items.First().Id.Should().Be("1");
        okResult.Value.Items.First().Name.Should().Be("Template 1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new FormTemplatesListRequest
        {
            Page = 2,
            PageSize = 20,
            Filter = ["folderId:null"],
            SortBy = FormTemplateListSortBy.Name,
            SortDir = SortDirection.Asc,
            CreatedFrom = "2024-01-01",
            CreatedTo = "2024-01-31",
            ModifiedFrom = "2024-02-01",
            ModifiedTo = "2024-02-28",
        };
        var result = Result.Success(Paged<FormTemplateDto>.Empty(20));

        _mediator.Send(Arg.Any<ListFormTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListFormTemplatesQuery>(query =>
                query.Page == request.Page &&
                query.PageSize == request.PageSize &&
                query.FilterExpressions == request.Filter &&
                query.FolderId == request.FolderId &&
                query.SortBy == FormTemplateListSortBy.Name &&
                query.SortDescending == false &&
                query.Created.InclusiveFrom == new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
                query.Created.ExclusiveTo == new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc) &&
                query.Modified.InclusiveFrom == new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc) &&
                query.Modified.ExclusiveTo == new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc)
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

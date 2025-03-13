using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
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
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new FormTemplatesListRequest { Page = 1, PageSize = 10 };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<ListFormTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithFormTemplates()
    {
        // Arrange
        var request = new FormTemplatesListRequest { Page = 1, PageSize = 10 };
        var formTemplates = new List<FormTemplateDto>
        {
            new() { Id = "1", Name = "Template 1" },
            new() { Id = "2", Name = "Template 2" }
        };
        var result = Result.Success(formTemplates.AsEnumerable());

        _mediator.Send(Arg.Any<ListFormTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<FormTemplateModelWithoutJsonData>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().HaveCount(2);
        okResult!.Value!.First().Id.Should().Be("1");
        okResult!.Value!.First().Name.Should().Be("Template 1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new FormTemplatesListRequest { Page = 2, PageSize = 20 };
        var result = Result.Success(Enumerable.Empty<FormTemplateDto>());
        
        _mediator.Send(Arg.Any<ListFormTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListFormTemplatesQuery>(query =>
                query.Page == request.Page &&
                query.PageSize == request.PageSize
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Forms.List;
using Endatix.Core.UseCases.Forms;

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
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new FormsListRequest { Page = -1 };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithForms()
    {
        // Arrange
        var request = new FormsListRequest { Page = 1, PageSize = 10 };
        var forms = new List<FormDto> 
        { 
            new() { Id = "1", Name = "Form 1" },
            new() { Id = "2", Name = "Form 2" }
        };
        var result = Result.Success(forms.AsEnumerable());

        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<FormModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Count().Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new FormsListRequest
        {
            Page = 2,
            PageSize = 20
        };
        var result = Result.Success(Enumerable.Empty<FormDto>());
        
        _mediator.Send(Arg.Any<ListFormsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListFormsQuery>(query =>
                query.Page == request.Page &&
                query.PageSize == request.PageSize
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

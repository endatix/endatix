using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Themes;
using Endatix.Core.UseCases.Themes.GetById;

namespace Endatix.Api.Tests.Endpoints.Themes;

public class GetByIdTests
{
    private readonly IMediator _mediator;
    private readonly GetById _endpoint;

    public GetByIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetById>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ThemeNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new GetByIdRequest { ThemeId = 1 };
        var result = Result<Theme>.NotFound();

        _mediator.Send(Arg.Any<GetThemeByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new GetByIdRequest { ThemeId = 0 }; // Invalid ID
        var result = Result<Theme>.Invalid();

        _mediator.Send(Arg.Any<GetThemeByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ThemeFound_ReturnsOkWithTheme()
    {
        // Arrange
        var request = new GetByIdRequest { ThemeId = 1 };
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "Test Description", "{\"primaryColor\":\"#123456\"}") { Id = 1 };
        var result = Result<Theme>.Success(theme);

        _mediator.Send(Arg.Any<GetThemeByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<ThemeModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(theme.Id.ToString());
        okResult!.Value!.Name.Should().Be(theme.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetByIdRequest { ThemeId = 1 };
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme") { Id = 1 };
        var result = Result<Theme>.Success(theme);

        _mediator.Send(Arg.Any<GetThemeByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetThemeByIdQuery>(query => query.ThemeId == request.ThemeId),
            Arg.Any<CancellationToken>()
        );
    }
}
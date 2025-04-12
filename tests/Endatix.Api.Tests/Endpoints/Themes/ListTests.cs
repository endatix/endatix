using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Themes;
using Endatix.Core.UseCases.Themes.List;

namespace Endatix.Api.Tests.Endpoints.Themes;

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
        var request = new ListRequest();
        var result = Result<List<Theme>>.Invalid();
        
        _mediator.Send(Arg.Any<ListThemesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithThemes()
    {
        // Arrange
        var request = new ListRequest();
        var themes = new List<Theme>
        {
            new Theme(SampleData.TENANT_ID, "Theme 1") { Id = 1 },
            new Theme(SampleData.TENANT_ID, "Theme 2") { Id = 2 }
        };
        var result = Result<List<Theme>>.Success(themes);
        
        _mediator.Send(Arg.Any<ListThemesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<ThemeModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyList_ReturnsOkWithEmptyList()
    {
        // Arrange
        var request = new ListRequest();
        var themes = new List<Theme>();
        var result = Result<List<Theme>>.Success(themes);
        
        _mediator.Send(Arg.Any<ListThemesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<ThemeModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().BeEmpty();
    }
} 
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Themes;
using Endatix.Core.UseCases.Themes.Update;
using System.Text.Json;

namespace Endatix.Api.Tests.Endpoints.Themes;

public class UpdateTests
{
    private readonly IMediator _mediator;
    private readonly Update _endpoint;

    public UpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Update>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ThemeNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme",
            Description = "Updated Description",
            JsonData = "{\"primaryColor\":\"#654321\"}"
        };
        var result = Result<Theme>.NotFound();
        
        _mediator.Send(Arg.Any<UpdateThemeCommand>(), Arg.Any<CancellationToken>())
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
        var request = new UpdateRequest
        {
            ThemeId = 1,
            Name = "", // Invalid name
            Description = "Updated Description",
            JsonData = "{\"primaryColor\":\"#654321\"}"
        };
        var result = Result<Theme>.Invalid();
        
        _mediator.Send(Arg.Any<UpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJsonData_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme",
            Description = "Updated Description",
            JsonData = "{ invalid json }"
        };

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedTheme()
    {
        // Arrange
        var request = new UpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme",
            Description = "Updated Description",
            JsonData = "{\"primaryColor\":\"#654321\"}"
        };
        
        var theme = new Theme(SampleData.TENANT_ID, "Updated Theme", "Updated Description", "{\"primaryColor\":\"#654321\"}") { Id = 1 };
        var result = Result<Theme>.Success(theme);
        
        _mediator.Send(Arg.Any<UpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<UpdateResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(theme.Id.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new UpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme",
            Description = "Updated Description",
            JsonData = "{\"primaryColor\":\"#654321\"}"
        };
        
        var theme = new Theme(SampleData.TENANT_ID, "Updated Theme", "Updated Description", "{\"primaryColor\":\"#654321\"}") { Id = 1 };
        var result = Result<Theme>.Success(theme);
        
        _mediator.Send(Arg.Any<UpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateThemeCommand>(cmd =>
                cmd.ThemeId == request.ThemeId &&
                cmd.Name == request.Name &&
                cmd.Description == request.Description
            ),
            Arg.Any<CancellationToken>()
        );
    }
} 
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Themes;
using Endatix.Core.UseCases.Themes.PartialUpdate;
using System.Text.Json;

namespace Endatix.Api.Tests.Endpoints.Themes;

public class PartialUpdateTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdate _endpoint;

    public PartialUpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdate>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ThemeNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new PartialUpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme"
        };
        var result = Result<Theme>.NotFound();

        _mediator.Send(Arg.Any<PartialUpdateThemeCommand>(), Arg.Any<CancellationToken>())
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
        var request = new PartialUpdateRequest
        {
            ThemeId = 0, // Invalid ID
            Name = "Updated Theme"
        };
        var result = Result<Theme>.Invalid();

        _mediator.Send(Arg.Any<PartialUpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

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
        var request = new PartialUpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme"
        };

        var theme = new Theme(SampleData.TENANT_ID, "Updated Theme") { Id = 1 };
        var result = Result<Theme>.Success(theme);

        _mediator.Send(Arg.Any<PartialUpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(theme.Id.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_OnlyNameProvided_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateRequest
        {
            ThemeId = 1,
            Name = "Updated Theme"
        };

        var theme = new Theme(SampleData.TENANT_ID, "Updated Theme") { Id = 1 };
        var result = Result<Theme>.Success(theme);

        _mediator.Send(Arg.Any<PartialUpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateThemeCommand>(cmd =>
                cmd.ThemeId == request.ThemeId &&
                cmd.Name == request.Name &&
                cmd.Description == null &&
                cmd.ThemeData == null
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_OnlyDescriptionProvided_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateRequest
        {
            ThemeId = 1,
            Description = "Updated Description"
        };

        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "Updated Description") { Id = 1 };
        var result = Result<Theme>.Success(theme);

        _mediator.Send(Arg.Any<PartialUpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateThemeCommand>(cmd =>
                cmd.ThemeId == request.ThemeId &&
                cmd.Name == null &&
                cmd.Description == request.Description &&
                cmd.ThemeData == null
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_OnlyJsonDataProvided_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateRequest
        {
            ThemeId = 1,
            JsonData = "{\"primaryColor\":\"#123456\"}"
        };

        var theme = new Theme(SampleData.TENANT_ID, "Test Theme") { Id = 1 };
        var result = Result<Theme>.Success(theme);

        _mediator.Send(Arg.Any<PartialUpdateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateThemeCommand>(cmd =>
                cmd.ThemeId == request.ThemeId &&
                cmd.Name == null &&
                cmd.Description == null &&
                cmd.ThemeData != null
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
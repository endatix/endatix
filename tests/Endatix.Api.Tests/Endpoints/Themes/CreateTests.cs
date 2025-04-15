using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Themes;
using Endatix.Core.UseCases.Themes.Create;

using Endatix.Core.Entities;

namespace Endatix.Api.Tests.Endpoints.Themes;

public class CreateTests
{
    private readonly IMediator _mediator;
    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Create>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new CreateRequest
        {
            Name = "Test Theme",
            Description = "Test Description",
            JsonData = ""
        };

        var result = Result.Invalid();

        _mediator.Send(Arg.Any<CreateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(invalidRequest, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithTheme()
    {
        // Arrange
        var request = new CreateRequest
        {
            Name = "Test Theme",
            Description = "Test Description",
            JsonData = "{\"primaryColor\":\"#123456\"}"
        };

        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "Test Description", "{\"primaryColor\":\"#123456\"}") { Id = 1 };
        var result = Result<Theme>.Created(theme);

        _mediator.Send(Arg.Any<CreateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result as Created<CreateResponse>;
        createdResult.Should().NotBeNull();
        createdResult!.Value.Should().NotBeNull();
        createdResult!.Value!.Id.Should().Be(theme.Id.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJsonData_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateRequest
        {
            Name = "Test Theme",
            Description = "Test Description",
            JsonData = "{ invalid json }"
        };

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new CreateRequest
        {
            Name = "Test Theme",
            Description = "Test Description",
            JsonData = "{\"primaryColor\":\"#123456\"}"
        };

        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "Test Description", "{\"primaryColor\":\"#123456\"}") { Id = 1 };
        var result = Result<Theme>.Created(theme);

        _mediator.Send(Arg.Any<CreateThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateThemeCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
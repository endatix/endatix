using Endatix.Api.Endpoints.TenantBranding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.TenantBranding;

public class RequestBodyTooLargeFilterTests
{
    private readonly RequestBodyTooLargeFilter _filter = new();
    private readonly EndpointFilterInvocationContext _context =
        EndpointFilterInvocationContext.Create(new DefaultHttpContext());

    [Fact]
    public async Task InvokeAsync_WhenTheBodyIsOverTheLimit_IsPayloadTooLarge()
    {
        // Act
        var result = await _filter.InvokeAsync(_context,
            _ => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        // Assert
        result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact]
    public async Task InvokeAsync_WithAnyOtherBadRequest_Rethrows()
    {
        // Act
        var invoke = async () => await _filter.InvokeAsync(_context,
            _ => throw new BadHttpRequestException("Unexpected end of request content.", StatusCodes.Status400BadRequest));

        // Assert
        await invoke.Should().ThrowAsync<BadHttpRequestException>();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheBodyFits_ReturnsTheEndpointsResult()
    {
        // Arrange
        var expected = new object();

        // Act
        var result = await _filter.InvokeAsync(_context, _ => ValueTask.FromResult<object?>(expected));

        // Assert
        result.Should().BeSameAs(expected);
    }
}

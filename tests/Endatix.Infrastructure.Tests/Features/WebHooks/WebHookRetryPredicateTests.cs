using System.Net;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Infrastructure.Networking;
using FluentAssertions;
using Polly;
using Polly.Retry;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class WebHookRetryPredicateTests
{
    // The predicate's exception clause matches no exception today (it tests the arguments' type), so this pins the
    // refusal exclusion for when that clause is corrected; it passes without the exclusion too.
    [Fact]
    public async Task ShouldRetry_RefusedEndpointAddress_IsNotRetried()
    {
        // Arrange
        var refused = new HttpRequestException(
            "Connection refused", new OutboundAddressRefusedException(OutboundAddressRefusalReason.NonPublicAddress));

        // Act
        var retry = await WebHookRetryPredicate.ShouldRetry(Arguments(Outcome.FromException<HttpResponseMessage>(refused)));

        // Assert
        retry.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldRetry_ServerError_IsRetried()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act
        var retry = await WebHookRetryPredicate.ShouldRetry(Arguments(Outcome.FromResult(response)));

        // Assert
        retry.Should().BeTrue();
    }

    private static RetryPredicateArguments<HttpResponseMessage> Arguments(Outcome<HttpResponseMessage> outcome) =>
        new(ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken), outcome, attemptNumber: 0);
}

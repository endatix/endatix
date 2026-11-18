using Endatix.Core.Common;
using Endatix.Framework.Logging;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Infrastructure.Tests.Features.ReCaptcha;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class PrivateNetworkTargetsStartupWarningTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task StartAsync_Switch_LogsOneWarningOnlyWhenOn(bool allowed, int warnings)
    {
        // Arrange
        var logger = new RecordingLogger<PrivateNetworkTargetsStartupWarning>();
        var warning = new PrivateNetworkTargetsStartupWarning(new OutboundAddressPolicy(allowed), logger);

        // Act
        await warning.StartAsync(CancellationToken.None);

        // Assert
        logger.Entries.Should().BeEquivalentTo(
            Enumerable.Repeat((LogLevel.Warning, EndatixEventIds.Webhooks.PrivateNetworkTargetsAllowed), warnings));
    }
}

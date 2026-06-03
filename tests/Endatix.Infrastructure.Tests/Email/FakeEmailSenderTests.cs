using Microsoft.Extensions.Logging;
using Endatix.Infrastructure.Email;

namespace Endatix.Infrastructure.Tests.Email;

public class FakeEmailSenderTests
{
    [Fact]
    public void ProviderMetadata_ReturnsConfiguredFakeProvider()
    {
        var sut = new FakeEmailSender(Substitute.For<ILogger<FakeEmailSender>>());

        sut.ProviderName.Should().Be("Fake");
        sut.IsConfigured.Should().BeTrue();
    }
}

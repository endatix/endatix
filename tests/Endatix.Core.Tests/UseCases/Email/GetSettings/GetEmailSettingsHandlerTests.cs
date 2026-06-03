using Endatix.Core.Features.Email;
using Endatix.Core.UseCases.Email.GetSettings;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.Email.GetSettings;

public class GetEmailSettingsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSettingsFromResolvedEmailSender()
    {
        var emailSender = Substitute.For<IEmailSender>();
        emailSender.ProviderName.Returns("SendGrid");
        emailSender.IsConfigured.Returns(true);
        var sut = new GetEmailSettingsHandler(emailSender);

        var result = await sut.Handle(new GetEmailSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProviderName.Should().Be("SendGrid");
        result.Value.IsConfigured.Should().BeTrue();
    }
}

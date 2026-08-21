using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Email.ListTemplates;
using FluentAssertions;

namespace Endatix.Core.Tests.UseCases.Email.ListTemplates;

public class ListEmailTemplatesHandlerTests
{
    private readonly IRepository<EmailTemplate> _repository;
    private readonly IEmailTemplateFromAddressResolver _fromAddressResolver;
    private readonly ListEmailTemplatesHandler _sut;

    public ListEmailTemplatesHandlerTests()
    {
        _repository = Substitute.For<IRepository<EmailTemplate>>();
        _fromAddressResolver = Substitute.For<IEmailTemplateFromAddressResolver>();
        _sut = new ListEmailTemplatesHandler(_repository, _fromAddressResolver);
    }

    [Fact]
    public async Task Handle_WithTemplates_ReturnsEffectiveFromAddresses()
    {
        // Arrange
        List<EmailTemplate> templates =
        [
            new EmailTemplate(
                "email-verification",
                "Verify your email",
                "<p>Verify</p>",
                "Verify",
                "noreply@endatix.com")
        ];

        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns(templates);
        _fromAddressResolver
            .Resolve("email-verification", "noreply@endatix.com")
            .Returns("custom@example.com");

        // Act
        var result = await _sut.Handle(new ListEmailTemplatesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().ContainSingle();
        result.Value!.Single().FromAddress.Should().Be("custom@example.com");
        _fromAddressResolver.Received(1).Resolve("email-verification", "noreply@endatix.com");
    }
}

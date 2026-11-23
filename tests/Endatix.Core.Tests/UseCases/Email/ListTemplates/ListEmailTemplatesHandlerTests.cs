using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Email.ListTemplates;
using FluentAssertions;

namespace Endatix.Core.Tests.UseCases.Email.ListTemplates;

public class ListEmailTemplatesHandlerTests
{
    private static readonly DeploymentSender NoSender = new("noreply@endatix.com", "Endatix", ConfiguredAddress: null);

    private readonly IRepository<EmailTemplate> _repository;
    private readonly ListEmailTemplatesHandler _sut;

    public ListEmailTemplatesHandlerTests()
    {
        _repository = Substitute.For<IRepository<EmailTemplate>>();
        var settings = new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "custom@example.com"
            }
        };
        _sut = new ListEmailTemplatesHandler(_repository, settings, NoSender);
    }

    [Fact]
    public async Task Handle_WithTemplates_ReturnsEffectiveFromAddresses()
    {
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

        var result = await _sut.Handle(new ListEmailTemplatesQuery(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().ContainSingle();
        result.Value!.Single().FromAddress.Should().Be("custom@example.com");
    }

    [Fact]
    public async Task Handle_WithEmptyConfiguredFromAddress_UsesDatabaseValue()
    {
        var repository = Substitute.For<IRepository<EmailTemplate>>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new EmailTemplate(
                "email-verification",
                "Verify your email",
                "<p>Verify</p>",
                "Verify",
                "db@example.com")
        ]);

        var sut = new ListEmailTemplatesHandler(
            repository,
            new EmailTemplateSettings
            {
                EmailVerification = new EmailTemplateConfig
                {
                    TemplateId = "email-verification",
                    FromAddress = string.Empty
                }
            },
            NoSender);

        var result = await sut.Handle(new ListEmailTemplatesQuery(), TestContext.Current.CancellationToken);

        result.Value!.Single().FromAddress.Should().Be("db@example.com");
    }

    [Fact]
    public async Task Handle_ConfiguredSenderAddress_ListsItLikeTheSendDoes()
    {
        // Arrange
        var repository = Substitute.For<IRepository<EmailTemplate>>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new EmailTemplate("forgot-password", "Reset", "<p>Reset</p>", "Reset", "noreply@endatix.com")
        ]);
        var sender = new DeploymentSender("noreply@example.com", "Example Forms", "noreply@example.com");
        var sut = new ListEmailTemplatesHandler(repository, new EmailTemplateSettings(), sender);

        // Act
        var result = await sut.Handle(new ListEmailTemplatesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Single().FromAddress.Should().Be("noreply@example.com");
    }
}

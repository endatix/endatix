using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Integrations.Slack;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Integrations.Slack;

public class SlackNotificationHandlerTests
{
    private readonly ILogger<SlackNotificationHandler> _logger = Substitute.For<ILogger<SlackNotificationHandler>>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IRepository<TenantSettings> _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();
    private readonly IRepository<Form> _formRepository = Substitute.For<IRepository<Form>>();

    [Fact]
    public async Task Handle_MissingTenantSettings_SkipsNotificationWithoutThrowing()
    {
        // Arrange
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 1,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));
        var notification = new SubmissionCompletedEvent(submission);

        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((TenantSettings?)null);

        var sut = new SlackNotificationHandler(
            _logger,
            _httpClientFactory,
            _tenantSettingsRepository,
            _formRepository);

        // Act
        await sut.Handle(notification, CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Tenant settings are not configured")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        await _formRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        _httpClientFactory.DidNotReceive().CreateClient();
    }
}

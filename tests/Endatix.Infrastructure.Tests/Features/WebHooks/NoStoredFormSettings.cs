using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.WebHooks;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

/// <summary>No stored settings row for any form, so a form's webhooks come from its old column.</summary>
internal static class NoStoredFormSettings
{
    /// <summary>A reader of <paramref name="forms"/>' webhooks with no settings rows.</summary>
    public static FormWebHookSettingsReader Reader(IRepository<Form> forms) => new(forms, Repository());

    public static IFormSettingsRepository Repository()
    {
        var repository = Substitute.For<IFormSettingsRepository>();
        repository.ReadAsync(Arg.Any<Form>(), Arg.Any<CancellationToken>())
            .Returns(call => FormSettings.For(call.Arg<Form>()));
        return repository;
    }
}

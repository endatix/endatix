using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests;

/// <summary>Builds an <see cref="IFormSettingsRepository"/> whose form saves win every race.</summary>
internal static class FormSettingsRepositoryStub
{
    public static IFormSettingsRepository Saving()
    {
        var repository = Substitute.For<IFormSettingsRepository>();
        repository.UpdateFormOrConcedeAsync(Arg.Any<Form>(), Arg.Any<CancellationToken>()).Returns(true);
        return repository;
    }
}

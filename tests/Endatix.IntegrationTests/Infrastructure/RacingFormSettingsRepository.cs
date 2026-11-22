using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Repositories;

namespace Endatix.IntegrationTests;

/// <summary>Lets another write land between a request's read of a form's settings and its save.</summary>
internal sealed class RacingFormSettingsRepository(FormSettingsRepository inner, Func<Task> otherWrite) : IFormSettingsRepository
{
    public Task<FormSettings> ReadAsync(Form form, CancellationToken cancellationToken) =>
        inner.ReadAsync(form, cancellationToken);

    public Task<FormSettings?> FindAsync(long formId, CancellationToken cancellationToken) =>
        inner.FindAsync(formId, cancellationToken);

    public async Task<bool> SaveOrConcedeAsync(FormSettings settings, CancellationToken cancellationToken)
    {
        await otherWrite();
        return await inner.SaveOrConcedeAsync(settings, cancellationToken);
    }
}

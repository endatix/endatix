using System.Linq;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Features.Forms;

/// <summary>
/// Checks for form dependencies on a data list.
/// </summary>
internal sealed class DataListDependencyChecker(AppDbContext dbContext, IFormsRepository formsRepository) : IDataListDependencyChecker
{
    /// <inheritdoc />
    public Task<bool> HasFormDependenciesAsync(long dataListId, CancellationToken cancellationToken = default)
    {
        var identifier = dataListId.ToString();
        return dbContext.FormDependencies.AnyAsync(
            x => x.DependencyType == FormDependencyType.DataList && x.DependencyIdentifier == identifier,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<FormDto>> GetDependentFormsAsync(long dataListId, CancellationToken cancellationToken = default)
    {
        FormsByDataListDependencySpec spec = new(dataListId);
        var forms = await formsRepository.ListAsync(spec, cancellationToken);
        
        return [.. forms];
    }
}

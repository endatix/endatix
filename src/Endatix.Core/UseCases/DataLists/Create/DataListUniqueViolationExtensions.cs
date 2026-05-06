using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.DataLists.Create;

internal static class DataListUniqueViolationExtensions
{
    public static bool IsDataListNameViolation(this UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, DataList.UniqueConstraints.NamePerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(DataList.NormalizedName), StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(DataList.Name), StringComparison.OrdinalIgnoreCase);
}

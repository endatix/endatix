using System.Linq;
using Endatix.Core.Configuration;

namespace Endatix.Infrastructure.Data
{
    public class TableNamePrefix
    {
    public static string GetEntityName(string entityName)
    {
        var entityEntryTypeArray = entityName.Split('.');
        return entityEntryTypeArray.LastOrDefault() ?? entityName;
    }

    public static string GetTableName(string entityName, string? configuredTableName = null)
    {
        var entityTypeName = GetEntityName(entityName);
        var tableName = configuredTableName;

        // Respect explicit ToTable(...) names and only apply conventions to default names.
        if (!string.IsNullOrWhiteSpace(tableName) &&
            !string.Equals(tableName, entityTypeName, StringComparison.Ordinal))
        {
            return tableName;
        }

        tableName = entityTypeName;
        if (!tableName.EndsWith("s", true, null))
        {
            tableName += "s";
        }

        return string.IsNullOrEmpty(EndatixConfig.Configuration.TablePrefix)
            ? tableName
            : $"{EndatixConfig.Configuration.TablePrefix}.{tableName}";
    }
    }
}
namespace Endatix.Infrastructure.Data
{
    public static class TableNamePrefix
    {
        public static string GetEntityName(string entityName)
        {
            var entityEntryTypeArray = entityName.Split('.');
            return entityEntryTypeArray[^1];
        }

        public static string GetTableName(string entityName, string? configuredTableName = null)
        {
            var entityTypeName = GetEntityName(entityName);

            // Respect explicit ToTable(...) names and only apply conventions to default names.
            if (!string.IsNullOrWhiteSpace(configuredTableName) &&
                !string.Equals(configuredTableName, entityTypeName, StringComparison.Ordinal))
            {
                return configuredTableName;
            }

            if (entityTypeName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                return entityTypeName;
            }

            return entityTypeName + "s";
        }
    }
}

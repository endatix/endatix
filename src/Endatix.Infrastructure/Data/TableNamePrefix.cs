using System.Linq;
using Endatix.Core.Configuration;

namespace Endatix.Infrastructure.Data
{
    public class TableNamePrefix
    {
        public static string GetTableName(string entityName)
        {
            var entityEntryTypeArray = entityName.Split('.');
            string name = entityEntryTypeArray.LastOrDefault();
            if (!name.EndsWith("s", true, null))
            {
                name += "s";
            }

            return string.IsNullOrEmpty(EndatixConfig.Configuration.TablePrefix) ? name : $"{EndatixConfig.Configuration.TablePrefix}.{name}";
        }
    }
}
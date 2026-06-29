using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Data.Config.AppEntities;

namespace Endatix.Persistence.SqlServer.Config
{
    /// <summary>
    /// SQL Server specific configuration for the <see cref="FormDependency"/> entity.
    /// </summary>
    [ApplyConfigurationFor<AppDbContext>]
    public class FormDependencyConfigurationSqlServer : IEntityTypeConfiguration<FormDependency>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<FormDependency> builder)
        {
            builder.HasIndex(
                    nameof(FormDependency.FormId),
                    FormDependencyConfiguration.DependencyTypeIndexPropertyName,
                    nameof(FormDependency.DependencyIdentifier))
                .IsUnique()
                .HasDatabaseName(FormDependencyConfiguration.UNIQUE_FORM_DEPENDENCY_INDEX_NAME)
                .HasFilter($"[{nameof(FormDependency.IsDeleted)}] = 0");
        }
    }
}

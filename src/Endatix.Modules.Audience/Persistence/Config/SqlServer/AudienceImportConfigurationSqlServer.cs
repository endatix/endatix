using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="AudienceImport"/>.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class AudienceImportConfigurationSqlServer : IEntityTypeConfiguration<AudienceImport>
{
    public void Configure(EntityTypeBuilder<AudienceImport> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Imports_TenantId",
            $"[{nameof(AudienceImport.TenantId)}] > 0"));
    }
}

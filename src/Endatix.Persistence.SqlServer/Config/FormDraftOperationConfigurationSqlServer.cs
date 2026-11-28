using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormDraftOperationConfigurationSqlServer : IEntityTypeConfiguration<FormDraftOperation>
{
    public void Configure(EntityTypeBuilder<FormDraftOperation> builder)
    {
        // Native json can rewrite number tokens. Validate the lexical document without converting it.
        builder.Property(row => row.Payload).HasColumnType("nvarchar(max)");
        var snapshots = builder.HasIndex(row => new { row.FormId, row.Version })
            .HasFilter($"[Kind] = N'{FormDraftOperationKinds.SNAPSHOT}'");
        SqlServerIndexBuilderExtensions.IncludeProperties(snapshots, row => row.TenantId);
        builder.ToTable(table => table.HasCheckConstraint("CK_FormDraftOperations_Payload_Json", "ISJSON([Payload], VALUE) = 1"));
    }
}

using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormDraftOperationConfigurationSqlServer : IEntityTypeConfiguration<FormDraftOperation>
{
    public void Configure(EntityTypeBuilder<FormDraftOperation> builder) =>
        builder.Property(row => row.Payload).HasColumnType("json");
}

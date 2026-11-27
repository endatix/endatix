using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config;

[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormDraftOperationConfigurationPostgreSql : IEntityTypeConfiguration<FormDraftOperation>
{
    public void Configure(EntityTypeBuilder<FormDraftOperation> builder) =>
        builder.Property(row => row.Payload).HasColumnType("jsonb");
}

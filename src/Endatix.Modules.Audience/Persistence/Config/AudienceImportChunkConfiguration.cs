using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="AudienceImportChunk"/>: one row per chunk index of an import.
/// </summary>
internal sealed class AudienceImportChunkConfiguration(AudienceSqlDialect dialect)
    : IEntityTypeConfiguration<AudienceImportChunk>
{
    public void Configure(EntityTypeBuilder<AudienceImportChunk> builder)
    {
        builder.ToTable("ImportChunks", table => table.HasCheckConstraint(
            "CK_ImportChunks_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(chunk => chunk.TenantId).IsRequired();
        builder.Property(chunk => chunk.ImportId).IsRequired();
        builder.Property(chunk => chunk.RowsJson).HasColumnType(dialect.JsonColumnType).IsRequired();
        builder.HasOne<AudienceImport>()
            .WithMany()
            .HasForeignKey(chunk => chunk.ImportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(chunk => new { chunk.ImportId, chunk.ChunkIndex })
            .IsUnique()
            .HasDatabaseName(AudienceImportChunk.UniqueConstraints.OnePerIndex);
    }
}

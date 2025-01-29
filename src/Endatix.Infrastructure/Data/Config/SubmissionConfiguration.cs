using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.ApplicationCore.Infrastructure.Data.Config;
public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    private const int TOKEN_VALUE_MAX_LENGTH = 64;

    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions");

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.Property(s => s.Id)
            .IsRequired();

        builder.Property(s => s.FormId)
           .IsRequired();

        builder.Property(s => s.JsonData)
            .IsRequired();

        builder.HasOne(s => s.FormDefinition)
            .WithMany(fd => fd.Submissions)
            .HasForeignKey(s => s.FormDefinitionId)
            .OnDelete(DeleteBehavior.NoAction)
           .IsRequired();

        builder.OwnsOne(s => s.Token, tokenBuilder =>
           {
               tokenBuilder.Property(t => t.Value)
                           .HasMaxLength(TOKEN_VALUE_MAX_LENGTH);
               tokenBuilder.Property(t => t.ExpiresAt);
               tokenBuilder.HasIndex(t => t.Value);
           });

        // Index for lookups
        builder.HasIndex(s => s.FormId);
        builder.HasIndex(s => s.FormDefinitionId);
    }
}
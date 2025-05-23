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

        builder.OwnsOne(s => s.Status, statusBuilder =>
           {
               statusBuilder.Property(s => s.Code)
                          .HasColumnName("Status")
                          .HasMaxLength(SubmissionStatus.STATUS_CODE_MAX_LENGTH)
                          .IsRequired();
               // Name can be resolved at runtime from Code
               statusBuilder.Ignore(s => s.Name);
           });

        // FormId index is needed for frequent lookups by form
        builder.HasIndex(s => s.FormId);
        builder.HasIndex(s => s.FormDefinitionId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>]
public class CustomQuestionConfiguration : IEntityTypeConfiguration<CustomQuestion>
{
    public void Configure(EntityTypeBuilder<CustomQuestion> builder)
    {
        builder.ToTable("CustomQuestions");

        builder.Property(q => q.Id)
            .IsRequired();

        builder.Property(q => q.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(q => q.JsonData)
            .IsRequired();
    }
} 
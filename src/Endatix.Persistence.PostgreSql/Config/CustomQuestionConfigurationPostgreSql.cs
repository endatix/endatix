using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class CustomQuestionConfigurationPostgreSql : IEntityTypeConfiguration<CustomQuestion>
    {
        public void Configure(EntityTypeBuilder<CustomQuestion> builder)
        {
            // Configure JsonData as JSONB for better query performance
            builder.Property(q => q.JsonData)
                .HasColumnType("jsonb");
        }
    }
} 
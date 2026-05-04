using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class FormDependencyConfiguration : IEntityTypeConfiguration<FormDependency>
{
    /// <summary>
    /// Database index name for the unique constraint on form dependency identity (shared by provider configs).
    /// </summary>
    public const string UNIQUE_FORM_DEPENDENCY_INDEX_NAME =
        "IX_FormDependencies_FormId_DependencyType_DependencyIdentifier";

    /// <summary>
    /// Shadow property name aligned with <see cref="FormDependencyType.Code"/> for indexing.
    /// </summary>
    public const string DependencyTypeIndexPropertyName = "DependencyTypeIndex";

    public void Configure(EntityTypeBuilder<FormDependency> builder)
    {
        builder.ToTable("FormDependencies");

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.FormId).IsRequired();
        builder.ComplexProperty(x => x.DependencyType, dependencyType =>
        {
            dependencyType.Property<string>(nameof(FormDependencyType.Code))
                .HasColumnName("DependencyType")
                .HasMaxLength(FormDependencyType.TYPE_CODE_MAX_LENGTH)
                .IsRequired();
            dependencyType.Ignore(nameof(FormDependencyType.Name));
        });

        // Keep a dedicated shadow property for indexing. EF currently does not accept
        // HasIndex over complex member paths (DependencyType.Code) in this model setup.
        builder.Property<string>(DependencyTypeIndexPropertyName)
            .HasColumnName("DependencyType")
            .HasMaxLength(FormDependencyType.TYPE_CODE_MAX_LENGTH)
            .IsRequired();

        builder.Property(x => x.DependencyIdentifier)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.FormId });

        builder.HasOne(x => x.Form)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.FormId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

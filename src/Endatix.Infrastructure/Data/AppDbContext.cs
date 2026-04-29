using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Abstractions;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore.Metadata;
using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Endatix.Core.Exceptions;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Represents the application database context for persisting the Endatix Domain entities
/// </summary>
public class AppDbContext : DbContext, ITenantDbContext
{
    private const string DUPLICATE_SUBMISSION_CONSTRAINT_NAME = "UX_Submissions_FormId_SubmittedBy";

    private readonly IIdGenerator<long> _idGenerator;
    private readonly ITenantContext _tenantContext;

    protected AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options, IIdGenerator<long> idGenerator, ITenantContext tenantContext) : base(options)
    {
        _idGenerator = idGenerator;
        _tenantContext = tenantContext;
    }

    public DbSet<Form> Forms { get; set; }

    public DbSet<FormDefinition> FormDefinitions { get; set; }

    public DbSet<FormTemplate> FormTemplates { get; set; }

    public DbSet<Submission> Submissions { get; set; }

    public DbSet<SubmissionVersion> SubmissionVersions { get; set; }

    public DbSet<Theme> Themes { get; set; }

    public DbSet<CustomQuestion> CustomQuestions { get; set; }

    public DbSet<SubmissionExportRow> SubmissionExportRows { get; set; }

    public DbSet<DynamicExportRow> DynamicExportRows { get; set; }

    public DbSet<EmailTemplate> EmailTemplates { get; set; }

    public DbSet<TenantSettings> TenantSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyEndatixQueryFilters(this);

        // Apply base configurations from Infrastructure assembly
        builder.ApplyConfigurationsFor<AppDbContext>(AssemblyReference.Assembly);

        // Apply database-specific configurations from the migrations assembly
        var migrationsAssembly = Database.GetService<IMigrationsAssembly>();
        if (migrationsAssembly?.Assembly != null)
        {
            builder.ApplyConfigurationsFromAssembly(migrationsAssembly.Assembly);
        }

        builder.Entity<SubmissionExportRow>()
            .HasNoKey()
            .ToTable(t => t.ExcludeFromMigrations());

        builder.Entity<DynamicExportRow>()
            .HasNoKey()
            .ToTable(t => t.ExcludeFromMigrations());

        PrefixTableNames(builder);
    }

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    public override int SaveChanges()
    {
        try
        {
            ProcessEntities();
            return base.SaveChanges();
        }
        catch (DbUpdateException dbUpdateException) when (IsDuplicateSubmissionConstraintViolation(dbUpdateException))
        {
            throw new DuplicateSubmissionException("A submission already exists for this user and form.", dbUpdateException);
        }
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ProcessEntities();
            return await base.SaveChangesAsync(true, cancellationToken);
        }
        catch (DbUpdateException dbUpdateException) when (IsDuplicateSubmissionConstraintViolation(dbUpdateException))
        {
            throw new DuplicateSubmissionException("A submission already exists for this user and form.", dbUpdateException);
        }
    }

    private static bool IsDuplicateSubmissionConstraintViolation(DbUpdateException dbUpdateException)
    {
        var current = dbUpdateException.InnerException;
        while (current is not null)
        {
            if (IsPostgreSqlUniqueViolation(current) || IsSqlServerDuplicateKeyViolation(current))
            {
                return ContainsSubmissionConstraintName(current);
            }

            current = current.InnerException;
        }

        return false;
    }

    private static bool IsPostgreSqlUniqueViolation(Exception exception)
    {
        if (!string.Equals(exception.GetType().Name, "PostgresException", StringComparison.Ordinal))
        {
            return false;
        }

        var sqlState = exception.GetType().GetProperty("SqlState")?.GetValue(exception) as string;
        return string.Equals(sqlState, "23505", StringComparison.Ordinal);
    }

    private static bool IsSqlServerDuplicateKeyViolation(Exception exception)
    {
        if (!string.Equals(exception.GetType().Name, "SqlException", StringComparison.Ordinal))
        {
            return false;
        }

        var number = exception.GetType().GetProperty("Number")?.GetValue(exception);
        if (number is not int sqlErrorNumber)
        {
            return false;
        }

        return sqlErrorNumber is 2601 or 2627;
    }

    private static bool ContainsSubmissionConstraintName(Exception exception)
    {
        var constraintName = exception.GetType().GetProperty("ConstraintName")?.GetValue(exception) as string;
        if (string.Equals(constraintName, DUPLICATE_SUBMISSION_CONSTRAINT_NAME, StringComparison.Ordinal))
        {
            return true;
        }

        return exception.Message.Contains(DUPLICATE_SUBMISSION_CONSTRAINT_NAME, StringComparison.Ordinal);
    }

    private void ProcessEntities()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Generate an id if necessary
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "Id") &&
                        entry.CurrentValues["Id"] is default(long))
                    {
                        entry.CurrentValues["Id"] = _idGenerator.CreateId();
                    }

                    // Set the CreatedAt value when not already provided
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "CreatedAt"))
                    {
                        var createdAtObj = entry.CurrentValues["CreatedAt"];
                        if (createdAtObj is DateTime createdAt && createdAt == default)
                        {
                            entry.CurrentValues["CreatedAt"] = DateTime.UtcNow;
                        }
                    }

                    break;
                case EntityState.Modified:
                    // Set the ModifiedAt value
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "ModifiedAt"))
                    {
                        entry.CurrentValues["ModifiedAt"] = DateTime.UtcNow;
                    }

                    break;
            }
        }
    }

    private void PrefixTableNames(ModelBuilder builder)
    {
        Guard.Against.Null(builder);
        var entityTypes = builder.Model.GetEntityTypes();
        if (entityTypes is null || !entityTypes.Any())
        {
            return;
        }

        foreach (var entity in entityTypes)
        {
            if (ShouldPrefixTable(entity))
            {
                builder.Entity(entity.Name).ToTable(TableNamePrefix.GetTableName(entity.Name));
            }
        }
    }

    private bool ShouldPrefixTable(IMutableEntityType? entityType)
    {
        Guard.Against.Null(entityType);

        if (entityType.IsOwned())
        {
            return false;
        }

        return true;
    }
}

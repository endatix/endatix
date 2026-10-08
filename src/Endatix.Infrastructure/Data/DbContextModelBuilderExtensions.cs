using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Config;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Endatix.Infrastructure.Data;

public static class DbContextModelBuilderExtensions
{
    /// <summary>
    /// Client snowflake on every <c>long Id</c> PK: EF <see cref="SnowflakeValueGeneratorFactory"/>
    /// + <c>OnAdd</c> + store strategy <c>None</c> for the context's provider only. Call last in
    /// <c>OnModelCreating</c>, passing the context's <see cref="DbContext.Database"/>.
    /// The factory only constructs the generator type (model is cached). <c>IIdGenerator&lt;long&gt;</c>
    /// is resolved at <c>Add</c> from host DI.
    /// </summary>
    /// <remarks>
    /// Only the active provider's <c>ValueGenerationStrategy</c> annotation is written, so a snapshot
    /// scaffolded for one provider compiles in a project that references only that provider's EF
    /// package. A non-relational provider gets the generator and no store strategy.
    /// </remarks>
    public static void ApplySnowflakeIdValueGenerators(this ModelBuilder builder, DatabaseFacade database)
    {
        Guard.Against.Null(builder);
        Guard.Against.Null(database);

        ApplySnowflakeIds(builder, StoreStrategyWriterFor(database));
    }

    /// <summary>
    /// Same as <see cref="ApplySnowflakeIdValueGenerators(ModelBuilder, DatabaseFacade)"/>, but writes
    /// the <c>ValueGenerationStrategy</c> annotation of both PostgreSQL and SQL Server.
    /// </summary>
    [Obsolete("Writes the id annotations of both PostgreSQL and SQL Server, which breaks snapshots in projects " +
        "that reference one EF provider. Use ApplySnowflakeIdValueGenerators(ModelBuilder, DatabaseFacade) " +
        "with the context's Database instead.")]
    public static void ApplySnowflakeIdValueGenerators(this ModelBuilder builder)
    {
        Guard.Against.Null(builder);

        ApplySnowflakeIds(builder, idProperty =>
        {
            idProperty.SetValueGenerationStrategy(NpgsqlValueGenerationStrategy.None);
            idProperty.SetValueGenerationStrategy(SqlServerValueGenerationStrategy.None);
        });
    }

    private static Action<IMutableProperty> StoreStrategyWriterFor(DatabaseFacade database)
    {
        if (database.IsNpgsql())
        {
            return idProperty => idProperty.SetValueGenerationStrategy(NpgsqlValueGenerationStrategy.None);
        }

        if (database.IsSqlServer())
        {
            return idProperty => idProperty.SetValueGenerationStrategy(SqlServerValueGenerationStrategy.None);
        }

        return _ => { };
    }

    private static void ApplySnowflakeIds(ModelBuilder builder, Action<IMutableProperty> setStoreStrategy)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().Where(HasLongIdKey))
        {
            IMutableProperty idProperty = builder.Entity(entityType.ClrType)
                .Property<long>("Id")
                .HasValueGeneratorFactory<SnowflakeValueGeneratorFactory>()
                .ValueGeneratedOnAdd()
                .Metadata;

            setStoreStrategy(idProperty);
            idProperty.ValueGenerated = ValueGenerated.OnAdd;
        }
    }

    private static bool HasLongIdKey(IMutableEntityType entityType) =>
        !entityType.IsOwned()
        && entityType.FindPrimaryKey() is not null
        && entityType.FindProperty("Id")?.ClrType == typeof(long);

    /// <summary>
    /// Applies named Endatix query filters for soft deletion and tenant isolation.
    /// </summary>
    /// <param name="builder">The model builder to apply the filters to.</param>
    /// <param name="dbContext">The database context to get the tenant id from.</param>
    public static void ApplyEndatixQueryFilters(this ModelBuilder builder, ITenantDbContext dbContext)
    {
        var getTenantIdMethod = dbContext.GetType().GetMethod(nameof(ITenantDbContext.GetTenantId));
        var currentTenantId = Expression.Call(Expression.Constant(dbContext), getTenantIdMethod!);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            LambdaExpression? softDeleteFilter = null;
            LambdaExpression? tenantFilter = null;

            var isDeletedProperty = entityType.ClrType.GetProperty(
                nameof(BaseEntity.IsDeleted),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (isDeletedProperty is not null && isDeletedProperty.PropertyType == typeof(bool))
            {
                var isDeletedExpression = Expression.Property(parameter, isDeletedProperty);
                softDeleteFilter = Expression.Lambda(
                    Expression.Equal(isDeletedExpression, Expression.Constant(false)),
                    parameter);
            }

            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var currentTenantIdIsZero = Expression.Equal(
                    Expression.Convert(currentTenantId, typeof(long)),
                    Expression.Constant(0L));
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantOwned.TenantId));
                var tenantIdEquals = Expression.Equal(
                    tenantIdProperty,
                    Expression.Convert(currentTenantId, typeof(long)));
                tenantFilter = Expression.Lambda(
                    Expression.OrElse(currentTenantIdIsZero, tenantIdEquals),
                    parameter);
            }

            if (softDeleteFilter is null && tenantFilter is null)
            {
                continue;
            }

            var entityBuilder = builder.Entity(entityType.ClrType);

            if (softDeleteFilter is not null)
            {
                entityBuilder.HasQueryFilter(EndatixQueryFilterNames.SoftDelete, softDeleteFilter);
            }

            if (tenantFilter is not null)
            {
                entityBuilder.HasQueryFilter(EndatixQueryFilterNames.Tenant, tenantFilter);
            }
        }
    }

    /// <summary>
    /// Re-applies each entity's table name so it resolves against the context's default schema.
    /// </summary>
    /// <remarks>
    /// Needed by module contexts that call <see cref="ModelBuilder.HasDefaultSchema"/>: entities
    /// mapped with an explicit <c>ToTable(name)</c> keep the schema they were configured with, so
    /// without this pass a module's tables can land outside its own schema.
    /// </remarks>
    public static void ApplyModuleTableNames(this ModelBuilder builder)
    {
        Guard.Against.Null(builder);

        foreach (var entity in builder.Model.GetEntityTypes().Where(entity => !entity.IsOwned()))
        {
            builder.Entity(entity.Name).ToTable(entity.GetTableName());
        }
    }

    /// <summary>
    /// Applies entity type configurations from the specified assembly, filtered by DbContext type using generic attributes.
    /// This allows isolating configurations for different DbContexts that share the same assembly.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type to filter configurations for.</typeparam>
    /// <param name="builder">The model builder to apply configurations to.</param>
    /// <param name="assembly">The assembly containing the configurations.</param>
    public static void ApplyConfigurationsFor<TDbContext>(this ModelBuilder builder, Assembly assembly) where TDbContext : DbContext
    {
        Guard.Against.Null(builder, nameof(builder));
        Guard.Against.Null(assembly, nameof(assembly));

        var targetAttributeType = typeof(ApplyConfigurationForAttribute<>).MakeGenericType(typeof(TDbContext));

        var configurationTypes = assembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.GetCustomAttributes(targetAttributeType, false).Any() &&
                type.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));

        foreach (var configurationType in configurationTypes)
        {
            var configurationInstance = Activator.CreateInstance(configurationType);

            if (configurationInstance is not null)
            {
                builder.ApplyConfiguration((dynamic)configurationInstance);
            }
        }
    }
}

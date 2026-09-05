using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Config;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Data;

public static class DbContextModelBuilderExtensions
{
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
    /// Configures Snowflake Id generation for every <see cref="BaseEntity"/> in the model, so an Id is
    /// assigned when the entity starts being tracked rather than at SaveChanges time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BaseEntity.Id"/> is <c>DatabaseGeneratedOption.None</c>, so the default value of 0 is a
    /// real key value rather than an EF temporary one. Without an Add-time generator, adding more than
    /// one new entity before saving puts several rows with Id 0 into the identity map and the change
    /// tracker rejects the second as a duplicate key.
    /// </para>
    /// <para>
    /// EF caches one model per context type, so the <paramref name="valueGeneratorFactory"/> passed by
    /// the first context built in a process is captured for the lifetime of that model. Pass the
    /// DI-registered singleton rather than constructing one per context, or every later context will
    /// silently generate ids from the first one's generator.
    /// </para>
    /// </remarks>
    /// <param name="builder">The model builder to configure.</param>
    /// <param name="valueGeneratorFactory">Factory producing the Snowflake-backed value generator.</param>
    public static void ConfigureEntityIdValueGenerators(
        this ModelBuilder builder,
        EfCoreValueGeneratorFactory valueGeneratorFactory)
    {
        Guard.Against.Null(builder, nameof(builder));
        Guard.Against.Null(valueGeneratorFactory, nameof(valueGeneratorFactory));

        var entityTypes = builder.Model.GetEntityTypes()
            .Where(entityType =>
                !entityType.IsOwned() &&
                typeof(BaseEntity).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in entityTypes)
        {
            builder.Entity(entityType.ClrType)
                .Property<long>(nameof(BaseEntity.Id))
                .HasValueGenerator((property, _) => valueGeneratorFactory.Create<long>(property))
                .ValueGeneratedNever();
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

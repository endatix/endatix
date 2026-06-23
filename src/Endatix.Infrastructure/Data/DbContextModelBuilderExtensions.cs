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
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var entityBuilder = builder.Entity(entityType.ClrType);

            var isDeletedProperty = entityType.ClrType.GetProperty(
                nameof(BaseEntity.IsDeleted),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (isDeletedProperty is not null && isDeletedProperty.PropertyType == typeof(bool))
            {
                var isDeletedExpression = Expression.Property(parameter, isDeletedProperty);
                var softDeleteFilter = Expression.Equal(isDeletedExpression, Expression.Constant(false));
                entityBuilder.HasQueryFilter(
                    EndatixQueryFilterNames.SoftDelete,
                    Expression.Lambda(softDeleteFilter, parameter));
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
                var tenantFilter = Expression.OrElse(currentTenantIdIsZero, tenantIdEquals);
                entityBuilder.HasQueryFilter(
                    EndatixQueryFilterNames.Tenant,
                    Expression.Lambda(tenantFilter, parameter));
            }
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

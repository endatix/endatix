using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data;

public static class DbContextModelBuilderExtensions
{
    /// <summary>
    /// Applies the endatix query filters to the model builder.
    /// It adds a query filter for the tenant id and the is deleted property.
    /// </summary>
    /// <param name="builder">The model builder to apply the filters to.</param>
    /// <param name="dbContext">The database context to get the tenant id from.</param>
    public static void ApplyEndatixQueryFilters(this ModelBuilder builder, ITenantDbContext dbContext)
    {

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var filters = new List<Expression>();

            // Add deletion filter for BaseEntity descendants
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var isDeletedProperty = Expression.Property(parameter, "IsDeleted");
                var isDeletedFilter = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                filters.Add(isDeletedFilter);
            }

            // Add tenant filter for TenantEntity descendants
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var currentTenantId = Expression.Call(Expression.Constant(dbContext), dbContext.GetType().GetMethod(nameof(ITenantDbContext.GetTenantId))!);
                var currentTenantIdIsZero = Expression.Equal(Expression.Convert(currentTenantId, typeof(long)), Expression.Constant(0L));
                var tenantIdProperty = Expression.Property(parameter, "TenantId");
                var tenantIdEquals = Expression.Equal(tenantIdProperty, Expression.Convert(currentTenantId, typeof(long)));
                var tenantFilter = Expression.OrElse(currentTenantIdIsZero, tenantIdEquals);
                filters.Add(tenantFilter);
            }

            if (filters.Any())
            {
                var combinedFilter = filters.Aggregate(Expression.AndAlso);
                var lambda = Expression.Lambda(combinedFilter, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
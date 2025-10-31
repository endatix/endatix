using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Identity.Repositories;

/// <summary>
/// Repository for managing roles in the AppIdentityDbContext.
/// Handles complex multi-entity operations with transaction management.
/// Does not inherit from RepositoryBase because AppRole does not implement IAggregateRoot (it's an ASP.NET Identity entity).
/// </summary>
public class RolesRepository : IRolesRepository
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly AppIdentityDbContext _identityDbContext;

    public RolesRepository(
        AppIdentityDbContext dbContext,
        [FromKeyedServices("identity")] IUnitOfWork unitOfWork,
        IIdGenerator<long> idGenerator)
    {
        _identityDbContext = dbContext;
        _unitOfWork = unitOfWork;
        _idGenerator = idGenerator;
    }

    public async Task<AppRole> CreateRoleWithPermissionsAsync(
        AppRole role,
        List<long> permissionIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            role.Id = _idGenerator.CreateId();

            // Save role first
            _identityDbContext.Roles.Add(role);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Add permissions after role is saved with pre-generated IDs
            foreach (var permissionId in permissionIds)
            {
                var rolePermission = new RolePermission(role.Id, permissionId)
                {
                    Id = _idGenerator.CreateId()
                };
                _identityDbContext.RolePermissions.Add(rolePermission);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return role;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteRoleAsync(AppRole role, CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Explicitly remove RolePermissions first
            var rolePermissions = _identityDbContext.RolePermissions.Where(rp => rp.RoleId == role.Id);
            _identityDbContext.RolePermissions.RemoveRange(rolePermissions);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Then remove role
            _identityDbContext.Roles.Remove(role);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

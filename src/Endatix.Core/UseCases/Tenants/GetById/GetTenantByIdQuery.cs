using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Tenants.GetById;

/// <summary>
/// Loads a platform tenant and its self-registration policy by id.
/// </summary>
public sealed record GetTenantByIdQuery(long TenantId) : IQuery<Result<TenantDto>>;

using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.InviteUser;

internal sealed record CheckInviteTarget([property: Sensitive(SensitivityType.Email)] string Email, long TenantId) : IQuery<Result>;

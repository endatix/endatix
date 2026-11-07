using System.Reflection;
using System.Runtime.CompilerServices;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities.Lifecycle;

/// <summary>
/// The host codes this test assembly registers, once, before any test reads the process-wide catalog.
/// </summary>
internal static class HostCollectionStatuses
{
    public const string PanelOverquota = "panel_overquota";
    public const string InviteExpired = "invite_expired";
    public const string InviteSent = "invite_sent";
    public const string UnderReview = "under_review";

    public static IReadOnlyDictionary<string, CollectionPhase> Codes { get; } = new Dictionary<string, CollectionPhase>
    {
        [PanelOverquota] = CollectionPhase.Closed,
        [InviteExpired] = CollectionPhase.Lapsed,
        [InviteSent] = CollectionPhase.Pending,
        [UnderReview] = CollectionPhase.Active,
    };

    // Other test projects load this assembly for its helpers, after their own tests read the catalog: register only when
    // this is the test assembly being run.
    [ModuleInitializer]
    internal static void Register()
    {
        if (Assembly.GetEntryAssembly() == typeof(HostCollectionStatuses).Assembly)
        {
            CollectionStatusCatalog.Configure(Codes);
        }
    }
}

using Endatix.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Features.WebHooks;

public class WebHooksPlugin : IPluginInitializer
{
    public class EventNames
    {
        public const string FORM_CREATED = "form_created";
        public const string FORM_UPDATED = "form_updated";
        public const string FORM_ENABLED_STATE_CHANGED = "form_enabled_state_changed";
        public const string SUBMISSION_COMPLETED = "submission_completed";
        /// <summary>A submission ended in a closed outcome, e.g. a screen-out.</summary>
        public const string SUBMISSION_CLOSED = "submission_closed";

        [Obsolete("Retired: use SUBMISSION_CLOSED.", error: true)]
        public const string SUBMISSION_COLLECTION_STATUS_CHANGED = "submission_collection_status_changed";
        public const string FORM_DELETED = "form_deleted";
    }

    public static Action<IServiceCollection> InitializationDelegate => throw new NotImplementedException();
}
using Endatix.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Features.WebHooks;

public class WebHooksPlugin : IPluginInitializer
{
    public class EventNames
    {
        public const string FORM_SUBMITTED = "form_submitted";
    }

    public static Action<IServiceCollection> InitializationDelegate => throw new NotImplementedException();
}
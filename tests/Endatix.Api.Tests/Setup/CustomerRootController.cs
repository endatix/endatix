using Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Tests.Setup;

/// <summary>A host's own controller on <c>GET /</c>, as a customer host that maps controllers has.</summary>
[ApiController]
public sealed class CustomerRootController : ControllerBase
{
    internal const string BODY = "the host's own root";

    [HttpGet("/")]
    public ContentResult Get() => Content(BODY);
}

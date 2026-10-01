using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A local HTTP endpoint that records every webhook POST it receives and answers with the status configured for
/// its path.
/// </summary>
internal sealed class StubWebHookReceiver : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, HttpStatusCode> _statuses = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ReceivedWebHook> _received = new();
    private readonly Task _serving;

    public StubWebHookReceiver()
    {
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _serving = ServeAsync();
    }

    public string BaseUrl { get; }

    public IReadOnlyCollection<ReceivedWebHook> Received => _received;

    public string UrlFor(string path, HttpStatusCode status = HttpStatusCode.OK)
    {
        _statuses[path] = status;
        return $"{BaseUrl}/{path}";
    }

    public int CountFor(string path) => _received.Count(request => request.Path == path);

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        try
        {
            await _serving;
        }
        catch (Exception)
        {
            // The listener throws out of GetContextAsync once it is closed.
        }
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }

            var path = context.Request.Url!.AbsolutePath.Trim('/');
            _received.Enqueue(new ReceivedWebHook(path, context.Request.Headers["X-Endatix-Hook-Id"]));
            context.Response.StatusCode = (int)_statuses.GetValueOrDefault(path, HttpStatusCode.OK);
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}

internal sealed record ReceivedWebHook(string Path, string? HookId);

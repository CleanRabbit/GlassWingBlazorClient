using System.Net;

namespace GlassWingClient.Services;

// Every non-exempt write endpoint returns a uniform 409 { error, reason } when a welfare
// block is active (see WelfareLockFilter.cs, server-side), regardless of which specific
// action triggered it. This is the shared safety net for a player parked on one screen who
// attempts a blocked action without having navigated recently — the failed attempt itself
// becomes the trigger that refreshes welfare state and pops the right overlay/banner, instead
// of just surfacing a generic error string.
public class WelfareBlockDetectionHandler(WelfareBlockSignal signal) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method != HttpMethod.Get && response.StatusCode == HttpStatusCode.Conflict)
            signal.Raise();
        return response;
    }
}

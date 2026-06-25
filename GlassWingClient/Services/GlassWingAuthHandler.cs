using System.Net.Http.Headers;

namespace GlassWingClient.Services;

public class GlassWingAuthHandler(AuthStateService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var header = auth.GetAuthHeaderValue();
        if (header is not null)
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(header);
        return await base.SendAsync(request, cancellationToken);
    }
}

using CPCA.MailMule.Backend.Middleware;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;

namespace CPCA.MailMule.Backend.Services;

internal sealed class JwtInjectingTransformer : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        String destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        var tokenService = httpContext.RequestServices.GetRequiredService<InternalTokenService>();
        var user = httpContext.User.Identity?.Name ?? "unknown";
        var token = tokenService.CreateToken(user);

        proxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        proxyRequest.Headers.Remove(CorrelationIdHeaderNames.CorrelationId);
        proxyRequest.Headers.TryAddWithoutValidation(CorrelationIdHeaderNames.CorrelationId, httpContext.TraceIdentifier);
    }
}

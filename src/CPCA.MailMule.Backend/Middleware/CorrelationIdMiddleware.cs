using CPCA.MailMule;
using Serilog.Context;

namespace CPCA.MailMule.Backend.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdHeaderNames.CorrelationId] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static String GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderNames.CorrelationId, out var correlationIds))
        {
            var correlationId = correlationIds.FirstOrDefault(static value => !String.IsNullOrWhiteSpace(value));

            if (!String.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }
        }

        return Guid.NewGuid().ToString("n");
    }
}
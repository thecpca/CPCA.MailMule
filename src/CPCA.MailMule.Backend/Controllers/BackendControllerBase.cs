using Microsoft.AspNetCore.Mvc;

namespace CPCA.MailMule.Backend.Controllers;

[ApiController]
public abstract class BackendControllerBase : ControllerBase
{
    protected String GetCurrentUserName()
    {
        return User.Identity?.Name ?? "Unknown";
    }

    protected static Boolean IsAjaxRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Requested-With", out var requestedWithValues)
            && requestedWithValues.Any(value => String.Equals(value, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (request.GetTypedHeaders().Accept?.Any(header =>
                String.Equals(header.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        return request.Headers.ContainsKey("Authorization");
    }
}

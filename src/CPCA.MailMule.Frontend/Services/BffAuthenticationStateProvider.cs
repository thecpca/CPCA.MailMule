using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace CPCA.MailMule.Frontend.Services;

public class BffAuthenticationStateProvider(HttpClient http, NavigationManager navigationManager)
    : AuthenticationStateProvider
{
    private readonly HttpClient _http = http;
    private readonly NavigationManager _navigationManager = navigationManager;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await _http.GetAsync("/bff/user");

            if (!response.IsSuccessStatusCode)
            {
#if DEBUG
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Not authenticated
                    _navigationManager.NavigateTo("https://localhost:5001/signin", forceLoad: true);
                }
#endif
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();

            if (userInfo is null)
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, userInfo.Name)
            };

            foreach (var role in userInfo.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, MailMuleEndpoints.Backend);
            var principal = new ClaimsPrincipal(identity);

            return new AuthenticationState(principal);
        }
        catch
        {
            // Not authenticated or call failed
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private class UserInfo
    {
        public String Name { get; set; } = "";
        public String[] Roles { get; set; } = [];
    }
}

using CPCA.MailMule.Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;

namespace CPCA.MailMule.Backend;

public static class Program
{
    public static async Task Main(String[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.WithProperty("InstanceId", Guid.NewGuid().ToString("n"))
            .Enrich.WithProperty("ApplicationName", ThisAssembly.AssemblyTitle)
            .Enrich.WithProperty("ApplicationVersion", ThisAssembly.AssemblyVersion)
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Console()
            .CreateLogger();

        builder.Services.AddSerilog();

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddProblemDetails();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        if (builder.Environment.IsDevelopment())
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
        }

        // -----------------------------
        // Authentication configuration
        // -----------------------------
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = "bff-auth";
            options.Cookie.HttpOnly = true;

            // Required because the Backend is running on a different port than the Frontend.
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
        })
        .AddOpenIdConnect(options =>
        {
            // ToDo: Move these OpenIdConnect settings to configuration
            options.Authority = builder.Configuration["OpenId:Authority"];
            options.ClientId = builder.Configuration["OpenId:ClientId"];
            options.ClientSecret = builder.Configuration["OpenId:ClientSecret"];

            //// Bypass IHttpClientFactory so ConfigureHttpClientDefaults (Aspire resilience
            //// + service discovery) does NOT apply to the external OIDC backchannel.
            //options.BackchannelHttpHandler = new HttpClientHandler();

            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;

            options.SaveTokens = true; // Store tokens server-side only

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            options.GetClaimsFromUserInfoEndpoint = true;

            options.TokenValidationParameters.NameClaimType = "name";
        });

        builder.Services.AddAuthorization();

        var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"]
            ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");

        // -----------------------------
        // CORS for Blazor WASM
        // -----------------------------
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("wasm",
                policy => policy
                    .WithOrigins(frontendBaseUrl)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        // AddHttpForwarderWithServiceDiscovery registers IHttpForwarder backed by an
        // IHttpClientFactory-managed HttpClient. AddServiceDefaults already wired
        // Aspire service discovery into ConfigureHttpClientDefaults, so the forwarder's
        // client resolves "apiservice" from the Aspire-injected environment variables.
        builder.Services.AddHttpForwarderWithServiceDiscovery();

        builder.Services.AddSingleton<InternalTokenService>();

        // -----------------------------
        // Minimal API
        // -----------------------------
        var app = builder.Build();

        app.UseSerilogRequestLogging(); 

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseCors("wasm");
        app.UseAuthentication();
        app.UseAuthorization();

        // Catch-all forwarding to the ApiService. The JwtInjectingTransformer adds
        // the internal Bearer token so the ApiService can validate the caller.
        // The local /api/secure endpoint below takes priority over this catch-all.
        app.MapForwarder("/api/{**catch-all}", $"https://{MailMuleEndpoints.WebApi}",
            new ForwarderRequestConfig(),
            new JwtInjectingTransformer())
            .RequireAuthorization();

        // -----------------------------
        // Login / Logout
        // -----------------------------
        app.MapGet("/signin", async ctx =>
        {
            await ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = frontendBaseUrl + "/" });
        });

        app.MapGet("/signout", async ctx =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = frontendBaseUrl + "/" });
        });

        // -----------------------------
        // Protected API endpoint
        // -----------------------------
        app.MapGet("/bff/secure", (HttpContext ctx) =>
        {
            var name = ctx.User.Identity?.Name ?? "Unknown";
            return Results.Ok(new { message = $"Hello {name}, you are authenticated." });
        })
        .RequireAuthorization();

        app.MapGet("/bff/user", (HttpContext ctx) =>
        {
            if (!ctx.User.Identity?.IsAuthenticated ?? true)
            {
                return Results.Unauthorized();
            }

            var name = ctx.User.Identity!.Name ?? "Unknown";

            var roles = ctx.User.Claims
                .Where(c => c.Type is "role" or "roles")
                .Select(c => c.Value)
                .ToArray();

            return Results.Ok(new
            {
                name,
                roles
            });
        })
        .RequireAuthorization();

        // -----------------------------
        await app.RunAsync();
    }

    private sealed class JwtInjectingTransformer : HttpTransformer
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
        }
    }
}

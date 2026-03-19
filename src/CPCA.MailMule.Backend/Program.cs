// CPCA MailMule
// Copyright (C) 2026 Doug Wilson
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU Affero General Public License as published by the Free Software Foundation, either
// version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License along with this
// program. If not, see <https://www.gnu.org/licenses/>.

using CPCA.MailMule;
using CPCA.MailMule.Backend.Services;
using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
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

        var connstring = builder.Configuration.GetConnectionString("MailMule")
            ?? throw new InvalidOperationException("Connection string 'MailMule' is not configured.");

        builder.Services.AddMailMule(options => options.UsePostgreSql(connstring));
        builder.Services.AddMailMuleApplication();

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
        // client resolves "imapservice" from the Aspire-injected environment variables.
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

        // Catch-all forwarding to the ImapService. The JwtInjectingTransformer adds
        // the internal Bearer token so the ImapService can validate the caller.
        // The local /api/secure endpoint below takes priority over this catch-all.
        app.MapForwarder("/api/{**catch-all}", $"https://{MailMuleEndpoints.ImapService}",
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
        // Admin API endpoints
        // -----------------------------
        
        // GET /admin/incoming - List all incoming mailboxes
        app.MapGet("/admin/incoming", async (IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailboxes = await service.GetMailboxesByTypeAsync("Incoming", ct);
            return Results.Ok(mailboxes);
        })
        .RequireAuthorization()
        .WithName("ListIncomingMailboxes")
        .WithOpenApi()
        .Produces<IEnumerable<MailboxConfigDto>>();

        // POST /admin/incoming - Create incoming mailbox
        app.MapPost("/admin/incoming", async (CreateMailboxConfigDto dto, IMailboxConfigService service, CancellationToken ct) =>
        {
            var id = await service.CreateMailboxAsync(dto with { MailboxType = "Incoming" }, ct);
            return Results.Created($"/admin/incoming/{id}", id);
        })
        .RequireAuthorization()
        .WithName("CreateIncomingMailbox")
        .WithOpenApi()
        .Produces<Int64>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        // GET /admin/incoming/{id} - Get specific incoming mailbox
        app.MapGet("/admin/incoming/{id:long}", async (Int64 id, IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailbox = await service.GetMailboxAsync(id, ct);
            return mailbox == null ? Results.NotFound() : Results.Ok(mailbox);
        })
        .RequireAuthorization()
        .WithName("GetIncomingMailbox")
        .WithOpenApi()
        .Produces<MailboxConfigDto>()
        .Produces(StatusCodes.Status404NotFound);

        // PUT /admin/incoming/{id} - Update incoming mailbox
        app.MapPut("/admin/incoming/{id:long}", async (Int64 id, UpdateMailboxConfigDto dto, IMailboxConfigService service, CancellationToken ct) =>
        {
            if (dto.Id != id)
            {
                return Results.BadRequest("ID mismatch");
            }

            try
            {
                await service.UpdateMailboxAsync(dto, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization()
        .WithName("UpdateIncomingMailbox")
        .WithOpenApi()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /admin/incoming/{id} - Delete incoming mailbox
        app.MapDelete("/admin/incoming/{id:long}", async (Int64 id, IMailboxConfigService service, CancellationToken ct) =>
        {
            try
            {
                await service.DeleteMailboxAsync(id, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization()
        .WithName("DeleteIncomingMailbox")
        .WithOpenApi()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /admin/incoming/test-connection - Test mailbox connection
        app.MapPost("/admin/incoming/test-connection", async (MailboxConnectionTestRequest request, IMailboxConfigService service, CancellationToken ct) =>
        {
            var result = await service.TestConnectionAsync(request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("TestIncomingConnection")
        .WithOpenApi()
        .Produces<MailboxConnectionTestResult>();

        // GET /admin/outgoing - List all outgoing mailboxes
        app.MapGet("/admin/outgoing", async (IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailboxes = await service.GetMailboxesByTypeAsync("Outgoing", ct);
            return Results.Ok(mailboxes);
        })
        .RequireAuthorization()
        .WithName("ListOutgoingMailboxes")
        .WithOpenApi()
        .Produces<IEnumerable<MailboxConfigDto>>();

        // POST /admin/outgoing - Create outgoing mailbox
        app.MapPost("/admin/outgoing", async (CreateMailboxConfigDto dto, IMailboxConfigService service, CancellationToken ct) =>
        {
            var id = await service.CreateMailboxAsync(dto with { MailboxType = "Outgoing" }, ct);
            return Results.Created($"/admin/outgoing/{id}", id);
        })
        .RequireAuthorization()
        .WithName("CreateOutgoingMailbox")
        .WithOpenApi()
        .Produces<Int64>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        // GET /admin/outgoing/{id} - Get specific outgoing mailbox
        app.MapGet("/admin/outgoing/{id:long}", async (Int64 id, IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailbox = await service.GetMailboxAsync(id, ct);
            return mailbox == null ? Results.NotFound() : Results.Ok(mailbox);
        })
        .RequireAuthorization()
        .WithName("GetOutgoingMailbox")
        .WithOpenApi()
        .Produces<MailboxConfigDto>()
        .Produces(StatusCodes.Status404NotFound);

        // PUT /admin/outgoing/{id} - Update outgoing mailbox
        app.MapPut("/admin/outgoing/{id:long}", async (Int64 id, UpdateMailboxConfigDto dto, IMailboxConfigService service, CancellationToken ct) =>
        {
            if (dto.Id != id)
            {
                return Results.BadRequest("ID mismatch");
            }

            try
            {
                await service.UpdateMailboxAsync(dto, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization()
        .WithName("UpdateOutgoingMailbox")
        .WithOpenApi()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /admin/outgoing/{id} - Delete outgoing mailbox
        app.MapDelete("/admin/outgoing/{id:long}", async (Int64 id, IMailboxConfigService service, CancellationToken ct) =>
        {
            try
            {
                await service.DeleteMailboxAsync(id, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization()
        .WithName("DeleteOutgoingMailbox")
        .WithOpenApi()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /admin/outgoing/test-connection - Test mailbox connection
        app.MapPost("/admin/outgoing/test-connection", async (MailboxConnectionTestRequest request, IMailboxConfigService service, CancellationToken ct) =>
        {
            var result = await service.TestConnectionAsync(request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("TestOutgoingConnection")
        .WithOpenApi()
        .Produces<MailboxConnectionTestResult>();
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

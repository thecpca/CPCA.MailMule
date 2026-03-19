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
using CPCA.MailMule.Backend.HealthChecks;
using CPCA.MailMule.Backend.Middleware;
using CPCA.MailMule.Backend.Services;
using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

        builder.Services
            .AddHealthChecks()
            .AddCheck<BackendDatabaseReadyHealthCheck>("database", tags: ["ready"]);

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

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("Operator", policy => policy.RequireRole("Operator", "Admin"));
        });

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

        app.UseMiddleware<CorrelationIdMiddleware>();
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
            .RequireAuthorization("Operator");

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
        .RequireAuthorization("Operator");

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

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        // -----------------------------
        // Admin API endpoints
        // -----------------------------
        
        // GET /admin/incoming - List all incoming mailboxes
        app.MapGet("/admin/incoming", async (IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailboxes = await service.GetMailboxesByTypeAsync("Incoming", ct);
            return Results.Ok(mailboxes);
        })
        .RequireAuthorization("Admin")
        .WithName("ListIncomingMailboxes")
        .Produces<IEnumerable<MailboxConfigDto>>();

        // POST /admin/incoming - Create incoming mailbox
        app.MapPost("/admin/incoming", async (CreateMailboxConfigDto dto, IMailboxConfigService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            var id = await service.CreateMailboxAsync(dto with { MailboxType = "Incoming" }, ct);

            logger.LogInformation(
                "Admin mailbox configuration created by {User} for {MailboxType} mailbox {MailboxId} ({DisplayName})",
                GetCurrentUserName(context),
                "Incoming",
                id,
                dto.DisplayName);

            return Results.Created($"/admin/incoming/{id}", id);
        })
        .RequireAuthorization("Admin")
        .WithName("CreateIncomingMailbox")
        .Produces<Int64>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        // GET /admin/incoming/{id} - Get specific incoming mailbox
        app.MapGet("/admin/incoming/{id:long}", async (Int64 id, IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailbox = await service.GetMailboxAsync(id, ct);
            return mailbox == null ? Results.NotFound() : Results.Ok(mailbox);
        })
        .RequireAuthorization("Admin")
        .WithName("GetIncomingMailbox")
        .Produces<MailboxConfigDto>()
        .Produces(StatusCodes.Status404NotFound);

        // PUT /admin/incoming/{id} - Update incoming mailbox
        app.MapPut("/admin/incoming/{id:long}", async (Int64 id, UpdateMailboxConfigDto dto, IMailboxConfigService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            if (dto.Id != id)
            {
                return Results.BadRequest("ID mismatch");
            }

            var existingMailbox = await service.GetMailboxAsync(id, ct);
            if (existingMailbox == null)
            {
                return Results.NotFound();
            }

            var changedFields = GetChangedFields(existingMailbox, dto);

            try
            {
                await service.UpdateMailboxAsync(dto, ct);

                foreach (var fieldName in changedFields)
                {
                    logger.LogInformation(
                        "Admin mailbox configuration field changed by {User}: {MailboxType} mailbox {MailboxId} {FieldName}",
                        GetCurrentUserName(context),
                        "Incoming",
                        id,
                        fieldName);
                }

                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("Admin")
        .WithName("UpdateIncomingMailbox")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /admin/incoming/{id} - Delete incoming mailbox
        app.MapDelete("/admin/incoming/{id:long}", async (Int64 id, IMailboxConfigService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            try
            {
                var mailbox = await service.GetMailboxAsync(id, ct);
                await service.DeleteMailboxAsync(id, ct);

                logger.LogInformation(
                    "Admin mailbox configuration deleted by {User} for {MailboxType} mailbox {MailboxId} ({DisplayName})",
                    GetCurrentUserName(context),
                    "Incoming",
                    id,
                    mailbox?.DisplayName ?? String.Empty);

                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("Admin")
        .WithName("DeleteIncomingMailbox")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /admin/incoming/test-connection - Test mailbox connection
        app.MapPost("/admin/incoming/test-connection", async (MailboxConnectionTestRequest request, IMailboxConfigService service, CancellationToken ct) =>
        {
            var result = await service.TestConnectionAsync(request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("Admin")
        .WithName("TestIncomingConnection")
        .Produces<MailboxConnectionTestResult>();

        // GET /admin/outgoing - List all outgoing mailboxes
        app.MapGet("/admin/outgoing", async (IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailboxes = await service.GetMailboxesByTypeAsync("Outgoing", ct);
            return Results.Ok(mailboxes);
        })
        .RequireAuthorization("Admin")
        .WithName("ListOutgoingMailboxes")
        .Produces<IEnumerable<MailboxConfigDto>>();

        // POST /admin/outgoing - Create outgoing mailbox
        app.MapPost("/admin/outgoing", async (CreateMailboxConfigDto dto, IMailboxConfigService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            var id = await service.CreateMailboxAsync(dto with { MailboxType = "Outgoing" }, ct);

            logger.LogInformation(
                "Admin mailbox configuration created by {User} for {MailboxType} mailbox {MailboxId} ({DisplayName})",
                GetCurrentUserName(context),
                "Outgoing",
                id,
                dto.DisplayName);

            return Results.Created($"/admin/outgoing/{id}", id);
        })
        .RequireAuthorization("Admin")
        .WithName("CreateOutgoingMailbox")
        .Produces<Int64>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        // GET /admin/outgoing/{id} - Get specific outgoing mailbox
        app.MapGet("/admin/outgoing/{id:long}", async (Int64 id, IMailboxConfigService service, CancellationToken ct) =>
        {
            var mailbox = await service.GetMailboxAsync(id, ct);
            return mailbox == null ? Results.NotFound() : Results.Ok(mailbox);
        })
        .RequireAuthorization("Admin")
        .WithName("GetOutgoingMailbox")
        .Produces<MailboxConfigDto>()
        .Produces(StatusCodes.Status404NotFound);

        // PUT /admin/outgoing/{id} - Update outgoing mailbox
        app.MapPut("/admin/outgoing/{id:long}", async (Int64 id, UpdateMailboxConfigDto dto, IMailboxConfigService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            if (dto.Id != id)
            {
                return Results.BadRequest("ID mismatch");
            }

            var existingMailbox = await service.GetMailboxAsync(id, ct);
            if (existingMailbox == null)
            {
                return Results.NotFound();
            }

            var changedFields = GetChangedFields(existingMailbox, dto);

            try
            {
                await service.UpdateMailboxAsync(dto, ct);

                foreach (var fieldName in changedFields)
                {
                    logger.LogInformation(
                        "Admin mailbox configuration field changed by {User}: {MailboxType} mailbox {MailboxId} {FieldName}",
                        GetCurrentUserName(context),
                        "Outgoing",
                        id,
                        fieldName);
                }

                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("Admin")
        .WithName("UpdateOutgoingMailbox")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /admin/outgoing/{id} - Delete outgoing mailbox
        app.MapDelete("/admin/outgoing/{id:long}", async (Int64 id, IMailboxConfigService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            try
            {
                var mailbox = await service.GetMailboxAsync(id, ct);
                await service.DeleteMailboxAsync(id, ct);

                logger.LogInformation(
                    "Admin mailbox configuration deleted by {User} for {MailboxType} mailbox {MailboxId} ({DisplayName})",
                    GetCurrentUserName(context),
                    "Outgoing",
                    id,
                    mailbox?.DisplayName ?? String.Empty);

                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("Admin")
        .WithName("DeleteOutgoingMailbox")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /admin/outgoing/test-connection - Test mailbox connection
        app.MapPost("/admin/outgoing/test-connection", async (MailboxConnectionTestRequest request, IMailboxConfigService service, CancellationToken ct) =>
        {
            var result = await service.TestConnectionAsync(request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("Admin")
        .WithName("TestOutgoingConnection")
        .Produces<MailboxConnectionTestResult>();

        // GET /admin/settings - Get user settings
        app.MapGet("/admin/settings", async (IUserSettingsService service, CancellationToken ct) =>
        {
            var settings = await service.GetAsync(ct);
            return Results.Ok(settings);
        })
        .RequireAuthorization("Admin")
        .WithName("GetUserSettings")
        .Produces<UserSettingsDto>();

        // PUT /admin/settings - Update user settings
        app.MapPut("/admin/settings", async (UserSettingsDto dto, IUserSettingsService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            await service.UpdateAsync(dto, ct);

            logger.LogInformation(
                "User settings updated by {User}: UndoWindowSeconds={UndoWindowSeconds}, PageSize={PageSize}",
                GetCurrentUserName(context),
                dto.UndoWindowSeconds,
                dto.PageSize);

            return Results.NoContent();
        })
        .RequireAuthorization("Admin")
        .WithName("UpdateUserSettings")
        .Produces(StatusCodes.Status204NoContent);

        // GET /admin/app-settings - Get application settings
        app.MapGet("/admin/app-settings", async (IApplicationSettingsService service, CancellationToken ct) =>
        {
            var settings = await service.GetAsync(ct);
            return Results.Ok(settings);
        })
        .RequireAuthorization("Admin")
        .WithName("GetApplicationSettings")
        .Produces<ApplicationSettingsDto>();

        // PUT /admin/app-settings - Update application settings
        app.MapPut("/admin/app-settings", async (ApplicationSettingsDto dto, IApplicationSettingsService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            await service.UpdateAsync(dto, ct);

            logger.LogInformation(
                "Application settings updated by {User}: InactivityTimeoutMinutes={InactivityTimeoutMinutes}",
                GetCurrentUserName(context),
                dto.InactivityTimeoutMinutes);

            return Results.NoContent();
        })
        .RequireAuthorization("Admin")
        .WithName("UpdateApplicationSettings")
        .Produces(StatusCodes.Status204NoContent);

        // GET /admin/errors - List incoming messages in Error state
        app.MapGet("/admin/errors", async (IIncomingMessageService service, CancellationToken ct) =>
        {
            var errors = await service.GetErrorMessagesAsync(ct);
            return Results.Ok(errors);
        })
        .RequireAuthorization("Admin")
        .WithName("ListErrorMessages")
        .Produces<IEnumerable<IncomingMessageDto>>();

        // POST /admin/errors/{id}/requeue - Requeue an error message
        app.MapPost("/admin/errors/{id}/requeue", async (Int64 id, IIncomingMessageService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            await service.RequeueAsync(id, ct);

            logger.LogInformation(
                "Error message {MessageId} requeued by {User}",
                id,
                GetCurrentUserName(context));

            return Results.NoContent();
        })
        .RequireAuthorization("Admin")
        .WithName("RequeueErrorMessage")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /admin/errors/{id}/dismiss - Dismiss an error message
        app.MapPost("/admin/errors/{id}/dismiss", async (Int64 id, IIncomingMessageService service, HttpContext context, ILogger<AdminApiLog> logger, CancellationToken ct) =>
        {
            await service.DismissAsync(id, ct);

            logger.LogInformation(
                "Error message {MessageId} dismissed by {User}",
                id,
                GetCurrentUserName(context));

            return Results.NoContent();
        })
        .RequireAuthorization("Admin")
        .WithName("DismissErrorMessage")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // -----------------------------
        await app.RunAsync();
    }

    private static String GetCurrentUserName(HttpContext context)
    {
        return context.User.Identity?.Name ?? "Unknown";
    }

    private static IReadOnlyList<String> GetChangedFields(MailboxConfigDto existingMailbox, UpdateMailboxConfigDto updatedMailbox)
    {
        var changedFields = new List<String>();

        if (!String.Equals(existingMailbox.DisplayName, updatedMailbox.DisplayName, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.DisplayName));
        if (!String.Equals(existingMailbox.ImapHost, updatedMailbox.ImapHost, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.ImapHost));
        if (existingMailbox.ImapPort != updatedMailbox.ImapPort) changedFields.Add(nameof(existingMailbox.ImapPort));
        if (!String.Equals(existingMailbox.MailboxType, updatedMailbox.MailboxType, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.MailboxType));
        if (!String.Equals(existingMailbox.Security, updatedMailbox.Security, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.Security));
        if (!String.Equals(existingMailbox.Username, updatedMailbox.Username, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.Username));
        if (!String.Equals(existingMailbox.InboxFolderPath, updatedMailbox.InboxFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.InboxFolderPath));
        if (!String.Equals(existingMailbox.OutboxFolderPath, updatedMailbox.OutboxFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.OutboxFolderPath));
        if (!String.Equals(existingMailbox.SentFolderPath, updatedMailbox.SentFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.SentFolderPath));
        if (!String.Equals(existingMailbox.ArchiveFolderPath, updatedMailbox.ArchiveFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.ArchiveFolderPath));
        if (!String.Equals(existingMailbox.JunkFolderPath, updatedMailbox.JunkFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.JunkFolderPath));
        if (existingMailbox.PollIntervalSeconds != updatedMailbox.PollIntervalSeconds) changedFields.Add(nameof(existingMailbox.PollIntervalSeconds));
        if (existingMailbox.DeleteMessage != updatedMailbox.DeleteMessage) changedFields.Add(nameof(existingMailbox.DeleteMessage));
        if (existingMailbox.IsActive != updatedMailbox.IsActive) changedFields.Add(nameof(existingMailbox.IsActive));
        if (existingMailbox.SortOrder != updatedMailbox.SortOrder) changedFields.Add(nameof(existingMailbox.SortOrder));
        if (!String.IsNullOrWhiteSpace(updatedMailbox.Password)) changedFields.Add(nameof(updatedMailbox.Password));

        return changedFields;
    }

    private sealed class AdminApiLog;

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
            proxyRequest.Headers.Remove(CorrelationIdHeaderNames.CorrelationId);
            proxyRequest.Headers.TryAddWithoutValidation(CorrelationIdHeaderNames.CorrelationId, httpContext.TraceIdentifier);
        }
    }
}

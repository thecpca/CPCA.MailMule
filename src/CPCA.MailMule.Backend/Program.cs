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

using CPCA.MailMule.Backend.HealthChecks;
using CPCA.MailMule.Backend.Middleware;
using CPCA.MailMule.Backend.Options;
using CPCA.MailMule.Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Security.Claims;
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
        builder.AddServiceDefaults();

        builder.Services.AddProblemDetails();
        builder.Services.AddControllers();

        var connstring = builder.Configuration.GetConnectionString("MailMule")
            ?? throw new InvalidOperationException("Connection string 'MailMule' is not configured.");

        builder.Services.AddMailMule(options => options.UsePostgreSql(connstring));
        builder.Services.AddMailMuleApplication();

        builder.Services
            .AddHealthChecks()
            .AddCheck<BackendDatabaseReadyHealthCheck>("database", tags: ["ready"]);

        builder.Services.Configure<BffAuthorizationOptions>(builder.Configuration.GetSection("Authorization"));
        builder.Services.AddTransient<IClaimsTransformation, BffRoleClaimsTransformation>();
        builder.Services.AddOpenApi();

        if (builder.Environment.IsDevelopment())
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
        }

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = "bff-auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);

            options.Events.OnRedirectToLogin = context =>
            {
                if (IsAjaxRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (IsAjaxRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        })
        .AddOpenIdConnect(options =>
        {
            options.Authority = builder.Configuration["OpenId:Authority"];
            options.ClientId = builder.Configuration["OpenId:ClientId"];
            options.ClientSecret = builder.Configuration["OpenId:ClientSecret"];
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = true;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.GetClaimsFromUserInfoEndpoint = true;

            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("Operator", policy => policy.RequireRole("Operator", "Admin"));
        });

        var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"]
            ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("wasm",
                policy => policy
                    .WithOrigins(frontendBaseUrl)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        builder.Services.AddHttpForwarderWithServiceDiscovery();
        builder.Services.AddSingleton<InternalTokenService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MailMuleDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseCors("wasm");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapForwarder(
                "/api/{**catch-all}",
                $"https://{MailMuleEndpoints.ImapService}",
                new ForwarderRequestConfig(),
                new JwtInjectingTransformer())
            .RequireAuthorization("Operator");

        app.MapControllers();

        await app.RunAsync();
    }

    private static Boolean IsAjaxRequest(HttpRequest request)
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

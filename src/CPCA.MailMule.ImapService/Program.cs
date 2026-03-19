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
using CPCA.MailMule.ImapService.HealthChecks;
using CPCA.MailMule.ImapService.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Security.Cryptography;

namespace CPCA.MailMule.ImapService;

public partial class Program
{
    private static async Task Main(String[] args)
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
            .AddCheck<ImapMailboxReadyHealthCheck>("imap_mailboxes", tags: ["ready"]);

        builder.Services.AddControllers();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddAuthentication("jwt")
            .AddJwtBearer("jwt", options =>
            {
                var pubKey = File.ReadAllText(builder.Configuration["Jwt:PublicKeyPath"]!);
                var rsa = RSA.Create();
                rsa.ImportFromPem(pubKey);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = MailMuleEndpoints.Backend,

                    ValidateAudience = true,
                    ValidAudience = MailMuleEndpoints.ImapService,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),

                    ValidateLifetime = true
                };
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/", () => "API service is running. Navigate to /GetWeatherForecast to see sample data.");

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapControllers();
        app.MapDefaultEndpoints();

        await app.RunAsync();
    }
}

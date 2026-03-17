using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Security.Cryptography;

namespace CPCA.MailMule.ApiService;

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
                    ValidAudience = MailMuleEndpoints.WebApi,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),

                    ValidateLifetime = true
                };
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseSerilogRequestLogging(); 

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/", () => "API service is running. Navigate to /GetWeatherForecast to see sample data.");

        app.MapControllers();
        app.MapDefaultEndpoints();

        await app.RunAsync();
    }
}

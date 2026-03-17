namespace CPCA.MailMule.AppHost;

public partial class Program
{
    private static void Main(String[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        var apiService = builder.AddProject<Projects.CPCA_MailMule_ApiService>(MailMuleEndpoints.WebApi);
                // .WithHttpHealthCheck("/health");

        var backEnd = builder.AddProject<Projects.CPCA_MailMule_Backend>(MailMuleEndpoints.Backend)
                .WithExternalHttpEndpoints()
                // .WithHttpHealthCheck("/health")
                .WithReference(apiService);

        var frontEnd = builder.AddProject<Projects.CPCA_MailMule_Frontend>(MailMuleEndpoints.Frontend)
                .WithExternalHttpEndpoints()
                .WithReference(apiService)
                .WithReference(backEnd)
                .WaitFor(backEnd);

        builder.Build().Run();
    }
}

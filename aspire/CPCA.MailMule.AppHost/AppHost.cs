namespace CPCA.MailMule.AppHost;

public partial class Program
{
    private static void Main(String[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        var postgres = builder.AddPostgres("postgres");
        var mailMuleDatabase = postgres.AddDatabase("MailMule");

        var imapService = builder.AddProject<Projects.CPCA_MailMule_ImapService>(MailMuleEndpoints.ImapService)
                .WithReference(mailMuleDatabase);
                // .WithHttpHealthCheck("/health");

        var backEnd = builder.AddProject<Projects.CPCA_MailMule_Backend>(MailMuleEndpoints.Backend)
                .WithExternalHttpEndpoints()
                .WithReference(mailMuleDatabase)
                // .WithHttpHealthCheck("/health")
                .WithReference(imapService);

        var frontEnd = builder.AddProject<Projects.CPCA_MailMule_Frontend>(MailMuleEndpoints.Frontend)
                .WithExternalHttpEndpoints()
                .WithReference(imapService)
                .WithReference(backEnd)
                .WaitFor(backEnd);

        builder.Build().Run();
    }
}

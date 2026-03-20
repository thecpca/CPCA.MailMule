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

namespace CPCA.MailMule.AppHost;

public partial class Program
{
    private static void Main(String[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        var postgres = builder.AddPostgres("postgres");
        var mailMuleDatabase = postgres.AddDatabase("MailMule");

        var imapService = builder.AddProject<Projects.CPCA_MailMule_ImapService>(MailMuleEndpoints.ImapService)
                .WithReference(mailMuleDatabase)
                .WaitFor(mailMuleDatabase);
        // .WithHttpHealthCheck("/health");

        var backEnd = builder.AddProject<Projects.CPCA_MailMule_Backend>(MailMuleEndpoints.Backend)
                .WithExternalHttpEndpoints()
                .WithReference(mailMuleDatabase)
                // .WithHttpHealthCheck("/health")
                .WithReference(imapService)
                .WaitFor(mailMuleDatabase);

        var frontEnd = builder.AddProject<Projects.CPCA_MailMule_Frontend>(MailMuleEndpoints.Frontend)
                .WithExternalHttpEndpoints()
                .WithReference(imapService)
                .WithReference(backEnd)
                .WaitFor(backEnd);

        builder.Build().Run();
    }
}

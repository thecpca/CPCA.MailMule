using System.Security.Claims;
using CPCA.MailMule.Backend.Options;
using CPCA.MailMule.Backend.Services;
using Microsoft.Extensions.Options;

namespace CPCA.MailMule.Tests;

public sealed class BffRoleClaimsTransformationTests
{
    [Fact]
    public async Task TransformAsync_AssignsAdminAdministratorAndOperatorRoles_ForConfiguredAdministrator()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "root@dkw.io"));
        var transformation = CreateSut(new BffAuthorizationOptions
        {
            Administrators = ["root@dkw.io"]
        });

        var transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
        Assert.Contains(transformed.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Administrator");
        Assert.Contains(transformed.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Operator");
    }

    [Fact]
    public async Task TransformAsync_AssignsOperatorRole_FromPreferredUsername()
    {
        var principal = CreatePrincipal(new Claim("preferred_username", "operator@dkw.io"));
        var transformation = CreateSut(new BffAuthorizationOptions
        {
            Operators = ["operator@dkw.io"]
        });

        var transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Operator");
        Assert.DoesNotContain(transformed.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    [Fact]
    public async Task TransformAsync_DoesNotAssignRoles_ForUnconfiguredIdentity()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "user@dkw.io"));
        var transformation = CreateSut(new BffAuthorizationOptions
        {
            Administrators = ["root@dkw.io"],
            Operators = ["operator@dkw.io"]
        });

        var transformed = await transformation.TransformAsync(principal);

        Assert.DoesNotContain(transformed.Claims, claim => claim.Type == ClaimTypes.Role);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "bff"));
    }

    private static BffRoleClaimsTransformation CreateSut(BffAuthorizationOptions options)
    {
        return new BffRoleClaimsTransformation(Options.Create(options));
    }
}

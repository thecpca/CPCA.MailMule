using System.Security.Claims;
using CPCA.MailMule.Backend.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CPCA.MailMule.Backend.Services;

public sealed class BffRoleClaimsTransformation(IOptions<BffAuthorizationOptions> options) : IClaimsTransformation
{
    private static readonly String[] CandidateIdentityClaimTypes =
    [
        ClaimTypes.Email,
        "email",
        "preferred_username",
        "upn"
    ];

    private readonly BffAuthorizationOptions _options = options.Value;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || identity.IsAuthenticated != true)
        {
            return Task.FromResult(principal);
        }

        var identities = GetCandidateIdentityValues(principal);
        if (identities.Count == 0)
        {
            return Task.FromResult(principal);
        }

        var existingRoles = principal.FindAll(ClaimTypes.Role)
            .Select(static claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rolesToAdd = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        if (MatchesConfiguredIdentity(identities, _options.Administrators))
        {
            rolesToAdd.Add("Admin");
            rolesToAdd.Add("Administrator");
            rolesToAdd.Add("Operator");
        }
        else if (MatchesConfiguredIdentity(identities, _options.Operators))
        {
            rolesToAdd.Add("Operator");
        }

        foreach (var role in rolesToAdd)
        {
            if (existingRoles.Add(role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.FromResult(principal);
    }

    private static HashSet<String> GetCandidateIdentityValues(ClaimsPrincipal principal)
    {
        return principal.Claims
            .Where(claim => CandidateIdentityClaimTypes.Contains(claim.Type, StringComparer.Ordinal))
            .Select(static claim => claim.Value.Trim())
            .Where(static value => !String.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesConfiguredIdentity(HashSet<String> identities, IEnumerable<String> configuredValues)
    {
        foreach (var configuredValue in configuredValues)
        {
            if (!String.IsNullOrWhiteSpace(configuredValue)
                && identities.Contains(configuredValue.Trim()))
            {
                return true;
            }
        }

        return false;
    }
}

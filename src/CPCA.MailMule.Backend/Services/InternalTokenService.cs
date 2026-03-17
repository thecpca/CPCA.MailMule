using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CPCA.MailMule.Backend.Services;

public class InternalTokenService
{
    private readonly SigningCredentials _creds;

    // ToDo: Change this to use IOptions<JwtSettings> and load the RSA key once at startup, rather than on every token generation.
    public InternalTokenService(IConfiguration config)
    {
        var key = File.ReadAllText(config["Jwt:PrivateKeyPath"]!);
        var rsa = RSA.Create();
        rsa.ImportFromPem(key);
        _creds = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
    }

    public String CreateToken(String subject)
    {
        var handler = new JwtSecurityTokenHandler();

        var token = new JwtSecurityToken(
            issuer: MailMuleEndpoints.Backend,
            audience: MailMuleEndpoints.WebApi,
            claims: [new Claim("sub", subject)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: _creds);

        return handler.WriteToken(token);
    }
}

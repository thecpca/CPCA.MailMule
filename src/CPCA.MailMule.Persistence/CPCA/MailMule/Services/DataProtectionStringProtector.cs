using Microsoft.AspNetCore.DataProtection;

namespace CPCA.MailMule;

internal sealed class DataProtectionStringProtector(IDataProtectionProvider provider) : IStringProtector
{
    private static readonly String Purpose = "MailMule.StringProtection.v1";

    private readonly IDataProtector protector = provider.CreateProtector(Purpose);

    public String Protect(String plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);

        return protector.Protect(plainText);
    }

    public String Unprotect(String cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        return protector.Unprotect(cipherText);
    }
}
